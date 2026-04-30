using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace NoiseFacadeGH
{
    static class Icons
    {
        static Bitmap _icon24, _icon48;

        public static Bitmap Icon24 => _icon24 ?? (_icon24 = Load("NoiseFacadeGH.NoiseFacade_24.png"));
        public static Bitmap Icon48 => _icon48 ?? (_icon48 = Load("NoiseFacadeGH.NoiseFacade_48.png"));

        static Bitmap Load(string resource)
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                    return s != null ? new Bitmap(s) : null;
            }
            catch { return null; }
        }
    }

    public class NoiseFacadeInfo : GH_AssemblyInfo
    {
        public override string Name          => "NoiseFacade GH";
        public override string Description   => "Acoustic noise heat-map on architectural geometry — direct + reflected, Galapagos-ready";
        public override Guid   Id            => new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
        public override string AuthorName    => "NoiseFacade";
        public override string AuthorContact => "https://github.com/LCS3002/NoiseFacadeGH";
        public override Bitmap Icon          => Icons.Icon48;
    }

    public class NoiseFacadeComponent : GH_Component
    {
        // volatile so the Draw thread always sees the latest reference
        private volatile Mesh _displayMesh;
        private volatile Mesh _reflectedDisplayMesh;

        public NoiseFacadeComponent()
            : base("NoiseFacade", "NFacade",
                   "Acoustic noise heat-map on facade geometry.\n" +
                   "Direct: inverse-square law + Lambert cosine, multi-source energy summation.\n" +
                   "Optional: first-order reflections + interior exposure score for Galapagos optimisation.",
                   "Analysis", "Acoustic")
        { }

        public override Guid ComponentGuid => new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901");

        protected override Bitmap Icon => Icons.Icon24;

        // ---- inputs / outputs -----------------------------------------------

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGeometryParameter("Geometry",    "G",   "Facade geometry – Mesh, Surface, Brep, SubD or Extrusion",                    GH_ParamAccess.item);
            p.AddPointParameter  ("Sources",      "S",   "Noise source points (one per dB level)",                                      GH_ParamAccess.list);
            p.AddNumberParameter ("dB Levels",    "dB",  "Sound power level at each source in dB SPL",                                  GH_ParamAccess.list);
            p.AddIntegerParameter("Quality",      "Q",   "Mesh quality: 0 = fast, 1 = default, 2 = analysis, 3 = fine",                 GH_ParamAccess.item, 1);
            p.AddNumberParameter ("Min dB",       "Min", "Lower bound of colour scale (auto if omitted)",                                GH_ParamAccess.item);
            p.AddNumberParameter ("Max dB",       "Max", "Upper bound of colour scale (auto if omitted)",                                GH_ParamAccess.item);
            p.AddPointParameter  ("Interior Pt",  "IP",  "Point inside the building — activates interior exposure score for Galapagos",  GH_ParamAccess.item);
            p.AddBooleanParameter("Reflections",  "R",   "Enable first-order acoustic reflections (may be slower on dense meshes)",      GH_ParamAccess.item, false);
            p.AddNumberParameter ("Absorption",   "α",   "Reflection energy absorption per bounce in dB (default 3 dB)",                 GH_ParamAccess.item, 3.0);
            p[3].Optional = true;
            p[4].Optional = true;
            p[5].Optional = true;
            p[6].Optional = true;
            p[7].Optional = true;
            p[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddMeshParameter  ("Mesh",          "M",     "Vertex-coloured mesh (direct + reflected energy)",                              GH_ParamAccess.item);
            p.AddNumberParameter("Face dB",       "dB",    "Per-face dB values (list)",                                                    GH_ParamAccess.list);
            p.AddNumberParameter("Min dB",        "Min",   "Actual scale minimum (dB)",                                                    GH_ParamAccess.item);
            p.AddNumberParameter("Max dB",        "Max",   "Actual scale maximum (dB)",                                                    GH_ParamAccess.item);
            p.AddNumberParameter("Interior dB",   "IntdB", "Interior noise exposure score in dB — connect to Galapagos fitness (minimise)", GH_ParamAccess.item);
            p.AddMeshParameter  ("Reflected Mesh","RM",    "False-colour mesh showing first-order reflection hotspots",                     GH_ParamAccess.item);
        }

        // ---- solve ----------------------------------------------------------

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _displayMesh         = null;
            _reflectedDisplayMesh = null;

            GeometryBase geo       = null;
            var sources            = new List<Point3d>();
            var levels             = new List<double>();
            int quality            = 1;
            double userMin         = double.NaN, userMax = double.NaN;
            Point3d interiorPt     = Point3d.Unset;
            bool enableReflections = false;
            double absorption      = 3.0;

            if (!DA.GetData(0, ref geo) || geo == null)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No geometry"); return; }

            if (!DA.GetDataList(1, sources) || sources.Count == 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No source points"); return; }

            if (!DA.GetDataList(2, levels) || levels.Count == 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No dB levels"); return; }

            DA.GetData(3, ref quality);
            quality = Math.Max(0, Math.Min(3, quality));

            bool hasMin      = DA.GetData(4, ref userMin);
            bool hasMax      = DA.GetData(5, ref userMax);
            bool hasInterior = DA.GetData(6, ref interiorPt);
            DA.GetData(7, ref enableReflections);
            DA.GetData(8, ref absorption);

            // pad levels list to match sources
            while (levels.Count < sources.Count)
                levels.Add(levels[levels.Count - 1]);

            Mesh mesh = ConvertToMesh(geo, quality);
            if (mesh == null)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not convert geometry to mesh"); return; }

            if (mesh.Faces.Count == 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh has no faces"); return; }

            mesh.FaceNormals.ComputeFaceNormals();
            mesh.Normals.ComputeNormals();

            // ---- direct acoustic model ----
            int fc = mesh.Faces.Count;
            var directDb = new double[fc];
            for (int fi = 0; fi < fc; fi++)
            {
                directDb[fi] = ComputeFaceDb(
                    FaceCentroid(mesh, mesh.Faces[fi]),
                    FaceNormal(mesh, fi),
                    sources, levels);
            }

            // ---- first-order reflections ----
            double[] reflectedDb = null;
            if (enableReflections)
            {
                if (fc > 4000)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"Reflections: {fc} faces — computation may take a moment. Reduce Quality if slow.");

                reflectedDb = ComputeReflections(mesh, sources, levels, absorption);

                // merge reflected energy into the total
                for (int fi = 0; fi < fc; fi++)
                {
                    double direct   = Math.Pow(10.0, directDb[fi] / 10.0);
                    double reflected = reflectedDb[fi] > -100.0
                        ? Math.Pow(10.0, reflectedDb[fi] / 10.0)
                        : 0.0;
                    directDb[fi] = 10.0 * Math.Log10(direct + reflected);
                }
            }

            // reuse directDb as totalDb from here on
            var faceDb = directDb;

            double scaleMin = (hasMin && !double.IsNaN(userMin)) ? userMin : faceDb.Min();
            double scaleMax = (hasMax && !double.IsNaN(userMax)) ? userMax : faceDb.Max();
            if (Math.Abs(scaleMax - scaleMin) < 1e-6) scaleMax = scaleMin + 1.0;

            PaintVertices(mesh, faceDb, scaleMin, scaleMax);
            _displayMesh = BuildDisplayMesh(mesh);

            DA.SetData    (0, mesh);
            DA.SetDataList(1, faceDb);
            DA.SetData    (2, scaleMin);
            DA.SetData    (3, scaleMax);

            // ---- interior exposure score ----
            if (hasInterior && interiorPt != Point3d.Unset)
            {
                double interiorScore = ComputeInteriorScore(mesh, faceDb, interiorPt);
                DA.SetData(4, interiorScore);
            }

            // ---- reflected hotspot mesh ----
            if (reflectedDb != null)
            {
                var reflMesh  = mesh.DuplicateMesh();
                var validRefl = reflectedDb.Where(d => d > -100.0).ToArray();
                double rMin   = validRefl.Length > 0 ? validRefl.Min() : -20.0;
                double rMax   = validRefl.Length > 0 ? validRefl.Max() :   0.0;
                if (Math.Abs(rMax - rMin) < 1e-6) rMax = rMin + 1.0;
                PaintVertices(reflMesh, reflectedDb, rMin, rMax);
                _reflectedDisplayMesh = BuildDisplayMesh(reflMesh);
                DA.SetData(5, reflMesh);
            }
        }

        // ---- viewport preview -----------------------------------------------

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            var m = _displayMesh;
            if (m != null && m.VertexColors.Count > 0)
                args.Display.DrawMeshFalseColors(m);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            var m = _displayMesh;
            if (m != null)
                args.Display.DrawMeshWires(m, args.WireColour);
        }

        // push every vertex outward along its normal by 0.1% of bounding-box diagonal
        static Mesh BuildDisplayMesh(Mesh src)
        {
            var m      = src.DuplicateMesh();
            double off = src.GetBoundingBox(false).Diagonal.Length * 0.001;
            if (off < 1e-6) off = 1e-6;
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                var n = m.Normals[i];
                var v = (Point3d)m.Vertices[i];
                v += new Vector3d(n.X, n.Y, n.Z) * off;
                m.Vertices[i] = new Point3f((float)v.X, (float)v.Y, (float)v.Z);
            }
            return m;
        }

        // ---- geometry conversion --------------------------------------------

        static Mesh ConvertToMesh(GeometryBase geo, int quality)
        {
            var mp = quality == 0 ? MeshingParameters.FastRenderMesh
                   : quality == 1 ? MeshingParameters.Default
                   : quality == 2 ? MeshingParameters.DefaultAnalysisMesh
                                  : MeshingParameters.QualityRenderMesh;

            if (geo is Mesh directMesh)
                return directMesh.DuplicateMesh();

            Brep brep = null;

            if      (geo is Surface  srf)  brep = srf.ToBrep();
            else if (geo is Brep      b)   brep = b;
            else if (geo is Extrusion ext) brep = ext.ToBrep(false);
            else if (geo is SubD subd)
            {
                try   { brep = subd.ToBrep(new SubDToBrepOptions()); }
                catch
                {
                    var sm = Mesh.CreateFromSubD(subd, 3);
                    if (sm != null && sm.Faces.Count > 0) return sm;
                }
            }

            if (brep != null)
            {
                var arr = Mesh.CreateFromBrep(brep, mp);
                if (arr != null && arr.Length > 0)
                {
                    var combined = new Mesh();
                    foreach (var m in arr) combined.Append(m);
                    return combined;
                }
            }
            return null;
        }

        // ---- direct acoustic model ------------------------------------------

        // L = L_src − 20·log10(d) − 11 + 10·log10(cosθ + 0.01)
        // multi-source: 10·log10(Σ 10^(Li/10))
        static double ComputeFaceDb(Point3d centroid, Vector3d normal,
                                    List<Point3d> sources, List<double> levels)
        {
            double energySum = 0.0;
            for (int i = 0; i < sources.Count; i++)
            {
                var dir = sources[i] - centroid;
                double d = Math.Max(dir.Length, 0.1);
                dir.Unitize();
                double cosTheta = Math.Max(Vector3d.Multiply(normal, dir), 0.0);
                double L = levels[i]
                           - 20.0 * Math.Log10(d)
                           - 11.0
                           + 10.0 * Math.Log10(cosTheta + 0.01);
                energySum += Math.Pow(10.0, L / 10.0);
            }
            return energySum > 0.0 ? 10.0 * Math.Log10(energySum) : -200.0;
        }

        // ---- first-order reflections ----------------------------------------

        // For each face, cast the reflected ray for every source.
        // The energy lands on the face that the reflected ray hits.
        // L_ref = L_src − 20·log10(d1+d2) − 11 + 10·log10(cosθ_i + 0.01) − α
        static double[] ComputeReflections(Mesh mesh, List<Point3d> sources,
                                           List<double> levels, double absorptionDb)
        {
            int fc = mesh.Faces.Count;
            var reflEnergy = new double[fc];

            for (int si = 0; si < sources.Count; si++)
            {
                for (int fi = 0; fi < fc; fi++)
                {
                    Point3d  centroid = FaceCentroid(mesh, mesh.Faces[fi]);
                    Vector3d normal   = FaceNormal(mesh, fi);

                    // direction from source to this (reflecting) face
                    Vector3d toFace = centroid - sources[si];
                    double d1 = Math.Max(toFace.Length, 0.1);
                    toFace.Unitize();

                    // cosine of incidence at the reflecting face
                    double cosTheta = Math.Max(-Vector3d.Multiply(toFace, normal), 0.0);
                    if (cosTheta < 0.01) continue; // back-face: skip

                    // mirror toFace around normal: reflDir = toFace − 2(toFace·n)n
                    double dot     = Vector3d.Multiply(toFace, normal);
                    Vector3d reflDir = toFace - 2.0 * dot * normal;
                    reflDir.Unitize();

                    // offset origin slightly to avoid self-intersection
                    double eps    = Math.Max(d1 * 1e-4, 0.005);
                    Point3d origin = centroid + normal * eps;

                    double tHit = Intersection.MeshRay(mesh, new Ray3d(origin, reflDir));
                    if (double.IsNaN(tHit) || double.IsInfinity(tHit) || tHit < 1e-3) continue;

                    double d2        = Math.Max(tHit, 0.01);
                    double totalDist = Math.Max(d1 + d2, 0.1);

                    double L_ref = levels[si]
                        - 20.0 * Math.Log10(totalDist)
                        - 11.0
                        + 10.0 * Math.Log10(cosTheta + 0.01)
                        - absorptionDb;

                    // use mesh's spatial index to find the hit face
                    Point3d hitPt = origin + reflDir * tHit;
                    var mp = mesh.ClosestMeshPoint(hitPt, 1e10);
                    int hitFi = mp != null ? mp.FaceIndex : -1;
                    if (hitFi >= 0 && hitFi != fi)
                        reflEnergy[hitFi] += Math.Pow(10.0, L_ref / 10.0);
                }
            }

            var result = new double[fc];
            for (int fi = 0; fi < fc; fi++)
                result[fi] = reflEnergy[fi] > 0.0 ? 10.0 * Math.Log10(reflEnergy[fi]) : -200.0;
            return result;
        }

        // ---- interior exposure score ----------------------------------------

        // Each face acts as a secondary acoustic source toward the interior.
        // Energy at the interior point: Σ [ 10^(dBi/10) × area_i / dist_i² ]
        // Gives a single dB scalar suitable as a Galapagos fitness (minimise).
        static double ComputeInteriorScore(Mesh mesh, double[] faceDb, Point3d interiorPt)
        {
            double energySum = 0.0;
            for (int fi = 0; fi < mesh.Faces.Count; fi++)
            {
                Point3d centroid = FaceCentroid(mesh, mesh.Faces[fi]);
                double  area     = FaceArea(mesh, mesh.Faces[fi]);
                double  dist     = Math.Max(centroid.DistanceTo(interiorPt), 0.1);
                // inverse-square contribution weighted by face area
                energySum += Math.Pow(10.0, faceDb[fi] / 10.0) * area / (dist * dist);
            }
            return energySum > 0.0 ? 10.0 * Math.Log10(energySum) : -200.0;
        }

        // ---- geometry helpers -----------------------------------------------

        static Point3d FaceCentroid(Mesh mesh, MeshFace f)
        {
            var vA = (Point3d)mesh.Vertices[f.A];
            var vB = (Point3d)mesh.Vertices[f.B];
            var vC = (Point3d)mesh.Vertices[f.C];
            if (f.IsQuad)
            {
                var vD = (Point3d)mesh.Vertices[f.D];
                return new Point3d(
                    (vA.X + vB.X + vC.X + vD.X) * 0.25,
                    (vA.Y + vB.Y + vC.Y + vD.Y) * 0.25,
                    (vA.Z + vB.Z + vC.Z + vD.Z) * 0.25);
            }
            return new Point3d(
                (vA.X + vB.X + vC.X) / 3.0,
                (vA.Y + vB.Y + vC.Y) / 3.0,
                (vA.Z + vB.Z + vC.Z) / 3.0);
        }

        static Vector3d FaceNormal(Mesh mesh, int fi)
        {
            var nf = mesh.FaceNormals[fi];
            var n  = new Vector3d(nf.X, nf.Y, nf.Z);
            n.Unitize();
            return n;
        }

        static double FaceArea(Mesh mesh, MeshFace f)
        {
            var A = (Point3d)mesh.Vertices[f.A];
            var B = (Point3d)mesh.Vertices[f.B];
            var C = (Point3d)mesh.Vertices[f.C];
            double area = Vector3d.CrossProduct(B - A, C - A).Length * 0.5;
            if (f.IsQuad)
            {
                var D = (Point3d)mesh.Vertices[f.D];
                area += Vector3d.CrossProduct(C - A, D - A).Length * 0.5;
            }
            return area;
        }

        // ---- colour ---------------------------------------------------------

        // gradient: blue → cyan → yellow → orange → red
        static readonly double[] StopT = { 0.00, 0.25, 0.50, 0.75, 1.00 };
        static readonly int[]    StopR = {    0,    0,  255,  255,  255 };
        static readonly int[]    StopG = {    0,  255,  255,  128,    0 };
        static readonly int[]    StopB = {  255,  255,    0,    0,    0 };

        static Color DbToColor(double db, double minDb, double maxDb)
        {
            double t = (db - minDb) / (maxDb - minDb);
            t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
            for (int i = 0; i < StopT.Length - 1; i++)
            {
                if (t <= StopT[i + 1])
                {
                    double seg = (t - StopT[i]) / (StopT[i + 1] - StopT[i]);
                    return Color.FromArgb(
                        (int)(StopR[i] + seg * (StopR[i + 1] - StopR[i])),
                        (int)(StopG[i] + seg * (StopG[i + 1] - StopG[i])),
                        (int)(StopB[i] + seg * (StopB[i + 1] - StopB[i])));
                }
            }
            return Color.FromArgb(255, 0, 0);
        }

        // per-vertex dB = average of incident face dBs for smooth gradient
        static void PaintVertices(Mesh mesh, double[] faceDb, double scaleMin, double scaleMax)
        {
            int vc    = mesh.Vertices.Count;
            var sum   = new double[vc];
            var cnt   = new int[vc];

            for (int fi = 0; fi < mesh.Faces.Count; fi++)
            {
                var    f  = mesh.Faces[fi];
                double db = faceDb[fi];
                sum[f.A] += db; cnt[f.A]++;
                sum[f.B] += db; cnt[f.B]++;
                sum[f.C] += db; cnt[f.C]++;
                if (f.IsQuad) { sum[f.D] += db; cnt[f.D]++; }
            }

            var colors = new Color[vc];
            for (int vi = 0; vi < vc; vi++)
            {
                double avg = cnt[vi] > 0 ? sum[vi] / cnt[vi] : scaleMin;
                colors[vi] = DbToColor(avg, scaleMin, scaleMax);
            }
            mesh.VertexColors.SetColors(colors);
        }
    }
}

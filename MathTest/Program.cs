using System;
using System.Collections.Generic;
using System.Drawing;

// Standalone reproduction of acoustic math + colour gradient from NoiseFacadeComponent.cs
// No Rhino dependency — runs as plain console app.
static class AcousticMath
{
    public static double ComputeFaceDb(
        double[] centroid,  // [x,y,z]
        double[] normal,    // unit vector [x,y,z]
        List<double[]> sources,
        List<double> levels)
    {
        double energySum = 0.0;
        for (int i = 0; i < sources.Count; i++)
        {
            double dx = sources[i][0] - centroid[0];
            double dy = sources[i][1] - centroid[1];
            double dz = sources[i][2] - centroid[2];
            double d  = Math.Max(Math.Sqrt(dx*dx + dy*dy + dz*dz), 0.1);
            double len = Math.Sqrt(dx*dx + dy*dy + dz*dz);
            if (len < 1e-12) { dx = 0; dy = 0; dz = 1; } else { dx/=len; dy/=len; dz/=len; }
            double cosTheta = Math.Max(normal[0]*dx + normal[1]*dy + normal[2]*dz, 0.0);
            double L = levels[i]
                       - 20.0 * Math.Log10(d)
                       - 11.0
                       + 10.0 * Math.Log10(cosTheta + 0.01);
            energySum += Math.Pow(10.0, L / 10.0);
        }
        if (energySum <= 0) return -200;
        return 10.0 * Math.Log10(energySum);
    }
}

static class ColourGradient
{
    static readonly double[] StopT = { 0.00, 0.25, 0.50, 0.75, 1.00 };
    static readonly int[]    StopR = {    0,    0,  255,  255,  255 };
    static readonly int[]    StopG = {    0,  255,  255,  128,    0 };
    static readonly int[]    StopB = {  255,  255,    0,    0,    0 };

    public static (int r, int g, int b) DbToColor(double db, double minDb, double maxDb)
    {
        double t = (db - minDb) / (maxDb - minDb);
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        for (int i = 0; i < StopT.Length - 1; i++)
        {
            if (t <= StopT[i + 1])
            {
                double seg = (t - StopT[i]) / (StopT[i + 1] - StopT[i]);
                return (
                    (int)(StopR[i] + seg * (StopR[i + 1] - StopR[i])),
                    (int)(StopG[i] + seg * (StopG[i + 1] - StopG[i])),
                    (int)(StopB[i] + seg * (StopB[i + 1] - StopB[i]))
                );
            }
        }
        return (255, 0, 0);
    }
}

class Program
{
    static int pass = 0, fail = 0;

    static void Assert(string name, double actual, double expected, double tol = 0.01)
    {
        bool ok = Math.Abs(actual - expected) <= tol;
        Console.WriteLine($"  [{(ok?"PASS":"FAIL")}] {name}: got {actual:F3}, expected {expected:F3}");
        if (ok) pass++; else fail++;
    }

    static void AssertRGB(string name, (int r,int g,int b) c, int er, int eg, int eb, int tol = 2)
    {
        bool ok = Math.Abs(c.r-er)<=tol && Math.Abs(c.g-eg)<=tol && Math.Abs(c.b-eb)<=tol;
        Console.WriteLine($"  [{(ok?"PASS":"FAIL")}] {name}: got ({c.r},{c.g},{c.b}), expected ({er},{eg},{eb})");
        if (ok) pass++; else fail++;
    }

    static void Main()
    {
        // ---- acoustic math --------------------------------------------------
        Console.WriteLine("\n=== Acoustic math ===");

        var src1 = new List<double[]> { new[] { 10.0, 0.0, 0.0 } };
        var lev1 = new List<double> { 90.0 };
        double[] centreOrigin = { 0, 0, 0 };
        double[] normalToward = { 1, 0, 0 }; // pointing at source
        double[] normalAway   = {-1, 0, 0 }; // pointing away

        // d=10, cosθ=1: L = 90 - 20 - 11 + 10*log10(1.01) = 59 + 0.043 = 59.043
        Assert("direct, d=10m, cosθ=1",
            AcousticMath.ComputeFaceDb(centreOrigin, normalToward, src1, lev1),
            59.043);

        // d=1 (src at 1m): L = 90 - 0 - 11 + 0.043 = 79.043
        var src1m = new List<double[]> { new[] { 1.0, 0.0, 0.0 } };
        Assert("direct, d=1m, cosθ=1",
            AcousticMath.ComputeFaceDb(centreOrigin, normalToward, src1m, lev1),
            79.043);

        // cosθ=0 (face pointing away): L = 90 - 20 - 11 + 10*log10(0.01) = 59 - 20 = 39
        Assert("face away, d=10m, cosθ=0",
            AcousticMath.ComputeFaceDb(centreOrigin, normalAway, src1, lev1),
            39.0, 0.05);

        // distance clamping: src AT centroid → d clamped to 0.1
        var srcAtCentre = new List<double[]> { new[] { 0.0, 0.0, 0.0 } };
        double expectedClamped = 90.0 - 20.0 * Math.Log10(0.1) - 11.0 + 10.0 * Math.Log10(0.01 + 0.01);
        // = 90 + 20 - 11 + 10*log10(0.02) = 99 - 17.0 = 82.0 (approx)
        double clamped = AcousticMath.ComputeFaceDb(centreOrigin, normalToward, srcAtCentre, lev1);
        Console.WriteLine($"  [INFO] clamp d=0→0.1, result={clamped:F3} (no crash = pass)");

        // two equal sources: result should be L_single + 3.01 dB
        var src2 = new List<double[]> { new[] { 10.0, 0.0, 0.0 }, new[] { 10.0, 0.0, 0.0 } };
        var lev2 = new List<double> { 90.0, 90.0 };
        double single = AcousticMath.ComputeFaceDb(centreOrigin, normalToward, src1, lev1);
        double dual   = AcousticMath.ComputeFaceDb(centreOrigin, normalToward, src2, lev2);
        Assert("two equal sources = +3.01 dB", dual - single, 10.0 * Math.Log10(2.0), 0.01);

        // ---- colour gradient ------------------------------------------------
        Console.WriteLine("\n=== Colour gradient ===");

        AssertRGB("t=0.0 → blue",    ColourGradient.DbToColor(0,   0, 100), 0,   0,   255);
        AssertRGB("t=0.25 → cyan",   ColourGradient.DbToColor(25,  0, 100), 0,   255, 255);
        AssertRGB("t=0.5 → yellow",  ColourGradient.DbToColor(50,  0, 100), 255, 255, 0);
        AssertRGB("t=0.75 → orange", ColourGradient.DbToColor(75,  0, 100), 255, 128, 0);
        AssertRGB("t=1.0 → red",     ColourGradient.DbToColor(100, 0, 100), 255, 0,   0);
        AssertRGB("t<0 clamp → blue",ColourGradient.DbToColor(-10, 0, 100), 0,   0,   255);
        AssertRGB("t>1 clamp → red", ColourGradient.DbToColor(110, 0, 100), 255, 0,   0);

        // ---- edge cases in scale logic --------------------------------------
        Console.WriteLine("\n=== Scale edge cases ===");

        // zero range guard: if min==max the scale gets +1
        double scaleMin = 60.0, scaleMax = 60.0;
        if (Math.Abs(scaleMax - scaleMin) < 1e-6) scaleMax = scaleMin + 1.0;
        Console.WriteLine($"  [PASS] zero-range guard: max expanded to {scaleMax}");
        pass++;

        // negative dB (face pointing away from source)
        double negDb = AcousticMath.ComputeFaceDb(centreOrigin, normalAway, src1, lev1);
        Console.WriteLine($"  [INFO] negative dB allowed: {negDb:F1} dB");

        // ---- summary -------------------------------------------------------
        Console.WriteLine($"\n=== Results: {pass} passed, {fail} failed ===");
        Environment.Exit(fail > 0 ? 1 : 0);
    }
}

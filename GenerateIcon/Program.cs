using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Manta — icon generator.
// Assembly icons (manta ray, teal): Manta_24.png, Manta_48.png
// BT acoustic icons (amber on navy): BatSource, BatMesh, BatNoise, BatInterior, BatContours, BatLegend
// MN environment icons (teal on navy): MantaWind, MantaSun, MantaPressure
// Logo (512px): Manta_logo.png
class Program
{
    // ── Palettes ──────────────────────────────────────────────────────────────
    static readonly Color Navy     = Color.FromArgb(  8,  12,  28);
    static readonly Color Amber    = Color.FromArgb(245, 166,  35);
    static readonly Color Teal     = Color.FromArgb(  0, 210, 180);
    static readonly Color Cyan     = Color.FromArgb( 60, 220, 255);
    static readonly Color Wht      = Color.White;

    // Acoustic gradient (blue → cyan → yellow → orange → red)
    static readonly (double t, Color c)[] Stops = {
        (0.00, Color.FromArgb(  0,   0, 255)),
        (0.25, Color.FromArgb(  0, 220, 255)),
        (0.50, Color.FromArgb(255, 240,   0)),
        (0.75, Color.FromArgb(255, 110,   0)),
        (1.00, Color.FromArgb(255,   0,   0)),
    };

    static Color AcousticGrad(double t)
    {
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            if (t <= Stops[i + 1].t)
            {
                double s = (t - Stops[i].t) / (Stops[i + 1].t - Stops[i].t);
                var c0 = Stops[i].c; var c1 = Stops[i + 1].c;
                return Color.FromArgb(
                    Cl(c0.R + (int)(s * (c1.R - c0.R))),
                    Cl(c0.G + (int)(s * (c1.G - c0.G))),
                    Cl(c0.B + (int)(s * (c1.B - c0.B))));
            }
        }
        return Stops[Stops.Length - 1].c;
    }

    static int Cl(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // ── Canvas helpers ────────────────────────────────────────────────────────
    static Bitmap Canvas(int sz, Action<Graphics, float> draw)
    {
        var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Navy);
            draw(g, sz);
        }
        return bmp;
    }

    static PointF[] Sc(float sc, PointF[] pts)
    {
        var r = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            r[i] = new PointF(pts[i].X * sc, pts[i].Y * sc);
        return r;
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X,          r.Y,          d, d, 180, 90);
        p.AddArc(r.Right - d,  r.Y,          d, d, 270, 90);
        p.AddArc(r.Right - d,  r.Bottom - d, d, d,   0, 90);
        p.AddArc(r.X,          r.Bottom - d, d, d,  90, 90);
        p.CloseFigure();
        return p;
    }

    // ── Assembly icon: manta ray silhouette (teal) ────────────────────────────
    static Bitmap DrawManta(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;
            using (var b = new SolidBrush(Teal))
            {
                // Main wing shape — viewed from above
                g.FillPolygon(b, Sc(sc, new PointF[]
                {
                    new PointF(12,  3),   // front tip
                    new PointF(22, 10),   // right wingtip
                    new PointF(19, 14),   // right trailing edge
                    new PointF(14, 19),   // right tail base
                    new PointF(12, 22),   // tail tip
                    new PointF(10, 19),   // left tail base
                    new PointF( 5, 14),   // left trailing edge
                    new PointF( 2, 10),   // left wingtip
                }));
                // Left cephalic fin
                g.FillPolygon(b, Sc(sc, new PointF[]
                    { new PointF(10, 3), new PointF(9, 7), new PointF(11, 8) }));
                // Right cephalic fin
                g.FillPolygon(b, Sc(sc, new PointF[]
                    { new PointF(14, 3), new PointF(15, 7), new PointF(13, 8) }));
            }
            // Body oval — subtle darker overlay
            using (var b = new SolidBrush(Color.FromArgb(80, 0, 80, 70)))
                g.FillEllipse(b, 9f*sc, 8f*sc, 6f*sc, 8f*sc);
            // Bright eye dot
            float er = sc * 0.7f;
            using (var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                g.FillEllipse(b, 12f*sc - er, 10f*sc - er, er*2, er*2);
        });
    }

    // ── MN Wind: curl-noise streamlines + arrow ───────────────────────────────
    static Bitmap DrawWind(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;

            // Three wavy streamlines
            float[] baseY  = { 7f, 12f, 17f };
            float[] phases = { 0f, 0.4f, 0.8f };
            using (var pen = new Pen(Teal, sc * 1.1f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int li = 0; li < 3; li++)
                {
                    var pts = new List<PointF>();
                    for (int i = 0; i <= 18; i++)
                    {
                        float t  = (float)i / 18f;
                        float x  = (2f + t * 15f) * sc;
                        float y  = baseY[li] * sc
                                 + (float)Math.Sin((t + phases[li]) * Math.PI * 2.2f) * sc * 0.9f;
                        pts.Add(new PointF(x, y));
                    }
                    g.DrawLines(pen, pts.ToArray());
                }
            }

            // Arrow head pointing right
            float aY = 12f * sc;
            using (var b = new SolidBrush(Cyan))
                g.FillPolygon(b, new PointF[]
                {
                    new PointF(22f * sc, aY),
                    new PointF(16f * sc, aY - 2.8f * sc),
                    new PointF(16f * sc, aY + 2.8f * sc),
                });
        });
    }

    // ── MN Sun: sun disc + radiating rays ─────────────────────────────────────
    static Bitmap DrawSun(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc    = s / 24f;
            float cx    = 12f * sc, cy = 12f * sc;
            float coreR = 3.4f * sc;
            float rayR  = 7.5f * sc;

            // Glow halo
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - coreR*2.2f, cy - coreR*2.2f, coreR*4.4f, coreR*4.4f);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor    = Color.FromArgb(70, Teal);
                    pgb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb, path);
                }
            }

            // 8 rays
            using (var pen = new Pen(Teal, sc * 1.1f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    g.DrawLine(pen,
                        cx + (float)Math.Cos(angle) * (coreR + sc * 0.8f),
                        cy + (float)Math.Sin(angle) * (coreR + sc * 0.8f),
                        cx + (float)Math.Cos(angle) * rayR,
                        cy + (float)Math.Sin(angle) * rayR);
                }
            }

            // Sun disc
            using (var b = new SolidBrush(Cyan))
                g.FillEllipse(b, cx - coreR, cy - coreR, coreR*2, coreR*2);
            // Bright core
            float cr2 = coreR * 0.45f;
            using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                g.FillEllipse(b, cx - cr2, cy - cr2, cr2*2, cr2*2);
        });
    }

    // ── MN Pressure: source dot + expanding arcs ──────────────────────────────
    static Bitmap DrawPressure(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;
            float cx = 4.5f * sc, cy = 12f * sc;

            // Source glow
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - sc*3f, cy - sc*3f, sc*6f, sc*6f);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor    = Color.FromArgb(90, Cyan);
                    pgb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb, path);
                }
            }
            using (var b = new SolidBrush(Teal))
                g.FillEllipse(b, cx - sc, cy - sc, sc*2, sc*2);
            using (var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                g.FillEllipse(b, cx - sc*0.45f, cy - sc*0.45f, sc*0.9f, sc*0.9f);

            // Expanding arcs
            for (int i = 1; i <= 4; i++)
            {
                float r     = sc * (1.8f + i * 3.0f);
                int   alpha = 210 - i * 40;
                float thick = sc * (1.1f - i * 0.10f);
                if (cx + r > s * 1.1f) continue;
                using (var pen = new Pen(Color.FromArgb(alpha, Teal), Math.Max(thick, 0.5f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap   = LineCap.Round;
                    g.DrawArc(pen, cx - r, cy - r, r*2, r*2, -65, 130);
                }
            }
        });
    }

    // ── BT Source: speaker cone + sound waves ─────────────────────────────────
    static Bitmap DrawSource(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;
            using (var b = new SolidBrush(Amber))
            {
                g.FillRectangle(b, 3*sc, 9*sc, 4*sc, 6*sc);
                g.FillPolygon(b, Sc(sc, new PointF[]
                    { new PointF(7, 7), new PointF(12, 4), new PointF(12, 20), new PointF(7, 17) }));
            }
            for (int i = 1; i <= 3; i++)
            {
                int   alpha = 255 - i * 55;
                float r     = sc * (1.8f + i * 2.4f);
                using (var pen = new Pen(Color.FromArgb(alpha, Amber), sc * 1.1f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 12*sc - r, 12*sc - r, r*2, r*2, -55, 110);
                }
            }
        });
    }

    // ── BT Mesh: vertex-grid icon ─────────────────────────────────────────────
    static Bitmap DrawMesh(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc  = s / 24f;
            float pad = 2.5f * sc;
            float w   = s - pad * 2;
            int   n   = 4;
            using (var pen = new Pen(Color.FromArgb(120, Amber), sc * 0.7f))
            {
                for (int i = 0; i <= n; i++)
                {
                    float t = (float)i / n;
                    g.DrawLine(pen, pad + t*w, pad,   pad + t*w, pad + w);
                    g.DrawLine(pen, pad,   pad + t*w, pad + w,   pad + t*w);
                }
            }
            using (var b = new SolidBrush(Amber))
            for (int row = 0; row <= n; row++)
            for (int col = 0; col <= n; col++)
            {
                float x  = pad + (float)col / n * w;
                float y  = pad + (float)row / n * w;
                float dr = sc * 0.85f;
                g.FillEllipse(b, x - dr, y - dr, dr*2, dr*2);
            }
        });
    }

    // ── BT Noise: gradient heat-map bars ──────────────────────────────────────
    static Bitmap DrawNoise(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc   = s / 24f;
            int   bars = 6;
            float pad  = 2 * sc;
            float bw   = (s - pad * 2 - (bars - 1) * sc * 0.4f) / bars;
            float bh   = s - pad * 2;
            float y0   = pad;
            for (int i = 0; i < bars; i++)
            {
                double t  = (double)i / (bars - 1);
                float  x0 = pad + i * (bw + sc * 0.4f);
                using (var b = new SolidBrush(AcousticGrad(t)))
                    g.FillRectangle(b, x0, y0, bw, bh);
                using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                    g.FillRectangle(b, x0, y0, bw, bh * 0.25f);
            }
        });
    }

    // ── BT Interior: room + rays + interior point ─────────────────────────────
    static Bitmap DrawInterior(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc  = s / 24f;
            float pad = 3 * sc;
            float w   = s - pad * 2;
            using (var pen = new Pen(Amber, sc * 0.9f))
                g.DrawRectangle(pen, pad, pad, w, w);
            float cx = s * 0.5f, cy = s * 0.52f;
            using (var pen = new Pen(Color.FromArgb(140, Amber), sc * 0.6f))
            {
                g.DrawLine(pen, cx, cy, cx,     pad);
                g.DrawLine(pen, cx, cy, cx,     pad + w);
                g.DrawLine(pen, cx, cy, pad,     cy);
                g.DrawLine(pen, cx, cy, pad + w, cy);
                g.DrawLine(pen, cx, cy, pad,     pad);
                g.DrawLine(pen, cx, cy, pad + w, pad);
                g.DrawLine(pen, cx, cy, pad,     pad + w);
                g.DrawLine(pen, cx, cy, pad + w, pad + w);
            }
            float dr = sc * 1.3f;
            using (var b = new SolidBrush(Wht))
                g.FillEllipse(b, cx - dr, cy - dr, dr*2, dr*2);
            using (var b = new SolidBrush(Color.FromArgb(80, Wht)))
                g.FillEllipse(b, cx - dr*2, cy - dr*2, dr*4, dr*4);
        });
    }

    // ── BT Contours: concentric isodecibel lines ──────────────────────────────
    static Bitmap DrawContours(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;
            float cx = s * 0.5f, cy = s * 0.55f;
            for (int i = 4; i >= 1; i--)
            {
                double t  = (double)(i - 1) / 3.0;
                float  rx = i * 2.6f * sc;
                float  ry = i * 1.9f * sc;
                using (var pen = new Pen(AcousticGrad(t), sc * 1.0f))
                    g.DrawEllipse(pen, cx - rx, cy - ry, rx*2, ry*2);
            }
            float dr = sc * 1.2f;
            using (var b = new SolidBrush(Amber))
                g.FillEllipse(b, cx - dr, cy - dr, dr*2, dr*2);
        });
    }

    // ── BT Legend: vertical gradient bar + ticks ─────────────────────────────
    static Bitmap DrawLegend(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            float sc = s / 24f;
            float bx = 4*sc, by = 2*sc, bw = 5*sc, bh = s - 4*sc;
            int strips = sz * 2;
            for (int i = 0; i < strips; i++)
            {
                double t = 1.0 - (double)i / strips;
                float  y = by + (float)i / strips * bh;
                using (var b = new SolidBrush(AcousticGrad(t)))
                    g.FillRectangle(b, bx, y, bw, bh / strips + 1.5f);
            }
            using (var pen = new Pen(Color.FromArgb(80, Wht), sc * 0.5f))
                g.DrawRectangle(pen, bx, by, bw, bh);
            using (var pen = new Pen(Wht, sc * 0.7f))
            for (int i = 0; i < 5; i++)
            {
                float t = (float)i / 4;
                float y = by + t * bh;
                g.DrawLine(pen, bx + bw, y, bx + bw + 3*sc, y);
            }
        });
    }

    // ── Logo (512 px) — for README ────────────────────────────────────────────
    static Bitmap DrawLogo(int sz)
    {
        return Canvas(sz, (g, s) =>
        {
            // Background gradient
            using (var brush = new LinearGradientBrush(
                new PointF(0, 0), new PointF(s*0.3f, s),
                Color.FromArgb(6, 9, 22), Color.FromArgb(14, 24, 52)))
                g.FillRectangle(brush, 0, 0, s, s);

            // Noise texture
            var rng = new Random(42);
            for (int i = 0; i < 300; i++)
            {
                float x = (float)(rng.NextDouble() * s);
                float y = (float)(rng.NextDouble() * s);
                int   a = rng.Next(3, 14);
                g.FillRectangle(new SolidBrush(Color.FromArgb(a, 255, 255, 255)), x, y, 1, 1);
            }

            // Facade panel grid
            int   cols = 7, rows = 5;
            float fL = s*0.04f, fR = s*0.96f, fT = s*0.05f, fB = s*0.62f;
            float fW = fR - fL, fH = fB - fT;
            float gapX = fW*0.009f, gapY = fH*0.018f;
            float pw   = (fW - gapX*(cols-1))/cols;
            float ph   = (fH - gapY*(rows-1))/rows;
            float shear = fH*0.06f;

            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                // Blend acoustic and teal palette across the facade
                double tAc = (double)col / (cols - 1);
                Color  fc  = col < cols/2
                    ? AcousticGrad(tAc * 0.5)
                    : Color.FromArgb(
                        Cl((int)(0   + tAc * 0)),
                        Cl((int)(180 + tAc * 30)),
                        Cl((int)(160 + tAc * 20)));
                double br = 0.55 + 0.45*(1.0 - (double)row/rows);
                fc = Color.FromArgb(Cl((int)(fc.R*br)), Cl((int)(fc.G*br)), Cl((int)(fc.B*br)));
                float x0 = fL + col*(pw+gapX);
                float y0 = fT + row*(ph+gapY) + (float)col/(cols-1)*shear;
                using (var b = new SolidBrush(fc)) g.FillRectangle(b, x0, y0, pw, ph);
                using (var b = new LinearGradientBrush(
                    new PointF(x0, y0), new PointF(x0, y0+ph*0.3f),
                    Color.FromArgb(55,255,255,255), Color.Transparent))
                    g.FillRectangle(b, x0, y0, pw, ph*0.30f);
            }

            // Grid lines
            using (var pen = new Pen(Color.FromArgb(22, 210, 230, 255), s*0.002f))
            {
                for (int col = 0; col <= cols; col++)
                {
                    float x  = fL + col*(pw+gapX);
                    float sy = (float)col/cols*shear;
                    g.DrawLine(pen, x, fT+sy, x, fB+sy);
                }
                for (int row = 0; row <= rows; row++)
                {
                    float y = fT + row*(ph+gapY);
                    g.DrawLine(pen, fL, y, fR, y+shear);
                }
            }

            // Source — now teal
            float srcX = s*0.13f, srcY = s*0.36f;
            using (var pen = new Pen(Color.FromArgb(14, 0, 210, 180), s*0.002f))
            for (int i = 0; i <= 6; i++)
            {
                float targetY = fT + (fB - fT)*i/6f;
                g.DrawLine(pen, srcX, srcY, fL, targetY);
            }
            for (int i = 1; i <= 7; i++)
            {
                float rad   = s*(0.018f + i*0.028f);
                int   alpha = Math.Max(190 - i*23, 10);
                float thick = s*0.0055f*(8-i)/7f;
                using (var pen = new Pen(Color.FromArgb(alpha, 0, 210, 180), Math.Max(thick, 0.5f)))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, srcX-rad, srcY-rad, rad*2, rad*2, 175, 190);
                }
            }
            float glowR = s*0.028f;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(srcX-glowR*2.5f, srcY-glowR*2.5f, glowR*5, glowR*5);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor    = Color.FromArgb(80, 0, 210, 180);
                    pgb.SurroundColors = new[]{ Color.Transparent };
                    g.FillPath(pgb, path);
                }
            }
            using (var b = new SolidBrush(Color.FromArgb(255, 60, 220, 255)))
                g.FillEllipse(b, srcX-glowR, srcY-glowR, glowR*2, glowR*2);
            float coreR = glowR*0.52f;
            g.FillEllipse(Brushes.White, srcX-coreR, srcY-coreR, coreR*2, coreR*2);

            // Vignette
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-s*0.15f, -s*0.15f, s*1.30f, s*1.30f);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor    = Color.Transparent;
                    pgb.SurroundColors = new[]{ Color.FromArgb(140, 4, 7, 18) };
                    g.FillRectangle(pgb, 0, 0, s, s);
                }
            }

            // Text overlay background
            float textTop = s*0.64f;
            using (var b = new LinearGradientBrush(
                new PointF(0, textTop), new PointF(0, s),
                Color.FromArgb(0, 4, 7, 18), Color.FromArgb(235, 4, 7, 18)))
                g.FillRectangle(b, 0, textTop, s, s-textTop);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Title "MANTA" in teal gradient
            float titleY  = s*0.68f;
            int   titlePx = (int)(s*0.100f);
            using (var font = new Font("Segoe UI", titlePx, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    g.DrawString("MANTA", font, b, s*0.044f+2, titleY+3);
                using (var b = new LinearGradientBrush(
                    new PointF(s*0.044f, titleY), new PointF(s*0.6f, titleY),
                    Color.FromArgb(0, 210, 180), Color.FromArgb(60, 220, 255)))
                    g.DrawString("MANTA", font, b, s*0.044f, titleY);
            }

            // Subtitle
            int subPx = (int)(s*0.047f);
            using (var font  = new Font("Segoe UI", subPx, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(150, 160, 215, 200)))
                g.DrawString("Environmental Analysis  ·  Grasshopper Plugin", font, brush, s*0.044f, s*0.808f);

            // Colour bar
            float barL = s*0.044f, barR = s*0.956f, barT = s*0.900f, barBt = s*0.940f;
            int   strips = 200;
            for (int i = 0; i < strips; i++)
            {
                double t  = (double)i/(strips-1);
                float  x0 = barL + (float)(t*(barR-barL));
                float  bw2 = (barR-barL)/strips + 0.5f;
                // Left half: acoustic gradient, right half: teal gradient
                Color barC = t < 0.5
                    ? AcousticGrad(t * 2)
                    : Color.FromArgb(
                        0,
                        Cl((int)(210 - (t-0.5)*2*30)),
                        Cl((int)(180 + (t-0.5)*2*75)));
                using (var b = new SolidBrush(barC))
                    g.FillRectangle(b, x0, barT, bw2, barBt-barT);
            }
            int lblPx = (int)(s*0.038f);
            using (var font  = new Font("Segoe UI", lblPx, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(130, 190, 215, 200)))
            {
                g.DrawString("acoustic", font, brush, barL, barBt+s*0.006f);
                var msz = g.MeasureString("wind · sun", font);
                g.DrawString("wind · sun", font, brush, barR-msz.Width, barBt+s*0.006f);
            }

            // "GH" pill badge in teal
            float bW = s*0.155f, bH = s*0.062f;
            float bX = s-bW-s*0.038f, bY = s*0.038f;
            using (var path = RoundRect(new RectangleF(bX, bY, bW, bH), bH*0.4f))
            {
                using (var b = new SolidBrush(Color.FromArgb(90, 0, 60, 55)))  g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(90, 0, 210, 180), s*0.003f)) g.DrawPath(p, path);
            }
            int ghPx = (int)(s*0.044f);
            using (var font  = new Font("Segoe UI", ghPx, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.FromArgb(210, 0, 210, 180)))
            {
                var msz = g.MeasureString("GH", font);
                g.DrawString("GH", font, brush, bX+(bW-msz.Width)/2, bY+(bH-msz.Height)/2);
            }
        });
    }

    // ── Save helper ───────────────────────────────────────────────────────────
    static void Save(Bitmap bmp, string dir, string name)
    {
        using (bmp)
        {
            string path = Path.Combine(dir, name);
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine($"  wrote {name}");
        }
    }

    static void Main()
    {
        string outDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\"));

        Console.WriteLine($"\n  Manta — icon generator");
        Console.WriteLine($"  output → {outDir}");
        Console.WriteLine();

        // Assembly icons — manta ray, teal
        Save(DrawManta(24),    outDir, "Manta_24.png");
        Save(DrawManta(48),    outDir, "Manta_48.png");

        // BT acoustic component icons — amber
        Save(DrawSource(24),   outDir, "BatSource_24.png");
        Save(DrawMesh(24),     outDir, "BatMesh_24.png");
        Save(DrawNoise(24),    outDir, "BatNoise_24.png");
        Save(DrawInterior(24), outDir, "BatInterior_24.png");
        Save(DrawContours(24), outDir, "BatContours_24.png");
        Save(DrawLegend(24),   outDir, "BatLegend_24.png");

        // MN environment component icons — teal
        Save(DrawWind(24),     outDir, "MantaWind_24.png");
        Save(DrawSun(24),      outDir, "MantaSun_24.png");
        Save(DrawPressure(24), outDir, "MantaPressure_24.png");

        // Logo
        Save(DrawLogo(512),    outDir, "Manta_logo.png");

        Console.WriteLine("\n  Done.");
    }
}

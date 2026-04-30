using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Generates NoiseFacade GH component icons (24 × 24, 48 × 48)
// and a 512 × 512 logo PNG for GitHub / README.
class Program
{
    // Blue → cyan → yellow → orange → red  (matches the component exactly)
    static readonly (double t, Color c)[] Stops = {
        (0.00, Color.FromArgb(  0,   0, 255)),
        (0.25, Color.FromArgb(  0, 220, 255)),
        (0.50, Color.FromArgb(255, 240,   0)),
        (0.75, Color.FromArgb(255, 110,   0)),
        (1.00, Color.FromArgb(255,   0,   0)),
    };

    static Color Gradient(double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            if (t <= Stops[i + 1].t)
            {
                double s = (t - Stops[i].t) / (Stops[i + 1].t - Stops[i].t);
                var c0 = Stops[i].c; var c1 = Stops[i + 1].c;
                return Color.FromArgb(
                    Clamp(c0.R + (int)(s * (c1.R - c0.R))),
                    Clamp(c0.G + (int)(s * (c1.G - c0.G))),
                    Clamp(c0.B + (int)(s * (c1.B - c0.B))));
            }
        }
        return Stops[Stops.Length - 1].c;
    }

    static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // -------------------------------------------------------------------------
    //  Icon  (24 px / 48 px)
    // -------------------------------------------------------------------------

    static void DrawIcon(Graphics g, int size)
    {
        float s = size;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        float pad = s * 0.06f;
        var bg = new RectangleF(pad, pad, s - pad * 2, s - pad * 2);
        float r = s * 0.20f;

        // --- dark background ---
        using (var path = RoundRect(bg, r))
        using (var brush = new SolidBrush(Color.FromArgb(10, 14, 32)))
            g.FillPath(brush, path);

        // --- facade panel grid  3 cols × 2 rows ---
        int cols = 3, rows = 2;
        float gridL = s * 0.11f, gridR = s * 0.92f;
        float gridT = s * 0.09f, gridB = s * 0.62f;
        float gapX  = s * 0.030f, gapY = s * 0.025f;
        float pw    = (gridR - gridL - gapX * (cols - 1)) / cols;
        float ph    = (gridB - gridT - gapY * (rows - 1)) / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                double t   = (double)col / (cols - 1);
                Color  fc  = Gradient(t);
                // shade the bottom row darker (depth cue)
                double dim = row == 0 ? 1.0 : 0.70;
                fc = Color.FromArgb(
                    Clamp((int)(fc.R * dim)),
                    Clamp((int)(fc.G * dim)),
                    Clamp((int)(fc.B * dim)));

                float x = gridL + col * (pw + gapX);
                float y = gridT + row * (ph + gapY);

                using (var brush = new SolidBrush(fc))
                    g.FillRectangle(brush, x, y, pw, ph);

                // top highlight strip
                using (var brush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillRectangle(brush, x, y, pw, ph * 0.18f);
            }
        }

        // --- panel gap lines ---
        using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255), Math.Max(s * 0.012f, 0.5f)))
        {
            for (int col = 1; col < cols; col++)
            {
                float x = gridL + col * (pw + gapX) - gapX * 0.5f;
                g.DrawLine(pen, x, gridT, x, gridB);
            }
            float midY = gridT + ph + gapY * 0.5f;
            g.DrawLine(pen, gridL, midY, gridR, midY);
        }

        // --- sound source + waves (bottom-left) ---
        float cx = s * 0.22f, cy = s * 0.82f;
        using (var pen = new Pen(Color.White, Math.Max(s * 0.030f, 0.5f)))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap   = LineCap.Round;
            for (int i = 1; i <= 3; i++)
            {
                float rad   = s * (0.025f + i * 0.042f);
                int   alpha = 220 - i * 55;
                pen.Color = Color.FromArgb(alpha, 140, 200, 255);
                pen.Width = Math.Max(s * 0.028f / i, 0.5f);
                g.DrawArc(pen, cx - rad, cy - rad, rad * 2, rad * 2, 195, 150);
            }
        }
        float dr = s * 0.040f;
        using (var brush = new SolidBrush(Color.FromArgb(255, 160, 210, 255)))
            g.FillEllipse(brush, cx - dr, cy - dr, dr * 2, dr * 2);
        float ir = dr * 0.5f;
        g.FillEllipse(Brushes.White, cx - ir, cy - ir, ir * 2, ir * 2);

        // --- border ---
        using (var path = RoundRect(bg, r))
        using (var pen = new Pen(Color.FromArgb(50, 80, 140, 255), Math.Max(s * 0.018f, 0.5f)))
            g.DrawPath(pen, path);
    }

    // -------------------------------------------------------------------------
    //  Logo  (512 px)
    // -------------------------------------------------------------------------

    static void DrawLogo(Graphics g, int size)
    {
        float s = size;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // ── Background ─────────────────────────────────────────────────────
        using (var brush = new LinearGradientBrush(
            new PointF(0, 0), new PointF(s * 0.3f, s),
            Color.FromArgb(6, 9, 22), Color.FromArgb(14, 24, 52)))
            g.FillRectangle(brush, 0, 0, s, s);

        // Subtle diagonal noise texture (hand-drawn dots)
        var rng = new Random(42);
        for (int i = 0; i < 250; i++)
        {
            float x = (float)(rng.NextDouble() * s);
            float y = (float)(rng.NextDouble() * s);
            int   a = rng.Next(3, 12);
            g.FillRectangle(new SolidBrush(Color.FromArgb(a, 255, 255, 255)), x, y, 1, 1);
        }

        // ── Facade panel grid ──────────────────────────────────────────────
        int cols = 7, rows = 5;
        float fL = s * 0.04f, fR = s * 0.96f;
        float fT = s * 0.05f, fB = s * 0.63f;
        float fW = fR - fL, fH = fB - fT;
        float gapX = fW * 0.009f, gapY = fH * 0.018f;
        float pw   = (fW - gapX * (cols - 1)) / cols;
        float ph   = (fH - gapY * (rows - 1)) / rows;

        // Slight downward shear: right columns sit lower (perspective)
        float shear = fH * 0.06f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                double t   = (double)col / (cols - 1);
                Color  fc  = Gradient(t);

                // Row shadow: top row brightest, bottom darkest
                double rowBrightness = 0.55 + 0.45 * (1.0 - (double)row / rows);
                fc = Color.FromArgb(
                    Clamp((int)(fc.R * rowBrightness)),
                    Clamp((int)(fc.G * rowBrightness)),
                    Clamp((int)(fc.B * rowBrightness)));

                float x0 = fL + col * (pw + gapX);
                float y0 = fT + row * (ph + gapY) + (float)col / (cols - 1) * shear;

                // Panel body
                using (var brush = new SolidBrush(fc))
                    g.FillRectangle(brush, x0, y0, pw, ph);

                // Top highlight (catches light)
                using (var brush = new LinearGradientBrush(
                    new PointF(x0, y0), new PointF(x0, y0 + ph * 0.3f),
                    Color.FromArgb(55, 255, 255, 255), Color.Transparent))
                    g.FillRectangle(brush, x0, y0, pw, ph * 0.30f);

                // Bottom shadow
                using (var brush = new LinearGradientBrush(
                    new PointF(x0, y0 + ph * 0.7f), new PointF(x0, y0 + ph),
                    Color.Transparent, Color.FromArgb(50, 0, 0, 0)))
                    g.FillRectangle(brush, x0, y0 + ph * 0.7f, pw, ph * 0.30f);
            }
        }

        // ── Grid lines ─────────────────────────────────────────────────────
        using (var pen = new Pen(Color.FromArgb(22, 210, 230, 255), s * 0.002f))
        {
            for (int col = 0; col <= cols; col++)
            {
                float x  = fL + col * (pw + gapX);
                float sy = (float)col / cols * shear;
                g.DrawLine(pen, x, fT + sy, x, fB + sy);
            }
            for (int row = 0; row <= rows; row++)
            {
                float y = fT + row * (ph + gapY);
                g.DrawLine(pen, fL, y, fR, y + shear);
            }
        }

        // ── Sound source ───────────────────────────────────────────────────
        float srcX = s * 0.13f, srcY = s * 0.36f;

        // Propagation rays (fanned toward facade)
        using (var pen = new Pen(Color.FromArgb(14, 140, 200, 255), s * 0.002f))
        {
            for (int i = 0; i <= 6; i++)
            {
                float targetY = fT + (fB - fT) * i / 6f;
                g.DrawLine(pen, srcX, srcY, fL, targetY);
            }
        }

        // Wave rings (concentric arcs)
        for (int i = 1; i <= 7; i++)
        {
            float rad   = s * (0.018f + i * 0.028f);
            int   alpha = Math.Max(190 - i * 23, 10);
            float thick = s * 0.0055f * (8 - i) / 7f;
            using (var pen = new Pen(Color.FromArgb(alpha, 100, 170, 255), Math.Max(thick, 0.5f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap   = LineCap.Round;
                g.DrawArc(pen, srcX - rad, srcY - rad, rad * 2, rad * 2, 175, 190);
            }
        }

        // Source dot (two circles: glow + core)
        float glowR = s * 0.028f;
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(srcX - glowR * 2.5f, srcY - glowR * 2.5f, glowR * 5, glowR * 5);
            using (var pgb = new PathGradientBrush(path))
            {
                pgb.CenterColor    = Color.FromArgb(80, 120, 190, 255);
                pgb.SurroundColors = new[] { Color.Transparent };
                g.FillPath(pgb, path);
            }
        }
        using (var brush = new SolidBrush(Color.FromArgb(255, 160, 215, 255)))
            g.FillEllipse(brush, srcX - glowR, srcY - glowR, glowR * 2, glowR * 2);
        float coreR = glowR * 0.52f;
        g.FillEllipse(Brushes.White, srcX - coreR, srcY - coreR, coreR * 2, coreR * 2);

        // ── Edge vignette (depth) ──────────────────────────────────────────
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(-s * 0.15f, -s * 0.15f, s * 1.30f, s * 1.30f);
            using (var pgb = new PathGradientBrush(path))
            {
                pgb.CenterColor    = Color.Transparent;
                pgb.SurroundColors = new[] { Color.FromArgb(140, 4, 7, 18) };
                g.FillRectangle(pgb, 0, 0, s, s);
            }
        }

        // ── Text area overlay ──────────────────────────────────────────────
        float textTop = s * 0.64f;
        using (var brush = new LinearGradientBrush(
            new PointF(0, textTop), new PointF(0, s),
            Color.FromArgb(0, 4, 7, 18), Color.FromArgb(235, 4, 7, 18)))
            g.FillRectangle(brush, 0, textTop, s, s - textTop);

        // ── "NOISEFACADE" title ────────────────────────────────────────────
        float titleY = s * 0.685f;
        int   titlePx = (int)(s * 0.093f);

        using (var font = new Font("Segoe UI", titlePx, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            // drop shadow
            using (var brush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.DrawString("NOISEFACADE", font, brush, s * 0.044f + 2, titleY + 3);

            // gradient fill: white → very-light-blue
            using (var brush = new LinearGradientBrush(
                new PointF(s * 0.044f, titleY), new PointF(s * 0.96f, titleY),
                Color.White, Color.FromArgb(230, 210, 230, 255)))
                g.DrawString("NOISEFACADE", font, brush, s * 0.044f, titleY);
        }

        // ── Subtitle ───────────────────────────────────────────────────────
        int subPx = (int)(s * 0.047f);
        using (var font  = new Font("Segoe UI", subPx, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(Color.FromArgb(150, 160, 200, 245)))
            g.DrawString("Acoustic Analysis  ·  Grasshopper Plugin", font, brush, s * 0.044f, s * 0.805f);

        // ── Colour scale bar ───────────────────────────────────────────────
        float barL = s * 0.044f, barR = s * 0.956f;
        float barT = s * 0.898f, barBt = s * 0.938f;
        float barH = barBt - barT;

        // bar itself
        int strips = 200;
        for (int i = 0; i < strips; i++)
        {
            double t  = (double)i / (strips - 1);
            float  x0 = barL + (float)(t * (barR - barL));
            float  w  = (barR - barL) / strips + 0.5f;
            using (var brush = new SolidBrush(Gradient(t)))
                g.FillRectangle(brush, x0, barT, w, barH);
        }
        // top highlight on bar
        using (var brush = new LinearGradientBrush(
            new PointF(0, barT), new PointF(0, barT + barH * 0.4f),
            Color.FromArgb(40, 255, 255, 255), Color.Transparent))
            g.FillRectangle(brush, barL, barT, barR - barL, barH * 0.4f);

        // bar labels
        int lblPx = (int)(s * 0.038f);
        using (var font = new Font("Segoe UI", lblPx, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            using (var brush = new SolidBrush(Color.FromArgb(130, 190, 215, 255)))
            {
                g.DrawString("quiet", font, brush, barL, barBt + s * 0.006f);
                var sz = g.MeasureString("loud", font);
                g.DrawString("loud",  font, brush, barR - sz.Width, barBt + s * 0.006f);
            }
        }

        // ── "GH" pill badge (top-right) ────────────────────────────────────
        float bW = s * 0.155f, bH = s * 0.062f;
        float bX = s - bW - s * 0.038f, bY = s * 0.038f;
        using (var path = RoundRect(new RectangleF(bX, bY, bW, bH), bH * 0.4f))
        {
            using (var brush = new SolidBrush(Color.FromArgb(90, 20, 50, 110)))
                g.FillPath(brush, path);
            using (var pen = new Pen(Color.FromArgb(90, 80, 140, 255), s * 0.003f))
                g.DrawPath(pen, path);
        }
        int ghPx = (int)(s * 0.044f);
        using (var font  = new Font("Segoe UI", ghPx, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(Color.FromArgb(210, 130, 185, 255)))
        {
            var sz = g.MeasureString("GH", font);
            g.DrawString("GH", font, brush,
                bX + (bW - sz.Width)  / 2,
                bY + (bH - sz.Height) / 2);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X,              r.Y,               d, d, 180, 90);
        p.AddArc(r.Right - d,      r.Y,               d, d, 270, 90);
        p.AddArc(r.Right - d,      r.Bottom - d,      d, d,   0, 90);
        p.AddArc(r.X,              r.Bottom - d,      d, d,  90, 90);
        p.CloseFigure();
        return p;
    }

    static Bitmap Render(int size, Action<Graphics, int> draw)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) draw(g, size);
        return bmp;
    }

    static void Main()
    {
        // output straight to the project root (parent of GenerateIcon)
        string outDir = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\"));

        // component icons
        using (var b = Render(24,  DrawIcon)) b.Save(Path.Combine(outDir, "NoiseFacade_24.png"),  ImageFormat.Png);
        using (var b = Render(48,  DrawIcon)) b.Save(Path.Combine(outDir, "NoiseFacade_48.png"),  ImageFormat.Png);
        // logo
        using (var b = Render(512, DrawLogo)) b.Save(Path.Combine(outDir, "NoiseFacade_logo.png"), ImageFormat.Png);

        Console.WriteLine("Icons written to: " + outDir);
    }
}

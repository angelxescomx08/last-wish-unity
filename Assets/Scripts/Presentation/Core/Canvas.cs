using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>
    /// Immediate-mode drawing on the virtual 1280×720 canvas, modelled on the pygame calls the
    /// original game uses (blit, fill, draw.rect/line/circle/polygon, font.render). Everything
    /// becomes textured quads/triangles sent to the backend in submission order, so a whole frame
    /// is a handful of batched meshes; nothing here allocates per frame.
    /// </summary>
    public sealed class Canvas
    {
        public const int Width = 1280, Height = 720;
        private readonly IRenderBackend backend;
        private readonly Stack<Mat2D> transforms = new Stack<Mat2D>();
        private readonly Stack<float> alphas = new Stack<float>();
        private readonly Stack<RectF?> clips = new Stack<RectF?>();
        private Mat2D transform = Mat2D.Identity;
        private float alpha = 1f;
        private RectF? clip;
        private readonly List<(float x, float y)> scratch = new List<(float x, float y)>(64);
        private readonly List<int> earIndices = new List<int>(64);

        public Canvas(IRenderBackend backend) { this.backend = backend ?? throw new ArgumentNullException(nameof(backend)); }
        public IRenderBackend Backend => backend;
        public Tex White => backend.WhiteTexture;
        public Mat2D Transform => transform;
        public float Alpha => alpha;
        public RectF? CurrentClip => clip;

        /// <summary>Frames drawn so far (cache bookkeeping: what was used this frame).</summary>
        public int FrameIndex { get; private set; }
        public void BeginFrame()
        {
            FrameIndex++;
            transforms.Clear(); alphas.Clear(); clips.Clear(); tints.Clear();
            transform = Mat2D.Identity; alpha = 1f; clip = null; tint = Rgba.White; tinted = false;
            backend.BeginFrame();
        }
        public void EndFrame() { backend.SetClip(null); backend.EndFrame(); }

        /// <summary>Draw into an offscreen surface (like composing a cached pygame surface) with fresh state.</summary>
        public void BeginTarget(Tex target)
        {
            transforms.Push(transform); alphas.Push(alpha); clips.Push(clip); tints.Push(tint);
            transform = Mat2D.Identity; alpha = 1f; clip = null; tint = Rgba.White; tinted = false;
            backend.BeginTarget(target);
            backend.SetClip(null);
        }
        public void EndTarget()
        {
            backend.EndTarget();
            transform = transforms.Pop(); alpha = alphas.Pop(); clip = clips.Pop(); tint = tints.Pop();
            tinted = tint.R != 255 || tint.G != 255 || tint.B != 255;
            backend.SetClip(clip);
        }

        // ------------------------------------------------------------ state
        public void PushTransform(Mat2D m) { transforms.Push(transform); transform = m.Then(transform); }
        public void PushTranslate(float x, float y) => PushTransform(Mat2D.Translate(x, y));
        /// <summary>Rotate (degrees, pygame sense) and scale around a pivot.</summary>
        public void PushRotateScale(float pivotX, float pivotY, float degrees, float scale)
        {
            var m = Mat2D.Translate(-pivotX, -pivotY).Then(Mat2D.Scale(scale, scale)).Then(Mat2D.RotateDeg(degrees)).Then(Mat2D.Translate(pivotX, pivotY));
            PushTransform(m);
        }
        public void PopTransform() => transform = transforms.Pop();
        public void PushAlpha(float a) { alphas.Push(alpha); alpha *= a < 0 ? 0 : a > 1 ? 1 : a; }
        public void PopAlpha() => alpha = alphas.Pop();
        /// <summary>surface.set_clip: in screen (virtual) coordinates, intersected with the current clip.</summary>
        public void PushClip(RectF r)
        {
            clips.Push(clip);
            clip = clip.HasValue ? clip.Value.Clip(r) : r;
            backend.SetClip(clip);
        }
        public void PopClip() { clip = clips.Pop(); backend.SetClip(clip); }

        private readonly Stack<Rgba> tints = new Stack<Rgba>();
        private Rgba tint = Rgba.White;
        private bool tinted;
        /// <summary>Multiply the RGB of everything drawn (surface.fill(..., BLEND_RGBA_MULT) on a composed surface).</summary>
        public void PushTint(Rgba t)
        {
            tints.Push(tint);
            tint = new Rgba(tint.R * t.R / 255, tint.G * t.G / 255, tint.B * t.B / 255);
            tinted = tint.R != 255 || tint.G != 255 || tint.B != 255;
        }
        public void PopTint() { tint = tints.Pop(); tinted = tint.R != 255 || tint.G != 255 || tint.B != 255; }
        private Rgba Tinted(Rgba c)
        {
            if (tinted) c = new Rgba(c.R * tint.R / 255, c.G * tint.G / 255, c.B * tint.B / 255, c.A);
            return alpha >= 1f ? c : c.MulAlpha(alpha);
        }
        /// <summary>pygame's BLEND_RGB_ADD ignores every alpha (per-pixel and set_alpha): only RGB is added.</summary>
        private Rgba Tinted(Rgba c, Blend blend) => blend == Blend.Add || blend == Blend.AddMask ? c.WithAlpha(255) : Tinted(c);
        private static bool Invisible(Rgba c, Blend blend) => blend == Blend.Add || blend == Blend.AddMask ? c.R == 0 && c.G == 0 && c.B == 0 : blend != Blend.Multiply && c.A == 0;
        private (float, float) P(float x, float y) => transform.Apply(x, y);

        // ------------------------------------------------------------ images
        public void Blit(Sprite s, float x, float y) => Blit(s, x, y, 1f, Rgba.White);
        public void Blit(Sprite s, float x, float y, float scale) => Blit(s, x, y, scale, Rgba.White);
        public void Blit(Sprite s, float x, float y, float scale, Rgba tint, Blend blend = Blend.Alpha)
        {
            if (!s.Valid) return;
            if (transform.IsAxisAligned) { x = (float)Math.Floor(x); y = (float)Math.Floor(y); }
            BlitRect(s, new RectF(x, y, s.W * scale, s.H * scale), tint, blend);
        }
        /// <summary>Blit centred at (cx, cy) like rect.center placement (integer like pygame).</summary>
        public void BlitCentered(Sprite s, float cx, float cy, float scale = 1f, Rgba? tint = null, Blend blend = Blend.Alpha)
        {
            if (!s.Valid) return;
            var w = (int)Math.Round(s.W * scale); var h = (int)Math.Round(s.H * scale);
            BlitRect(s, RectF.CenteredInt(cx, cy, w, h), tint ?? Rgba.White, blend);
        }
        public void BlitRect(Sprite s, RectF dst, Rgba tint, Blend blend = Blend.Alpha)
        {
            if (!s.Valid || dst.W == 0 || dst.H == 0) return;
            var c = Tinted(tint, blend); if (Invisible(c, blend)) return;
            var (x0, y0) = P(dst.X, dst.Y); var (x1, y1) = P(dst.Right, dst.Y);
            var (x2, y2) = P(dst.Right, dst.Bottom); var (x3, y3) = P(dst.X, dst.Bottom);
            backend.Quad(s.Texture, s.X, s.Y, s.X + s.W, s.Y + s.H, x0, y0, x1, y1, x2, y2, x3, y3, c, blend);
        }
        /// <summary>Blit with horizontal / vertical flip (pygame.transform.flip).</summary>
        public void BlitFlipped(Sprite s, RectF dst, bool flipX, bool flipY, Rgba tint, Blend blend = Blend.Alpha)
        {
            if (!s.Valid) return;
            var c = Tinted(tint, blend); if (Invisible(c, blend)) return;
            float u0 = s.X, u1 = s.X + s.W, v0 = s.Y, v1 = s.Y + s.H;
            if (flipX) { var t = u0; u0 = u1; u1 = t; }
            if (flipY) { var t = v0; v0 = v1; v1 = t; }
            var (x0, y0) = P(dst.X, dst.Y); var (x1, y1) = P(dst.Right, dst.Y);
            var (x2, y2) = P(dst.Right, dst.Bottom); var (x3, y3) = P(dst.X, dst.Bottom);
            backend.Quad(s.Texture, u0, v0, u1, v1, x0, y0, x1, y1, x2, y2, x3, y3, c, blend);
        }
        /// <summary>Draw <paramref name="s"/> rotated by <paramref name="degrees"/> and scaled around its centre placed at (cx, cy).</summary>
        public void BlitRotated(Sprite s, float cx, float cy, float degrees, float scale, Rgba tint, Blend blend = Blend.Alpha)
        {
            if (!s.Valid) return;
            PushRotateScale(cx, cy, degrees, scale);
            BlitRect(s, new RectF(cx - s.W / 2f, cy - s.H / 2f, s.W, s.H), tint, blend);
            PopTransform();
        }
        /// <summary>Repeat a sprite over <paramref name="area"/> (kit tiling) without stretching.</summary>
        public void Tile(Sprite s, RectF area, float scale, Rgba tint)
        {
            if (!s.Valid || area.W <= 0 || area.H <= 0) return;
            var tw = s.W * scale; var th = s.H * scale;
            for (var y = area.Y; y < area.Bottom - 0.001f; y += th)
                for (var x = area.X; x < area.Right - 0.001f; x += tw)
                {
                    var w = Math.Min(tw, area.Right - x); var h = Math.Min(th, area.Bottom - y);
                    var sw = (int)Math.Ceiling(w / scale); var sh = (int)Math.Ceiling(h / scale);
                    BlitRect(s.Sub(0, 0, Math.Min(s.W, sw), Math.Min(s.H, sh)), new RectF(x, y, Math.Min(s.W, sw) * scale, Math.Min(s.H, sh) * scale), tint);
                }
        }

        // ------------------------------------------------------------ shapes
        public void Fill(RectF r, Rgba color, Blend blend = Blend.Alpha)
        {
            if (r.W <= 0 || r.H <= 0) return;
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            var (x0, y0) = P(r.X, r.Y); var (x1, y1) = P(r.Right, r.Y);
            var (x2, y2) = P(r.Right, r.Bottom); var (x3, y3) = P(r.X, r.Bottom);
            backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, x0, y0, x1, y1, x2, y2, x3, y3, c, blend);
        }
        public void Fill(float x, float y, float w, float h, Rgba color) => Fill(new RectF(x, y, w, h), color);

        /// <summary>pygame.draw.rect: width 0 fills; border_radius rounds the corners.</summary>
        public void Rect(RectF r, Rgba color, int width = 0, int radius = 0, Blend blend = Blend.Alpha)
        {
            if (r.W <= 0 || r.H <= 0) return;
            radius = Math.Min(radius, (int)Math.Min(r.W, r.H) / 2);
            if (radius <= 0)
            {
                if (width <= 0) { Fill(r, color, blend); return; }
                width = (int)Math.Min(width, Math.Min(r.W, r.H) / 2 + 1);
                Fill(new RectF(r.X, r.Y, r.W, width), color, blend);
                Fill(new RectF(r.X, r.Bottom - width, r.W, width), color, blend);
                Fill(new RectF(r.X, r.Y + width, width, r.H - 2 * width), color, blend);
                Fill(new RectF(r.Right - width, r.Y + width, width, r.H - 2 * width), color, blend);
                return;
            }
            if (width <= 0)
            {
                Fill(new RectF(r.X + radius, r.Y, r.W - 2 * radius, r.H), color, blend);
                Fill(new RectF(r.X, r.Y + radius, radius, r.H - 2 * radius), color, blend);
                Fill(new RectF(r.Right - radius, r.Y + radius, radius, r.H - 2 * radius), color, blend);
                QuarterDisc(r.X + radius, r.Y + radius, radius, 180, color, blend);
                QuarterDisc(r.Right - radius, r.Y + radius, radius, 270, color, blend);
                QuarterDisc(r.Right - radius, r.Bottom - radius, radius, 0, color, blend);
                QuarterDisc(r.X + radius, r.Bottom - radius, radius, 90, color, blend);
                return;
            }
            Fill(new RectF(r.X + radius, r.Y, r.W - 2 * radius, width), color, blend);
            Fill(new RectF(r.X + radius, r.Bottom - width, r.W - 2 * radius, width), color, blend);
            Fill(new RectF(r.X, r.Y + radius, width, r.H - 2 * radius), color, blend);
            Fill(new RectF(r.Right - width, r.Y + radius, width, r.H - 2 * radius), color, blend);
            QuarterRing(r.X + radius, r.Y + radius, radius, width, 180, color, blend);
            QuarterRing(r.Right - radius, r.Y + radius, radius, width, 270, color, blend);
            QuarterRing(r.Right - radius, r.Bottom - radius, radius, width, 0, color, blend);
            QuarterRing(r.X + radius, r.Bottom - radius, radius, width, 90, color, blend);
        }

        private static int Segments(float radius) => Math.Max(6, Math.Min(64, (int)(radius * 0.9f) + 6));

        private void QuarterDisc(float cx, float cy, float r, float startDeg, Rgba color, Blend blend)
        {
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            var n = Math.Max(2, Segments(r) / 4);
            var (pcx, pcy) = P(cx, cy);
            var a0 = startDeg * Math.PI / 180;
            var prev = P(cx + (float)Math.Cos(a0) * r, cy + (float)Math.Sin(a0) * r);
            for (var i = 1; i <= n; i++)
            {
                var a = a0 + Math.PI / 2 * i / n;
                var next = P(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
                backend.Triangle(pcx, pcy, prev.Item1, prev.Item2, next.Item1, next.Item2, c, blend);
                prev = next;
            }
        }

        private void QuarterRing(float cx, float cy, float r, float width, float startDeg, Rgba color, Blend blend)
        {
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            var n = Math.Max(2, Segments(r) / 4); var inner = Math.Max(0, r - width);
            var a0 = startDeg * Math.PI / 180;
            for (var i = 0; i < n; i++)
            {
                var a = a0 + Math.PI / 2 * i / n; var b = a0 + Math.PI / 2 * (i + 1) / n;
                var o0 = P(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
                var o1 = P(cx + (float)Math.Cos(b) * r, cy + (float)Math.Sin(b) * r);
                var i0 = P(cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner);
                var i1 = P(cx + (float)Math.Cos(b) * inner, cy + (float)Math.Sin(b) * inner);
                backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, o0.Item1, o0.Item2, o1.Item1, o1.Item2, i1.Item1, i1.Item2, i0.Item1, i0.Item2, c, blend);
            }
        }

        /// <summary>pygame.draw.circle (width 0 = filled disc).</summary>
        public void Circle(float cx, float cy, float radius, Rgba color, int width = 0, Blend blend = Blend.Alpha)
        {
            if (radius <= 0) return;
            // pygame's centre is a pixel: the disc spans r pixels on each side of it.
            if (transform.IsAxisAligned) { cx = (float)Math.Floor(cx) + 0.5f; cy = (float)Math.Floor(cy) + 0.5f; } else { cx += 0.5f; cy += 0.5f; }
            radius += 0.5f;
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            var n = Segments(radius);
            if (width <= 0 || width >= radius)
            {
                var (pcx, pcy) = P(cx, cy);
                var prev = P(cx + radius, cy);
                for (var i = 1; i <= n; i++)
                {
                    var a = 2 * Math.PI * i / n;
                    var next = P(cx + (float)Math.Cos(a) * radius, cy + (float)Math.Sin(a) * radius);
                    backend.Triangle(pcx, pcy, prev.Item1, prev.Item2, next.Item1, next.Item2, c, blend);
                    prev = next;
                }
                return;
            }
            var inner = radius - width;
            for (var i = 0; i < n; i++)
            {
                var a = 2 * Math.PI * i / n; var b = 2 * Math.PI * (i + 1) / n;
                var o0 = P(cx + (float)Math.Cos(a) * radius, cy + (float)Math.Sin(a) * radius);
                var o1 = P(cx + (float)Math.Cos(b) * radius, cy + (float)Math.Sin(b) * radius);
                var i0 = P(cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner);
                var i1 = P(cx + (float)Math.Cos(b) * inner, cy + (float)Math.Sin(b) * inner);
                backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, o0.Item1, o0.Item2, o1.Item1, o1.Item2, i1.Item1, i1.Item2, i0.Item1, i0.Item2, c, blend);
            }
        }

        /// <summary>pygame.draw.ellipse inside <paramref name="r"/>.</summary>
        public void Ellipse(RectF r, Rgba color, int width = 0, Blend blend = Blend.Alpha)
        {
            if (r.W <= 0 || r.H <= 0) return;
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            var rx = r.W / 2f; var ry = r.H / 2f; var cx = r.X + rx; var cy = r.Y + ry;
            var n = Segments(Math.Max(rx, ry));
            for (var i = 0; i < n; i++)
            {
                var a = 2 * Math.PI * i / n; var b = 2 * Math.PI * (i + 1) / n;
                var o0 = P(cx + (float)Math.Cos(a) * rx, cy + (float)Math.Sin(a) * ry);
                var o1 = P(cx + (float)Math.Cos(b) * rx, cy + (float)Math.Sin(b) * ry);
                if (width <= 0)
                {
                    var (pcx, pcy) = P(cx, cy);
                    backend.Triangle(pcx, pcy, o0.Item1, o0.Item2, o1.Item1, o1.Item2, c, blend);
                }
                else
                {
                    var irx = Math.Max(0, rx - width); var iry = Math.Max(0, ry - width);
                    var i0 = P(cx + (float)Math.Cos(a) * irx, cy + (float)Math.Sin(a) * iry);
                    var i1 = P(cx + (float)Math.Cos(b) * irx, cy + (float)Math.Sin(b) * iry);
                    backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, o0.Item1, o0.Item2, o1.Item1, o1.Item2, i1.Item1, i1.Item2, i0.Item1, i0.Item2, c, blend);
                }
            }
        }

        /// <summary>pygame.draw.arc: angles in radians, counter-clockwise from +x (screen y up).</summary>
        public void Arc(RectF r, double start, double stop, Rgba color, int width = 1, Blend blend = Blend.Alpha)
        {
            var c = Tinted(color, blend); if (Invisible(c, blend) || r.W <= 0 || r.H <= 0) return;
            var rx = r.W / 2f; var ry = r.H / 2f; var cx = r.X + rx; var cy = r.Y + ry;
            while (stop < start) stop += 2 * Math.PI;
            var n = Math.Max(2, (int)(Segments(Math.Max(rx, ry)) * (stop - start) / (2 * Math.PI)) + 1);
            for (var i = 0; i < n; i++)
            {
                var a = start + (stop - start) * i / n; var b = start + (stop - start) * (i + 1) / n;
                float irx = Math.Max(0, rx - width), iry = Math.Max(0, ry - width);
                var o0 = P(cx + (float)Math.Cos(a) * rx, cy - (float)Math.Sin(a) * ry);
                var o1 = P(cx + (float)Math.Cos(b) * rx, cy - (float)Math.Sin(b) * ry);
                var i0 = P(cx + (float)Math.Cos(a) * irx, cy - (float)Math.Sin(a) * iry);
                var i1 = P(cx + (float)Math.Cos(b) * irx, cy - (float)Math.Sin(b) * iry);
                backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, o0.Item1, o0.Item2, o1.Item1, o1.Item2, i1.Item1, i1.Item2, i0.Item1, i0.Item2, c, blend);
            }
        }

        /// <summary>pygame.draw.line with a square-ended thick quad.</summary>
        public void Line(float x0, float y0, float x1, float y1, Rgba color, float width = 1, Blend blend = Blend.Alpha)
        {
            var c = Tinted(color, blend); if (Invisible(c, blend) || width <= 0) return;
            if (x0 == x1 && y0 == y1) { Fill(new RectF(x0 - width / 2f, y0 - width / 2f, width, width), color, blend); return; }
            float dx = x1 - x0, dy = y1 - y0;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                // pygame thick lines grow vertically for mostly horizontal lines.
                var h = width / 2f;
                var a = P(x0, y0 - h); var b = P(x1 + Math.Sign(dx) * 0.5f, y1 - h); var cc = P(x1 + Math.Sign(dx) * 0.5f, y1 + h); var d = P(x0, y0 + h);
                if (width <= 1) { a = P(x0, y0); b = P(x1 + Math.Sign(dx), y1); cc = P(x1 + Math.Sign(dx), y1 + 1); d = P(x0, y0 + 1); }
                backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, a.Item1, a.Item2, b.Item1, b.Item2, cc.Item1, cc.Item2, d.Item1, d.Item2, c, blend);
            }
            else
            {
                var w = width / 2f;
                var a = P(x0 - w, y0); var b = P(x0 + w, y0); var cc = P(x1 + w, y1 + Math.Sign(dy) * 0.5f); var d = P(x1 - w, y1 + Math.Sign(dy) * 0.5f);
                if (width <= 1) { a = P(x0, y0); b = P(x0 + 1, y0); cc = P(x1 + 1, y1 + Math.Sign(dy)); d = P(x1, y1 + Math.Sign(dy)); }
                backend.Quad(backend.WhiteTexture, 0, 0, 1, 1, a.Item1, a.Item2, b.Item1, b.Item2, cc.Item1, cc.Item2, d.Item1, d.Item2, c, blend);
            }
        }

        public void Lines(IReadOnlyList<(float x, float y)> points, bool closed, Rgba color, float width = 1, Blend blend = Blend.Alpha)
        {
            if (points == null || points.Count < 2) return;
            for (var i = 0; i < points.Count - 1; i++) Line(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, color, width, blend);
            if (closed) Line(points[points.Count - 1].x, points[points.Count - 1].y, points[0].x, points[0].y, color, width, blend);
        }

        /// <summary>pygame.draw.polygon (concave shapes are ear-clipped).</summary>
        public void Polygon(IReadOnlyList<(float x, float y)> points, Rgba color, int width = 0, Blend blend = Blend.Alpha)
        {
            if (points == null || points.Count < 3) return;
            if (width > 0) { Lines(points, true, color, width, blend); return; }
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            scratch.Clear(); earIndices.Clear();
            for (var i = 0; i < points.Count; i++) { scratch.Add(points[i]); earIndices.Add(i); }
            double area = 0;
            for (var i = 0; i < scratch.Count; i++) { var a = scratch[i]; var b = scratch[(i + 1) % scratch.Count]; area += a.x * b.y - b.x * a.y; }
            var ccw = area > 0;
            var guard = 0;
            while (earIndices.Count > 3 && guard++ < 1000)
            {
                var clipped = false;
                for (var i = 0; i < earIndices.Count; i++)
                {
                    int ia = earIndices[(i + earIndices.Count - 1) % earIndices.Count], ib = earIndices[i], ic = earIndices[(i + 1) % earIndices.Count];
                    var a = scratch[ia]; var b = scratch[ib]; var cc = scratch[ic];
                    var cross = (b.x - a.x) * (cc.y - a.y) - (b.y - a.y) * (cc.x - a.x);
                    if (ccw ? cross <= 0 : cross >= 0) continue;
                    var inside = false;
                    foreach (var j in earIndices)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (PointInTriangle(scratch[j], a, b, cc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    Tri(a, b, cc, c, blend); earIndices.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (earIndices.Count >= 3)
                for (var i = 1; i < earIndices.Count - 1; i++) Tri(scratch[earIndices[0]], scratch[earIndices[i]], scratch[earIndices[i + 1]], c, blend);
        }

        private static bool PointInTriangle((float x, float y) p, (float x, float y) a, (float x, float y) b, (float x, float y) c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            var neg = d1 < 0 || d2 < 0 || d3 < 0; var pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private void Tri((float x, float y) a, (float x, float y) b, (float x, float y) c, Rgba color, Blend blend)
        {
            var pa = P(a.x, a.y); var pb = P(b.x, b.y); var pc = P(c.x, c.y);
            backend.Triangle(pa.Item1, pa.Item2, pb.Item1, pb.Item2, pc.Item1, pc.Item2, color, blend);
        }

        /// <summary>Raw triangle in the current transform (already tinted by the caller's colour).</summary>
        public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend = Blend.Alpha)
        {
            var c = Tinted(color, blend); if (Invisible(c, blend)) return;
            Tri((x0, y0), (x1, y1), (x2, y2), c, blend);
        }

        /// <summary>Textured quad with explicit corners in the current transform.</summary>
        public void QuadUV(Sprite s, (float x, float y) tl, (float x, float y) tr, (float x, float y) br, (float x, float y) bl, Rgba tint, Blend blend = Blend.Alpha)
        {
            if (!s.Valid) return;
            var c = Tinted(tint, blend); if (Invisible(c, blend)) return;
            var a = P(tl.x, tl.y); var b = P(tr.x, tr.y); var cc = P(br.x, br.y); var d = P(bl.x, bl.y);
            backend.Quad(s.Texture, s.X, s.Y, s.X + s.W, s.Y + s.H, a.Item1, a.Item2, b.Item1, b.Item2, cc.Item1, cc.Item2, d.Item1, d.Item2, c, blend);
        }

        private readonly List<(float x, float y, float u, float v)> polyA = new List<(float, float, float, float)>(32), polyB = new List<(float, float, float, float)>(32);

        /// <summary>Draw <paramref name="s"/> stretched over <paramref name="dst"/> but only where it lies inside the
        /// convex polygon <paramref name="clip"/> (local coordinates). Used for light bands masked by a card.</summary>
        public void BlitClippedConvex(Sprite s, RectF dst, IReadOnlyList<(float x, float y)> clip, Rgba tint, Blend blend)
        {
            if (!s.Valid || clip == null || clip.Count < 3) return;
            var c = Tinted(tint, blend); if (Invisible(c, blend)) return;
            polyA.Clear();
            polyA.Add((dst.X, dst.Y, s.X, s.Y)); polyA.Add((dst.Right, dst.Y, s.X + s.W, s.Y));
            polyA.Add((dst.Right, dst.Bottom, s.X + s.W, s.Y + s.H)); polyA.Add((dst.X, dst.Bottom, s.X, s.Y + s.H));
            double area = 0;
            for (var i = 0; i < clip.Count; i++) { var a = clip[i]; var b = clip[(i + 1) % clip.Count]; area += a.x * b.y - b.x * a.y; }
            var sign = area >= 0 ? 1f : -1f;
            for (var e = 0; e < clip.Count && polyA.Count >= 3; e++)
            {
                var p0 = clip[e]; var p1 = clip[(e + 1) % clip.Count];
                polyB.Clear();
                for (var i = 0; i < polyA.Count; i++)
                {
                    var a = polyA[i]; var b = polyA[(i + 1) % polyA.Count];
                    var da = sign * ((p1.x - p0.x) * (a.y - p0.y) - (p1.y - p0.y) * (a.x - p0.x));
                    var db = sign * ((p1.x - p0.x) * (b.y - p0.y) - (p1.y - p0.y) * (b.x - p0.x));
                    if (da >= 0) polyB.Add(a);
                    if ((da >= 0) != (db >= 0))
                    {
                        var t = da / (da - db);
                        polyB.Add((a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.u + (b.u - a.u) * t, a.v + (b.v - a.v) * t));
                    }
                }
                polyA.Clear(); polyA.AddRange(polyB);
            }
            for (var i = 1; i < polyA.Count - 1; i++)
            {
                var a = polyA[0]; var b = polyA[i]; var d = polyA[i + 1];
                var pa = P(a.x, a.y); var pb = P(b.x, b.y); var pd = P(d.x, d.y);
                backend.TexTriangle(s.Texture, pa.Item1, pa.Item2, a.u, a.v, pb.Item1, pb.Item2, b.u, b.v, pd.Item1, pd.Item2, d.u, d.v, c, blend);
            }
        }
    }
}
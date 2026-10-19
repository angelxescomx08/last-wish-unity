using System;

namespace LastWish.Presentation
{
    /// <summary>8-bit RGBA colour (pygame.Color).</summary>
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly byte R, G, B, A;
        public Rgba(int r, int g, int b, int a = 255)
        { R = Clamp(r); G = Clamp(g); B = Clamp(b); A = Clamp(a); }
        private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        public static Rgba FromFloat(double r, double g, double b, double a = 255) => new Rgba((int)r, (int)g, (int)b, (int)a);
        public Rgba WithAlpha(int a) => new Rgba(R, G, B, a);
        public Rgba MulAlpha(float f) => new Rgba(R, G, B, (int)(A * f + .5f));
        /// <summary>fx.bursts.scaled: every channel × k (clamped), alpha kept.</summary>
        public Rgba Scaled(double k) => new Rgba((int)(R * k), (int)(G * k), (int)(B * k), A);
        public static Rgba Lerp(Rgba a, Rgba b, double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return new Rgba((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t), (int)(a.A + (b.A - a.A) * t));
        }
        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba o && Equals(o);
        public override int GetHashCode() => R | G << 8 | B << 16 | A << 24;
        public override string ToString() => $"({R},{G},{B},{A})";
        public static readonly Rgba White = new Rgba(255, 255, 255), Black = new Rgba(0, 0, 0), Clear = new Rgba(0, 0, 0, 0);
    }

    /// <summary>Axis-aligned rectangle with pygame.Rect semantics (right/bottom exclusive).</summary>
    public struct RectF : IEquatable<RectF>
    {
        public float X, Y, W, H;
        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float Left => X; public float Top => Y;
        public float Right => X + W; public float Bottom => Y + H;
        public float CenterX => X + W / 2f; public float CenterY => Y + H / 2f;
        public (float x, float y) Center => (X + W / 2f, Y + H / 2f);
        public static RectF FromCenter(float cx, float cy, float w, float h) => new RectF(cx - w / 2f, cy - h / 2f, w, h);
        /// <summary>pygame-style integer center placement: x = cx - w // 2.</summary>
        public static RectF CenteredInt(float cx, float cy, int w, int h) => new RectF((float)Math.Floor(cx - Math.Floor(w / 2.0)), (float)Math.Floor(cy - Math.Floor(h / 2.0)), w, h);
        public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y && py < Y + H;
        public bool Contains((float x, float y) p) => Contains(p.x, p.y);
        public bool Intersects(RectF o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
        public RectF Inflate(float dx, float dy) => new RectF(X - dx / 2f, Y - dy / 2f, W + dx, H + dy);
        public RectF Move(float dx, float dy) => new RectF(X + dx, Y + dy, W, H);
        public RectF WithCenter(float cx, float cy) => new RectF(cx - W / 2f, cy - H / 2f, W, H);
        public RectF Clip(RectF o)
        {
            var l = Math.Max(X, o.X); var t = Math.Max(Y, o.Y); var r = Math.Min(Right, o.Right); var b = Math.Min(Bottom, o.Bottom);
            return r <= l || b <= t ? new RectF(l, t, 0, 0) : new RectF(l, t, r - l, b - t);
        }
        public RectF Union(RectF o)
        {
            var l = Math.Min(X, o.X); var t = Math.Min(Y, o.Y); var r = Math.Max(Right, o.Right); var b = Math.Max(Bottom, o.Bottom);
            return new RectF(l, t, r - l, b - t);
        }
        public bool Equals(RectF o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is RectF o && Equals(o);
        public override int GetHashCode() => (X, Y, W, H).GetHashCode();
        public override string ToString() => $"Rect({X},{Y},{W},{H})";
    }

    /// <summary>Integer rectangle with exact pygame.Rect arithmetic (centre = x + w // 2, inflate halves toward zero).</summary>
    public struct IRect : IEquatable<IRect>
    {
        public int X, Y, W, H;
        public IRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public int Right => X + W; public int Bottom => Y + H;
        public int CenterX => X + W / 2; public int CenterY => Y + H / 2;
        public IRect Inflate(int dx, int dy) => new IRect(X - dx / 2, Y - dy / 2, W + dx, H + dy);
        public IRect Move(int dx, int dy) => new IRect(X + dx, Y + dy, W, H);
        public static IRect AtCenter(int cx, int cy, int w, int h) => new IRect(cx - w / 2, cy - h / 2, w, h);
        public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y && py < Y + H;
        public static implicit operator RectF(IRect r) => new RectF(r.X, r.Y, r.W, r.H);
        public bool Equals(IRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object obj) => obj is IRect o && Equals(o);
        public override int GetHashCode() => (X, Y, W, H).GetHashCode();
        public override string ToString() => $"IRect({X},{Y},{W},{H})";
    }

    /// <summary>2D affine transform (a c tx / b d ty).</summary>
    public readonly struct Mat2D
    {
        public readonly float A, B, C, D, Tx, Ty;
        public Mat2D(float a, float b, float c, float d, float tx, float ty) { A = a; B = b; C = c; D = d; Tx = tx; Ty = ty; }
        public static readonly Mat2D Identity = new Mat2D(1, 0, 0, 1, 0, 0);
        public static Mat2D Translate(float x, float y) => new Mat2D(1, 0, 0, 1, x, y);
        public static Mat2D Scale(float sx, float sy) => new Mat2D(sx, 0, 0, sy, 0, 0);
        /// <summary>Rotation in degrees, counter-clockwise on screen like pygame.transform.rotate.</summary>
        public static Mat2D RotateDeg(float degrees)
        {
            var r = -degrees * (float)Math.PI / 180f; var c = (float)Math.Cos(r); var s = (float)Math.Sin(r);
            return new Mat2D(c, s, -s, c, 0, 0);
        }
        /// <summary>this then other (other ∘ this).</summary>
        public Mat2D Then(Mat2D o) => new Mat2D(
            o.A * A + o.C * B, o.B * A + o.D * B,
            o.A * C + o.C * D, o.B * C + o.D * D,
            o.A * Tx + o.C * Ty + o.Tx, o.B * Tx + o.D * Ty + o.Ty);
        public (float x, float y) Apply(float x, float y) => (A * x + C * y + Tx, B * x + D * y + Ty);
        public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && Tx == 0 && Ty == 0;
        public bool IsAxisAligned => B == 0 && C == 0;
    }

    /// <summary>pygame blend modes: alpha blit, BLEND_RGB_ADD (alpha ignored), BLEND_RGB_MULT, and an
    /// additive light masked by the texture alpha (what BLEND_RGB_ADD onto an SRCALPHA surface shows), and a flat
    /// silhouette (the vertex colour with the texture's alpha: a sprite filled white with BLEND_RGBA_MAX).</summary>
    public enum Blend { Alpha, Add, Multiply, AddMask, Silhouette }

    /// <summary>A texture known to the backend (asset or procedural).</summary>
    public sealed class Tex
    {
        public int Id { get; }
        public string Key { get; }
        public int Width { get; }
        public int Height { get; }
        public bool Smooth { get; }
        /// <summary>An offscreen render target (premultiplied colour, straight alpha mask).</summary>
        public bool IsTarget { get; }
        public Tex(int id, string key, int width, int height, bool smooth = false, bool isTarget = false) { Id = id; Key = key; Width = width; Height = height; Smooth = smooth; IsTarget = isTarget; }
        public override string ToString() => Key;
    }

    /// <summary>A region of a texture (sprite).</summary>
    public readonly struct Sprite
    {
        public readonly Tex Texture;
        public readonly int X, Y, W, H;
        public Sprite(Tex texture, int x, int y, int w, int h) { Texture = texture; X = x; Y = y; W = w; H = h; }
        public Sprite(Tex texture) : this(texture, 0, 0, texture?.Width ?? 0, texture?.Height ?? 0) { }
        public bool Valid => Texture != null && W > 0 && H > 0;
        public Sprite Sub(int x, int y, int w, int h) => new Sprite(Texture, X + x, Y + y, w, h);
    }

    /// <summary>What the presentation needs from a GPU backend: textured quads/triangles in submission order.</summary>
    public interface IRenderBackend
    {
        Tex LoadTexture(string key);
        Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false);
        Tex WhiteTexture { get; }
        void BeginFrame();
        /// <summary>Corners in virtual px (top-left origin) TL, TR, BR, BL; uv in texels of <paramref name="tex"/>.</summary>
        void Quad(Tex tex, float u0, float v0, float u1, float v1,
            float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend);
        void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend);
        /// <summary>Textured triangle; uv in texels.</summary>
        void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend);
        /// <summary>An offscreen surface (pygame.Surface(SRCALPHA)) that can be drawn into and then blitted.</summary>
        Tex CreateTarget(string key, int width, int height);
        void ReleaseTarget(Tex target);
        /// <summary>Following draws go into <paramref name="target"/> (cleared to transparent) until <see cref="EndTarget"/>.
        /// Targets are rendered before the frame, so a target must not be redrawn after being used in the same frame.</summary>
        void BeginTarget(Tex target);
        void EndTarget();
        /// <summary>Scissor in virtual px, or null for none.</summary>
        void SetClip(RectF? clip);
        void EndFrame();
    }
}

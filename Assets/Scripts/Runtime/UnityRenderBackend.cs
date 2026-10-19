using System;
using System.Collections.Generic;
using LastWish.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using Blend = LastWish.Presentation.Blend;
using Canvas = LastWish.Presentation.Canvas;

namespace LastWish.Runtime
{
    /// <summary>
    /// GPU backend of the presentation canvas: every frame the quads/triangles are appended to one
    /// reusable mesh (split into sub-meshes when the texture or blend mode changes) and drawn in
    /// submission order by a command buffer on the canvas camera. Clipping is done on the CPU so no
    /// scissor state depends on the graphics API. Steady state allocates nothing.
    /// </summary>
    public sealed class UnityRenderBackend : IRenderBackend, IDisposable
    {
        private struct Batch { public int Texture; public Blend Blend; public int Start; public int Count; }
        /// <summary>A run of batches drawn into one target (-1 = the main canvas).</summary>
        private struct Pass { public int Target; public int FirstBatch; public int EndBatch; }

        private readonly Dictionary<int, Texture> textures = new Dictionary<int, Texture>();
        private readonly Dictionary<string, Tex> byKey = new Dictionary<string, Tex>();
        private readonly List<Vector3> positions = new List<Vector3>(16384);
        private readonly List<Color32> colors = new List<Color32>(16384);
        private readonly List<Vector2> uvs = new List<Vector2>(16384);
        private readonly List<int> indices = new List<int>(24576);
        private readonly List<Batch> batches = new List<Batch>(256);
        private readonly Mesh mesh;
        private readonly Material alphaMaterial, addMaterial, multiplyMaterial, addMaskMaterial, premulMaterial, silhouetteMaterial;
        private readonly List<Pass> passes = new List<Pass>(64);
        private readonly Stack<int> targetStack = new Stack<int>();
        private int currentTarget = -1, passStart;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly CommandBuffer commands = new CommandBuffer { name = "LastWish canvas" };
        private readonly Vector2[] clipIn = new Vector2[16], clipOut = new Vector2[16];
        private readonly Vector2[] uvIn = new Vector2[16], uvOut = new Vector2[16];
        private readonly int mainTex = Shader.PropertyToID("_MainTex");
        private RectF? clip;
        private int nextId;
        public Tex WhiteTexture { get; }
        public CommandBuffer Commands => commands;
        /// <summary>Where the main canvas is drawn (the 1280×720 virtual canvas at an integer scale).</summary>
        public RenderTexture MainTarget { get; set; }
        public int LastBatchCount { get; private set; }
        public int LastVertexCount { get; private set; }
        public Func<string, Texture2D> Loader { get; set; }
        /// <summary>Flip the vertical axis of every target (set by <see cref="Calibrate"/> on APIs whose render textures come out upside down).</summary>
        public bool FlipY { get; set; }

        public UnityRenderBackend()
        {
            mesh = new Mesh { name = "LastWish canvas" };
            mesh.MarkDynamic();
            mesh.indexFormat = IndexFormat.UInt32;
            alphaMaterial = new Material(LoadShader("CanvasAlpha")) { hideFlags = HideFlags.HideAndDontSave };
            addMaterial = new Material(LoadShader("CanvasAdd")) { hideFlags = HideFlags.HideAndDontSave };
            multiplyMaterial = new Material(LoadShader("CanvasMultiply")) { hideFlags = HideFlags.HideAndDontSave };
            addMaskMaterial = new Material(LoadShader("CanvasAddMask")) { hideFlags = HideFlags.HideAndDontSave };
            premulMaterial = new Material(LoadShader("CanvasPremul")) { hideFlags = HideFlags.HideAndDontSave };
            silhouetteMaterial = new Material(LoadShader("CanvasSilhouette")) { hideFlags = HideFlags.HideAndDontSave };
            var white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white); white.Apply(false, true);
            WhiteTexture = Register("white", white, false);
        }

        public static Shader LoadShader(string name)
        {
            var shader = Resources.Load<Shader>("Game/Shaders/" + name) ?? Shader.Find("LastWish/" + name);
            if (shader == null) throw new InvalidOperationException("Missing shader " + name);
            return shader;
        }

        private Tex Register(string key, Texture texture, bool smooth)
        {
            var tex = new Tex(nextId++, key, texture.width, texture.height, smooth);
            textures[tex.Id] = texture; byKey[key] = tex; return tex;
        }

        public Tex LoadTexture(string key)
        {
            if (byKey.TryGetValue(key, out var known)) return known;
            var texture = Loader?.Invoke(key) ?? Resources.Load<Texture2D>("Game/" + key);
            if (texture == null) throw new InvalidOperationException("Missing texture: " + key);
            return Register(key, texture, texture.filterMode != FilterMode.Point);
        }

        public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false)
        {
            if (byKey.TryGetValue(key, out var known)) return known;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, name = key };
            var data = new Color32[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var p = pixels[y * width + x];
                    data[(height - 1 - y) * width + x] = new Color32(p.R, p.G, p.B, p.A);
                }
            texture.SetPixels32(data); texture.Apply(false, true);
            return Register(key, texture, smooth);
        }

        public Tex CreateTarget(string key, int width, int height)
        {
            var rt = new RenderTexture(Math.Max(1, width), Math.Max(1, height), 0, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, name = key, hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
            var tex = new Tex(nextId++, key, rt.width, rt.height, true, true);
            textures[tex.Id] = rt;
            return tex;
        }

        public void ReleaseTarget(Tex target)
        {
            if (target == null || !textures.TryGetValue(target.Id, out var texture)) return;
            textures.Remove(target.Id);
            if (texture is RenderTexture rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
        }

        public void BeginTarget(Tex target)
        {
            ClosePass();
            targetStack.Push(currentTarget);
            currentTarget = target.Id;
        }

        public void EndTarget()
        {
            ClosePass();
            currentTarget = targetStack.Count > 0 ? targetStack.Pop() : -1;
        }

        private void ClosePass()
        {
            if (batches.Count > passStart || currentTarget >= 0)
                passes.Add(new Pass { Target = currentTarget, FirstBatch = passStart, EndBatch = batches.Count });
            passStart = batches.Count;
        }

        public void BeginFrame()
        {
            positions.Clear(); colors.Clear(); uvs.Clear(); indices.Clear(); batches.Clear(); passes.Clear(); targetStack.Clear();
            clip = null; currentTarget = -1; passStart = 0;
        }

        public void SetClip(RectF? value) => clip = value;

        private void EnsureBatch(int texture, Blend blend)
        {
            if (batches.Count > passStart)
            {
                var last = batches[batches.Count - 1];
                if (last.Texture == texture && last.Blend == blend) return;
            }
            batches.Add(new Batch { Texture = texture, Blend = blend, Start = indices.Count, Count = 0 });
        }

        private void Grow(int count)
        {
            var b = batches[batches.Count - 1]; b.Count += count; batches[batches.Count - 1] = b;
        }

        public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
        {
            float iw = 1f / tex.Width, ih = 1f / tex.Height;
            // Unity textures have their origin at the bottom-left.
            float tu0 = u0 * iw, tu1 = u1 * iw, tv0 = 1f - v0 * ih, tv1 = 1f - v1 * ih;
            if (clip.HasValue)
            {
                var c = clip.Value;
                var minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)); var maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
                var minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)); var maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
                if (maxX <= c.X || minX >= c.Right || maxY <= c.Y || minY >= c.Bottom) return;
                if (minX < c.X || maxX > c.Right || minY < c.Y || maxY > c.Bottom)
                {
                    clipIn[0] = new Vector2(x0, y0); clipIn[1] = new Vector2(x1, y1); clipIn[2] = new Vector2(x2, y2); clipIn[3] = new Vector2(x3, y3);
                    uvIn[0] = new Vector2(tu0, tv0); uvIn[1] = new Vector2(tu1, tv0); uvIn[2] = new Vector2(tu1, tv1); uvIn[3] = new Vector2(tu0, tv1);
                    Polygon(tex.Id, 4, color, blend);
                    return;
                }
            }
            EnsureBatch(tex.Id, blend);
            var start = positions.Count;
            var col = new Color32(color.R, color.G, color.B, color.A);
            positions.Add(new Vector3(x0, -y0, 0)); positions.Add(new Vector3(x1, -y1, 0)); positions.Add(new Vector3(x2, -y2, 0)); positions.Add(new Vector3(x3, -y3, 0));
            uvs.Add(new Vector2(tu0, tv0)); uvs.Add(new Vector2(tu1, tv0)); uvs.Add(new Vector2(tu1, tv1)); uvs.Add(new Vector2(tu0, tv1));
            colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2); indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            Grow(6);
        }

        public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend)
        {
            if (clip.HasValue)
            {
                var c = clip.Value;
                var minX = Math.Min(x0, Math.Min(x1, x2)); var maxX = Math.Max(x0, Math.Max(x1, x2));
                var minY = Math.Min(y0, Math.Min(y1, y2)); var maxY = Math.Max(y0, Math.Max(y1, y2));
                if (maxX <= c.X || minX >= c.Right || maxY <= c.Y || minY >= c.Bottom) return;
                if (minX < c.X || maxX > c.Right || minY < c.Y || maxY > c.Bottom)
                {
                    clipIn[0] = new Vector2(x0, y0); clipIn[1] = new Vector2(x1, y1); clipIn[2] = new Vector2(x2, y2);
                    uvIn[0] = uvIn[1] = uvIn[2] = new Vector2(.5f, .5f);
                    Polygon(WhiteTexture.Id, 3, color, blend);
                    return;
                }
            }
            EnsureBatch(WhiteTexture.Id, blend);
            var start = positions.Count;
            var col = new Color32(color.R, color.G, color.B, color.A);
            positions.Add(new Vector3(x0, -y0, 0)); positions.Add(new Vector3(x1, -y1, 0)); positions.Add(new Vector3(x2, -y2, 0));
            var uv = new Vector2(.5f, .5f); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            colors.Add(col); colors.Add(col); colors.Add(col);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            Grow(3);
        }

        public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend)
        {
            float iw = 1f / tex.Width, ih = 1f / tex.Height;
            clipIn[0] = new Vector2(x0, y0); clipIn[1] = new Vector2(x1, y1); clipIn[2] = new Vector2(x2, y2);
            uvIn[0] = new Vector2(u0 * iw, 1f - v0 * ih); uvIn[1] = new Vector2(u1 * iw, 1f - v1 * ih); uvIn[2] = new Vector2(u2 * iw, 1f - v2 * ih);
            if (clip.HasValue) { Polygon(tex.Id, 3, color, blend); return; }
            EnsureBatch(tex.Id, blend);
            var start = positions.Count; var col = new Color32(color.R, color.G, color.B, color.A);
            for (var i = 0; i < 3; i++) { positions.Add(new Vector3(clipIn[i].x, -clipIn[i].y, 0)); uvs.Add(uvIn[i]); colors.Add(col); }
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            Grow(3);
        }

        /// <summary>Sutherland–Hodgman against the clip rect, then a triangle fan.</summary>
        private void Polygon(int texture, int count, Rgba color, Blend blend)
        {
            var c = clip.Value;
            count = ClipEdge(count, 0, c.X, true); if (count < 3) return;
            count = ClipEdge(count, 0, c.Right, false); if (count < 3) return;
            count = ClipEdge(count, 1, c.Y, true); if (count < 3) return;
            count = ClipEdge(count, 1, c.Bottom, false); if (count < 3) return;
            EnsureBatch(texture, blend);
            var start = positions.Count; var col = new Color32(color.R, color.G, color.B, color.A);
            for (var i = 0; i < count; i++) { positions.Add(new Vector3(clipIn[i].x, -clipIn[i].y, 0)); uvs.Add(uvIn[i]); colors.Add(col); }
            for (var i = 1; i < count - 1; i++) { indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1); }
            Grow((count - 2) * 3);
        }

        private int ClipEdge(int count, int axis, float limit, bool keepGreater)
        {
            var n = 0;
            for (var i = 0; i < count; i++)
            {
                var a = clipIn[i]; var b = clipIn[(i + 1) % count]; var ua = uvIn[i]; var ub = uvIn[(i + 1) % count];
                var va = axis == 0 ? a.x : a.y; var vb = axis == 0 ? b.x : b.y;
                var ina = keepGreater ? va >= limit : va <= limit; var inb = keepGreater ? vb >= limit : vb <= limit;
                if (ina && n < clipOut.Length) { clipOut[n] = a; uvOut[n] = ua; n++; }
                if (ina != inb && n < clipOut.Length)
                {
                    var t = (limit - va) / (vb - va);
                    clipOut[n] = Vector2.Lerp(a, b, t); uvOut[n] = Vector2.Lerp(ua, ub, t); n++;
                }
            }
            for (var i = 0; i < n; i++) { clipIn[i] = clipOut[i]; uvIn[i] = uvOut[i]; }
            return n;
        }

        public void EndFrame()
        {
            commands.Clear();
            ClosePass();
            LastBatchCount = batches.Count; LastVertexCount = positions.Count;
            if (batches.Count > 0) UploadMesh();
            // Offscreen surfaces first (in the order they were composed), then the canvas.
            foreach (var pass in passes)
            {
                if (pass.Target < 0 || !textures.TryGetValue(pass.Target, out var texture)) continue;
                var rt = (RenderTexture)texture;
                commands.SetRenderTarget(rt);
                commands.ClearRenderTarget(false, true, Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Projection(rt.width, rt.height));
                DrawBatches(pass);
            }
            if (MainTarget != null)
            {
                commands.SetRenderTarget(MainTarget);
                commands.ClearRenderTarget(false, true, Color.black);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Projection(Canvas.Width, Canvas.Height));
            }
            foreach (var pass in passes) if (pass.Target < 0) DrawBatches(pass);
        }

        private Matrix4x4 Projection(int width, int height)
        {
            var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, width, -height, 0, -1, 1), true);
            return FlipY ? Matrix4x4.Scale(new Vector3(1, -1, 1)) * projection : projection;
        }

        /// <summary>
        /// Draw white on the top half of the main target, read a pixel near the top and set <see cref="FlipY"/>
        /// so the canvas is upright when sampled as a texture (D3D11/12, Vulkan, Metal and GL differ).
        /// </summary>
        public void Calibrate(Canvas canvas)
        {
            if (MainTarget == null) return;
            FlipY = false;
            canvas.BeginFrame();
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height / 2), Rgba.White);
            canvas.EndFrame();
            Graphics.ExecuteCommandBuffer(commands);
            var previous = RenderTexture.active;
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = MainTarget;
                probe.ReadPixels(new Rect(MainTarget.width / 2, MainTarget.height - 4, 1, 1), 0, 0);   // bottom-left origin: near the top
                probe.Apply(false);
                FlipY = probe.GetPixel(0, 0).r < 0.5f;
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(probe); }
            Debug.Log("Last Wish canvas: " + SystemInfo.graphicsDeviceType + (FlipY ? ", flipped targets" : ", upright targets"));
        }

        private void DrawBatches(Pass pass)
        {
            for (var i = pass.FirstBatch; i < pass.EndBatch; i++)
            {
                var b = batches[i];
                if (!textures.TryGetValue(b.Texture, out var texture)) continue;
                block.SetTexture(mainTex, texture);
                var material = b.Blend == Blend.Add ? addMaterial : b.Blend == Blend.Multiply ? multiplyMaterial : b.Blend == Blend.AddMask ? addMaskMaterial
                    : b.Blend == Blend.Silhouette ? silhouetteMaterial : texture is RenderTexture ? premulMaterial : alphaMaterial;
                commands.DrawMesh(mesh, Matrix4x4.identity, material, i, 0, block);
            }
        }

        private void UploadMesh()
        {
            mesh.Clear(true);
            mesh.SetVertices(positions); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = batches.Count;
            mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Count, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            for (var i = 0; i < batches.Count; i++)
                mesh.SetSubMesh(i, new SubMeshDescriptor(batches[i].Start, batches[i].Count), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers);
            mesh.bounds = new Bounds(new Vector3(640, -360, 0), new Vector3(100000, 100000, 10));
        }

        public void Dispose()
        {
            commands.Release();
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(alphaMaterial); UnityEngine.Object.Destroy(addMaterial); UnityEngine.Object.Destroy(multiplyMaterial); UnityEngine.Object.Destroy(addMaskMaterial); UnityEngine.Object.Destroy(premulMaterial); UnityEngine.Object.Destroy(silhouetteMaterial);
            foreach (var t in textures.Values) if (t is RenderTexture rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
        }
    }
}

using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Pixel-art camera for the Built-in Render Pipeline: renders the scene into a small HDR target
    /// (640×360 by default, 1 texel = 1 art pixel), then a presenter camera upscales it with sharp-bilinear
    /// sampling and adds a smooth bloom layer computed from the HDR values above the threshold.
    /// The crisp pixel core and the soft glow are separate layers, so turning bloom off keeps the pixel art intact.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelGlowCamera : MonoBehaviour
    {
        [Header("Capa de píxeles")]
        [Tooltip("Resolución interna en píxeles de arte (16:9).")]
        public int referenceWidth = 640;
        public int referenceHeight = 360;
        [Tooltip("Píxeles de arte por unidad de mundo (los sprites y el atlas usan 16).")]
        public float pixelsPerUnit = 16f;
        [Tooltip("Objetivo HDR (ARGBHalf) para que las partículas superen 1.0 y alimenten el bloom.")]
        public bool hdr = true;

        [Header("Bloom")]
        public bool bloom = true;
        [Min(0f)] public float bloomThreshold = 1.15f;
        [Range(0f, 1f)] public float bloomKnee = 0.35f;
        [Min(0f)] public float bloomIntensity = 0.85f;
        [Range(0.05f, 1f)] public float bloomScatter = 0.75f;
        [Range(1, 8)] public int bloomIterations = 6;
        [ColorUsage(false, true)] public Color bloomTint = Color.white;
        [Tooltip("Límite de brillo que entra al bloom (evita destellos de un solo píxel).")]
        [Min(1f)] public float bloomClamp = 20f;

        [Header("Tono")]
        [Tooltip("Cuánto se blanquean los núcleos por encima de 1.0 (núcleo blanco aunque el bloom esté apagado).")]
        [Range(0f, 1f)] public float hotWhite = 0.45f;
        [Range(0f, 1f)] public float vignette = 0.22f;

        [Header("Shaders (asignados por el constructor)")]
        public Shader bloomShader;
        public Shader compositeShader;

        public Camera PixelCamera { get; private set; }
        public RenderTexture PixelTarget { get; private set; }

        private Material bloomMaterial, compositeMaterial;
        private Camera presenter;
        private readonly RenderTexture[] levels = new RenderTexture[10];

        private static readonly int FilterId = Shader.PropertyToID("_Filter");
        private static readonly int ScatterId = Shader.PropertyToID("_Scatter");
        private static readonly int ClampId = Shader.PropertyToID("_Clamp");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int LetterboxId = Shader.PropertyToID("_Letterbox");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int BloomEnabledId = Shader.PropertyToID("_BloomEnabled");
        private static readonly int HotWhiteId = Shader.PropertyToID("_HotWhite");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");

        public float OrthographicSize => referenceHeight / (2f * pixelsPerUnit);

        private void OnEnable()
        {
            PixelCamera = GetComponent<Camera>();
            PixelCamera.orthographic = true;
            PixelCamera.orthographicSize = OrthographicSize;
            PixelCamera.allowMSAA = false;
            PixelCamera.allowHDR = hdr;
            if (bloomShader == null) bloomShader = Shader.Find("Hidden/LastWish/VFX/Bloom");
            if (compositeShader == null) compositeShader = Shader.Find("Hidden/LastWish/VFX/Composite");
            if (bloomShader != null) bloomMaterial = new Material(bloomShader) { hideFlags = HideFlags.HideAndDontSave };
            if (compositeShader != null) compositeMaterial = new Material(compositeShader) { hideFlags = HideFlags.HideAndDontSave };
            CreateTarget();
            CreatePresenter();
        }

        private void OnDisable()
        {
            if (PixelCamera != null) PixelCamera.targetTexture = null;
            if (PixelTarget != null) { PixelTarget.Release(); Destroy(PixelTarget); PixelTarget = null; }
            if (presenter != null) Destroy(presenter.gameObject);
            if (bloomMaterial != null) Destroy(bloomMaterial);
            if (compositeMaterial != null) Destroy(compositeMaterial);
        }

        private void CreateTarget()
        {
            var format = hdr && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            PixelTarget = new RenderTexture(referenceWidth, referenceHeight, 24, format)
            {
                name = "VfxLab pixel layer",
                filterMode = FilterMode.Bilinear,     // sharp-bilinear upscale in the composite keeps the pixels crisp
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave,
            };
            PixelTarget.Create();
            PixelCamera.targetTexture = PixelTarget;
        }

        private void CreatePresenter()
        {
            var go = new GameObject("VfxLab Presenter", typeof(Camera), typeof(PixelGlowPresenter)) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            presenter = go.GetComponent<Camera>();
            presenter.cullingMask = 0;
            presenter.clearFlags = CameraClearFlags.SolidColor;
            presenter.backgroundColor = Color.black;
            presenter.orthographic = true;
            presenter.depth = PixelCamera.depth + 1;
            presenter.allowHDR = false;
            presenter.allowMSAA = false;
            presenter.useOcclusionCulling = false;
            go.GetComponent<PixelGlowPresenter>().Owner = this;
        }

        private void LateUpdate()
        {
            if (PixelCamera != null && !Mathf.Approximately(PixelCamera.orthographicSize, OrthographicSize))
                PixelCamera.orthographicSize = OrthographicSize;
        }

        /// <summary>Upscale the pixel layer (with bloom) into <paramref name="destination"/> (null = screen).</summary>
        public void Composite(RenderTexture destination, int width, int height)
        {
            if (PixelTarget == null || compositeMaterial == null) { Graphics.Blit(PixelTarget, destination); return; }
            RenderTexture glow = null;
            var useBloom = bloom && bloomMaterial != null && bloomIntensity > 0f;
            if (useBloom) glow = RenderBloom(PixelTarget);
            compositeMaterial.SetTexture(BloomTexId, glow != null ? (Texture)glow : Texture2D.blackTexture);
            compositeMaterial.SetFloat(BloomEnabledId, glow != null ? 1f : 0f);
            compositeMaterial.SetFloat(BloomIntensityId, bloomIntensity);
            compositeMaterial.SetColor(BloomTintId, bloomTint);
            compositeMaterial.SetFloat(HotWhiteId, hotWhite);
            compositeMaterial.SetFloat(VignetteId, vignette);
            var scale = Mathf.Min(width / (float)referenceWidth, height / (float)referenceHeight);
            float pw = referenceWidth * scale, ph = referenceHeight * scale;
            compositeMaterial.SetVector(LetterboxId, new Vector4((width - pw) * 0.5f / width, (height - ph) * 0.5f / height, pw / width, ph / height));
            compositeMaterial.SetFloat(ScaleId, scale);
            Graphics.Blit(PixelTarget, destination, compositeMaterial, 0);
            if (glow != null) RenderTexture.ReleaseTemporary(glow);
        }

        /// <summary>Composite into a render texture of any size (captures, thumbnails).</summary>
        public void CompositeTo(RenderTexture destination) => Composite(destination, destination.width, destination.height);

        private RenderTexture RenderBloom(RenderTexture source)
        {
            var knee = Mathf.Max(bloomThreshold * bloomKnee, 1e-4f);
            bloomMaterial.SetVector(FilterId, new Vector4(bloomThreshold, bloomThreshold - knee, 2f * knee, 0.25f / knee));
            bloomMaterial.SetFloat(ScatterId, bloomScatter);
            bloomMaterial.SetFloat(ClampId, bloomClamp);
            int w = source.width, h = source.height;
            var format = source.format;
            levels[0] = RenderTexture.GetTemporary(w, h, 0, format);
            levels[0].filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, levels[0], bloomMaterial, 0);
            var count = 1;
            for (; count < Mathf.Min(bloomIterations + 1, levels.Length); count++)
            {
                w /= 2; h /= 2;
                if (w < 2 || h < 2) break;
                levels[count] = RenderTexture.GetTemporary(w, h, 0, format);
                levels[count].filterMode = FilterMode.Bilinear;
                Graphics.Blit(levels[count - 1], levels[count], bloomMaterial, 1);
            }
            for (var i = count - 1; i > 0; i--)
            {
                Graphics.Blit(levels[i], levels[i - 1], bloomMaterial, 2);
                RenderTexture.ReleaseTemporary(levels[i]);
                levels[i] = null;
            }
            var result = levels[0];
            levels[0] = null;
            return result;
        }

        /// <summary>Screen pixel (bottom-left origin, e.g. the mouse) to a world point on the z = 0 plane.</summary>
        public Vector3 ScreenToWorld(Vector2 screen)
        {
            var cam = PixelCamera != null ? PixelCamera : GetComponent<Camera>();
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            var scale = Mathf.Min(sw / referenceWidth, sh / referenceHeight);
            float pw = referenceWidth * scale, ph = referenceHeight * scale;
            var vp = new Vector2((screen.x - (sw - pw) * 0.5f) / pw, (screen.y - (sh - ph) * 0.5f) / ph);
            var p = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, -cam.transform.position.z));
            p.z = 0f;
            return p;
        }

        /// <summary>Round a world position to the art-pixel grid (keeps moving sprites crisp).</summary>
        public static Vector3 Snap(Vector3 p, float ppu = 16f)
        {
            return new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, p.z);
        }
    }
}

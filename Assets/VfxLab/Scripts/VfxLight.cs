using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Fake 2D point light for the Built-in pipeline (Light2D needs URP): a sprite drawn with
    /// LastWish/VFX/Light2D that multiplies-and-adds onto the sprites behind it. It costs nothing while its level
    /// is 0 (the renderer is disabled), so effects only light the scene while they are visible.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class VfxLight : MonoBehaviour
    {
        /// <summary>Global toggle for the demo (L key).</summary>
        public static bool GloballyEnabled = true;
        /// <summary>
        /// Light budget: these lights multiply onto the scene (dst * (1 + light)), so overlapping lights compound.
        /// When their summed level exceeds this value every light is scaled down to fit it.
        /// </summary>
        public static float MaxTotalLevel = 1.6f;
        private static float requestedThisFrame, requestedLastFrame;
        private static int lastFrame = -1;

        public Texture2D falloff;
        [ColorUsage(false)] public Color color = new Color(0.35f, 0.85f, 1f);
        [Tooltip("Radio en unidades de mundo (16 px = 1).")]
        [Min(0.1f)] public float radius = 5f;
        [Min(0f)] public float intensity = 1f;
        [Range(0f, 1f)] public float flicker = 0.08f;
        public float flickerSpeed = 22f;
        [Tooltip("Forma del pulso (0..1 en tiempo, valor multiplicado por el pico).")]
        public AnimationCurve pulseShape = new AnimationCurve(new Keyframe(0, 1, 0, -1.5f), new Keyframe(1, 0, -0.5f, 0));

        private SpriteRenderer sr;
        private MaterialPropertyBlock block;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static Sprite cachedSprite;
        private static Texture2D cachedFor;
        private float level, pulsePeak, pulseTime = -1f, pulseDuration = 1f, seed;

        public float Level => level;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GloballyEnabled = true; requestedThisFrame = requestedLastFrame = 0f; lastFrame = -1; cachedSprite = null; cachedFor = null; }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
            seed = Random.value * 100f;
            if (falloff != null)
            {
                if (cachedSprite == null || cachedFor != falloff)
                {
                    cachedSprite = Sprite.Create(falloff, new Rect(0, 0, falloff.width, falloff.height), new Vector2(0.5f, 0.5f), 16f);
                    cachedSprite.hideFlags = HideFlags.DontSave;
                    cachedFor = falloff;
                }
                sr.sprite = cachedSprite;
            }
            sr.enabled = false;
        }

        private void OnEnable() { level = 0f; pulseTime = -1f; if (sr != null) sr.enabled = false; }

        /// <summary>Hold the light at a level (0 turns it off).</summary>
        public void SetLevel(float value) { level = Mathf.Max(0f, value); pulseTime = -1f; }

        /// <summary>Flash to <paramref name="peak"/> and fade following <see cref="pulseShape"/>.</summary>
        public void Pulse(float peak, float duration)
        {
            pulsePeak = peak; pulseDuration = Mathf.Max(0.01f, duration); pulseTime = 0f;
        }

        private void LateUpdate()
        {
            var current = level;
            if (pulseTime >= 0f)
            {
                pulseTime += Time.deltaTime;
                var k = pulseTime / pulseDuration;
                current = Mathf.Max(current, k >= 1f ? 0f : pulseShape.Evaluate(k) * pulsePeak);
                if (k >= 1f) pulseTime = -1f;
            }
            if (Time.frameCount != lastFrame) { requestedLastFrame = requestedThisFrame; requestedThisFrame = 0f; lastFrame = Time.frameCount; }
            requestedThisFrame += current * intensity;
            if (requestedLastFrame > MaxTotalLevel) current *= MaxTotalLevel / requestedLastFrame;
            var on = GloballyEnabled && current > 0.002f && sr.sprite != null;
            sr.enabled = on;
            if (!on) return;
            var n = 1f - flicker + flicker * Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f;
            sr.GetPropertyBlock(block);
            block.SetFloat(IntensityId, current * intensity * n);
            sr.SetPropertyBlock(block);
            sr.color = color;
            var size = sr.sprite.bounds.size.x;
            var s = radius * 2f / Mathf.Max(0.01f, size);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}

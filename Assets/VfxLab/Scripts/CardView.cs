using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// One card of the lab hand: static frame, animated pixel illustration with its emissive layer (HDR),
    /// ambient particles (motes, glints, rising sparks), foil sheen, hover outline and a burst when played.
    /// Positions are snapped to the art-pixel grid so the card stays crisp while it moves.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [Header("Identidad")]
        public string cardId = "punalada";
        public string displayName = "Puñalada";

        [Header("Capas")]
        public SpriteRenderer baseRenderer;
        public SpriteRenderer baseGlowRenderer;
        public SpriteSheetAnimator art;
        public SpriteSheetAnimator artGlow;
        public SpriteRenderer outlineRenderer;
        public SpriteRenderer sheenRenderer;
        public Texture2D baseTexture, baseGlowTexture, outlineTexture;

        [Header("Partículas")]
        public ParticleSystem motes;
        public ParticleSystem glints;
        public ParticleSystem rising;
        public ParticleSystem playBurst;

        [Header("Comportamiento")]
        [Tooltip("Elevación al pasar el ratón, en píxeles de arte.")]
        public float hoverLiftPixels = 10f;
        public float liftSpeed = 14f;
        [Tooltip("Intensidad HDR de la capa emisiva en reposo / al pasar el ratón.")]
        public float glowIdle = 1.5f;
        public float glowHover = 2.4f;
        [Tooltip("Multiplicador de emisión de las partículas ambientales al pasar el ratón.")]
        public float hoverEmission = 2.5f;
        [ColorUsage(false)] public Color outlineColor = new Color(0.45f, 0.95f, 1f);
        public float pixelsPerUnit = 16f;

        public bool Hovered { get; set; }

        private Vector3 rest;
        private float lift, kick, glowNow, sheenBoost;
        private float motesRate, glintRate, risingRate;
        private MaterialPropertyBlock block;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int BoostId = Shader.PropertyToID("_Boost");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            rest = transform.localPosition;
            AssignSprite(baseRenderer, baseTexture);
            AssignSprite(baseGlowRenderer, baseGlowTexture);
            AssignSprite(sheenRenderer, baseTexture);
            AssignSprite(outlineRenderer, outlineTexture);
            if (motes != null) motesRate = motes.emission.rateOverTimeMultiplier;
            if (glints != null) glintRate = glints.emission.rateOverTimeMultiplier;
            if (rising != null) risingRate = rising.emission.rateOverTimeMultiplier;
            glowNow = glowIdle;
            if (sheenRenderer != null)
            {
                sheenRenderer.GetPropertyBlock(block);
                block.SetFloat(PhaseId, Mathf.Repeat(rest.x * 0.13f, 1f));
                sheenRenderer.SetPropertyBlock(block);
            }
        }

        private void AssignSprite(SpriteRenderer r, Texture2D tex)
        {
            if (r == null || tex == null) return;
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.DontSave;
            r.sprite = s;
        }

        public Bounds WorldBounds
        {
            get
            {
                var c = transform.position;
                var size = baseTexture != null ? new Vector2(baseTexture.width, baseTexture.height) / pixelsPerUnit : new Vector2(6.5f, 9f);
                return new Bounds(new Vector3(c.x, c.y - lift / pixelsPerUnit * 0.5f, 0f), new Vector3(size.x, size.y + lift / pixelsPerUnit, 1f));
            }
        }

        /// <summary>Card played: kick up, flash the foil and burst sparks out of the frame.</summary>
        public void Play()
        {
            kick = 1f;
            sheenBoost = 1.6f;
            if (playBurst != null) playBurst.Play(true);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var targetLift = (Hovered ? hoverLiftPixels : 0f) + kick * 8f;
            lift = Mathf.Lerp(lift, targetLift, 1f - Mathf.Exp(-liftSpeed * dt));
            kick = Mathf.MoveTowards(kick, 0f, dt * 3.5f);
            sheenBoost = Mathf.MoveTowards(sheenBoost, Hovered ? 0.25f : 0f, dt * 2.5f);
            transform.localPosition = PixelGlowCamera.Snap(rest + new Vector3(0f, lift / pixelsPerUnit, 0f), pixelsPerUnit);

            glowNow = Mathf.Lerp(glowNow, Hovered ? glowHover : glowIdle, 1f - Mathf.Exp(-8f * dt));
            var pulse = 1f + 0.08f * Mathf.Sin(Time.time * 2.4f + rest.x);
            SetIntensity(artGlow != null ? artGlow.GetComponent<SpriteRenderer>() : null, glowNow * pulse);
            SetIntensity(baseGlowRenderer, (glowNow * 0.8f) * pulse);
            if (outlineRenderer != null)
            {
                var a = Mathf.Clamp01((lift - 1f) / Mathf.Max(1f, hoverLiftPixels - 1f)) + kick;
                outlineRenderer.enabled = a > 0.02f;
                outlineRenderer.color = outlineColor;
                SetIntensity(outlineRenderer, Mathf.Clamp01(a) * 0.9f);
            }
            if (sheenRenderer != null)
            {
                sheenRenderer.GetPropertyBlock(block);
                block.SetFloat(BoostId, sheenBoost);
                sheenRenderer.SetPropertyBlock(block);
            }
            var m = Hovered ? hoverEmission : 1f;
            SetRate(motes, motesRate * m);
            SetRate(glints, glintRate * m);
            SetRate(rising, risingRate * m);
        }

        private void SetIntensity(SpriteRenderer r, float value)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetFloat(IntensityId, value);
            r.SetPropertyBlock(block);
        }

        private static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTimeMultiplier = rate;
        }
    }
}

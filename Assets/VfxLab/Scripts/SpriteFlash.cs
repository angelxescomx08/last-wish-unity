using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>Hit reaction for a sprite drawn with LastWish/VFX/Sprite: white flash plus a short pixel shake.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlash : MonoBehaviour
    {
        public Color flashColor = new Color(0.6f, 0.92f, 1f);
        public float flashTime = 0.09f;
        [Tooltip("Mezcla máxima hacia el color del destello (1 = silueta plana; menos conserva el detalle del sprite).")]
        [Range(0f, 1f)] public float flashAmount = 0.55f;
        [Tooltip("Amplitud del temblor en píxeles de arte.")]
        public float shakePixels = 2f;
        public float shakeTime = 0.18f;
        public float pixelsPerUnit = 16f;

        private SpriteRenderer sr;
        private MaterialPropertyBlock block;
        private float flash, shake;
        private Vector3 rest;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private void Awake() { sr = GetComponent<SpriteRenderer>(); block = new MaterialPropertyBlock(); rest = transform.localPosition; }

        public void Hit(float strength = 1f) { flash = flashTime * strength; shake = shakeTime * strength; }

        private void LateUpdate()
        {
            flash = Mathf.Max(0f, flash - Time.deltaTime);
            shake = Mathf.Max(0f, shake - Time.deltaTime);
            sr.GetPropertyBlock(block);
            block.SetFloat(FlashId, flashTime > 0 ? Mathf.Clamp01(flash / flashTime) * flashAmount : 0f);
            block.SetColor(FlashColorId, flashColor);
            sr.SetPropertyBlock(block);
            if (shake > 0f)
            {
                var k = shake / Mathf.Max(0.01f, shakeTime);
                var px = Mathf.Round(Mathf.Sin(Time.time * 90f) * shakePixels * k);
                transform.localPosition = rest + new Vector3(px / pixelsPerUnit, 0f, 0f);
            }
            else transform.localPosition = rest;
        }
    }
}

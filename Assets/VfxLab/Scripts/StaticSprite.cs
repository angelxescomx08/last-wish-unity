using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>Builds a pixel sprite from a texture at runtime (no sprite import/slicing settings needed).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StaticSprite : MonoBehaviour
    {
        public Texture2D texture;
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public float pixelsPerUnit = 16f;

        private void Awake()
        {
            if (texture == null) return;
            var s = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.DontSave;
            GetComponent<SpriteRenderer>().sprite = s;
        }
    }
}

using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>Camera that draws nothing itself; it presents the <see cref="PixelGlowCamera"/> layer on screen.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelGlowPresenter : MonoBehaviour
    {
        public PixelGlowCamera Owner;
        private Camera cam;

        private void Awake() => cam = GetComponent<Camera>();

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (Owner == null || Owner.PixelTarget == null) { Graphics.Blit(source, destination); return; }
            int w = destination != null ? destination.width : cam.pixelWidth;
            int h = destination != null ? destination.height : cam.pixelHeight;
            Owner.Composite(destination, w, h);
        }
    }
}

using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>Tiny pixel-snapped camera shake for impacts (moves this transform; put it on the camera rig).</summary>
    public sealed class CameraShake : MonoBehaviour
    {
        public static bool GloballyEnabled = true;
        public float pixelsPerUnit = 16f;
        private float amplitude, time, duration;
        private Vector3 rest;

        private void Awake() => rest = transform.localPosition;

        /// <summary>Shake by <paramref name="pixels"/> art pixels for <paramref name="seconds"/>.</summary>
        public void Kick(float pixels, float seconds)
        {
            if (!GloballyEnabled) return;
            if (pixels >= amplitude * (1f - time / Mathf.Max(duration, 0.001f))) { amplitude = pixels; duration = seconds; time = 0f; }
        }

        private void LateUpdate()
        {
            if (time >= duration) { transform.localPosition = rest; return; }
            time += Time.unscaledDeltaTime;
            var k = 1f - Mathf.Clamp01(time / duration);
            var a = amplitude * k * k;
            var x = Mathf.Round((Mathf.PerlinNoise(time * 40f, 0.3f) * 2f - 1f) * a);
            var y = Mathf.Round((Mathf.PerlinNoise(0.7f, time * 40f) * 2f - 1f) * a);
            transform.localPosition = rest + new Vector3(x / pixelsPerUnit, y / pixelsPerUnit, 0f);
        }
    }
}

using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>Returns its object to <see cref="VfxPool"/> once every child particle system is done (or on timeout).</summary>
    [DisallowMultipleComponent]
    public sealed class PooledVfx : MonoBehaviour
    {
        [Tooltip("Devolver al pool automáticamente cuando no quedan partículas vivas.")]
        public bool autoRelease = true;
        [Tooltip("Tiempo mínimo antes de comprobar si terminó.")]
        public float minTime = 0.25f;
        [Tooltip("Tiempo máximo de vida (seguridad).")]
        public float maxTime = 6f;

        public GameObject Prefab { get; internal set; }
        /// <summary>Set by sequencers while they still drive the effect, so it is not released mid-sequence.</summary>
        public bool Busy { get; set; }

        private ParticleSystem[] systems;
        private float timer;

        private void Awake() => systems = GetComponentsInChildren<ParticleSystem>(true);

        private void OnEnable() => timer = 0f;

        private void Update()
        {
            if (!autoRelease) return;
            timer += Time.deltaTime;
            if (timer > maxTime) { Release(); return; }
            if (Busy || timer < minTime) return;
            foreach (var s in systems) if (s != null && s.IsAlive(false)) return;
            Release();
        }

        public void Release()
        {
            Busy = false;
            if (Prefab != null) VfxPool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}

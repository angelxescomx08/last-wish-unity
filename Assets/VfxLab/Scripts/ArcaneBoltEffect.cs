using System;
using System.Collections;
using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Sequencer of the cyan arcane bolt: anticipation (charge at the caster) → launch → travel along an arc
    /// → impact → dissipation. Each phase owns its own particle layers (core, glow, sparks, trails, impact) so they
    /// can be tuned in the prefab; timings, arc and lights are editable here. Pooled through <see cref="VfxPool"/>.
    /// </summary>
    [RequireComponent(typeof(PooledVfx))]
    public sealed class ArcaneBoltEffect : MonoBehaviour
    {
        [Header("Fases (segundos)")]
        [Tooltip("Carga en la mano antes de lanzar (anticipación).")]
        [Min(0f)] public float anticipation = 0.38f;
        [Tooltip("Duración del vuelo hasta el objetivo.")]
        [Min(0.05f)] public float travelTime = 0.3f;
        [Tooltip("Altura del arco en unidades (16 px = 1).")]
        public float arcHeight = 1.2f;
        [Tooltip("Aceleración del vuelo (1 = constante, >1 arranca lento y llega rápido).")]
        [Range(0.5f, 2.5f)] public float travelEase = 1.35f;

        [Header("Capas")]
        public Transform chargeRoot;
        public ParticleSystem[] chargeSystems;
        public Transform launchRoot;
        public ParticleSystem[] launchSystems;
        public Transform projectileRoot;
        [Tooltip("Capas locales del proyectil (núcleo y halo) que se apagan de golpe al impactar.")]
        public ParticleSystem[] projectileHeads;
        public TrailRenderer projectileTrail;
        [Tooltip("Estela de píxeles en espacio de mundo: se emite a lo largo del recorrido (espaciado exacto, sin saltos al reutilizar).")]
        public ParticleSystem wake;
        [Min(0f)] public float wakePerUnit = 10f;
        public float wakeJitter = 0.07f;
        [Tooltip("Chispas que se desprenden durante el vuelo.")]
        public ParticleSystem wakeSparks;
        [Min(0f)] public float sparksPerUnit = 1.4f;
        public Transform impactRoot;
        public ParticleSystem[] impactSystems;
        [Tooltip("Capas del impacto que no se repiten si otro impacto ocurrió hace menos de 'impactFlashCooldown' (destello y halo).")]
        public ParticleSystem[] impactFlashSystems;
        [Tooltip("Legibilidad con muchos efectos: segundos durante los que un nuevo impacto omite el destello y el halo.")]
        [Min(0f)] public float impactFlashCooldown = 0.12f;
        private static float lastFlashTime = -10f;

        [Header("Luces (Light2D simulada)")]
        public VfxLight chargeLight;
        public float chargeLightPeak = 0.55f;
        public VfxLight projectileLight;
        public float projectileLightLevel = 0.45f;
        public VfxLight impactLight;
        public float impactLightPeak = 0.6f;
        public float impactLightTime = 0.5f;

        /// <summary>Raised at the moment of impact with the impact position.</summary>
        public event Action<Vector3> Impact;
        public bool Running { get; private set; }

        private PooledVfx pooled;
        private Coroutine routine;

        private void Awake() => pooled = GetComponent<PooledVfx>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lastFlashTime = -10f;

        private void OnDisable()
        {
            Running = false;
            Impact = null;
        }

        /// <summary>Play the whole sequence from <paramref name="from"/> (caster hand) to <paramref name="to"/>.</summary>
        public void Fire(Vector3 from, Vector3 to, Action<Vector3> onImpact = null)
        {
            if (routine != null) StopCoroutine(routine);
            Impact = onImpact;
            routine = StartCoroutine(Run(from, to));
        }

        private IEnumerator Run(Vector3 from, Vector3 to)
        {
            Running = true;
            pooled.Busy = true;
            transform.position = Vector3.zero;
            var dir = (to - from);
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 1) anticipation: sparks and runes converge into a growing core at the hand
            projectileRoot.gameObject.SetActive(false);
            chargeRoot.position = from;
            foreach (var s in chargeSystems) s.Play(true);
            for (float t = 0; t < anticipation; t += Time.deltaTime)
            {
                if (chargeLight != null) chargeLight.SetLevel(chargeLightPeak * Mathf.SmoothStep(0f, 1f, t / Mathf.Max(anticipation, 0.001f)));
                yield return null;
            }
            foreach (var s in chargeSystems) s.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (chargeLight != null) chargeLight.Pulse(chargeLightPeak * 1.3f, 0.25f);

            // 2) launch burst pointing at the target
            launchRoot.SetPositionAndRotation(from, Quaternion.Euler(0, 0, angle));
            foreach (var s in launchSystems) s.Play(true);

            // 3) travel along a quadratic arc; the head faces its velocity
            projectileRoot.position = from;
            projectileRoot.gameObject.SetActive(true);
            if (projectileTrail != null) { projectileTrail.Clear(); projectileTrail.emitting = true; }
            foreach (var s in projectileHeads) s.Play(true);
            if (wake != null) wake.Play(true);
            if (wakeSparks != null) wakeSparks.Play(true);
            var control = (from + to) * 0.5f + Vector3.up * arcHeight;
            var previous = from;
            float wakeCarry = 0f, sparkCarry = 0f;
            for (float t = 0; t < travelTime; t += Time.deltaTime)
            {
                var k = Mathf.Pow(Mathf.Clamp01(t / travelTime), travelEase);
                var p = Bezier(from, control, to, k);
                var v = p - previous;
                if (v.sqrMagnitude > 1e-6f) projectileRoot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                projectileRoot.position = p;
                EmitAlong(wake, previous, p, wakePerUnit, ref wakeCarry, wakeJitter);
                EmitAlong(wakeSparks, previous, p, sparksPerUnit, ref sparkCarry, 0.1f);
                previous = p;
                if (projectileLight != null) projectileLight.SetLevel(projectileLightLevel);
                yield return null;
            }
            EmitAlong(wake, previous, to, wakePerUnit, ref wakeCarry, wakeJitter);
            projectileRoot.position = to;

            // 4) impact
            foreach (var s in projectileHeads) s.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (wake != null) wake.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (wakeSparks != null) wakeSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (projectileTrail != null) projectileTrail.emitting = false;
            if (projectileLight != null) projectileLight.SetLevel(0f);
            impactRoot.SetPositionAndRotation(to, Quaternion.Euler(0, 0, angle));
            var flash = Time.time - lastFlashTime >= impactFlashCooldown;
            foreach (var s in impactSystems)
                if (flash || impactFlashSystems == null || System.Array.IndexOf(impactFlashSystems, s) < 0) s.Play(true);
            if (flash) lastFlashTime = Time.time;
            if (impactLight != null) { impactLight.transform.position = to; impactLight.Pulse(impactLightPeak * (flash ? 1f : 0.4f), impactLightTime); }
            Impact?.Invoke(to);

            // 5) dissipation: wait for the world-space layers to fade, then go back to the pool
            Running = false;
            pooled.Busy = false;
            routine = null;
        }

        private static void EmitAlong(ParticleSystem ps, Vector3 a, Vector3 b, float perUnit, ref float carry, float jitter)
        {
            if (ps == null || perUnit <= 0f) return;
            carry += Vector3.Distance(a, b) * perUnit;
            var n = Mathf.FloorToInt(carry);
            if (n <= 0) return;
            carry -= n;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (var i = 1; i <= n; i++)
            {
                var j = UnityEngine.Random.insideUnitCircle * jitter;
                ep.position = Vector3.Lerp(a, b, i / (float)n) + new Vector3(j.x, j.y, 0f);
                ps.Emit(ep, 1);
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}

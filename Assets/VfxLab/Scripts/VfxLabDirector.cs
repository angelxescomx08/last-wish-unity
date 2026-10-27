using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Demo scene controller: the rogue plays her basic cards and casts the cyan arcane bolt at the golem.
    /// Keys: 1-3 or click a card, Space bolt, B bloom, L lights, K shake, T slow motion, S stress test, H help.
    /// </summary>
    public sealed class VfxLabDirector : MonoBehaviour
    {
        [Header("Escena")]
        public PixelGlowCamera pixelCamera;
        public CameraShake cameraShake;
        public CardView[] cards;
        public SpriteSheetAnimator hero;
        public Transform heroHand;
        public SpriteSheetAnimator target;
        public SpriteFlash targetFlash;
        public Transform targetCenter;

        [Header("Efecto")]
        public GameObject boltPrefab;
        [Min(0)] public int prewarm = 6;
        [Tooltip("Retraso entre empezar la animación de ataque y soltar el proyectil (la carga ocurre durante el impulso).")]
        public float castDelay = 0.0f;
        [Tooltip("Desviación aleatoria del punto de impacto (unidades).")]
        public float impactSpread = 0.6f;
        [Tooltip("Retraso de la animación de daño del objetivo: deja leer el destello del impacto antes del parpadeo blanco del sprite.")]
        public float targetHurtDelay = 0.05f;
        [Tooltip("Fotograma inicial de la animación de daño (el 0 del gólem es una silueta blanca completa).")]
        public int targetHurtStartFrame = 2;
        public float shakePixels = 2f;
        public float shakeTime = 0.16f;

        [Header("Demo")]
        [Range(0.05f, 1f)] public float slowMotion = 0.25f;
        public bool showHelp = true;
        [Tooltip("Juega las cartas solas en bucle (útil para grabar).")]
        public bool autoPlay;
        public float autoPlayInterval = 2.2f;
        [Tooltip("Resaltar cartas con el ratón (las capturas automáticas lo desactivan).")]
        public bool mouseHover = true;

        private readonly List<ParticleSystem> allSystems = new List<ParticleSystem>();
        private float statsTimer, fpsTime, fps, autoTimer;
        private int fpsFrames, liveParticles, activeEffects;
        private bool slow;
        private int nextAuto;
        private GUIStyle style, shadow;

        private void Start()
        {
            if (boltPrefab != null) VfxPool.Prewarm(boltPrefab, prewarm);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) PlayCard(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) PlayCard(1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) PlayCard(2);
                if (kb.spaceKey.wasPressedThisFrame) Cast();
                if (kb.bKey.wasPressedThisFrame && pixelCamera != null) pixelCamera.bloom = !pixelCamera.bloom;
                if (kb.lKey.wasPressedThisFrame) VfxLight.GloballyEnabled = !VfxLight.GloballyEnabled;
                if (kb.kKey.wasPressedThisFrame) CameraShake.GloballyEnabled = !CameraShake.GloballyEnabled;
                if (kb.tKey.wasPressedThisFrame) { slow = !slow; Time.timeScale = slow ? slowMotion : 1f; }
                if (kb.sKey.wasPressedThisFrame) StartCoroutine(Stress());
                if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
                if (kb.aKey.wasPressedThisFrame) autoPlay = !autoPlay;
            }
            if (mouseHover && mouse != null && pixelCamera != null && cards != null)
            {
                var world = pixelCamera.ScreenToWorld(mouse.position.ReadValue());
                for (var i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null) continue;
                    var b = cards[i].WorldBounds;
                    cards[i].Hovered = world.x >= b.min.x && world.x <= b.max.x && world.y >= b.min.y && world.y <= b.max.y;
                    if (cards[i].Hovered && mouse.leftButton.wasPressedThisFrame) PlayCard(i);
                }
            }
            if (autoPlay)
            {
                autoTimer += Time.deltaTime;
                if (autoTimer >= autoPlayInterval) { autoTimer = 0f; PlayCard(nextAuto); nextAuto = (nextAuto + 1) % Mathf.Max(1, cards.Length); }
            }
            UpdateStats();
        }

        /// <summary>Play card i: the card reacts and the rogue casts the bolt.</summary>
        public void PlayCard(int index)
        {
            if (cards == null || index < 0 || index >= cards.Length || cards[index] == null) return;
            cards[index].Play();
            Cast();
        }

        /// <summary>The rogue throws a cyan arcane bolt at the golem.</summary>
        public void Cast() => StartCoroutine(CastRoutine(heroHand != null ? heroHand.position : Vector3.zero, TargetPoint(), true));

        private Vector3 TargetPoint()
        {
            var p = targetCenter != null ? targetCenter.position : Vector3.zero;
            return p + (Vector3)(Random.insideUnitCircle * impactSpread);
        }

        private IEnumerator CastRoutine(Vector3 from, Vector3 to, bool animateHero)
        {
            if (boltPrefab == null) yield break;
            if (animateHero && hero != null) hero.Play("attack");
            if (castDelay > 0f) yield return new WaitForSeconds(castDelay);
            var bolt = VfxPool.Spawn<ArcaneBoltEffect>(boltPrefab, from);
            bolt.Fire(from, to, OnImpact);
        }

        private void OnImpact(Vector3 point)
        {
            if (targetFlash != null) targetFlash.Hit();
            if (target != null) StartCoroutine(HurtLater());
            if (cameraShake != null) cameraShake.Kick(shakePixels, shakeTime);
        }

        private IEnumerator HurtLater()
        {
            if (targetHurtDelay > 0f) yield return new WaitForSeconds(targetHurtDelay);
            target.PlayFromFrame("hurt", targetHurtStartFrame);
        }

        /// <summary>Legibility test: many simultaneous bolts and card bursts.</summary>
        public IEnumerator Stress()
        {
            for (var i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].Play();
            for (var i = 0; i < 8; i++)
            {
                var from = (heroHand != null ? heroHand.position : Vector3.zero) + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 3.5f), 0f);
                StartCoroutine(CastRoutine(from, TargetPoint() + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0), i == 0));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
            }
        }

        private void UpdateStats()
        {
            fpsTime += Time.unscaledDeltaTime; fpsFrames++;
            if (fpsTime >= 0.5f) { fps = fpsFrames / fpsTime; fpsTime = 0f; fpsFrames = 0; }
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer > 0f) return;
            statsTimer = 0.25f;
            allSystems.Clear();
            allSystems.AddRange(FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None));
            liveParticles = 0;
            foreach (var s in allSystems) liveParticles += s.particleCount;
            activeEffects = 0;
            foreach (var e in FindObjectsByType<ArcaneBoltEffect>(FindObjectsSortMode.None)) if (e.isActiveAndEnabled) activeEffects++;
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 60), richText = true };
                style.normal.textColor = new Color(0.85f, 0.95f, 1f);
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0, 0, 0, 0.8f);
            }
            var bloom = pixelCamera != null && pixelCamera.bloom;
            var text = $"<b>VFX Lab · Pícara</b>   FPS {fps:0}   partículas {liveParticles}   efectos {activeEffects}\n" +
                       $"Bloom {(bloom ? "ON" : "OFF")}   Luces {(VfxLight.GloballyEnabled ? "ON" : "OFF")}   Temblor {(CameraShake.GloballyEnabled ? "ON" : "OFF")}   {(slow ? "Cámara lenta" : "")}";
            if (showHelp)
                text += "\n1-3 / clic: jugar carta   Espacio: proyectil   S: prueba de estrés   B: bloom   L: luces   K: temblor   T: cámara lenta   A: auto   H: ocultar";
            var r = new Rect(12, 8, Screen.width - 24, 90);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, shadow);
            GUI.Label(r, text, style);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            VfxLight.GloballyEnabled = true;
            CameraShake.GloballyEnabled = true;
        }
    }
}

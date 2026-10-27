using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using PS = UnityEngine.ParticleSystem;

namespace LastWish.VfxLab.Editor
{
    /// <summary>
    /// Builds the VFX Lab: materials, the cyan arcane bolt prefab (core / glow / sparks / trails / impact layers),
    /// the three rogue card prefabs and the demo scene. Everything is regenerated from code so the setup is
    /// reproducible; after building, tune the prefabs in the Inspector (rebuilding overwrites them).
    /// Units: 1 world unit = 16 art pixels; the atlas tiles are 16 px, so a particle of size 1 is pixel-exact.
    /// </summary>
    public static class VfxLabBuilder
    {
        public const string Root = "Assets/VfxLab";
        public const string ArtDir = Root + "/Art";
        public const string MatDir = Root + "/Materials";
        public const string PrefabDir = Root + "/Prefabs";
        public const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/VfxLab.unity";
        public const string BoltPrefabPath = PrefabDir + "/VFX_ArcaneBolt_Cyan.prefab";
        private const float PPU = 16f;

        // Atlas rows (Tools/VfxLab/vfx_atlas.py)
        private const int RowSpark = 0, RowMote = 1, RowShapes = 2, RowImpact = 3, RowTwinkle = 4, RowRunes = 6;
        private const int TileBlob = RowShapes * 8 + 0, TileShard = RowShapes * 8 + 6;

        private static readonly Color Cyan = new Color(0.30f, 0.86f, 1f);
        private static readonly Color CyanPale = new Color(0.70f, 0.96f, 1f);
        private static readonly Color Blue = new Color(0.20f, 0.42f, 1f);
        private static readonly Color Violet = new Color(0.50f, 0.25f, 0.95f);
        private static readonly Color White = Color.white;

        private sealed class Mats
        {
            public Material add, addSoft, trail, alpha, spriteAdd, light, sprite, sheen;
            public Texture2D atlas, lightTex;
        }

        // ------------------------------------------------------------------------------------------ menu
        [MenuItem("Last Wish/VFX Lab/Construir escena y prefabs", priority = 100)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("VFX Lab: sal del modo Play antes de construir."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("VFX_LAB_BUILT " + ScenePath);
        }

        [MenuItem("Last Wish/VFX Lab/Jugar VFX Lab", priority = 101)]
        public static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) BuildAll();
            EditorSceneManager.OpenScene(ScenePath);
            ClearStartSceneForThisRun();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Last Wish/VFX Lab/Construir y capturar fotogramas", priority = 102)]
        public static void BuildAndCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndCaptureUnattended();
        }

        /// <summary>Rebuild and run the capture test without prompts (callers make sure no scene is dirty).</summary>
        internal static void BuildAndCaptureUnattended()
        {
            BuildAll();
            EditorSceneManager.OpenScene(ScenePath);
            ClearStartSceneForThisRun();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, assemblyNames = new[] { "LastWish.VfxLab.Tests" } }));
            Debug.Log("VFX_LAB_CAPTURE_STARTED -> TestResults/vfxlab");
        }

        // The project's EditorStartup makes Play always start Bootstrap (playModeStartScene). The lab clears it for
        // one run and restores it when the editor is back in Edit mode; SessionState survives the domain reload.
        private const string RestoreKey = "LastWish.VfxLab.RestoreStartScene";

        internal static void ClearStartSceneForThisRun()
        {
            var current = EditorSceneManager.playModeStartScene;
            if (!SessionState.GetBool(RestoreKey + ".pending", false))
                SessionState.SetString(RestoreKey, current != null ? AssetDatabase.GetAssetPath(current) : "");
            SessionState.SetBool(RestoreKey + ".pending", true);
            EditorSceneManager.playModeStartScene = null;
        }

        [InitializeOnLoadMethod]
        private static void HookStartSceneRestore()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey + ".pending", false)) return;
            var path = SessionState.GetString(RestoreKey, "");
            SessionState.EraseBool(RestoreKey + ".pending");
            SessionState.EraseString(RestoreKey);
            if (string.IsNullOrEmpty(path) && !Application.isBatchMode) path = "Assets/Scenes/Bootstrap.unity";   // what EditorStartup sets
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod LastWish.VfxLab.Editor.VfxLabBuilder.BuildBatch -quit</summary>
        public static void BuildBatch()
        {
            BuildAll();
            Debug.Log("VFX_LAB_BUILT " + ScenePath);
        }

        public static void BuildAll()
        {
            foreach (var d in new[] { MatDir, PrefabDir, SceneDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            var mats = BuildMaterials();
            var bolt = BuildBolt(mats);
            var cards = new List<GameObject>();
            foreach (var spec in CardSpecs) cards.Add(BuildCard(mats, spec));
            BuildScene(mats, bolt, cards);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------------------------------ assets
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new InvalidOperationException("VFX Lab: falta " + path + " (ejecuta Tools/VfxLab/generate_vfxlab_art.py).");
            return a;
        }

        private static Texture2D Art(string name) => Load<Texture2D>(ArtDir + "/" + name + ".png");

        private static Material Mat(string name, string shaderName, Texture tex, Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("VFX Lab: shader no encontrado " + shaderName);
            var path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            if (tex != null) m.mainTexture = tex;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mats BuildMaterials()
        {
            var m = new Mats { atlas = Art("vfx_atlas"), lightTex = Art("vfx_light") };
            var trailTex = Art("vfx_trail");
            m.add = Mat("VFX_Particles_Add", "LastWish/VFX/Additive", m.atlas, x => { x.SetFloat("_Intensity", 1f); x.SetFloat("_UseCustomIntensity", 1f); x.SetFloat("_Steps", 0f); });
            m.addSoft = Mat("VFX_Particles_AddSoft", "LastWish/VFX/Additive", m.atlas, x => { x.SetFloat("_Intensity", 1f); x.SetFloat("_UseCustomIntensity", 1f); x.SetFloat("_Steps", 4f); x.SetFloat("_Dither", 1f); });
            m.trail = Mat("VFX_Trail_Add", "LastWish/VFX/Additive", trailTex, x => { x.SetFloat("_Intensity", 1.25f); x.SetFloat("_UseCustomIntensity", 0f); x.SetFloat("_Steps", 3f); x.SetFloat("_Dither", 1f); });
            m.alpha = Mat("VFX_Particles_Alpha", "LastWish/VFX/Alpha", m.atlas, x => x.SetFloat("_DitherAlpha", 1f));
            m.spriteAdd = Mat("VFX_Sprite_Add", "LastWish/VFX/Additive", null, x => { x.SetFloat("_Intensity", 1.5f); x.SetFloat("_UseCustomIntensity", 0f); x.SetFloat("_Steps", 0f); });
            m.light = Mat("VFX_Light2D", "LastWish/VFX/Light2D", m.lightTex, x => { x.SetFloat("_Intensity", 1f); x.SetFloat("_Spill", 0.08f); });
            m.sprite = Mat("VFX_Sprite", "LastWish/VFX/Sprite", null, null);
            m.sheen = Mat("VFX_CardSheen", "LastWish/VFX/CardSheen", null, x => { x.SetColor("_Color", new Color(0.55f, 0.9f, 1f)); x.SetFloat("_Intensity", 0.22f); x.SetFloat("_Width", 0.09f); });
            return m;
        }

        // ------------------------------------------------------------------------------------------ particle helpers
        private static PS NewSystem(Transform parent, string name, Material mat, int order, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<PS>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.Custom1X });
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startRotation = 0f;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.rateOverDistance = 0f;
            em.SetBursts(Array.Empty<PS.Burst>());
            var sh = ps.shape;
            sh.enabled = false;
            var cd = ps.customData;
            cd.enabled = true;
            cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 1);
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new PS.MinMaxCurve(1f));
            var ts = ps.textureSheetAnimation;
            ts.enabled = true;
            ts.mode = ParticleSystemAnimationMode.Grid;
            ts.numTilesX = 8;
            ts.numTilesY = 8;
            ts.animation = ParticleSystemAnimationType.WholeSheet;
            ts.frameOverTime = new PS.MinMaxCurve(0f);
            ts.startFrame = new PS.MinMaxCurve(0f);
            ts.cycleCount = 1;
            return ps;
        }

        private static AnimationCurve Curve(params float[] tv)
        {
            var keys = new Keyframe[tv.Length / 2];
            for (var i = 0; i < keys.Length; i++) keys[i] = new Keyframe(tv[i * 2], tv[i * 2 + 1]);
            var c = new AnimationCurve(keys);
            for (var i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        private static Gradient Grad((float t, Color c)[] colors, (float t, float a)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (var i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (var i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return g;
        }

        private static void Life(PS ps, float min, float max) { var m = ps.main; m.startLifetime = new PS.MinMaxCurve(min, max); }
        private static void Speed(PS ps, float min, float max) { var m = ps.main; m.startSpeed = new PS.MinMaxCurve(min, max); }
        private static void Size(PS ps, float min, float max) { var m = ps.main; m.startSize = new PS.MinMaxCurve(min, max); }
        private static void StartColor(PS ps, Color a, Color b) { var m = ps.main; m.startColor = new PS.MinMaxGradient(a, b); }
        private static void World(PS ps) { var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.World; }
        private static void Duration(PS ps, float d, bool loop = false) { var m = ps.main; m.duration = d; m.loop = loop; }
        private static void Max(PS ps, int n) { var m = ps.main; m.maxParticles = n; }
        private static void Gravity(PS ps, float g) { var m = ps.main; m.gravityModifier = g; }

        private static void Rate(PS ps, float perSecond, float perUnit = 0f)
        {
            var e = ps.emission;
            e.rateOverTime = perSecond;
            e.rateOverDistance = perUnit;
        }

        private static void Burst(PS ps, params (float time, int count)[] bursts)
        {
            var e = ps.emission;
            var list = new PS.Burst[bursts.Length];
            for (var i = 0; i < bursts.Length; i++) list[i] = new PS.Burst(bursts[i].time, (short)bursts[i].count);
            e.SetBursts(list);
        }

        private static void Circle(PS ps, float radius, float thickness = 1f, float arc = 360f, float rotZ = 0f)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Circle;
            s.radius = radius;
            s.radiusThickness = thickness;
            s.arc = arc;
            s.arcMode = ParticleSystemShapeMultiModeValue.Random;
            s.rotation = new Vector3(0f, 0f, rotZ);
        }

        private static void Box(PS ps, Vector2 size, float rotZ = 0f, bool edge = false)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = edge ? ParticleSystemShapeType.BoxEdge : ParticleSystemShapeType.Box;
            s.scale = new Vector3(size.x, size.y, 0f);
            s.rotation = new Vector3(0f, 0f, rotZ);
        }

        /// <summary>Single atlas tile for the whole life.</summary>
        private static void Tile(PS ps, int tile)
        {
            var ts = ps.textureSheetAnimation;
            ts.animation = ParticleSystemAnimationType.WholeSheet;
            ts.startFrame = new PS.MinMaxCurve(tile);
            ts.frameOverTime = new PS.MinMaxCurve(0f);
        }

        /// <summary>Random tile in [first, last] for the whole life.</summary>
        private static void TileRandom(PS ps, int first, int last)
        {
            var ts = ps.textureSheetAnimation;
            ts.animation = ParticleSystemAnimationType.WholeSheet;
            ts.startFrame = new PS.MinMaxCurve(first, last + 0.99f);
            ts.frameOverTime = new PS.MinMaxCurve(0f);
        }

        /// <summary>Flipbook along one atlas row; from/to are normalized positions in the row (frame = floor(v * 8)).</summary>
        private static void Flipbook(PS ps, int row, float from, float to)
        {
            var ts = ps.textureSheetAnimation;
            ts.animation = ParticleSystemAnimationType.SingleRow;
            ts.rowMode = ParticleSystemAnimationRowMode.Custom;
            ts.rowIndex = row;
            ts.startFrame = new PS.MinMaxCurve(0f);
            ts.frameOverTime = new PS.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, Mathf.Min(to, 0.999f)));
        }

        private static void ColorLife(PS ps, Gradient g)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            c.color = new PS.MinMaxGradient(g);
        }

        private static void SizeLife(PS ps, AnimationCurve curve)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new PS.MinMaxCurve(1f, curve);
        }

        /// <summary>HDR intensity over the particle life (read by the shader from Custom1.x).</summary>
        private static void Intensity(PS ps, float start, float end)
        {
            var cd = ps.customData;
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new PS.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end)));
        }

        private static void IntensityCurve(PS ps, AnimationCurve curve)
        {
            var cd = ps.customData;
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new PS.MinMaxCurve(1f, curve));
        }

        private static void Velocity(PS ps, Vector2 xRange, Vector2 yRange, float orbital = 0f, float radial = 0f, bool world = false)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            v.x = new PS.MinMaxCurve(xRange.x, xRange.y);
            v.y = new PS.MinMaxCurve(yRange.x, yRange.y);
            v.z = new PS.MinMaxCurve(0f, 0f);
            v.orbitalX = new PS.MinMaxCurve(0f, 0f);
            v.orbitalY = new PS.MinMaxCurve(0f, 0f);
            v.orbitalZ = new PS.MinMaxCurve(orbital, orbital);
            v.radial = new PS.MinMaxCurve(radial, radial);
        }

        private static void Drag(PS ps, float drag)
        {
            var l = ps.limitVelocityOverLifetime;
            l.enabled = true;
            l.limit = new PS.MinMaxCurve(1000f);
            l.dampen = 0f;
            l.drag = new PS.MinMaxCurve(drag);
            l.multiplyDragByParticleSize = false;
            l.multiplyDragByParticleVelocity = false;
        }

        private static void Noise(PS ps, float strength, float frequency, float scroll = 0.6f)
        {
            var n = ps.noise;
            n.enabled = true;
            n.separateAxes = true;
            n.strengthX = new PS.MinMaxCurve(strength);
            n.strengthY = new PS.MinMaxCurve(strength);
            n.strengthZ = new PS.MinMaxCurve(0f);
            n.frequency = frequency;
            n.scrollSpeed = scroll;
            n.damping = true;
            n.quality = ParticleSystemNoiseQuality.Medium;
        }

        private static void Trails(PS ps, Material trailMat, float ratio, float lifetime, float widthPixels = 1f)
        {
            var t = ps.trails;
            t.enabled = true;
            t.mode = ParticleSystemTrailMode.PerParticle;
            t.ratio = ratio;
            t.lifetime = new PS.MinMaxCurve(lifetime);
            t.minVertexDistance = 0.06f;
            t.worldSpace = true;
            t.dieWithParticles = true;
            t.sizeAffectsWidth = false;
            t.sizeAffectsLifetime = false;
            t.inheritParticleColor = true;
            t.textureMode = ParticleSystemTrailTextureMode.Stretch;
            t.widthOverTrail = new PS.MinMaxCurve(widthPixels / PPU, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            t.colorOverTrail = new PS.MinMaxGradient(Grad(new[] { (0f, White), (1f, White) }, new[] { (0f, 1f), (1f, 0f) }));
            ps.GetComponent<ParticleSystemRenderer>().trailMaterial = trailMat;
        }

        private static VfxLight NewLight(Transform parent, string name, Mats m, Color color, float radius, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = m.light;
            sr.sortingOrder = order;
            sr.enabled = false;
            var l = go.AddComponent<VfxLight>();
            l.falloff = m.lightTex;
            l.color = color;
            l.radius = radius;
            return l;
        }

        // Sorting orders (Default layer): background -100, characters 0, lights 5, effects 20-45, cards 100+.
        private const int OrderLight = 5;

        // ------------------------------------------------------------------------------------------ bolt prefab
        private static GameObject BuildBolt(Mats m)
        {
            var root = new GameObject("VFX_ArcaneBolt_Cyan");
            root.AddComponent<PooledVfx>();
            var fx = root.AddComponent<ArcaneBoltEffect>();
            var whiteCyan = Grad(new[] { (0f, White), (0.25f, CyanPale), (0.6f, Cyan), (1f, Blue) }, new[] { (0f, 1f), (0.7f, 1f), (1f, 0f) });

            // 1) anticipation: converging sparks, orbiting runes, growing core and halo
            var charge = new GameObject("Anticipation").transform;
            charge.SetParent(root.transform, false);
            var converge = NewSystem(charge, "Charge_Converge (Sparks)", m.add, 32);
            Duration(converge, 0.36f); Life(converge, 0.24f, 0.34f); Max(converge, 60);
            Rate(converge, 90f); Circle(converge, 1.9f, 0f);
            Velocity(converge, Vector2.zero, Vector2.zero, orbital: 2.2f, radial: -6.2f);
            Flipbook(converge, RowSpark, 0.3f, 0.85f);
            ColorLife(converge, Grad(new[] { (0f, Cyan), (0.6f, CyanPale), (1f, White) }, new[] { (0f, 0f), (0.25f, 1f), (1f, 1f) }));
            Intensity(converge, 1.6f, 3.2f);

            var runes = NewSystem(charge, "Charge_Runes", m.add, 31);
            Duration(runes, 0.4f); Life(runes, 0.40f, 0.40f); Max(runes, 8);
            Burst(runes, (0f, 5)); Circle(runes, 1.5f, 0f);
            Velocity(runes, Vector2.zero, Vector2.zero, orbital: 7f, radial: -2.6f);
            TileRandom(runes, RowRunes * 8, RowRunes * 8 + 7);
            ColorLife(runes, Grad(new[] { (0f, CyanPale), (1f, Cyan) }, new[] { (0f, 0f), (0.25f, 1f), (0.8f, 1f), (1f, 0f) }));
            Intensity(runes, 1.4f, 2.4f);

            var core = NewSystem(charge, "Charge_Core", m.add, 34);
            Duration(core, 0.42f); Life(core, 0.42f, 0.42f); Max(core, 2);
            Burst(core, (0f, 1));
            Flipbook(core, RowTwinkle, 0f, 0.62f);
            ColorLife(core, Grad(new[] { (0f, CyanPale), (1f, White) }, new[] { (0f, 1f), (1f, 1f) }));
            IntensityCurve(core, Curve(0f, 1.2f, 0.8f, 3.4f, 1f, 4.5f));

            var glow = NewSystem(charge, "Charge_Glow", m.addSoft, 30);
            Duration(glow, 0.42f); Life(glow, 0.42f, 0.42f); Max(glow, 2);
            Burst(glow, (0f, 1)); Tile(glow, TileBlob);
            SizeLife(glow, Curve(0f, 0.6f, 1f, 3.0f));
            ColorLife(glow, Grad(new[] { (0f, Cyan), (1f, Cyan) }, new[] { (0f, 0.15f), (1f, 0.55f) }));
            Intensity(glow, 0.7f, 1.0f);
            fx.chargeLight = NewLight(charge, "Light_Charge", m, Cyan, 4.5f, OrderLight);
            fx.chargeRoot = charge;
            fx.chargeSystems = new[] { converge, runes, core, glow };

            // 2) launch: flash + spark fan pointing at the target (root is rotated toward it)
            var launch = new GameObject("Launch").transform;
            launch.SetParent(root.transform, false);
            var lflash = NewSystem(launch, "Launch_Flash (Impact)", m.add, 36);
            Duration(lflash, 0.15f); Life(lflash, 0.14f, 0.14f); Max(lflash, 2); Burst(lflash, (0f, 1));
            Flipbook(lflash, RowImpact, 0f, 0.6f);
            ColorLife(lflash, Grad(new[] { (0f, White), (1f, Cyan) }, new[] { (0f, 1f), (1f, 0.6f) }));
            Intensity(lflash, 3f, 1f);
            var lsparks = NewSystem(launch, "Launch_Sparks", m.add, 35);
            World(lsparks); Duration(lsparks, 0.3f); Life(lsparks, 0.14f, 0.3f); Max(lsparks, 30);
            Burst(lsparks, (0f, 16)); Circle(lsparks, 0.15f, 1f, 110f, -55f); Speed(lsparks, 6f, 15f); Drag(lsparks, 7f);
            Flipbook(lsparks, RowSpark, 0.2f, 0.95f); ColorLife(lsparks, whiteCyan); Intensity(lsparks, 3f, 1f);
            fx.launchRoot = launch;
            fx.launchSystems = new[] { lflash, lsparks };

            // 3) projectile: local core + halo, world-space wake, peeling sparks, ribbon trail
            var proj = new GameObject("Projectile").transform;
            proj.SetParent(root.transform, false);
            var pcore = NewSystem(proj, "Bolt_Core", m.add, 40);
            Duration(pcore, 1f, true); Life(pcore, 0.07f, 0.1f); Max(pcore, 10); Rate(pcore, 45f);
            TileRandom(pcore, RowSpark * 8 + 0, RowSpark * 8 + 1);
            ColorLife(pcore, Grad(new[] { (0f, White), (1f, CyanPale) }, new[] { (0f, 1f), (1f, 1f) }));
            Intensity(pcore, 5f, 4f);
            var pglow = NewSystem(proj, "Bolt_Glow", m.addSoft, 38);
            Duration(pglow, 1f, true); Life(pglow, 0.1f, 0.14f); Max(pglow, 10); Rate(pglow, 28f);
            Tile(pglow, TileBlob); Size(pglow, 2.2f, 2.9f);
            ColorLife(pglow, Grad(new[] { (0f, Cyan), (1f, Blue) }, new[] { (0f, 0.75f), (1f, 0.2f) }));
            Intensity(pglow, 1.4f, 0.8f);
            var wake = NewSystem(root.transform, "Bolt_Wake (Trails)", m.add, 37);
            World(wake); Duration(wake, 1f, true); Life(wake, 0.22f, 0.45f); Max(wake, 500);
            Velocity(wake, new Vector2(-0.7f, 0.7f), new Vector2(-0.7f, 0.9f), world: true); Noise(wake, 0.9f, 2.2f);
            Flipbook(wake, RowSpark, 0.4f, 0.98f);
            ColorLife(wake, Grad(new[] { (0f, White), (0.15f, CyanPale), (0.5f, Cyan), (1f, Violet) }, new[] { (0f, 1f), (0.6f, 0.9f), (1f, 0f) }));
            Intensity(wake, 2.4f, 0.6f);
            var psparks = NewSystem(root.transform, "Bolt_Sparks", m.add, 39);
            World(psparks); Duration(psparks, 1f, true); Life(psparks, 0.25f, 0.5f); Max(psparks, 80);
            Velocity(psparks, new Vector2(-4f, 4f), new Vector2(-3f, 5f), world: true); Gravity(psparks, 0.45f); Drag(psparks, 3f);
            Flipbook(psparks, RowSpark, 0.25f, 0.95f); ColorLife(psparks, whiteCyan); Intensity(psparks, 2.8f, 1f);
            Trails(psparks, m.trail, 1f, 0.09f);
            var trail = proj.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = m.trail;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = Curve(0f, 1f, 1f, 0f);
            trail.widthMultiplier = 2f / PPU;
            trail.colorGradient = Grad(new[] { (0f, White), (0.3f, CyanPale), (1f, Cyan) }, new[] { (0f, 1f), (1f, 0f) });
            trail.numCapVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 36;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            fx.projectileLight = NewLight(proj, "Light_Projectile", m, Cyan, 3.5f, OrderLight);
            fx.projectileRoot = proj;
            fx.projectileHeads = new[] { pcore, pglow };
            fx.wake = wake;
            fx.wakeSparks = psparks;
            fx.projectileTrail = trail;

            // 4) impact: flash, halo, expanding pixel ring, sparks (with trails and ember sub-emitters), shards, lingering motes
            var impact = new GameObject("Impact").transform;
            impact.SetParent(root.transform, false);
            var iflash = NewSystem(impact, "Impact_Flash", m.add, 45);
            Duration(iflash, 0.25f); Life(iflash, 0.22f, 0.22f); Max(iflash, 2); Burst(iflash, (0f, 1)); Size(iflash, 2f, 2f);
            Flipbook(iflash, RowImpact, 0f, 1f);
            ColorLife(iflash, Grad(new[] { (0f, White), (0.4f, CyanPale), (1f, Cyan) }, new[] { (0f, 1f), (1f, 0.5f) }));
            IntensityCurve(iflash, Curve(0f, 3.2f, 0.4f, 1.8f, 1f, 0.8f));
            var ihalo = NewSystem(impact, "Impact_Glow", m.addSoft, 41);
            Duration(ihalo, 0.35f); Life(ihalo, 0.32f, 0.32f); Max(ihalo, 2); Burst(ihalo, (0f, 1)); Tile(ihalo, TileBlob);
            SizeLife(ihalo, Curve(0f, 3f, 1f, 7.5f));
            ColorLife(ihalo, Grad(new[] { (0f, Cyan), (1f, Blue) }, new[] { (0f, 0.6f), (1f, 0f) }));
            Intensity(ihalo, 0.9f, 0.3f);
            var ring = NewSystem(impact, "Impact_Ring", m.add, 43);
            World(ring); Duration(ring, 0.35f); Life(ring, 0.28f, 0.32f); Max(ring, 60); Burst(ring, (0f, 44));
            Circle(ring, 0.2f, 0f); Speed(ring, 11f, 11f); Drag(ring, 5f);
            Flipbook(ring, RowSpark, 0.6f, 0.98f);
            ColorLife(ring, Grad(new[] { (0f, White), (0.4f, CyanPale), (1f, Blue) }, new[] { (0f, 1f), (0.7f, 0.8f), (1f, 0f) }));
            Intensity(ring, 2.6f, 1f);
            var sparks = NewSystem(impact, "Impact_Sparks (Sparks)", m.add, 44);
            World(sparks); Duration(sparks, 0.9f); Life(sparks, 0.35f, 0.85f); Max(sparks, 80); Burst(sparks, (0f, 34), (0.04f, 10));
            Circle(sparks, 0.1f, 1f); Speed(sparks, 6f, 21f); Drag(sparks, 4f); Gravity(sparks, 0.6f);
            Flipbook(sparks, RowSpark, 0f, 0.95f);
            ColorLife(sparks, Grad(new[] { (0f, White), (0.25f, CyanPale), (0.6f, Cyan), (1f, Violet) }, new[] { (0f, 1f), (0.7f, 1f), (1f, 0f) }));
            Intensity(sparks, 3.2f, 0.8f);
            Trails(sparks, m.trail, 0.65f, 0.1f);
            var embers = NewSystem(sparks.transform, "Impact_Embers (SubEmitter)", m.add, 42);
            World(embers); Duration(embers, 0.1f); Life(embers, 0.6f, 1.2f); Max(embers, 80); Burst(embers, (0f, 1));
            Velocity(embers, new Vector2(-0.3f, 0.3f), new Vector2(0.6f, 1.7f), world: true); Noise(embers, 0.6f, 1.5f);
            Flipbook(embers, RowMote, 0f, 0.98f);
            ColorLife(embers, Grad(new[] { (0f, CyanPale), (0.5f, Cyan), (1f, Violet) }, new[] { (0f, 1f), (1f, 0f) }));
            Intensity(embers, 1.6f, 0.5f);
            var sub = sparks.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(embers, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing, 0.45f);
            var shards = NewSystem(impact, "Impact_Shards", m.add, 42);
            World(shards); Duration(shards, 1f); Life(shards, 0.5f, 0.9f); Max(shards, 16); Burst(shards, (0f, 8));
            Circle(shards, 0.2f, 1f); Speed(shards, 4f, 9f); Gravity(shards, 1.1f); Drag(shards, 1.2f);
            var sm = shards.main; sm.startRotation = new PS.MinMaxCurve(0f, Mathf.PI * 2f);
            var rot = shards.rotationOverLifetime; rot.enabled = true; rot.z = new PS.MinMaxCurve(-6f, 6f);
            Tile(shards, TileShard);
            ColorLife(shards, Grad(new[] { (0f, CyanPale), (1f, Blue) }, new[] { (0f, 1f), (0.7f, 1f), (1f, 0f) }));
            Intensity(shards, 1.8f, 0.8f);
            var linger = NewSystem(impact, "Impact_Linger (Dissipation)", m.add, 41);
            World(linger); Duration(linger, 0.6f); Life(linger, 0.8f, 1.6f); Max(linger, 60); Rate(linger, 34f);
            Circle(linger, 1.3f, 1f); Velocity(linger, new Vector2(-0.4f, 0.4f), new Vector2(0.4f, 1.3f), world: true); Noise(linger, 0.5f, 1.2f);
            Flipbook(linger, RowTwinkle, 0f, 0.98f); StartColor(linger, Cyan, Violet);
            ColorLife(linger, Grad(new[] { (0f, White), (1f, White) }, new[] { (0f, 0f), (0.2f, 1f), (1f, 0f) }));
            Intensity(linger, 1.5f, 0.7f);
            fx.impactRoot = impact;
            fx.impactSystems = new[] { iflash, ihalo, ring, sparks, shards, linger };
            fx.impactFlashSystems = new[] { iflash, ihalo };
            fx.impactLight = NewLight(root.transform, "Light_Impact", m, Cyan, 7f, OrderLight);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BoltPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------------------------------ cards
        private sealed class CardSpec
        {
            public string id, name;
            public Color a, b;
            public Vector2 glintCenter, glintSize;
            public float glintRotation;
        }

        private static readonly CardSpec[] CardSpecs =
        {
            new CardSpec { id = "punalada", name = "Puñalada", a = new Color(0.45f, 0.95f, 1f), b = new Color(0.85f, 0.95f, 1f), glintCenter = new Vector2(0.55f, 1.1f), glintSize = new Vector2(3.2f, 0.25f), glintRotation = 26.57f },
            new CardSpec { id = "esquiva", name = "Esquiva", a = new Color(0.35f, 0.9f, 1f), b = new Color(0.6f, 1f, 0.9f), glintCenter = new Vector2(0.2f, 1.4f), glintSize = new Vector2(3.4f, 2.2f) },
            new CardSpec { id = "finta", name = "Finta", a = new Color(0.75f, 0.5f, 1f), b = new Color(0.45f, 0.9f, 1f), glintCenter = new Vector2(0f, 1.0f), glintSize = new Vector2(4.6f, 2.8f) },
        };

        private static SpriteRenderer NewSprite(Transform parent, string name, Material mat, int order, Vector3 localPos = default)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = order;
            return sr;
        }

        private static GameObject BuildCard(Mats m, CardSpec spec)
        {
            var root = new GameObject("Card_" + spec.id);
            var view = root.AddComponent<CardView>();
            view.cardId = spec.id;
            view.displayName = spec.name;
            view.baseTexture = Art("card_" + spec.id + "_base");
            view.baseGlowTexture = Art("card_" + spec.id + "_base_glow");
            view.outlineTexture = Art("card_outline");
            view.outlineColor = spec.a;
            const int o = 100;           // the scene adds 20 per card
            view.baseRenderer = NewSprite(root.transform, "Base", m.sprite, o);
            view.baseGlowRenderer = NewSprite(root.transform, "Base_Glow", m.spriteAdd, o + 1);
            var artPos = new Vector3(0f, 19f / PPU, 0f);          // art window centre relative to the card centre
            var art = NewSprite(root.transform, "Art", m.sprite, o + 2, artPos).gameObject.AddComponent<SpriteSheetAnimator>();
            art.sheet = Art("card_" + spec.id + "_art");
            art.cellWidth = 88; art.cellHeight = 64; art.pixelsPerUnit = PPU;
            art.clips = new[] { new SpriteSheetAnimator.Clip { name = "loop", row = 0, frames = 24, fps = 12f, loop = true } };
            var glow = NewSprite(root.transform, "Art_Glow", m.spriteAdd, o + 3, artPos).gameObject.AddComponent<SpriteSheetAnimator>();
            glow.sheet = Art("card_" + spec.id + "_art_glow");
            glow.cellWidth = 88; glow.cellHeight = 64; glow.pixelsPerUnit = PPU;
            glow.clips = art.clips;
            glow.follow = art;
            view.art = art;
            view.artGlow = glow;
            view.sheenRenderer = NewSprite(root.transform, "Sheen", m.sheen, o + 4);
            view.outlineRenderer = NewSprite(root.transform, "Outline", m.spriteAdd, o + 5);
            view.outlineRenderer.enabled = false;

            var colors = Grad(new[] { (0f, White), (1f, White) }, new[] { (0f, 0f), (0.15f, 1f), (0.75f, 1f), (1f, 0f) });
            var motes = NewSystem(root.transform, "Aura_Motes", m.add, o + 6, artPos);
            Duration(motes, 4f, true); Life(motes, 1.2f, 2.4f); Max(motes, 40); Rate(motes, 7f);
            Box(motes, new Vector2(5.2f, 3.6f)); Velocity(motes, new Vector2(-0.15f, 0.15f), new Vector2(0.3f, 0.9f)); Noise(motes, 0.3f, 0.8f);
            Flipbook(motes, RowTwinkle, 0f, 0.98f); StartColor(motes, spec.a, spec.b); ColorLife(motes, colors);
            Intensity(motes, 1.6f, 1.0f);
            var playOn = motes.main; playOn.playOnAwake = true; playOn.prewarm = true;

            var glints = NewSystem(root.transform, "Aura_Glints", m.add, o + 7, new Vector3(spec.glintCenter.x, spec.glintCenter.y, 0f));
            Duration(glints, 3f, true); Life(glints, 0.45f, 0.6f); Max(glints, 8); Rate(glints, 1.1f);
            Box(glints, spec.glintSize, spec.glintRotation);
            Flipbook(glints, RowTwinkle, 0f, 0.98f); StartColor(glints, White, spec.a);
            Intensity(glints, 3.4f, 2f);
            var gm = glints.main; gm.playOnAwake = true; gm.prewarm = true;

            var rising = NewSystem(root.transform, "Aura_Rising", m.add, o + 8, new Vector3(0f, 4.3f, 0f));
            Duration(rising, 3f, true); Life(rising, 0.9f, 1.7f); Max(rising, 24); Rate(rising, 2.6f);
            Box(rising, new Vector2(5.6f, 0.1f)); Velocity(rising, new Vector2(-0.3f, 0.3f), new Vector2(1.1f, 2.3f)); Noise(rising, 0.45f, 1.1f);
            Flipbook(rising, RowSpark, 0.35f, 0.98f); StartColor(rising, spec.a, spec.b); ColorLife(rising, colors);
            Intensity(rising, 2.2f, 0.8f);
            var rm = rising.main; rm.playOnAwake = true; rm.prewarm = true;

            var burst = NewSystem(root.transform, "Play_Burst (Impact)", m.add, o + 9);
            Duration(burst, 0.5f); Life(burst, 0.35f, 0.75f); Max(burst, 80); Burst(burst, (0f, 56));
            Box(burst, new Vector2(6.5f, 9f), 0f, edge: true); Velocity(burst, Vector2.zero, Vector2.zero, radial: 3.5f); Drag(burst, 2.5f);
            Flipbook(burst, RowSpark, 0f, 0.98f); StartColor(burst, White, spec.a);
            ColorLife(burst, Grad(new[] { (0f, White), (0.3f, spec.a), (1f, spec.b) }, new[] { (0f, 1f), (0.7f, 1f), (1f, 0f) }));
            Intensity(burst, 3.2f, 1f);
            view.motes = motes; view.glints = glints; view.rising = rising; view.playBurst = burst;

            var path = PrefabDir + "/Card_" + spec.id + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------------------------------ scene
        private static void BuildScene(Mats m, GameObject bolt, List<GameObject> cardPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rig = new GameObject("CameraRig");
            var shake = rig.AddComponent<CameraShake>();
            var camGo = new GameObject("PixelCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 360f / (2f * PPU);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.allowMSAA = false;
            cam.allowHDR = true;
            var pixel = camGo.AddComponent<PixelGlowCamera>();
            pixel.bloomShader = Shader.Find("Hidden/LastWish/VFX/Bloom");
            pixel.compositeShader = Shader.Find("Hidden/LastWish/VFX/Composite");

            var bg = NewSprite(null, "Background (room_combat)", m.sprite, -100);
            var bgSprite = bg.gameObject.AddComponent<StaticSprite>();
            bgSprite.texture = Load<Texture2D>("Assets/Resources/Game/Dungeon/room_combat.png");

            // La Pícara (hero sheet: 96 px cells, 64 columns; feet 3 px above the cell bottom)
            var hero = NewSprite(null, "Rogue", m.sprite, 0, new Vector3(-12f, 1.25f, 0f)).gameObject.AddComponent<SpriteSheetAnimator>();
            hero.sheet = Load<Texture2D>("Assets/Resources/Game/Heroes/rogue_96.png");
            hero.cellWidth = 96; hero.cellHeight = 96; hero.pixelsPerUnit = PPU; hero.pivot = new Vector2(0.5f, 3f / 96f);
            hero.clips = new[]
            {
                new SpriteSheetAnimator.Clip { name = "idle", row = 0, frames = 48, fps = 30f, loop = true },
                new SpriteSheetAnimator.Clip { name = "attack", row = 1, frames = 40, fps = 41f, loop = false },
                new SpriteSheetAnimator.Clip { name = "hurt", row = 3, frames = 32, fps = 57f, loop = false },
                new SpriteSheetAnimator.Clip { name = "cast", row = 4, frames = 44, fps = 55f, loop = false },
            };
            var hand = new GameObject("CastPoint").transform;
            hand.SetParent(hero.transform, false);
            hand.localPosition = new Vector3(1.6f, 2.4f, 0f);

            // Target: the moss golem (160x108 cells, anchor 112,102)
            var golem = NewSprite(null, "Golem", m.sprite, 0, new Vector3(7f, 1.25f, 0f)).gameObject.AddComponent<SpriteSheetAnimator>();
            golem.sheet = Load<Texture2D>("Assets/Resources/Game/Enemies/golem_sheet.png");
            golem.cellWidth = 160; golem.cellHeight = 108; golem.pixelsPerUnit = PPU; golem.pivot = new Vector2(112f / 160f, 6f / 108f);
            golem.clips = new[]
            {
                new SpriteSheetAnimator.Clip { name = "idle", row = 0, frames = 12, fps = 8.7f, loop = true },
                new SpriteSheetAnimator.Clip { name = "hurt", row = 3, frames = 6, fps = 12f, loop = false },
            };
            var flash = golem.gameObject.AddComponent<SpriteFlash>();
            flash.flashAmount = 0f;      // the golem's hurt animation already starts with a white flash frame
            var center = new GameObject("HitPoint").transform;
            center.SetParent(golem.transform, false);
            center.localPosition = new Vector3(-7f / PPU, 43f / PPU, 0f);

            var cards = new CardView[cardPrefabs.Count];
            for (var i = 0; i < cardPrefabs.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefabs[i]);
                go.transform.position = new Vector3((i - (cardPrefabs.Count - 1) * 0.5f) * 108f / PPU, -100f / PPU, 0f);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += i * 20;
                cards[i] = go.GetComponent<CardView>();
                var anim = go.GetComponentInChildren<SpriteSheetAnimator>();
                if (anim != null) anim.timeOffset = i * 0.37f;
            }

            var directorGo = new GameObject("VfxLabDirector");
            var director = directorGo.AddComponent<VfxLabDirector>();
            director.pixelCamera = pixel;
            director.cameraShake = shake;
            director.cards = cards;
            director.hero = hero;
            director.heroHand = hand;
            director.target = golem;
            director.targetFlash = flash;
            director.targetCenter = center;
            director.boltPrefab = bolt;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}

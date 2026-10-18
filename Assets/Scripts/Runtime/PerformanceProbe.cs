#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Unity.Profiling;

namespace LastWish.Presentation
{
    public sealed class PerformanceProbe : MonoBehaviour
    {
        private string output;
        private ProfilerRecorder gcRecorder;
        private void OnDestroy() { gcRecorder.Dispose(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--last-wish-benchmark");
            if (index < 0 || index + 1 >= args.Length) return;
            new GameObject("PerformanceProbe").AddComponent<PerformanceProbe>().output = args[index + 1];
        }
        private IEnumerator Start()
        {
            AudioListener.volume = 0;
            var deadline = Time.realtimeSinceStartup + 30;
            while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
            var game = GameBootstrap.Instance;
            if (game == null || !game.IsReady) { UnityEngine.Application.Quit(2); yield break; }
            game.StartBattle(LastWish.Domain.HeroKind.Mage);
            for (var i = 0; i < 60; i++) yield return null;
            var samples = new float[300];
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var idleAllocation = 0L;
            for (var i = 0; i < samples.Length; i++) { yield return null; samples[i] = Time.unscaledDeltaTime * 1000; idleAllocation += gcRecorder.LastValue; }
            Array.Sort(samples);
            var refresh = (Action)typeof(GameBootstrap).GetMethod("RefreshBattle", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate(typeof(Action), game);
            for (var i = 0; i < 3; i++) { refresh(); yield return null; }
            var durations = new double[30]; var allocation = 0L;
            for (var i = 0; i < durations.Length; i++)
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                refresh();
                durations[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                yield return null;
                allocation += gcRecorder.LastValue;
            }
            Array.Sort(durations);
            var result = new Result {
                unity = UnityEngine.Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                context = "Windows development player; D3D11; 1280x720; 60 FPS target; hidden automation window",
                gcCounterAvailable = gcRecorder.Valid, frames = samples.Length, frameMedianMs = samples[150], frameP95Ms = samples[284],
                idleGcBytesPerFrame = idleAllocation / (double)samples.Length,
                refreshCalls = durations.Length, refreshMedianMs = durations[15], refreshP95Ms = durations[28],
                refreshFrameGcBytes = allocation / (double)durations.Length
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("LAST_WISH_BENCHMARK_READY " + output);
            UnityEngine.Application.Quit(0);
        }
        [Serializable] private sealed class Result
        {
            public string unity, graphics, context;
            public int frames, refreshCalls;
            public bool gcCounterAvailable;
            public double frameMedianMs, frameP95Ms, idleGcBytesPerFrame, refreshMedianMs, refreshP95Ms, refreshFrameGcBytes;
        }
    }
}
#endif

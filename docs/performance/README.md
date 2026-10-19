> Historical: this measured the previous uGUI version of the Unity port (removed on 2026-10-19).

# Windows player measurement — 2026-10-18

Unity 6000.3.6f1 development build, Windows x64, Mono, D3D11, RTX 3060 Ti,
1280×720, target 60 FPS, hidden automation window. Deterministic Mage demo.
Same computer and launch flags for both samples. This compares the Unity UI
before and after pooling; it does not compare Unity with Python.

| Metric | Before pooling | Final migration build |
|---|---:|---:|
| Idle frame median | 16.667 ms | 16.668 ms |
| Idle frame p95 | 17.059 ms | 17.072 ms |
| Unchanged RefreshBattle median | 3.0443 ms | 0.0301 ms |
| Unchanged RefreshBattle p95 | 3.5299 ms | 0.0800 ms |
| Mean GC bytes in a refresh frame | 635323.6 | 1639.6 |
| Mean idle GC bytes/frame | 6.53 | 6.53 |

Refresh CPU median fell 99.01%; allocation in those frames fell
99.74%. Idle is still approximately 60 FPS, as capped.
The supported conclusion is less work and garbage when refreshing an unchanged
hand. This does not prove faster GPU rendering, higher uncapped FPS, or the same
improvement for every card, enemy, resource load or machine. A changing hand may
still allocate descriptions. Screenshots/test captures also stall their own frames.

## Method

`PerformanceProbe` runs only in Editor/development builds when explicitly passed
`--last-wish-benchmark OUTPUT.json`. It waits for startup, starts the demo, warms
60 frames, samples 300 idle frame times, warms 3 refreshes and times 30 unchanged
`RefreshBattle` calls (one per frame). It uses `ProfilerRecorder` Memory /
`GC Allocated In Frame`, checked valid in both JSON files. Allocation is the
whole frame's recorded GC allocation, not exclusive allocation inside that method.
Percentiles use sorted samples. No GPU timings or maximum-FPS claim is made.
The baseline was captured locally before pooling, with the same probe/new input;
its raw JSON is retained. The final sample includes the 165-card migration.

From the Unity repository after **Last Wish > Build Windows**, run:

```powershell
$player = Start-Process -FilePath './Builds/Windows/LastWish.exe' -ArgumentList @('-batchmode', '-force-d3d11', '-screen-width', '1280', '-screen-height', '720', '--last-wish-benchmark', "$PWD/TestResults/player-after.json", '-logFile', "$PWD/TestResults/player-after.log") -WindowStyle Hidden -PassThru -Wait
if ($player.ExitCode -ne 0) { throw 'Benchmark player failed' }
./Tools/ComparePerformance.ps1 -Before docs/performance/player-before.json -After TestResults/player-after.json
```

## Findings and applied guidance

- The hand refresh destroyed/recreated every card GameObject and all its text,
  images, listeners and layout. `CardView` now keeps a bounded pool and rebinds
  only when its card/context changes. Its identity is checked in PlayMode.
- The FPS display changed inside the main UI canvas. A nested canvas now
  isolates that frequently changing label. Decorative graphics do not receive
  raycasts; tooltips update on pointer events, not an Update loop.
- Enemy atlases are loaded asynchronously for the required encounter, and parsed
  atlas metadata is cached. Original PNGs/timing/anchors are preserved.
- Legacy Input Manager polling/module were replaced with Input System 1.17
  actions and `InputSystemUIInputModule`; `activeInputHandler=1`.

These choices follow Unity's [UI optimization guidance](https://unity.com/how-to/unity-ui-optimization-tips)
(pool UI, isolate changing canvases, trim raycast targets), its
[UI input documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/UISupport.html)
(the required uGUI module), and its
[player profiling guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-profiling-applications.html).
They are applied where this project's measurements justify them. Built-in 2D
rendering is retained; there is no demonstrated need for a pipeline/DOTS rewrite.

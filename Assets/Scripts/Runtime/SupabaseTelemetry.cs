using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LastWish.Presentation;
using UnityEngine;
using UnityEngine.Networking;

namespace LastWish.Runtime
{
    /// <summary>
    /// Where the power telemetry goes (docs/telemetry.md). Shipped values: Resources/Telemetry/telemetry.json;
    /// <c>telemetry_config.json</c> in Application.persistentDataPath overrides them on one machine
    /// (Tools/Telemetry/LocalSupabase.ps1 writes it for the local Docker Supabase).
    /// </summary>
    [Serializable]
    public sealed class TelemetryConfig
    {
        public const string OverrideFile = "telemetry_config.json";
        /// <summary>false: nothing is recorded or sent.</summary>
        public bool enabled = true;
        /// <summary>Supabase project URL (https://xyz.supabase.co, or http://127.0.0.1:54321 locally).</summary>
        public string url = "";
        /// <summary>Publishable key (sb_publishable_…) or the legacy anon key: public by design, it can only call ingest_run.</summary>
        public string key = "";

        public static TelemetryConfig Load(string dataPath)
        {
            var config = new TelemetryConfig();
            try
            {
                var shipped = Resources.Load<TextAsset>("Telemetry/telemetry");
                if (shipped != null) { JsonUtility.FromJsonOverwrite(shipped.text, config); Resources.UnloadAsset(shipped); }
                var local = Path.Combine(dataPath, OverrideFile);
                if (File.Exists(local)) JsonUtility.FromJsonOverwrite(File.ReadAllText(local), config);
            }
            catch (Exception error) { Debug.LogWarning("Telemetría: configuración no válida (" + error.Message + ")"); }
            config.url = (config.url ?? "").Trim().TrimEnd('/'); config.key = (config.key ?? "").Trim();
            return config;
        }

        /// <summary>The ingest endpoint, or null when the URL is missing or unsafe (plain HTTP only to this computer).</summary>
        public string Endpoint
        {
            get
            {
                if (!enabled || key.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
                var https = uri.Scheme == Uri.UriSchemeHttps;
                var local = uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "localhost");
                return https || local ? url + "/rest/v1/rpc/ingest_run" : null;
            }
        }
    }

    /// <summary>
    /// The telemetry folders (plain files, no Unity): <c>current</c> holds the checkpoint of the run being played,
    /// <c>outbox</c> the runs waiting for upload, <c>rejected</c> the ones the server refused. Not thread-safe:
    /// <see cref="SupabaseTelemetry"/> calls it from one background task at a time.
    /// </summary>
    public sealed class TelemetryOutbox
    {
        public const int MaxQueued = 200;
        public string Outbox { get; }
        public string Current { get; }
        public string Rejected { get; }

        public TelemetryOutbox(string root)
        {
            Outbox = Path.Combine(root, "outbox"); Current = Path.Combine(root, "current"); Rejected = Path.Combine(root, "rejected");
            Directory.CreateDirectory(Outbox); Directory.CreateDirectory(Current); Directory.CreateDirectory(Rejected);
        }

        public static bool SafeName(string runId) => Guid.TryParse(runId, out _);

        public void Checkpoint(string runId, string json) => WriteAtomic(Path.Combine(Current, runId + ".json"), json);

        public void Submit(string runId, string json)
        {
            WriteAtomic(Path.Combine(Outbox, runId + ".json"), json);
            DeleteQuietly(Path.Combine(Current, runId + ".json"));
            Trim();
        }

        /// <summary>Checkpoints left by the previous session (closed or crashed mid-run) are queued as they are: "unfinished".</summary>
        public void RecoverCheckpoints()
        {
            foreach (var path in Directory.GetFiles(Current, "*.json"))
            {
                var target = Path.Combine(Outbox, Path.GetFileName(path));
                if (File.Exists(target)) File.Delete(path); else File.Move(path, target);
            }
            Trim();
        }

        /// <summary>Keep at most <see cref="MaxQueued"/> runs waiting (the oldest go first).</summary>
        public void Trim()
        {
            var files = new DirectoryInfo(Outbox).GetFiles("*.json").OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
            for (var i = 0; i < files.Count - MaxQueued; i++) DeleteQuietly(files[i].FullName);
        }

        /// <summary>The oldest queued run and its request body (<c>{"payload": run}</c>), or (null, null); a body of null = unreadable.</summary>
        public (string path, byte[] body) Next()
        {
            var next = new DirectoryInfo(Outbox).GetFiles("*.json").OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).FirstOrDefault();
            if (next == null) return (null, null);
            try { return (next.FullName, Encoding.UTF8.GetBytes("{\"payload\":" + File.ReadAllText(next.FullName, Encoding.UTF8) + "}")); }
            catch (IOException) { return (next.FullName, null); }
        }

        /// <summary>The server has it: forget the file (renamed aside if it cannot be deleted, so it is never sent again).</summary>
        public void Sent(string path)
        {
            DeleteQuietly(path);
            if (File.Exists(path)) { try { File.Move(path, path + ".sent"); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }

        public void Reject(string path)
        {
            if (!File.Exists(path)) return;
            var target = Path.Combine(Rejected, Path.GetFileName(path));
            if (File.Exists(target)) File.Delete(target);
            File.Move(path, target);
        }

        public int Queued => Directory.GetFiles(Outbox, "*.json").Length;

        private static void WriteAtomic(string path, string text)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }

        private static void DeleteQuietly(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// <see cref="ITelemetrySink"/> on disk + Supabase. The game loop only hands over a string: files are written on a
    /// background thread (one at a time, in order), and uploads run as UnityWebRequests from a coroutine that exists only
    /// while there is something to send. A run is a file in <c>telemetry/outbox</c> until the server accepts it, so runs
    /// survive having no network, a closed game or a crash (the last checkpoint is sent as "unfinished").
    /// </summary>
    public sealed class SupabaseTelemetry : MonoBehaviour, ITelemetrySink
    {
        /// <summary>PlayMode tests switch telemetry off before booting the game.</summary>
        public static bool Suppressed { get; set; }

        private const float StartDelay = 8f, Spacing = 0.5f, MaxBackoff = 600f;
        private TelemetryConfig config;
        private TelemetryOutbox files;
        private string endpoint;
        private readonly object ioLock = new object();
        private Task io = Task.CompletedTask;
        private bool uploading, warnedNetwork;
        private int failures;

        public string GameVersion { get; private set; } = "dev";
        public string PlayerId { get; private set; }
        public bool CanSend => endpoint != null;

        /// <summary>The sink for this session, or null when telemetry is off (config, tests) or the disk is unusable.</summary>
        public static SupabaseTelemetry Create(GameObject host)
        {
            if (Suppressed || host == null) return null;
            var dataPath = UnityEngine.Application.persistentDataPath;
            var config = TelemetryConfig.Load(dataPath);
            if (!config.enabled) return null;
            try
            {
                var root = Path.Combine(dataPath, "telemetry");
                var files = new TelemetryOutbox(root);
                var playerId = LoadPlayerId(Path.Combine(root, "player_id.txt"));
                var sink = host.AddComponent<SupabaseTelemetry>();
                sink.config = config; sink.endpoint = config.Endpoint; sink.files = files; sink.PlayerId = playerId;
                sink.GameVersion = string.IsNullOrEmpty(UnityEngine.Application.version) ? "dev" : UnityEngine.Application.version;
                sink.Enqueue(files.RecoverCheckpoints);
                if (sink.endpoint == null && config.url.Length > 0) Debug.LogWarning("Telemetría: la URL debe ser https:// (o http:// a este equipo) y hace falta la clave; las partidas se guardan en " + files.Outbox);
                sink.StartCoroutine(sink.FirstKick());
                return sink;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                Debug.LogWarning("Telemetría desactivada: " + error.Message);
                return null;
            }
        }

        private static string LoadPlayerId(string path)
        {
            if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path).Trim(), out var id)) return id.ToString("D");
            var fresh = Guid.NewGuid().ToString("D");
            File.WriteAllText(path, fresh);
            return fresh;
        }

        // ------------------------------------------------------------------ ITelemetrySink (game loop: no I/O here)
        public void Checkpoint(string runId, string json)
        {
            if (!TelemetryOutbox.SafeName(runId) || json == null) return;
            Enqueue(() => files.Checkpoint(runId, json));
        }

        public void Submit(string runId, string json)
        {
            if (!TelemetryOutbox.SafeName(runId) || json == null) return;
            Enqueue(() => files.Submit(runId, json));
            Kick();
        }

        /// <summary>Wait (bounded) for pending disk writes: the game is closing.</summary>
        public void Flush(int milliseconds)
        {
            Task pending; lock (ioLock) pending = io;
            try { pending.Wait(milliseconds); } catch (AggregateException) { }
        }

        // ------------------------------------------------------------------ background disk work (one at a time)
        private void Enqueue(Action work)
        {
            lock (ioLock)
                io = io.ContinueWith(_ =>
                {
                    try { work(); }
                    catch (Exception error) { Debug.LogWarning("Telemetría: no se pudo escribir (" + error.Message + ")"); }
                }, TaskScheduler.Default);
        }

        private Task<T> Background<T>(Func<T> work)
        {
            lock (ioLock)
            {
                var task = io.ContinueWith(_ => work(), TaskScheduler.Default);
                io = task;
                return task;
            }
        }

        // ------------------------------------------------------------------ uploads
        private IEnumerator FirstKick()
        {
            yield return new WaitForSecondsRealtime(StartDelay);     // never compete with the loading screen
            Kick();
        }

        private void Kick()
        {
            if (uploading || endpoint == null || !isActiveAndEnabled) return;
            uploading = true;
            StartCoroutine(Upload());
        }

        private IEnumerator Upload()
        {
            while (true)
            {
                var pick = Background(files.Next);
                while (!pick.IsCompleted) yield return null;
                var (path, body) = pick.IsFaulted ? (null, null) : pick.Result;
                if (path == null) break;
                if (body == null) { Enqueue(() => files.Reject(path)); continue; }

                long code = 0; var networkError = false; string answer = null;
                using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("apikey", config.key);
                    if (config.key.StartsWith("eyJ", StringComparison.Ordinal)) request.SetRequestHeader("Authorization", "Bearer " + config.key);   // legacy anon JWT
                    request.timeout = 20;
                    UnityWebRequestAsyncOperation operation = null;
                    try { operation = request.SendWebRequest(); }
                    catch (InvalidOperationException error)
                    {
                        Debug.LogWarning("Telemetría: Unity no permite la conexión (" + error.Message + "). Revisa Project Settings > Player > Allow downloads over HTTP.");
                        endpoint = null; break;
                    }
                    yield return operation;
                    code = request.responseCode;
                    networkError = request.result == UnityWebRequest.Result.ConnectionError;
                    answer = request.downloadHandler?.text;
                }

                if (code >= 200 && code < 300)
                {
                    Enqueue(() => files.Sent(path));
                    failures = 0; warnedNetwork = false;
                    yield return new WaitForSecondsRealtime(Spacing);
                }
                else if (code == 400 || code == 413 || code == 422)
                {
                    // The server refused this run (schema or validation): keep it aside, never retry it forever.
                    Debug.LogWarning("Telemetría: el servidor rechazó " + Path.GetFileName(path) + ": " + Shorten(answer));
                    Enqueue(() => files.Reject(path));
                }
                else
                {
                    failures++;
                    if (!warnedNetwork)
                    {
                        warnedNetwork = true;
                        Debug.LogWarning(networkError ? "Telemetría: sin conexión con " + config.url + "; se reintentará."
                            : "Telemetría: respuesta " + code + " de " + config.url + " (" + Shorten(answer) + "); se reintentará.");
                    }
                    yield return new WaitForSecondsRealtime(Mathf.Min(MaxBackoff, 15f * Mathf.Pow(2f, Mathf.Min(failures - 1, 6))));
                }
            }
            uploading = false;
        }

        private static string Shorten(string text) => string.IsNullOrEmpty(text) ? "" : text.Length <= 200 ? text : text.Substring(0, 200) + "…";

        private void OnDisable() { uploading = false; }
    }
}

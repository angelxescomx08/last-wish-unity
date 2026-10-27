using System;
using System.IO;
using System.Linq;
using System.Text;
using LastWish.Runtime;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>The telemetry folders and config (docs/telemetry.md): plain files, no network.</summary>
    public sealed class TelemetryOutboxTests
    {
        private string root;
        private static readonly string A = "6f1d1c1e-8a2b-4c3d-9e4f-0a1b2c3d4e5f", B = "11111111-2222-4333-8444-555555555555";

        [SetUp] public void CreateRoot() => root = Path.Combine(Path.GetTempPath(), "lw-telemetry-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void DeleteRoot() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        [Test] public void AFinishedRunReplacesItsCheckpointInTheOutbox()
        {
            var box = new TelemetryOutbox(root);
            box.Checkpoint(A, "{\"outcome\":\"unfinished\"}");
            box.Checkpoint(A, "{\"outcome\":\"unfinished\",\"rooms\":2}");
            Assert.That(Directory.GetFiles(box.Current).Single(), Does.EndWith(A + ".json"), "one checkpoint per run, rewritten");
            Assert.That(box.Queued, Is.Zero, "a run still on is not sent");
            box.Submit(A, "{\"outcome\":\"death\"}");
            Assert.That(Directory.GetFiles(box.Current), Is.Empty);
            var (path, body) = box.Next();
            Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("{\"payload\":{\"outcome\":\"death\"}}"));
            box.Sent(path);
            Assert.That(box.Next().path, Is.Null);
        }

        [Test] public void CheckpointsOfAClosedGameAreQueuedAsTheyAre()
        {
            var box = new TelemetryOutbox(root);
            box.Checkpoint(A, "{\"outcome\":\"unfinished\"}");
            box.Submit(B, "{\"outcome\":\"quit\"}");
            box.Checkpoint(B, "{\"outcome\":\"unfinished\"}");     // never overwrites the finished copy
            var next = new TelemetryOutbox(root);
            next.RecoverCheckpoints();
            Assert.That(Directory.GetFiles(next.Current), Is.Empty);
            Assert.That(next.Queued, Is.EqualTo(2));
            Assert.That(File.ReadAllText(Path.Combine(next.Outbox, B + ".json")), Does.Contain("quit"));
        }

        [Test] public void RefusedRunsGoAsideAndTheQueueIsBounded()
        {
            var box = new TelemetryOutbox(root);
            box.Submit(A, "{}");
            var (path, _) = box.Next();
            box.Reject(path);
            Assert.That(box.Queued, Is.Zero);
            Assert.That(Directory.GetFiles(box.Rejected).Single(), Does.EndWith(A + ".json"));
            for (var i = 0; i < TelemetryOutbox.MaxQueued + 5; i++) box.Submit(Guid.NewGuid().ToString("D"), "{}");
            Assert.That(box.Queued, Is.EqualTo(TelemetryOutbox.MaxQueued));
        }

        [Test] public void OnlyRunIdsBecomeFileNames()
        {
            Assert.That(TelemetryOutbox.SafeName(A), Is.True);
            Assert.That(TelemetryOutbox.SafeName("../../preferences"), Is.False);
            Assert.That(TelemetryOutbox.SafeName(null), Is.False);
        }

        [Test] public void PlainHttpIsOnlyUsedForThisComputer()
        {
            TelemetryConfig Config(string url, string key = "sb_publishable_x", bool enabled = true) => new TelemetryConfig { url = url, key = key, enabled = enabled };
            Assert.That(Config("http://127.0.0.1:54321").Endpoint, Is.EqualTo("http://127.0.0.1:54321/rest/v1/rpc/ingest_run"));
            Assert.That(Config("http://localhost:54321").Endpoint, Is.Not.Null);
            Assert.That(Config("https://abc.supabase.co").Endpoint, Is.EqualTo("https://abc.supabase.co/rest/v1/rpc/ingest_run"));
            Assert.That(Config("http://abc.supabase.co").Endpoint, Is.Null, "never plain HTTP over the internet");
            Assert.That(Config("https://abc.supabase.co", "").Endpoint, Is.Null, "no key, no upload");
            Assert.That(Config("https://abc.supabase.co", enabled: false).Endpoint, Is.Null);
            Assert.That(Config("").Endpoint, Is.Null);
        }
    }
}

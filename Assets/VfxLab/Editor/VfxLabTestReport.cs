using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LastWish.VfxLab.Editor
{
    /// <summary>
    /// Runs the project's tests from the open editor (requested through <see cref="VfxLabRemote"/>) and writes
    /// TestResults/remote-&lt;run&gt;.txt: the overall result, the counts and one line per test (status, duration, full
    /// name) with the message of every failure. Registered on every domain reload, so it also hears PlayMode runs;
    /// it only writes for runs it started (SessionState), never for runs started from the Test Runner window.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class VfxLabTestReport : ICallbacks
    {
        private const string RunKey = "LastWish.VfxLab.TestReport";

        static VfxLabTestReport()
        {
            if (Application.isBatchMode) return;
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new VfxLabTestReport());
        }

        /// <summary>"EditMode" / "PlayMode" [class or namespace]; returns the report name.</summary>
        public static string Run(string spec)
        {
            var parts = (spec ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var mode = parts.Length > 0 && parts[0].Equals("EditMode", StringComparison.OrdinalIgnoreCase) ? TestMode.EditMode : TestMode.PlayMode;
            var filter = new Filter { testMode = mode };
            var group = parts.Length > 1 ? parts[1].Trim() : null;
            if (!string.IsNullOrEmpty(group)) filter.groupNames = new[] { "^" + group.Replace(".", "\\.") };
            var name = mode + (string.IsNullOrEmpty(group) ? "" : "-" + group.Substring(group.LastIndexOf('.') + 1));
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Report(name), "running " + DateTime.Now.ToString("HH:mm:ss") + "\n");
            SessionState.SetString(RunKey, name);
            if (mode == TestMode.PlayMode) VfxLabBuilder.ClearStartSceneForThisRun();
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
            return name;
        }

        private static string Report(string name) => Path.Combine("TestResults", "remote-" + name + ".txt");

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var name = SessionState.GetString(RunKey, "");
            if (string.IsNullOrEmpty(name)) return;
            SessionState.EraseString(RunKey);
            var text = new StringBuilder();
            text.AppendLine($"{result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} " +
                            $"inconclusive={result.InconclusiveCount} duration={result.Duration:0.0}s finished={DateTime.Now:HH:mm:ss}");
            var leaves = new StringBuilder();
            Walk(result, leaves);
            text.Append(leaves);
            try { Directory.CreateDirectory("TestResults"); File.WriteAllText(Report(name), text.ToString()); }
            catch (IOException e) { Debug.LogException(e); }
        }

        private static void Walk(ITestResultAdaptor r, StringBuilder text)
        {
            if (r.HasChildren) { foreach (var child in r.Children) Walk(child, text); return; }
            text.AppendLine($"{r.TestStatus,-12} {r.Duration,7:0.00}s {r.FullName}");
            if (r.TestStatus == TestStatus.Failed)
            {
                foreach (var line in ((r.Message ?? "") + "\n" + (r.StackTrace ?? "")).Split('\n'))
                    if (line.Trim().Length > 0) text.AppendLine("      " + line.TrimEnd());
            }
        }
    }
}

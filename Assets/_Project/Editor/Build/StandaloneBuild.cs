using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CloseTheDeal.Build;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CloseTheDeal.Editor.Build
{
    /// <summary>Builds a standalone Windows/macOS player stamped with BuildInfo commit metadata.</summary>
    public static class StandaloneBuild
    {
        static readonly string[] Scenes = { "Assets/_Project/Scenes/Lanes/Lane_Integration.unity" };

        const string GameFileName = "SealTheDeal";

        [MenuItem("Seal the Deal/Build/Standalone (macOS)")]
        static void BuildMacFromMenu() => Run(BuildTarget.StandaloneOSX, Path.Combine("Builds", "macOS"), 0);

        [MenuItem("Seal the Deal/Build/Standalone (Windows)")]
        static void BuildWindowsFromMenu() => Run(BuildTarget.StandaloneWindows64, Path.Combine("Builds", "Windows"), 0);

        public static void BuildFromCommandLine()
        {
            string targetName = Arg("-playerTarget") ?? "mac";
            BuildTarget target = targetName.StartsWith("win", StringComparison.OrdinalIgnoreCase) ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneOSX;
            string output = Arg("-buildOut") ?? Path.Combine("Builds", target == BuildTarget.StandaloneOSX ? "macOS" : "Windows");
            int.TryParse(Arg("-buildNumber"), out int number);
            bool ok = Run(target, output, number);
            EditorApplication.Exit(ok ? 0 : 2);
        }

        static bool Run(BuildTarget target, string outputFolder, int buildNumber)
        {
            var report = new StringBuilder();
            string folder = Path.GetFullPath(outputFolder);
            Directory.CreateDirectory(folder);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                return Fail(report, folder, $"This Unity has no {target} player installed. Add the module in Unity Hub (Windows Build Support (Mono or IL2CPP)) and build again.");

            string missing = MissingScene();
            if (missing != null)
                return Fail(report, folder, "The scene to build is missing: " + missing);

            int number = buildNumber > 0 ? buildNumber : CommitCount();
            string commit = Git("rev-parse --short HEAD");
            string location = Path.Combine(folder, target == BuildTarget.StandaloneOSX ? GameFileName + ".app" : GameFileName + ".exe");
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            DateTime started = DateTime.UtcNow;
            BuildReport result = BuildPipeline.BuildPlayer(options);
            TimeSpan took = DateTime.UtcNow - started;
            if (result.summary.result != BuildResult.Succeeded)
                return Fail(report, folder, $"{result.summary.result} after {took.TotalSeconds:0} s with {result.summary.totalErrors} errors. The Unity log above lists them.");

            WriteStamp(StreamingAssetsOf(target, location), number, commit, target);
            long bytes = target == BuildTarget.StandaloneOSX ? SizeOf(location) : SizeOf(Path.GetDirectoryName(location));
            report.AppendLine($"Built {target} to {location}");
            report.AppendLine($"Version {Application.version}, build {number}, commit {commit}");
            report.AppendLine($"Size {bytes / (1024f * 1024f):0.0} MB, took {took.TotalSeconds:0} s, {result.summary.totalWarnings} warnings");
            File.WriteAllText(Path.Combine(folder, "build_report.txt"), report.ToString());
            Debug.Log("[Build] " + report.ToString().Replace(Environment.NewLine, " · "));
            return true;
        }

        static bool Fail(StringBuilder report, string folder, string why)
        {
            report.AppendLine("FAILED: " + why);
            File.WriteAllText(Path.Combine(folder, "build_report.txt"), report.ToString());
            Debug.LogError("[Build] " + why);
            return false;
        }

        static string MissingScene()
        {
            foreach (string scene in Scenes)
            {
                if (!File.Exists(scene))
                    return scene;
            }

            return null;
        }

        static string StreamingAssetsOf(BuildTarget target, string location)
        {
            if (target == BuildTarget.StandaloneOSX)
                return Path.Combine(location, "Contents", "Resources", "Data", "StreamingAssets");
            string data = Path.Combine(Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location) + "_Data");
            return Path.Combine(data, "StreamingAssets");
        }

        static void WriteStamp(string streamingAssets, int number, string commit, BuildTarget target)
        {
            Directory.CreateDirectory(streamingAssets);
            string json = "{\n"
                + $"  \"buildNumber\": {number},\n"
                + $"  \"commit\": \"{commit}\",\n"
                + $"  \"builtUtc\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",\n"
                + $"  \"target\": \"{target}\"\n"
                + "}\n";
            File.WriteAllText(Path.Combine(streamingAssets, BuildInfo.StampFile), json);
        }

        static long SizeOf(string path)
        {
            if (File.Exists(path))
                return new FileInfo(path).Length;
            long total = 0;
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                total += new FileInfo(file).Length;
            return total;
        }

        static int CommitCount() => int.TryParse(Git("rev-list --count HEAD"), out int count) ? count : 0;

        static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };
                using Process process = Process.Start(start);
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return process.ExitCode == 0 ? output : string.Empty;
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            {
                return string.Empty;
            }
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}

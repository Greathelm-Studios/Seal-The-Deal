using System;
using System.IO;
using UnityEngine;

namespace CloseTheDeal.Build
{
    /// <summary>
    /// Which build this is: version, build number, commit and when it was made, for the menu's
    /// corner, bug reports and the first line of the log. A standalone build reads the stamp the
    /// build script wrote into StreamingAssets/build_info.json; the editor reports itself as an
    /// editor session. Design: docs/systems/BUILD.md.
    /// </summary>
    public static class BuildInfo
    {
        public const string StampFile = "build_info.json";

        [Serializable]
        sealed class Stamp
        {
            public int buildNumber;
            public string commit;
            public string builtUtc;
            public string target;
        }

        static Stamp _stamp;
        static bool _read;

        /// <summary>Player-facing version, from Player Settings (also what Steam lobbies compare).</summary>
        public static string Version => Application.version;

        /// <summary>Counts up with each build; 0 in the editor or if the stamp is missing.</summary>
        public static int BuildNumber => Read()?.buildNumber ?? 0;

        /// <summary>Short commit the build was made from, or "" when unknown.</summary>
        public static string Commit => Read()?.commit ?? string.Empty;

        /// <summary>When the build was made (UTC, ISO 8601), or "" in the editor.</summary>
        public static string BuiltUtc => Read()?.builtUtc ?? string.Empty;

        public static bool IsStamped => Read() != null;

        /// <summary>One short line for a menu corner: "v0.1.0 · build 41 · 3f2a9c1", or "v0.1.0 · editor".</summary>
        public static string Display
        {
            get
            {
                Stamp stamp = Read();
                if (stamp == null)
                    return Application.isEditor ? $"v{Version} · editor" : $"v{Version}";
                return string.IsNullOrEmpty(stamp.commit) ? $"v{Version} · build {stamp.buildNumber}" : $"v{Version} · build {stamp.buildNumber} · {stamp.commit}";
            }
        }

        static Stamp Read()
        {
            if (_read)
                return _stamp;

            _read = true;
            string path = Path.Combine(Application.streamingAssetsPath, StampFile);
            try
            {
                _stamp = File.Exists(path) ? JsonUtility.FromJson<Stamp>(File.ReadAllText(path)) : null;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Build] Couldn't read {path} ({e.Message}); showing the version only.");
                _stamp = null;
            }

            return _stamp;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _stamp = null;
            _read = false;
        }

        /// <summary>The first line of every log names the build, so a sent log says what it came from.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LogOnLaunch()
        {
            Debug.Log($"[Build] {Application.productName} {Display}{(BuiltUtc.Length > 0 ? " built " + BuiltUtc : string.Empty)} on {SystemInfo.operatingSystem}, {SystemInfo.processorType}, {SystemInfo.graphicsDeviceName}.");
        }
    }
}

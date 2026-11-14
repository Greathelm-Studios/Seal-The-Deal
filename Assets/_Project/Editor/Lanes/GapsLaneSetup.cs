using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Narration;
using CloseTheDeal.Onboarding;
using CloseTheDeal.Net;
using CloseTheDeal.Profile;
using CloseTheDeal.Records;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// The gaps lane's setup: adds the match records and the profile keeper to the open scene
    /// (only what is missing), and a scripted check of both in Lane_Integration.unity.
    /// Menu: Close the Deal > Lanes > Gaps. Design: docs/systems/RECORDS.md, docs/systems/PROFILE.md.
    /// </summary>
    public static class GapsLaneSetup
    {
        internal const string ObjectName = "Records";

        [MenuItem("Close the Deal/Lanes/Gaps/Add Records And Profile To Open Scene")]
        static void AddFromMenu()
        {
            string added = AddToOpenScene();
            EditorUtility.DisplayDialog("Records and profile", added.Length > 0 ? "Added: " + added + ". Save the scene to keep it." : "Already in the scene; nothing changed.", "OK");
        }

        /// <summary>Shell: adds the records to Lane_Integration.unity and saves it.</summary>
        public static void AddToIntegrationFromCommandLine()
        {
            EditorSceneManager.OpenScene(GapsLaneRun.ScenePath, OpenSceneMode.Single);
            string added = AddToOpenScene();
            if (added.Length > 0)
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Gaps] " + (added.Length > 0 ? "Added " + added + " to Lane_Integration.unity." : "Lane_Integration.unity already had the records."));
        }

        /// <summary>Adds what is missing; never changes what is there. Returns what was added, or "".</summary>
        internal static string AddToOpenScene()
        {
            var parts = new List<string>();
            MatchRecords records = Object.FindAnyObjectByType<MatchRecords>(FindObjectsInactive.Include);
            GameObject host = records != null ? records.gameObject : null;
            if (host == null)
            {
                host = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Add records");
                host.AddComponent<MatchRecords>();
                parts.Add("MatchRecords");
            }

            if (Object.FindAnyObjectByType<ProfileKeeper>(FindObjectsInactive.Include) == null)
            {
                Undo.AddComponent<ProfileKeeper>(host);
                parts.Add("ProfileKeeper");
            }

            EnsureContent(parts);
            AddWithAsset<MatchSecretary>(host, "_lines", AssetDatabase.LoadAssetAtPath<MatchSecretaryLines>(LinesPath), parts);
            AddWithAsset<FirstRunHints>(host, "_hints", AssetDatabase.LoadAssetAtPath<OnboardingHints>(HintsPath), parts);

            if (parts.Count > 0)
                EditorSceneManager.MarkSceneDirty(host.scene);
            return string.Join(", ", parts);
        }

        /// <summary>Adds the component if the scene has none, and fills its asset slot only while it is empty.</summary>
        static void AddWithAsset<T>(GameObject host, string field, Object asset, List<string> parts) where T : Component
        {
            T component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (component == null)
            {
                component = Undo.AddComponent<T>(host);
                parts.Add(typeof(T).Name);
            }

            var serialized = new SerializedObject(component);
            SerializedProperty slot = serialized.FindProperty(field);
            if (slot == null || slot.objectReferenceValue != null || asset == null)
                return;
            slot.objectReferenceValue = asset;
            serialized.ApplyModifiedProperties();
            parts.Add(typeof(T).Name + " asset");
        }

        // ---- Content assets (created once; a person's edits are never overwritten) ----------------

        internal const string NarrationFolder = "Assets/_Project/Profiles/Narration";
        internal const string LinesPath = NarrationFolder + "/MatchSecretaryLines.asset";
        internal const string HintsPath = "Assets/_Project/Profiles/Onboarding/OnboardingHints.asset";

        /// <summary>Makes the lines and hints assets if missing. Never touches existing ones.</summary>
        internal static void EnsureContent(List<string> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<MatchSecretaryLines>(LinesPath) == null)
            {
                var lines = ScriptableObject.CreateInstance<MatchSecretaryLines>();
                lines.EditorInit(SecretaryContent());
                Create(lines, LinesPath);
                parts.Add("MatchSecretaryLines.asset");
            }

            if (AssetDatabase.LoadAssetAtPath<OnboardingHints>(HintsPath) == null)
            {
                var hints = ScriptableObject.CreateInstance<OnboardingHints>();
                hints.EditorInit(HintContent());
                Create(hints, HintsPath);
                parts.Add("OnboardingHints.asset");
            }
        }

        static void Create(ScriptableObject asset, string path)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            AssetDatabase.CreateAsset(asset, path);
            GeneratedAssetGuard.MarkGenerated(asset);
            AssetDatabase.SaveAssets();
        }

        static MatchSecretaryLines.Moment M(MatchMoment when, int priority, params string[] lines) =>
            new() { When = when, Priority = priority, Lines = lines };

        /// <summary>Proposals for the owner, every one.</summary>
        static MatchSecretaryLines.Moment[] SecretaryContent() => new[]
        {
            M(MatchMoment.MatchStart, 30,
                "The quarter is open. Please carry the briefcase upward.",
                "Today's agenda: one briefcase, two roofs. Any other business can wait.",
                "The briefcase is on the plaza. It won't file itself."),
            M(MatchMoment.FirstPickup, 35,
                "{name} has the briefcase. I've updated the org chart.",
                "{firm} has taken possession of the briefcase. Possession is most of the law.",
                "The briefcase is with {name}. Please don't shake it."),
            M(MatchMoment.FirstKill, 30,
                "{killer} has let {victim} go. HR has been informed.",
                "First casualty of the quarter: {victim}. {killer} will be expensing the ammunition."),
            M(MatchMoment.Steal, 50,
                "{firm} has acquired the briefcase from {rival}. Hostile, but legal.",
                "The briefcase has changed hands. {rival} will want that in writing.",
                "{name} has taken the briefcase. {rival} has been notified."),
            M(MatchMoment.FloorOpened, 40,
                "{floor} is open. Please mind the wet paint.",
                "{floor} is now accessible. Visitors must be accompanied by a briefcase.",
                "{floor} has been unlocked. The staff there are thrilled."),
            M(MatchMoment.CheckpointMoved, 25,
                "Your firm has moved up to {floor}. Your desks will follow.",
                "{floor} is your firm's new address. I've changed the letterhead."),
            M(MatchMoment.WindowKill, 60,
                "{victim} has left the building. {killer} held the window.",
                "{victim} has taken the express route down, courtesy of {killer}.",
                "{killer} has shown {victim} the quickest way out."),
            M(MatchMoment.FellOut, 20,
                "{name} has left the building without signing out.",
                "{name} took the stairs the wrong way."),
            M(MatchMoment.BriefcaseReturned, 30,
                "The briefcase went out of a window. It's back on {place}.",
                "Lost property has returned the briefcase to {place}."),
            M(MatchMoment.CapWarning, 80,
                "The quarter closes in {time}. Please wrap up any hostilities.",
                "{time} left in the quarter. I'm already putting my coat on."),
            M(MatchMoment.Won, 100,
                "{firm} has sealed the deal. Drinks are in the boardroom. They're not free.",
                "Congratulations, {firm}. Your bonus is in the post. Second class.",
                "Deal sealed. {firm} may now use the executive lift."),
            M(MatchMoment.Lost, 100,
                "{firm} has sealed the deal. Please clear your desks by five.",
                "The deal went to {firm}. I've cancelled your parking.",
                "{firm} closed it. HR will be in touch about your future. Briefly."),
            M(MatchMoment.NoWinner, 100,
                "The quarter has closed with no deal. The shareholders are not surprised.",
                "No deal this quarter. I'll schedule a meeting about it.")
        };

        static OnboardingHints.Hint H(string id, HintTrigger when, float seconds, string text) =>
            new() { Id = id, When = when, Seconds = seconds, Text = text };

        /// <summary>Proposals for the owner, every one.</summary>
        static OnboardingHints.Hint[] HintContent() => new[]
        {
            H("reception", HintTrigger.ArrivedInReception, 8f, "Sign in with a firm at one of the stands, then ready up at the desk. Press {interact} at either."),
            H("briefcase-rule", HintTrigger.MatchRunning, 8f, "One briefcase decides the match. Carry it onto either roof and your firm wins."),
            H("carrying", HintTrigger.CarryingBriefcase, 7f, "You have the briefcase. Every floor you carry it onto opens for both towers. Press {drop} to hand it to a teammate."),
            H("rival-carrying", HintTrigger.RivalCarrying, 7f, "The rival firm has the briefcase. Knock the carrier down and it drops. Skybridges on checkpoint floors cross to their tower."),
            H("died", HintTrigger.Died, 6f, "You'll be back on this floor in a few seconds. You keep your guns. Anything you carried was dropped."),
            H("fell-out", HintTrigger.FellOut, 6f, "Leaving through a window sends you back to your firm's last checkpoint, without the briefcase."),
            H("kept", HintTrigger.MatchRecorded, 6f, "Every finished match is kept. Your wins, kills and time with the briefcase carry over.")
        };
    }

    /// <summary>
    /// Plays Lane_Integration.unity by script to check the records and the profile. Roles:
    /// solo (one copy; with -gapsCorrupt the profile file is spoiled first), host and client
    /// (two copies of the project, same -gapsPort). The scene gets the records for the run
    /// only; it is never saved. Shell:
    /// Unity -batchmode -projectPath &lt;copy&gt; -executeMethod CloseTheDeal.Editor.Lanes.GapsLaneRun.RunFromShell
    ///   -gapsRole solo|host|client -gapsOut &lt;folder&gt; -gapsPort 17795 -profileSave &lt;file&gt; [-gapsCorrupt]
    /// </summary>
    public static class GapsLaneRun
    {
        internal const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Integration.unity";
        const string ActiveKey = "GapsLaneRun.Active";
        const string RoleKey = "GapsLaneRun.Role";
        const string OutKey = "GapsLaneRun.Out";
        const ushort DefaultPort = 17795;

        static readonly Stack<IEnumerator> Steps = new();
        static readonly StringBuilder Report = new();
        static object _waiting;
        static float _waitUntil;
        static bool _started;
        static string _role;
        static string _out;
        static int _failures;
        static int _passes;
        static int _profileErrors;
        static int _finals;
        static bool _deadAtFinal;
        static MatchPhase? _phaseAtJoin;
        static readonly List<string> Said = new();
        static readonly List<string> Hinted = new();

        [MenuItem("Close the Deal/Lanes/Gaps/Run Records Check (solo)")]
        static void RunFromMenu() => Begin("solo", "Temp/GapsRun");

        public static void RunFromShell() => Begin(Arg("-gapsRole") ?? "solo", Arg("-gapsOut") ?? "Temp/GapsRun");

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static void Begin(string role, string outFolder)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GapsLaneSetup.AddToOpenScene();
            SetPort(ushort.TryParse(Arg("-gapsPort"), out ushort port) ? port : DefaultPort);
            if (Flag("-gapsCorrupt"))
                SpoilProfile();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(RoleKey, role);
            SessionState.SetString(OutKey, outFolder);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Our own port, so this run never joins someone else's editor on FishNet's default.</summary>
        static void SetPort(ushort port)
        {
            var net = Object.FindAnyObjectByType<NetworkManager>();
            Tugboat tugboat = net != null ? net.GetComponentInChildren<Tugboat>(true) : null;
            if (tugboat != null)
                tugboat.SetPort(port);
            else
                Debug.LogError("[GapsRun] No Tugboat transport found to set the port on.");
        }

        static void SpoilProfile()
        {
            string path = ProfileStore.FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, "{ \"version\": 1, \"stats\": [3, 1, ");
            Debug.Log("[GapsRun] Spoiled the profile file on purpose: " + path);
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Pump;
        }

        // ---- The stepper ---------------------------------------------------------------------

        static void Pump()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (!_started)
                StartScript();
            if (!Ready())
                return;

            _waiting = null;
            if (!Step())
                Finish();
        }

        static void StartScript()
        {
            _started = true;
            _role = SessionState.GetString(RoleKey, "solo");
            _out = SessionState.GetString(OutKey, "Temp/GapsRun");
            Directory.CreateDirectory(_out);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += CountProfileErrors;
            Subtitles.Shown += line => { Said.Add(line.Moment); Note($"said ({line.Speaker}, {line.Moment}): {line.Text}"); };
            FirstRunHints.HintShown += hint => { Hinted.Add(hint.Id); Note($"hint ({hint.Id}): {hint.Text}"); };
            Steps.Push(_role == "client" ? ClientScript() : _role == "host" ? HostScript() : SoloScript());
        }

        static void CountProfileErrors(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && (message.Contains("[Profile]") || message.Contains("Records")))
                _profileErrors++;
        }

        static bool Step()
        {
            while (Steps.Count > 0)
            {
                bool more;
                try
                {
                    more = Steps.Peek().MoveNext();
                }
                catch (Exception e)
                {
                    Check("the script ran to its end", false, e.ToString());
                    return false;
                }

                if (!more)
                {
                    Steps.Pop();
                    continue;
                }

                object current = Steps.Peek().Current;
                if (current is IEnumerator inner)
                {
                    Steps.Push(inner);
                    continue;
                }

                _waiting = current;
                if (current is float seconds)
                    _waitUntil = Time.time + seconds;
                return true;
            }

            return false;
        }

        static bool Ready()
        {
            switch (_waiting)
            {
                case float:
                    return Time.time >= _waitUntil;
                case Until until:
                    return until.Done();
                default:
                    return true;
            }
        }

        sealed class Until
        {
            readonly Func<bool> _condition;
            readonly float _giveUpAt;
            readonly string _what;

            public Until(Func<bool> condition, float timeout, string what)
            {
                _condition = condition;
                _giveUpAt = Time.time + timeout;
                _what = what;
            }

            public bool Done()
            {
                if (_condition())
                    return true;
                if (Time.time < _giveUpAt)
                    return false;

                Check("waited for " + _what, false, "timed out");
                return true;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Pump;
            Application.logMessageReceived -= CountProfileErrors;
            SessionState.SetBool(ActiveKey, false);
            Note($"RESULT: {_failures} failed, {_passes} passed ({_role}).");
            File.WriteAllText(Path.Combine(_out, _role + "_report.txt"), Report.ToString());
            if (Application.isBatchMode)
                EditorApplication.Exit(_failures == 0 ? 0 : 3);
            else
                EditorApplication.ExitPlaymode();
        }

        static void Note(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[GapsRun] " + line);
        }

        static void Check(string what, bool ok, string detail = "")
        {
            if (ok)
                _passes++;
            else
                _failures++;
            Note($"{(ok ? "PASS" : "FAIL")}  {what}{(detail.Length > 0 ? "  · " + detail : "")}");
        }

        // ---- Reading the game ----------------------------------------------------------------

        static FirmMember Me => FirmMember.Local;
        static MatchDirector Match => MatchDirector.Instance;
        static MatchRecords Records => MatchRecords.Instance;
        static int MyId => InstanceFinder.ClientManager.Connection.ClientId;
        static int P(ProfileStat stat) => ProfileStore.Get(stat);

        static FirmMember Other()
        {
            IReadOnlyList<FirmMember> all = FirmMember.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != Me)
                    return all[i];
            }

            return null;
        }

        static MatchLine LineOf(FirmMember member)
        {
            Records.TryGetLine(member.Owner.ClientId, out MatchLine line);
            return line;
        }

        static string Show(MatchLine l) =>
            $"k{l.Kills} d{l.Deaths} f{l.Falls} wk{l.WindowKills} pick{l.Pickups} steal{l.Steals} open{l.FloorsOpened} carry{l.CarrySeconds}s best{l.BestFloor} played{l.SecondsPlayed}s";

        static string ShowProfile() =>
            $"matches {P(ProfileStat.MatchesPlayed)}, wins {P(ProfileStat.Wins)}, losses {P(ProfileStat.Losses)}, kills {P(ProfileStat.Kills)}, deaths {P(ProfileStat.Deaths)}, falls {P(ProfileStat.Falls)}, window kills {P(ProfileStat.WindowKills)}, carry {P(ProfileStat.CarrySeconds)}s, best floor {P(ProfileStat.BestFloor)}, history {ProfileStore.Current.Recent.Count}";

        static void WatchFinals()
        {
            Records.FinalRecord += _ =>
            {
                _finals++;
                _deadAtFinal = Me != null && Me.GetComponent<Health>().IsDead;
            };
        }

        /// <summary>Feet on a floor of a tower, in the open: the same spot the integration check uses.</summary>
        static Vector3 OpenSpot(int tower, int floor)
        {
            int solid = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Prop"));
            for (float x = -8f; x <= 8f; x += 2f)
            {
                for (float z = -3f; z >= -11f; z -= 2f)
                {
                    Vector3 spot = Match.Arena.FloorPoint(tower, floor, new Vector3(x, 0f, z));
                    if (Physics.Raycast(spot + Vector3.up * 1.2f, Vector3.down, out RaycastHit ground, 2.5f, solid, QueryTriggerInteraction.Ignore)
                        && !Physics.CheckCapsule(ground.point + Vector3.up * 0.5f, ground.point + Vector3.up * 1.5f, 0.35f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                        return ground.point + Vector3.up * 0.05f;
                }
            }

            return Match.Arena.FloorPoint(tower, floor, new Vector3(0f, 0.1f, -7f));
        }

        static IEnumerator Host()
        {
            var lobby = Object.FindAnyObjectByType<SteamLobby>();
            yield return new Until(() => lobby.Ready, 30f, "the network to be ready");
            lobby.Host();
            yield return new Until(() => Me != null, 60f, "this machine's player to spawn");
            yield return new Until(() => Records != null && Match != null, 10f, "the records and the match");
            Check("this copy is the host, on its own port", InstanceFinder.IsServerStarted);
            WatchFinals();
            yield return 1f;
        }

        static IEnumerator StartMatch(bool rematch)
        {
            if (rematch && Match.CanHostRematch)
                Match.HostRematch();
            else
                Match.HostStart();
            yield return new Until(() => Match.Phase == MatchPhase.Running, 20f, "the match to run");
            yield return 0.5f;
        }

        static IEnumerator TakeBriefcase()
        {
            Briefcase briefcase = Briefcase.Current;
            Me.ServerTeleport(briefcase.Centre + Vector3.back * 0.8f);
            yield return 0.4f;
            Me.RequestInteract();
            yield return new Until(() => Match.IsCarrying(Me), 4f, "the briefcase");
        }

        static IEnumerator CarryToRoof()
        {
            Me.ServerTeleport(OpenSpot(Me.Firm, Match.Arena.RoofFloor));
            yield return new Until(() => Match.Phase == MatchPhase.Ended, 5f, "the win");
        }

        // ---- Solo: profile across launches ------------------------------------------------------

        static IEnumerator SoloScript()
        {
            Note("ROLE solo: one copy hosting alone. Profile at " + ProfileStore.FilePath);
            ProfileStore.LoadState state = ProfileStore.State == ProfileStore.LoadState.NotLoaded ? LoadNow() : ProfileStore.State;
            int before = P(ProfileStat.MatchesPlayed);
            Note($"profile on launch: {state}, {ShowProfile()}");
            CheckLaunch(state, before);

            yield return Host();
            yield return StartMatch(false);
            yield return 2f;
            yield return TakeBriefcase();
            yield return 1.5f;
            yield return CarryToRoof();
            yield return new Until(() => _finals == 1, 5f, "this player's final record");
            CheckSoloRecord(before);
            yield return new Until(() => before > 0 || Hinted.Contains("carrying"), 20f, "the queued carrying hint");
            CheckSoloVoice(before);
        }

        static void CheckLaunch(ProfileStore.LoadState state, int before)
        {
            if (Flag("-gapsCorrupt"))
            {
                Check("corrupt file: a fresh profile started, no error logged", state == ProfileStore.LoadState.RecoveredFromCorrupt && before == 0 && _profileErrors == 0, $"{state}, errors {_profileErrors}");
                Check("corrupt file: the unreadable file was kept as .corrupt", File.Exists(ProfileStore.FilePath + ".corrupt"));
            }
            else if (state != ProfileStore.LoadState.Fresh)
            {
                Check("relaunch: last launch's profile loaded", state == ProfileStore.LoadState.Loaded && before >= 1, $"{state}, {ShowProfile()}");
            }
        }

        static void CheckSoloRecord(int before)
        {
            Records.TryGetLastFinal(out MatchFinal final);
            Note("final record: " + Show(final.Line));
            Check("solo: the finished match was added to the profile", P(ProfileStat.MatchesPlayed) == before + 1 && P(ProfileStat.Wins) >= 1, ShowProfile());
            Check("solo: carry time and a pick-up counted", final.Line.Pickups == 1 && final.Line.CarrySeconds >= 1, Show(final.Line));
            Check("solo: the file on disk holds the new count", File.ReadAllText(ProfileStore.FilePath).Contains("\"stats\"") && !File.Exists(ProfileStore.FilePath + ".tmp"));
            Check("solo: no profile or records errors in the log", _profileErrors == 0, _profileErrors.ToString());
        }

        static void CheckSoloVoice(int before)
        {
            Check("secretary: spoke at the start and on the win", Said.Contains("MatchStart") && Said.Contains("Won"), string.Join(", ", Said));
            if (before == 0)
                Check("first run: the briefcase rule and carrying hints showed and are remembered", Hinted.Contains("briefcase-rule") && Hinted.Contains("carrying") && ProfileStore.Current.HasSeen("briefcase-rule"), string.Join(", ", Hinted));
            else
                Check("second launch: hints already seen were not shown again", !Hinted.Contains("briefcase-rule") && !Hinted.Contains("carrying"), Hinted.Count == 0 ? "none shown" : string.Join(", ", Hinted));
        }

        static ProfileStore.LoadState LoadNow()
        {
            _ = ProfileStore.Current;
            return ProfileStore.State;
        }

        // ---- Host --------------------------------------------------------------------------------

        static IEnumerator HostScript()
        {
            Note("ROLE host: hosting; a second copy joins late as the client. Profile at " + ProfileStore.FilePath);
            int before = P(ProfileStat.MatchesPlayed);
            yield return Host();
            yield return StartMatch(false);
            Note("match 1 running before anyone else joined");
            yield return new Until(() => FirmMember.All.Count == 2, 240f, "the client to join late");
            FirmMember them = Other();
            if (them == null)
                yield break;

            yield return new Until(() => Records.TryGetLine(them.Owner.ClientId, out _), 5f, "the late joiner's line");
            Check("HOST: the late joiner has a line of their own", Records.TryGetLine(them.Owner.ClientId, out MatchLine joined), Show(joined));
            yield return 3f;
            yield return HostKills(them);
            yield return HostKnocksOut(them);
            yield return TakeBriefcase();
            yield return 2.5f;
            yield return CarryToRoof();
            yield return new Until(() => _finals == 1, 5f, "the host's own final record");
            Check("HOST: match 1 is in the host's profile as a win with two kills", P(ProfileStat.MatchesPlayed) == before + 1 && P(ProfileStat.Wins) >= 1 && P(ProfileStat.Kills) >= 2, ShowProfile());

            yield return 6f;
            yield return StartMatch(true);
            yield return 3f;
            them.GetComponent<Health>().ApplyDamage(1000f, them.Chest, Vector3.forward, MyId);
            yield return TakeBriefcase();
            yield return CarryToRoof();
            Check("HOST: match 2 ended while the client was dead", them.GetComponent<Health>().IsDead, "client dead at the end");
            yield return new Until(() => _finals == 2, 5f, "the host's second final record");

            yield return 6f;
            yield return StartMatch(true);
            yield return 4f;
            Note("match 3 running: the host now leaves mid-match");
            int matches = P(ProfileStat.MatchesPlayed);
            InstanceFinder.ServerManager.StopConnection(true);
            InstanceFinder.ClientManager.StopConnection();
            yield return 2f;
            Check("HOST: leaving mid-match wrote nothing to the host's profile", P(ProfileStat.MatchesPlayed) == matches && !File.Exists(ProfileStore.FilePath + ".tmp"), ShowProfile());
            yield return 3f;
        }

        static IEnumerator HostKills(FirmMember them)
        {
            int theirDeaths = LineOf(them).Deaths, myKills = LineOf(Me).Kills;
            them.GetComponent<Health>().ApplyDamage(1000f, them.Chest, Vector3.forward, MyId);
            yield return 0.5f;
            Check("HOST: a kill counts for the killer and a death for the victim", LineOf(them).Deaths == theirDeaths + 1 && LineOf(Me).Kills == myKills + 1, $"them {Show(LineOf(them))} · me {Show(LineOf(Me))}");
            yield return new Until(() => !them.GetComponent<Health>().IsDead, 10f, "the client to respawn");
            yield return 1f;
        }

        static IEnumerator HostKnocksOut(FirmMember them)
        {
            int falls = LineOf(them).Falls, windowKills = LineOf(Me).WindowKills;
            them.GetComponent<Health>().ApplyDamage(10f, them.Chest, Vector3.forward, MyId);
            yield return 0.3f;
            them.ServerTeleport(them.Position + Vector3.down * 60f);
            yield return new Until(() => LineOf(them).Falls > falls, 6f, "the fall out");
            Check("HOST: a fall after the host's hit is the host's window kill", LineOf(them).Falls == falls + 1 && LineOf(Me).WindowKills == windowKills + 1, $"them {Show(LineOf(them))} · me {Show(LineOf(Me))}");
            yield return 1.5f;
        }

        // ---- Client ------------------------------------------------------------------------------

        static IEnumerator ClientScript()
        {
            Note("ROLE client: joins the host's match late. Everything here is what the client saw. Profile at " + ProfileStore.FilePath);
            var lobby = Object.FindAnyObjectByType<SteamLobby>();
            yield return new Until(() => lobby.Ready && Records != null, 30f, "the network to be ready");
            yield return KeepJoining(lobby);
            if (Me == null)
                yield break;

            WatchFinals();
            Check("CLIENT: this copy is not the host", !InstanceFinder.IsServerStarted);
            Check("CLIENT: joined while the match was already running", _phaseAtJoin == MatchPhase.Running, "phase when this player spawned: " + _phaseAtJoin);
            yield return new Until(() => Records.TryGetLocalLine(out _), 10f, "this player's line from the host");
            Check("CLIENT: the host's lines arrived, its own and the host's", Records.Lines.Count == 2, Records.Lines.Count + " lines");

            yield return ClientMatchOne();

            yield return new Until(() => Match.Phase == MatchPhase.Running, 60f, "match 2 to run");
            yield return new Until(() => Match.Phase == MatchPhase.Ended, 60f, "match 2 to end");
            yield return new Until(() => _finals == 2, 5f, "the second final record");
            Check("CLIENT: dead when match 2 ended, and still recorded", _deadAtFinal && P(ProfileStat.MatchesPlayed) == 2 && P(ProfileStat.Deaths) == 2, ShowProfile());

            yield return new Until(() => Match.Phase == MatchPhase.Running, 60f, "match 3 to run");
            int matches = P(ProfileStat.MatchesPlayed);
            yield return new Until(() => !InstanceFinder.ClientManager.Started, 60f, "the host to leave");
            yield return 2f;
            Check("CLIENT: the host left mid-match: nothing written, lines cleared", P(ProfileStat.MatchesPlayed) == matches && Records.Lines.Count == 0 && !File.Exists(ProfileStore.FilePath + ".tmp"), ShowProfile());
            Check("CLIENT: no profile or records errors in the log", _profileErrors == 0, _profileErrors.ToString());
            Check("CLIENT: the secretary called the window kill and the loss", Said.Contains("WindowKill") && Said.Contains("Lost"), string.Join(", ", Said));
            Check("CLIENT: the death and fall hints showed once each", Hinted.FindAll(h => h == "died").Count == 1 && Hinted.FindAll(h => h == "fell-out").Count == 1, string.Join(", ", Hinted));
        }

        static IEnumerator ClientMatchOne()
        {
            yield return new Until(() => Match.Phase == MatchPhase.Ended, 120f, "match 1 to end");
            ClaimNinetyNineKills();
            yield return new Until(() => _finals == 1, 5f, "this player's final record");
            Records.TryGetLastFinal(out MatchFinal final);
            Note("final record: " + Show(final.Line));
            Check("CLIENT: its own claimed 99 kills were ignored; the host's 0 went to the profile", P(ProfileStat.Kills) == 0 && final.Line.Kills == 0, ShowProfile());
            Check("CLIENT: match 1 in the profile as a loss with the host's death, fall and time", P(ProfileStat.MatchesPlayed) == 1 && P(ProfileStat.Losses) == 1 && P(ProfileStat.Deaths) == 1 && P(ProfileStat.Falls) == 1 && final.Line.SecondsPlayed > 0, ShowProfile());
        }

        static IEnumerator KeepJoining(SteamLobby lobby)
        {
            float nextTry = 0f;
            yield return new Until(() =>
            {
                if (Me != null && Match != null && _phaseAtJoin == null)
                    _phaseAtJoin = Match.Phase;
                if (Me != null && Match != null && Match.Phase == MatchPhase.Running)
                    return true;
                if (!lobby.IsActive && Time.time >= nextTry)
                {
                    nextTry = Time.time + 2f;
                    lobby.JoinLocal();
                }

                return false;
            }, 240f, "the client to join a running match");
            yield return 1f;
        }

        /// <summary>Writes a made-up 99 kills into this machine's own copy of its line, as a cheating client might.</summary>
        static void ClaimNinetyNineKills()
        {
            FieldInfo field = typeof(MatchRecords).GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic);
            var lines = (List<MatchLine>)field.GetValue(Records);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].ClientId != MyId)
                    continue;
                MatchLine claimed = lines[i];
                claimed.Kills = 99;
                lines[i] = claimed;
            }
        }
    }
}

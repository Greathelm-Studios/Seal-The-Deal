#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CloseTheDeal.Net;
using CloseTheDeal.UI;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CloseTheDeal.Player.Dev
{
    /// <summary>
    /// A scripted play session for the movement lane scene. It plays the game through
    /// made-up keyboard, mouse and pad devices, so every press goes through the real bindings
    /// and the real input reader, then measures what the body did and saves camera frames.
    /// It only exists in the editor and in development builds, and only wakes up when the
    /// game is started with -movementSession solo|host|client. Start it through
    /// MovementLaneSetup (docs/systems/PLAYER_MOVEMENT.md, "Checking it").
    /// </summary>
    public sealed class PlayerMovementSession : MonoBehaviour
    {
        const float StairRadius = 1.7f;
        const float HardLimitSeconds = 300f;
        const int CaptureWidth = 1280;
        const int CaptureHeight = 720;
        const float AimGain = 0.5f;

        string _role;
        string _outFolder;
        string _logPath;
        int _latencyMs;
        int _failures;
        int _captures;

        Keyboard _keyboard;
        Mouse _mouse;
        Gamepad _pad;
        readonly List<Key> _held = new();
        bool _leftMouse;

        SteamLobby _lobby;
        PlayerMotor _remote;
        Camera _observer;
        RenderTexture _target;
        Texture2D _readback;

        // Tick samples for whatever window is open.
        bool _windowOpen;
        int _ticks, _airTicks;
        float _minSpeed, _maxSpeed, _speedSum, _minRise, _maxRise, _lastY, _topY;
        // Whole-session watch on the local body.
        float _worstCorrection;
        int _lastCorrectionCount;
        float _worstCameraRatio;
        int _cameraBacksteps;
        Vector3 _lastCameraPosition;
        bool _hasCameraPosition;
        // What the local machine saw the other player do.
        float _remoteMaxCrouch, _remoteMaxLean, _remoteMaxSpeed;
        bool _remoteSprinted, _remoteMantled, _remoteAirborne, _remoteSlid;
        int _localJumps, _localLands, _localRefusals, _localMantles, _localKnocks;
        float _lastLandSpeed;
        MantleKind _lastMantleKind;
        MantleRefusal _lastRefusal;

        static PlayerMotor Me => PlayerMotor.Local;
        static Rigidbody Body => Me.GetComponent<Rigidbody>();
        static PlayerCamera View => PlayerCamera.Instance;
        static Vector3 Feet => Me.transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string role = Arg("-movementSession");
            if (role == null)
                return;

            var go = new GameObject("Movement session");
            DontDestroyOnLoad(go);
            var session = go.AddComponent<PlayerMovementSession>();
            session._role = role;
            session._outFolder = Arg("-sessionOut") ?? Application.temporaryCachePath;
            int.TryParse(Arg("-sessionLatency") ?? "0", out session._latencyMs);
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        // ---- Session shell -------------------------------------------------------------------

        IEnumerator Start()
        {
            PrepareRun();

            _lobby = FindAnyObjectByType<SteamLobby>();
            yield return Until(() => _lobby != null && _lobby.Ready, 30f, "lobby ready");
            UseOwnPort();
            Note($"role {_role}, local mode {_lobby.LocalMode}, latency each way {_latencyMs} ms, graphics {SystemInfo.graphicsDeviceType}");

            if (_role == "client")
                yield return JoinAsClient();
            else
                _lobby.Host();

            yield return Until(() => Me != null, 30f, "local player spawned");
            if (Me != null)
            {
                AddLatency();
                InstanceFinder.TimeManager.OnPostTick += OnPostTick;
                HookLocal(Me);
                Check("first spawn is idle on the floor", Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, $"state {Me.State}, speed {Me.PlanarSpeed:0.00}");
                yield return RunRole();
            }

            Finish();
        }

        IEnumerator RunRole()
        {
            switch (_role)
            {
                case "solo": return Solo();
                case "host": return Host();
                case "client": return Client();
                case "tower": return Tower();
                default:
                    Check("known session role", false, _role);
                    return Wait(0f);
            }
        }

        InputSettings.BackgroundBehavior _authoredBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _authoredEditorInput;
#endif

        /// <summary>Log file, frame pacing, and made-up devices that work without a focused window.</summary>
        void PrepareRun()
        {
            Directory.CreateDirectory(_outFolder);
            _logPath = Path.Combine(_outFolder, _role + ".log");
            File.WriteAllText(_logPath, string.Empty);
            Application.logMessageReceived += OnUnityLog;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;

            // Project settings: changed for the run only and put back in Finish.
            _authoredBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _authoredEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _keyboard = InputSystem.AddDevice<Keyboard>("Session keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Session mouse");
            _pad = InputSystem.AddDevice<Gamepad>("Session pad");
            PlayerInputReader.ScriptedCapture = true;
        }

        void RestoreSettings()
        {
            InputSystem.settings.backgroundBehavior = _authoredBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _authoredEditorInput;
#endif
            PlayerInputReader.ScriptedCapture = false;
        }

        /// <summary>Other people's test sessions on this Mac use the default local port; keep to ours.</summary>
        void UseOwnPort()
        {
            ushort port = ushort.TryParse(Arg("-sessionPort"), out ushort chosen) ? chosen : (ushort)17781;
            var tugboat = FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat != null)
                tugboat.SetPort(port);
            Note($"local port {port}");
        }

        /// <summary>Delays this instance's outgoing packets; with the same on the other side, the round trip is twice this.</summary>
        void AddLatency()
        {
            if (_latencyMs <= 0)
                return;

            var simulator = InstanceFinder.TransportManager.LatencySimulator;
            simulator.SetEnabled(true);
            simulator.SetLatency(_latencyMs);
        }

        IEnumerator JoinAsClient()
        {
            // The host may still be starting; a join that finds nobody just drops back to the menu.
            for (int attempt = 0; attempt < 20 && Me == null; attempt++)
            {
                if (!_lobby.IsActive)
                    _lobby.JoinLocal();
                yield return Wait(3f);
            }
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > HardLimitSeconds)
            {
                Check("session finished inside its time limit", false, "hard limit hit");
                Finish();
                return;
            }

            if (Me == null)
                return;

            WatchLocal();
            WatchRemote();
            if (_role != "solo")
            {
                LogPose("own", Me.AnimState, ref _lastOwnPose);
                if (_remote != null)
                    LogPose("other", _remote.AnimState, ref _lastOtherPose);
            }
        }

        int _lastOwnPose = -1, _lastOtherPose = -1;

        /// <summary>One line whenever a body's crouch, lean, slide, sprint, grounded or climb state changes, for lining one machine's view of a body up with the other's.</summary>
        void LogPose(string who, in PlayerAnimState a, ref int last)
        {
            int packed = Mathf.RoundToInt(a.Crouch * 4f) | (Mathf.RoundToInt((a.Lean + 1f) * 2f) << 4) | ((a.Sliding ? 1 : 0) << 8)
                | ((a.Sprinting ? 1 : 0) << 9) | ((a.Grounded ? 1 : 0) << 10) | ((a.Mantling ? 1 : 0) << 11);
            if (packed == last)
                return;

            last = packed;
            Note($"pose {who}: crouch {a.Crouch:0.00} lean {a.Lean:0.00} slide {a.Sliding} sprint {a.Sprinting} grounded {a.Grounded} climbing {a.Mantling} speed {a.PlanarSpeed:0.0}");
        }

        void Finish()
        {
            if (_logPath == null)
                return;

            if (_corrections != null)
                File.WriteAllText(Path.Combine(_outFolder, _role + "_corrections.csv"), _corrections.ToString());
            Note($"RESULT {(_failures == 0 ? "PASS" : "FAIL")} failures {_failures} captures {_captures}");
            Application.logMessageReceived -= OnUnityLog;
            RestoreSettings();
            _logPath = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(_failures == 0 ? 0 : 1);
#else
            Application.Quit(_failures == 0 ? 0 : 1);
#endif
        }

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error)
                return;

            // The editor's own background jobs (its search indexer, for one) are not the game.
            if (stackTrace.Contains("UnityEditor.") && !stackTrace.Contains("CloseTheDeal"))
                return;

            _failures++;
            File.AppendAllText(_logPath, $"UNITY {type}: {condition}\n{stackTrace}\n");
        }

        void Note(string line)
        {
            string stamped = string.Format(CultureInfo.InvariantCulture, "{0,7:0.00}  {1}", Time.time, line);
            Debug.Log("[Session] " + stamped);
            if (_logPath != null)
                File.AppendAllText(_logPath, stamped + "\n");
        }

        void Check(string what, bool ok, string detail)
        {
            if (!ok)
                _failures++;
            Note($"{(ok ? "PASS" : "FAIL")}  {what}  [{detail}]");
        }

        // ---- Watching ------------------------------------------------------------------------

        void HookLocal(PlayerMotor motor)
        {
            motor.Jumped += () => _localJumps++;
            motor.Landed += speed => { _localLands++; _lastLandSpeed = speed; };
            motor.MantleStarted += kind => { _localMantles++; _lastMantleKind = kind; };
            motor.MantleRefused += why => { _localRefusals++; _lastRefusal = why; };
            motor.KnockedDown += () => { _localKnocks++; Note($"event: knocked down at {Feet}, state {Me.State}"); };
        }

        void HookRemote(PlayerMotor motor)
        {
            _remote = motor;
            motor.Jumped += () => Note("remote event: jumped");
            motor.Landed += speed => Note($"remote event: landed at {speed:0.0} m/s");
            motor.MantleStarted += kind => Note("remote event: mantle started, " + kind);
            motor.CrouchChanged += down => Note("remote event: crouch " + (down ? "down" : "up"));
            motor.SprintChanged += on => Note("remote event: sprint " + (on ? "on" : "off"));
            motor.SlideChanged += on => Note("remote event: slide " + (on ? "on" : "off"));
            motor.KnockedDown += () => Note("remote event: knocked");
        }

        void OnPostTick()
        {
            if ((!_windowOpen && _trace == null) || Me == null)
                return;

            float speed = Me.PlanarSpeed;
            float y = Feet.y;
            _trace?.Append((Time.time - _traceStart).ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(speed.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(Me.State).Append('\n');
            _ticks++;
            if (Me.State == MoveState.Airborne)
                _airTicks++;
            _minSpeed = Mathf.Min(_minSpeed, speed);
            _maxSpeed = Mathf.Max(_maxSpeed, speed);
            _speedSum += speed;
            if (_ticks > 1)
            {
                _minRise = Mathf.Min(_minRise, y - _lastY);
                _maxRise = Mathf.Max(_maxRise, y - _lastY);
            }
            _lastY = y;
            _topY = Mathf.Max(_topY, y);
        }

        void OpenWindow()
        {
            _windowOpen = true;
            _ticks = _airTicks = 0;
            _minSpeed = float.MaxValue;
            _maxSpeed = _speedSum = 0f;
            _minRise = float.MaxValue;
            _maxRise = float.MinValue;
            _topY = float.MinValue;
        }

        string CloseWindow()
        {
            _windowOpen = false;
            float mean = _ticks > 0 ? _speedSum / _ticks : 0f;
            return $"ticks {_ticks}, airborne ticks {_airTicks}, speed min {_minSpeed:0.00} mean {mean:0.00} max {_maxSpeed:0.00} m/s, rise per tick min {_minRise * 100f:0.0} max {_maxRise * 100f:0.0} cm, top {_topY:0.00} m";
        }

        /// <summary>Corrections from the host, and whether the view ever moved faster than the body or against it.</summary>
        void WatchLocal()
        {
            if (Me.CorrectionCount != _lastCorrectionCount)
            {
                _lastCorrectionCount = Me.CorrectionCount;
                LogCorrection();
                _worstCorrection = Mathf.Max(_worstCorrection, Me.LastCorrectionMetres);
                _sectionWorst = Mathf.Max(_sectionWorst, Me.LastCorrectionMetres);
            }

            if (View == null)
                return;

            Vector3 now = View.transform.position;
            if (_hasCameraPosition && Time.deltaTime > 0f && Me.State != MoveState.Mantling)
            {
                // Level only: the landing dip moves the view down on purpose.
                Vector3 moved = now - _lastCameraPosition;
                moved.y = 0f;
                Vector3 velocity = Body.linearVelocity;
                velocity.y = 0f;
                float bodySpeed = velocity.magnitude;
                if (bodySpeed > 2f)
                {
                    _worstCameraRatio = Mathf.Max(_worstCameraRatio, moved.magnitude / Time.deltaTime / bodySpeed);
                    if (Vector3.Dot(moved, velocity.normalized) < -0.02f)
                        _cameraBacksteps++;
                }
            }
            _lastCameraPosition = now;
            _hasCameraPosition = true;
        }

        string _section;
        int _sectionCorrectionsAtStart;
        float _sectionWorst;
        int _sectionRemoteBacksteps;
        float _sectionRemoteWorstStep;
        int _sectionCameraBacksteps;
        Transform _remoteGraphics;
        Vector3 _lastRemotePosition;
        bool _hasRemotePosition;

        /// <summary>Closes the last part of the route with its own numbers, and starts counting afresh.</summary>
        void Section(string next)
        {
            if (_section != null)
            {
                int corrections = Me.CorrectionCount - _sectionCorrectionsAtStart;
                Note($"MEASURE section '{_section}': {corrections} corrections over 1 cm, worst {_sectionWorst * 100f:0.0} cm; own view moved against the body on {_cameraBacksteps - _sectionCameraBacksteps} frames; other player's body moved against its velocity on {_sectionRemoteBacksteps} frames, biggest single-frame step {_sectionRemoteWorstStep:0.00}x its speed; ping {InstanceFinder.TimeManager.RoundTripTime} ms");
            }

            _section = next;
            _sectionCorrectionsAtStart = Me != null ? Me.CorrectionCount : 0;
            _sectionWorst = 0f;
            _sectionRemoteBacksteps = 0;
            _sectionRemoteWorstStep = 0f;
            _sectionCameraBacksteps = _cameraBacksteps;
        }

        System.Text.StringBuilder _corrections;

        /// <summary>One CSV row per correction seen (at most one a frame): how big, whether it was a clock-label offset, and what the body was doing.</summary>
        void LogCorrection()
        {
            _corrections ??= new System.Text.StringBuilder("seconds,section,metres,state,speed,sliding,crouch,ping_ms\n");
            _corrections.Append(Time.time.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(_section.Replace(',', ';')).Append(',')
                .Append(Me.LastCorrectionMetres.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(Me.State).Append(',')
                .Append(Me.PlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(Me.AnimState.Sliding).Append(',')
                .Append(Me.Crouch.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append((int)InstanceFinder.TimeManager.RoundTripTime).Append('\n');
        }

        void WatchRemote()
        {
            if (_remote == null)
                return;

            WatchRemoteSmoothness();

            PlayerAnimState seen = _remote.AnimState;
            _remoteMaxCrouch = Mathf.Max(_remoteMaxCrouch, seen.Crouch);
            _remoteMaxLean = Mathf.Max(_remoteMaxLean, Mathf.Abs(seen.Lean));
            _remoteMaxSpeed = Mathf.Max(_remoteMaxSpeed, seen.PlanarSpeed);
            _remoteSprinted |= seen.Sprinting;
            _remoteMantled |= seen.Mantling;
            _remoteAirborne |= !seen.Grounded && !seen.Mantling;
            _remoteSlid |= seen.Sliding;
        }

        /// <summary>How the other player's drawn body moves on this screen: never backwards against its own velocity, never in jumps.</summary>
        void WatchRemoteSmoothness()
        {
            if (_remoteGraphics == null)
                _remoteGraphics = _remote.transform.Find("Graphics");
            if (_remoteGraphics == null || Time.deltaTime <= 0f)
                return;

            Vector3 now = _remoteGraphics.position;
            Vector3 velocity = _remote.GetComponent<Rigidbody>().linearVelocity;
            float speed = velocity.magnitude;
            if (_hasRemotePosition && speed > 2f)
            {
                Vector3 moved = now - _lastRemotePosition;
                if (Vector3.Dot(moved, velocity / speed) < -0.02f)
                    _sectionRemoteBacksteps++;
                _sectionRemoteWorstStep = Mathf.Max(_sectionRemoteWorstStep, moved.magnitude / Time.deltaTime / speed);
            }
            _lastRemotePosition = now;
            _hasRemotePosition = true;
        }

        // ---- Made-up devices -----------------------------------------------------------------

        void Press(params Key[] keys)
        {
            foreach (Key key in keys)
                if (!_held.Contains(key))
                    _held.Add(key);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_held.ToArray()));
        }

        void Release(params Key[] keys)
        {
            foreach (Key key in keys)
                _held.Remove(key);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_held.ToArray()));
        }

        void ReleaseAll()
        {
            _held.Clear();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            _leftMouse = false;
            MoveMouse(Vector2.zero);
        }

        void MoveMouse(Vector2 counts)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = counts }.WithButton(MouseButton.Left, _leftMouse));
        }

        IEnumerator Tap(Key key)
        {
            Press(key);
            yield return Wait(0.05f);
            Release(key);
            yield return null;
        }

        /// <summary>Turns the view with the mouse until it points where asked. Yaw 0 is +Z, 90 is +X; pitch is positive looking down.</summary>
        IEnumerator Face(float yaw, float pitch = 0f)
        {
            // Settled only once the view has held still on target for a few frames, so a move
            // still in flight can't land after this returns.
            float until = Time.time + 2f;
            int settled = 0;
            while (Time.time < until)
            {
                settled = Aim(yaw, pitch) ? 0 : settled + 1;
                if (settled >= 4)
                    yield break;
                yield return null;
            }
            Check("the scripted mouse reaches the asked view", false,
                $"wanted yaw {yaw:0.0} pitch {pitch:0.0}, got {View.Yaw:0.0} / {View.Pitch:0.0}; reader captured {PlayerInputReader.Captured}, reader look {Me.GetComponent<PlayerInputReader>().LookDelta}, reader enabled {Me.GetComponent<PlayerInputReader>().enabled}, session mouse added {_mouse.added}, Mouse.current {Mouse.current?.name}");
        }

        /// <summary>One frame of mouse toward the wanted view. False once it is already there.</summary>
        bool Aim(float yaw, float pitch)
        {
            float turn = Mathf.DeltaAngle(View.Yaw, yaw);
            // Past the limit the view stops there; aim for the limit so it can settle.
            float tilt = Mathf.Clamp(pitch, -Me.Profile.LookUpLimit, Me.Profile.LookDownLimit) - View.Pitch;

            if (Mathf.Abs(turn) < 0.05f && Mathf.Abs(tilt) < 0.05f)
            {
                MoveMouse(Vector2.zero);
                return false;
            }

            // Half the error a frame: the move just sent may not have reached the view yet, and
            // sending the whole error again on top of it would swing past and never settle.
            float perCount = Me.Profile.MouseSensitivity;
            MoveMouse(new Vector2(turn, -tilt) * (AimGain / perCount));
            return true;
        }

        static float YawToward(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        IEnumerator WalkTo(Vector3 point, bool sprint, float arrive = 0.35f, float limit = 12f)
        {
            Press(Key.W);
            if (sprint)
                Press(Key.LeftShift);

            float until = Time.time + limit;
            while (Time.time < until)
            {
                Vector3 to = point - Feet;
                to.y = 0f;
                if (to.magnitude < arrive)
                    break;
                Aim(YawToward(to), 0f);
                yield return null;
            }

            Release(Key.W, Key.LeftShift);
            MoveMouse(Vector2.zero);
            yield return Until(() => Me.PlanarSpeed < 0.1f, 2f, null);
        }

        /// <summary>
        /// Puts the body somewhere, still, facing a way: the host teleports it; a client, which
        /// must never move its own body outside a tick, sprints there across the floor.
        /// </summary>
        IEnumerator Place(Vector3 feet, float yaw)
        {
            if (!InstanceFinder.IsServerStarted)
            {
                ReleaseAll();
                yield return WalkTo(feet, true);
                yield return Face(yaw);
                yield return Wait(0.3f);
                yield break;
            }

            ReleaseAll();
            yield return Wait(0.2f);
            Body.position = feet;
            Me.transform.position = feet;
            Body.linearVelocity = Vector3.zero;
            yield return Wait(0.4f);
            yield return Face(yaw);
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        IEnumerator Until(Func<bool> done, float limit, string failAs)
        {
            float until = Time.time + limit;
            while (!done() && Time.time < until)
                yield return null;
            if (failAs != null && !done())
                Check(failAs, false, $"not within {limit:0} s");
        }

        // ---- Frames --------------------------------------------------------------------------

        void Capture(string name, Camera camera = null)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;

            camera = camera != null ? camera : Camera.main;
            if (_target == null)
            {
                _target = new RenderTexture(CaptureWidth, CaptureHeight, 24);
                _readback = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            }

            RenderTexture before = camera.targetTexture;
            camera.targetTexture = _target;
            camera.Render();
            camera.targetTexture = before;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            _readback.Apply();
            RenderTexture.active = active;

            _captures++;
            File.WriteAllBytes(Path.Combine(_outFolder, $"{_role}_{_captures:00}_{name}.png"), _readback.EncodeToPNG());
        }

        /// <summary>A second camera looking at a body from outside, since the owner never sees their own.</summary>
        void CaptureFromOutside(string name, Vector3 subject, Vector3 offset)
        {
            if (_observer == null)
            {
                _observer = new GameObject("Session observer").AddComponent<Camera>();
                _observer.enabled = false;
                _observer.fieldOfView = 60f;
            }

            Vector3 lookAt = subject + Vector3.up;
            _observer.transform.position = lookAt + offset;
            _observer.transform.rotation = Quaternion.LookRotation(lookAt - _observer.transform.position);
            Capture(name, _observer);
        }

        // ---- Scene lookups -------------------------------------------------------------------

        static Bounds CourseBounds(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found == null)
                throw new InvalidOperationException("Lane scene has no '" + name + "'. Run Close the Deal > Lanes > Set Up Movement.");

            var bounds = new Bounds(found.transform.position, Vector3.zero);
            foreach (Collider collider in found.GetComponentsInChildren<Collider>())
                bounds.Encapsulate(collider.bounds);
            return bounds;
        }

        /// <summary>The ground floor's stair column: the lowest object called Stairs, the nearest one when there are two towers.</summary>
        static Vector3 StairCentre()
        {
            Transform lowest = null;
            Vector3 feet = Me != null ? Feet : Vector3.zero;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "Stairs")
                    continue;
                bool lower = lowest == null || t.position.y < lowest.position.y - 0.1f;
                bool sameLevelNearer = lowest != null && Mathf.Abs(t.position.y - lowest.position.y) <= 0.1f
                    && (t.position - feet).sqrMagnitude < (lowest.position - feet).sqrMagnitude;
                if (lower || sameLevelNearer)
                    lowest = t;
            }
            if (lowest == null)
                throw new InvalidOperationException("Lane scene has no 'Stairs'.");
            return lowest.position;
        }

        /// <summary>The rise of one flight: to the lane slice's upper floor, or in a tower to the next stair column up.</summary>
        static float FloorHeight()
        {
            GameObject upper = GameObject.Find("Upper floor");
            if (upper != null)
                return upper.transform.position.y - StairCentre().y;

            if (CloseTheDeal.Tower.TowerBuilder.Instance != null)
                return CloseTheDeal.Tower.TowerBuilder.Instance.Profile.FloorHeight;

            Vector3 lowest = StairCentre();
            float next = float.MaxValue;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Vector3 p = t.position;
                if (t.name == "Stairs" && p.y > lowest.y + 0.5f && Mathf.Abs(p.x - lowest.x) < 0.5f && Mathf.Abs(p.z - lowest.z) < 0.5f)
                    next = Mathf.Min(next, p.y);
            }
            if (next == float.MaxValue)
                throw new InvalidOperationException("No stairs above the lowest flight.");
            return next - lowest.y;
        }

        /// <summary>The real tower from the floor templates: the same sprint up and down its lobby flight.</summary>
        IEnumerator Tower()
        {
            yield return Wait(1f);
            Note($"tower: lowest stairs at {StairCentre()}, flight rises {FloorHeight():0.00} m; player at {Feet}");
            yield return StairsUpCheck();
            yield return StairsDownCheck();
        }

        // ---- Solo: every move, measured ------------------------------------------------------

        IEnumerator Solo()
        {
            yield return Wait(0.5f);
            yield return LookChecks();
            yield return WalkSprintChecks();
            yield return JumpChecks();
            yield return CrouchChecks();
            yield return LeanCheck();
            yield return DeskClimbCheck();
            yield return SillVaultCheck();
            yield return LedgeClimbChecks();
            yield return WallRefusalCheck();
            yield return MantleKeepsSpeedCheck();
            yield return BoxStepsCheck();
            yield return StairsUpCheck();
            yield return StairsDownCheck();
            yield return HopChecks();
            yield return AirStrafeCheck();
            yield return SlideChecks();
            yield return RampSlideJumpCheck();
            yield return StairSlideMeasure();
            yield return MenuMidJumpCheck();
            yield return KnockMidMantleCheck();
            yield return RespawnCheck();
        }

        IEnumerator LookChecks()
        {
            yield return Face(0f);
            yield return Face(0f, -95f);
            Check("looks straight up", View.Pitch <= -88.9f, $"pitch {View.Pitch:0.0} deg");
            Capture("look_up");
            yield return Face(0f, 95f);
            Check("looks straight down", View.Pitch >= 88.9f, $"pitch {View.Pitch:0.0} deg");
            Capture("look_down");
            yield return Face(0f);

            yield return StickRate(new Vector2(1f, 0f), "full");
            yield return StickRate(new Vector2(0.5f, 0f), "half");
            yield return Frames(3);
            InputSystem.QueueStateEvent(_pad, new GamepadState { rightStick = new Vector2(0f, 1f) });
            yield return Frames(2);
            float before = View.Pitch;
            float started = Time.time;
            yield return Wait(0.3f);
            float pitchRate = (before - View.Pitch) / (Time.time - started);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            Note($"MEASURE stick pitch at full tilt {pitchRate:0} deg/s, pitch {before:0.0} to {View.Pitch:0.0} (profile {Me.Profile.StickPitchSpeed:0})");
            yield return Face(0f);
        }

        IEnumerator StickRate(Vector2 stick, string label)
        {
            yield return Frames(2);
            float turned = 0f;
            float last = View.Yaw;
            float started = Time.time;
            InputSystem.QueueStateEvent(_pad, new GamepadState { rightStick = stick });
            yield return Frames(2);
            last = View.Yaw;
            started = Time.time;
            while (Time.time - started < 1f)
            {
                yield return null;
                turned += Mathf.DeltaAngle(last, View.Yaw);
                last = View.Yaw;
            }
            float rate = turned / (Time.time - started);
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            Note($"MEASURE stick turn at {label} tilt {rate:0} deg/s");
            if (label == "full")
                Check("pad stick turns at a usable speed", rate > 120f, $"{rate:0} deg/s (was about 7)");
        }

        IEnumerator WalkSprintChecks()
        {
            yield return Place(new Vector3(-12f, 0.05f, -3.5f), 90f);
            MovementProfile p = Me.Profile;

            Vector3 from = Feet;
            float started = Time.time;
            OpenWindow();
            Press(Key.W);
            yield return Until(() => Me.PlanarSpeed >= p.WalkSpeed * 0.95f, 2f, "reaches walking speed");
            Note($"MEASURE walk: {Time.time - started:0.00} s to 95% of {p.WalkSpeed:0.0} m/s");
            yield return Wait(0.8f);
            Capture("walk");
            Check("walks at the profile speed", Mathf.Abs(Me.PlanarSpeed - p.WalkSpeed) < 0.2f, $"{Me.PlanarSpeed:0.00} m/s");
            Check("animation state while walking", Me.AnimState.LocalMove.y > p.WalkSpeed * 0.9f && Mathf.Abs(Me.AnimState.LocalMove.x) < 0.3f && !Me.AnimState.Sprinting && Me.AnimState.Grounded,
                $"local move {Me.AnimState.LocalMove}, sprinting {Me.AnimState.Sprinting}");

            Press(Key.LeftShift);
            yield return Wait(1f);
            Capture("sprint");
            Check("sprints at the profile speed", Mathf.Abs(Me.PlanarSpeed - p.SprintSpeed) < 0.2f, $"{Me.PlanarSpeed:0.00} m/s");
            Check("animation state says sprinting", Me.AnimState.Sprinting, $"fov {Camera.main.fieldOfView:0.0}");
            Note("MEASURE walk and sprint on the flat: " + CloseWindow());

            Vector3 at = Feet;
            started = Time.time;
            ReleaseAll();
            yield return Until(() => Me.PlanarSpeed < 0.05f, 2f, "stops when the keys are let go");
            Note($"MEASURE stop from sprint: {Time.time - started:0.00} s, {Vector3.Distance(at, Feet):0.00} m; ran {Vector3.Distance(from, at):0.0} m");

            // Strafing must read as sideways to a rig.
            Press(Key.D);
            yield return Wait(0.6f);
            Note($"strafe: camera yaw {View.Yaw:0.0}, body yaw {Me.transform.eulerAngles.y:0.0}, velocity {Body.linearVelocity}");
            Check("animation state while strafing right", Me.AnimState.LocalMove.x > p.WalkSpeed * 0.9f && Mathf.Abs(Me.AnimState.LocalMove.y) < 0.3f, $"local move {Me.AnimState.LocalMove}");
            ReleaseAll();
            yield return Wait(0.4f);
        }

        IEnumerator JumpChecks()
        {
            yield return Place(new Vector3(0f, 0.05f, -4f), 90f);
            MovementProfile p = Me.Profile;
            float floor = Feet.y;

            int jumps = _localJumps, lands = _localLands;
            OpenWindow();
            yield return Tap(Key.Space);
            yield return Until(() => _localLands > lands, 2f, "tap jump lands");
            float tap = _topY - floor;
            CloseWindow();

            // Held to the top, then let go, so hold-to-hop doesn't start a second jump.
            lands = _localLands;
            float started = Time.time;
            OpenWindow();
            Press(Key.Space);
            yield return Wait(0.3f);
            Capture("jump");
            yield return Wait(0.15f);
            Release(Key.Space);
            yield return Until(() => _localLands > lands, 2f, "held jump lands");
            float hold = _topY - floor;
            float airtime = Time.time - started;
            CloseWindow();

            Note($"MEASURE jump: tap {tap:0.00} m, hold {hold:0.00} m (profile {p.JumpHeight:0.00}), {airtime:0.00} s in the air, landed at {_lastLandSpeed:0.0} m/s");
            Check("a held jump reaches the profile height", Mathf.Abs(hold - p.JumpHeight) < 0.12f, $"{hold:0.00} m");
            Check("a tap is a shorter hop", tap < hold * 0.7f, $"{tap:0.00} m against {hold:0.00} m");
            Check("jump and landing events fired once each", _localJumps - jumps == 2 && _localLands - lands == 1, $"jumps {_localJumps - jumps}, landings since the hold {_localLands - lands}");

            float straight = 0f, pulled = 0f;
            yield return RunningJump(false, d => straight = d);
            yield return RunningJump(true, d => pulled = d);
            Note($"MEASURE running jump from a sprint: {straight:0.0} m held forward, {pulled:0.0} m when pulling back in the air");
            Check("pulling back in the air shortens a jump", pulled < straight - 1f, $"{pulled:0.0} m against {straight:0.0} m");
        }

        IEnumerator RunningJump(bool pullBack, Action<float> report)
        {
            yield return Place(new Vector3(-12f, 0.05f, -3.5f), 90f);
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.8f);
            Vector3 takeoff = Feet;
            int lands = _localLands;
            Press(Key.Space);
            yield return Wait(0.1f);
            if (pullBack)
            {
                Release(Key.W);
                Press(Key.S);
            }
            yield return Wait(0.3f);
            Release(Key.Space);
            yield return Until(() => _localLands > lands, 2f, "running jump lands");
            report(Vector3.Distance(takeoff, Feet));
            ReleaseAll();
            yield return Wait(0.4f);
        }

        IEnumerator CrouchChecks()
        {
            yield return Place(new Vector3(0f, 0.05f, -4f), 90f);
            MovementProfile p = Me.Profile;
            var capsule = Me.GetComponent<CapsuleCollider>();
            float standing = capsule.height;
            float standingEye = View.transform.position.y - Feet.y;

            Press(Key.LeftCtrl);
            yield return Wait(0.5f);
            float crouchedEye = View.transform.position.y - Feet.y;
            Check("crouch shrinks the body", Mathf.Abs(capsule.height - p.CrouchHeight) < 0.01f && Me.AnimState.Crouched, $"capsule {capsule.height:0.00} m from {standing:0.00} m");
            Note($"MEASURE crouch: eye {standingEye:0.00} m standing, {crouchedEye:0.00} m crouched");
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.8f);
            Check("crouched speed, sprint ignored", Mathf.Abs(Me.PlanarSpeed - p.CrouchSpeed) < 0.2f, $"{Me.PlanarSpeed:0.00} m/s");
            ReleaseAll();
            yield return Wait(0.5f);
            Check("stands back up", Mathf.Abs(capsule.height - standing) < 0.01f, $"capsule {capsule.height:0.00} m");

            // Through a 1.3 m gap: standing is refused, crouching fits, and letting go inside stays down.
            Bounds tunnel = CourseBounds("Crouch tunnel");
            Vector3 mouth = new(tunnel.min.x - 1.2f, 0.05f, tunnel.center.z);
            yield return Place(mouth, 90f);
            Press(Key.W);
            yield return Wait(1f);
            Check("a standing body does not fit under 1.3 m", Feet.x < tunnel.min.x, $"stopped at x {Feet.x:0.00}, mouth at {tunnel.min.x:0.00}");
            Press(Key.LeftCtrl);
            yield return Until(() => Feet.x > tunnel.center.x, 3f, "crouch-walks into the gap");
            Release(Key.W, Key.LeftCtrl);
            yield return Wait(0.6f);
            Capture("crouch_under_gap");
            Check("letting go of crouch under a low roof stays crouched", Me.Crouch > 0.99f, $"crouch {Me.Crouch:0.00}");
            Press(Key.W);
            yield return Until(() => Feet.x > tunnel.max.x + 0.6f, 3f, "walks out of the gap");
            ReleaseAll();
            yield return Wait(0.5f);
            Check("stands once clear of the roof", Me.Crouch < 0.01f, $"crouch {Me.Crouch:0.00}");
        }

        IEnumerator LeanCheck()
        {
            yield return Place(new Vector3(0f, 0.05f, -4f), 0f);
            Vector3 upright = View.transform.position;
            Press(Key.E);
            yield return Wait(0.5f);
            float right = View.transform.position.x - upright.x;
            Capture("lean_right");
            Release(Key.E);
            Press(Key.Q);
            yield return Wait(0.7f);
            float left = View.transform.position.x - upright.x;
            Note($"lean: camera yaw {View.Yaw:0.0}, motor lean {Me.Lean:0.00}, view offset {View.transform.position - upright}, feet {Feet}");
            ReleaseAll();
            Check("lean on Q and E", Mathf.Abs(right - Me.Profile.LeanDistance) < 0.03f && Mathf.Abs(left + Me.Profile.LeanDistance) < 0.03f, $"view slid {right:0.00} m right, {left:0.00} m left");
            yield return Wait(0.4f);
        }

        /// <summary>Space in front of a desk: climb onto it.</summary>
        IEnumerator DeskClimbCheck()
        {
            Bounds desk = CourseBounds("Desk 0.75 m");
            yield return Place(new Vector3(desk.center.x, 0.05f, desk.max.z + 0.7f), 180f);
            int mantles = _localMantles;
            float started = Time.time;
            yield return Tap(Key.Space);
            Capture("climb_desk_mid");
            yield return Until(() => Me.State != MoveState.Mantling && Time.time - started > 0.1f, 2f, null);
            float took = Time.time - started;
            yield return Wait(0.3f);
            Capture("climb_desk_top");
            Check("Space at a desk climbs onto it", _localMantles == mantles + 1 && _lastMantleKind == MantleKind.Climb && Mathf.Abs(Feet.y - desk.max.y) < 0.08f && Me.State == MoveState.Grounded,
                $"kind {_lastMantleKind}, feet at {Feet.y:0.00} m, {took:0.00} s");
        }

        /// <summary>Sprinting at the window sill: carried over it onto the street.</summary>
        IEnumerator SillVaultCheck()
        {
            yield return Place(new Vector3(4f, 0.05f, -4f), 0f);
            int mantles = _localMantles;
            Press(Key.W, Key.LeftShift);
            yield return Until(() => Feet.z > -1.1f, 2f, "runs up to the sill");
            float speedIn = Me.PlanarSpeed;
            float started = Time.time;
            yield return Tap(Key.Space);
            Capture("vault_sill_mid");
            yield return Until(() => Feet.z > 0.5f && Me.State == MoveState.Grounded, 2f, "lands on the street past the sill");
            float took = Time.time - started;
            float speedOut = Me.PlanarSpeed;
            Capture("vault_sill_landed");
            ReleaseAll();
            Check("Space at the 0.6 m sill vaults it", _localMantles == mantles + 1 && _lastMantleKind == MantleKind.Vault && Feet.z > 0.5f,
                $"kind {_lastMantleKind}, {took:0.00} s from press to standing on the street, in at {speedIn:0.0} m/s, {speedOut:0.0} m/s on landing");
            yield return Wait(0.4f);
        }

        /// <summary>The tallest ledge the profile allows, from a standstill and from a running jump.</summary>
        IEnumerator LedgeClimbChecks()
        {
            Bounds ledge = CourseBounds("Ledge 1.6 m");
            yield return Place(new Vector3(ledge.center.x, 0.05f, ledge.max.z + 0.7f), 180f);
            int mantles = _localMantles;
            float started = Time.time;
            yield return Tap(Key.Space);
            yield return Wait(0.12f);
            CaptureFromOutside("climb_ledge_mid_outside", Feet, new Vector3(3f, 1.2f, 3f));
            yield return Until(() => Me.State != MoveState.Mantling, 2f, null);
            float took = Time.time - started;
            yield return Wait(0.3f);
            Check("climbs a ledge at the profile's limit", _localMantles == mantles + 1 && Mathf.Abs(Feet.y - ledge.max.y) < 0.08f, $"feet at {Feet.y:0.00} m, {took:0.00} s (profile says {Me.Profile.MantleDuration:0.00} s for the tallest)");

            yield return Place(new Vector3(ledge.center.x, 0.05f, ledge.max.z + 3.5f), 180f);
            mantles = _localMantles;
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.2f);
            yield return Tap(Key.Space);
            yield return Until(() => _localMantles > mantles, 2f, "a running jump at a ledge grabs it");
            yield return Until(() => Me.State != MoveState.Mantling, 2f, null);
            Release(Key.W, Key.LeftShift);
            Check("a running jump ends on top of the ledge", Feet.y > ledge.max.y - 0.1f, $"feet at {Feet.y:0.00} m");
            yield return Wait(0.6f);
        }

        /// <summary>Too tall: refused, and it says so.</summary>
        IEnumerator WallRefusalCheck()
        {
            Bounds wall = CourseBounds("Wall 2.4 m");
            yield return Place(new Vector3(wall.center.x, 0.05f, wall.max.z + 0.7f), 180f);
            int mantles = _localMantles, refusals = _localRefusals, jumps = _localJumps, lands = _localLands;
            yield return Tap(Key.Space);
            yield return Wait(0.25f);
            Capture("refused_wall");
            yield return Until(() => _localLands > lands, 2f, "comes back down after a refused climb");
            yield return Wait(0.3f);
            Check("a 2.4 m wall is refused cleanly", _localRefusals == refusals + 1 && _lastRefusal == MantleRefusal.TooHigh && _localMantles == mantles && _localJumps == jumps + 1 && Feet.y < 0.1f && Me.State == MoveState.Grounded,
                $"refusals +{_localRefusals - refusals} ({_lastRefusal}), climbs +{_localMantles - mantles}, jumps +{_localJumps - jumps}, feet at {Feet.y:0.00} m, state {Me.State}");
        }

        /// <summary>Sprint at the desk and climb it without letting go: most of the speed must come out the other side.</summary>
        IEnumerator MantleKeepsSpeedCheck()
        {
            Bounds desk = CourseBounds("Desk 0.75 m");
            yield return Place(new Vector3(desk.center.x, 0.05f, desk.max.z + 4f), 180f);
            int mantles = _localMantles;
            Press(Key.W, Key.LeftShift);
            yield return Until(() => Feet.z < desk.max.z + 1.0f, 2f, "sprints up to the desk");
            float speedIn = Me.PlanarSpeed;
            yield return Tap(Key.Space);
            yield return Until(() => _localMantles > mantles, 1f, "the sprinting climb starts");
            float started = Time.time;
            yield return Until(() => Me.State != MoveState.Mantling, 2f, null);
            float took = Time.time - started;
            yield return Frames(2);
            float speedOut = Me.PlanarSpeed;
            ReleaseAll();
            Check("a climb keeps most of the speed it was entered with", speedOut >= speedIn * 0.8f, $"in at {speedIn:0.0} m/s, out at {speedOut:0.0} m/s, {took:0.00} s on the desk edge");
            yield return Wait(0.6f);
        }

        IEnumerator BoxStepsCheck()
        {
            Bounds steps = CourseBounds("Box steps");
            yield return Place(new Vector3(steps.min.x - 1.5f, 0.05f, steps.center.z), 90f);
            OpenWindow();
            Press(Key.W);
            yield return Wait(1.5f);
            ReleaseAll();
            Note($"MEASURE stepped stairs, 0.18 m risers and no ramp, walking: {CloseWindow()}; top of the steps is {steps.max.y:0.00} m");
            yield return Wait(0.3f);
        }

        /// <summary>Up one whole flight at a sprint.</summary>
        IEnumerator StairsUpCheck()
        {
            Vector3 centre = StairCentre();
            float rise = FloorHeight();
            yield return Place(centre + Spoke(-30f) * StairRadius + Vector3.up * 0.05f, YawToward(Tangent(-30f, 1f)));
            float started = Time.time;
            Press(Key.W, Key.LeftShift);
            // Up to speed on the floor first, so the slowest speed measured is the stairs', not the standing start.
            while (Me.PlanarSpeed < Me.Profile.SprintSpeed * 0.95f && Time.time - started < 1f)
            {
                SteerRoundStairs(centre, 1f);
                yield return null;
            }
            OpenWindow();
            bool captured = false;
            while (Feet.y < centre.y + rise - 0.1f && Time.time - started < 12f)
            {
                SteerRoundStairs(centre, 1f);
                if (!captured && Feet.y > centre.y + rise * 0.5f)
                {
                    captured = true;
                    Capture("stairs_halfway");
                }
                yield return null;
            }
            float took = Time.time - started;
            string up = CloseWindow();
            int upAir = _airTicks;
            float upSlowest = _minSpeed;

            yield return StepOffTowardWindows();
            Capture("stairs_top");
            Note($"MEASURE spiral stairs up at a sprint, {rise:0.0} m: {took:0.00} s; {up}");
            Check("climbs a whole flight at a sprint", Feet.y > centre.y + rise - 0.1f && Me.State == MoveState.Grounded, $"feet at {Feet.y:0.00} m, state {Me.State}");
            Check("never leaves the treads or stalls on the way up", upAir == 0 && upSlowest > Me.Profile.WalkSpeed, $"airborne ticks {upAir}, slowest {upSlowest:0.00} m/s (sprint is {Me.Profile.SprintSpeed:0.0})");
        }

        IEnumerator StepOffTowardWindows()
        {
            float until = Time.time + 0.6f;
            while (Time.time < until)
            {
                Aim(0f, 0f);
                yield return null;
            }
            ReleaseAll();
            yield return Wait(0.5f);
        }

        /// <summary>And back down, where a body without ground snapping hops off every tread.</summary>
        IEnumerator StairsDownCheck()
        {
            Vector3 centre = StairCentre();
            float rise = FloorHeight();
            yield return Place(centre + Spoke(-4f) * StairRadius + Vector3.up * (rise + 0.05f), YawToward(Tangent(-4f, -1f)));
            float started = Time.time;
            OpenWindow();
            Press(Key.W, Key.LeftShift);
            while (Feet.y > centre.y + 0.15f && Time.time - started < 12f)
            {
                SteerRoundStairs(centre, -1f);
                yield return null;
            }
            float took = Time.time - started;
            string down = CloseWindow();
            int downAir = _airTicks;
            ReleaseAll();
            yield return Wait(0.4f);
            Note($"MEASURE spiral stairs down at a sprint: {took:0.00} s; {down}");
            Check("stays on the treads on the way down", downAir == 0 && Feet.y < centre.y + 0.2f, $"airborne ticks {downAir}, feet at {Feet.y:0.00} m");
        }

        // ---- Momentum ------------------------------------------------------------------------

        /// <summary>Hold jump out of a sprint: three hops in a row, with nothing lost on the landings.</summary>
        IEnumerator HopChecks()
        {
            yield return Place(new Vector3(-18f, 0.05f, 4f), 90f);
            StartTrace("hops_straight");
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.7f);
            float entry = Me.PlanarSpeed;
            int jumps = _localJumps;
            OpenWindow();
            Press(Key.Space);
            yield return Until(() => _localJumps >= jumps + 4, 6f, "three hops chained by holding jump");
            string hops = CloseWindow();
            float slowest = _minSpeed;
            ReleaseAll();
            StopTrace();
            Note($"MEASURE three chained hops, forward held: entered at {entry:0.00} m/s; {hops}");
            Check("speed is kept through three chained hops", slowest >= entry - 0.1f, $"entered at {entry:0.00} m/s, slowest {slowest:0.00} m/s");
            yield return Wait(0.8f);
        }

        /// <summary>Strafe key held, view turned the same way, hopping: the classic way to curve and gain speed.</summary>
        IEnumerator AirStrafeCheck()
        {
            yield return Place(new Vector3(-6f, 0.05f, 5f), 90f);
            MovementProfile p = Me.Profile;
            StartTrace("air_strafe");
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.7f);
            float entry = Me.PlanarSpeed;
            int jumps = _localJumps;
            Release(Key.W, Key.LeftShift);
            Press(Key.Space, Key.A);
            float perTick = p.AirAcceleration * (float)InstanceFinder.TimeManager.TickDelta;
            bool captured = false;
            float limit = Time.time + 8f;
            while (_localJumps < jumps + 4 && Time.time < limit)
            {
                // Keep the strafe direction just short of square to the velocity, so every tick adds.
                Vector3 velocity = Body.linearVelocity;
                velocity.y = 0f;
                float speed = Mathf.Max(velocity.magnitude, 0.1f);
                float off = Mathf.Acos(Mathf.Clamp((p.AirStrafeSpeed - perTick) / speed, -1f, 1f)) * Mathf.Rad2Deg;
                Aim(YawToward(velocity) - off + 90f, 0f);
                if (!captured && _localJumps == jumps + 2 && Me.State == MoveState.Airborne)
                {
                    captured = true;
                    Capture("air_strafe_second_hop");
                }
                yield return null;
            }
            float exit = Me.PlanarSpeed;
            ReleaseAll();
            StopTrace();
            Note($"MEASURE air strafing through three hops: {entry:0.00} m/s in, {exit:0.00} m/s out");
            Check("air strafing gains speed", exit > entry + 1f, $"{entry:0.00} to {exit:0.00} m/s");
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 4f, null);
        }

        /// <summary>Crouch out of a sprint: a boosted slide that carries, then one that is jumped out of.</summary>
        IEnumerator SlideChecks()
        {
            MovementProfile p = Me.Profile;
            yield return Place(new Vector3(-18f, 0.05f, 8f), 90f);
            StartTrace("slide_flat");
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.7f);
            float entry = Me.PlanarSpeed;
            Vector3 from = Feet;
            float started = Time.time;
            Release(Key.W, Key.LeftShift);
            Press(Key.LeftCtrl);
            yield return Wait(0.1f);
            float boosted = Me.PlanarSpeed;
            bool sliding = Me.AnimState.Sliding;
            yield return Wait(0.4f);
            Capture("slide");
            yield return Until(() => !Me.AnimState.Sliding, 6f, "the slide runs out");
            float took = Time.time - started;
            float distance = Vector3.Distance(from, Feet);
            ReleaseAll();
            StopTrace();
            Note($"MEASURE slide on the flat: in at {entry:0.00} m/s, {boosted:0.00} m/s after the boost, {distance:0.0} m in {took:0.00} s");
            Check("crouching out of a sprint slides with a boost", sliding && boosted > entry + p.SlideBoost * 0.7f, $"sliding {sliding}, {entry:0.00} to {boosted:0.00} m/s");
            yield return Wait(1.6f);

            // Slide, then jump: the jump leaves with the slide's speed.
            yield return Place(new Vector3(-18f, 0.05f, 8f), 90f);
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.7f);
            Release(Key.W, Key.LeftShift);
            Press(Key.LeftCtrl);
            yield return Wait(0.25f);
            float sliding2 = Me.PlanarSpeed;
            yield return Tap(Key.Space);
            float inAir = Me.PlanarSpeed;
            ReleaseAll();
            Check("a jump out of a slide keeps its speed", Me.State == MoveState.Airborne && inAir >= sliding2 - 0.3f, $"sliding at {sliding2:0.00} m/s, {inAir:0.00} m/s in the air, state {Me.State}");
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 4f, null);
            yield return Wait(1f);
        }

        /// <summary>Off a platform, slide down a 20° ramp and jump at the bottom.</summary>
        IEnumerator RampSlideJumpCheck()
        {
            Bounds ramp = CourseBounds("Slide ramp");
            Vector3 top = new(ramp.min.x + 0.6f, ramp.max.y + 0.05f, ramp.center.z);
            yield return Place(top, 90f);
            StartTrace("slide_ramp_jump");
            Press(Key.W, Key.LeftShift);
            yield return Until(() => Me.PlanarSpeed >= Me.Profile.SlideMinSpeed, 2f, "gets up to sliding speed on the platform");
            Release(Key.LeftShift);
            Press(Key.LeftCtrl);
            yield return Wait(0.05f);
            float start = Me.PlanarSpeed;
            OpenWindow();
            bool captured = false;
            float limit = Time.time + 6f;
            while (Feet.y > 0.6f && Time.time < limit)
            {
                if (!captured && Feet.y < ramp.max.y * 0.5f)
                {
                    captured = true;
                    CaptureFromOutside("slide_down_ramp_outside", Feet, new Vector3(2f, 1.5f, -5f));
                }
                yield return null;
            }
            float bottom = Me.PlanarSpeed;
            string onRamp = CloseWindow();
            int air = _airTicks;
            yield return Tap(Key.Space);
            float jumped = Me.PlanarSpeed;
            MoveState state = Me.State;
            Capture("slide_jump_off_ramp");
            ReleaseAll();
            StopTrace();
            Note($"MEASURE slide down the 20 degree ramp: {start:0.00} m/s at the top, {bottom:0.00} m/s at the bottom; {onRamp}");
            Check("a slide builds speed downhill and stays on the ramp", bottom > start + 1f && air == 0, $"{start:0.00} to {bottom:0.00} m/s, airborne ticks {air}");
            Check("the jump at the bottom keeps the slide's speed", state == MoveState.Airborne && jumped >= bottom - 0.5f, $"{jumped:0.00} m/s in the air, state {state}");
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 5f, null);
        }

        /// <summary>What a slide does on the spiral itself. Measured, not judged: a slide steers like a jump, and the spiral is tight.</summary>
        IEnumerator StairSlideMeasure()
        {
            Vector3 centre = StairCentre();
            float rise = FloorHeight();
            yield return Place(centre + Spoke(-4f) * StairRadius + Vector3.up * (rise + 0.05f), YawToward(Tangent(-4f, -1f)));
            Press(Key.W, Key.LeftShift);
            float started = Time.time;
            while (Me.PlanarSpeed < Me.Profile.SlideMinSpeed && Time.time - started < 2f)
            {
                SteerRoundStairs(centre, -1f);
                yield return null;
            }
            Press(Key.LeftCtrl);
            OpenWindow();
            float farthest = 0f;
            while (Feet.y > centre.y + 0.15f && Time.time - started < 8f)
            {
                SteerRoundStairs(centre, -1f);
                Vector3 out_ = Feet - centre;
                out_.y = 0f;
                farthest = Mathf.Max(farthest, out_.magnitude);
                yield return null;
            }
            string slide = CloseWindow();
            ReleaseAll();
            Note($"MEASURE slide down the spiral stairs: {Time.time - started:0.00} s to the bottom, drifted out to {farthest:0.00} m from the column (treads end at 2.60 m); {slide}");
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 5f, null);
        }

        // ---- Speed traces --------------------------------------------------------------------

        System.Text.StringBuilder _trace;
        string _traceName;
        float _traceStart;

        void StartTrace(string name)
        {
            _trace = new System.Text.StringBuilder("seconds,speed,state\n");
            _traceName = name;
            _traceStart = Time.time;
        }

        void StopTrace()
        {
            if (_trace == null)
                return;

            File.WriteAllText(Path.Combine(_outFolder, $"{_role}_trace_{_traceName}.csv"), _trace.ToString());
            _trace = null;
        }

        /// <summary>Unit spoke from the stair column at a compass angle: 0 toward +Z, clockwise from above, the way the flight climbs.</summary>
        static Vector3 Spoke(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        static Vector3 Tangent(float degrees, float way)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad)) * way;
        }

        /// <summary>Looks along the treads, leaning in or out to hold the middle of them.</summary>
        void SteerRoundStairs(Vector3 centre, float way)
        {
            Vector3 out_ = Feet - centre;
            out_.y = 0f;
            float radius = out_.magnitude;
            float degrees = Mathf.Atan2(out_.x, out_.z) * Mathf.Rad2Deg;
            Vector3 along = Tangent(degrees, way) + out_ / radius * (StairRadius - radius);
            Aim(YawToward(along), 0f);
        }

        IEnumerator MenuMidJumpCheck()
        {
            yield return Place(new Vector3(-12f, 0.05f, -3.5f), 90f);
            var menu = FindAnyObjectByType<GameMenu>();
            int jumps = _localJumps, lands = _localLands;

            Press(Key.W, Key.LeftShift, Key.Space);
            yield return Wait(0.5f);
            Press(Key.E);
            yield return Wait(0.15f);

            // The mouse is freed in mid-air with every key still down: exactly what Esc does.
            PlayerInputReader.ScriptedCapture = false;
            Cursor.lockState = CursorLockMode.None;
            yield return Frames(2);
            Note($"menu opened in mid-air: state {Me.State}, speed {Me.PlanarSpeed:0.0} m/s, reader captured {PlayerInputReader.Captured}");
            yield return Until(() => _localLands > lands, 2f, "lands with the menu open");
            yield return Wait(0.8f);
            Check("menu opened mid-jump leaves the player idle", Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.05f && Mathf.Abs(Me.Lean) < 0.01f && _localJumps == jumps + 1,
                $"state {Me.State}, speed {Me.PlanarSpeed:0.00} m/s, lean {Me.Lean:0.00}, jumps +{_localJumps - jumps} with Space still held");

            // Press again while it is open, then close it with nothing held: nothing may fire late.
            Release(Key.Space);
            yield return Frames(2);
            yield return Tap(Key.Space);
            ReleaseAll();
            yield return Frames(2);
            PlayerInputReader.ScriptedCapture = true;
            if (menu != null)
                menu.Resume();
            yield return Wait(0.6f);
            Check("no stuck or late input after the menu closes", _localJumps == jumps + 1 && Me.PlanarSpeed < 0.05f, $"jumps +{_localJumps - jumps}, speed {Me.PlanarSpeed:0.00} m/s");
        }

        IEnumerator KnockMidMantleCheck()
        {
            Bounds ledge = CourseBounds("Ledge 1.6 m");
            yield return Place(new Vector3(ledge.center.x, 0.05f, ledge.max.z + 0.7f), 180f);
            int knocks = _localKnocks;
            yield return Tap(Key.Space);
            yield return Until(() => Me.State == MoveState.Mantling, 1f, "starts the climb to be knocked out of");
            yield return Wait(0.08f);
            float at = Feet.y;
            Me.ApplyKnockback(new Vector3(0f, 5f, 8f), 0.7f);
            yield return Frames(3);
            Check("a blast mid-climb takes over at once", Me.State == MoveState.Knocked, $"state {Me.State}, feet were at {at:0.00} m");
            yield return Wait(0.3f);
            CaptureFromOutside("knocked_mid_climb_outside", Feet, new Vector3(4f, 1.5f, 3f));
            yield return Until(() => Me.State == MoveState.Grounded, 3f, "recovers to the floor after the blast");
            float thrown = Feet.z - (ledge.max.z + 0.7f);
            Press(Key.W);
            yield return Wait(0.6f);
            bool walks = Me.PlanarSpeed > Me.Profile.WalkSpeed * 0.9f;
            ReleaseAll();
            Check("thrown clear and back in control", thrown > 1.5f && walks && _localKnocks == knocks + 1, $"thrown {thrown:0.0} m, walks again {walks}, knock events +{_localKnocks - knocks}");
            yield return Wait(0.4f);
        }

        IEnumerator RespawnCheck()
        {
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.3f);
            InstanceFinder.TimeManager.OnPostTick -= OnPostTick;
            _lobby.Leave();
            yield return Until(() => Me == null, 5f, "leaving mid-sprint removes the player");
            ReleaseAll();
            yield return Wait(1f);
            _lobby.Host();
            yield return Until(() => Me != null, 15f, "hosting again spawns a player");
            if (Me == null)
                yield break;
            yield return Wait(0.5f);
            Check("a second spawn is idle and has the camera", Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f && Vector3.Distance(View.transform.position, Feet) < 2f,
                $"state {Me.State}, speed {Me.PlanarSpeed:0.00} m/s, camera {Vector3.Distance(View.transform.position, Feet):0.00} m from the feet");
        }

        // ---- Host and client -----------------------------------------------------------------

        PlayerMotor FindOther()
        {
            foreach (PlayerMotor motor in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                if (motor != Me)
                    return motor;
            return null;
        }

        IEnumerator Host()
        {
            yield return Until(() => FindOther() != null, 150f, "a client joined");
            PlayerMotor other = FindOther();
            if (other == null)
                yield break;

            HookRemote(other);
            Note("client joined; host showcase starts");
            StartCoroutine(ThrowTheClientWhenItLeans());
            Section("host showing off, client watching");
            StartCoroutine(FilmRemote());

            // Things for the client to see this body do, once it has had time to start watching.
            yield return Wait(3.5f);
            Press(Key.LeftCtrl);
            yield return Wait(1f);
            ReleaseAll();
            yield return Wait(0.5f);
            Press(Key.E);
            yield return Wait(1f);
            ReleaseAll();
            yield return Wait(0.5f);
            yield return Face(0f);
            yield return Place(new Vector3(-12f, 0.05f, -3.5f), 90f);
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.8f);
            Release(Key.W, Key.LeftShift);
            Press(Key.LeftCtrl);
            yield return Wait(0.6f);
            yield return Tap(Key.Space);
            ReleaseAll();
            yield return Wait(1.5f);
            Bounds ledge = CourseBounds("Ledge 1.6 m");
            yield return Place(new Vector3(ledge.center.x, 0.05f, ledge.max.z + 0.7f), 180f);
            yield return Tap(Key.Space);
            yield return Wait(1f);
            // Out of the client's way: players collide, and the client's route runs where the host stopped.
            yield return Place(new Vector3(12f, 0.05f, -12f), 0f);
            Note($"host showcase over; host rests at {Feet}");
            Section("host watching the client's route");

            // Watch the client's body until it leaves.
            Vector3 rest = Vector3.zero;
            float still = 0f;
            float until = Time.time + 220f;
            while (_remote != null && _remote.IsSpawned && Time.time < until)
            {
                if (_remote.AnimState.PlanarSpeed < 0.05f && _remote.AnimState.Grounded)
                {
                    still += Time.deltaTime;
                    if (still > 0.5f && Vector3.Distance(rest, _remote.transform.position) > 0.01f)
                    {
                        rest = _remote.transform.position;
                        Note($"host sees the client at rest at ({rest.x:0.000}, {rest.y:0.000}, {rest.z:0.000})");
                    }
                }
                else
                {
                    still = 0f;
                }
                yield return null;
            }

            Section("end");
            Check("the client's body left with it", _remote == null || !_remote.IsSpawned, "despawned on the host");
            ReportRemote("host saw the client");
            yield return Wait(1f);
        }

        /// <summary>The client's control check: a throw the host decides that the client could not have predicted.</summary>
        IEnumerator ThrowTheClientWhenItLeans()
        {
            yield return Until(() => _remote == null || !_remote.IsSpawned || _remote.AnimState.Lean > 0.9f, 220f, null);
            if (_remote == null || !_remote.IsSpawned)
                yield break;

            Note("host sees the client lean fully; throwing it");
            _remote.ApplyKnockback(new Vector3(0f, 6f, 8f), 0.7f);
        }

        IEnumerator FilmRemote()
        {
            for (int shot = 0; shot < 24 && _remote != null && _remote.IsSpawned; shot++)
            {
                PlayerAnimState seen = _remote.AnimState;
                string what = seen.Mantling ? "climbing" : !seen.Grounded ? "in_the_air" : seen.Crouched ? "crouched" : seen.Sprinting ? "sprinting" : "standing";
                CaptureFromOutside("client_seen_by_host_" + what, _remote.transform.position, new Vector3(3.5f, 1.5f, 4f));
                yield return Wait(0.75f);
            }
        }

        IEnumerator Client()
        {
            yield return Until(() => FindOther() != null, 30f, "the host's body is here");
            PlayerMotor other = FindOther();
            if (other == null)
                yield break;

            HookRemote(other);
            Note("client route starts; ping " + InstanceFinder.TimeManager.RoundTripTime + " ms");

            // Stand and watch the host's showcase, then run a route of our own.
            Section("standing, watching the host");
            yield return Wait(17f);

            Section("sprint, slide and hop");
            yield return Face(270f);
            StartTrace("client_sprint_slide_hop");
            OpenWindow();
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.5f);
            Release(Key.W, Key.LeftShift);
            Press(Key.LeftCtrl);
            yield return Wait(0.5f);
            Press(Key.Space);
            yield return Wait(0.5f);
            ReleaseAll();
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 4f, null);
            StopTrace();
            Note($"MEASURE client sprint, slide and hop: {CloseWindow()}");

            Section("three chained hops");
            yield return HopChecks();
            Section("air strafing");
            yield return AirStrafeCheck();
            Section("sprinting climb onto the desk");
            yield return MantleKeepsSpeedCheck();
            Section("slide on the flat");
            yield return SlideChecks();

            Section("climb the desk and crouch on it");
            Bounds desk = CourseBounds("Desk 0.75 m");
            yield return Place(new Vector3(desk.center.x, 0f, desk.max.z + 0.8f), 180f);
            int mantles = _localMantles;
            yield return Tap(Key.Space);
            yield return Until(() => _localMantles > mantles, 1f, "client climbs the desk");
            yield return Until(() => Me.State == MoveState.Grounded, 2f, null);
            yield return Wait(0.3f);
            Check("client stands on the desk", Mathf.Abs(Feet.y - desk.max.y) < 0.08f, $"feet at {Feet.y:0.00} m");
            Note($"client at rest at ({Feet.x:0.000}, {Feet.y:0.000}, {Feet.z:0.000})");
            Press(Key.LeftCtrl);
            yield return Wait(1f);
            ReleaseAll();
            yield return Wait(0.3f);

            Section("spiral stairs up at a sprint");
            yield return ClientStairsUp();
            Section("slide down the spiral stairs into a jump");
            yield return ClientStairSlideJump();
            // Control: a blast the host decides and the client could not have predicted must show up
            // as a snap, or the zero readings above would mean nothing.
            float routeWorst = _worstCorrection;
            int routeBacksteps = _cameraBacksteps;
            int routeCorrections = Me.CorrectionCount;
            Section("control: a blast the client cannot predict");
            // Leaning is the signal: the client leans nowhere else, and the host throws it when it sees it.
            Press(Key.E);
            yield return Until(() => _localKnocks > 0, 4f, "the host threw the leaning client");
            ReleaseAll();
            yield return Wait(1.5f);
            float control = _sectionWorst;
            Check("the correction measure sees a snap when the host throws the body", control > 0.3f, $"largest snap {control * 100f:0.0} cm");
            Section("end");

            Note($"MEASURE client prediction, whole route: {routeCorrections} corrections over a centimetre, worst {routeWorst * 100f:0.0} cm (before the control blast); view speed at most {_worstCameraRatio:0.00}x the body's, moved against the body on {routeBacksteps} frames; ping {InstanceFinder.TimeManager.RoundTripTime} ms");
            Check("no rubber-banding on the client", routeWorst < 0.05f && routeBacksteps == 0, $"worst correction {routeWorst * 100f:0.0} cm, backward view frames {routeBacksteps}");
            ReportRemote("client saw the host");
            Check("the host's animation state reached the client", _remoteMaxCrouch > 0.99f && _remoteMaxLean > 0.99f && _remoteSprinted && _remoteSlid && _remoteMantled && _remoteAirborne,
                $"slid {_remoteSlid}, crouch {_remoteMaxCrouch:0.00}, lean {_remoteMaxLean:0.00}, sprinted {_remoteSprinted}, climbed {_remoteMantled}, airborne {_remoteAirborne}");

            // Drop the connection in the middle of a sprint.
            Press(Key.W, Key.LeftShift);
            yield return Wait(0.3f);
            InstanceFinder.TimeManager.OnPostTick -= OnPostTick;
            _lobby.Leave();
            yield return Until(() => Me == null, 5f, "leaving mid-sprint removes the local player");
            ReleaseAll();
            yield return Wait(1f);
        }

        IEnumerator ClientStairsUp()
        {
            Vector3 centre = StairCentre();
            float rise = FloorHeight();
            yield return WalkTo(new Vector3(-6f, 0f, -4.5f), true);
            yield return WalkTo(centre + Spoke(-25f) * (StairRadius + 1.5f), true);
            yield return WalkTo(centre + Spoke(-12f) * StairRadius, false, 0.25f);
            OpenWindow();
            Press(Key.W, Key.LeftShift);
            float started = Time.time;
            while (Feet.y < centre.y + rise - 0.1f && Time.time - started < 12f)
            {
                SteerRoundStairs(centre, 1f);
                yield return null;
            }
            Note($"MEASURE client, spiral stairs up at a sprint: {Time.time - started:0.00} s; {CloseWindow()}");
            yield return StepOffTowardWindows();
            yield return Wait(1.2f);
            Check("client reached the upper floor", Feet.y > centre.y + rise - 0.1f, $"feet at {Feet.y:0.00} m");
            Note($"client at rest at ({Feet.x:0.000}, {Feet.y:0.000}, {Feet.z:0.000})");
        }

        /// <summary>From the top of the flight: sprint into a slide down the treads and jump off the bottom.</summary>
        IEnumerator ClientStairSlideJump()
        {
            Vector3 centre = StairCentre();
            float rise = FloorHeight();
            yield return WalkTo(centre + Spoke(-6f) * StairRadius + Vector3.up * rise, false, 0.3f);
            yield return Face(YawToward(Tangent(-6f, -1f)));
            StartTrace("client_stair_slide_jump");
            OpenWindow();
            Press(Key.W, Key.LeftShift);
            float started = Time.time;
            bool crouched = false;
            while (Feet.y > centre.y + 0.5f && Time.time - started < 8f)
            {
                SteerRoundStairs(centre, -1f);
                if (!crouched && Me.PlanarSpeed >= Me.Profile.SlideMinSpeed)
                {
                    crouched = true;
                    Press(Key.LeftCtrl);
                }
                yield return null;
            }
            bool slid = Me.AnimState.Sliding;
            float bottom = Me.PlanarSpeed;
            int jumps = _localJumps;
            yield return Tap(Key.Space);
            float jumped = Me.PlanarSpeed;
            ReleaseAll();
            yield return Until(() => Me.State == MoveState.Grounded && Me.PlanarSpeed < 0.1f, 4f, null);
            StopTrace();
            Note($"MEASURE client slide down the spiral into a jump: {Time.time - started:0.00} s, sliding at the bottom {slid}, {bottom:0.00} m/s at the bottom, {jumped:0.00} m/s leaving the jump; {CloseWindow()}");
            Check("client slides down the stairs and jumps out of it", slid && _localJumps > jumps, $"sliding {slid}, jumps +{_localJumps - jumps}");
        }

        void ReportRemote(string who)
        {
            Note($"{who}: crouch up to {_remoteMaxCrouch:0.00}, lean up to {_remoteMaxLean:0.00}, speed up to {_remoteMaxSpeed:0.0} m/s, sprinting {_remoteSprinted}, sliding {_remoteSlid}, climbing {_remoteMantled}, airborne {_remoteAirborne}");
        }
    }
}
#endif

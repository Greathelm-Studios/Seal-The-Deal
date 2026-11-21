using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Plays Lane_Integration.unity headlessly and films the UI kit: the title and settings at 1920x1080, 1280x720
    /// and 2560x1080, every control in its five states, a frame sequence of hover -> press -> screen push, a clip
    /// sequence, the quit confirm answering Esc, the settings surviving a relaunch (two runs), and the
    /// menu-open = mouse-free = player-idle rule with the pad's A held (A is also Jump). Every UiSound request is
    /// logged. Needs graphics: the Editor binary with -batchmode but WITHOUT -quit or -nographics (it exits itself):
    ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod CloseTheDeal.Editor.UI.UiKitCapture.RunFromShell
    ///         -uiCaptureOut &lt;folder&gt; -uiCaptureMode capture|persist-write|persist-read -local
    /// Nothing is saved: the scene changes only in Play Mode. The persist runs restore the player's own settings.
    /// </summary>
    public static partial class UiKitCapture
    {
        const string ActiveKey = "UiKitCapture.Active";
        const string OutKey = "UiKitCapture.Out";
        const string ModeKey = "UiKitCapture.Mode";
        const float Step = 1f / 30f;

        static readonly Stack<IEnumerator> Steps = new();
        static object _waiting;
        static float _waitUntil;
        static bool _started;
        static string _out;
        static string _mode;
        static int _failures;
        static readonly StringBuilder Log = new();
        static readonly List<string> Sounds = new();

        static Camera _cam;
        static RenderTexture _rt;
        static Canvas _canvas;
        static CanvasScaler _scaler;
        static Keyboard _keyboard;
        static UiRoot _root;

        public static void RunFromShell()
        {
            string mode = Arg("-uiCaptureMode") ?? "capture";
            string folder = Arg("-uiCaptureOut") ?? "Temp/UiCapture";
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(UiKitSetup.IntegrationScene, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(OutKey, Path.GetFullPath(folder));
            SessionState.SetString(ModeKey, mode);
            EditorApplication.EnterPlaymode();
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update += Pump;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            UiMotion.Paused = false;
            _started = false;
            _waiting = null;
            Steps.Clear();
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Pump;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        // ---- Stepper -------------------------------------------------------------------------------

        static void Pump()
        {
            if (!EditorApplication.isPlaying)
                return;
            if (!_started)
                Begin();
            if (!Ready())
                return;
            _waiting = null;

            try
            {
                if (!Advance())
                    Finish();
            }
            catch (Exception e)
            {
                Fail("the capture script threw: " + e);
                Finish();
            }
        }

        static void Begin()
        {
            _started = true;
            _out = SessionState.GetString(OutKey, "Temp/UiCapture");
            _mode = SessionState.GetString(ModeKey, "capture");
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("UI capture keyboard");
            // These runs step the kit's motion by hand, which lane 8's transitions (real frames) can't follow; the
            // transitions run (wide-transitions) switches them back on for its own clip.
            UiScreenStack.UseTransitions = false;
            UiSound.Played += e => { Sounds.Add($"{Time.frameCount} {e}"); Debug.Log("[UiSound] " + e); };
            Steps.Push(_mode.StartsWith("wide") ? WideScript(_mode) : _mode == "persist-write" ? PersistWrite() : _mode == "persist-read" ? PersistRead() : _mode == "menu-rule" ? MenuRuleOnly() : CaptureScript());
        }

        static bool Ready()
        {
            switch (_waiting)
            {
                case null: return true;
                case float: return Time.realtimeSinceStartup >= _waitUntil;
                case Func<bool> until: return until() || Time.realtimeSinceStartup >= _waitUntil;
                default: return true;
            }
        }

        static bool Advance()
        {
            while (Steps.Count > 0)
            {
                IEnumerator top = Steps.Peek();
                if (!top.MoveNext())
                {
                    Steps.Pop();
                    continue;
                }

                object current = top.Current;
                if (current is IEnumerator nested)
                {
                    Steps.Push(nested);
                    continue;
                }

                _waiting = current;
                if (current is float seconds)
                    _waitUntil = Time.realtimeSinceStartup + seconds;
                else if (current is Func<bool>)
                    _waitUntil = Time.realtimeSinceStartup + 20f;
                return true;
            }

            return false;
        }

        static void Finish()
        {
            UiMotion.Paused = false;
            Note(_failures == 0 ? "RESULT: all checks passed" : $"RESULT: {_failures} check(s) failed");
            File.WriteAllText(Path.Combine(_out, $"ui_{_mode}_log.txt"), Log.ToString());
            File.WriteAllText(Path.Combine(_out, $"ui_{_mode}_sounds.txt"), string.Join("\n", Sounds));
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Pump;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        static void Note(string line)
        {
            Log.AppendLine(line);
            Debug.Log("[UiCapture] " + line);
        }

        static void Check(string what, bool ok, string detail = "")
        {
            if (!ok)
                _failures++;
            Note((ok ? "PASS  " : "FAIL  ") + what + (string.IsNullOrEmpty(detail) ? string.Empty : "  (" + detail + ")"));
        }

        static void Fail(string what) => Check(what, false);

        static Func<bool> Until(Func<bool> condition) => condition;

        // ---- The main capture run ----------------------------------------------------------------------

        static IEnumerator CaptureScript()
        {
            yield return Until(() => Object.FindAnyObjectByType<UiRoot>() != null);
            _root = Object.FindAnyObjectByType<UiRoot>();
            SteamLobby lobby = Object.FindAnyObjectByType<SteamLobby>();
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            Check("the kit is in the scene with a game menu and a lobby", _root != null && lobby != null && menu != null);
            if (_root == null || lobby == null || menu == null)
                yield break;

            yield return null;
            var title = _root.Title;
            UiButton host = Find<UiButton>(title, "HostGame");
            Note($"first frame: lobby ready {lobby.Ready}, Host interactable {host.interactable}, status '{Status(title)}'");
            if (!lobby.Ready)
                Check("edge: before the network is ready Host is greyed out and the status says why",
                    !host.interactable && !string.IsNullOrEmpty(Status(title)), Status(title));
            else
                Note("the network was already ready on the first frame, so the not-ready state is checked by greying Host out below");

            SetUpCameraOnce();
            yield return Until(() => lobby.Ready);
            Check("the network became ready and Host can be used", lobby.Ready && host.interactable);
            Check("the title is the open screen and the old card is hidden", _root.Stack.Top == title && !menu.transform.Find("Menu").gameObject.activeSelf);
            Check("menu open = mouse free at the title", Cursor.lockState != CursorLockMode.Locked && !PlayerInputReader.Captured);

            yield return RefusedHost(title, host);
            UiMotion.Paused = true;
            Settle(1.5f);
            Shot("title_idle_1080", 1920, 1080);
            Shot("title_idle_720", 1280, 720);
            Shot("title_idle_2560x1080", 2560, 1080);
            CheckInside(title.transform, 1280, 720, "title at 1280x720");
            CheckInside(title.transform, 2560, 1080, "title at 2560x1080");

            yield return StateSheets(title);
            yield return HoverPressPush(title);
            yield return SettingsShots();
            yield return QuitConfirm(title);
            yield return Clip(title);
            UiMotion.Paused = false;
            yield return SoundProof();
            yield return RebindClash();
            yield return MenuRule(lobby, menu);
        }

        /// <summary>A press on a greyed-out Host (the network not ready) refuses: Error sound, amber mark, the reason in words.</summary>
        static IEnumerator RefusedHost(TitleScreen title, UiButton host)
        {
            int sound0 = Sounds.Count;
            host.interactable = false;
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(host.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(host.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(host.gameObject, ped, ExecuteEvents.pointerClickHandler);
            bool errorSound = Sounds.Count > sound0 && Sounds[^1].EndsWith(" Error");
            string status = Status(title);
            UiMotion.Paused = true;
            Settle(0.05f);
            SetUpCameraOnce();
            Shot("title_host_refused_1080", 1920, 1080);
            UiMotion.Paused = false;
            Check("edge: pressing a greyed-out Host refuses with the Error sound and says why, and hosts nothing",
                errorSound && status == "Host game opens once the network is ready." && !Object.FindAnyObjectByType<SteamLobby>().IsActive,
                $"status '{status}', sounds {string.Join(",", Sounds.GetRange(sound0, Sounds.Count - sound0))}");
            host.interactable = true;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(host.gameObject);
            yield return 0.5f;
        }

        static void SetUpCameraOnce()
        {
            if (_cam == null)
                SetUpCamera();
        }

        /// <summary>Only the menu rule check (mode menu-rule), without the captures.</summary>
        static IEnumerator MenuRuleOnly()
        {
            yield return Until(() => Object.FindAnyObjectByType<UiRoot>() != null);
            _root = Object.FindAnyObjectByType<UiRoot>();
            SteamLobby lobby = Object.FindAnyObjectByType<SteamLobby>();
            yield return Until(() => lobby.Ready);
            yield return MenuRule(lobby, Object.FindAnyObjectByType<GameMenu>());
        }

        // ---- The player's Music volume, borrowed by a check ------------------------------------------------

        static float _heldMusic = -1f;

        /// <summary>
        /// Notes the player's Music volume before a check changes it. It comes back with ReleaseMusic, and also if the
        /// run is stopped first (Play Mode ends, or the Editor quits), so a killed capture never leaves it changed.
        /// </summary>
        static void HoldMusic()
        {
            if (_heldMusic >= 0f)
                return;
            _heldMusic = UiAudio.Get(AudioBus.Music);
            EditorApplication.playModeStateChanged += ReleaseOnExit;
            Application.quitting += ReleaseMusic;
        }

        static void ReleaseOnExit(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                ReleaseMusic();
        }

        static void ReleaseMusic()
        {
            EditorApplication.playModeStateChanged -= ReleaseOnExit;
            Application.quitting -= ReleaseMusic;
            if (_heldMusic < 0f)
                return;
            UiAudio.SetVolume(AudioBus.Music, _heldMusic);
            _heldMusic = -1f;
        }

        static string Status(TitleScreen title) => Find<UiStatusLine>(title, "StatusLine")?.Text;

        static T Find<T>(Component under, string name) where T : Component
        {
            foreach (T c in under.GetComponentsInChildren<T>(true))
            {
                if (c.name == name)
                    return c;
            }

            return null;
        }

        static void Settle(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
                UiMotion.Tick(Step);
        }

        // ---- Rendering -----------------------------------------------------------------------------------

        static void SetUpCamera()
        {
            _canvas = _root.GetComponent<Canvas>();
            _scaler = _root.GetComponent<CanvasScaler>();
            var go = new GameObject("UiCaptureCamera");
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.62f, 0.65f, 0.68f);
            _cam.cullingMask = 1 << 5;
            _cam.orthographic = true;
            _cam.enabled = false;
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _cam;
            _canvas.planeDistance = 1f;
        }

        static void Resize(int w, int h)
        {
            if (_rt != null && _rt.width == w && _rt.height == h)
                return;
            if (_rt != null)
                _rt.Release();
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = _rt;
            _scaler.enabled = false;
            _scaler.enabled = true;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_root.transform);
            UiMotion.Tick(0f);
            Canvas.ForceUpdateCanvases();
        }

        static Texture2D Grab(int w, int h)
        {
            Resize(w, h);
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        static void Shot(string name, int w, int h)
        {
            Texture2D tex = Grab(w, h);
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Note("wrote " + name + ".png");
        }

        /// <summary>Every visible graphic of a screen must sit inside the canvas at this size (nothing leaves the screen).</summary>
        static void CheckInside(Transform screen, int w, int h, string label)
        {
            Resize(w, h);
            var canvasRect = (RectTransform)_root.transform;
            var corners = new Vector3[4];
            Rect bounds = canvasRect.rect;
            int outside = 0;
            string first = null;
            foreach (TMP_Text text in screen.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(text.text))
                    continue;
                text.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i += 2)
                {
                    Vector3 local = canvasRect.InverseTransformPoint(corners[i]);
                    if (!bounds.Contains(new Vector2(local.x, local.y)))
                    {
                        outside++;
                        first ??= text.name;
                    }
                }
            }

            Check($"{label}: every line of text is on screen", outside == 0, first != null ? "first outside: " + first : $"canvas {bounds.width:0} x {bounds.height:0}");
        }

        // ---- States ---------------------------------------------------------------------------------------

        static readonly string[] StateNames = { "default", "hover", "focus", "pressed", "disabled" };

        /// <summary>Each control in its five states, cropped and laid out as a sheet: one row per control.</summary>
        static IEnumerator StateSheets(TitleScreen title)
        {
            var rows = new List<(string, Selectable)>
            {
                ("Host game (primary)", Find<UiButton>(title, "HostGame")),
                ("Settings (secondary)", Find<UiButton>(title, "Settings")),
                ("Quit to desktop (quiet)", Find<UiButton>(title, "QuitToDesktop")),
            };
            yield return Sheet("states_title", rows, 1920, 1080);

            _root.Stack.Push(Settings());
            Settle(1f);
            SettingsScreen settings = Settings();
            Tabs().Select(3, false);
            Settle(0.5f);
            var settingRows = new List<(string, Selectable)>
            {
                ("Tab", Find<UiButton>(settings, "AccessibilityTab")),
                ("Toggle", Find<UiToggle>(settings, "ReduceMotion")),
                ("Stepper", Find<UiStepper>(settings, "CrosshairStyle")),
                ("Swatches", Find<UiSwatches>(settings, "CrosshairColour")),
                ("Slider", Find<UiSlider>(settings, "CrosshairSize")),
                ("Reset to defaults", Find<UiButton>(settings, "ResetToDefaults")),
            };
            yield return Sheet("states_settings", settingRows, 1920, 1080);
            _root.Stack.Pop();
            Settle(1f);
        }

        static SettingsScreen Settings() => _root.GetComponentInChildren<SettingsScreen>(true);

        static UiTabPages Tabs() => Settings().GetComponentInChildren<UiTabPages>(true);

        static IEnumerator Sheet(string name, List<(string label, Selectable control)> rows, int w, int h)
        {
            const int cellW = 520, cellH = 120;
            var sheet = new Texture2D(cellW * StateNames.Length, cellH * rows.Count, TextureFormat.RGB24, false);
            for (int r = 0; r < rows.Count; r++)
            {
                Selectable control = rows[r].control;
                if (control == null)
                {
                    Fail("state sheet: missing control " + rows[r].label);
                    continue;
                }

                bool wasInteractable = control.interactable;
                for (int s = 0; s < StateNames.Length; s++)
                {
                    ShowState(control, s);
                    Settle(0.4f);
                    Texture2D frame = Grab(w, h);
                    Blit(frame, sheet, ControlRect(control, w, h, cellW, cellH), s * cellW, (rows.Count - 1 - r) * cellH, cellW, cellH);
                    Object.DestroyImmediate(frame);
                    control.interactable = wasInteractable;
                    UiInput.ForceKeysOrPad(false);
                }

                ShowState(control, 0);
                Settle(0.4f);
                Note($"{name}: {rows[r].label} drawn in {string.Join(", ", StateNames)}");
                yield return null;
            }

            File.WriteAllBytes(Path.Combine(_out, name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Note($"wrote {name}.png (columns: {string.Join(", ", StateNames)})");
        }

        static void ShowState(Selectable control, int state)
        {
            EventSystem.current.SetSelectedGameObject(null);
            control.interactable = state != 4;
            bool hover = state == 1;
            bool pressed = state == 3;
            bool focus = state == 2;
            if (focus)
            {
                UiInput.ForceKeysOrPad(true);
                EventSystem.current.SetSelectedGameObject(control.gameObject);
            }

            if (control is UiControl c)
                c.ShowState(hover, pressed, focus);
            else if (control is UiSlider s)
                s.ShowState(hover, pressed, focus);
        }

        static RectInt ControlRect(Selectable control, int w, int h, int cellW, int cellH)
        {
            var corners = new Vector3[4];
            ((RectTransform)control.transform).GetWorldCorners(corners);
            Vector3 min = _cam.WorldToScreenPoint(corners[0]);
            Vector3 max = _cam.WorldToScreenPoint(corners[2]);
            int cx = Mathf.RoundToInt((min.x + max.x) * 0.5f);
            int cy = Mathf.RoundToInt((min.y + max.y) * 0.5f);
            int x = Mathf.Clamp(cx - cellW / 2, 0, w - cellW);
            int y = Mathf.Clamp(cy - cellH / 2, 0, h - cellH);
            return new RectInt(x, y, cellW, cellH);
        }

        static void Blit(Texture2D from, Texture2D to, RectInt source, int dx, int dy, int cw, int ch)
        {
            Color[] block = from.GetPixels(source.x, source.y, cw, ch);
            to.SetPixels(dx, dy, cw, ch, block);
        }

        // ---- Hover -> press -> push -------------------------------------------------------------------------

        /// <summary>Frames every 1/30 s while the pointer hovers Settings, presses it, lets go and the settings screen pushes in.</summary>
        static IEnumerator HoverPressPush(TitleScreen title)
        {
            UiButton settings = Find<UiButton>(title, "Settings");
            string folder = Path.Combine(_out, "strip");
            Directory.CreateDirectory(folder);
            int sound0 = Sounds.Count;
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            TMP_Text stamp = TimeStamp();
            int frame = 0;
            for (float t = 0f; t <= 0.8f + 0.001f; t += Step, frame++)
            {
                int ms = Mathf.RoundToInt(t * 1000f);
                stamp.text = $"{ms} ms" + (frame == 1 ? "  hover" : frame == 4 ? "  press" : frame == 6 ? "  release: push" : string.Empty);
                if (frame == 1)
                    ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerEnterHandler);
                if (frame == 4)
                    ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerDownHandler);
                if (frame == 6)
                {
                    ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerClickHandler);
                }

                UiMotion.Tick(Step);
                Texture2D tex = Grab(1280, 720);
                File.WriteAllBytes(Path.Combine(folder, $"f{frame:00}_{ms:000}ms.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            Object.DestroyImmediate(stamp.transform.parent.gameObject);
            Check("hover -> press -> click opened Settings", _root.Stack.Top is SettingsScreen);
            Note("strip sounds: " + string.Join(", ", Sounds.GetRange(sound0, Sounds.Count - sound0)));
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerExitHandler);
            yield return null;

            _root.Stack.Pop();
            Settle(1f);
            Check("Back returned to the title", _root.Stack.Top == title);
        }

        /// <summary>A capture-only time stamp in the top-left corner of the frame (never saved).</summary>
        static TMP_Text TimeStamp()
        {
            var plate = new GameObject("CaptureStamp", typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)plate.transform;
            rect.SetParent(_root.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(420f, 56f);
            plate.AddComponent<Image>().color = new Color(0.067f, 0.106f, 0.22f, 0.92f);
            var textGo = new GameObject("Text", typeof(RectTransform)) { layer = 5 };
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 30f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        // ---- Settings ---------------------------------------------------------------------------------------

        static readonly string[] TabNames = { "controls", "video", "audio", "accessibility" };

        /// <summary>Every settings tab at the three sizes, the second row focused so the marks show.</summary>
        static IEnumerator SettingsShots()
        {
            _root.Stack.Push(Settings());
            Settle(1f);
            UiInput.ForceKeysOrPad(true);
            for (int i = 0; i < TabNames.Length; i++)
            {
                Tabs().Select(i, false);
                Settle(0.5f);
                UiSettingsPage page = Tabs().CurrentPage;
                Selectable row = page.FirstRow;
                if (row != null)
                {
                    Navigation nav = row.navigation;
                    Selectable second = nav.selectOnDown != null && nav.selectOnDown.transform.IsChildOf(page.transform) ? nav.selectOnDown : row;
                    EventSystem.current.SetSelectedGameObject(second.gameObject);
                }

                Settle(0.6f);
                Shot($"settings_{TabNames[i]}_1080", 1920, 1080);
                Shot($"settings_{TabNames[i]}_720", 1280, 720);
                Shot($"settings_{TabNames[i]}_2560x1080", 2560, 1080);
                CheckInside(Settings().transform, 1280, 720, $"settings {TabNames[i]} at 1280x720");
                CheckInside(Settings().transform, 2560, 1080, $"settings {TabNames[i]} at 2560x1080");
            }

            UiInput.ForceKeysOrPad(false);
            Tabs().Select(0, false);
            _root.Stack.Pop();
            Settle(1f);
            yield return null;
        }

        /// <summary>
        /// Proves the hub reaches the sound lane's player: requests a few events and lists every AudioSource that
        /// starts with its clip and the mixer group it routes to.
        /// </summary>
        static IEnumerator SoundProof() => SoundProof(UiSoundEvent.Confirm, UiSoundEvent.Back, UiSoundEvent.TabChange, UiSoundEvent.Notification, UiSoundEvent.Error);

        static IEnumerator SoundProof(params UiSoundEvent[] events)
        {
            Note("sound player registered: " + (UiSound.Player != null ? UiSound.Player.GetType().Name : "none"));
            UiSoundPlayer.IgnoreFocus = true; // A batch run has no focused window; the player stays silent unfocused by design.
            int started0 = UiSoundPlayer.Started;
            var heard = new List<string>();
            foreach (UiSoundEvent e in events)
            {
                UiSound.Play(e);
                yield return null;
                foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>())
                {
                    if (!source.isPlaying || source.clip == null)
                        continue;
                    string line = $"{e}: clip {source.clip.name} -> mixer group {(source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "none")}";
                    if (!heard.Contains(line))
                        heard.Add(line);
                }

                yield return 0.35f;
            }

            foreach (string line in heard)
                Note("heard " + line);
            Note($"sounds started by the player: {UiSoundPlayer.Started - started0}, dropped by its gap and voice rules: {UiSoundPlayer.Dropped}");
            UiSoundPlayer.IgnoreFocus = false;
            Check("the hub plays the sound lane's clips through a mixer group", heard.Count > 0 && heard.TrueForAll(l => !l.EndsWith("none")), $"{heard.Count} sources seen");
        }

        /// <summary>
        /// Settings > Controls with custom keys live (the flag the input reader will set is forced here, since that hook is
        /// on the movement lane's branch): Jump asks for a key, G is pressed, and G already belongs to Drop / Throw, so
        /// the clash dialog asks; Esc keeps things as they were. The player's own keys are put back afterwards.
        /// </summary>
        static IEnumerator RebindClash()
        {
            string originals = InputPrefs.BindingOverridesJson;
            bool hooked = InputPrefs.BindingsHooked;
            InputPrefs.BindingsHooked = true;
            InputPrefs.BindingOverridesJson = string.Empty;
            _root.Stack.Push(Settings());
            Tabs().Select(0, false);
            yield return 0.6f;
            var controls = (ControlsPage)Tabs().CurrentPage;
            controls.ShowSaved();
            UiButton jump = Find<UiButton>(controls, "KeyButton");
            EventSystem.current.SetSelectedGameObject(jump.gameObject);
            jump.Activate();
            yield return 0.3f;
            Check("pressing a key cell listens (Esc and arrows go to the rebind)", controls.Listening && UiInput.Capturing);
            UiMotion.Paused = true;
            Settle(0.3f);
            UiInput.ForceKeysOrPad(true);
            Shot("settings_controls_listening_1080", 1920, 1080);
            UiMotion.Paused = false;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.G));
            yield return 0.25f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.4f;
            Check("G for Jump: G is Drop / Throw's, so a dialog asks", _root.Stack.Top is UiConfirmDialog, _root.Stack.Top != null ? _root.Stack.Top.name : "none");
            UiMotion.Paused = true;
            Settle(0.6f);
            Shot("settings_controls_conflict_1080", 1920, 1080);
            UiMotion.Paused = false;
            yield return Press(Key.Escape);
            Check("Esc on the clash keeps both keys as they were", _root.Stack.Top is SettingsScreen && string.IsNullOrEmpty(InputPrefs.BindingOverridesJson));

            // Again, choosing Assign: Jump takes G and Drop / Throw is left with no key.
            jump.Activate();
            yield return 0.3f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.G));
            yield return 0.25f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.4f;
            Find<UiButton>((UiConfirmDialog)_root.Stack.Top, "Confirm").Activate();
            yield return 0.4f;
            string json = InputPrefs.BindingOverridesJson;
            Note("after Assign: " + json);
            Check("Assign on the clash moves G to Jump and clears it from Drop / Throw (saved)",
                json.Contains("Player/Jump") && json.Contains("<Keyboard>/g") && json.Contains("Player/Throw"));
            UiMotion.Paused = true;
            Settle(0.6f);
            Shot("settings_controls_assigned_1080", 1920, 1080);
            UiMotion.Paused = false;

            // Close and reopen: the page must still answer (saves reach the status line, the tab ticks).
            _root.Stack.Pop();
            yield return 0.6f;
            _root.Stack.Push(Settings());
            yield return 0.6f;
            Tabs().Select(2, true);
            yield return 0.4f;
            UiSlider music = Find<UiSlider>(Settings(), "Volume1");
            HoldMusic();
            EventSystem.current.SetSelectedGameObject(music.gameObject);
            Move(MoveDirection.Left);
            yield return null;
            UiStatusLine settingsStatus = Find<UiStatusLine>(Settings(), "StatusLine");
            Check("after closing and reopening Settings, a change still reports 'Saved' and Reset turns on",
                settingsStatus.Text == "Saved on this PC." && Find<UiButton>(Settings(), "ResetToDefaults").interactable, settingsStatus.Text);
            ReleaseMusic();

            _root.Stack.Pop();
            yield return 0.6f;
            InputPrefs.BindingOverridesJson = originals;
            InputPrefs.BindingsHooked = hooked;
        }

        // ---- Quit confirm: Esc = Stay ---------------------------------------------------------------------------

        static IEnumerator QuitConfirm(TitleScreen title)
        {
            UiMotion.Paused = false;
            yield return Press(Key.Escape);
            UiScreen top = _root.Stack.Top;
            GameObject focused = EventSystem.current.currentSelectedGameObject;
            Check("Esc at the title asks 'Quit to desktop?' with Stay focused", top is UiConfirmDialog && focused != null && focused.name == "Stay",
                $"top {(top != null ? top.name : "none")}, focus {(focused != null ? focused.name : "none")}");
            UiMotion.Paused = true;
            Settle(1f);
            UiInput.ForceKeysOrPad(true);
            Settle(0.3f);
            Shot("title_quit_confirm_1080", 1920, 1080);
            UiInput.ForceKeysOrPad(false);
            UiMotion.Paused = false;
            yield return Press(Key.Escape);
            Check("Esc again is Stay: the dialog closes and the game keeps running", _root.Stack.Top == title && EditorApplication.isPlaying);
            UiMotion.Paused = true;
            Settle(1f);
        }

        static IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return 0.12f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.35f;
        }

        // ---- The clip ------------------------------------------------------------------------------------

        /// <summary>A 30 fps sequence at 1280x720: idle, keyboard focus moving, hover, press, push to settings, change a value, back.</summary>
        static IEnumerator Clip(TitleScreen title)
        {
            string folder = Path.Combine(_out, "clip");
            Directory.CreateDirectory(folder);
            int n = 0;
            void Film(float seconds)
            {
                for (float t = 0f; t < seconds; t += Step)
                {
                    UiMotion.Tick(Step);
                    Texture2D tex = Grab(1280, 720);
                    File.WriteAllBytes(Path.Combine(folder, $"c{n++:0000}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
            }

            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            UiButton host = Find<UiButton>(title, "HostGame");
            UiButton settings = Find<UiButton>(title, "Settings");
            EventSystem.current.SetSelectedGameObject(host.gameObject);
            Film(1.5f);
            UiInput.ForceKeysOrPad(true);
            Move(MoveDirection.Down);
            Film(0.5f);
            Move(MoveDirection.Down);
            Film(0.5f);
            Move(MoveDirection.Down);
            Film(0.4f);
            Move(MoveDirection.Down); // the end of the list: the marks bump
            Film(0.6f);
            Move(MoveDirection.Up);
            Film(0.5f);
            UiInput.ForceKeysOrPad(false);
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerEnterHandler);
            Film(0.4f);
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerDownHandler);
            Film(0.12f);
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(settings.gameObject, ped, ExecuteEvents.pointerExitHandler);
            Film(1f);
            UiInput.ForceKeysOrPad(true);
            Tabs().Select(1, true);
            Film(0.6f);
            Tabs().Select(2, true);
            Film(0.6f);
            Tabs().Select(3, true);
            Film(0.6f);
            EventSystem.current.SetSelectedGameObject(Tabs().CurrentTab.gameObject);
            Move(MoveDirection.Down);
            Film(0.4f);
            Move(MoveDirection.Down);
            Film(0.4f);
            Move(MoveDirection.Right);
            Film(0.5f);
            Move(MoveDirection.Left);
            Film(0.5f);
            _root.Stack.Cancel();
            Film(1.2f);
            UiInput.ForceKeysOrPad(false);
            Note($"clip: {n} frames at 30 fps in clip/");
            yield return null;
        }

        static void Move(MoveDirection direction)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
                return;
            var axis = new AxisEventData(EventSystem.current) { moveDir = direction };
            ExecuteEvents.Execute(selected, axis, ExecuteEvents.moveHandler);
        }

        // ---- Menu open = mouse free = player idle, and the Enter guard ----------------------------------------

        /// <summary>
        /// Batch mode has no window, so the OS cursor never reads locked and the game menu's "mouse freed elsewhere"
        /// rule reopens the menu the frame after it closes. So this checks what the rule is made of: the kit closes
        /// when a game starts; with the menu open every input reads idle even with keys held; and a close asked for
        /// with the confirm button down waits for its release. Locking itself needs a human with a window.
        /// </summary>
        static IEnumerator MenuRule(SteamLobby lobby, GameMenu menu)
        {
            // Our own port, so this run can never join someone's open editor game on 7770 instead of hosting.
            ushort port = ushort.TryParse(Arg("-lanePort"), out ushort p) ? p : (ushort)17785;
            var tugboat = Object.FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat != null)
                tugboat.SetPort(port);
            Note($"local test port {port} (Tugboat {(tugboat != null ? "found" : "missing")}), local mode {lobby.LocalMode}");

            Find<UiButton>(_root.Title, "HostGame").Activate();
            yield return Until(() => PlayerMotor.Local != null);
            yield return 0.5f;
            PlayerMotor me = PlayerMotor.Local;
            Check("this run is the host (not a client of someone else's game)", lobby.IsHosting);
            if (me == null)
            {
                Fail("hosting locally spawned a player (needed for the menu rule check)");
                yield break;
            }

            float zoomWait = 0f;
            while (_root.Stack.Count > 0 && zoomWait < 3f)
            {
                zoomWait += Time.unscaledDeltaTime;
                yield return null;
            }

            float zoom = CloseTheDeal.UI.Title.UiTransition.Duration(CloseTheDeal.UI.Title.UiTransitionKind.FrameZoom);
            Check("a game starting closes the kit's screens once the window zoom covers the view", _root.Stack.Count == 0 && zoomWait <= zoom + 0.25f,
                $"after {zoomWait:0.00} s more (zoom {zoom:0.00} s)");
            Note($"batch mode: cursor reads {Cursor.lockState} after the menu asked for Locked, so the menu reopens (no window to lock)");

            var reader = me.GetComponent<PlayerInputReader>();
            menu.Open();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.LeftShift, Key.Space));
            yield return 0.4f;
            Check("menu open: W + Shift + Space held read as idle (no move, no sprint, no jump)",
                menu.IsOpen && !PlayerInputReader.Captured && reader.Move == Vector2.zero && !reader.Sprint && !reader.JumpHeld,
                $"move {reader.Move}, sprint {reader.Sprint}");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.2f;

            Gamepad pad = InputSystem.AddDevice<Gamepad>("UI capture pad");
            int closedFrame = -1, releaseFrame = -1;
            bool armed = false;
            void OnState()
            {
                if (armed && !menu.IsOpen && closedFrame < 0)
                    closedFrame = Time.frameCount;
            }

            menu.StateChanged += OnState;
            GameObject focused = EventSystem.current.currentSelectedGameObject;
            Note("focus when the in-game card opened: " + (focused != null ? focused.name : "none"));
            // The pad's A (Submit, also Jump) goes down, and Back to game is confirmed on that press.
            InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.South));
            yield return 0.1f;
            var module = EventSystem.current.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
            InputAction submit = module != null && module.submit != null ? module.submit.action : null;
            Note($"pad south down {pad.buttonSouth.isPressed}; UI Submit action {(submit != null ? submit.name : "none")}, enabled {submit?.enabled}, bound to {submit?.controls.Count} controls, pressed {submit?.IsPressed()}");
            Check("A reads as held by the UI", UiInput.SubmitHeld);
            armed = true;
            menu.Resume();
            yield return 0.3f;
            Check("A held: Back to game waits, the menu stays open and input stays idle", menu.IsOpen && closedFrame < 0 && !PlayerInputReader.Captured,
                $"open {menu.IsOpen}");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            releaseFrame = Time.frameCount;
            yield return 0.3f;
            menu.StateChanged -= OnState;
            Check("letting go of A closes the menu, not before", closedFrame >= releaseFrame - 1 && closedFrame > 0,
                $"released frame {releaseFrame}, closed frame {closedFrame}");
            yield return CursorHolds(menu, pad);
            InputSystem.RemoveDevice(pad);

            lobby.Leave();
            yield return 1.5f;
            Check("leaving the game brings the title back", _root.Stack.Top == _root.Title && menu.IsOpen);
            Check("leaving the game drops every cursor hold", !GameMenu.CursorHeld);
        }

        /// <summary>
        /// GameMenu.HoldCursor / ReleaseCursor (lane 6's fitting room and host list): a hold keeps the mouse free with the
        /// menu closed, Esc still opens the menu, a release with A still down waits for A, and a destroyed holder stops
        /// counting. Batch mode can't lock the cursor, so this reads what the menu asked for (CursorFree).
        /// </summary>
        static IEnumerator CursorHolds(GameMenu menu, Gamepad pad)
        {
            var owner = new GameObject("CaptureHolder");
            menu.Open();
            yield return 0.1f;
            GameMenu.HoldCursor(owner);
            GameMenu.HoldCursor(owner); // twice from one owner counts once
            menu.Resume();
            yield return 0.4f;
            Check("hold: the menu closes and stays closed with the mouse free and the player idle",
                !menu.IsOpen && menu.CursorFree && GameMenu.CursorHeld && !PlayerInputReader.Captured,
                $"open {menu.IsOpen}, free {menu.CursorFree}");

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            yield return 0.1f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.2f;
            Check("hold: Esc still opens the menu", menu.IsOpen);
            menu.Resume();
            yield return 0.3f;
            Check("hold: closing the menu again leaves the mouse free (still held)", !menu.IsOpen && menu.CursorFree);

            InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.South));
            yield return 0.1f;
            GameMenu.ReleaseCursor(owner);
            yield return 0.2f;
            Check("release with A down: the mouse stays free until A is let go", menu.CursorFree && !GameMenu.CursorHeld);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            int upFrame = Time.frameCount;
            bool askedBack = false;
            void OnMenu() => askedBack |= !menu.CursorFree || menu.IsOpen;
            menu.StateChanged += OnMenu;
            for (int i = 0; i < 20 && !askedBack; i++)
            {
                yield return null;
                askedBack |= !menu.CursorFree || menu.IsOpen;
            }

            menu.StateChanged -= OnMenu;
            Note($"A let go at frame {upFrame}; answered by frame {Time.frameCount}, SubmitHeld {UiInput.SubmitHeld}");
            Note($"after A let go: free {menu.CursorFree}, open {menu.IsOpen} (batch mode can't lock, so the menu then reopens to match)");
            Check("release: once A is up the menu asked for the mouse back (or reopened because batch mode can't lock it)", askedBack);

            GameMenu.HoldCursor(owner);
            menu.Resume();
            yield return 0.2f;
            Check("hold again: closed with the mouse free", !menu.IsOpen && menu.CursorFree);
            Object.Destroy(owner);
            bool relocked = false;
            for (int i = 0; i < 20 && !relocked; i++)
            {
                yield return null;
                relocked = !menu.CursorFree;
            }

            Check("a destroyed holder stops counting and the menu asks for the mouse back (not opening itself)", !GameMenu.CursorHeld && relocked);

            GameMenu.HoldCursor(menu); // held again, then the game is left below: leaving must drop it
            menu.Open();
            yield return 0.1f;
        }

        // ---- Settings persist across a relaunch ----------------------------------------------------------------

        const string OriginalsFile = "ui_settings_originals.txt";
        const string BindingsFile = "ui_bindings_originals.txt";

        static IEnumerator PersistWrite()
        {
            yield return Until(() => Object.FindAnyObjectByType<UiRoot>() != null);
            _root = Object.FindAnyObjectByType<UiRoot>();
            File.WriteAllText(Path.Combine(_out, OriginalsFile),
                $"{(UiSettings.ReducedMotion ? 1 : 0)} {(int)UiSettings.CrosshairStyle} {(int)UiSettings.CrosshairColour} {UiSettings.CrosshairSize} {(UiSettings.NetworkStats ? 1 : 0)} {(UiVideo.VSync ? 1 : 0)} {UiAudio.Get(AudioBus.Music).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            File.WriteAllText(Path.Combine(_out, BindingsFile), InputPrefs.BindingOverridesJson);
            UiSettings.ResetToDefaults();
            Note("player's own settings saved aside; starting from defaults");

            _root.Stack.Push(Settings());
            yield return 0.5f;
            UiInput.ForceKeysOrPad(true);
            SettingsScreen s = Settings();
            Tabs().Select(3, false);
            yield return 0.3f;
            EventSystem.current.SetSelectedGameObject(Find<UiSlider>(s, "CrosshairSize").gameObject);
            for (int i = 0; i < 5; i++)
            {
                Move(MoveDirection.Right);
                yield return null;
            }

            EventSystem.current.SetSelectedGameObject(Find<UiStepper>(s, "CrosshairStyle").gameObject);
            Move(MoveDirection.Left);
            yield return null;
            Find<UiToggle>(s, "ReduceMotion").OnSubmit(new BaseEventData(EventSystem.current));
            yield return null;
            Note($"after editing through the controls: size {UiSettings.CrosshairSize}, style {UiSettings.CrosshairStyle}, reduce motion {UiSettings.ReducedMotion}");
            Check("Accessibility: the controls wrote the settings", UiSettings.CrosshairSize == 150 && UiSettings.CrosshairStyle == CrosshairStyle.Cross && UiSettings.ReducedMotion);

            Tabs().Select(1, false);
            yield return 0.3f;
            Note($"Video: quality levels on this platform: {string.Join(", ", QualitySettings.names)}");
            UiToggle vsync = Find<UiToggle>(s, "VSync");
            bool vsyncBefore = UiVideo.VSync;
            EventSystem.current.SetSelectedGameObject(vsync.gameObject);
            vsync.OnSubmit(new BaseEventData(EventSystem.current));
            yield return null;
            Note($"Video: vertical sync {vsyncBefore} -> {UiVideo.VSync}, vSyncCount {QualitySettings.vSyncCount}");
            Check("Video: the vertical sync toggle changed and applied it", UiVideo.VSync != vsyncBefore && QualitySettings.vSyncCount == (UiVideo.VSync ? 1 : 0));

            Tabs().Select(2, false);
            yield return 0.3f;
            EventSystem.current.SetSelectedGameObject(Find<UiSlider>(s, "Volume1").gameObject);
            for (int i = 0; i < 6; i++)
            {
                Move(MoveDirection.Left);
                yield return null;
            }

            Note($"Audio: music {UiAudio.Get(AudioBus.Music):0.00}");
            Check("Audio: the music slider wrote 50%", Mathf.RoundToInt(UiAudio.Get(AudioBus.Music) * 100f) == 50);

            Tabs().Select(0, false);
            InputPrefs.BindingsHooked = true; // the reader's flag, forced: the hook is on the movement lane's branch
            InputPrefs.BindingOverridesJson = string.Empty;
            yield return 0.3f;
            var controls = (ControlsPage)Tabs().CurrentPage;
            controls.ShowSaved();
            UiButton jump = Find<UiButton>(controls, "KeyButton");
            EventSystem.current.SetSelectedGameObject(jump.gameObject);
            jump.Activate();
            yield return 0.3f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.H));
            yield return 0.25f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.4f;
            Note("Controls: overrides " + InputPrefs.BindingOverridesJson);
            Check("Controls: Jump moved to H and was kept as an override", InputPrefs.BindingOverridesJson.Contains("<Keyboard>/h"));
            Find<UiButton>(s, "Back").Activate(); // Leaving Settings writes them to disk.
            yield return 0.5f;
        }

        static IEnumerator PersistRead()
        {
            yield return Until(() => Object.FindAnyObjectByType<UiRoot>() != null);
            _root = Object.FindAnyObjectByType<UiRoot>();
            _root.Stack.Push(Settings());
            yield return 0.5f;
            SettingsScreen s = Settings();
            Tabs().Select(3, false);
            yield return 0.3f;
            int shownSize = Find<UiSlider>(s, "CrosshairSize").DisplayValue;
            Note($"after relaunch: size {UiSettings.CrosshairSize} (slider shows {shownSize}), style {UiSettings.CrosshairStyle}, reduce motion {UiSettings.ReducedMotion}");
            Check("relaunch, Accessibility: the settings from the last run are back, and the settings screen shows them",
                UiSettings.CrosshairSize == 150 && shownSize == 150 && UiSettings.CrosshairStyle == CrosshairStyle.Cross && UiSettings.ReducedMotion);
            Check("relaunch, Video: vertical sync is still off and applied at launch", !UiVideo.VSync && QualitySettings.vSyncCount == 0,
                $"saved {UiVideo.VSync}, vSyncCount {QualitySettings.vSyncCount}");
            Check("relaunch, Audio: music is still 50%", Mathf.RoundToInt(UiAudio.Get(AudioBus.Music) * 100f) == 50);
            Check("relaunch, Controls: Jump is still on H", InputPrefs.BindingOverridesJson.Contains("<Keyboard>/h"));

            string path = Path.Combine(_out, OriginalsFile);
            string[] v = File.Exists(path) ? File.ReadAllText(path).Split(' ') : new string[0];
            if (v.Length >= 7)
            {
                UiSettings.ReducedMotion = v[0] == "1";
                UiSettings.CrosshairStyle = (CrosshairStyle)int.Parse(v[1]);
                UiSettings.CrosshairColour = (CrosshairColour)int.Parse(v[2]);
                UiSettings.CrosshairSize = int.Parse(v[3]);
                UiSettings.NetworkStats = v[4] == "1";
                UiVideo.VSync = v[5] == "1";
                UiAudio.SetVolume(AudioBus.Music, float.Parse(v[6], System.Globalization.CultureInfo.InvariantCulture));
                string bindings = Path.Combine(_out, BindingsFile);
                InputPrefs.BindingOverridesJson = File.Exists(bindings) ? File.ReadAllText(bindings) : string.Empty;
                PlayerPrefs.Save();
                Note("restored the player's own settings: " + File.ReadAllText(path));
            }
        }
    }
}

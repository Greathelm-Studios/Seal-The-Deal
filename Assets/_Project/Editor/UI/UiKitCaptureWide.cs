using System.Collections;
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// The wide pass's capture runs, one per slice (-uiCaptureMode wide1: the edge marker and device-aware key hints;
    /// wide2: the title on the live sky and the settings fixes; wide3: hosting, joining, loading and every problem,
    /// with a real local host and a real failed join on this PC).
    /// Same rules as
    /// the main run: Lane_Integration in Play Mode, headless with graphics, PASS / FAIL lines and a sound log.
    /// </summary>
    public static partial class UiKitCapture
    {
        static IEnumerator WideScript(string mode)
        {
            yield return Until(() => Object.FindAnyObjectByType<UiRoot>() != null);
            _root = Object.FindAnyObjectByType<UiRoot>();
            SteamLobby lobby = Object.FindAnyObjectByType<SteamLobby>();
            yield return Until(() => lobby != null && lobby.Ready);
            SetUpCamera();
            UiMotion.Paused = true;
            Settle(1.5f);
            switch (mode)
            {
                case "wide1":
                    _root.enabled = false; // the marker sheets clear the screens; the root would put the title back
                    yield return EdgeMarkers();
                    yield return KeyHintDevices();
                    yield return WideClip1();
                    _root.enabled = true;
                    break;
                case "wide2":
                    yield return TitleLiveSky();
                    yield return SettingsFixes();
                    yield return WideClip2();
                    break;
                case "wide3":
                    yield return SessionScreens(lobby);
                    break;
                case "wide-transitions":
                    yield return StackTransitions(lobby);
                    break;
                case "wide-frame":
                    yield return TitleFrame(Arg("-uiShotPrefix") ?? "14_title_frame");
                    break;
                default:
                    Fail("no capture script for mode " + mode);
                    break;
            }

            UiGlyphs.Force(UiDeviceKind.KeyboardMouse);
        }

        // ---- Slice 1: edge marker ---------------------------------------------------------------------

        static readonly List<UiEdgeMarker> Markers = new();

        static UiEdgeMarker NewMarker(string name, UiMarkKind kind)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(UiKitAssetsPrefab("EdgeMarker"), _root.transform);
            go.name = name;
            var marker = go.GetComponent<UiEdgeMarker>();
            marker.Kind = kind;
            Markers.Add(marker);
            return marker;
        }

        static GameObject UiKitAssetsPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(UiKitAssets.KitFolder + "/" + name + ".prefab");

        /// <summary>
        /// Six marks over an empty canvas (the grey stands in for the game): one in view, four out of view in each
        /// direction (one behind the camera, mirrored), and one straight behind (the bottom edge). At three sizes.
        /// </summary>
        static IEnumerator EdgeMarkers()
        {
            _root.Stack.Clear();
            Settle(1f);
            UiEdgeMarker inView = NewMarker("InView", UiMarkKind.Ours);
            UiEdgeMarker right = NewMarker("OffRight", UiMarkKind.Rival);
            UiEdgeMarker topLeft = NewMarker("OffTopLeft", UiMarkKind.Ours);
            UiEdgeMarker behindLeft = NewMarker("BehindLeft", UiMarkKind.Rival);
            UiEdgeMarker straightBehind = NewMarker("StraightBehind", UiMarkKind.Loose);
            UiEdgeMarker below = NewMarker("OffBelow", UiMarkKind.Loose);

            (int w, int h)[] sizes = { (1920, 1080), (1280, 720), (2560, 1080) };
            foreach ((int w, int h) in sizes)
            {
                Resize(w, h);
                Vector2 size = ((RectTransform)_root.transform).rect.size;
                Vector2 c = size * 0.5f;
                inView.Place(c + new Vector2(260f, 120f), true, 18.4f);
                right.Place(c + new Vector2(size.x * 1.4f, 90f), true, 42f);
                topLeft.Place(new Vector2(-300f, size.y + 500f), true, 7f);
                behindLeft.Place(c + new Vector2(-200f, 40f), false, 61f); // mirrored: behind you, to the left
                straightBehind.Place(c, false, 3f);
                below.Place(new Vector2(c.x + 100f, -900f), true, -1f);
                Settle(0.5f);
                Shot($"12_marker_{w}x{h}", w, h);

                float edge = 48f;
                bool allInside = true;
                foreach (UiEdgeMarker m in Markers)
                {
                    Vector2 p = m.MarkPosition;
                    allInside &= p.x >= edge - 0.5f && p.y >= edge - 0.5f && p.x <= size.x - edge + 0.5f && p.y <= size.y - edge + 0.5f;
                }

                Check($"marker {w}x{h}: every mark stays inside the 48 px safe edge", allInside);
                Check($"marker {w}x{h}: in view sits on its target with no chevron", !inView.OnEdge && Vector2.Distance(inView.MarkPosition, c + new Vector2(260f, 120f)) < 0.5f);
                Check($"marker {w}x{h}: off to the right waits on the right edge", right.OnEdge && Mathf.Abs(right.MarkPosition.x - (size.x - edge)) < 0.5f);
                Check($"marker {w}x{h}: behind and to the left goes to the left edge, not into view",
                    behindLeft.OnEdge && Mathf.Abs(behindLeft.MarkPosition.x - edge) < 0.5f, $"at {behindLeft.MarkPosition}");
                Check($"marker {w}x{h}: straight behind goes to the bottom edge",
                    straightBehind.OnEdge && Mathf.Abs(straightBehind.MarkPosition.y - edge) < 0.5f && Mathf.Abs(straightBehind.MarkPosition.x - c.x) < 0.5f,
                    $"at {straightBehind.MarkPosition}");
            }

            // Through the real projection: a camera at the origin looking along +z, a point 10 m behind it.
            var probe = new GameObject("MarkerProbeCamera").AddComponent<Camera>();
            probe.enabled = false;
            probe.targetTexture = _cam.targetTexture;
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float canvasW = ((RectTransform)_root.transform).rect.width;
            straightBehind.Track(probe, new Vector3(0.5f, 0f, -10f));
            Check("marker through WorldToCanvas.TryProject: a point behind you and to the right waits on the right edge",
                straightBehind.OnEdge && straightBehind.MarkPosition.x > canvasW - 49f, $"at {straightBehind.MarkPosition}");
            straightBehind.Track(probe, new Vector3(0f, 0f, -10f));
            Check("marker through WorldToCanvas.TryProject: a point dead behind you goes to the bottom edge",
                straightBehind.OnEdge && straightBehind.MarkPosition.y < 49f, $"at {straightBehind.MarkPosition}");
            straightBehind.Track(probe, new Vector3(0f, 0f, 10f));
            Check("marker through WorldToCanvas.TryProject: a point straight ahead sits in the centre, in view",
                !straightBehind.OnEdge && Vector2.Distance(straightBehind.MarkPosition, ((RectTransform)_root.transform).rect.size * 0.5f) < 2f,
                $"at {straightBehind.MarkPosition}");
            Object.DestroyImmediate(probe.gameObject);
            straightBehind.Place(((RectTransform)_root.transform).rect.size * 0.5f, false, 3f);

            int sizeAllocs = 0;
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
                right.Place(new Vector2(4000f, 500f + i), true, 42.2f);
            sizeAllocs = (int)(System.GC.GetAllocatedBytesForCurrentThread() - before);
            Check("marker: placing it every frame allocates nothing while the metre stays the same", sizeAllocs == 0, $"{sizeAllocs} bytes over 200 calls");
            yield return null;
        }

        // ---- Slice 1: key hints by device ----------------------------------------------------------------

        static IEnumerator KeyHintDevices()
        {
            foreach (UiEdgeMarker m in Markers)
                m.Hide();
            Settle(0.3f);
            _root.Stack.Push(_root.Title);
            Settle(1.5f);
            TitleScreen title = _root.Title;
            UiKeyHint select = Find<UiKeyHint>(title, "SelectHint");
            UiKeyHint back = Find<UiKeyHint>(title, "BackHint");
            UiKeyHint move = Find<UiKeyHint>(title, "MoveHint");
            (UiDeviceKind kind, string name, string s, string b, string m)[] expect =
            {
                (UiDeviceKind.KeyboardMouse, "keys", "Enter", "Esc", "↑↓"),
                (UiDeviceKind.Xbox, "xbox", "A", "B", "LS"),
                (UiDeviceKind.PlayStation, "playstation", "Cross", "Circle", "L stick"),
            };
            foreach (var e in expect)
            {
                UiGlyphs.Force(e.kind);
                Settle(0.2f);
                Shot($"12_title_hints_{e.name}_1080", 1920, 1080);
                Check($"title key hints on {e.name}: select, back and move read {e.s} / {e.b} / {e.m}",
                    select.KeyText == e.s && back.KeyText == e.b && move.KeyText == e.m, $"{select.KeyText} / {back.KeyText} / {move.KeyText}");
            }

            _root.Stack.Push(Settings());
            Settle(1.2f);
            UiKeyHint nextTab = Find<UiKeyHint>(Settings(), "NextTabHint");
            foreach (var e in expect)
            {
                UiGlyphs.Force(e.kind);
                Settle(0.2f);
                Shot($"12_settings_hints_{e.name}_1080", 1920, 1080);
                string want = e.kind == UiDeviceKind.KeyboardMouse ? "E" : e.kind == UiDeviceKind.PlayStation ? "R1" : "RB";
                Check($"settings tab hint on {e.name} reads {want}", nextTab.KeyText == want, nextTab.KeyText);
            }

            UiGlyphs.Force(UiDeviceKind.KeyboardMouse);
            _root.Stack.Pop();
            Settle(1.2f);
            yield return null;
        }

        // ---- Slice 1 clip ----------------------------------------------------------------------------------

        static int _frame;

        static void Film(string folder, float seconds, System.Action<float> each = null)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                each?.Invoke(t);
                UiMotion.Tick(Step);
                Texture2D tex = Grab(1280, 720);
                File.WriteAllBytes(Path.Combine(folder, $"c{_frame++:0000}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }

        static IEnumerator WideClip1()
        {
            string folder = Path.Combine(_out, "clip_wide1");
            Directory.CreateDirectory(folder);
            _frame = 0;
            _root.Stack.Clear();
            Film(folder, 0.6f);
            Resize(1280, 720);
            Vector2 size = ((RectTransform)_root.transform).rect.size;
            Vector2 c = size * 0.5f;
            UiEdgeMarker sweep = Markers[1];
            UiEdgeMarker flash = Markers[0];
            for (int i = 2; i < Markers.Count; i++)
                Markers[i].Hide();
            flash.Kind = UiMarkKind.Ours;
            // A target walks from in view, off the right, round behind and back in from the left.
            Film(folder, 4f, t =>
            {
                float a = t / 4f * Mathf.PI * 2f;
                var world = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                bool front = world.z > 0.05f;
                float x = front ? c.x + world.x / Mathf.Max(world.z, 0.05f) * 520f : c.x + world.x * 900f;
                sweep.Place(new Vector2(x, c.y + 60f), front, 30f + 10f * Mathf.Sin(a));
                flash.Place(c + new Vector2(-220f, -120f), true, 12f);
            });
            flash.Kind = UiMarkKind.Rival;
            flash.Flash();
            Film(folder, 0.8f, _ => flash.Place(c + new Vector2(-220f, -120f), true, 12f));
            foreach (UiEdgeMarker m in Markers)
                m.Hide();
            Film(folder, 0.3f);
            _root.Stack.Push(_root.Title);
            Film(folder, 1.2f);
            UiGlyphs.Force(UiDeviceKind.Xbox);
            Film(folder, 0.8f);
            UiGlyphs.Force(UiDeviceKind.PlayStation);
            Film(folder, 0.8f);
            UiGlyphs.Force(UiDeviceKind.KeyboardMouse);
            Film(folder, 0.8f);
            Note($"clip_wide1: {_frame} frames at 30 fps");
            foreach (UiEdgeMarker m in Markers)
                Object.DestroyImmediate(m.gameObject);
            Markers.Clear();
            yield return null;
        }

        // ---- Slice 2: the title on the live sky, settings fixes ---------------------------------------------

        static IEnumerator TitleLiveSky()
        {
            TitleScreen title = _root.Title;
            // The live sky renders on the Play Mode frames; give it real frames to draw before the shots.
            for (int i = 0; i < 20; i++)
                yield return null;
            Transform staticSky = title.transform.Find("SkyWindow/StaticSky");
            Transform slot = title.BackdropSlot;
            bool live = slot != null && slot.GetComponentInChildren<UnityEngine.UI.RawImage>(true) != null;
            Check("title: the live sky is in the backdrop slot and the flat fallback is off",
                live && staticSky != null && !staticSky.gameObject.activeSelf, $"live image {live}, fallback active {staticSky != null && staticSky.gameObject.activeSelf}");
            Check("title: the keys row names the keyboard's keys from the first frame (before any device change)",
                Find<UiKeyHint>(title, "SelectHint").KeyText == "Enter" && Find<UiKeyHint>(title, "BackHint").KeyText == "Esc",
                Find<UiKeyHint>(title, "SelectHint").KeyText + " / " + Find<UiKeyHint>(title, "BackHint").KeyText);
            Check("title: no painted clouds, tower or dot screen left in the panes",
                staticSky != null && staticSky.childCount == 0);
            Settle(0.5f);
            Shot("12_title_live_sky_1080", 1920, 1080);
            Shot("12_title_live_sky_720", 1280, 720);
            Shot("12_title_live_sky_2560x1080", 2560, 1080);
            CheckInside(title.transform, 1280, 720, "title at 1280x720");
            CheckInside(title.transform, 2560, 1080, "title at 2560x1080");
        }

        // ---- Lane 8's transitions from the screen stack ----------------------------------------------------

        static int _filmFrame;
        static readonly List<string> TransitionLog = new();

        /// <summary>
        /// One real frame filmed: the kit's motion steps 1/30 s by hand, and lane 8's transition steps by itself (its
        /// step is capped at 1/30 s and batch frames are slower than that), so the clip plays at 30 fps.
        /// </summary>
        static void FilmFrame(string folder)
        {
            UiMotion.Tick(Step);
            Texture2D tex = Grab(1280, 720);
            File.WriteAllBytes(Path.Combine(folder, $"t{_filmFrame:0000}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            if (CloseTheDeal.UI.Title.UiTransition.IsPlaying)
                TransitionLog.Add(_filmFrame.ToString("0000"));
            _filmFrame++;
        }

        static IEnumerator Film(string folder, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                FilmFrame(folder);
                yield return null;
            }
        }

        /// <summary>
        /// Settings opens and closes under PaneSlide, hosting opens under CloudWipe, and the game starts under FrameZoom,
        /// all filmed from the transition's own overlay (bound to the capture camera, so it is in the frames). Checks
        /// that each change waited for its cover, and that Esc during a transition does nothing.
        /// </summary>
        static IEnumerator StackTransitions(SteamLobby lobby)
        {
            string folder = Path.Combine(_out, "transitions");
            Directory.CreateDirectory(folder);
            _filmFrame = 0;
            TransitionLog.Clear();
            UiScreenStack.UseTransitions = true; // the other capture runs step motion by hand and switch them off
            var host = CloseTheDeal.UI.Title.UiTransitionHost.Get(_cam); // the overlay draws into the capture's frames
            foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 5;
            UiMotion.Paused = true;
            TitleScreen title = _root.Title;
            SettingsScreen settings = Settings();
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            yield return Film(folder, 0.5f);

            // Settings: PaneSlide forward.
            UiButton open = Find<UiButton>(title, "Settings");
            ExecuteEvents.Execute(open.gameObject, ped, ExecuteEvents.pointerDownHandler);
            yield return Film(folder, 0.1f);
            ExecuteEvents.Execute(open.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(open.gameObject, ped, ExecuteEvents.pointerClickHandler);
            bool waited = _root.Stack.Top == title && _root.Stack.Busy;
            int before = TransitionLog.Count;
            yield return Film(folder, 0.9f);
            Check("Settings opens under PaneSlide: the change waits for the cover, then Settings is on top",
                waited && _root.Stack.Top == settings && TransitionLog.Count > before, $"{TransitionLog.Count - before} covered frames");

            // Back: PaneSlide again; Esc pressed while it plays is ignored (no second pop).
            settings.OnCancel();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            before = TransitionLog.Count;
            yield return Film(folder, 1.0f);
            Check("Back from Settings under PaneSlide lands on the title; an Esc during the cover changes nothing",
                _root.Stack.Top == title && _root.Stack.Count == 1 && TransitionLog.Count > before, $"top {_root.Stack.Top?.name}, count {_root.Stack.Count}");

            // Host: the session panel under CloudWipe, then the game under FrameZoom.
            ushort port = ushort.TryParse(Arg("-lanePort"), out ushort p) ? p : (ushort)17785;
            var tugboat = Object.FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat != null)
                tugboat.SetPort(port);
            UiButton hostButton = Find<UiButton>(title, "HostGame");
            ExecuteEvents.Execute(hostButton.gameObject, ped, ExecuteEvents.pointerDownHandler);
            yield return Film(folder, 0.1f);
            ExecuteEvents.Execute(hostButton.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(hostButton.gameObject, ped, ExecuteEvents.pointerClickHandler);
            bool sawSession = false;
            for (float t = 0f; t < 8f && (PlayerMotor.Local == null || _root.Stack.Count > 0 || CloseTheDeal.UI.Title.UiTransition.IsPlaying); t += Step)
            {
                FilmFrame(folder);
                sawSession |= _root.Stack.Top == _root.Session;
                yield return null;
            }

            yield return Film(folder, 0.6f);
            Check("this run is the host (not a client of someone else's game)", lobby.IsHosting);
            Check("hosting showed the session panel (CloudWipe) and the game start cleared the screens (FrameZoom)",
                PlayerMotor.Local != null && _root.Stack.Count == 0, $"session seen {sawSession}, stack {_root.Stack.Count}");
            Note($"filmed {_filmFrame} frames, {TransitionLog.Count} of them under a transition");

            lobby.Leave();
            yield return Film(folder, 1.5f);
            Check("leaving brings the title back (under CloudWipe)", _root.Stack.Top == title);
            Note("covered frames: " + string.Join(",", TransitionLog));
            UiMotion.Paused = false;
            UiScreenStack.UseTransitions = false;
        }

        /// <summary>
        /// The title's window frame on the live sky at 1080p, 720p and 21:9 (prefix chosen on the command line, so a
        /// before / after pair comes from the same code), with the frame's measured bar widths in the log.
        /// </summary>
        static IEnumerator TitleFrame(string prefix)
        {
            TitleScreen title = _root.Title;
            for (int i = 0; i < 20; i++)
                yield return null;
            Settle(0.5f);
            Resize(1920, 1080);
            var mullion = title.transform.Find("SkyWindow/Frame/Mullions/MullionLeft") as RectTransform;
            var reveal = mullion != null ? mullion.Find("Reveal") as RectTransform : null;
            var transom = title.transform.Find("SkyWindow/Frame/Transom") as RectTransform;
            var shade = transom != null ? transom.Find("Shadow") as RectTransform : null;
            if (mullion != null && transom != null)
                Note($"frame at 1080p: mullion face {mullion.rect.width:0} px + reveal {(reveal != null ? reveal.rect.width : 0f):0} px; transom {transom.rect.height:0} px + shadow {(shade != null ? shade.rect.height : 0f):0} px");
            Shot(prefix + "_1080", 1920, 1080);
            Shot(prefix + "_720", 1280, 720);
            Shot(prefix + "_2560x1080", 2560, 1080);
            CheckInside(title.transform, 1280, 720, "title at 1280x720");
            CheckInside(title.transform, 2560, 1080, "title at 2560x1080");
        }

        static IEnumerator SettingsFixes()
        {
            _root.Stack.Push(Settings());
            Settle(1.2f);
            UiTabPages tabs = Tabs();
            tabs.Select(2, false);
            Settle(0.6f);
            UiSlider[] sliders = Settings().GetComponentsInChildren<UiSlider>(false);
            float tallest = 0f;
            foreach (UiSlider s in sliders)
            {
                if (s.handleRect != null)
                    tallest = Mathf.Max(tallest, s.handleRect.rect.height);
            }

            Check("audio: slider thumbs are shorter than a row, so full-volume rows never join into one bar", tallest > 0f && tallest <= 32f, $"tallest thumb {tallest:0} px");
            TMPro.TMP_Text heading = Find<TMPro.TMP_Text>(Settings(), "ChangesHeading");
            TMPro.TMP_Text body = Find<TMPro.TMP_Text>(Settings(), "ChangesBody");
            string emptyBody = body != null ? body.text : null;
            Check("empty: with nothing changed the summary says so", emptyBody != null && emptyBody.StartsWith("Nothing on this tab"), emptyBody);
            Shot("12_settings_audio_1080", 1920, 1080);
            Shot("12_settings_audio_720", 1280, 720);
            Shot("12_settings_audio_2560x1080", 2560, 1080);
            CheckInside(Settings().transform, 1280, 720, "settings at 1280x720");

            // A change shows up in the summary at once (Music down to 40%, then back).
            int before = Mathf.RoundToInt(UiAudio.Get(AudioBus.Music) * 100f);
            UiSlider music = Find<UiSlider>(Settings(), "Volume1");
            music.value = 8f; // 40%
            Settle(0.4f);
            Check("audio: the summary lists the changed music level against its shipped value",
                body != null && body.text.Contains("Music") && body.text.Contains("40%") && heading.text.EndsWith("1"), body != null ? body.text : "no body");
            Shot("12_settings_audio_changed_1080", 1920, 1080);
            UiAudio.SetVolume(AudioBus.Music, before / 100f);

            tabs.Select(3, false);
            Settle(0.6f);
            Shot("12_settings_accessibility_1080", 1920, 1080);
            tabs.Select(0, false);
            Settle(0.6f);
            Check("controls: the key list fills the page, so the summary is hidden there",
                Find<CanvasGroup>(Settings(), "Block") != null && Find<CanvasGroup>(Settings(), "Block").alpha == 0f);
            Shot("12_settings_controls_1080", 1920, 1080);
            tabs.Select(1, false);
            Settle(0.6f);
            Shot("12_settings_video_1080", 1920, 1080);
            _root.Stack.Pop();
            Settle(1.2f);
            yield return null;
        }

        static IEnumerator WideClip2()
        {
            string folder = Path.Combine(_out, "clip_wide2");
            Directory.CreateDirectory(folder);
            _frame = 0;
            for (int i = 0; i < 10; i++)
                yield return null;
            Film(folder, 1.5f);
            UiButton settingsButton = Find<UiButton>(_root.Title, "Settings");
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(settingsButton.gameObject, ped, ExecuteEvents.pointerEnterHandler);
            Film(folder, 0.3f);
            ExecuteEvents.Execute(settingsButton.gameObject, ped, ExecuteEvents.pointerDownHandler);
            Film(folder, 0.1f);
            ExecuteEvents.Execute(settingsButton.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(settingsButton.gameObject, ped, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(settingsButton.gameObject, ped, ExecuteEvents.pointerExitHandler);
            Film(folder, 0.9f);
            UiInput.ForceKeysOrPad(true);
            Tabs().Select(2, true);
            Film(folder, 0.6f);
            UiSlider music = Find<UiSlider>(Settings(), "Volume1");
            EventSystem.current.SetSelectedGameObject(music.gameObject);
            Film(folder, 0.3f);
            int before = Mathf.RoundToInt(UiAudio.Get(AudioBus.Music) * 100f);
            for (int i = 0; i < 4; i++)
            {
                Move(MoveDirection.Left);
                Film(folder, 0.2f);
            }

            Film(folder, 0.5f);
            UiAudio.SetVolume(AudioBus.Music, before / 100f);
            Settings().GetComponentInChildren<AudioPage>(true).ShowSaved();
            _root.Stack.Cancel();
            Film(folder, 1.2f);
            UiInput.ForceKeysOrPad(false);
            Note($"clip_wide2: {_frame} frames at 30 fps");
        }

        // ---- Slice 3: hosting, joining, loading, problems ------------------------------------------------------

        static IEnumerator SessionScreens(SteamLobby lobby)
        {
            ushort port = ushort.TryParse(Arg("-lanePort"), out ushort p) ? p : (ushort)17785;
            var tugboat = Object.FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat != null)
                tugboat.SetPort(port);
            Note($"local test port {port}, local mode {lobby.LocalMode}, Steam ready {SteamService.IsReady}");
            UiSoundPlayer.IgnoreFocus = true;
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            string folder = Path.Combine(_out, "clip_wide3");
            Directory.CreateDirectory(folder);
            _frame = 0;
            UiMotion.Paused = false;
            for (int i = 0; i < 10; i++)
                yield return null;
            UiMotion.Paused = true;
            TitleScreen title = _root.Title;
            Settle(0.6f);
            Shot("13_title_local_1080", 1920, 1080);
            Shot("13_title_local_720", 1280, 720);
            Shot("13_title_local_2560x1080", 2560, 1080);
            Check("offline: in local mode without Steam the title says friends can't join and how to fix it",
                Find<TMPro.TMP_Text>(title, "JoinHint").gameObject.activeSelf && Find<TMPro.TMP_Text>(title, "JoinHint").text.StartsWith("Steam is not running"));
            UiMotion.Paused = true;
            yield return MarkerSideSwitch(folder);
            UiMotion.Paused = false;

            // 1. A join to a port where something takes the packets but never answers (a silent UDP socket): the
            // client really waits, so the connecting state, its clock and Cancel can be seen; then the problem.
            ushort silentPort = (ushort)(port + 1);
            var silent = new System.Net.Sockets.UdpClient(silentPort);
            tugboat?.SetPort(silentPort);
            int sound0 = Sounds.Count;
            lobby.JoinLocal();
            yield return Until(() => _root.Stack.Top == _root.Session);
            Check("joining: the session panel opens over the title", _root.Stack.Top == _root.Session, lobby.Stage.ToString());
            Film(folder, 0.6f);
            UiMotion.Paused = true;
            Settle(0.1f);
            Shot("13_session_joining_1080", 1920, 1080);
            Shot("13_session_joining_720", 1280, 720);
            Shot("13_session_joining_2560x1080", 2560, 1080);
            CheckInside(_root.Session.transform, 1280, 720, "session panel at 1280x720");
            CheckInside(_root.Session.transform, 2560, 1080, "session panel at 2560x1080");
            UiMotion.Paused = false;
            Note($"stage while joining: {lobby.Stage}, status '{lobby.StatusText}', {lobby.ConnectElapsed:0.0} s of {lobby.ConnectTimeout:0} s");
            yield return 3f;
            UiMotion.Paused = true;
            Settle(0.1f);
            Shot("13_session_joining_3s_1080", 1920, 1080);
            UiMotion.Paused = false;
            Check("joining: after 3 s the track is filling at the real rate", lobby.Stage == LobbyStage.Connecting && lobby.ConnectElapsed > 2.5f,
                $"{lobby.Stage}, {lobby.ConnectElapsed:0.0} s");
            float waitUntil = Time.realtimeSinceStartup + 25f;
            yield return Until(() => lobby.Problem != LobbyProblem.None || !lobby.IsActive || Time.realtimeSinceStartup > waitUntil);
            yield return 0.6f;
            Check("failure: a join with no host ends on the problem panel, with Try again usable",
                _root.Stack.Top == _root.Problem && lobby.Problem == LobbyProblem.Unreachable && Find<UiButton>(_root.Problem, "Fix").interactable,
                $"problem {lobby.Problem}, top {(_root.Stack.Top != null ? _root.Stack.Top.name : "none")}, status '{lobby.StatusText}'");
            Check("the problem arrived with the Error sound", Sounds.GetRange(sound0, Sounds.Count - sound0).Exists(x => x.EndsWith(" Error")),
                string.Join(",", Sounds.GetRange(sound0, Sounds.Count - sound0)));
            UiMotion.Paused = true;
            Settle(0.4f);
            Shot("13_problem_unreachable_1080", 1920, 1080);
            Shot("13_problem_unreachable_720", 1280, 720);
            Shot("13_problem_unreachable_2560x1080", 2560, 1080);
            CheckInside(_root.Problem.transform, 1280, 720, "problem panel at 1280x720");
            UiMotion.Paused = false;
            Film(folder, 0.8f);

            // 2. Back (Esc) forgets it: the title, its status, no panel.
            _root.Stack.Cancel();
            yield return 0.6f;
            Check("Back on a problem returns to the title and forgets the problem",
                _root.Stack.Top == title && lobby.Problem == LobbyProblem.None, $"top {_root.Stack.Top?.name}");
            Film(folder, 0.6f);

            // 3. Cancel while connecting: the lobby stops, the title says so, no problem.
            lobby.JoinLocal();
            yield return Until(() => _root.Stack.Top == _root.Session);
            yield return 1f;
            Film(folder, 0.5f);
            _root.Stack.Cancel();
            yield return 0.6f;
            Check("edge: Cancel (Esc) while connecting stops the join and lands on the title with no problem",
                !lobby.IsActive && _root.Stack.Top == title && lobby.Problem == LobbyProblem.None && lobby.StatusText == "You left the game.",
                $"active {lobby.IsActive}, top {_root.Stack.Top?.name}, status '{lobby.StatusText}'");
            Film(folder, 0.6f);
            silent.Close();
            tugboat?.SetPort(port);

            // 4. Host: the session panel says hosting, then the window closes over the view and the game starts.
            bool sawSession = false;
            System.Action watch = () => sawSession |= _root.Stack.Top == _root.Session;
            _root.Stack.Changed += watch;
            Find<UiButton>(title, "HostGame").Activate();
            yield return null;
            Check("this run is the host (not a client of someone else's game)", lobby.IsHosting);
            FilmTransition(folder, CloseTheDeal.UI.Title.UiTransitionKind.FrameZoom);
            Film(folder, 0.4f);
            yield return Until(() => PlayerMotor.Local != null);
            Film(folder, 1.2f);
            yield return 0.6f;
            _root.Stack.Changed -= watch;
            Check("hosting: the session panel showed, then the screens cleared for the game (window zoom)",
                sawSession && _root.Stack.Count == 0 && !menu.OutOfGame, $"saw session {sawSession}, stack {_root.Stack.Count}");
            Check("in a game the kit is out of the way and the player is ours", !menu.OutOfGame && PlayerMotor.Local != null && _root.Stack.Count == 0);
            lobby.Leave();
            yield return 1.2f;
            Check("leaving brings the title back with 'You left the game.' and no problem panel",
                _root.Stack.Top == title && lobby.Problem == LobbyProblem.None, $"top {_root.Stack.Top?.name}");
            Film(folder, 0.8f);
            Note($"clip_wide3: {_frame} frames at 30 fps");

            yield return ProblemSheet(lobby);
            yield return SoundProof(UiSoundEvent.PanelOpen, UiSoundEvent.PanelClose, UiSoundEvent.Error, UiSoundEvent.Back, UiSoundEvent.Confirm, UiSoundEvent.Select);
            UiSoundPlayer.IgnoreFocus = false;
        }

        /// <summary>
        /// Films lane 8's transition through its own preview path onto the capture camera (the live overlay draws on an
        /// overlay canvas the capture camera can't see). The game view behind it isn't in the capture.
        /// </summary>
        static void FilmTransition(string folder, CloseTheDeal.UI.Title.UiTransitionKind kind)
        {
            float length = CloseTheDeal.UI.Title.UiTransition.Duration(kind);
            for (float t = 0f; t <= length; t += Step)
            {
                CloseTheDeal.UI.Title.UiTransition.Preview(kind, t / length, _cam);
                Texture2D tex = Grab(1280, 720);
                File.WriteAllBytes(Path.Combine(folder, $"c{_frame++:0000}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            CloseTheDeal.UI.Title.UiTransition.ClearPreview();
        }

        /// <summary>A target crossing behind you: the mark slides round the bottom edge to the other side, not a jump.</summary>
        static IEnumerator MarkerSideSwitch(string folder)
        {
            var marker = NewMarker("SideSwitch", UiMarkKind.Rival);
            Resize(1280, 720);
            Vector2 size = ((RectTransform)_root.transform).rect.size;
            Vector2 c = size * 0.5f;
            marker.Place(new Vector2(-500f, c.y), true, 20f);
            Settle(0.3f);
            marker.Place(new Vector2(-500f, c.y), true, 20f);
            marker.Place(new Vector2(size.x + 500f, c.y), false, 20f); // now behind, on the right
            Vector2 first = marker.MarkPosition;
            Check("marker: a side flip starts a slide instead of jumping across", first.x < c.x, $"first frame at {first}");
            Film(folder, 0.3f, _ => marker.Place(new Vector2(size.x + 500f, c.y), false, 20f));
            Check("marker: the slide lands on the new side within 150 ms", Mathf.Abs(marker.MarkPosition.x - (size.x - 48f)) < 1f, $"at {marker.MarkPosition}");
            Object.DestroyImmediate(marker.gameObject);
            Markers.Clear();
            yield return null;
        }

        /// <summary>
        /// Every problem the lobby can report, drawn by the real panel. Only Unreachable happens on one PC without
        /// Steam; the others are filled in by the panel's own Show(problem) for the sheet and say so.
        /// </summary>
        static IEnumerator ProblemSheet(SteamLobby lobby)
        {
            UiMotion.Paused = true;
            _root.enabled = false; // the root would close a panel the lobby isn't asking for; the sheet drives it by hand
            _root.Stack.Push(_root.Problem);
            Settle(1f);
            var problems = (LobbyProblem[])System.Enum.GetValues(typeof(LobbyProblem));
            const int cellW = 1000, cellH = 240;
            var sheet = new Texture2D(cellW * 2, cellH * ((problems.Length) / 2), TextureFormat.RGB24, false);
            int n = 0;
            foreach (LobbyProblem problem in problems)
            {
                if (problem == LobbyProblem.None)
                    continue;
                _root.Problem.Show(problem);
                Settle(0.2f);
                Texture2D frame = Grab(1920, 1080);
                int col = n % 2, row = n / 2;
                Blit(frame, sheet, new RectInt(824, 1080 - 980, cellW, cellH), col * cellW, sheet.height - (row + 1) * cellH, cellW, cellH);
                Object.DestroyImmediate(frame);
                string headline = Find<TMPro.TMP_Text>(_root.Problem, "Headline").text;
                UiButton fix = Find<UiButton>(_root.Problem, "Fix");
                Note($"problem {problem}: '{headline}' fix '{(fix.gameObject.activeSelf ? fix.Fx.Label.text : "none")}' usable {fix.interactable}");
                n++;
            }

            File.WriteAllBytes(Path.Combine(_out, "13_states_problems.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Note("wrote 13_states_problems.png (every LobbyProblem, two per row, in enum order)");
            Check("every problem has a headline and a way forward", n == problems.Length - 1);
            _root.Stack.Pop();
            _root.enabled = true;
            Settle(1f);
            yield return null;
        }
    }
}

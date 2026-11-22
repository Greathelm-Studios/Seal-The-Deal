using System.Collections;
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// The pointer and resolution run (-uiCaptureMode wide-probe). Clicks every settings control through the real
    /// GraphicRaycaster (the main run sends events straight to controls, so it can't see a click that never lands),
    /// presses the arrow keys on each stepper, and films the title and every settings tab from 1024x768 up to 8K,
    /// with native-pixel crops of a stepper and a button at 4K and 8K.
    /// </summary>
    public static partial class UiKitCapture
    {
        static readonly List<RaycastResult> Hits = new();

        static readonly (int w, int h)[] ProbeSizes =
        {
            (1024, 768), (1280, 720), (1920, 1080), (2560, 1600), (3840, 2160), (5120, 1440), (7680, 4320),
        };

        static IEnumerator Probe()
        {
            Resize(1920, 1080);
            var title = _root.Title;
            foreach (UiButton button in title.GetComponentsInChildren<UiButton>(false))
                ProbeHit(button, button.gameObject, "title");

            _root.Stack.Push(Settings());
            Settle(1f);
            yield return 1f;
            yield return ProbeSettingsTabs();

            UiInput.ForceKeysOrPad(false);
            yield return ArrowFilm();
            yield return Resolutions(title);
            Tabs().Select(0, false);
            _root.Stack.Pop();
            Settle(1f);
        }

        static IEnumerator ProbeSettingsTabs()
        {
            for (int i = 0; i < TabNames.Length; i++)
            {
                Tabs().Select(i, false);
                Settle(0.5f);
                yield return 0.6f;
                UiSettingsPage page = Tabs().CurrentPage;
                ProbeHit(Tabs().TabAt(i), Tabs().TabAt(i).gameObject, "tab");
                foreach (Selectable control in page.GetComponentsInChildren<Selectable>(false))
                    yield return ProbeControl(control, TabNames[i]);
            }
        }

        /// <summary>
        /// The same clicks in a hosted game: Esc opens the in-game menu, its Settings button opens Settings, and the
        /// kit's motion runs live the whole time. Every canvas renders through the capture camera so the hit order
        /// between the kit and the HUD canvases is the game's own (their sort orders).
        /// </summary>
        static IEnumerator ProbeInGame(CloseTheDeal.Net.SteamLobby lobby)
        {
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            InGameMenuScreen pane = _root.GameMenuScreen;
            Check("the kit has the in-game menu", pane != null && menu != null);
            if (pane == null || menu == null)
                yield break;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _cam;
                canvas.planeDistance = 2f;
            }

            UiMotion.Paused = false;
            UiScreenStack.UseTransitions = true;
            Find<UiButton>(_root.Title, "HostGame").Activate();
            yield return Until(() => CloseTheDeal.Player.PlayerMotor.Local != null);
            yield return 3f;
            Check("hosting", lobby.IsHosting);
            if (!menu.IsOpen)
            {
                yield return Press(Key.Escape);
                yield return 0.8f;
            }

            Check("Esc opened the in-game menu", menu.IsOpen && _root.Stack.Top == pane, _root.Stack.Top != null ? _root.Stack.Top.name : "none");
            Resize(1920, 1080);
            UiButton settings = Find<UiButton>(pane, "Settings");
            if (ProbeHit(settings, settings.gameObject, "ingame"))
                settings.Activate();
            yield return 1.2f;
            Check("Settings opened from the in-game menu", _root.Stack.Top is SettingsScreen);
            if (!(_root.Stack.Top is SettingsScreen))
                yield break;
            _live = true;
            yield return ProbeSettingsTabs();
            _live = false;
        }

        static bool _live;

        static IEnumerator ProbeControl(Selectable control, string tab)
        {
            string label = $"{tab}/{control.name}";
            if (control is UiStepper stepper)
            {
                int count = stepper.OptionCount;
                Note($"{label}: {count} option(s), index {stepper.Index}, interactable {stepper.IsInteractable()}");
                foreach (UiClickZone zone in stepper.GetComponentsInChildren<UiClickZone>(false))
                {
                    int before = stepper.Index;
                    yield return ClickThrough(zone.gameObject, label + "/" + zone.name);
                    bool landed = _landed;
                    if (landed && count > 1)
                        Check($"{label}: clicking {zone.name} changes the choice", stepper.Index != before, $"{before} -> {stepper.Index}");
                    yield return CloseDialog();
                    if (stepper.Index != before)
                        stepper.Step(zone.name == "NextZone" ? -1 : 1);   // put it back
                    yield return CloseDialog();
                }

                if (count > 1 && stepper.IsInteractable())
                {
                    UiInput.ForceKeysOrPad(true);
                    EventSystem.current.SetSelectedGameObject(stepper.gameObject);
                    Settle(0.2f);
                    int before = stepper.Index;
                    yield return Press(Key.RightArrow);
                    int afterRight = stepper.Index;
                    yield return CloseDialog();
                    EventSystem.current.SetSelectedGameObject(stepper.gameObject);
                    int beforeLeft = stepper.Index;
                    yield return Press(Key.LeftArrow);
                    int afterLeft = stepper.Index;
                    yield return CloseDialog();
                    Check($"{label}: the arrow keys step it", afterRight != before && afterLeft != beforeLeft,
                        $"right {before} -> {afterRight}, left {beforeLeft} -> {afterLeft}");
                    UiInput.ForceKeysOrPad(false);
                }
            }
            else if (control is UiSwatches swatches)
            {
                foreach (UiClickZone zone in swatches.GetComponentsInChildren<UiClickZone>(false))
                {
                    int before = swatches.Index;
                    yield return ClickThrough(zone.gameObject, $"{label}/{zone.name}");
                    if (_landed)
                        Note($"{label}/{zone.name}: picked {before} -> {swatches.Index}");
                    swatches.Pick(before);
                }
            }
            else
            {
                ProbeHit(control, control.gameObject, tab);
            }

            yield return null;
        }

        /// <summary>The top raycast hit at the target's centre must route a click to the target itself.</summary>
        static bool ProbeHit(Component target, GameObject expected, string tab)
        {
            GameObject handler = TopHandler(target.transform as RectTransform, out string top);
            bool ok = handler == expected;
            Check($"{tab}/{target.name}: a mouse click at its centre reaches it", ok, $"top hit {top}, handler {(handler != null ? handler.name : "none")}");
            return ok;
        }

        /// <summary>Press and release through the raycaster, like the input module does: down, a frame, up and click on the same handler.</summary>
        static bool _landed;

        static IEnumerator ClickThrough(GameObject zone, string label)
        {
            _landed = false;
            GameObject down = TopHandler((RectTransform)zone.transform, out string topDown);
            Check($"{label}: a mouse press lands on it", down == zone, $"top hit {topDown}");
            if (down != zone)
                yield break;
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = Centre((RectTransform)zone.transform) };
            ExecuteEvents.Execute(down, ped, ExecuteEvents.pointerDownHandler);
            if (_live)
                yield return 0.12f;   // a real click's hold: the press and focus motion run meanwhile
            else
                Settle(Step);
            GameObject up = TopHandler((RectTransform)zone.transform, out string topUp);
            Check($"{label}: still under the pointer on release", up == zone, $"top hit {topUp}");
            ExecuteEvents.Execute(down, ped, ExecuteEvents.pointerUpHandler);
            if (up == zone)
                ExecuteEvents.Execute(zone, ped, ExecuteEvents.pointerClickHandler);
            _landed = up == zone;
        }

        /// <summary>A resolution or display mode change asks "Keep this resolution?": answer Revert (Esc) and wait for it to close.</summary>
        static IEnumerator CloseDialog()
        {
            yield return 0.3f;
            if (_root.Stack.Top is UiConfirmDialog)
            {
                Note("a confirm dialog opened: answering Esc");
                yield return Press(Key.Escape);
                yield return 0.6f;
            }
        }

        static Vector2 Centre(RectTransform rect)
        {
            Vector3 world = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(_cam, world);
        }

        static GameObject TopHandler(RectTransform rect, out string top)
        {
            Canvas.ForceUpdateCanvases();
            var ped = new PointerEventData(EventSystem.current) { position = Centre(rect) };
            Hits.Clear();
            EventSystem.current.RaycastAll(ped, Hits);
            if (Hits.Count == 0)
            {
                top = "nothing";
                return null;
            }

            top = Hits[0].gameObject.name;
            for (int i = 1; i < Mathf.Min(4, Hits.Count); i++)
                top += " > " + Hits[i].gameObject.name;
            GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            GameObject down = ExecuteEvents.GetEventHandler<IPointerDownHandler>(Hits[0].gameObject);
            return click != null && (down == null || click.transform.IsChildOf(down.transform)) ? click : down;
        }

        /// <summary>The arrow's answers frame by frame at 4K: mouse over the right arrow, a click, a left key step.</summary>
        static IEnumerator ArrowFilm()
        {
            Tabs().Select(1, false);
            Settle(0.6f);
            UiStepper glass = Find<UiStepper>(Settings(), "UiGlass");
            if (glass == null)
            {
                Fail("the Menu glass stepper is on the video tab");
                yield break;
            }

            int start = glass.Index;
            var rect = (RectTransform)glass.transform;
            int frame = 0;
            Crop($"probe_arrow_{frame++:00}_rest", rect, 3840, 2160);
            glass.HoverZone(1, true);
            for (int i = 0; i < 6; i++)
            {
                Settle(Step);
                Crop($"probe_arrow_{frame++:00}_hover", rect, 3840, 2160);
            }

            glass.StepFromClick(1);
            for (int i = 0; i < 10; i++)
            {
                Settle(Step);
                Crop($"probe_arrow_{frame++:00}_click", rect, 3840, 2160);
            }

            glass.HoverZone(1, false);
            glass.Step(-1);
            for (int i = 0; i < 10; i++)
            {
                Settle(Step);
                Crop($"probe_arrow_{frame++:00}_key", rect, 3840, 2160);
            }

            Settle(0.5f);
            Check("the arrow film put Menu glass back", glass.Index == start, $"{start} -> {glass.Index}");
            yield return null;
        }

        // ---- Resolutions -----------------------------------------------------------------------------------

        static IEnumerator Resolutions(TitleScreen title)
        {
            UiInput.ForceKeysOrPad(true);
            Tabs().Select(1, false);   // video: steppers, toggles and the most text
            Settle(0.5f);
            UiStepper sample = Tabs().CurrentPage.GetComponentInChildren<UiStepper>(false);
            if (sample != null)
                EventSystem.current.SetSelectedGameObject(sample.gameObject);
            Settle(0.6f);
            foreach ((int w, int h) in ProbeSizes)
            {
                for (int i = 0; i < TabNames.Length; i++)
                {
                    Tabs().Select(i, false);
                    Settle(0.4f);
                    if (w == 1920 || w >= 3840 || i == 1)
                        Shot($"probe_settings_{TabNames[i]}_{w}x{h}", w, h);
                    CheckInside(Settings().transform, w, h, $"settings {TabNames[i]} at {w}x{h}");
                }
            }

            Tabs().Select(1, false);
            Settle(0.4f);
            if (sample != null)
                EventSystem.current.SetSelectedGameObject(sample.gameObject);
            Settle(0.6f);
            UiButton back = Find<UiButton>(Settings(), "Back");
            foreach (int w in new[] { 3840, 7680 })
            {
                int h = w * 9 / 16;
                if (sample != null)
                    Crop($"probe_crop_stepper_{w}", (RectTransform)sample.transform, w, h);
                if (back != null)
                    Crop($"probe_crop_button_{w}", (RectTransform)back.transform, w, h);
            }

            UiInput.ForceKeysOrPad(false);
            _root.Stack.Pop();
            Settle(1f);
            foreach ((int w, int h) in ProbeSizes)
            {
                if (w >= 3840 || w == 1024)
                    Shot($"probe_title_{w}x{h}", w, h);
                CheckInside(title.transform, w, h, $"title at {w}x{h}");
            }

            _root.Stack.Push(Settings());
            Settle(1f);
            yield return null;
        }

        /// <summary>The control and a margin around it at native pixels: what the player's screen really shows.</summary>
        static void Crop(string name, RectTransform rect, int w, int h)
        {
            Texture2D full = Grab(w, h);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(_cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(_cam, corners[2]);
            int margin = h / 40;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(a.x) - margin, 0, w - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(a.y) - margin, 0, h - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(b.x) + margin, x0 + 1, w), y1 = Mathf.Clamp(Mathf.CeilToInt(b.y) + margin, y0 + 1, h);
            var crop = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, y0, x1 - x0, y1 - y0));
            crop.Apply();
            File.WriteAllBytes(Path.Combine(_out, name + ".png"), crop.EncodeToPNG());
            Object.DestroyImmediate(crop);
            Object.DestroyImmediate(full);
            Note("wrote " + name + ".png");
        }
    }
}

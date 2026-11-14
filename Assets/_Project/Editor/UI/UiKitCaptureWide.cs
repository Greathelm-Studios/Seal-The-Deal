using System.Collections;
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Net;
using CloseTheDeal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// The wide pass's capture runs, one per slice (-uiCaptureMode wide1: the edge marker and device-aware key hints).
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
                    yield return EdgeMarkers();
                    yield return KeyHintDevices();
                    yield return WideClip1();
                    break;
                default:
                    yield return WideMore(mode, lobby);
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

        static IEnumerator WideMore(string mode, SteamLobby lobby)
        {
            Fail("no capture script for mode " + mode);
            yield break;
        }
    }
}

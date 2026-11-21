using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI.Title
{
    /// <summary>The screen-to-screen transitions the UI lane's screen stack can call.</summary>
    public enum UiTransitionKind
    {
        /// <summary>A stepped flat-cloud wipe sweeps across and away. For ordinary screen changes. 0.28 s.</summary>
        CloudWipe,
        /// <summary>Three white panes shut like a sash, then slide away the other way. For back and forward. 0.26 s.</summary>
        PaneSlide,
        /// <summary>The window frame closes in on the view (zoom-through): a game start. 0.85 s, the one long one.</summary>
        FrameZoom,
        /// <summary>A plain short white fade. Used for every kind when reduced motion is on. 0.12 s.</summary>
        Fade,
    }

    /// <summary>Screen-to-screen full-screen transitions (CloudWipe, PaneSlide, FrameZoom, Fade).</summary>
    public static class UiTransition
    {
        /// <summary>Shared accessibility switch with UiSettings.ReducedMotion.</summary>
        public static bool ReducedMotion
        {
            get => UiSettings.ReducedMotion;
            set => UiSettings.ReducedMotion = value;
        }

        /// <summary>Normalized 0..1 progress callback during FrameZoom for camera push-in.</summary>
        public static Action<float> ZoomProgress;

        public static bool IsPlaying => UiTransitionHost.Instance != null && UiTransitionHost.Instance.Playing;

        /// <summary>Seconds the player waits from start to start of the new screen's reveal ("covered") and in total.</summary>
        public static void Timing(UiTransitionKind kind, out float cover, out float reveal)
        {
            if (ReducedMotion) kind = UiTransitionKind.Fade;
            switch (kind)
            {
                case UiTransitionKind.CloudWipe: cover = 0.14f; reveal = 0.14f; break;
                case UiTransitionKind.PaneSlide: cover = 0.13f; reveal = 0.13f; break;
                case UiTransitionKind.FrameZoom: cover = 0.55f; reveal = 0.30f; break;
                default: cover = 0.06f; reveal = 0.06f; break;
            }
        }

        public static float Duration(UiTransitionKind kind) { Timing(kind, out float c, out float r); return c + r; }

        public static void Play(UiTransitionKind kind, Action onCovered = null, Action onDone = null)
        {
            if (!Application.isPlaying) { onCovered?.Invoke(); onDone?.Invoke(); return; }
            UiTransitionHost host = UiTransitionHost.Get();
            host.Run(ReducedMotion ? UiTransitionKind.Fade : kind, onCovered, onDone);
        }

        /// <summary>Draws one frame of a transition (t = 0..1 over its whole length) onto a camera. Capture tools only.</summary>
        public static void Preview(UiTransitionKind kind, float t, Camera camera)
        {
            UiTransitionHost.Get(camera).Show(kind, t);
        }

        public static void ClearPreview() => UiTransitionHost.Discard();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ZoomProgress = null;
            UiTransitionHost.ResetStatic();
        }
    }

    /// <summary>The overlay canvas that draws the transitions. Built in code on first use; nothing to place in a scene.</summary>
    public sealed class UiTransitionHost : MonoBehaviour
    {
        // Pass C palette: Paper, the milky cloud shade, the lifted haze, and Deep Navy for hairlines.
        static readonly Color Paper = new Color32(0xF3, 0xF4, 0xF4, 255);
        static readonly Color Milk = new Color32(0xE0, 0xEA, 0xFA, 255);
        static readonly Color Haze = new Color32(0xB9, 0xD2, 0xF8, 255);
        static readonly Color Navy = new Color32(0x11, 0x1B, 0x38, 255);

        const int Cols = 24, Rows = 14;
        public static UiTransitionHost Instance { get; private set; }
        public bool Playing { get; private set; }

        internal static void ResetStatic() => Instance = null;

        Canvas _canvas;
        Image[] _cells;
        float[] _threshold;
        bool[] _cellShown;
        Color[] _cellColor;
        Image[] _panes;
        Image[] _bars;      // frame: top, bottom, left, right
        Image[] _cross;     // mullion cross
        Image _fade;

        bool _preview;
        UiTransitionKind? _drawnKind;

        public static UiTransitionHost Get(Camera camera = null)
        {
            // A host made by an edit-mode capture is bound to that capture's camera: never play through it.
            if (Instance != null && Instance._preview && Application.isPlaying)
                Discard();
            if (Instance == null)
            {
                var go = new GameObject("UiTransitionHost") { hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.HideAndDontSave };
                if (Application.isPlaying) DontDestroyOnLoad(go);
                Instance = go.AddComponent<UiTransitionHost>();
                Instance._preview = !Application.isPlaying;
                Instance.Build();
            }
            if (camera != null)
            {
                Instance._canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Instance._canvas.worldCamera = camera;
                Instance._canvas.planeDistance = 1f;
            }
            return Instance;
        }

        public static void Discard()
        {
            if (Instance == null) return;
            if (Application.isPlaying) Destroy(Instance.gameObject); else DestroyImmediate(Instance.gameObject);
            Instance = null;
        }

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            RectTransform root = (RectTransform)transform;

            _cells = new Image[Cols * Rows];
            _threshold = new float[Cols * Rows];
            _cellShown = new bool[Cols * Rows];
            _cellColor = new Color[Cols * Rows];
            for (int j = 0; j < Rows; j++)
            {
                // A stepped cumulus profile: each row's front is pushed a few cells by a fixed hash, so the
                // leading edge is a cloud's outline, not a straight line.
                float push = 5f * Mathf.Sin(Mathf.PI * (j + 0.5f) / Rows) + (Hash(j * 7 + 3) % 3);
                for (int i = 0; i < Cols; i++)
                {
                    int k = j * Cols + i;
                    _threshold[k] = (i - push + 8f) / (Cols + 8f);
                    _cells[k] = Cell(root, "Cell", i / (float)Cols, j / (float)Rows, (i + 1) / (float)Cols, (j + 1) / (float)Rows, Paper);
                    _cellShown[k] = true;
                    _cellColor[k] = Paper;
                }
            }

            _panes = new Image[3];
            for (int p = 0; p < 3; p++)
            {
                _panes[p] = Cell(root, "Pane", p / 3f, 0f, (p + 1) / 3f, 1f, Paper);
                Image edge = Cell(_panes[p].rectTransform, "Edge", 0f, 0f, 0f, 1f, Navy);
                edge.rectTransform.sizeDelta = new Vector2(2f, 0f);
                edge.rectTransform.anchoredPosition = new Vector2(1f, 0f);
            }

            _bars = new Image[4];
            for (int b = 0; b < 4; b++) _bars[b] = Cell(root, "FrameBar", 0, 0, 1, 1, Paper);
            _cross = new Image[2];
            for (int c = 0; c < 2; c++) _cross[c] = Cell(root, "FrameCross", 0, 0, 1, 1, Navy);

            _fade = Cell(root, "Fade", 0f, 0f, 1f, 1f, Paper);
            HideAll();
        }

        static Image Cell(Transform parent, string name, float x0, float y0, float x1, float y1, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = true;   // a transition blocks clicks until it is gone
            return img;
        }

        static int Hash(int n) { n = (n << 13) ^ n; return Mathf.Abs(n * (n * n * 15731 + 789221) + 1376312589) >> 8; }

        void HideAll()
        {
            HideVisuals();
            _drawnKind = null;
            if (_canvas != null)
                _canvas.enabled = false;
        }

        void HideVisuals()
        {
            for (int k = 0; k < _cells.Length; k++)
            {
                if (_cellShown[k])
                {
                    _cells[k].enabled = false;
                    _cellShown[k] = false;
                }
            }
            foreach (Image i in _panes)
            {
                if (i.gameObject.activeSelf)
                    i.gameObject.SetActive(false);
            }
            foreach (Image i in _bars)
            {
                if (i.enabled)
                    i.enabled = false;
            }
            foreach (Image i in _cross)
            {
                if (i.enabled)
                    i.enabled = false;
            }
            if (_fade.enabled)
                _fade.enabled = false;
        }

        int _pending;
        EventSystem _blocked;
        bool _blockedWas;

        public void Run(UiTransitionKind kind, Action onCovered, Action onDone)
        {
            // Hold EventSystem navigation off until queued transitions finish so Confirm cannot fire twice under cover.
            if (_pending++ == 0 && EventSystem.current != null)
            {
                _blocked = EventSystem.current;
                _blockedWas = _blocked.sendNavigationEvents;
                _blocked.sendNavigationEvents = false;
            }
            StartCoroutine(Play(kind, onCovered, onDone));
        }

        void Unblock()
        {
            if (_blocked != null)
                _blocked.sendNavigationEvents = _blockedWas;
            _blocked = null;
        }

        void OnDestroy() => Unblock();

        IEnumerator Play(UiTransitionKind kind, Action onCovered, Action onDone)
        {
            while (Playing)
                yield return null;
            Playing = true;
            HideAll();
            _canvas.enabled = true;
            UiSound.Play(SoundFor(kind));   // each kind has its own sound; FrameZoom's is the game-start sting
            UiTransition.Timing(kind, out float cover, out float reveal);
            // Start at t=0 and cap step to 1/30 s so a slow click frame cannot skip the cover phase.
            for (float t = 0f; t < cover; t += Step())
            {
                Draw(kind, true, Mathf.Clamp01(t / cover));
                yield return null;
            }
            Draw(kind, true, 1f);
            onCovered?.Invoke();
            for (float t = 0f; t < reveal; t += Step())
            {
                Draw(kind, false, Mathf.Clamp01(t / reveal));
                yield return null;
            }
            HideAll();
            Playing = false;
            if (--_pending == 0)
                Unblock();
            onDone?.Invoke();
        }

        static UiSoundEvent SoundFor(UiTransitionKind kind)
        {
            switch (kind)
            {
                case UiTransitionKind.CloudWipe: return UiSoundEvent.TransitionCloudWipe;
                case UiTransitionKind.PaneSlide: return UiSoundEvent.TransitionPaneSlide;
                case UiTransitionKind.FrameZoom: return UiSoundEvent.TransitionFrameZoom;
                default: return UiSoundEvent.TransitionFade;
            }
        }

        /// <summary>Seconds to advance this frame: real time, but never more than a 30 fps frame.</summary>
        static float Step() => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        /// <summary>One frame at t = 0..1 over the whole transition (capture and previews).</summary>
        public void Show(UiTransitionKind kind, float t)
        {
            if (!_canvas.enabled)
                _canvas.enabled = true;
            UiTransition.Timing(kind, out float cover, out float reveal);
            float time = t * (cover + reveal);
            if (time < cover) Draw(kind, true, time / cover); else Draw(kind, false, Mathf.Clamp01((time - cover) / reveal));
        }

        // Ease-out going in (the player's input answers at once), ease-in-out going out.
        static float Out(float p) => 1f - Mathf.Pow(1f - p, 3f);
        static float InOut(float p) => p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;

        void Draw(UiTransitionKind kind, bool covering, float p)
        {
            if (!_canvas.enabled)
                _canvas.enabled = true;
            if (_drawnKind != kind)
            {
                HideVisuals();
                _drawnKind = kind;
            }

            switch (kind)
            {
                case UiTransitionKind.CloudWipe: DrawCloud(covering, covering ? Out(p) : InOut(p)); break;
                case UiTransitionKind.PaneSlide: DrawPanes(covering, covering ? Out(p) : InOut(p)); break;
                case UiTransitionKind.FrameZoom: DrawFrame(covering, p); break;
                default:
                    if (!_fade.enabled)
                        _fade.enabled = true;
                    _fade.color = new Color(Paper.r, Paper.g, Paper.b, covering ? p : 1f - p);
                    break;
            }
        }

        void DrawCloud(bool covering, float e)
        {
            float front = e * 1.12f;           // the front runs past the last cell so the cover is complete at e = 1
            for (int k = 0; k < _cells.Length; k++)
            {
                float th = _threshold[k];
                bool shown = covering ? front > th : front <= th;
                if (!shown)
                {
                    if (_cellShown[k])
                    {
                        _cells[k].enabled = false;
                        _cellShown[k] = false;
                    }
                    continue;
                }

                if (!_cellShown[k])
                {
                    _cells[k].enabled = true;
                    _cellShown[k] = true;
                }

                float edge = covering ? front - th : th - front;     // 0 at the leading edge
                int row = k / Cols;
                Color c = edge < 0.06f ? Haze : (edge < 0.14f || row < 2) ? Milk : Paper;
                if (_cellColor[k] != c)
                {
                    _cellColor[k] = c;
                    _cells[k].color = c;
                }
            }
        }

        void DrawPanes(bool covering, float e)
        {
            for (int p = 0; p < 3; p++)
            {
                if (!_panes[p].gameObject.activeSelf)
                    _panes[p].gameObject.SetActive(true);
                // Shut: panes drop in from above and below alternately. Open: each leaves the other way.
                float dir = p % 2 == 0 ? 1f : -1f;
                float offset = covering ? (1f - e) * dir : -e * dir;
                var rt = _panes[p].rectTransform;
                rt.anchorMin = new Vector2(p / 3f, offset);
                rt.anchorMax = new Vector2((p + 1) / 3f, 1f + offset);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        void DrawFrame(bool covering, float p)
        {
            if (covering)
            {
                if (_fade.enabled)
                    _fade.enabled = false;
                // Moves on the first frame (the press answers at once), then accelerates over the camera.
                float e = 0.18f * p + 0.82f * p * p * p;
                // The hole's half-size, as the same fraction of width and height: it keeps the screen's own shape.
                float h = Mathf.Lerp(0.62f, 0f, e);
                const float cx = 0.5f, cy = 0.5f;
                Bar(_bars[0], 0f, cy + h, 1f, 1f);
                Bar(_bars[1], 0f, 0f, 1f, cy - h);
                Bar(_bars[2], 0f, cy - h, cx - h, cy + h);
                Bar(_bars[3], cx + h, cy - h, 1f, cy + h);
                if (h < 0.18f)
                {
                    float c = 1f - h / 0.18f;
                    Cross(_cross[0], cx - 0.0015f, cy - h, cx + 0.0015f, cy + h, c);
                    Cross(_cross[1], cx - h, cy - 0.0025f, cx + h, cy + 0.0025f, c);
                }
                else
                {
                    if (_cross[0].enabled) _cross[0].enabled = false;
                    if (_cross[1].enabled) _cross[1].enabled = false;
                }
                UiTransition.ZoomProgress?.Invoke(p);
            }
            else
            {
                for (int b = 0; b < _bars.Length; b++)
                {
                    if (_bars[b].enabled)
                        _bars[b].enabled = false;
                }
                for (int c = 0; c < _cross.Length; c++)
                {
                    if (_cross[c].enabled)
                        _cross[c].enabled = false;
                }
                if (!_fade.enabled)
                    _fade.enabled = true;
                _fade.color = new Color(Paper.r, Paper.g, Paper.b, 1f - InOut(p));
                UiTransition.ZoomProgress?.Invoke(1f);
            }
        }

        static void Bar(Image i, float x0, float y0, float x1, float y1)
        {
            i.enabled = x1 > x0 && y1 > y0;
            var rt = i.rectTransform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Cross(Image i, float x0, float y0, float x1, float y1, float alpha)
        {
            Bar(i, x0, y0, x1, y1);
            i.color = new Color(Navy.r, Navy.g, Navy.b, alpha * 0.6f);
        }
    }
}

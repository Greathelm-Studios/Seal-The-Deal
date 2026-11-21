using CloseTheDeal.Nameplates;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Whose thing a marker points at: the shape and colour say it, never colour alone.</summary>
    public enum UiMarkKind
    {
        /// <summary>Your firm: a cyan diamond.</summary>
        Ours = 0,
        /// <summary>The rival firm: an amber triangle.</summary>
        Rival = 1,
        /// <summary>Nobody's (a loose briefcase): a paper square.</summary>
        Loose = 2
    }

    /// <summary>World-space target marker that tracks in-view or clamps to screen edges when off-screen.</summary>
    [AddComponentMenu("Close the Deal/UI/Edge Marker")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiEdgeMarker : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's tokens: colours and fade timing.")]
        [SerializeField] UiTokens _tokens;

        [Header("Parts")]
        [Tooltip("The part that moves: the mark, its chevron and its distance tag. Anchored at the bottom-left corner.")]
        [SerializeField] RectTransform _body;
        [Tooltip("The coloured mark.")]
        [SerializeField] Image _shape;
        [Tooltip("The navy keyline behind the mark (the same shape, a little larger), so it reads over bright and dark scenery alike.")]
        [SerializeField] Image _keyline;
        [Tooltip("The chevron that points toward the target while the mark waits at the edge. Drawn pointing right.")]
        [SerializeField] RectTransform _pointer;
        [Tooltip("The chevron's image, tinted like the mark.")]
        [SerializeField] Image _pointerImage;
        [Tooltip("Optional second chevron. Together the arrows frame the target in view. At the screen edge only the direction arrow shows.")]
        [SerializeField] Image _oppositeChevron;
        [Tooltip("The distance, e.g. '24 m'.")]
        [SerializeField] TMP_Text _distance;
        [Tooltip("The distance tag's plate, hidden when no distance is given. It sits on the side away from the chevron.")]
        [SerializeField] RectTransform _tag;

        [Header("Shapes")]
        [Tooltip("Your firm's mark.")]
        [SerializeField] Sprite _diamond;
        [Tooltip("The rival firm's mark.")]
        [SerializeField] Sprite _triangle;
        [Tooltip("A loose thing's mark.")]
        [SerializeField] Sprite _square;

        [Header("Layout")]
        [Tooltip("How far in from the screen edge the mark waits, in pixels at 1920 x 1080. 48 keeps it clear of TV overscan and the corners' HUD.")]
        [Min(0f)] [SerializeField] float _safeEdge = 48f;
        [Tooltip("How far the chevron sits from the mark's centre, in pixels at 1920 x 1080.")]
        [Min(0f)] [SerializeField] float _pointerDistance = 30f;
        [Tooltip("The gap from the mark's centre to the near edge of the distance tag, in pixels at 1920 x 1080.")]
        [Min(0f)] [SerializeField] float _tagDistance = 22f;
        [Tooltip("On: while the target is in view the mark sits over it. Off: the mark shows only at the edge, when the target is out of view.")]
        [SerializeField] bool _showInView = true;

        CanvasGroup _group;
        RectTransform _area;
        Canvas _canvas;
        UiTween _fade;
        UiTween _flash;
        bool _visible;
        bool _onEdge;
        Vector2 _edgeDir = Vector2.down;
        UiTween _switch;
        float _switchFrom, _switchTo;
        int _shownMetres = int.MinValue;
        UiMarkKind _kind = (UiMarkKind)(-1);

        /// <summary>True while the mark waits at the screen edge (the target is out of view or behind you).</summary>
        public bool OnEdge => _onEdge;

        /// <summary>True while the mark is showing or fading in.</summary>
        public bool Visible => _visible;

        /// <summary>Where the mark is now, in the marker's canvas units from its bottom-left corner.</summary>
        public Vector2 MarkPosition => _body != null ? _body.anchoredPosition : Vector2.zero;

        /// <summary>Whose thing this points at. Changing it swaps the shape and colour at once.</summary>
        public UiMarkKind Kind
        {
            get => _kind;
            set
            {
                if (value == _kind)
                    return;
                _kind = value;
                ApplyKind();
            }
        }

        void Awake() => EnsureReady();

        void EnsureReady()
        {
            if (_group != null)
                return;
            UiMotion.EnsureSubCanvas(gameObject);
            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _area = (RectTransform)transform;
            _fade.Snap(0f);
            _flash.Snap(0f);
            _switch.Snap(1f);
            _group.alpha = 0f;
            if (_kind < 0)
                Kind = UiMarkKind.Ours;
        }

        void OnEnable()
        {
            if (_group == null)
                return;
            _fade.Snap(_visible ? 1f : 0f);
            _flash.Snap(0f);
            Tick(0f);
        }

        void OnDisable() => UiMotion.Sleep(this);

        /// <summary>Places the marker from WorldToCanvas.TryProject output; negative metres hides distance tag.</summary>
        public void Place(Vector2 canvasPoint, bool inFront, float metres)
        {
            EnsureReady();
            if (_body == null)
                return;
            Vector2 size = _area.rect.size;
            float edge = Mathf.Min(_safeEdge, size.x * 0.25f, size.y * 0.25f);
            Vector2 min = new(edge, edge);
            Vector2 max = size - min;
            bool inside = canvasPoint.x >= min.x && canvasPoint.x <= max.x && canvasPoint.y >= min.y && canvasPoint.y <= max.y;
            bool inView = inFront && inside;

            Vector2 centre = size * 0.5f;
            Vector2 half = max - centre;
            Vector2 dir = inView ? Vector2.up : Direction(canvasPoint, centre);
            bool wasOnEdge = _onEdge;
            _onEdge = !inView;
            if (_onEdge && wasOnEdge && _visible && !inFront)
                dir = SideSwitch(dir); // it crossed behind you: slide round the bottom, the way you'd turn
            else
                _switch.Snap(1f);
            _edgeDir = dir;
            Vector2 at = inView ? canvasPoint : EdgePoint(centre + dir * 100f, centre, half);
            _body.anchoredPosition = at;
            SetVisible(!inView || _showInView);
            PlaceCues(dir, inView);
            ShowDistance(metres);
        }

        void PlaceCues(Vector2 dir, bool inView)
        {
            if (_pointer != null)
            {
                bool showPointer = !inView || _oppositeChevron != null;
                if (_pointer.gameObject.activeSelf != showPointer)
                    _pointer.gameObject.SetActive(showPointer);
                if (_oppositeChevron != null && _oppositeChevron.gameObject.activeSelf != inView)
                    _oppositeChevron.gameObject.SetActive(inView);
                if (inView && _oppositeChevron != null)
                    _oppositeChevron.rectTransform.anchoredPosition = Vector2.right * _pointerDistance;
                if (showPointer)
                {
                    _pointer.anchoredPosition = (inView ? Vector2.left : dir) * _pointerDistance;
                    _pointer.localRotation = Quaternion.Euler(0f, 0f, inView ? 0f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                }
            }

            if (_tag != null)
            {
                Vector2 half = _tag.rect.size * 0.5f;
                _tag.anchoredPosition = -dir * (_tagDistance + Mathf.Abs(dir.x) * half.x + Mathf.Abs(dir.y) * half.y);
            }
        }

        /// <summary>Projects worldPoint for the owning HUD's LateUpdate and places the marker.</summary>
        public void Track(Camera camera, Vector3 worldPoint, bool showDistance = true)
        {
            if (camera == null)
            {
                Hide();
                return;
            }

            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            Canvas root = _canvas != null ? _canvas.rootCanvas : null;
            bool inFront = WorldToCanvas.TryProject(camera, root, worldPoint, out Vector2 at);
            float metres = showDistance ? Vector3.Distance(camera.transform.position, worldPoint) : -1f;
            Place(at, inFront, metres);
        }

        /// <summary>Hides the mark (nothing to point at), fading it out.</summary>
        public void Hide()
        {
            EnsureReady();
            SetVisible(false);
        }

        /// <summary>Plays a single attention flash (scale pop, or opacity dip under Reduce motion).</summary>
        public void Flash()
        {
            EnsureReady();
            if (_tokens == null)
                return;
            _flash.Start(1f, 0f, _tokens.PanelIn + _tokens.ConfirmTick, _tokens.EaseOut);
            UiMotion.Wake(this);
        }

        Vector2 SideSwitch(Vector2 dir)
        {
            float to = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (_switch.Running)
            {
                _switchTo = to;
                return AngleDir(SwitchAngle(_switch.Value));
            }

            float from = Mathf.Atan2(_edgeDir.y, _edgeDir.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(from, to)) < 100f || UiMotion.Reduced || _tokens == null)
                return dir;
            _switchFrom = from;
            _switchTo = to;
            _switch.Start(0f, 1f, SwitchSeconds, _tokens.EaseInOut);
            UiMotion.Wake(this);
            return AngleDir(from);
        }

        const float SwitchSeconds = 0.14f;

        /// <summary>From the old side down through straight below you (-90°) to the new side.</summary>
        float SwitchAngle(float t)
        {
            if (t < 0.5f)
                return Mathf.LerpAngle(_switchFrom, -90f, t * 2f);
            return Mathf.LerpAngle(-90f, _switchTo, (t - 0.5f) * 2f);
        }

        static Vector2 AngleDir(float degrees) =>
            new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        static Vector2 EdgePoint(Vector2 point, Vector2 centre, Vector2 half)
        {
            Vector2 dir = Direction(point, centre);
            float tx = Mathf.Abs(dir.x) > 1e-5f ? half.x / Mathf.Abs(dir.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(dir.y) > 1e-5f ? half.y / Mathf.Abs(dir.y) : float.PositiveInfinity;
            return centre + dir * Mathf.Min(tx, ty);
        }

        static Vector2 Direction(Vector2 point, Vector2 centre)
        {
            Vector2 d = point - centre;
            return d.sqrMagnitude < 1f ? Vector2.down : d.normalized;
        }

        void ShowDistance(float metres)
        {
            int whole = metres < 0f ? -1 : Mathf.RoundToInt(metres);
            if (whole == _shownMetres)
                return;
            _shownMetres = whole;
            if (_tag != null)
                _tag.gameObject.SetActive(whole >= 0);
            if (_distance != null && whole >= 0)
                _distance.SetText("{0} m", whole);
        }

        void SetVisible(bool visible)
        {
            if (visible == _visible || _tokens == null)
                return;
            _visible = visible;
            float duration = visible ? _tokens.ConfirmTick : _tokens.HoverIn * 0.8f;
            _fade.Start(_fade.Value, visible ? 1f : 0f, duration, visible ? _tokens.EaseOut : _tokens.EaseIn);
            UiMotion.Wake(this);
        }

        void ApplyKind()
        {
            if (_tokens == null)
                return;
            Color colour = _kind == UiMarkKind.Rival ? _tokens.Amber : _kind == UiMarkKind.Loose ? _tokens.Paper : _tokens.Cyan;
            Sprite sprite = _kind == UiMarkKind.Rival ? _triangle : _kind == UiMarkKind.Loose ? _square : _diamond;
            Paint(_shape, sprite, colour);
            Paint(_keyline, sprite, _tokens.Navy);
            // Scale square down so all three marker shapes read at equal visual weight.
            float scale = _kind == UiMarkKind.Loose ? 0.72f : 1f;
            if (_shape != null)
                _shape.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (_keyline != null)
                _keyline.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (_oppositeChevron != null)
                _oppositeChevron.color = _kind == UiMarkKind.Loose ? _tokens.Navy : colour;
            Paint(_pointerImage, _pointerImage != null ? _pointerImage.sprite : null, _kind == UiMarkKind.Loose ? _tokens.Navy : colour);
        }

        static void Paint(Image image, Sprite sprite, Color colour)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.color = colour;
        }

        public bool Tick(float dt)
        {
            _fade.Step(dt);
            _flash.Step(dt);
            _switch.Step(dt);
            _group.alpha = _fade.Value;
            float f = _flash.Value;
            bool reduced = UiMotion.Reduced;
            if (_body != null)
            {
                float scale = reduced ? 1f : 1f + 0.25f * f;
                _body.localScale = new Vector3(scale, scale, 1f);
            }

            if (reduced && f > 0f)
                _group.alpha = _fade.Value * (1f - 0.5f * f); // a dip instead of a pop

            return _fade.Running || _flash.Running || _switch.Running;
        }
    }
}

using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Records;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Nameplates
{
    /// <summary>Local overlay rendering teammate nameplates and timed raider markers. Design: docs/systems/NAMEPLATES.md.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class Nameplates : MonoBehaviour
    {
        [Header("Look (the UI kit's)")]
        [Tooltip("The kit's tokens: colours and the Body type role.")]
        [SerializeField] UiTokens _tokens;

        [Tooltip("The kit's Plate prefab, used as each name's background.")]
        [SerializeField] GameObject _platePrefab;

        [Tooltip("The kit's diamond sprite: the own-firm mark before a name.")]
        [SerializeField] Sprite _diamondSprite;

        [Tooltip("The kit's triangle sprite: the rival marker, amber with a navy edge so it reads on white walls and on sky.")]
        [SerializeField] Sprite _markerSprite;

        [Header("Rules")]
        [Tooltip("How far away a teammate's name still shows, in metres. The research note says 40: about the width of the gap plus a floor.")]
        [Range(5f, 120f)] [SerializeField] float _range = 40f;

        [Tooltip("How far above the top of the head the name sits, in metres.")]
        [Range(0f, 1.5f)] [SerializeField] float _headroom = 0.35f;

        [Tooltip("How long a rival who walks into your tower is marked, in seconds, counted from when they enter.")]
        [Range(0f, 10f)] [SerializeField] float _raiderSeconds = 2f;

        [Tooltip("Text size of a name, in pixels at 1920 x 1080. The research note's floor is 18.")]
        [Range(14f, 32f)] [SerializeField] float _nameSize = 18f;

        sealed class Tag
        {
            public FirmMember Member;
            public Health Health;
            public RectTransform Chip;
            public CanvasGroup ChipGroup;
            public TMP_Text Name;
            public RectTransform Marker;
            public CanvasGroup MarkerGroup;
            public string ShownName;
            public bool InMyTower;
            public float MarkerUntil;
        }

        const float Padding = 10f;
        const float DiamondSize = 10f;
        const float ChipHeight = 30f;
        const float MarkerSize = 28f;
        const float MarkerEdge = 3f;

        readonly List<Tag> _tags = new();
        Canvas _canvas;
        Camera _camera;
        LobbyRoster _roster;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        void OnEnable()
        {
            FirmMember.OnRosterChanged += Rebuild;
            FirmMember.OnLocalChanged += Rebuild;
            Rebuild();
        }

        void OnDisable()
        {
            FirmMember.OnRosterChanged -= Rebuild;
            FirmMember.OnLocalChanged -= Rebuild;
            if (_roster != null)
                _roster.OnChanged -= RefreshNames;
            _roster = null;
        }

        /// <summary>Whether this player's name is fully shown on this screen now. For checks and other UI.</summary>
        public bool ShowsName(FirmMember member)
        {
            Tag tag = Find(member);
            return tag != null && tag.ChipGroup.alpha > 0.99f;
        }

        /// <summary>Whether this rival is marked as being inside your tower on this screen now.</summary>
        public bool ShowsMarker(FirmMember member)
        {
            Tag tag = Find(member);
            return tag != null && tag.MarkerGroup.alpha > 0.99f;
        }

        void Rebuild()
        {
            IReadOnlyList<FirmMember> members = FirmMember.All;
            for (int i = _tags.Count - 1; i >= 0; i--)
            {
                // Local player may appear in FirmMember.All before FirmMember.Local resolves.
                if (_tags[i].Member == null || _tags[i].Member == FirmMember.Local || !Contains(members, _tags[i].Member))
                    Remove(i);
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != FirmMember.Local && Find(members[i]) == null)
                    _tags.Add(Make(members[i]));
            }

            RefreshNames();
        }

        static bool Contains(IReadOnlyList<FirmMember> members, FirmMember member)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == member)
                    return true;
            }

            return false;
        }

        Tag Find(FirmMember member)
        {
            for (int i = 0; i < _tags.Count; i++)
            {
                if (_tags[i].Member == member)
                    return _tags[i];
            }

            return null;
        }

        void Remove(int index)
        {
            Tag tag = _tags[index];
            if (tag.Chip != null)
                Destroy(tag.Chip.gameObject);
            if (tag.Marker != null)
                Destroy(tag.Marker.gameObject);
            _tags.RemoveAt(index);
        }

        void RefreshNames()
        {
            if (_roster == null && LobbyRoster.Instance != null)
            {
                _roster = LobbyRoster.Instance;
                _roster.OnChanged += RefreshNames;
            }

            for (int i = 0; i < _tags.Count; i++)
                SetName(_tags[i]);
        }

        void SetName(Tag tag)
        {
            if (tag.Member == null || tag.Member.Owner == null)
                return;
            string name = MatchRecords.NameOf(tag.Member.Owner.ClientId);
            if (name == tag.ShownName)
                return;

            tag.ShownName = name;
            tag.Name.text = name;
            float width = Padding + DiamondSize + Padding * 0.6f + tag.Name.preferredWidth + Padding;
            tag.Chip.sizeDelta = new Vector2(width, ChipHeight);
        }

        Tag Make(FirmMember member)
        {
            var tag = new Tag { Member = member, Health = member.GetComponent<Health>() };
            tag.Chip = MakeChip(member.name, out tag.Name);
            tag.ChipGroup = tag.Chip.gameObject.AddComponent<CanvasGroup>();
            tag.ChipGroup.alpha = 0f;
            tag.Chip.gameObject.SetActive(false);
            tag.Marker = MakeMarker();
            tag.MarkerGroup = tag.Marker.gameObject.AddComponent<CanvasGroup>();
            tag.MarkerGroup.alpha = 0f;
            tag.Marker.gameObject.SetActive(false);
            return tag;
        }

        RectTransform MakeChip(string label, out TMP_Text text)
        {
            RectTransform chip = Node("Name · " + label, transform);
            chip.pivot = new Vector2(0.5f, 0f);
            if (_platePrefab != null)
                Stretch((RectTransform)Instantiate(_platePrefab, chip, false).transform);

            RectTransform diamond = Node("Diamond", chip);
            diamond.anchorMin = diamond.anchorMax = new Vector2(0f, 0.5f);
            diamond.anchoredPosition = new Vector2(Padding + DiamondSize * 0.5f, 0f);
            diamond.sizeDelta = new Vector2(DiamondSize, DiamondSize);
            Image glyph = diamond.gameObject.AddComponent<Image>();
            glyph.sprite = _diamondSprite;
            glyph.color = _tokens != null ? _tokens.Cyan : Color.cyan;
            glyph.raycastTarget = false;

            RectTransform nameRect = Node("Text", chip);
            Stretch(nameRect);
            nameRect.offsetMin = new Vector2(Padding + DiamondSize + Padding * 0.6f, 0f);
            nameRect.offsetMax = new Vector2(-Padding, 0f);
            text = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
            UiTypeRole role = _tokens != null ? _tokens.Body : default;
            if (role.Font != null)
                text.font = role.Font;
            text.fontSize = _nameSize;
            text.characterSpacing = role.Tracking;
            text.color = _tokens != null ? _tokens.Navy : Color.black;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return chip;
        }

        RectTransform MakeMarker()
        {
            RectTransform marker = Node("Rival marker", transform);
            marker.pivot = new Vector2(0.5f, 0f);
            marker.sizeDelta = new Vector2(MarkerSize + MarkerEdge * 2f, MarkerSize + MarkerEdge * 2f);
            Image edge = marker.gameObject.AddComponent<Image>();
            edge.sprite = _markerSprite;
            edge.color = _tokens != null ? _tokens.Navy : Color.black;
            edge.raycastTarget = false;

            RectTransform fill = Node("Fill", marker);
            fill.anchorMin = fill.anchorMax = new Vector2(0.5f, 0.5f);
            fill.sizeDelta = new Vector2(MarkerSize - MarkerEdge, MarkerSize - MarkerEdge);
            fill.anchoredPosition = new Vector2(0f, -MarkerEdge * 0.25f);
            Image image = fill.gameObject.AddComponent<Image>();
            image.sprite = _markerSprite;
            image.color = _tokens != null ? _tokens.Amber : new Color(1f, 0.62f, 0.1f);
            image.raycastTarget = false;
            return marker;
        }

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void LateUpdate()
        {
            FirmMember me = FirmMember.Local;
            Camera cam = ResolveCamera();
            MatchDirector match = MatchDirector.Instance;
            bool running = match != null && match.Phase == MatchPhase.Running && match.Arena != null;
            for (int i = 0; i < _tags.Count; i++)
            {
                if (me == null || cam == null || _tags[i].Member == null)
                    Hide(_tags[i]);
                else
                    Place(_tags[i], me, cam, running ? match : null);
            }
        }

        void Hide(Tag tag)
        {
            Fade(tag.ChipGroup, false);
            Fade(tag.MarkerGroup, false);
        }

        void Place(Tag tag, FirmMember me, Camera cam, MatchDirector runningMatch)
        {
            bool team = tag.Member.Firm == me.Firm;
            bool marked = !team && runningMatch != null && RaiderMarked(tag, me, runningMatch);
            Vector2 at = default;
            bool onScreen = (team || marked) && Alive(tag) && TryPlace(tag, cam, out at);
            Fade(tag.ChipGroup, onScreen && team);
            Fade(tag.MarkerGroup, onScreen && marked);
            if (onScreen && team)
                tag.Chip.anchoredPosition = at;
            else if (onScreen && marked)
                tag.Marker.anchoredPosition = at;
        }

        Camera ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            return _camera;
        }

        static bool Alive(Tag tag) => tag.Health == null || !tag.Health.IsDead;

        bool TryPlace(Tag tag, Camera cam, out Vector2 at)
        {
            Vector3 head = tag.Member.Position + Vector3.up * (tag.Member.BodyHeight + _headroom);
            at = default;
            if ((head - cam.transform.position).sqrMagnitude > _range * _range)
                return false;
            return WorldToCanvas.TryProject(cam, _canvas, head, out at);
        }

        bool RaiderMarked(Tag tag, FirmMember me, MatchDirector match)
        {
            bool inside = match.Arena.PlaceOf(tag.Member.Position, 0f) == me.Firm;
            if (inside && !tag.InMyTower)
                tag.MarkerUntil = Time.time + _raiderSeconds;
            tag.InMyTower = inside;
            return inside && Time.time < tag.MarkerUntil;
        }

        void Fade(CanvasGroup group, bool show)
        {
            GameObject go = group.gameObject;
            if (show && !go.activeSelf)
                go.SetActive(true);
            float target = show ? 1f : 0f;
            if (!Mathf.Approximately(group.alpha, target))
            {
                float seconds = _tokens != null ? (show ? _tokens.PanelIn : _tokens.PanelOut) : 0.15f;
                group.alpha = UiMotion.Reduced || seconds <= 0f ? target : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / seconds);
            }
            if (!show && group.alpha <= 0f && go.activeSelf)
                go.SetActive(false);
        }
    }
}

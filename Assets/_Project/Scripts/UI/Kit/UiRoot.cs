using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The UI kit's canvas in a scene. Shows the title screen whenever the game menu is at the title (not hosting,
    /// joining or playing) and closes its screens when the game takes over; the game menu keeps owning the rule
    /// menu open = mouse free = player idle. Also reads the device the player is on each frame (before the
    /// EventSystem runs) and passes Esc / the pad's B to the top screen. Built by Close the Deal > Lanes > Set Up UI.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Close the Deal/UI/UI Root")]
    public sealed class UiRoot : MonoBehaviour
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The game menu this kit takes the title state from.")]
        [SerializeField] GameMenu _menu;
        [Tooltip("The kit's screen stack.")]
        [SerializeField] UiScreenStack _stack;
        [Tooltip("The first screen: the title and main menu.")]
        [SerializeField] TitleScreen _title;

        bool _repeatApplied;
        bool _started;

        void Awake()
        {
            // The kit canvas is a prefab: when it is regenerated its link to the scene's game menu can drop, so find it once.
            if (_menu == null)
                _menu = FindAnyObjectByType<GameMenu>();
            if (_title != null)
                _title.Bind(_menu);
        }

        public UiScreenStack Stack => _stack;

        public TitleScreen Title => _title;

        void OnEnable()
        {
            if (_menu == null)
            {
                Debug.LogError("[UI] The UI root has no game menu, so it can't tell when to show the title. Run Close the Deal > Lanes > Set Up UI.", this);
                return;
            }

            _menu.StateChanged += Sync;
            _menu.HandTitleToKit(true);
            if (_started)
                Sync();
        }

        /// <summary>
        /// The first screen opens here, not in OnEnable: this runs early (order -200), before the EventSystem and the
        /// focus marks are enabled, and the title needs both to take focus and draw its marks on top.
        /// </summary>
        void Start()
        {
            _started = true;
            if (_menu != null)
                Sync();
        }

        void OnDisable()
        {
            if (_menu == null)
                return;
            _menu.StateChanged -= Sync;
            _menu.HandTitleToKit(false);
        }

        void Update()
        {
            UiInput.Poll();
            if (!_repeatApplied && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentInputModule != null)
            {
                UiInput.ApplyRepeat(_tokens);
                _repeatApplied = true;
            }

            if (_stack.Count > 0 && UiInput.CancelPressed)
                _stack.Cancel();
        }

        void Sync()
        {
            bool atTitle = _menu.AtTitle;
            if (atTitle && _stack.Count == 0)
                _stack.Push(_title);
            else if (!atTitle && _stack.Count > 0)
                _stack.Clear();
        }
    }
}

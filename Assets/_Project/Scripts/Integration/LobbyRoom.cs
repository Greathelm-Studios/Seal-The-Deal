using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>Marks the shared reception room as neutral ground and gates lobby interaction outside it. Design: docs/systems/INTEGRATION.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRoom : MonoBehaviour
    {
        [Tooltip("The middle of the room, in metres from this object, at floor level. Set by the integration set-up tool.")]
        [SerializeField] Vector3 _centre = new(12f, 0f, -8f);

        [Tooltip("The room's inside size in metres: width, height above the floor, depth. Anyone inside it is in the lobby.")]
        [SerializeField] Vector3 _size = new(24f, 4.5f, 16f);

        public static LobbyRoom Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        MatchDirector _match;
        bool _blockingLobby;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _match = MatchDirector.Instance;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            SetLobbyBlocked(false);
        }

        /// <summary>True when the point is inside the room, between its floor and its ceiling.</summary>
        public bool Contains(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world) - _centre;
            Vector3 half = _size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && local.y >= -0.5f && local.y <= _size.y && Mathf.Abs(local.z) <= half.z;
        }


        void Update()
        {
            PlayerMotor local = PlayerMotor.Local;
            SetLobbyBlocked(local == null || !Contains(local.transform.position));
        }

        void SetLobbyBlocked(bool blocked)
        {
            LobbyInteractor lobby = LobbyInteractor.Instance;
            if (blocked == _blockingLobby || lobby == null)
                return;

            _blockingLobby = blocked;
            if (blocked)
                lobby.Block();
            else
                lobby.Unblock();
        }
    }
}

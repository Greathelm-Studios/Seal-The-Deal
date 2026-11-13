using System;
using System.Collections.Generic;
using CloseTheDeal.Player;
using CloseTheDeal.Tower;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// A player's place in a firm. The host decides the firm when the player spawns and it is
    /// synced to everyone, late joiners included. On the owning client it also reads the two
    /// match keys (pick up, drop) and asks the host to act; the host decides every outcome.
    /// Lives on Player.prefab. Design and ownership: docs/systems/MATCH.md.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class FirmMember : NetworkBehaviour
    {
        /// <summary>Lane-local bindings, defined in code until the movement lane folds them into the shared actions asset. E is lean-right, so interact is F.</summary>
        public const string InteractKey = "<Keyboard>/f";
        public const string InteractPad = "<Gamepad>/buttonNorth";
        public const string DropKey = "<Keyboard>/g";
        public const string DropPad = "<Gamepad>/dpad/down";

        /// <summary>What to print on a prompt for each key.</summary>
        public const string InteractKeyLabel = "F";
        public const string DropKeyLabel = "G";

        /// <summary>Every player on this machine, whoever owns them.</summary>
        public static IReadOnlyList<FirmMember> All => Members;

        /// <summary>The player this machine controls. Null with no player.</summary>
        public static FirmMember Local { get; private set; }

        /// <summary>The local player appeared, left, or learned their firm.</summary>
        public static event Action OnLocalChanged;

        /// <summary>Someone joined, left or learned their firm: head counts per firm may differ.</summary>
        public static event Action OnRosterChanged;

        /// <summary>A line for the local player only (why the roof did nothing, and so on).</summary>
        public static event Action<MatchNotice> OnNotice;

        /// <summary>What the local player's interact or drop key would do now. Raised only when it changes.</summary>
        public static event Action<MatchPrompt> OnPromptChanged;

        /// <summary>A player fell out of the buildings and was put back. Raised on every machine, for sound and effects.</summary>
        public static event Action<FirmMember> OnFellOut;

        [Tooltip("Where a carried briefcase hangs on this player, as other players see it. Built by the lane setup tool under the body's lean pivot; leave empty and the briefcase hangs at the player's feet.")]
        [SerializeField] Transform _carryAnchor;

        static readonly List<FirmMember> Members = new();
        static MatchPrompt _shownPrompt;

        readonly SyncVar<byte> _firm = new();
        readonly SyncVar<byte> _slot = new();

        PlayerMotor _motor;
        Rigidbody _rigidbody;
        CapsuleCollider _capsule;
        InputAction _interact;
        InputAction _drop;
        byte _assignedFirm;
        byte _assignedSlot;

        /// <summary>Host only: ticks this player has been outside the buildings in a row.</summary>
        internal int OutsideTicks;

        /// <summary>Host only: was at a closed floor gate last tick, so the refusal is said once per approach.</summary>
        internal bool WasAtGate;

        /// <summary>Host only: has been told this match why a gate is shut, so later refusals use the short line.</summary>
        internal bool GateExplained;

        /// <summary>Host only: was on a roof last tick, so the "needs the briefcase" line is said once per visit.</summary>
        internal bool WasOnRoof;

        /// <summary>Which firm this player belongs to: the index of the building they climb.</summary>
        public int Firm => _firm.Value;

        /// <summary>This player's place within their firm, 0 to 3. Picks their spawn point.</summary>
        public int Slot => _slot.Value;

        public PlayerMotor Motor => _motor;

        /// <summary>The body's collider: what the floor gates let through when this player carries the briefcase.</summary>
        public Collider Body => _capsule;
        public Vector3 Position => _rigidbody.position;
        public Vector3 Velocity => _rigidbody.linearVelocity;
        public Vector3 Chest => _rigidbody.position + Vector3.up * (_capsule.height * 0.5f);
        public Vector3 Forward => _rigidbody.rotation * Vector3.forward;
        public float BodyHeight => _capsule.height;
        public float BodyRadius => _capsule.radius;
        public bool Knocked => _motor.State == MoveState.Knocked;
        public bool Grounded => _motor.Grounded;

        /// <summary>Where a carried briefcase hangs on this player for everyone but the carrier.</summary>
        public Transform CarryAnchor => _carryAnchor != null ? _carryAnchor : transform;

        // ---- Firm assignment -----------------------------------------------------------------

        /// <summary>Every player the team spawner is about to spawn is told its firm first, so the firm rides in the spawn message.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSpawner()
        {
            Members.Clear();
            Local = null;
            _shownPrompt = MatchPrompt.None;
            TeamSpawner.OnPlayerSpawning -= AssignOnSpawn;
            TeamSpawner.OnPlayerSpawning += AssignOnSpawn;
        }

        static void AssignOnSpawn(NetworkObject player, NetworkConnection connection, int team, int slot)
        {
            if (player.TryGetComponent(out FirmMember member))
            {
                member._assignedFirm = (byte)team;
                member._assignedSlot = (byte)slot;
            }
        }

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Members.Add(this);
            _firm.OnChange += OnFirmChanged;
            OnRosterChanged?.Invoke();
            // The briefcase status can name this player as carrier before their body exists here.
            if (MatchDirector.Instance != null)
                MatchDirector.Instance.ApplyGates();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _firm.OnChange -= OnFirmChanged;
            Members.Remove(this);
            OnRosterChanged?.Invoke();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _firm.Value = _assignedFirm;
            _slot.Value = _assignedSlot;
            OutsideTicks = 0;
            WasOnRoof = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            // Leaving mid-carry: the briefcase stays in the game, where this player stood.
            if (MatchDirector.Instance != null)
                MatchDirector.Instance.ServerMemberLeft(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            _interact = new InputAction("Interact", InputActionType.Button);
            _interact.AddBinding(InteractKey);
            _interact.AddBinding(InteractPad);
            _drop = new InputAction("Drop", InputActionType.Button);
            _drop.AddBinding(DropKey);
            _drop.AddBinding(DropPad);
            _interact.Enable();
            _drop.Enable();

            Local = this;
            OnLocalChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner)
                return;

            _interact?.Dispose();
            _drop?.Dispose();
            _interact = null;
            _drop = null;

            if (Local == this)
                Local = null;
            SetPrompt(MatchPrompt.None);
            OnLocalChanged?.Invoke();
        }

        void OnFirmChanged(byte previous, byte next, bool asServer)
        {
            if (asServer != IsServerStarted)
                return;

            OnRosterChanged?.Invoke();
            if (Local == this)
                OnLocalChanged?.Invoke();
        }

        // ---- Owner: keys and prompt ----------------------------------------------------------

        /// <summary>Owner only. Keys count only while the mouse is captured, which is exactly while the game menu is closed.</summary>
        void Update()
        {
            if (_interact == null)
                return;

            bool captured = PlayerInputReader.Captured;
            MatchDirector match = MatchDirector.Instance;
            MatchPrompt prompt = captured && match != null ? match.PromptFor(this) : MatchPrompt.None;
            SetPrompt(prompt);
            if (!captured)
                return;

            // Sent even when this machine's prompt is down: the host's view of the reach decides.
            if (_interact.WasPressedThisFrame())
                RequestInteract();
            if (_drop.WasPressedThisFrame() && prompt == MatchPrompt.DropBriefcase)
                RequestDrop();
        }

        static void SetPrompt(MatchPrompt prompt)
        {
            if (prompt == _shownPrompt)
                return;

            _shownPrompt = prompt;
            OnPromptChanged?.Invoke(prompt);
        }

        /// <summary>Owner only: ask the host to pick up the briefcase in reach. What the interact key calls.</summary>
        public void RequestInteract()
        {
            if (IsOwner)
                ServerInteract();
        }

        /// <summary>Owner only: ask the host to drop the briefcase this player carries. What the drop key calls.</summary>
        public void RequestDrop()
        {
            if (IsOwner)
                ServerDropBriefcase();
        }

        [ServerRpc]
        void ServerInteract()
        {
            if (MatchDirector.Instance != null)
                MatchDirector.Instance.ServerTryPickUp(this);
        }

        [ServerRpc]
        void ServerDropBriefcase()
        {
            if (MatchDirector.Instance != null)
                MatchDirector.Instance.ServerDrop(this, DropCause.Manual);
        }

        // ---- Host: moving and telling the player ---------------------------------------------

        /// <summary>
        /// Host only. Moves this player to another firm and place in it, for a lobby that
        /// chose the firms. Synced to everyone like the firm given on join.
        /// </summary>
        public void ServerSetFirm(int firm, int slot)
        {
            if (!IsServerStarted)
                return;

            _firm.Value = (byte)firm;
            _slot.Value = (byte)slot;
        }

        /// <summary>
        /// Host only. Puts the body somewhere else, standing still. The host's next state for
        /// this body carries the new position, so the owner's prediction is corrected to it.
        /// </summary>
        public void ServerTeleport(Vector3 position)
        {
            if (!IsServerStarted)
                return;

            _rigidbody.position = position;
            transform.position = position;
            _rigidbody.linearVelocity = Vector3.zero;
            OutsideTicks = 0;
            WasOnRoof = false;
        }

        /// <summary>Host only. Shows a one-off line on this player's own screen.</summary>
        public void Notify(MatchNotice notice)
        {
            if (IsServerStarted && Owner.IsValid)
                TargetNotice(Owner, notice);
        }

        /// <summary>Host only. Tells every machine this player fell out and was put back.</summary>
        public void AnnounceFellOut()
        {
            if (IsServerStarted)
                ObserversFellOut();
        }

        [TargetRpc]
        void TargetNotice(NetworkConnection connection, MatchNotice notice)
        {
            OnNotice?.Invoke(notice);
        }

        [ObserversRpc]
        void ObserversFellOut()
        {
            OnFellOut?.Invoke(this);
        }

        // ---- Lookup --------------------------------------------------------------------------

        /// <summary>The player with this network object id on this machine, or null.</summary>
        public static FirmMember Find(int objectId)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].ObjectId == objectId)
                    return Members[i];
            }

            return null;
        }

        /// <summary>How many players a firm has on this machine's view of the game.</summary>
        public static int CountIn(int firm)
        {
            int count = 0;
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].Firm == firm)
                    count++;
            }

            return count;
        }
    }
}

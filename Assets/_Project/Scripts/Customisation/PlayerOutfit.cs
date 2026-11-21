using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// What a player is wearing, kept by the host and seen by everyone (late joiners included).
    /// The owner loads their saved outfit and sends it as 8 bytes of ids; the host checks every
    /// id against the catalog and either keeps it or refuses it, and the kept value reaches all
    /// players through one synced value. Lock state is NOT checked by the host: the runs count
    /// lives on the player's own PC, so locks are honoured by the client only (cosmetic, no
    /// gameplay effect). Owns / synced / rate: docs/systems/CUSTOMISATION.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOutfit : NetworkBehaviour
    {
        /// <summary>The local player's outfit, once it has spawned.</summary>
        public static PlayerOutfit Local { get; private set; }

        /// <summary>Raised on the owner when the local player's outfit becomes available.</summary>
        public static event Action<PlayerOutfit> OnLocalSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Local = null;
            OnLocalSpawned = null;
        }

        /// <summary>Raised on every machine when this player's worn outfit changes (after the host accepted it).</summary>
        public event Action<OutfitLoadout> OnWornChanged;

        /// <summary>Raised on the owner when the host refused an outfit. The worn outfit stays as it was.</summary>
        public event Action OnRefused;

        /// <summary>Raised on the owner when their runs count goes up, so locked choices can open.</summary>
        public event Action<int> OnRunsStartedChanged;

        [Tooltip("The character drawing that wears the outfit.")]
        [SerializeField] OutfitView _view;

        readonly SyncVar<ulong> _worn = new();

        int _runsStarted;

        public OutfitView View => _view;
        public OutfitCatalog Catalog => _view != null ? _view.Catalog : null;
        public OutfitLoadout Worn => new(_worn.Value);

        /// <summary>Owner only: runs started on this PC, which decides what is unlocked.</summary>
        public int RunsStarted => _runsStarted;

        void Awake()
        {
            _worn.OnChange += OnWornChange;
        }

        void OnDestroy()
        {
            _worn.OnChange -= OnWornChange;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_view != null && Catalog != null)
                _view.Show(Catalog.IsValid(Worn) ? Worn : Catalog.Default, false);

            if (!IsOwner || Catalog == null)
                return;

            OutfitSave.Loaded saved = OutfitSave.Load(Catalog);
            _runsStarted = saved.RunsStarted;
            _view.Show(saved.Loadout, false);
            Local = this;
            CmdWear(saved.Loadout.Packed);
            OnLocalSpawned?.Invoke(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this)
                Local = null;
        }

        // ---- Owner API -----------------------------------------------------------------------

        /// <summary>Owner only: shows an outfit on this PC without telling anyone, for browsing.</summary>
        public void Preview(OutfitLoadout loadout)
        {
            if (IsOwner && _view != null)
                _view.Show(loadout, true);
        }

        /// <summary>Owner only: puts the worn outfit back on after browsing.</summary>
        public void Revert()
        {
            if (IsOwner && _view != null)
                _view.Show(Worn, true);
        }

        /// <summary>Owner only: keeps the outfit: saves it on this PC and asks the host to wear it.</summary>
        public void Commit(OutfitLoadout loadout)
        {
            if (!IsOwner || Catalog == null || !Catalog.IsWearable(loadout, _runsStarted))
                return;

            OutfitSave.SaveOutfit(Catalog, loadout);
            _view.Show(loadout, false);
            CmdWear(loadout.Packed);
        }

        /// <summary>Owner only: one more run started on this PC. Saved at once.</summary>
        public void RecordRunStarted()
        {
            if (!IsOwner || Catalog == null)
                return;

            _runsStarted = OutfitSave.AddRunStarted(Catalog);
            OnRunsStartedChanged?.Invoke(_runsStarted);
        }

        // ---- Host ----------------------------------------------------------------------------

        [ServerRpc]
        void CmdWear(ulong packed)
        {
            ServerWear(packed);
        }

        /// <summary>
        /// Host only: the one place an outfit request is judged. Public so a test can feed it a
        /// bad request directly. Returns true when accepted.
        /// </summary>
        public bool ServerWear(ulong packed)
        {
            if (!IsServerInitialized)
                return false;

            var loadout = new OutfitLoadout(packed);
            if (Catalog == null || !Catalog.IsValid(loadout))
            {
                Debug.LogWarning($"[Outfit] Refused an outfit from player {OwnerId}: it names a choice the catalog doesn't have (0x{packed:X16}).");
                if (Owner.IsValid)
                    TargetRefused(Owner);
                return false;
            }

            _worn.Value = packed;
            return true;
        }

        [TargetRpc]
        void TargetRefused(FishNet.Connection.NetworkConnection target)
        {
            if (_view != null)
                _view.Show(Worn, false);
            OnRefused?.Invoke();
        }

        void OnWornChange(ulong previous, ulong next, bool asServer)
        {
            // The host hears this twice (as server and as client); act once, on the client side.
            if (asServer)
                return;

            var loadout = new OutfitLoadout(next);
            if (_view != null)
                _view.Show(loadout, previous != next && IsSpawned);
            OnWornChanged?.Invoke(loadout);
        }
    }
}

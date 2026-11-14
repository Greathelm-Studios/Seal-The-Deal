using System;
using CloseTheDeal.Core;
using CloseTheDeal.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Combat
{
    /// <summary>What the gun in the owner's hand is doing.</summary>
    public enum WeaponState : byte
    {
        Ready = 0,
        Equipping = 1,
        Reloading = 2
    }

    /// <summary>What a shot stopped on, for impact effects and sounds. Glass never stops a shot; a pane it crossed raises GlassPane.AnyBroken instead.</summary>
    public enum ShotSurface : byte
    {
        /// <summary>It ran out of range.</summary>
        Nothing = 0,
        /// <summary>The building, furniture, a door: anything solid that isn't a body.</summary>
        World = 1,
        /// <summary>A player or a target dummy.</summary>
        Body = 2
    }

    /// <summary>One shot, for tracers, muzzle flashes and sound. Predicted is true on the shooter's own game, where it is drawn before the host has answered.</summary>
    public readonly struct ShotInfo
    {
        public readonly WeaponDefinition Weapon;
        public readonly Vector3 Origin;
        public readonly Vector3 End;
        public readonly bool HitSomething;
        /// <summary>What it stopped on. On the shooter's own game this is its own prediction; everywhere else, the host's answer.</summary>
        public readonly ShotSurface Surface;
        public readonly bool Predicted;
        /// <summary>Which shot of the burst this is, from 0. Only known on the shooter's own game; the view kick reads the recoil pattern with it.</summary>
        public readonly int ShotInBurst;

        public ShotInfo(WeaponDefinition weapon, Vector3 origin, Vector3 end, ShotSurface surface, bool predicted, int shotInBurst)
        {
            Weapon = weapon;
            Origin = origin;
            End = end;
            Surface = surface;
            HitSomething = surface != ShotSurface.Nothing;
            Predicted = predicted;
            ShotInBurst = shotInBurst;
        }
    }

    /// <summary>
    /// The guns a player carries: two slots, their magazines, firing, reloading and switching.
    /// The owner's game answers the button at once (tracer, recoil, ammo count) and tells the
    /// host; the host checks the shot is allowed, traces it against where everyone stood when
    /// the shooter saw them, and is the only one that deals damage or breaks glass.
    /// Pickups call Equip and Unequip on the host. Design and ownership: docs/systems/WEAPONS.md.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class WeaponHolder : NetworkBehaviour
    {
        public const int SlotCount = 2;

        /// <summary>
        /// Every machine. Lets another system hold this player's fire (hands full of a carried
        /// item, or standing in the lobby): return true and the owner's trigger stays up and the
        /// host refuses the shot. Reload and switching still work. Null means never held.
        /// </summary>
        public static Func<WeaponHolder, bool> FireHeld;

        // ---- Inspector -----------------------------------------------------------------------

        [Tooltip("The list of every gun in the game. Players' games name guns to each other by their place in it.")]
        [SerializeField] WeaponCatalog _catalog;

        [Tooltip("What stops a bullet: everything solid in the level and the furniture, but not players (they are tested separately) and never glass that has a Glass Pane on it.")]
        [SerializeField] LayerMask _solidMask = ~0;

        [Header("Lag allowance")]
        [Tooltip("How far back in time the host will look for where a target stood when the shooter fired, in seconds. 0.25 covers a 200 ms connection. Higher is kinder to laggy shooters but lets them hit people who have already reached cover. The host keeps half a second at most.")]
        [Range(0f, 0.5f)] [SerializeField] float _lagAllowanceSeconds = 0.25f;

        [Tooltip("How far the shooter's eye may be from where the host has their body, in metres, before the host fires from its own position instead. Covers leaning and the shooter's own movement during lag; 2.5 allows sprinting on a 200 ms connection.")]
        [Range(0.5f, 5f)] [SerializeField] float _originTolerance = 2.5f;

        // ---- Events --------------------------------------------------------------------------

        /// <summary>The gun in hand changed (null for empty hands), with its slot. Every machine.</summary>
        public event Action<WeaponDefinition, int> WeaponChanged;

        /// <summary>A shot left this player's gun. Every machine; Predicted on the shooter's own.</summary>
        public event Action<ShotInfo> Fired;

        /// <summary>The trigger was pulled on an empty magazine. Shooter's own game only.</summary>
        public event Action DryFired;

        /// <summary>A reload began, with how long it takes in seconds. Every machine.</summary>
        public event Action<float> ReloadStarted;

        /// <summary>The host confirmed the magazine is full. Shooter's own game only.</summary>
        public event Action ReloadFinished;

        /// <summary>A reload was cut off by a switch, a death or the host refusing it. Shooter's own game only.</summary>
        public event Action ReloadCancelled;

        /// <summary>Rounds in the magazine of the gun in hand, then its size. Shooter's own game only.</summary>
        public event Action<int, int> AmmoChanged;

        /// <summary>The host confirmed a hit: where, how much damage, whether it killed, and whether it was a head hit. Shooter's own game only; this is the hit marker.</summary>
        public event Action<Vector3, float, bool, bool> HitConfirmed;

        /// <summary>The host confirmed this player's shot killed someone, with where. Shooter's own game only; raised straight after HitConfirmed, for a kill sound and marker of its own.</summary>
        public event Action<Vector3> KillConfirmed;

        // ---- Runtime -------------------------------------------------------------------------

        const int PendingLength = 64;
        const byte LongAgo = byte.MaxValue;
        /// <summary>Remote bodies are drawn about this many ticks behind the newest host tick heard: FishNet's two ticks of interpolation plus about one more, measured in the lane self-check.</summary>
        const int InterpolationTicks = 3;
        /// <summary>How early a shot may arrive against the host's own clock before it is refused, in seconds. Covers network jitter, not a faster gun: the long-run rate is still the weapon's.</summary>
        const double RateSlackSeconds = 0.1;
        /// <summary>Ticks of grace on reload and equip timers, and on how rested the shooter claims the gun is.</summary>
        const uint TimingSlackTicks = 9;
        /// <summary>How long the owner trusts its own switch before taking the host's word for which gun is out, in seconds.</summary>
        const float SwitchTrustSeconds = 0.4f;
        /// <summary>How long past a reload's end the owner waits for the host's confirmation before giving up, in seconds.</summary>
        const float ReloadConfirmGraceSeconds = 0.75f;
        /// <summary>How long a press made while the gun is still cycling is remembered, in seconds, so fast taps on a one-per-press gun are never swallowed.</summary>
        const float PressBufferSeconds = 0.15f;
        const float FallbackEyeHeight = 1.6f;
        /// <summary>Sent when the shooter has not heard a host tick yet: the host then tests targets where they are now.</summary>
        const uint NoViewTick = uint.MaxValue;
        /// <summary>Slower than this on the ground counts as standing still for accuracy, in metres per second.</summary>
        const float MovingSpeed = 1f;
        const byte MovingFlag = 1;
        const byte AirborneFlag = 2;

        static readonly SyncTypeSettings EveryTick = new(0f);
        static readonly SyncTypeSettings OwnerEveryTick = new(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0f, Channel.Reliable);

        // Synced to everyone: which gun is in each slot (catalog place) and which slot is out.
        readonly SyncVar<byte> _slot0 = new(WeaponCatalog.None, EveryTick);
        readonly SyncVar<byte> _slot1 = new(WeaponCatalog.None, EveryTick);
        readonly SyncVar<byte> _active = new(EveryTick);
        // Synced to the owner only, as one value so its parts can never arrive apart:
        // magazine 0, magazine 1, the last shot number the host has dealt with, reloads completed.
        readonly SyncVar<ulong> _ownerState = new(OwnerEveryTick);

        readonly WeaponDefinition[] _weapons = new WeaponDefinition[SlotCount];
        Health _health;
        PlayerMotor _motor;
        Rigidbody _rigidbody;

        // Host side.
        readonly byte[] _hostAmmo = new byte[SlotCount];
        ushort _hostAckSeq;
        byte _hostReloadsDone;
        bool _hostReloading;
        uint _hostReloadDoneTick;
        uint _hostReadyTick;
        uint _hostLastShotTick;
        double _hostNextShotTime;
        float _hostBloom;

        // Owner side.
        readonly byte[] _pendingSlots = new byte[PendingLength];
        WeaponInput _input;
        ushort _seq;
        int _localActive;
        WeaponState _state;
        float _stateEndsAt;
        float _stateDuration;
        float _nextShotAt;
        float _switchTrustedUntil;
        float _bloom;
        uint _lastShotLocalTick;
        bool _hasShot;
        int _shotInBurst;
        uint _viewTick = NoViewTick;
        float _lastShotTime;
        bool _triggerHeld;
        bool _triggerPressed;
        float _pressBufferedUntil;
        byte _seenReloadsDone;
        int _shownAmmo = -1;
        int _shownMagazine = -1;

        // ---- Read-outs -----------------------------------------------------------------------

        /// <summary>The slot in hand. On the owner's game this is already the slot they just switched to.</summary>
        public int ActiveSlot => IsOwner ? _localActive : _active.Value;

        /// <summary>The gun in hand, or null for empty hands.</summary>
        public WeaponDefinition ActiveWeapon => _weapons[ActiveSlot];

        public WeaponDefinition WeaponInSlot(int slot) => slot >= 0 && slot < SlotCount ? _weapons[slot] : null;

        /// <summary>
        /// Owner only: how far through the current reload or draw the gun is, 0 to 1, and 0 when
        /// it is ready. A reload reads 1 at the end of its time until the host's full magazine arrives.
        /// </summary>
        public float StateProgress
        {
            get
            {
                if (_state == WeaponState.Ready || _stateDuration <= 0f)
                    return 0f;
                return Mathf.Clamp01(1f - (_stateEndsAt - Time.time) / _stateDuration);
            }
        }

        /// <summary>What the gun in the owner's hand is doing. Only meaningful on the owner's game.</summary>
        public WeaponState State => _state;

        /// <summary>
        /// Owner only: how far the next shot can stray from the crosshair right now, in
        /// degrees, from the same numbers the shot will use. For a crosshair whose gap tells
        /// the truth.
        /// </summary>
        public float CurrentSpreadDegrees
        {
            get
            {
                WeaponDefinition weapon = ActiveWeapon;
                if (weapon == null)
                    return 0f;

                float bloom = Settled(_bloom, weapon, Time.time - _lastShotTime);
                float spread = weapon.BaseSpread + bloom;
                byte stance = Stance();
                if ((stance & AirborneFlag) != 0)
                    return spread * weapon.AirborneSpreadMultiplier;
                return (stance & MovingFlag) != 0 ? spread * weapon.MovingSpreadMultiplier : spread;
            }
        }

        /// <summary>Rounds left in the gun in hand: the host's count on the host, the owner's own running count on their game.</summary>
        public int AmmoInMagazine => IsOwner ? OwnerAmmo(_localActive) : _hostAmmo[_active.Value];

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            _health = GetComponent<Health>();
            _motor = GetComponent<PlayerMotor>();
            _rigidbody = GetComponent<Rigidbody>();
            _slot0.OnChange += OnSlotChanged;
            _slot1.OnChange += OnSlotChanged;
            _active.OnChange += OnActiveChanged;
            _ownerState.OnChange += OnOwnerStateChanged;
            _health.Died += OnDied;
            _health.Respawned += OnRespawned;
        }

        void OnDestroy()
        {
            _slot0.OnChange -= OnSlotChanged;
            _slot1.OnChange -= OnSlotChanged;
            _active.OnChange -= OnActiveChanged;
            _ownerState.OnChange -= OnOwnerStateChanged;
            _health.Died -= OnDied;
            _health.Respawned -= OnRespawned;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // A pooled player comes back empty-handed, then gets the scene's starting guns.
            _slot0.Value = WeaponCatalog.None;
            _slot1.Value = WeaponCatalog.None;
            _active.Value = 0;
            Array.Clear(_weapons, 0, SlotCount);
            Array.Clear(_hostAmmo, 0, SlotCount);
            _hostAckSeq = 0;
            _hostReloadsDone = 0;
            _hostReloading = false;
            _hostBloom = 0f;
            _hostNextShotTime = 0d;
            GiveStartingLoadout();
            PushOwnerState();
            TimeManager.OnTick += HostTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= HostTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveSlots();
            _localActive = _active.Value;

            if (IsOwner)
            {
                _input = new WeaponInput();
                _seq = 0;
                _hasShot = false;
                _bloom = 0f;
                _triggerHeld = false;
                _triggerPressed = false;
                _seenReloadsDone = ReloadsDone(_ownerState.Value);
                BeginEquip(ActiveWeapon);
            }

            WeaponChanged?.Invoke(ActiveWeapon, ActiveSlot);
            RefreshOwnerAmmo();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _input?.Dispose();
            _input = null;
        }

        // ---- Pickups (host) ------------------------------------------------------------------

        /// <summary>
        /// Host only. Gives this player a gun with a full magazine: into the first empty slot,
        /// or over the gun in hand when both are taken. Returns false when it could not
        /// (not the host, or the gun is not in the catalog).
        /// </summary>
        public bool Equip(WeaponDefinition weapon)
        {
            int slot = _weapons[0] == null ? 0 : _weapons[1] == null ? 1 : _active.Value;
            return Equip(weapon, slot);
        }

        /// <summary>Host only. Gives this player a gun with a full magazine in a chosen slot (0 or 1), replacing what was there.</summary>
        public bool Equip(WeaponDefinition weapon, int slot)
        {
            byte id = _catalog != null ? _catalog.IndexOf(weapon) : WeaponCatalog.None;
            if (!IsServerStarted || slot < 0 || slot >= SlotCount || id == WeaponCatalog.None)
                return false;

            bool handsWereEmpty = _weapons[_active.Value] == null;
            if (slot == _active.Value)
                _hostReloading = false;

            _weapons[slot] = weapon;
            _hostAmmo[slot] = (byte)weapon.MagazineSize;
            SlotVar(slot).Value = id;
            if (handsWereEmpty)
                HostSetActive(slot);

            PushOwnerState();
            return true;
        }

        /// <summary>Host only. Takes away the gun in hand; the other slot's gun comes out if there is one.</summary>
        public void Unequip()
        {
            Unequip(_active.Value);
        }

        /// <summary>Host only. Empties a slot (0 or 1).</summary>
        public void Unequip(int slot)
        {
            if (!IsServerStarted || slot < 0 || slot >= SlotCount || _weapons[slot] == null)
                return;

            _weapons[slot] = null;
            _hostAmmo[slot] = 0;
            SlotVar(slot).Value = WeaponCatalog.None;

            int other = 1 - slot;
            if (slot == _active.Value)
            {
                _hostReloading = false;
                if (_weapons[other] != null)
                    HostSetActive(other);
            }

            PushOwnerState();
        }

        void GiveStartingLoadout()
        {
            StartingLoadout loadout = StartingLoadout.Instance;
            if (loadout == null)
                return;

            for (int i = 0; i < loadout.Weapons.Length && i < SlotCount; i++)
            {
                if (loadout.Weapons[i] != null && !Equip(loadout.Weapons[i], i))
                    Debug.LogWarning($"[Weapons] Starting gun '{loadout.Weapons[i].name}' is not in the weapon catalog; not given.");
            }
        }

        // ---- The owner's buttons -------------------------------------------------------------

        /// <summary>Owner only. The fire button went down. One shot for a one-per-press gun; an automatic keeps firing until ReleaseTrigger.</summary>
        public void PullTrigger()
        {
            if (!IsOwner)
                return;

            _triggerHeld = true;
            _triggerPressed = true;
        }

        /// <summary>Owner only. The fire button came up.</summary>
        public void ReleaseTrigger()
        {
            _triggerHeld = false;
        }

        /// <summary>Owner only. Starts a reload of the gun in hand. False when there is nothing to reload or the gun is busy.</summary>
        public bool Reload()
        {
            WeaponDefinition weapon = ActiveWeapon;
            if (!IsOwner || weapon == null || _health.IsDead || _state != WeaponState.Ready)
                return false;
            if (OwnerAmmo(_localActive) >= weapon.MagazineSize)
                return false;

            _state = WeaponState.Reloading;
            _stateEndsAt = Time.time + weapon.ReloadSeconds;
            _stateDuration = weapon.ReloadSeconds;
            ReloadStarted?.Invoke(weapon.ReloadSeconds);
            ServerReload((byte)_localActive);
            return true;
        }

        /// <summary>Owner only. Brings out the gun in a slot (0 or 1). Cuts off a reload without changing either magazine.</summary>
        public bool Switch(int slot)
        {
            if (!IsOwner || slot < 0 || slot >= SlotCount || slot == _localActive)
                return false;
            if (_weapons[slot] == null || _health.IsDead)
                return false;

            CancelOwnerReload();
            _localActive = slot;
            _switchTrustedUntil = Time.time + SwitchTrustSeconds;
            BeginEquip(_weapons[slot]);
            WeaponChanged?.Invoke(_weapons[slot], slot);
            RefreshOwnerAmmo();
            ServerSwitch((byte)slot);
            return true;
        }

        void Update()
        {
            if (!IsOwner || _input == null)
                return;

            ReadButtons();
            FollowHostSlot();
            TickState();

            if (_triggerPressed)
                _pressBufferedUntil = Time.time + PressBufferSeconds;
            _triggerPressed = false;

            bool pressed = Time.time <= _pressBufferedUntil;
            WeaponDefinition weapon = ActiveWeapon;
            if (weapon != null && (pressed || (_triggerHeld && weapon.Automatic)))
                TryFire(weapon, pressed);
        }

        /// <summary>Everything reads as idle while the mouse is free, which is exactly while the game menu is open (same rule as PlayerInputReader).</summary>
        void ReadButtons()
        {
            if (!PlayerInputReader.Captured)
            {
                ReleaseTrigger();
                return;
            }

            if (FireHeld != null && FireHeld(this))
                ReleaseTrigger();
            else if (_input.FirePressed)
                PullTrigger();
            if (_input.FireReleased)
                ReleaseTrigger();
            if (_input.ReloadPressed)
                Reload();
            if (_input.Slot1Pressed)
                Switch(0);
            if (_input.Slot2Pressed)
                Switch(1);
        }

        /// <summary>The host can change the gun in hand too (a pickup, an unequip). Once the owner's own switch has had time to land, the host's word stands.</summary>
        void FollowHostSlot()
        {
            if (_localActive == _active.Value || Time.time < _switchTrustedUntil)
                return;

            CancelOwnerReload();
            _localActive = _active.Value;
            BeginEquip(ActiveWeapon);
            WeaponChanged?.Invoke(ActiveWeapon, _localActive);
            RefreshOwnerAmmo();
        }

        void TickState()
        {
            if (_state == WeaponState.Equipping && Time.time >= _stateEndsAt)
                _state = WeaponState.Ready;

            // The host ends a reload by filling the magazine. If that never comes, it refused the reload.
            if (_state == WeaponState.Reloading && Time.time >= _stateEndsAt + ReloadConfirmGraceSeconds)
                CancelOwnerReload();
        }

        void TryFire(WeaponDefinition weapon, bool pressed)
        {
            if (_health.IsDead || _state != WeaponState.Ready)
            {
                _pressBufferedUntil = 0f;
                return;
            }

            // Still cycling: a remembered press waits here for the gun, it isn't thrown away.
            if (Time.time < _nextShotAt)
                return;

            _pressBufferedUntil = 0f;
            if (OwnerAmmo(_localActive) <= 0)
            {
                if (pressed)
                    DryFired?.Invoke();
                return;
            }

            FireShot(weapon);
        }

        void FireShot(WeaponDefinition weapon)
        {
            _seq++;
            _pendingSlots[_seq % PendingLength] = (byte)_localActive;

            uint localTick = TimeManager.LocalTick;
            byte ticksSince = _hasShot ? (byte)Math.Min(localTick - _lastShotLocalTick, LongAgo) : LongAgo;
            _lastShotLocalTick = localTick;
            _lastShotTime = Time.time;
            _hasShot = true;

            // Keep the long-run rate exact even though frames don't line up with the shot interval.
            bool onTime = Time.time - _nextShotAt < Time.deltaTime;
            _nextShotAt = (onTime ? _nextShotAt : Time.time) + weapon.ShotInterval;

            bool rested = ticksSince >= LongAgo || ticksSince * TimeManager.TickDelta > weapon.PatternResetSeconds;
            _shotInBurst = rested ? 0 : _shotInBurst + 1;

            EyeAndAim(out Vector3 origin, out Vector3 forward);
            uint packedAim = ShotResolver.PackDirection(forward);
            byte stance = Stance();
            float spread = NextSpread(ref _bloom, weapon, ticksSince, stance);
            Vector3 direction = ShotResolver.SpreadDirection(ShotResolver.UnpackDirection(packedAim), spread, ShotSeed(_seq));

            // Drawn straight away; the host's own trace is the one that counts.
            ShotResult seen = ShotResolver.Trace(origin, direction, weapon.Range, _solidMask, _health, false, 0, weapon.HeadFromHeight);
            Fired?.Invoke(new ShotInfo(weapon, origin, seen.End, SurfaceOf(seen), true, _shotInBurst));
            RefreshOwnerAmmo();

            // The host's own shot is judged on the spot, against the world exactly as the host sees it now;
            // sent round through the network it would be judged a tick late, with nothing to rewind for it.
            if (IsServerStarted)
                HandleFireRequest((byte)_localActive, _seq, origin, packedAim, ticksSince, _viewTick, stance);
            else
                ServerFire((byte)_localActive, _seq, origin, packedAim, ticksSince, _viewTick, stance);
        }

        /// <summary>
        /// The stray left after a rest of this many seconds. It holds through a burst and only
        /// settles once the trigger has rested as long as the recoil pattern needs to start over,
        /// so a burst grows wilder and a pause fixes it.
        /// </summary>
        static float Settled(float bloom, WeaponDefinition weapon, float rested)
        {
            if (rested < weapon.PatternResetSeconds)
                return bloom;
            return Mathf.Max(0f, bloom - weapon.BloomRecovery * (rested - weapon.PatternResetSeconds));
        }

        /// <summary>Whether the shooter is moving or in the air right now, as flags. Only the accuracy multipliers use it.</summary>
        byte Stance()
        {
            if (_motor == null)
                return 0;
            if (!_motor.Grounded)
                return AirborneFlag;
            return _motor.PlanarSpeed > MovingSpeed ? MovingFlag : (byte)0;
        }

        void EyeAndAim(out Vector3 origin, out Vector3 forward)
        {
            PlayerCamera cam = PlayerCamera.Instance;
            if (cam != null)
            {
                origin = cam.transform.position;
                forward = cam.transform.forward;
                return;
            }

            origin = HostEye();
            forward = transform.forward;
        }

        /// <summary>
        /// Owner only, after the frame's movement and before it is drawn: notes which of the
        /// host's ticks this screen is showing, because the next frame's shot is aimed at this
        /// picture. On the host that is its own latest tick; on a client, the newest tick it
        /// has heard from the host, less the ticks remote bodies are held back to smooth them.
        /// </summary>
        void LateUpdate()
        {
            if (!IsOwner)
                return;

            if (IsServerStarted)
            {
                _viewTick = TimeManager.Tick;
                return;
            }

            uint heard = TimeManager.LastPacketTick.RemoteTick;
            _viewTick = heard == FishNet.Managing.Timing.TimeManager.UNSET_TICK || heard < InterpolationTicks ? NoViewTick : heard - InterpolationTicks;
        }

        void BeginEquip(WeaponDefinition weapon)
        {
            _state = weapon != null && weapon.EquipSeconds > 0f ? WeaponState.Equipping : WeaponState.Ready;
            _stateEndsAt = weapon != null ? Time.time + weapon.EquipSeconds : 0f;
            _stateDuration = weapon != null ? weapon.EquipSeconds : 0f;
            _bloom = 0f;
            _hasShot = false;
            _shotInBurst = 0;
        }

        void CancelOwnerReload()
        {
            if (_state != WeaponState.Reloading)
                return;

            _state = WeaponState.Ready;
            ReloadCancelled?.Invoke();
        }

        // ---- Owner's running ammo count ------------------------------------------------------

        /// <summary>The host's last count for a slot, less the owner's shots the host has not answered yet.</summary>
        int OwnerAmmo(int slot)
        {
            ulong state = _ownerState.Value;
            int ammo = slot == 0 ? (byte)state : (byte)(state >> 8);
            ushort acked = (ushort)(state >> 16);

            int unanswered = Math.Min((int)(ushort)(_seq - acked), PendingLength);
            for (int i = 0; i < unanswered; i++)
            {
                if (_pendingSlots[(ushort)(_seq - i) % PendingLength] == slot)
                    ammo--;
            }

            return Math.Max(0, ammo);
        }

        void RefreshOwnerAmmo()
        {
            if (!IsOwner)
                return;

            WeaponDefinition weapon = ActiveWeapon;
            int magazine = weapon != null ? weapon.MagazineSize : 0;
            int ammo = weapon != null ? OwnerAmmo(_localActive) : 0;
            if (ammo == _shownAmmo && magazine == _shownMagazine)
                return;

            _shownAmmo = ammo;
            _shownMagazine = magazine;
            AmmoChanged?.Invoke(ammo, magazine);
        }

        void OnOwnerStateChanged(ulong previous, ulong next, bool asServer)
        {
            if (!IsOwner)
                return;

            byte reloadsDone = ReloadsDone(next);
            if (reloadsDone != _seenReloadsDone)
            {
                _seenReloadsDone = reloadsDone;
                if (_state == WeaponState.Reloading)
                {
                    _state = WeaponState.Ready;
                    ReloadFinished?.Invoke();
                }
            }

            RefreshOwnerAmmo();
        }

        // ---- Host: requests from the owner ---------------------------------------------------

        [ServerRpc]
        void ServerFire(byte slot, ushort seq, Vector3 origin, uint packedAim, byte ticksSince, uint viewTick, byte stance)
        {
            HandleFireRequest(slot, seq, origin, packedAim, ticksSince, viewTick, stance);
        }

        /// <summary>Host only. One shot the owner asks for: checked, traced, applied, and always answered.</summary>
        void HandleFireRequest(byte slot, ushort seq, Vector3 origin, uint packedAim, byte ticksSince, uint viewTick, byte stance)
        {
            // Shots arrive in order (reliable), so an honest owner always sends the next number.
            // Any other number is refused: the number seeds the spread and must not be chosen.
            bool next = seq == (ushort)(_hostAckSeq + 1);
            _hostAckSeq = seq;
            WeaponDefinition weapon = slot < SlotCount ? _weapons[slot] : null;
            uint tick = TimeManager.Tick;

            if (next && HostMayFire(slot, weapon, tick))
                HostFire(slot, weapon, seq, origin, packedAim, ticksSince, viewTick, stance, tick);

            // Sent either way: a refused shot still has to be answered, or the owner's count stays one short.
            PushOwnerState();
        }

        bool HostMayFire(int slot, WeaponDefinition weapon, uint tick)
        {
            if (weapon == null || slot != _active.Value || _health.IsDead || (FireHeld != null && FireHeld(this)))
                return false;
            if (tick + TimingSlackTicks < _hostReadyTick)
                return false;

            if (_hostReloading)
            {
                // The owner's reload ran on its own clock; a shot a few ticks early means it finished there.
                if (tick + TimingSlackTicks < _hostReloadDoneTick)
                    return false;
                HostFinishReload();
            }

            if (_hostAmmo[slot] == 0)
                return false;

            return TimeManager.TicksToTime(tick) >= _hostNextShotTime - RateSlackSeconds;
        }

        void HostFire(int slot, WeaponDefinition weapon, ushort seq, Vector3 origin, uint packedAim, byte ticksSince, uint viewTick, byte stance, uint tick)
        {
            _hostNextShotTime = Math.Max(TimeManager.TicksToTime(tick), _hostNextShotTime) + weapon.ShotInterval;
            _hostAmmo[slot]--;

            // The bloom uses the owner's own timing so both games stray the shot the same way,
            // but never as rested as the owner claims beyond what the host itself saw.
            uint rested = Math.Min(ticksSince, tick - _hostLastShotTick + TimingSlackTicks);
            _hostLastShotTick = tick;
            float spread = NextSpread(ref _hostBloom, weapon, rested, stance);

            uint seenTick = SeenTick(viewTick, tick);
            origin = TrustedOrigin(origin, tick - seenTick);

            Vector3 direction = ShotResolver.SpreadDirection(ShotResolver.UnpackDirection(packedAim), spread, ShotSeed(seq));
            ShotResult shot = ShotResolver.Trace(origin, direction, weapon.Range, _solidMask, _health, true, seenTick, weapon.HeadFromHeight);

            ShotResolver.BreakCrossedPanes(shot, direction, OwnerId);
            HostApplyHit(weapon, shot, direction);
            RpcShot(shot.End, (byte)SurfaceOf(shot));
        }

        /// <summary>
        /// Where the host lets a shot start. The shooter's own eye position is used when it is
        /// close enough to where the host has the body, allowing for the lean and for how far a
        /// moving body travels in the shooter's lag, and when nothing solid stands between the
        /// host's eye and it (glass doesn't count). Otherwise the host's own eye: a shot can't
        /// start on the far side of a wall or a locked door.
        /// </summary>
        Vector3 TrustedOrigin(Vector3 claimed, uint lagTicks)
        {
            Vector3 eye = HostEye();
            // The shooter's own game is ahead of the host's copy of the body along its movement, not in
            // any direction: allow the tolerance around the path the body travels in the shooter's lag.
            Vector3 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            Vector3 ahead = eye + velocity * (float)(lagTicks * TimeManager.TickDelta);
            if (DistanceToSegment(claimed, eye, ahead) > _originTolerance)
                return eye;

            return ShotResolver.ClearPath(eye, claimed, _solidMask) ? claimed : eye;
        }

        static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        /// <summary>The tick to test targets at: the one the shooter says it was looking at, never in the future and never further back than the lag allowance.</summary>
        uint SeenTick(uint viewTick, uint tick)
        {
            uint allowance = TimeManager.TimeToTicks(_lagAllowanceSeconds);
            uint oldest = tick > allowance ? tick - allowance : 0;
            // A tick from the future, or none at all, means "now".
            return viewTick > tick ? tick : Math.Max(viewTick, oldest);
        }

        void HostApplyHit(WeaponDefinition weapon, in ShotResult shot, Vector3 direction)
        {
            float damage = weapon.DamageAt(shot.Distance) * (shot.Precision ? weapon.HeadMultiplier : 1f);

            if (shot.Body != null)
            {
                Vector3 shove = Knockback.ShoveVelocity(direction, weapon.HitShove, weapon.ShoveLift);
                Vector3 killShove = Knockback.ShoveVelocity(direction, weapon.KillShove, weapon.ShoveLift);
                if (shot.Body.ApplyHit(damage, shot.End, direction, OwnerId, shove, killShove, weapon.HitControlLoss))
                    ConfirmHit(shot.End, damage, shot.Body.IsDead, shot.Precision);
                return;
            }

            IDamageable target = shot.Solid != null ? shot.Solid.GetComponentInParent<IDamageable>() : null;
            if (target == null)
                return;

            target.ApplyDamage(damage, shot.End, direction, OwnerId);
            ConfirmHit(shot.End, damage, false, false);
        }

        void ConfirmHit(Vector3 point, float damage, bool killed, bool precision)
        {
            if (Owner.IsValid)
                TargetHitConfirmed(Owner, point, damage, killed, precision);
        }

        [ServerRpc]
        void ServerReload(byte slot)
        {
            WeaponDefinition weapon = slot < SlotCount ? _weapons[slot] : null;
            if (weapon == null || slot != _active.Value || _health.IsDead || _hostReloading)
                return;
            if (_hostAmmo[slot] >= weapon.MagazineSize)
                return;

            _hostReloading = true;
            _hostReloadDoneTick = TimeManager.Tick + TimeManager.TimeToTicks(weapon.ReloadSeconds);
            RpcReloadStarted(weapon.ReloadSeconds);
        }

        [ServerRpc]
        void ServerSwitch(byte slot)
        {
            if (slot >= SlotCount || slot == _active.Value || _weapons[slot] == null || _health.IsDead)
                return;

            HostSetActive(slot);
        }

        void HostSetActive(int slot)
        {
            _hostReloading = false;
            _hostBloom = 0f;
            _hostReadyTick = TimeManager.Tick + TimeManager.TimeToTicks(_weapons[slot].EquipSeconds);
            _active.Value = (byte)slot;
        }

        /// <summary>Host, every tick. Does nothing unless a reload is running.</summary>
        void HostTick()
        {
            if (_hostReloading && TimeManager.Tick >= _hostReloadDoneTick)
                HostFinishReload();
        }

        void HostFinishReload()
        {
            _hostReloading = false;
            WeaponDefinition weapon = _weapons[_active.Value];
            if (weapon == null)
                return;

            _hostAmmo[_active.Value] = (byte)weapon.MagazineSize;
            _hostReloadsDone++;
            PushOwnerState();
        }

        void PushOwnerState()
        {
            _ownerState.Value = _hostAmmo[0]
                | ((ulong)_hostAmmo[1] << 8)
                | ((ulong)_hostAckSeq << 16)
                | ((ulong)_hostReloadsDone << 32);
        }

        // ---- Host: messages out --------------------------------------------------------------

        [ObserversRpc(ExcludeOwner = true)]
        void RpcShot(Vector3 end, byte surface, Channel channel = Channel.Unreliable)
        {
            Fired?.Invoke(new ShotInfo(ActiveWeapon, HostEye(), end, (ShotSurface)surface, false, 0));
        }

        [ObserversRpc(ExcludeOwner = true)]
        void RpcReloadStarted(float seconds)
        {
            ReloadStarted?.Invoke(seconds);
        }

        [TargetRpc]
        void TargetHitConfirmed(NetworkConnection owner, Vector3 point, float damage, bool killed, bool precision)
        {
            HitConfirmed?.Invoke(point, damage, killed, precision);
            if (killed)
                KillConfirmed?.Invoke(point);
        }

        // ---- Death and respawn (every machine) -----------------------------------------------

        void OnDied(Health health, DamageInfo info)
        {
            if (IsServerStarted)
                _hostReloading = false;

            if (IsOwner)
            {
                ReleaseTrigger();
                _triggerPressed = false;
                _pressBufferedUntil = 0f;
                CancelOwnerReload();
            }
        }

        void OnRespawned(Health health)
        {
            if (IsServerStarted)
                HostRefill();

            if (IsOwner)
                BeginEquip(ActiveWeapon);
        }

        /// <summary>A respawned player comes back with full magazines, and with the scene's starting guns if they had lost theirs.</summary>
        void HostRefill()
        {
            _hostReloading = false;
            _hostBloom = 0f;
            if (_weapons[0] == null && _weapons[1] == null)
                GiveStartingLoadout();

            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_weapons[slot] != null)
                    _hostAmmo[slot] = (byte)_weapons[slot].MagazineSize;
            }

            PushOwnerState();
        }

        // ---- Synced values landing -----------------------------------------------------------

        void OnSlotChanged(byte previous, byte next, bool asServer)
        {
            // The host filled _weapons itself in Equip; its client-side echo adds nothing.
            if (IsServerStarted)
            {
                if (asServer)
                    AnnounceIfInHand();
                return;
            }

            ResolveSlots();
            AnnounceIfInHand();
        }

        void OnActiveChanged(byte previous, byte next, bool asServer)
        {
            if (IsServerStarted && !asServer)
                return;

            // The owner's own game follows its own switch; FollowHostSlot handles the host overruling it.
            if (!IsOwner)
                WeaponChanged?.Invoke(_weapons[next], next);
        }

        void AnnounceIfInHand()
        {
            WeaponChanged?.Invoke(ActiveWeapon, ActiveSlot);
            RefreshOwnerAmmo();
        }

        void ResolveSlots()
        {
            if (IsServerStarted || _catalog == null)
                return;

            _weapons[0] = _catalog.Get(_slot0.Value);
            _weapons[1] = _catalog.Get(_slot1.Value);
        }

        // ---- Helpers -------------------------------------------------------------------------

        SyncVar<byte> SlotVar(int slot) => slot == 0 ? _slot0 : _slot1;

        static ShotSurface SurfaceOf(in ShotResult shot)
        {
            if (shot.Body != null)
                return ShotSurface.Body;
            return shot.Stopped ? ShotSurface.World : ShotSurface.Nothing;
        }

        static byte ReloadsDone(ulong ownerState) => (byte)(ownerState >> 32);

        /// <summary>Same number on the shooter's game and the host's, different for every shot and every player.</summary>
        uint ShotSeed(ushort seq) => unchecked((uint)OwnerId * 0x9E3779B1u + seq);

        /// <summary>Where the host has this player's eye, without the lean.</summary>
        Vector3 HostEye()
        {
            float height = _motor != null ? _motor.EyeHeight : FallbackEyeHeight;
            return transform.position + Vector3.up * height;
        }

        /// <summary>
        /// The stray for the next shot, in degrees, and the bloom it leaves behind. The bloom
        /// settles for the ticks since the last shot, then this shot adds to it. Runs the same
        /// on the shooter's game and the host's.
        /// </summary>
        float NextSpread(ref float bloom, WeaponDefinition weapon, uint ticksSince, byte stance)
        {
            float rested = ticksSince >= LongAgo ? float.MaxValue : (float)(ticksSince * TimeManager.TickDelta);
            bloom = Settled(bloom, weapon, rested);
            float spread = weapon.BaseSpread + bloom;
            bloom = Mathf.Min(weapon.MaxBloom, bloom + weapon.BloomPerShot);

            if ((stance & AirborneFlag) != 0)
                return spread * weapon.AirborneSpreadMultiplier;
            return (stance & MovingFlag) != 0 ? spread * weapon.MovingSpreadMultiplier : spread;
        }

        /// <summary>The lane-local buttons: fire, reload and the two slots, defined in code until they move into the shared actions asset.</summary>
        sealed class WeaponInput : IDisposable
        {
            readonly InputAction _fire = new("Fire", InputActionType.Button, "<Mouse>/leftButton");
            readonly InputAction _reload = new("Reload", InputActionType.Button, "<Keyboard>/r");
            readonly InputAction _slot1 = new("Weapon Slot 1", InputActionType.Button, "<Keyboard>/1");
            readonly InputAction _slot2 = new("Weapon Slot 2", InputActionType.Button, "<Keyboard>/2");

            public WeaponInput()
            {
                // Pad: right trigger fires, Y reloads, d-pad left and right pick the slots.
                // A, B, X and d-pad down belong to jump, crouch, interact and drop.
                _fire.AddBinding("<Gamepad>/rightTrigger");
                _reload.AddBinding("<Gamepad>/buttonNorth");
                _slot1.AddBinding("<Gamepad>/dpad/left");
                _slot2.AddBinding("<Gamepad>/dpad/right");
                _fire.Enable();
                _reload.Enable();
                _slot1.Enable();
                _slot2.Enable();
            }

            public bool FirePressed => _fire.WasPressedThisFrame();
            public bool FireReleased => _fire.WasReleasedThisFrame();
            public bool ReloadPressed => _reload.WasPressedThisFrame();
            public bool Slot1Pressed => _slot1.WasPressedThisFrame();
            public bool Slot2Pressed => _slot2.WasPressedThisFrame();

            public void Dispose()
            {
                _fire.Dispose();
                _reload.Dispose();
                _slot1.Dispose();
                _slot2.Dispose();
            }
        }
    }
}

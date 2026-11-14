using System;
using System.Collections.Generic;
using CloseTheDeal.Core;
using CloseTheDeal.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>One hit as every machine hears about it. Shove is the throw the host gave the body, zero for none.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly int AttackerClientId;
        public readonly Vector3 Shove;
        public readonly bool Fatal;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, int attackerClientId, Vector3 shove, bool fatal)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            AttackerClientId = attackerClientId;
            Shove = shove;
            Fatal = fatal;
        }
    }

    /// <summary>
    /// Hit points for a player or a target dummy. The host owns every number: it takes the
    /// damage, decides the death, counts down the respawn and puts the body back on the floor
    /// it died on. Everyone else gets the health, the dead flag and the last attacker as synced
    /// values, plus one message per hit, death and respawn to hang sound, effects and UI on.
    /// On a player the body is thrown through PlayerMotor's knockback, which also keeps a dead
    /// player from steering. Design and ownership: docs/systems/WEAPONS.md.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class Health : NetworkBehaviour, IDamageable
    {
        // ---- Inspector -----------------------------------------------------------------------

        [Header("Health")]
        [Tooltip("Hit points at full health. The pistol takes 34 a shot by default, so 100 is three pistol hits.")]
        [Min(1f)] [SerializeField] float _maxHealth = 100f;

        [Tooltip("How long a dead player waits before coming back on the floor they died on, in seconds. The GDD says 5.")]
        [Min(0f)] [SerializeField] float _respawnSeconds = 5f;

        [Tooltip("How fast the body is thrown when something other than a gun kills it (a fall trigger, a script), in metres per second.")]
        [Min(0f)] [SerializeField] float _defaultKillShove = 6f;

        [Header("Hit reaction")]
        [Tooltip("The colour the body flashes when hit.")]
        [SerializeField] Color _flashColour = new(1f, 0.25f, 0.2f, 1f);

        [Tooltip("How long the hit flash lasts, in seconds. Long enough to read from across the street, short enough that an automatic burst still flickers.")]
        [Range(0.02f, 0.5f)] [SerializeField] float _flashSeconds = 0.12f;

        [Tooltip("The part of the body that falls flat while dead and stands back up on respawn. Leave empty and a dead body only stops moving.")]
        [SerializeField] Transform _tipPivot;

        [Tooltip("How long the body takes to fall over or stand back up, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _tipSeconds = 0.25f;

        [Tooltip("How far the body rocks away from a hit, in degrees per point of damage. Uses the same part as falling over; 0 = no flinch.")]
        [Range(0f, 2f)] [SerializeField] float _flinchPerDamage = 0.35f;

        [Tooltip("The most one hit can rock the body, in degrees.")]
        [Range(0f, 45f)] [SerializeField] float _maxFlinch = 20f;

        [Tooltip("How long the body takes to stand straight again after a flinch, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _flinchSeconds = 0.3f;

        // ---- Events --------------------------------------------------------------------------

        /// <summary>This body took a hit. Raised on the host and on every client.</summary>
        public event Action<Health, DamageInfo> Damaged;

        /// <summary>This body died. The info's Shove is the ragdoll impulse. Raised on the host and on every client.</summary>
        public event Action<Health, DamageInfo> Died;

        /// <summary>This body is back. Raised on the host and on every client.</summary>
        public event Action<Health> Respawned;

        /// <summary>Health moved, with current and maximum. For a health bar; raised wherever the synced value lands.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>The same three, for anything that listens to every body at once (kill feed, the secretary).</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;
        public static event Action<Health, DamageInfo> AnyDied;
        public static event Action<Health> AnyRespawned;

        // ---- Runtime -------------------------------------------------------------------------

        const int HistoryLength = 32;
        const int SafeGroundEveryTicks = 15;
        const float RespawnControlLossSeconds = 0.1f;
        const float KillLift = 0.35f;
        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        /// <summary>Every body that exists on this machine. Shots are tested against these, never against their colliders.</summary>
        public static readonly List<Health> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }

        readonly SyncVar<float> _health = new(new SyncTypeSettings(0f));
        readonly SyncVar<int> _lastAttacker = new(-1, new SyncTypeSettings(0f));
        readonly SyncVar<bool> _dead = new(new SyncTypeSettings(0f));

        readonly Vector3[] _history = new Vector3[HistoryLength];
        readonly uint[] _historyTicks = new uint[HistoryLength];

        CapsuleCollider _capsule;
        Rigidbody _rigidbody;
        PlayerMotor _motor;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        Color[] _savedColours;
        bool _flashShown;

        uint _respawnTick;
        Vector3 _safePosition;
        bool _hasRespawnOverride;
        Vector3 _respawnOverride;
        float _flashLeft;
        float _respawnAt;
        uint _aliveFromTick;
        float _tip;
        Quaternion _tipRest;
        DecayingImpulse _flinch;
        Vector3 _flinchAxis = Vector3.right;
        float _flinchAngle;
        static readonly AnimationCurve FlinchCurve = DecayingImpulse.DefaultCurve(0.1f);

        public float Current => _health.Value;

        /// <summary>How long a death lasts before the respawn, in seconds.</summary>
        public float RespawnSeconds => _respawnSeconds;

        /// <summary>Seconds until this body comes back, counted on this machine from the moment it heard the death; 0 when alive, and 0 for a death that happened before this machine joined.</summary>
        public float SecondsUntilRespawn => IsDead ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;
        public float Max => _maxHealth;
        public bool IsDead => _dead.Value;

        /// <summary>Client id of whoever hit this body last, or -1 for the world or nobody yet.</summary>
        public int LastAttackerClientId => _lastAttacker.Value;

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            _capsule = GetComponent<CapsuleCollider>();
            _rigidbody = GetComponent<Rigidbody>();
            _motor = GetComponent<PlayerMotor>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _savedColours = new Color[_renderers.Length];
            _block = new MaterialPropertyBlock();
            if (_tipPivot != null)
                _tipRest = _tipPivot.localRotation;

            _health.OnChange += OnHealthChanged;
        }

        void OnDestroy()
        {
            _health.OnChange -= OnHealthChanged;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            All.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            All.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Value = _maxHealth;
            _dead.Value = false;
            _lastAttacker.Value = -1;
            _safePosition = transform.position;
            _hasRespawnOverride = false;
            _aliveFromTick = TimeManager.Tick;
            Array.Clear(_historyTicks, 0, HistoryLength);
            TimeManager.OnPostTick += HostPostTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnPostTick -= HostPostTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // A late joiner sees a body that is already down as down, without the fall.
            _tip = IsDead ? 1f : 0f;
            ApplyTip();
        }

        // ---- Damage (host) -------------------------------------------------------------------

        /// <summary>Host only. Plain damage with no shove unless it kills.</summary>
        public void ApplyDamage(float amount, Vector3 point, Vector3 direction, int attackerClientId)
        {
            Vector3 killShove = Knockback.ShoveVelocity(direction, _defaultKillShove, KillLift);
            ApplyHit(amount, point, direction, attackerClientId, Vector3.zero, killShove, 0f);
        }

        /// <summary>
        /// Host only. Damage plus the throw that goes with it: shove if the body survives,
        /// killShove if this is the hit that kills it. Returns false when nothing happened
        /// (not the host, or already dead).
        /// </summary>
        public bool ApplyHit(float amount, Vector3 point, Vector3 direction, int attackerClientId, Vector3 shove, Vector3 killShove, float controlLossSeconds)
        {
            if (!IsServerStarted || _dead.Value)
                return false;

            float left = Mathf.Max(0f, _health.Value - Mathf.Max(0f, amount));
            bool fatal = left <= 0f;
            _health.Value = left;
            _lastAttacker.Value = attackerClientId;

            var info = new DamageInfo(amount, point, direction, attackerClientId, fatal ? killShove : shove, fatal);
            if (fatal)
            {
                Die(info);
                return true;
            }

            if (_motor != null && shove != Vector3.zero)
                _motor.ApplyKnockback(shove, controlLossSeconds);

            RaiseDamaged(info);
            RpcDamaged(amount, point, direction, attackerClientId, shove);
            return true;
        }

        /// <summary>Host only. Back to full health, if alive. For healing pickups, checkpoints and tests.</summary>
        public void Restore()
        {
            if (IsServerStarted && !_dead.Value)
                _health.Value = _maxHealth;
        }

        /// <summary>Host only. Where this body comes back next time it dies, instead of the last safe ground. One use.</summary>
        public void OverrideNextRespawn(Vector3 position)
        {
            if (!IsServerStarted)
                return;

            _hasRespawnOverride = true;
            _respawnOverride = position;
        }

        void Die(DamageInfo info)
        {
            _dead.Value = true;
            _respawnTick = TimeManager.Tick + TimeManager.TimeToTicks(_respawnSeconds);

            // Knocked for the whole wait: the body flies with the kill and its owner can't steer it.
            if (_motor != null)
                _motor.ApplyKnockback(info.Shove, _respawnSeconds + RespawnControlLossSeconds);

            RaiseDamaged(info);
            RaiseDied(info);
            RpcDied(info.Amount, info.Point, info.Direction, info.AttackerClientId, info.Shove);
        }

        void Respawn()
        {
            Vector3 position = _hasRespawnOverride ? _respawnOverride : _safePosition;
            _hasRespawnOverride = false;

            if (_rigidbody != null)
            {
                _rigidbody.position = position;
                _rigidbody.linearVelocity = Vector3.zero;
                transform.position = position;
            }

            _health.Value = _maxHealth;
            _dead.Value = false;
            // Positions from before now belong to the corpse; a rewound shot must not find the new body there.
            _aliveFromTick = TimeManager.Tick;
            if (_motor != null)
                _motor.ApplyKnockback(Vector3.zero, RespawnControlLossSeconds);

            RaiseRespawned();
            RpcRespawned();
        }

        /// <summary>Host, once per tick after physics: position history for lag allowance, the respawn countdown, the last safe ground.</summary>
        void HostPostTick()
        {
            uint tick = TimeManager.Tick;
            int slot = (int)(tick % HistoryLength);
            _history[slot] = transform.position;
            _historyTicks[slot] = tick;

            if (_dead.Value)
            {
                DeadTick(tick);
                return;
            }

            if (_motor != null && tick % SafeGroundEveryTicks == 0 && _motor.State == MoveState.Grounded)
                _safePosition = transform.position;
        }

        void DeadTick(uint tick)
        {
            if (tick >= _respawnTick)
            {
                Respawn();
                return;
            }

            // A blast on the body restarts the motor's own short control loss; a dead player still must not steer.
            if (_motor != null && _motor.State != MoveState.Knocked)
                _motor.ApplyKnockback(Vector3.zero, (float)TimeManager.TicksToTime(_respawnTick - tick));
        }

        // ---- Lag allowance -------------------------------------------------------------------

        /// <summary>Host only. Where this body's feet were on a past tick; now, if that tick is no longer remembered.</summary>
        public Vector3 PositionAt(uint tick)
        {
            int slot = (int)(tick % HistoryLength);
            // A tick not remembered (the future, or too long ago) reads as now.
            return _historyTicks[slot] == tick ? _history[slot] : transform.position;
        }

        /// <summary>Host only. Whether this body was alive at a past tick, since its last spawn. A shot rewound to before a respawn can't hit it.</summary>
        public bool WasAliveAt(uint tick) => tick >= _aliveFromTick;

        /// <summary>The body's capsule as a segment and a radius, standing at the given feet position.</summary>
        public void CapsuleAt(Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = _capsule.radius;
            float half = Mathf.Max(0f, _capsule.height * 0.5f - radius);
            Vector3 centre = feet + _capsule.center;
            bottom = centre - Vector3.up * half;
            top = centre + Vector3.up * half;
        }

        // ---- Messages to clients -------------------------------------------------------------

        [ObserversRpc(ExcludeServer = true)]
        void RpcDamaged(float amount, Vector3 point, Vector3 direction, int attackerClientId, Vector3 shove, Channel channel = Channel.Unreliable)
        {
            RaiseDamaged(new DamageInfo(amount, point, direction, attackerClientId, shove, false));
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcDied(float amount, Vector3 point, Vector3 direction, int attackerClientId, Vector3 shove)
        {
            var info = new DamageInfo(amount, point, direction, attackerClientId, shove, true);
            RaiseDamaged(info);
            RaiseDied(info);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcRespawned()
        {
            RaiseRespawned();
        }

        void RaiseDamaged(DamageInfo info)
        {
            _flashLeft = _flashSeconds;
            Flinch(info);
            Damaged?.Invoke(this, info);
            AnyDamaged?.Invoke(this, info);
        }

        void RaiseDied(DamageInfo info)
        {
            _respawnAt = Time.time + _respawnSeconds;
            Died?.Invoke(this, info);
            AnyDied?.Invoke(this, info);
        }

        void RaiseRespawned()
        {
            Respawned?.Invoke(this);
            AnyRespawned?.Invoke(this);
        }

        void OnHealthChanged(float previous, float next, bool asServer)
        {
            // The host hears its own change as the server; skip its second, client-side echo.
            if (asServer || !IsServerStarted)
                HealthChanged?.Invoke(next, _maxHealth);
        }

        // ---- Visuals (every machine) ---------------------------------------------------------

        void Update()
        {
            float tipTarget = IsDead ? 1f : 0f;
            if (_flashLeft <= 0f && _tip == tipTarget && !_flinch.IsActive && _flinchAngle == 0f)
                return;

            _flinchAngle = _flinch.Step(Time.deltaTime, FlinchCurve);
            if (_flinch.IsActive || _flinchAngle == 0f)
                ApplyTip();

            if (_flashLeft > 0f)
            {
                _flashLeft -= Time.deltaTime;
                ApplyFlash(_flashLeft > 0f);
            }

            if (_tip != tipTarget)
            {
                _tip = Mathf.MoveTowards(_tip, tipTarget, Time.deltaTime / _tipSeconds);
                ApplyTip();
            }
        }

        /// <summary>Rocks the body away from the hit around the axis across the shot. Local looks only.</summary>
        void Flinch(DamageInfo info)
        {
            if (_tipPivot == null || _flinchPerDamage <= 0f || info.Fatal)
                return;

            Vector3 away = Vector3.ProjectOnPlane(info.Direction, Vector3.up);
            if (away.sqrMagnitude < 0.0001f)
                return;

            Transform parent = _tipPivot.parent != null ? _tipPivot.parent : transform;
            _flinchAxis = parent.InverseTransformDirection(Vector3.Cross(Vector3.up, away.normalized));
            _flinch.Add(Mathf.Min(_maxFlinch, info.Amount * _flinchPerDamage), _flinchSeconds, FlinchCurve);
        }

        /// <summary>
        /// Tints every body renderer, keeping whatever else other code has put in its property
        /// block (the character's eyes use one); ending the flash puts the material's own colour back.
        /// </summary>
        void ApplyFlash(bool on)
        {
            if (on == _flashShown)
                return;

            _flashShown = on;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                    continue;

                _block.Clear();
                r.GetPropertyBlock(_block);
                if (on)
                {
                    // Remember the colour in force now (another script's, or the material's) to put back after.
                    Material material = r.sharedMaterial;
                    _savedColours[i] = _block.HasColor(BaseColourId) ? _block.GetColor(BaseColourId)
                        : material != null && material.HasColor(BaseColourId) ? material.GetColor(BaseColourId) : Color.white;
                }

                _block.SetColor(BaseColourId, on ? _flashColour : _savedColours[i]);
                r.SetPropertyBlock(_block);
            }
        }

        void ApplyTip()
        {
            if (_tipPivot != null)
                _tipPivot.localRotation = Quaternion.AngleAxis(_flinchAngle, _flinchAxis) * _tipRest * Quaternion.Euler(-90f * _tip, 0f, 0f);
        }
    }
}

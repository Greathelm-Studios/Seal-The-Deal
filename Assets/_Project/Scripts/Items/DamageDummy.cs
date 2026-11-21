using System;
using CloseTheDeal.Core;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    /// <summary>Host-authoritative damageable practice target that tips over at zero health and recovers. Design: docs/systems/ITEMS.md.</summary>
    public sealed class DamageDummy : NetworkBehaviour, IDamageable
    {
        [Tooltip("Hit points when fresh. Players are expected to have 100.")]
        [Min(1f)] [SerializeField] float _maxHealth = 100f;

        [Tooltip("How long it lies knocked down before standing back up at full health, in seconds.")]
        [Min(0.1f)] [SerializeField] float _standUpSeconds = 4f;

        [Tooltip("The bar above it. Its width shows the health that is left.")]
        [SerializeField] Transform _bar;

        [Tooltip("The part that tips over when its health reaches zero.")]
        [SerializeField] Transform _tipPivot;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("It was hurt. The number is the hit points taken.")]
        [SerializeField] UnityEvent<float> _onHurt;
        [Tooltip("Its health reached zero and it fell over.")]
        [SerializeField] UnityEvent _onKnockedDown;

        const float TippedDegrees = 85f;

        readonly SyncVar<float> _health = new();
        float _standUpAt;
        bool _ticking;

        /// <summary>Every machine. It was hurt by this many hit points.</summary>
        public event Action<DamageDummy, float> Hurt;

        public float Health => _health.Value;
        public bool IsDown => _health.Value <= 0f;

        void Awake()
        {
            _health.OnChange += OnHealthChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Value = _maxHealth;
            _ticking = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            SetServerTick(false);
        }

        void SetServerTick(bool active)
        {
            if (_ticking == active || TimeManager == null)
                return;

            _ticking = active;
            if (active)
                TimeManager.OnTick += ServerTick;
            else
                TimeManager.OnTick -= ServerTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Show(_health.Value);
        }

        /// <summary>Host only.</summary>
        public void ApplyDamage(float amount, Vector3 point, Vector3 direction, int attackerClientId)
        {
            if (!IsServerStarted || IsDown || amount <= 0f)
                return;

            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (IsDown)
            {
                _standUpAt = Time.time + _standUpSeconds;
                SetServerTick(true);
            }

#if UNITY_EDITOR
            Debug.Log($"[Items] {name} took {amount:0.#} from client {attackerClientId}; {_health.Value:0.#} left.");
#endif
            ObserversHurt(amount);
        }

        void ServerTick()
        {
            if (!IsDown)
            {
                SetServerTick(false);
                return;
            }

            if (Time.time >= _standUpAt)
            {
                _health.Value = _maxHealth;
                SetServerTick(false);
            }
        }

        void OnHealthChanged(float previous, float next, bool asServer)
        {
            if (asServer && IsClientStarted)
                return;

            Show(next);
            if (next <= 0f && previous > 0f)
                _onKnockedDown.Invoke();
        }

        void Show(float health)
        {
            if (_bar != null)
            {
                Vector3 scale = _bar.localScale;
                scale.x = Mathf.Clamp01(health / _maxHealth);
                _bar.localScale = scale;
            }

            if (_tipPivot != null)
                _tipPivot.localRotation = Quaternion.Euler(health <= 0f ? TippedDegrees : 0f, 0f, 0f);
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversHurt(float amount)
        {
            Hurt?.Invoke(this, amount);
            _onHurt.Invoke(amount);
        }
    }
}

using System;
using FishNet.Managing.Timing;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    /// <summary>Host-timed venting smoke gadget that skitters across the floor when dropped. Design: docs/systems/ITEMS.md.</summary>
    public sealed class FireExtinguisher : ItemBehaviour
    {
        [Tooltip("Smoke time and how hard the gas pushes it about. Read at runtime, never written.")]
        [SerializeField] ExtinguisherProfile _profile;

        [Tooltip("The smoke. It must simulate in world space so the cloud stays where it was laid.")]
        [SerializeField] ParticleSystem _smoke;

        [Tooltip("Plays the vent loop while venting, routed to the item sound set's bus. Its own clip wins; empty uses the set's Vent Loop.")]
        [SerializeField] AudioSource _audio;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("The pin was pulled and the smoke started.")]
        [SerializeField] UnityEvent _onPinPulled;
        [Tooltip("It ran empty and the smoke stopped.")]
        [SerializeField] UnityEvent _onRanEmpty;

        const float TwoPi = Mathf.PI * 2f;

        readonly SyncVar<bool> _empty = new();
        readonly SyncVar<uint> _ventUntilTick = new();

        float _takeAwayAt;
        bool _sentAwayEmpty;
        bool _ticking;
        AudioClip _ownLoop;

        /// <summary>Every machine. The pin was pulled and the smoke started.</summary>
        public event Action<FireExtinguisher> PinPulled;
        /// <summary>Every machine. It ran empty.</summary>
        public event Action<FireExtinguisher> RanEmpty;

        public bool IsVenting => _ventUntilTick.Value != 0;
        public bool IsEmpty => _empty.Value;

        protected override void Awake()
        {
            base.Awake();
            _ventUntilTick.OnChange += OnVentChanged;
            if (_audio != null)
                _ownLoop = _audio.clip;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ItemAudio.Configure(_audio, Item.Sounds);
        }

        /// <summary>Stops smoke and loop audio when despawned or leaving the network.</summary>
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            ShowVenting(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _empty.Value = false;
            _ventUntilTick.Value = 0;
            _sentAwayEmpty = false;
            _takeAwayAt = 0f;
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
            ShowVenting(IsVenting);
        }

        public override void ServerUse(ItemCarrier user)
        {
            if (IsVenting || IsEmpty)
                return;

            _ventUntilTick.Value = TimeManager.Tick + TimeManager.TimeToTicks(_profile.SmokeSeconds, TickRounding.RoundUp);
            SetServerTick(true);
        }

        /// <summary>Out of play mid-vent (it fell out of the building): it stops venting and comes back full.</summary>
        public override void ServerOnSentAway(bool usedUp)
        {
            // An empty one lost out of the building stays empty: that is not a refill. A used-up
            // one comes back fresh after its definition's time, as designed.
            _sentAwayEmpty = !usedUp && _empty.Value;
            _ventUntilTick.Value = 0;
            SetServerTick(false);
        }

        public override void ServerOnReturned()
        {
            if (_sentAwayEmpty)
            {
                // Back where it started, still empty; it leaves play as an empty one does.
                _sentAwayEmpty = false;
                _takeAwayAt = Time.time + _profile.EmptyLingerSeconds;
                SetServerTick(true);
                return;
            }

            _empty.Value = false;
            _ventUntilTick.Value = 0;
            SetServerTick(false);
        }

        void ServerTick()
        {
            uint until = _ventUntilTick.Value;
            if (until == 0)
            {
                if (!_empty.Value)
                {
                    SetServerTick(false);
                    return;
                }
                TakeAwayWhenEmpty();
                return;
            }

            if (TimeManager.Tick >= until)
            {
                _empty.Value = true;
                _ventUntilTick.Value = 0;
                _takeAwayAt = Time.time + _profile.EmptyLingerSeconds;
                return;
            }

            if (Item.State == PickupState.InWorld)
                Skitter();
        }

        void TakeAwayWhenEmpty()
        {
            if (_empty.Value && Item.State == PickupState.InWorld && Time.time >= _takeAwayAt)
                Item.ServerUseUp();
        }

        void Skitter()
        {
            Rigidbody body = Item.Body;
            body.WakeUp();
            if (body.linearVelocity.sqrMagnitude < _profile.MaxSpeed * _profile.MaxSpeed)
                body.AddForce(-transform.forward * _profile.Thrust, ForceMode.Acceleration);
            body.AddTorque(Vector3.up * (_profile.Spin * TwoPi), ForceMode.Acceleration);
        }

        void OnVentChanged(uint previous, uint next, bool asServer)
        {
            // On the host this runs twice, as server then as client: answer once.
            if (!ShowsChange(asServer))
                return;

            bool venting = next != 0;
            ShowVenting(venting);

            if (venting && previous == 0)
            {
                PinPulled?.Invoke(this);
                _onPinPulled.Invoke();
            }
            else if (!venting && previous != 0 && _empty.Value)
            {
                ItemAudio.PlayAt(Item.Sounds, Item.Sounds?.VentEnd, transform.position);
                RanEmpty?.Invoke(this);
                _onRanEmpty.Invoke();
            }
        }

        void ShowVenting(bool venting)
        {
            ShowSmoke(venting);
            PlayHiss(venting);
        }

        void ShowSmoke(bool venting)
        {
            if (_smoke == null || venting == _smoke.isEmitting)
                return;

            if (venting)
                _smoke.Play(true);
            else
                _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void PlayHiss(bool venting)
        {
            if (_audio == null || venting == _audio.isPlaying)
                return;

            if (!venting)
            {
                _audio.Stop();
                return;
            }

            // Chosen each time it starts, so a clip added to the set later replaces the stand-in.
            _audio.clip = _ownLoop != null ? _ownLoop : Item.Sounds != null ? Item.Sounds.VentLoop : null;
            if (_audio.clip == null)
                return;
            _audio.volume = Item.Sounds != null ? Item.Sounds.Volume : 1f;
            _audio.loop = true;
            _audio.Play();
        }
    }
}

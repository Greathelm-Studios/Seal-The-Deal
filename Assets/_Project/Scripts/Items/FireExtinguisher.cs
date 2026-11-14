using System;
using FishNet.Managing.Timing;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// The fire extinguisher: pull the pin and it pours smoke that blocks sight, and it
    /// cannot be stopped. In the hands it lays smoke where the carrier walks; let go, the
    /// escaping gas drives it skittering round the floor into whoever is in the way.
    /// The host owns the timing and the motion; the smoke itself is drawn locally by every
    /// machine from the one synced tick, so no smoke is ever sent over the network.
    /// </summary>
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

        /// <summary>The tick the smoke stops. 0 while it is not venting.</summary>
        readonly SyncVar<uint> _ventUntilTick = new();
        readonly SyncVar<bool> _empty = new();

        float _takeAwayAt;
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

        /// <summary>
        /// Despawned (the match ended and the towers were rebuilt), or this machine left the game:
        /// the smoke and the loop stop here, whatever the last synced state said.
        /// </summary>
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            ShowVenting(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            TimeManager.OnTick += ServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= ServerTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ShowVenting(IsVenting);
        }

        // ---- Host ----------------------------------------------------------------------------

        public override void ServerUse(ItemCarrier user)
        {
            if (IsVenting || IsEmpty)
                return;

            _ventUntilTick.Value = TimeManager.Tick + TimeManager.TimeToTicks(_profile.SmokeSeconds, TickRounding.RoundUp);
        }

        /// <summary>Out of play mid-vent (it fell out of the building): it stops venting and comes back full.</summary>
        public override void ServerOnSentAway()
        {
            _ventUntilTick.Value = 0;
        }

        public override void ServerOnReturned()
        {
            _ventUntilTick.Value = 0;
            _empty.Value = false;
        }

        void ServerTick()
        {
            uint until = _ventUntilTick.Value;
            if (until == 0)
            {
                TakeAwayWhenEmpty();
                return;
            }

            if (TimeManager.Tick >= until)
            {
                _ventUntilTick.Value = 0;
                _empty.Value = true;
                _takeAwayAt = Time.time + _profile.EmptyLingerSeconds;
                return;
            }

            if (Item.State == PickupState.InWorld)
                Skitter();
        }

        /// <summary>An empty one lingers as something to throw, then leaves play once nobody is holding it.</summary>
        void TakeAwayWhenEmpty()
        {
            if (_empty.Value && Item.State == PickupState.InWorld && Time.time >= _takeAwayAt)
                Item.ServerUseUp();
        }

        /// <summary>The gas leaves the nozzle at the front, so the body is pushed backwards and spun.</summary>
        void Skitter()
        {
            Rigidbody body = Item.Body;
            body.WakeUp();
            if (body.linearVelocity.sqrMagnitude < _profile.MaxSpeed * _profile.MaxSpeed)
                body.AddForce(-transform.forward * _profile.Thrust, ForceMode.Acceleration);
            body.AddTorque(Vector3.up * (_profile.Spin * TwoPi), ForceMode.Acceleration);
        }

        // ---- Every machine -------------------------------------------------------------------

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
            else if (!venting && previous != 0)
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

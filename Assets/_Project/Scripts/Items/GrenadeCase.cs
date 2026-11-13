using System;
using CloseTheDeal.Combat;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// The grenade case: pick it up, arm it, throw it. It beeps faster and faster and then
    /// explodes wherever it is, the armer's hands included. The host owns the fuse and the
    /// explosion; everyone else is told the tick it goes off and beeps along locally.
    /// Not the objective briefcase: see docs/systems/ITEMS.md for how the two are told apart.
    /// </summary>
    public sealed class GrenadeCase : ItemBehaviour
    {
        [Tooltip("Fuse length, damage and the blast that throws players and furniture. Read at runtime, never written.")]
        [SerializeField] GrenadeCaseProfile _profile;

        [Tooltip("The warning light on the case. It flashes with each beep.")]
        [SerializeField] Renderer _light;

        [Tooltip("The ball of light shown where it explodes. Hidden until then.")]
        [SerializeField] Transform _flash;

        [Tooltip("Plays the beeps. Routed to the item sound set's mixer bus.")]
        [SerializeField] AudioSource _audio;

        [Tooltip("This case's own beep. Empty = the item sound set's beep.")]
        [SerializeField] AudioClip _beepClip;

        [Tooltip("This case's own explosion. Empty = the item sound set's.")]
        [SerializeField] AudioClip _boomClip;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("The case was armed.")]
        [SerializeField] UnityEvent _onArmed;
        [Tooltip("One beep of the fuse. Beeps speed up as it runs down.")]
        [SerializeField] UnityEvent _onBeep;
        [Tooltip("The case exploded.")]
        [SerializeField] UnityEvent _onExploded;

        const float LightOnSeconds = 0.05f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color LightOn = new(1f, 0.15f, 0.1f);
        static readonly Color LightOff = new(0.25f, 0.03f, 0.02f);

        /// <summary>The tick the case goes off. 0 while it is not armed.</summary>
        readonly SyncVar<uint> _detonateTick = new();

        MaterialPropertyBlock _block;
        float _nextBeepIn;
        float _lightOffIn;
        float _flashLeft;
        Vector3 _flashPoint;
        float _flashSize;

        /// <summary>Every machine. The case was armed.</summary>
        public event Action<GrenadeCase> Armed;
        /// <summary>Every machine. One beep of the fuse.</summary>
        public event Action<GrenadeCase> Beeped;
        /// <summary>Every machine. The case exploded at this world point.</summary>
        public event Action<GrenadeCase, Vector3> Exploded;

        public bool IsArmed => _detonateTick.Value != 0;

        /// <summary>Seconds until it goes off, by this machine's clock. 0 when not armed.</summary>
        public float FuseLeft
        {
            get
            {
                uint at = _detonateTick.Value;
                uint now = TimeManager.Tick;
                return at > now ? (float)TimeManager.TicksToTime(at - now) : 0f;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();
            _detonateTick.OnChange += OnDetonateTickChanged;
            if (_flash != null)
                _flash.gameObject.SetActive(false);
            enabled = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ItemAudio.Configure(_audio, Item.Sounds);
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

        // ---- Host ----------------------------------------------------------------------------

        public override void ServerUse(ItemCarrier user)
        {
            if (IsArmed)
                return;

            _detonateTick.Value = TimeManager.Tick + TimeManager.TimeToTicks(_profile.FuseSeconds, TickRounding.RoundUp);
        }

        public override void ServerOnReturned()
        {
            _detonateTick.Value = 0;
        }

        void ServerTick()
        {
            uint at = _detonateTick.Value;
            if (at != 0 && TimeManager.Tick >= at)
                ServerExplode();
        }

        void ServerExplode()
        {
            // Wherever the case is right now: on the floor, in the air, or in someone's hands.
            Vector3 point = Item.Body.worldCenterOfMass;
            _detonateTick.Value = 0;

            if (_profile.Blast == null)
            {
                Debug.LogError($"[Items] {name}'s Grenade Case profile has no Blast set, so it went off without throwing or hurting anything.");
                Item.ServerUseUp();
                return;
            }

            Knockback.Explode(point, _profile.Blast, ItemLayers.PlayerMask, ItemLayers.PropMask, KnockBuffer);
            ItemBlast.Damage(point, _profile.Blast.Radius, _profile.Damage, _profile.EdgeDamage, Item.LastHolderClientId);
            ObserversExploded(point, _profile.Blast.Radius);
            Item.ServerUseUp();
        }

        static readonly Collider[] KnockBuffer = new Collider[64];

        // ---- Every machine -------------------------------------------------------------------

        void OnDetonateTickChanged(uint previous, uint next, bool asServer)
        {
            // On the host this runs twice, as server then as client: answer once.
            if (!ShowsChange(asServer))
                return;

            bool armed = next != 0;
            if (armed && previous == 0)
            {
                _nextBeepIn = 0f;
                ItemAudio.PlayAt(Item.Sounds, Item.Sounds?.Arm, transform.position);
                Armed?.Invoke(this);
                _onArmed.Invoke();
            }

            if (!armed)
                ShowLight(false);

            enabled = armed || _flashLeft > 0f;
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversExploded(Vector3 point, float radius)
        {
            _flashPoint = point;
            _flashSize = radius * 2f;
            _flashLeft = _profile.FlashSeconds;
            if (_flash != null)
                _flash.gameObject.SetActive(true);
            enabled = true;

            ItemAudio.PlayAt(Item.Sounds, Pick(_boomClip, Item.Sounds?.Explosion), point);

            Exploded?.Invoke(this, point);
            _onExploded.Invoke();
        }

        /// <summary>Runs only while armed or flashing. Beeps speed up as the fuse runs down.</summary>
        void Update()
        {
            float dt = Time.deltaTime;

            if (IsArmed)
            {
                _nextBeepIn -= dt;
                if (_nextBeepIn <= 0f)
                {
                    float burnt = 1f - Mathf.Clamp01(FuseLeft / _profile.FuseSeconds);
                    _nextBeepIn = Mathf.Lerp(_profile.SlowBeepSeconds, _profile.FastBeepSeconds, burnt);
                    Beep();
                }

                if (_lightOffIn > 0f)
                {
                    _lightOffIn -= dt;
                    if (_lightOffIn <= 0f)
                        ShowLight(false);
                }
            }

            if (_flashLeft > 0f)
                StepFlash(dt);

            if (!IsArmed && _flashLeft <= 0f)
                enabled = false;
        }

        void Beep()
        {
            ShowLight(true);
            _lightOffIn = LightOnSeconds;
            AudioClip beep = Pick(_beepClip, Item.Sounds?.Beep);
            if (_audio != null && beep != null)
                _audio.PlayOneShot(beep, Item.Sounds != null ? Item.Sounds.Volume : 1f);

            Beeped?.Invoke(this);
            _onBeep.Invoke();
        }

        /// <summary>This case's own clip, else the shared set's. Null plays nothing.</summary>
        static AudioClip Pick(AudioClip own, AudioClip shared) => own != null ? own : shared;

        void ShowLight(bool on)
        {
            if (_light == null)
                return;

            _block.Clear();
            _light.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, on ? LightOn : LightOff);
            _light.SetPropertyBlock(_block);
        }

        /// <summary>The flash grows to the blast's size where it went off, whatever the case itself is doing by then.</summary>
        void StepFlash(float dt)
        {
            _flashLeft -= dt;
            if (_flash == null)
                return;

            if (_flashLeft <= 0f)
            {
                _flash.gameObject.SetActive(false);
                return;
            }

            float grown = 1f - _flashLeft / _profile.FlashSeconds;
            _flash.position = _flashPoint;
            _flash.localScale = Vector3.one * (_flashSize * Mathf.Sqrt(grown));
        }
    }
}

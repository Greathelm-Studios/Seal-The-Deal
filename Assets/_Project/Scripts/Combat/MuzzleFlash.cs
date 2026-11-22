using System;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>A muzzle's flash, light, smoke and sparks for one shot. Local only: each machine plays its own on every shot it sees.</summary>
    [DisallowMultipleComponent]
    public sealed class MuzzleFlash : MonoBehaviour
    {
        [Serializable]
        struct Layer
        {
            [Tooltip("One part of the flash: the fireball, the side flames, the smoke or the sparks.")]
            public ParticleSystem System;

            [Tooltip("How many pieces this part throws out on each shot, picked between the two numbers.")]
            public Vector2Int Count;
        }

        [Tooltip("The parts of the flash, each a burst on every shot.")]
        [SerializeField] Layer[] _layers = Array.Empty<Layer>();

        [Header("Light")]
        [Tooltip("The light the shot throws on the walls, hands and gun. Leave empty for none.")]
        [SerializeField] Light _light;

        [Tooltip("How bright the light is at the instant of the shot. It fades to nothing over the gun's flash time.")]
        [Range(0f, 20f)] [SerializeField] float _lightIntensity = 4f;

        [Tooltip("How much brighter or dimmer each shot's light may be, as a fraction. A little keeps a burst from strobing evenly.")]
        [Range(0f, 0.8f)] [SerializeField] float _lightJitter = 0.25f;

        float _lightLeft;
        float _lightTotal;
        float _lightPeak;
        bool _lightFresh;

        void Awake()
        {
            // Particles are only ever emitted by hand; the systems run with no emission so Emit always simulates.
            for (int i = 0; i < _layers.Length; i++)
            {
                ParticleSystem system = _layers[i].System;
                if (system == null)
                    continue;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = false;
                system.Play(false);
            }

            if (_light != null)
                _light.enabled = false;
        }

        void OnEnable()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].System != null && !_layers[i].System.isPlaying)
                    _layers[i].System.Play(false);
            }
        }

        /// <summary>Plays one shot's flash; the light lasts the given time in seconds.</summary>
        public void Play(float seconds)
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                Layer layer = _layers[i];
                if (layer.System == null)
                    continue;

                int count = UnityEngine.Random.Range(layer.Count.x, layer.Count.y + 1);
                if (count > 0)
                    layer.System.Emit(count);
            }

            if (_light == null || seconds <= 0f)
                return;

            _lightTotal = _lightLeft = seconds;
            _lightPeak = _lightIntensity * (1f + UnityEngine.Random.Range(-_lightJitter, _lightJitter));
            _light.intensity = _lightPeak;
            _light.enabled = true;
            _lightFresh = true;
        }

        void LateUpdate()
        {
            if (_lightLeft <= 0f)
                return;

            // The shot's own frame is drawn at full brightness.
            if (_lightFresh)
            {
                _lightFresh = false;
                return;
            }

            _lightLeft -= Time.deltaTime;
            if (_lightLeft <= 0f)
            {
                _light.enabled = false;
                return;
            }

            float left = _lightLeft / _lightTotal;
            _light.intensity = _lightPeak * left * left;
        }

        void OnDisable()
        {
            _lightLeft = 0f;
            if (_light != null)
                _light.enabled = false;
        }
    }
}

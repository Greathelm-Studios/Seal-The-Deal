using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Runs a target dummy back and forth along a line at a set speed, for practising on
    /// something that moves like a sprinting or air-strafing player. The host moves it once
    /// a tick; clients follow through the NetworkTransform on the same object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovingTarget : NetworkBehaviour
    {
        [Tooltip("How far the run goes from where the target was placed, in metres, as a direction and a length in the target's own space. (12, 0, 0) runs 12 m to its right and back.")]
        [SerializeField] Vector3 _run = new(12f, 0f, 0f);

        [Tooltip("How fast it runs, in metres per second. Players sprint at 9; a slide or a chain of jumps goes well past that.")]
        [Min(0f)] [SerializeField] float _speed = 9f;

        Vector3 _from;
        Vector3 _to;
        float _travelled;

        public float Speed => _speed;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _from = transform.position;
            _to = _from + transform.TransformDirection(_run);
            _travelled = 0f;
            TimeManager.OnTick += HostTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= HostTick;
        }

        void HostTick()
        {
            float length = Vector3.Distance(_from, _to);
            if (length <= 0f || _speed <= 0f)
                return;

            _travelled = Mathf.Repeat(_travelled + _speed * (float)TimeManager.TickDelta, length * 2f);
            transform.position = Vector3.Lerp(_from, _to, Mathf.PingPong(_travelled, length) / length);
        }
    }
}

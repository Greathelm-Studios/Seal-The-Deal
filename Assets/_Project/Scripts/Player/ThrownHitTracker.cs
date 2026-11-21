using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Temporary stand-in (deleted with <see cref="DevCarry"/>) that turns a thrown prop's impact on <see cref="HitDummy"/> into a <see cref="HitInfo"/> and restores thrower collisions.</summary>
    public sealed class ThrownHitTracker : MonoBehaviour
    {
        const float ClearTime = 0.3f;
        const float HitTime = 3f;

        Rigidbody _body;
        Collider _thrower;
        Collider[] _shapes;
        bool _reportsHit;
        float _clearAt, _endAt;
        // Sampled in FixedUpdate because OnCollisionEnter runs after the bounce alters linearVelocity.
        Vector3 _lastVelocity;

        public static void Arm(Rigidbody body, Collider thrower, Collider[] shapes, bool thrown)
        {
            if (!body.TryGetComponent(out ThrownHitTracker tracker))
                tracker = body.gameObject.AddComponent<ThrownHitTracker>();
            tracker._body = body;
            tracker._thrower = thrower;
            tracker._shapes = shapes;
            tracker._reportsHit = thrown;
            tracker._lastVelocity = body.linearVelocity;
            tracker._clearAt = Time.time + ClearTime;
            tracker._endAt = Time.time + (thrown ? HitTime : ClearTime);
        }

        void FixedUpdate()
        {
            if (_body != null)
                _lastVelocity = _body.linearVelocity;
        }

        void Update()
        {
            if (_shapes != null && Time.time >= _clearAt)
            {
                for (int i = 0; i < _shapes.Length; i++)
                {
                    if (_shapes[i] != null && _thrower != null)
                        Physics.IgnoreCollision(_thrower, _shapes[i], false);
                }
                _shapes = null;
            }
            if (Time.time >= _endAt && _shapes == null)
                Destroy(this);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_reportsHit || _body == null)
                return;
            HitDummy dummy = collision.collider.GetComponentInParent<HitDummy>();
            if (dummy == null)
                return;

            _reportsHit = false;
            float speed = _lastVelocity.magnitude;
            dummy.Hit(new HitInfo
            {
                Point = collision.GetContact(0).point,
                Direction = speed > 0f ? _lastVelocity / speed : transform.forward,
                Impulse = _body.mass * speed,
                Type = HitType.ThrownProp
            });
        }
    }
}

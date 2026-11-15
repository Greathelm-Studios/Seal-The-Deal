// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-14
//  Prompt    : Alex: "I wanna be able to pick up the blocks too" (and throw them at the test
//              dummy), with the pick-up built as a stand-in
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Rides on an object for a moment after DevCarry lets go of it: turns the first
//              time it strikes the test dummy into a hit of its weight times its speed, and
//              gives the player's capsule its collisions back. Its own component because the
//              collision callback only reaches components on the object that collided.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// <b>Stand-in, deleted with <see cref="DevCarry"/>.</b> Watches a just-released object:
    /// the first time a thrown one strikes the test dummy, the dummy is hit by it; after a
    /// moment the object may touch its thrower again; then this removes itself.
    /// </summary>
    /// <remarks>
    /// Host only, like <see cref="DevCarry"/>. WHY the impact is worked out from the object's
    /// weight and speed rather than read from the collision: the dummy's capsule does not move,
    /// so the collision reports an impulse that depends on bounciness and contact count; weight
    /// times speed is what the hit description means by a thrown object's impulse.
    /// </remarks>
    public sealed class ThrownHitTracker : MonoBehaviour
    {
        /// <summary>How long after release the object may not touch its thrower, seconds. WHY 0.3: long enough to leave the capsule at the slowest drop, short enough that a ball thrown at a wall can bounce back and hit you.</summary>
        const float ClearTime = 0.3f;

        /// <summary>How long a thrown object can still hit the dummy, seconds. WHY 3: a throw across the roof range lands well inside it; something rolling about later does not count.</summary>
        const float HitTime = 3f;

        Rigidbody _body;
        Collider _thrower;
        Collider[] _shapes;
        bool _reportsHit;
        float _clearAt, _endAt;
        /// <summary>The object's speed at the end of the last physics step, m/s: by the time the collision is reported it has already bounced.</summary>
        Vector3 _lastVelocity;

        /// <summary>
        /// Starts watching a just-released object, replacing any tracker it already has.
        /// </summary>
        /// <param name="body">The released object.</param>
        /// <param name="thrower">The capsule of the player who held it; its collisions with the object come back after a moment. May be null.</param>
        /// <param name="shapes">The object's colliders whose collisions with the thrower were switched off. May be null.</param>
        /// <param name="thrown">True for a throw (it may hit the dummy), false for setting it down.</param>
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

using System;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// What everything outside the motor reads: the animation state a rig feeds its Animator,
    /// and events for sound, effects and UI. Both are worked out once a frame from body state
    /// the host already sends, so they are right on the owner, the host and every watching
    /// client alike, and a replayed tick never fires an event twice.
    /// Also shows the lean and the crouch on a body that has no animation yet.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        /// <summary>Read once a frame to drive a character rig. Never written from outside.</summary>
        public PlayerAnimState AnimState { get; private set; }

        /// <summary>The player left the floor by jumping (not by walking off an edge).</summary>
        public event Action Jumped;

        /// <summary>The player touched down. The value is how fast they were falling, metres per second.</summary>
        public event Action<float> Landed;

        /// <summary>A climb or vault began.</summary>
        public event Action<MantleKind> MantleStarted;

        /// <summary>The player pressed jump at something that can't be climbed, and jumped instead. Says why.</summary>
        public event Action<MantleRefusal> MantleRefused;

        /// <summary>True on ducking, false on standing back up.</summary>
        public event Action<bool> CrouchChanged;

        /// <summary>True on reaching sprint speed, false on dropping out of it.</summary>
        public event Action<bool> SprintChanged;

        /// <summary>True on dropping into a slide, false when it ends.</summary>
        public event Action<bool> SlideChanged;

        /// <summary>A blast threw the player and took their control away.</summary>
        public event Action KnockedDown;

        Renderer[] _ownRenderers;
        UnityEngine.Rendering.ShadowCastingMode[] _authoredShadows;
        Vector3 _leanPivotRest;
        Vector3 _leanPivotScale;
        float _shownLean;
        float _shownCrouch;
        FeedbackCounters _seenCounters;

        void AwakeFeedback()
        {
            if (_profile != null && _profile.Sounds != null)
                gameObject.AddComponent<MovementSounds>().Init(this, _profile.Sounds);

            _ownRenderers = _cameraTarget.GetComponentsInChildren<Renderer>(true);
            _authoredShadows = new UnityEngine.Rendering.ShadowCastingMode[_ownRenderers.Length];
            for (int i = 0; i < _ownRenderers.Length; i++)
                _authoredShadows[i] = _ownRenderers[i].shadowCastingMode;

            if (_leanPivot == null)
                return;

            _leanPivotRest = _leanPivot.localPosition;
            _leanPivotScale = _leanPivot.localScale;
        }

        /// <summary>A fresh spawn starts with nothing to report: no landing, no jump left over from a pooled body's last life.</summary>
        void ResetFeedback()
        {
            _seenCounters = _counters;
            _shownLean = 0f;
            _shownCrouch = 0f;
            AnimState = BuildAnimState();
            PoseBody();
        }

        /// <summary>
        /// The first-person camera sits inside the body, so the owner sees only its shadow.
        /// A part the prefab sets to cast no shadow is hidden outright instead. Showing the
        /// body again puts back each part's own shadow setting from the prefab.
        /// </summary>
        void ShowOwnBody(bool visible)
        {
            for (int i = 0; i < _ownRenderers.Length; i++)
            {
                var authored = _authoredShadows[i];
                bool castsShadow = authored != UnityEngine.Rendering.ShadowCastingMode.Off;
                _ownRenderers[i].shadowCastingMode = visible || !castsShadow ? authored : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                _ownRenderers[i].forceRenderingOff = !visible && !castsShadow;
            }
        }

        void Update()
        {
            if (!IsSpawned)
                return;

            PlayerAnimState before = AnimState;
            AnimState = BuildAnimState();
            RaiseChanges(before, AnimState);
            EaseBodyPose();
        }

        PlayerAnimState BuildAnimState()
        {
            float sprintThreshold = (_profile.WalkSpeed + _profile.SprintSpeed) * 0.5f;
            return new PlayerAnimState(_rigidbody.linearVelocity, _rigidbody.rotation, _state, _ground.Found, _crouch, _lean, _mantle.Kind, _slide.Active, sprintThreshold);
        }

        void RaiseChanges(in PlayerAnimState before, in PlayerAnimState now)
        {
            RaiseCounted();

            if (now.Crouched != before.Crouched)
                CrouchChanged?.Invoke(now.Crouched);
            if (now.Sprinting != before.Sprinting)
                SprintChanged?.Invoke(now.Sprinting);
            if (now.Sliding != before.Sliding)
                SlideChanged?.Invoke(now.Sliding);
            if (now.Knocked && !before.Knocked)
                KnockedDown?.Invoke();
        }

        /// <summary>Jumps, landings, climbs and refusals are counted on the tick and carried by the host's state, so one is never missed between frames.</summary>
        void RaiseCounted()
        {
            if (_counters.Jumps != _seenCounters.Jumps)
                Jumped?.Invoke();
            if (_counters.Mantles != _seenCounters.Mantles)
                MantleStarted?.Invoke(_mantle.Kind);
            if (_counters.Refusals != _seenCounters.Refusals)
                MantleRefused?.Invoke(_counters.LastRefusal);
            if (_counters.Landings != _seenCounters.Landings)
                Landed?.Invoke(_counters.LastLandSpeed);
            _seenCounters = _counters;
        }

        /// <summary>Visual only: eases the hip pivot toward the ticked lean and crouch so every client sees them without stepping.</summary>
        void EaseBodyPose()
        {
            if (_leanPivot == null || (_shownLean == _lean && _shownCrouch == _crouch))
                return;

            _shownLean = Mathf.MoveTowards(_shownLean, _lean, _profile.LeanSpeed * Time.deltaTime);
            _shownCrouch = Mathf.MoveTowards(_shownCrouch, _crouch, _profile.CrouchRate * Time.deltaTime);
            PoseBody();
        }

        void PoseBody()
        {
            if (_leanPivot == null)
                return;

            // With no animation, a crouch is the whole body squashed toward the feet.
            float squash = _squashBodyWhenCrouched ? Mathf.Lerp(1f, CrouchedHeight / _standHeight, _shownCrouch) : 1f;
            float tilt = _profile.LeanAngle * Mathf.Deg2Rad;
            float headAbovePivot = (StandingEyeHeight - _leanPivotRest.y) * squash;
            // Slide whatever the tilt alone doesn't cover, so the head lands where the owner's eye is.
            float slide = _profile.LeanDistance - headAbovePivot * Mathf.Sin(tilt);

            Vector3 rest = new(_leanPivotRest.x, _leanPivotRest.y * squash, _leanPivotRest.z);
            _leanPivot.localPosition = rest + Vector3.right * (slide * _shownLean);
            _leanPivot.localRotation = Quaternion.Euler(0f, 0f, -_shownLean * _profile.LeanAngle);
            _leanPivot.localScale = new Vector3(_leanPivotScale.x, _leanPivotScale.y * squash, _leanPivotScale.z);
        }
    }
}

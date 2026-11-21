using System;
using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.Integration;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Profile;
using UnityEngine;

namespace CloseTheDeal.Onboarding
{
    /// <summary>One hint, ready to show.</summary>
    public readonly struct HintLine
    {
        public readonly string Id;
        public readonly string Text;
        public readonly float Seconds;

        public HintLine(string id, string text, float seconds)
        {
            Id = id;
            Text = text;
            Seconds = seconds;
        }
    }

    /// <summary>Triggers and queues one-time onboarding hints once a UI listener is subscribed. Design: docs/systems/ONBOARDING.md.</summary>
    [DisallowMultipleComponent]
    public sealed class FirstRunHints : MonoBehaviour
    {
        /// <summary>Show this hint now. Whoever draws it subscribes here.</summary>
        public static event Action<HintLine> HintShown;

        [Tooltip("What new players are told and when.")]
        [SerializeField] OnboardingHints _hints;

        [Tooltip("How often reception is checked for this player's arrival, in seconds, until the reception hint has been shown.")]
        [Range(0.1f, 2f)] [SerializeField] float _receptionCheckSeconds = 0.5f;

        readonly Queue<HintLine> _queue = new();
        MatchDirector _match;
        float _nextShow;
        float _nextReceptionCheck;
        bool _receptionSeen;
        string _receptionHintId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => HintShown = null;

        void OnEnable()
        {
            _receptionHintId = _hints != null && _hints.For(HintTrigger.ArrivedInReception) != null
                ? _hints.For(HintTrigger.ArrivedInReception).Id
                : null;
            _receptionSeen = string.IsNullOrEmpty(_receptionHintId) || ProfileStore.Current.HasSeen(_receptionHintId);
            FirmMember.OnNotice += OnNotice;
            Health.AnyDied += OnDied;
            ProfileStore.MatchRecorded += OnRecorded;
        }

        void OnDisable()
        {
            FirmMember.OnNotice -= OnNotice;
            Health.AnyDied -= OnDied;
            ProfileStore.MatchRecorded -= OnRecorded;
            if (_match == null)
                return;
            _match.OnPhaseChanged -= OnPhase;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match = null;
        }

        void Update()
        {
            if (_match == null && MatchDirector.Instance != null)
                Hook(MatchDirector.Instance);
            if (!_receptionSeen && Time.unscaledTime >= _nextReceptionCheck)
                CheckReception();
            if (_queue.Count > 0 && Time.unscaledTime >= _nextShow)
                ShowNext();
        }

        void Hook(MatchDirector match)
        {
            _match = match;
            _match.OnPhaseChanged += OnPhase;
            _match.OnBriefcasePickedUp += OnPickedUp;
        }

        void CheckReception()
        {
            _nextReceptionCheck = Time.unscaledTime + _receptionCheckSeconds;
            if (string.IsNullOrEmpty(_receptionHintId) || ProfileStore.Current.HasSeen(_receptionHintId))
            {
                _receptionSeen = true;
                return;
            }
            FirmMember me = FirmMember.Local;
            if (me != null && LobbyRoom.Instance != null && LobbyRoom.Instance.Contains(me.Position))
                Offer(HintTrigger.ArrivedInReception);
        }

        void OnPhase(MatchPhase previous, MatchPhase next)
        {
            if (next == MatchPhase.Running)
                Offer(HintTrigger.MatchRunning);
        }

        void OnPickedUp(int firm)
        {
            FirmMember me = FirmMember.Local;
            if (me == null)
                return;
            Offer(_match.IsCarrying(me) ? HintTrigger.CarryingBriefcase : firm != me.Firm ? HintTrigger.RivalCarrying : (HintTrigger?)null);
        }

        void OnDied(Health health, DamageInfo info)
        {
            FirmMember me = FirmMember.Local;
            if (me != null && health != null && health.gameObject == me.gameObject)
                Offer(HintTrigger.Died);
        }

        void OnNotice(MatchNotice notice)
        {
            if (notice == MatchNotice.FellOut)
                Offer(HintTrigger.FellOut);
        }

        void OnRecorded(MatchSummary summary, PlayerProfile profile) => Offer(HintTrigger.MatchRecorded);

        void Offer(HintTrigger? when)
        {
            if (when == null || _hints == null || HintShown == null)
                return;
            OnboardingHints.Hint hint = _hints.For(when.Value);
            if (hint == null || string.IsNullOrEmpty(hint.Id) || ProfileStore.Current.HasSeen(hint.Id) || IsQueued(hint.Id))
                return;

            string text = hint.Text.Replace("{interact}", FirmMember.InteractKeyLabel).Replace("{drop}", FirmMember.DropKeyLabel);
            _queue.Enqueue(new HintLine(hint.Id, text, hint.Seconds));
        }

        bool IsQueued(string id)
        {
            foreach (HintLine line in _queue)
            {
                if (line.Id == id)
                    return true;
            }

            return false;
        }

        void ShowNext()
        {
            HintLine line = _queue.Dequeue();
            if (HintShown == null)
            {
                _queue.Clear();
                return;
            }

            HintShown.Invoke(line);
            ProfileStore.MarkHintSeen(line.Id);
            if (line.Id == _receptionHintId)
                _receptionSeen = true;
            _nextShow = Time.unscaledTime + line.Seconds + _hints.GapSeconds;
        }
    }
}

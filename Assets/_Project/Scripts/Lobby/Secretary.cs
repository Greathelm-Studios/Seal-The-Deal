using System;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The secretary at the reception desk. Reads lines as subtitles when someone arrives,
    /// readies up or signs in, and when the run starts; turns her head toward whoever she is
    /// addressing. There is no voice audio yet: every line raises LineSpoken, so a voice can
    /// attach later by moment. Local presentation only; what triggers her is synced by the roster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Secretary : MonoBehaviour
    {
        /// <summary>A line was spoken on this machine: the moment (for picking a clip), the finished text and how long it stays up in seconds.</summary>
        public static event Action<SecretaryMoment, string, float> LineSpoken;

        [Tooltip("What she says.")]
        [SerializeField] SecretaryLines _lines;

        [Tooltip("The firm names, for {firm} in a line.")]
        [SerializeField] LobbyRules _rules;

        [Tooltip("What turns when she addresses someone: a child pivot made for this, never her placed body.")]
        [SerializeField] Transform _turnPivot;

        [Tooltip("How far she turns toward the person she addresses, in degrees either way. Small: she is busy.")]
        [Range(0f, 90f)] [SerializeField] float _maxTurn = 35f;

        [Tooltip("How fast she turns, in degrees per second.")]
        [Range(10f, 720f)] [SerializeField] float _turnSpeed = 180f;

        [Tooltip("Seconds after a line before she turns back to her screen.")]
        [Range(0f, 10f)] [SerializeField] float _lookHold = 3f;

        const float MinimumSeconds = 2.5f;

        LobbyRoster _roster;
        int _lastPick = -1;
        SecretaryMoment _lastMoment;
        float _targetYaw;
        float _yaw;
        float _lookUntil;
        Quaternion _pivotRest = Quaternion.identity;

        void Awake()
        {
            if (_turnPivot != null)
                _pivotRest = _turnPivot.localRotation;
        }

        void Update()
        {
            Hook();
            Turn();
        }

        /// <summary>The roster is a networked scene object; it may start after us, so we hook up when it is there.</summary>
        void Hook()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == _roster)
                return;

            if (_roster != null)
                Unhook();
            _roster = roster;
            if (_roster == null)
                return;

            _roster.OnArrived += OnArrived;
            _roster.OnReadyChanged += OnReadyChanged;
            _roster.OnFirmChosen += OnFirmChosen;
            _roster.OnFirmRefused += OnFirmRefused;
            _roster.OnStartBlocked += OnStartBlocked;
            ReceptionDesk.OnConfirmArmed += OnConfirmArmed;
            _roster.OnRunStarting += OnRunStarting;
        }

        void Unhook()
        {
            _roster.OnArrived -= OnArrived;
            _roster.OnReadyChanged -= OnReadyChanged;
            _roster.OnFirmChosen -= OnFirmChosen;
            _roster.OnFirmRefused -= OnFirmRefused;
            _roster.OnStartBlocked -= OnStartBlocked;
            ReceptionDesk.OnConfirmArmed -= OnConfirmArmed;
            _roster.OnRunStarting -= OnRunStarting;
        }

        void OnDestroy()
        {
            if (_roster != null)
                Unhook();
        }

        // Reception lines only: during a run she stays quiet, and the reset back to reception
        // (every ready cleared at once) is not a string of withdrawals.
        bool InReception => _roster != null && !_roster.RunInProgress;

        void OnArrived(LobbyMember member)
        {
            if (InReception)
                Say(SecretaryMoment.Arrival, member.Name, member.Firm);
        }

        void OnReadyChanged(LobbyMember member, bool ready)
        {
            if (!InReception)
                return;
            if (ready)
                Say(SecretaryMoment.Ready, member.Name, member.Firm);
            else if (_roster.TryGetLocal(out LobbyMember me) && me.ClientId == member.ClientId)
                Say(SecretaryMoment.NotReady, member.Name, member.Firm);
        }
        void OnFirmChosen(int firm) => Say(SecretaryMoment.FirmChosen, LocalName(), firm);
        void OnFirmRefused(int firm) => Say(SecretaryMoment.FirmRefused, LocalName(), firm);
        void OnStartBlocked(StartBlock block) => Say(SecretaryMoment.StartBlocked, LocalName(), 0);
        // The short-handed lines are ordered: the first for an empty firm, the second for uneven firms.
        void OnConfirmArmed(StartBlock block) => Say(SecretaryMoment.ShortHanded, LocalName(), _roster.SmallerFirm(), block == StartBlock.NeedBothFirms ? 0 : 1);
        void OnRunStarting() => Say(SecretaryMoment.Start, LocalName(), 0);

        string LocalName() => _roster != null && _roster.TryGetLocal(out LobbyMember me) ? me.Name : string.Empty;

        void Say(SecretaryMoment moment, string name, int firm, int fixedPick = -1)
        {
            string[] options = _lines != null ? _lines.For(moment) : null;
            if (options == null || options.Length == 0)
                return;

            int pick = fixedPick >= 0 && fixedPick < options.Length ? fixedPick : UnityEngine.Random.Range(0, options.Length);
            if (options.Length > 1 && moment == _lastMoment && pick == _lastPick)
                pick = (pick + 1) % options.Length; // Never the same line twice running.
            _lastPick = pick;
            _lastMoment = moment;

            // Built only when she speaks, a few times a minute at most.
            string text = options[pick].Replace("{name}", name).Replace("{firm}", _rules.FirmName(firm));
            LookAtLocalPlayer();
            LineSpoken?.Invoke(moment, text, ReadingSeconds(text));
        }

        float ReadingSeconds(string text)
        {
            int words = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                    words++;
            }

            return Mathf.Max(MinimumSeconds, words * _lines.SecondsPerWord);
        }

        /// <summary>
        /// She addresses the camera: everyone watching reads the line as said to them, and the
        /// head turn sells it. Turning toward the actual speaker would need their position, which
        /// is fine to add once a rigged head exists.
        /// </summary>
        void LookAtLocalPlayer()
        {
            if (_turnPivot == null || PlayerMotor.Local == null)
                return;

            Vector3 to = PlayerMotor.Local.transform.position - _turnPivot.position;
            to = _turnPivot.parent != null ? _turnPivot.parent.InverseTransformDirection(to) : to;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            _targetYaw = Mathf.Clamp(Mathf.DeltaAngle(0f, yaw), -_maxTurn, _maxTurn);
            _lookUntil = Time.time + _lookHold;
        }

        void Turn()
        {
            if (_turnPivot == null)
                return;

            float goal = Time.time < _lookUntil ? _targetYaw : 0f;
            if (Mathf.Approximately(_yaw, goal))
                return;

            _yaw = Mathf.MoveTowardsAngle(_yaw, goal, _turnSpeed * Time.deltaTime);
            _turnPivot.localRotation = _pivotRest * Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}

using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>The floor race's own HUD: floor and room, the current objective, the cash toast and the floor-change screen. Local to each machine.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorRaceHud : MonoBehaviour
    {
        enum Goal : byte
        {
            None, WaitForHost, FindCase, GrabCase, ReachLift, BoardWaiting, ReachOffice, EscortToLift, EscortToOffice,
            TakeItBack, HeadStartInside, HeadStartOutside
        }

        [Header("Parts (wired by the scene builder)")]
        [SerializeField] FloorRaceDirector _race;
        [SerializeField] MatchDirector _match;
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _floorText;
        [SerializeField] TMP_Text _roomText;
        [SerializeField] TMP_Text _goalText;
        [SerializeField] CanvasGroup _cashGroup;
        [SerializeField] TMP_Text _cashText;
        [SerializeField] CanvasGroup _transitionGroup;
        [SerializeField] TMP_Text _transitionTitle;
        [SerializeField] TMP_Text _transitionLine;

        [Header("Timing")]
        [Tooltip("Seconds the floor-change screen takes to fade in once the lift doors start closing, and to fade out on arrival.")]
        [Range(0.1f, 2f)] [SerializeField] float _fadeSeconds = 0.5f;

        [Tooltip("Seconds the screen dips for a player riding the lift as the car arrives. It hides the instant they are moved into the next floor's car. Riders see nothing else: they stay in the closed car for the whole ride.")]
        [Range(0.05f, 1f)] [SerializeField] float _arriveBlinkSeconds = 0.2f;

        [Tooltip("Seconds the cash line stays on screen after a floor is won.")]
        [Range(1f, 10f)] [SerializeField] float _cashSeconds = 4f;

        [Tooltip("How often the room name under the floor number is refreshed, in seconds. It only changes when the player walks into another room.")]
        [Range(0.1f, 1f)] [SerializeField] float _roomRefreshSeconds = 0.25f;

        Goal _goal = Goal.None;
        int _goalA = -1, _goalB = -1;
        int _shownFloor = -1;
        string _shownRoom;
        float _roomAt;
        float _cashUntil;
        RaceStage _titleStage = (RaceStage)255;
        int _titleFloor = -1;
        byte _titleFirm = byte.MaxValue;
        bool _riding;

        void OnEnable()
        {
            if (_race == null || _match == null || _root == null)
            {
                enabled = false;
                return;
            }

            _race.OnCashEarned += OnCashEarned;
            _cashGroup.alpha = 0f;
            _transitionGroup.alpha = 0f;
        }

        void OnDisable()
        {
            if (_race != null)
                _race.OnCashEarned -= OnCashEarned;
        }

        void OnCashEarned(int amount, int total, int floor)
        {
            _cashText.SetText("+{0} CASH   TOTAL {1}", amount, total);
            _cashUntil = Time.unscaledTime + _cashSeconds;
            _cashGroup.alpha = 1f;
        }

        void Update()
        {
            bool live = _match.IsLive && _race.Current != null;
            if (_root.activeSelf != live)
                _root.SetActive(live);
            if (!live)
                return;

            FloorRaceState state = _race.State;
            if (state.Floor != _shownFloor)
            {
                _shownFloor = state.Floor;
                _floorText.SetText("FLOOR {0} / {1}", state.Floor, _race.FloorCount);
            }

            RefreshRoom();
            RefreshGoal(state);
            RefreshTransition(state);
            if (_cashGroup.alpha > 0f && Time.unscaledTime >= _cashUntil)
                _cashGroup.alpha = 0f;
        }

        void RefreshRoom()
        {
            if (Time.unscaledTime < _roomAt)
                return;

            _roomAt = Time.unscaledTime + _roomRefreshSeconds;
            PlayerMotor local = PlayerMotor.Local;
            string room = local != null ? _race.RoomNameAt(local.transform.position) : string.Empty;
            if (ReferenceEquals(room, _shownRoom))
                return;

            _shownRoom = room;
            _roomText.text = room.ToUpperInvariant();
        }

        void RefreshGoal(FloorRaceState state)
        {
            FirmMember local = FirmMember.Local;
            Goal goal = Goal.None;
            int a = 0, b = 0;
            if (local != null)
                goal = GoalFor(local, state, out a, out b);
            if (goal == _goal && a == _goalA && b == _goalB)
                return;

            _goal = goal;
            _goalA = a;
            _goalB = b;
            switch (goal)
            {
                case Goal.WaitForHost: _goalText.text = "Waiting for the host to start"; break;
                case Goal.FindCase: _goalText.text = "Find the briefcase"; break;
                case Goal.GrabCase: _goalText.text = "Grab the briefcase"; break;
                case Goal.ReachLift: _goalText.text = "Find the lift and get your firm aboard"; break;
                case Goal.BoardWaiting: _goalText.SetText("Waiting for your firm: {0} of {1} aboard", a, b); break;
                case Goal.ReachOffice: _goalText.text = "Take it into the manager's office"; break;
                case Goal.EscortToLift: _goalText.text = "Get in the lift with the briefcase"; break;
                case Goal.EscortToOffice: _goalText.text = "Get the briefcase to the manager's office"; break;
                case Goal.TakeItBack: _goalText.text = "Take the briefcase back"; break;
                case Goal.HeadStartInside: _goalText.SetText("Doors open in {0}", a); break;
                case Goal.HeadStartOutside: _goalText.SetText("They arrive in {0}. Get ready", a); break;
                default: _goalText.text = string.Empty; break;
            }
        }

        Goal GoalFor(FirmMember local, FloorRaceState state, out int a, out int b)
        {
            a = 0;
            b = 0;
            MatchPhase phase = _match.Phase;
            if (phase == MatchPhase.Waiting)
                return Goal.WaitForHost;
            if (phase == MatchPhase.Ended)
                return Goal.None;
            if (state.Stage == RaceStage.HeadStart)
            {
                a = _race.HeadStartSecondsLeft;
                return local.Firm == state.LiftFirm ? Goal.HeadStartInside : Goal.HeadStartOutside;
            }

            BriefcaseStatus status = _match.CaseStatus;
            bool top = _race.IsTopFloor;
            if (status.State != BriefcaseState.Carried)
                return _race.BriefcaseHidden(status) ? Goal.FindCase : Goal.GrabCase;
            if (status.CarrierFirm != local.Firm)
                return Goal.TakeItBack;
            if (top)
                return _match.IsCarrying(local) ? Goal.ReachOffice : Goal.EscortToOffice;

            _race.CountAboard(local.Firm, out a, out b);
            FirmMember carrier = _match.Carrier();
            if (carrier != null && _race.InExitLift(carrier.Position) && a < b)
                return Goal.BoardWaiting;
            a = 0;
            b = 0;
            return _match.IsCarrying(local) ? Goal.ReachLift : Goal.EscortToLift;
        }

        void RefreshTransition(FloorRaceState state)
        {
            float since = _race.StageSeconds;
            FirmMember rider = FirmMember.Local;
            if (state.Stage == RaceStage.Departing)
                _riding = rider != null && rider.Firm == state.LiftFirm && _race.InExitLift(rider.Position);
            else if (state.Stage == RaceStage.Playing || state.Stage == RaceStage.Idle)
                _riding = false;

            float alpha = state.Stage switch
            {
                RaceStage.Departing => _riding ? 0f : Mathf.Clamp01((since - _race.Profile.DoorSeconds * 0.5f) / _fadeSeconds),
                RaceStage.Building => _riding ? Mathf.Clamp01((since - _race.RideLength(state)) / _arriveBlinkSeconds + 1f) : 1f,
                RaceStage.HeadStart => 1f - Mathf.Clamp01(since / (_riding ? _arriveBlinkSeconds : _fadeSeconds)),
                _ => 0f
            };
            if (!Mathf.Approximately(alpha, _transitionGroup.alpha))
                _transitionGroup.alpha = alpha;
            if (alpha <= 0f || (state.Stage == _titleStage && state.Floor == _titleFloor && state.LiftFirm == _titleFirm))
                return;

            _titleStage = state.Stage;
            _titleFloor = state.Floor;
            _titleFirm = state.LiftFirm;
            int arriving = state.Stage == RaceStage.Departing ? state.Floor + 1 : state.Floor;
            _transitionTitle.SetText("FLOOR {0}", arriving);
            if (state.LiftFirm == MatchDirector.NoFirm)
            {
                _transitionLine.text = "The briefcase is somewhere on this floor.";
                return;
            }

            FirmMember local = FirmMember.Local;
            bool ours = local != null && local.Firm == state.LiftFirm;
            _transitionLine.text = ours
                ? "Your firm took the briefcase up. The other firm gets there first."
                : _match.FirmName(state.LiftFirm) + " took the briefcase up. You get there first: be ready.";
        }
    }
}

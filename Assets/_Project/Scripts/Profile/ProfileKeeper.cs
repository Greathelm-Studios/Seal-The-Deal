using CloseTheDeal.MatchMode;
using CloseTheDeal.Records;
using UnityEngine;

namespace CloseTheDeal.Profile
{
    /// <summary>
    /// Loads this player's profile when the scene starts and adds each finished match to it, from
    /// the host's final record only. Sits beside MatchRecords. Local: nothing here is networked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileKeeper : MonoBehaviour
    {
        MatchRecords _records;

        void Start()
        {
            _ = ProfileStore.Current;
        }

        void Update()
        {
            if (_records != null || MatchRecords.Instance == null)
                return;

            _records = MatchRecords.Instance;
            _records.FinalRecord += OnFinalRecord;
            enabled = false;
        }

        void OnDestroy()
        {
            if (_records != null)
                _records.FinalRecord -= OnFinalRecord;
        }

        static void OnFinalRecord(MatchFinal final)
        {
            MatchSummary summary = ProfileStore.RecordMatch(final, MatchDirector.NoFirm);
            Debug.Log($"[Profile] Match recorded: {summary.Outcome}, {summary.Kills} kills, {summary.Deaths} deaths, {summary.CarrySeconds} s with the briefcase. Career: {ProfileStore.Get(ProfileStat.MatchesPlayed)} matches, {ProfileStore.Get(ProfileStat.Wins)} wins.");
        }
    }
}

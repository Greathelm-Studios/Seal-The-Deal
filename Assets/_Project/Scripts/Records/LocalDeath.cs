using System;
using CloseTheDeal.Combat;
using CloseTheDeal.MatchMode;
using UnityEngine;

namespace CloseTheDeal.Records
{
    /// <summary>What a death screen needs to say about this player going down.</summary>
    public readonly struct DeathReport
    {
        /// <summary>Killed by a rival, knocked out of a window by one, fell, or died to the world.</summary>
        public readonly KillKind Kind;
        /// <summary>Who gets the credit, or -1. Name: MatchRecords.NameOf.</summary>
        public readonly int KillerClientId;
        /// <summary>Seconds until this player is back; 0 for a fall (they are put back at once).</summary>
        public readonly float RespawnSeconds;
        /// <summary>Where they come back: "Floor 4" for a death, the checkpoint's name for a fall.</summary>
        public readonly string ComesBackOn;
        public DeathReport(KillKind kind, int killerClientId, float respawnSeconds, string comesBackOn)
        {
            Kind = kind;
            KillerClientId = killerClientId;
            RespawnSeconds = respawnSeconds;
            ComesBackOn = comesBackOn;
        }
    }

    /// <summary>Local player's death and fall-out summary events for the death HUD card. Design: docs/systems/RECORDS.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalDeath : MonoBehaviour
    {
        /// <summary>This player went down. Raised once per death or fall, on this machine only.</summary>
        public static event Action<DeathReport> Down;

        /// <summary>This player is back (respawned after a death, or set down after a fall).</summary>
        public static event Action Back;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Down = null;
            Back = null;
        }

        MatchRecords _records;

        void Awake() => Health.AnyRespawned += OnRespawned;

        void OnDestroy()
        {
            Health.AnyRespawned -= OnRespawned;
            if (_records != null)
                _records.Kill -= OnKill;
            _records = null;
        }

        void Update()
        {
            if (_records != null || MatchRecords.Instance == null)
                return;
            _records = MatchRecords.Instance;
            _records.Kill += OnKill;
            enabled = false;
        }

        static FirmMember Me => FirmMember.Local;

        void OnKill(KillEntry entry)
        {
            if (Me == null || Me.Owner == null || entry.VictimClientId != Me.Owner.ClientId)
                return;

            bool fell = entry.Kind == KillKind.Fell || entry.Kind == KillKind.KnockedOut;
            float seconds = fell ? 0f : Me.GetComponent<Health>().RespawnSeconds;
            Down?.Invoke(new DeathReport(entry.Kind, entry.KillerClientId, seconds, string.Empty));
            if (fell)
                Back?.Invoke();
        }

        static void OnRespawned(Health health)
        {
            if (Me != null && health.gameObject == Me.gameObject)
                Back?.Invoke();
        }
    }
}

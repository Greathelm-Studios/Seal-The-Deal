using System;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Payload raised by <see cref="HitFeed"/> when a body reacts to a hit.</summary>
    public readonly struct HitEvent
    {
        public readonly HitInfo Hit;
        public readonly Transform Victim;

        public HitEvent(in HitInfo hit, Transform victim)
        {
            Hit = hit;
            Victim = victim;
        }
    }

    /// <summary>Client-local notification bus raised on every machine after a body begins reacting to a hit.</summary>
    public static class HitFeed
    {
        public static event Action<HitEvent> Struck;

        /// <summary>Notifies visual/audio listeners of a hit on <paramref name="victim"/>.</summary>
        public static void Raise(in HitInfo hit, Transform victim)
        {
            if (victim == null)
                return;

            Struck?.Invoke(new HitEvent(hit, victim));
        }

        // Clears static listeners when entering Play with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Struck = null;
        }
    }
}

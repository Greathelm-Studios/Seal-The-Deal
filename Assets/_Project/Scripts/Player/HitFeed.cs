// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "add hit reactions when hit by guns/melee weapons for the players ...
//              create a blood splatter shader ... that paints around objects and ontop of them
//              when hit by these attacks"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The one place every machine hears "a body was just struck", so effects that
//              only decorate a hit (blood, later sound and sparks) can follow it without the
//              hit code knowing they exist.
// -----------------------------------------------------------------------------
using System;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>One hit that has just landed on one body, as told to things that decorate it.</summary>
    public readonly struct HitEvent
    {
        /// <summary>The hit itself: where, which way, how hard, what kind.</summary>
        public readonly HitInfo Hit;

        /// <summary>The root of the body that was struck: a player's root or the test dummy's. Never null when raised.</summary>
        public readonly Transform Victim;

        public HitEvent(in HitInfo hit, Transform victim)
        {
            Hit = hit;
            Victim = victim;
        }
    }

    /// <summary>
    /// Raised on every machine, once per hit on a body, after the body has started its reaction.
    /// </summary>
    /// <remarks>
    /// <b>Ownership (AGENTS.md §3):</b> nothing here is synced and nothing here decides anything.
    /// For players the host decides the hit and tells every machine; each machine then raises
    /// this locally. Listeners must only draw or play things: a listener that changes gameplay
    /// from here would be a client deciding an outcome.
    /// WHY a static event and not a call into each effect: the hit code belongs to the body,
    /// and effects come and go; with this the body never needs to know which exist.
    /// </remarks>
    public static class HitFeed
    {
        /// <summary>A body was struck on this machine's screen. Unsubscribe when the listener is disabled.</summary>
        public static event Action<HitEvent> Struck;

        /// <summary>Tells the listeners. Called by whatever applied the hit to the body, on every machine.</summary>
        public static void Raise(in HitInfo hit, Transform victim)
        {
            if (victim == null)
                return;

            Struck?.Invoke(new HitEvent(hit, victim));
        }

        // A static event outlives Play Mode when domain reload is off; clear it so a stopped
        // session's listeners are not called in the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Struck = null;
        }
    }
}

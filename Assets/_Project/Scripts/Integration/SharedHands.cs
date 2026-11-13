using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// One pair of hands per player, shared by the briefcase and carried items: holding one makes
    /// taking the other fail as "hands full", so G only ever drops one thing. When both are in
    /// reach the briefcase wins the interact key, and a lobby desk, stand or mirror wins over
    /// both. Guns go to the weapon slots, not the hands: a briefcase carrier can still take and
    /// fire one, but a player carrying an item cannot fire, and nobody fires in the lobby.
    /// Every rule here runs on every machine from synced state; the host's checks are the ones
    /// that decide. Nothing is sent. Design: docs/systems/INTEGRATION.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharedHands : MonoBehaviour
    {
        void OnEnable()
        {
            ItemCarrier.HandsTakenElsewhere = BriefcaseHoldsHands;
            ItemCarrier.InteractClaimedElsewhere = SomethingElseAnswersInteract;
            MatchDirector.HandsFull = ItemHoldsHands;
            WeaponHolder.FireHeld = FireIsHeld;
        }

        void OnDisable()
        {
            // Static hooks outlive the scene: take ours back so a later scene starts clean.
            if (ItemCarrier.HandsTakenElsewhere == BriefcaseHoldsHands)
                ItemCarrier.HandsTakenElsewhere = null;
            if (ItemCarrier.InteractClaimedElsewhere == SomethingElseAnswersInteract)
                ItemCarrier.InteractClaimedElsewhere = null;
            if (MatchDirector.HandsFull == ItemHoldsHands)
                MatchDirector.HandsFull = null;
            if (WeaponHolder.FireHeld == FireIsHeld)
                WeaponHolder.FireHeld = null;
        }

        /// <summary>The briefcase is in this player's hands, and the item is not a gun (guns go to the weapon slots).</summary>
        static bool BriefcaseHoldsHands(ItemCarrier carrier, Pickup item)
        {
            MatchDirector match = MatchDirector.Instance;
            if (match == null || item.Definition.Kind == ItemKind.Gun)
                return false;

            FirmMember member = PlayerParts.MemberOf(carrier);
            return member != null && match.IsCarrying(member);
        }

        /// <summary>Owner only: the lobby has something under the crosshair, or the briefcase would answer F.</summary>
        static bool SomethingElseAnswersInteract(ItemCarrier carrier)
        {
            LobbyInteractor lobby = LobbyInteractor.Instance;
            if (lobby != null && lobby.Target != null)
                return true;

            MatchDirector match = MatchDirector.Instance;
            FirmMember member = PlayerParts.MemberOf(carrier);
            return match != null && member != null && match.PromptFor(member) == MatchPrompt.PickUpBriefcase;
        }

        /// <summary>A carried item fills this player's hands, so the briefcase is not theirs to take.</summary>
        static bool ItemHoldsHands(FirmMember member)
        {
            ItemCarrier carrier = PlayerParts.CarrierOf(member);
            return carrier != null && carrier.Held != null;
        }

        /// <summary>Full hands can't shoot (the throw is the fire button), and the lobby is a gun-free room.</summary>
        static bool FireIsHeld(WeaponHolder holder)
        {
            ItemCarrier carrier = PlayerParts.CarrierOf(holder);
            if (carrier != null && carrier.Held != null)
                return true;

            LobbyRoom room = LobbyRoom.Instance;
            return room != null && room.Contains(holder.transform.position);
        }
    }
}

using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>Arbitrates shared hand ownership between lobby targets, the briefcase, items, and weapons. Design: docs/systems/INTEGRATION.md.</summary>
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

        static bool BriefcaseHoldsHands(ItemCarrier carrier, Pickup item)
        {
            MatchDirector match = MatchDirector.Instance;
            if (match == null || item.Definition.Kind == ItemKind.Gun)
                return false;

            FirmMember member = PlayerParts.MemberOf(carrier);
            return member != null && match.IsCarrying(member);
        }

        static bool SomethingElseAnswersInteract(ItemCarrier carrier)
        {
            LobbyInteractor lobby = LobbyInteractor.Instance;
            if (lobby != null && lobby.Target != null)
                return true;

            MatchDirector match = MatchDirector.Instance;
            FirmMember member = PlayerParts.MemberOf(carrier);
            return match != null && member != null && match.PromptFor(member) == MatchPrompt.PickUpBriefcase;
        }

        static bool ItemHoldsHands(FirmMember member)
        {
            ItemCarrier carrier = PlayerParts.CarrierOf(member);
            return carrier != null && carrier.Held != null;
        }

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

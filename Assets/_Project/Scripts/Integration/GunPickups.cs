using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.MatchMode;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// Host only. A gun taken from the level goes straight into the player's weapon slots and the
    /// pickup leaves play, so a gun never sits in the hands:
    /// - a gun the player already has: that gun's magazine is refilled;
    /// - a free slot: the gun goes in it;
    /// - both slots full: it replaces the gun in hand, and that gun drops at their feet as a pickup.
    /// The weapon holder's synced slots carry the result to the owner and everyone else.
    /// Owner: host. Synced: the holder's slot values and the pickup's despawn. Design: docs/systems/INTEGRATION.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunPickups : MonoBehaviour
    {
        [Tooltip("Which gun pickup gives which weapon, and what drops when a gun is swapped out. Read at runtime, never written.")]
        [SerializeField] GunPickupMap _map;

        [Tooltip("How fast a swapped-out gun leaves the player, in metres per second: a gentle toss forward so it lands in sight, not under their feet.")]
        [Min(0f)] [SerializeField] float _swapTossSpeed = 2f;

        const float SwapDropReach = 0.6f;
        const float SwapDropHeight = 1.1f;

        bool _warnedUnlisted;

        void OnEnable()
        {
            ItemCarrier.ServerItemTaken += OnItemTaken;
        }

        void OnDisable()
        {
            // Static: a missed unsubscribe would stack a handler per Play session.
            ItemCarrier.ServerItemTaken -= OnItemTaken;
        }

        void OnItemTaken(ItemCarrier carrier, Pickup pickup)
        {
            if (pickup.Definition.Kind != ItemKind.Gun)
                return;

            WeaponDefinition weapon = _map != null ? _map.WeaponFor(pickup.Definition) : null;
            WeaponHolder holder = PlayerParts.HolderOf(carrier);
            if (weapon == null || holder == null)
            {
                WarnUnlisted(pickup);
                NotWithTheBriefcase(carrier, pickup);
                return; // Carried like any object, as before this join existed.
            }

            if (!GiveGun(holder, weapon, out WeaponDefinition swappedOut))
            {
                Debug.LogWarning($"[Integration] {weapon.name} could not be equipped (is it in the weapon catalog?); the pickup stays in the hands.");
                NotWithTheBriefcase(carrier, pickup);
                return;
            }

            pickup.ServerUseUp();
            if (swappedOut != null)
                DropSwapped(carrier, swappedOut);
        }

        /// <summary>Puts the gun in a slot by the rules above. False when the holder refused it; swappedOut is the gun it replaced, or null.</summary>
        static bool GiveGun(WeaponHolder holder, WeaponDefinition weapon, out WeaponDefinition swappedOut)
        {
            swappedOut = null;
            int slot = SlotHolding(holder, weapon);
            if (slot < 0)
                slot = SlotHolding(holder, null); // The first free slot.
            if (slot < 0)
            {
                slot = holder.ActiveSlot; // Both full: the gun in hand goes.
                swappedOut = holder.WeaponInSlot(slot);
            }

            // Equip always gives a full magazine, so a gun already carried is simply refilled.
            if (holder.Equip(weapon, slot))
                return true;

            swappedOut = null;
            return false;
        }

        static int SlotHolding(WeaponHolder holder, WeaponDefinition weapon)
        {
            for (int slot = 0; slot < WeaponHolder.SlotCount; slot++)
            {
                if (holder.WeaponInSlot(slot) == weapon)
                    return slot;
            }

            return -1;
        }

        /// <summary>A gun that could not go to the slots is an ordinary item; it may not share the hands with the briefcase.</summary>
        static void NotWithTheBriefcase(ItemCarrier carrier, Pickup pickup)
        {
            MatchDirector match = MatchDirector.Instance;
            FirmMember member = PlayerParts.MemberOf(carrier);
            if (match == null || member == null || !match.IsCarrying(member))
                return;

            pickup.ServerRelease(ReleaseKind.Dropped, Vector3.zero);
            carrier.ServerDeny(DenyReason.HandsFull);
        }

        void DropSwapped(ItemCarrier carrier, WeaponDefinition weapon)
        {
            NetworkObject prefab = _map.PickupFor(weapon);
            FloorLoot loot = FloorLoot.Instance;
            if (prefab == null || loot == null)
                return;

            Transform body = carrier.transform;
            Vector3 forward = body.forward;
            Vector3 at = body.position + Vector3.up * SwapDropHeight + forward * SwapDropReach;
            loot.ServerSpawnLoose(prefab, at, Quaternion.LookRotation(forward, Vector3.up), forward * _swapTossSpeed);
        }

        void WarnUnlisted(Pickup pickup)
        {
            if (_warnedUnlisted)
                return;

            _warnedUnlisted = true;
            Debug.LogWarning($"[Integration] {pickup.Definition.name} is a gun with no line in the gun pickup map; it is carried in the hands instead of going to a weapon slot.");
        }
    }
}

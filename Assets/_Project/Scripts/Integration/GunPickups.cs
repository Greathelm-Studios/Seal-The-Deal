using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.MatchMode;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>Host-only bridge routing picked-up gun items into WeaponHolder slots. Design: docs/systems/INTEGRATION.md.</summary>
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

        /// <summary>Host only. Spawns a swapped-out gun (prefab, position, rotation, velocity); the mode that sets it despawns what it spawns. Unset, a swapped gun is not dropped.</summary>
        public static System.Action<NetworkObject, Vector3, Quaternion, Vector3> SpawnLoose;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SpawnLoose = null;
        }

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

        static bool GiveGun(WeaponHolder holder, WeaponDefinition weapon, out WeaponDefinition swappedOut)
        {
            swappedOut = null;
            int slot = SlotHolding(holder, weapon);
            if (slot < 0)
                slot = SlotHolding(holder, null);
            if (slot < 0)
            {
                slot = holder.ActiveSlot;
                swappedOut = holder.WeaponInSlot(slot);
            }

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
            LooseGunSpawns spawns = LooseGunSpawns.Instance;
            if (prefab == null || (SpawnLoose == null && spawns == null))
                return;

            Transform body = carrier.transform;
            Vector3 forward = body.forward;
            Vector3 at = body.position + Vector3.up * SwapDropHeight + forward * SwapDropReach;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            if (SpawnLoose != null)
                SpawnLoose(prefab, at, facing, forward * _swapTossSpeed);
            else
                spawns.ServerSpawnLoose(prefab, at, facing, forward * _swapTossSpeed);
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

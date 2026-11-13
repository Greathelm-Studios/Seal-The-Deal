using System.Collections.Generic;
using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using CloseTheDeal.MatchMode;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// Finds a player's other lanes' components from any one of them without a GetComponent on
    /// every call: each lookup is resolved once and remembered until someone joins or leaves.
    /// The local player's parts come straight from each lane's own Local.
    /// </summary>
    public static class PlayerParts
    {
        static readonly Dictionary<Component, ItemCarrier> Carriers = new();
        static readonly Dictionary<Component, FirmMember> Members = new();
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Carriers.Clear();
            Members.Clear();
            if (_hooked)
                FirmMember.OnRosterChanged -= Forget;
            _hooked = false;
        }

        static void Hook()
        {
            if (_hooked)
                return;

            FirmMember.OnRosterChanged += Forget;
            _hooked = true;
        }

        /// <summary>Someone joined or left: bodies may have been pooled or destroyed, so look everything up afresh.</summary>
        static void Forget()
        {
            Carriers.Clear();
            Members.Clear();
        }

        public static ItemCarrier CarrierOf(Component part)
        {
            if (part == null)
                return null;
            if (ItemCarrier.Local != null && part.gameObject == ItemCarrier.Local.gameObject)
                return ItemCarrier.Local;

            Hook();
            if (!Carriers.TryGetValue(part, out ItemCarrier carrier))
            {
                carrier = part.GetComponent<ItemCarrier>();
                Carriers[part] = carrier;
            }

            return carrier;
        }

        public static FirmMember MemberOf(Component part)
        {
            if (part == null)
                return null;
            if (FirmMember.Local != null && part.gameObject == FirmMember.Local.gameObject)
                return FirmMember.Local;

            Hook();
            if (!Members.TryGetValue(part, out FirmMember member))
            {
                member = part.GetComponent<FirmMember>();
                Members[part] = member;
            }

            return member;
        }

        /// <summary>Host only use: the weapon holder on the same player. Called once per pickup, so a plain lookup.</summary>
        public static WeaponHolder HolderOf(Component part)
        {
            return part != null ? part.GetComponent<WeaponHolder>() : null;
        }
    }
}

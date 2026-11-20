using System;
using CloseTheDeal.Combat;
using CloseTheDeal.Items;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>
    /// Designer-owned link between a gun lying in the level and the gun it puts in a weapon slot.
    /// Kept here so Items never needs to know about Combat. Read at runtime, never written
    /// (AGENTS.md §4). Design: docs/systems/INTEGRATION.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Integration/Gun Pickup Map", fileName = "GunPickupMap")]
    public sealed class GunPickupMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The gun as a pickup: what the prompt names when a player looks at it.")]
            public ItemDefinition Item;

            [Tooltip("The gun it puts in the player's weapon slots when taken. Must be in the weapon catalog.")]
            public WeaponDefinition Weapon;

            [Tooltip("The pickup dropped when this gun is swapped out of a full set of slots. Leave empty and a swapped-out gun is simply lost.")]
            public NetworkObject Pickup;
        }

        [Tooltip("One line per gun that can lie in the level.")]
        [SerializeField] Entry[] _guns = new Entry[0];

        /// <summary>The weapon this pickup gives, or null when it is not listed.</summary>
        public WeaponDefinition WeaponFor(ItemDefinition item)
        {
            for (int i = 0; i < _guns.Length; i++)
            {
                if (_guns[i].Item == item)
                    return _guns[i].Weapon;
            }

            return null;
        }

        /// <summary>The pickup prefab for a weapon, or null when it has none.</summary>
        public NetworkObject PickupFor(WeaponDefinition weapon)
        {
            for (int i = 0; i < _guns.Length; i++)
            {
                if (_guns[i].Weapon == weapon)
                    return _guns[i].Pickup;
            }

            return null;
        }
    }
}

using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Every gun the game knows, in a fixed order. The network names a gun by its place in
    /// this list (one byte), so every copy of the game must ship the same catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public const byte None = byte.MaxValue;

        [Tooltip("Every gun in the game. Add new guns at the end: the order is how players' games tell each other which gun someone holds, so reordering breaks play between different versions. At most 255.")]
        public WeaponDefinition[] Weapons = new WeaponDefinition[0];

        /// <summary>The gun's place in the list, or None when it is not listed.</summary>
        public byte IndexOf(WeaponDefinition weapon)
        {
            if (weapon == null)
                return None;

            for (int i = 0; i < Weapons.Length && i < None; i++)
            {
                if (Weapons[i] == weapon)
                    return (byte)i;
            }

            return None;
        }

        public WeaponDefinition Get(byte index)
        {
            return index < Weapons.Length ? Weapons[index] : null;
        }
    }
}

using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Put one in a scene to hand every player these guns the moment they spawn, and again on
    /// each respawn. The host reads it; a scene without one starts players empty-handed, and
    /// guns then come from pickups calling WeaponHolder.Equip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartingLoadout : MonoBehaviour
    {
        public static StartingLoadout Instance { get; private set; }

        [Tooltip("The guns every player starts with in this scene, slot 1 first. Only the first two are used. Each must be in the weapon catalog.")]
        public WeaponDefinition[] Weapons = new WeaponDefinition[0];

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}

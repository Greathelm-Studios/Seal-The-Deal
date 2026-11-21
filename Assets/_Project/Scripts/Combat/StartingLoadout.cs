using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Scene component defining starting weapons given by the host on spawn and respawn.</summary>
    [DisallowMultipleComponent]
    public sealed class StartingLoadout : MonoBehaviour
    {
        public static StartingLoadout Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

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

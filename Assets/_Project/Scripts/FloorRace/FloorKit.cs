using System;
using CloseTheDeal.FloorGen;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>Which kit prefab each generator piece is. Filled by Seal the Deal > Floor Race > Fill Floor Kit; read at runtime, never written.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Floor Kit", fileName = "FloorKit")]
    public sealed class FloorKit : ScriptableObject
    {
        [Tooltip("One kit prefab per piece, in the order of the piece list. The fill tool sets every empty slot from Art/Kit/Prefabs by name; swap one here to change how that piece looks on every generated floor.")]
        public GameObject[] Prefabs = new GameObject[0];

        [Tooltip("Material of the plain block drawn under the floor in play, standing in for the storeys below. Leave empty for no block.")]
        public Material BuildingBelow;

        public static int PieceCount => Enum.GetValues(typeof(KitPiece)).Length;

        public GameObject PrefabFor(KitPiece piece)
        {
            int index = (int)piece;
            return index < Prefabs.Length ? Prefabs[index] : null;
        }
    }
}

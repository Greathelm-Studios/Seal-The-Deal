using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>What an item is for. Decides nothing by itself except how other systems treat it.</summary>
    public enum ItemKind : byte
    {
        /// <summary>Office furniture: carried, dropped, thrown.</summary>
        Furniture = 0,
        /// <summary>A firearm. The weapon holder takes it off the pickup the moment a player takes it.</summary>
        Gun = 1,
        /// <summary>Something armed and thrown, like the grenade case.</summary>
        Throwable = 2,
        /// <summary>Something set off or put down, like the fire extinguisher or the wet floor sign.</summary>
        Gadget = 3
    }

    /// <summary>
    /// Designer-owned description of one item a player can pick up. Runtime only reads it
    /// (AGENTS.md §4). One asset per item under Assets/_Project/Profiles/Items.
    /// Design: docs/systems/ITEMS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Items/Item Definition", fileName = "Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("What it is")]
        [Tooltip("The name players read in the pick-up prompt, as in \"F · Pick up Chair\". Keep it to one or two words.")]
        public string DisplayName = "Item";

        [Tooltip("What sort of item this is. Guns are handed to the player's weapons when taken; everything else is carried in the hands.")]
        public ItemKind Kind = ItemKind.Furniture;

        [Tooltip("One line telling the player what it does, for the HUD and menus.")]
        [TextArea(1, 3)] public string WhatItDoes = "";

        [Header("Carrying")]
        [Tooltip("How heavy it is to carry and how hard it hits when thrown, in kilograms. Heavier slows the carrier more and hurts more on impact. Above the hands' weight limit it cannot be picked up at all. Keep it equal to the prefab's body mass.")]
        [Min(0.1f)] public float WeightKg = 5f;

        [Tooltip("Where its base sits while carried, in metres from the carrier's eye: x = right, y = up (negative is below the eye), z = ahead. Further ahead reads better but pokes into walls sooner.")]
        public Vector3 HeldOffset = new(0f, -0.5f, 0.8f);

        [Tooltip("How it is turned in the hands, in degrees: x tips it forward, y spins it, z rolls it.")]
        public Vector3 HeldEuler;

        [Tooltip("How much room it needs in front of the carrier, in metres (roughly half its width). Walls push it back toward the body by this much so it never pokes through. Bigger = stops further from walls.")]
        [Min(0.02f)] public float HoldClearance = 0.2f;

        [Tooltip("Tick for things that should work as a shield while carried, like a cubicle panel: it stays solid in the hands, so it blocks shots and other players. Unticked, it passes through everything until let go.")]
        public bool CoverWhileCarried;

        [Header("Hands (for the arm-posing layer)")]
        [Tooltip("Where the main (right) hand holds it, in metres in the item's own space from its pivot. The hand's palm faces along the seat's up (+Y); its fingers point along the seat's forward (+Z).")]
        public Vector3 MainHandSeat = new(0.2f, 0.3f, 0f);
        [Tooltip("How the main hand's seat is turned in the item's space, in degrees.")]
        public Vector3 MainHandEuler = new(0f, 0f, 90f);
        [Tooltip("Tick when it takes both hands (a chair, a panel); the support (left) hand then goes to the seat below.")]
        public bool TwoHanded = true;
        [Tooltip("Where the support (left) hand holds it, in metres in the item's space. Only used when Two Handed is ticked.")]
        public Vector3 SupportHandSeat = new(-0.2f, 0.3f, 0f);
        [Tooltip("How the support hand's seat is turned in the item's space, in degrees.")]
        public Vector3 SupportHandEuler = new(0f, 0f, -90f);

        [Header("Throwing")]
        [Tooltip("How fast it leaves the hands when thrown, in metres per second. 6 is a heave that lands a few metres away; 16 crosses the room flat.")]
        [Min(0f)] public float ThrowSpeed = 10f;

        [Tooltip("How fast it tumbles in the air, in full turns per second. 0 flies level; 2 spins like a thrown chair.")]
        [Min(0f)] public float ThrowTumble = 1f;

        [Header("Sound")]
        [Tooltip("This item's own sounds. Leave empty to use the carry profile's set, which every item shares.")]
        public ItemSoundSet Sounds;

        [Header("Using")]
        [Tooltip("The word shown for its right-mouse action while carried, as in \"RMB · Arm\". Leave empty for things that are only carried and thrown.")]
        public string UseVerb = "";

        [Tooltip("For items that get used up (it explodes, it runs empty): how long until a fresh one is back where this one started, in seconds. 0 or less = gone for the rest of the run.")]
        public float ReturnsAfterUsedSeconds = 20f;
    }
}

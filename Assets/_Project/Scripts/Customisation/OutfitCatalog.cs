using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>Designer-owned ordered list of outfit slots used for UI and host validation. Design: docs/systems/CUSTOMISATION.md.</summary>
    [CreateAssetMenu(menuName = "Close the Deal/Customisation/Outfit Catalog", fileName = "OutfitCatalog")]
    public sealed class OutfitCatalog : ScriptableObject
    {
        [Tooltip("The categories, in the order the fitting room lists them. At most 8: an outfit is sent as one byte per category.")]
        [SerializeField] OutfitSlot[] _slots = new OutfitSlot[0];

        public int SlotCount => Mathf.Min(_slots.Length, OutfitLoadout.MaxSlots);
        public OutfitSlot Slot(int i) => _slots[i];

        /// <summary>Every slot on its default choice.</summary>
        public OutfitLoadout Default
        {
            get
            {
                OutfitLoadout loadout = default;
                for (int i = 0; i < SlotCount; i++)
                    loadout = loadout.With(i, _slots[i] != null ? _slots[i].DefaultOption : 0);
                return loadout;
            }
        }

        /// <summary>True when every slot holds a choice that exists and no unused slot is set. Lock state is not checked: see IsWearable.</summary>
        public bool IsValid(OutfitLoadout loadout)
        {
            for (int i = 0; i < OutfitLoadout.MaxSlots; i++)
            {
                int option = loadout.Get(i);
                if (i >= SlotCount)
                {
                    if (option != 0)
                        return false;
                    continue;
                }

                if (_slots[i] == null || !_slots[i].IsValidOption(option))
                    return false;
            }

            return true;
        }

        /// <summary>Valid, and every choice is unlocked for a player who has started this many runs.</summary>
        public bool IsWearable(OutfitLoadout loadout, int runsStarted)
        {
            if (!IsValid(loadout))
                return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].Option(loadout.Get(i)).IsUnlocked(runsStarted))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Keeps every valid, unlocked choice and puts anything else back on that slot's default.
        /// Used on a loaded save, which may be from an older catalog or edited by hand.
        /// </summary>
        public OutfitLoadout Sanitise(OutfitLoadout loadout, int runsStarted)
        {
            OutfitLoadout clean = Default;
            for (int i = 0; i < SlotCount; i++)
            {
                int option = loadout.Get(i);
                OutfitSlot slot = _slots[i];
                if (slot != null && slot.IsValidOption(option) && slot.Option(option).IsUnlocked(runsStarted))
                    clean = clean.With(i, option);
            }

            return clean;
        }

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created catalog. Never called at runtime (AGENTS.md §4).</summary>
        public void EditorInit(OutfitSlot[] slots) => _slots = slots;
#endif
    }
}

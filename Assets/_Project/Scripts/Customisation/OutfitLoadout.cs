using System;

namespace CloseTheDeal.Customisation
{
    /// <summary>Packed 8-byte outfit loadout (one byte per slot) replicated over the network.</summary>
    public readonly struct OutfitLoadout : IEquatable<OutfitLoadout>
    {
        public const int MaxSlots = 8;

        public readonly ulong Packed;

        public OutfitLoadout(ulong packed) => Packed = packed;

        /// <summary>The chosen option index in a slot, 0–255.</summary>
        public int Get(int slot) => (int)((Packed >> (slot * 8)) & 0xFF);

        /// <summary>A copy with one slot changed. Values outside 0–255 are cut to the low byte.</summary>
        public OutfitLoadout With(int slot, int option)
        {
            int shift = slot * 8;
            ulong cleared = Packed & ~(0xFFUL << shift);
            return new OutfitLoadout(cleared | ((ulong)(option & 0xFF) << shift));
        }

        public bool Equals(OutfitLoadout other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is OutfitLoadout other && Equals(other);
        public override int GetHashCode() => Packed.GetHashCode();
        public static bool operator ==(OutfitLoadout a, OutfitLoadout b) => a.Packed == b.Packed;
        public static bool operator !=(OutfitLoadout a, OutfitLoadout b) => a.Packed != b.Packed;
    }
}

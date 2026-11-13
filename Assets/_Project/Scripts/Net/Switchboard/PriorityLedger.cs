namespace CloseTheDeal.Net
{
    /// <summary>
    /// One client's running score per body slot. A slot gains score each send while its body
    /// has something to say; a slot is due at a score of 1 or more; taking a slot returns its
    /// score to 0. Slot numbers are handed out by PropStream and are the same in every
    /// client's ledger. Pure; allocates only when it grows. Design: docs/systems/SWITCHBOARD.md.
    /// </summary>
    public sealed class PriorityLedger
    {
        /// <summary>A slot is due once its score reaches this.</summary>
        public const float DueScore = 1f;
        float[] _scores;

        public PriorityLedger(int initialSlots)
        {
            _scores = new float[initialSlots > 0 ? initialSlots : 0];
        }

        /// <summary>Makes room for slots 0..slotCount-1. New slots start at 0. Never shrinks.</summary>
        public void EnsureSlots(int slotCount)
        {
            if (slotCount <= _scores.Length)
                return;

            int capacity = _scores.Length > 0 ? _scores.Length : 1;
            while (capacity < slotCount)
            {
                int doubled = capacity <= int.MaxValue / 2 ? capacity * 2 : int.MaxValue;
                capacity = doubled;
            }
            System.Array.Resize(ref _scores, capacity);
        }

        /// <summary>Adds to a slot's score.</summary>
        public void Accrue(int slot, float amount)
        {
            if ((uint)slot < (uint)_scores.Length)
                _scores[slot] += amount;
        }

        /// <summary>Returns a slot's score to 0 without sending it (its body left, or went quiet).</summary>
        public void Clear(int slot)
        {
            if ((uint)slot < (uint)_scores.Length)
                _scores[slot] = 0f;
        }

        public float ScoreOf(int slot)
        {
            return (uint)slot < (uint)_scores.Length ? _scores[slot] : 0f;
        }

        /// <summary>
        /// Writes up to max due slots into "into", highest score first (ties: lower slot first),
        /// returns how many, and returns each taken slot's score to 0. Slots not taken keep
        /// their score. Only slots below slotCount are considered. No allocation.
        /// </summary>
        public int TakeDue(int slotCount, int[] into, int max)
        {
            int limit = System.Math.Min(max, into.Length);
            if (limit <= 0)
                return 0;

            int count = 0;
            int slots = System.Math.Min(slotCount, _scores.Length);
            for (int slot = 0; slot < slots; slot++)
            {
                float score = _scores[slot];
                if (score < DueScore)
                    continue;

                int position = 0;
                while (position < count && _scores[into[position]] >= score)
                    position++;
                if (position >= limit)
                    continue;

                if (count < limit)
                    count++;
                for (int i = count - 1; i > position; i--)
                    into[i] = into[i - 1];
                into[position] = slot;
            }

            for (int i = 0; i < count; i++)
                _scores[into[i]] = 0f;
            return count;
        }
    }
}

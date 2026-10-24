using System;
using System.Collections.Generic;

namespace SiegeDemo
{
    // Dedicated state: effects, layouts and UnityEngine.Random cannot reroll rewards.
    public struct RewardRandom
    {
        public uint State { get; private set; }
        public RewardRandom(uint state) { State = state == 0 ? 0x6d2b79f5u : state; }
        private uint Next()
        {
            uint value = State;
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            State = value;
            return value;
        }
        private int Index(int count)
        {
            // Xorshift32 ranges over 1..uint.MaxValue; reject an incomplete final bucket.
            uint limit = uint.MaxValue - uint.MaxValue % (uint)count;
            uint value;
            do { value = Next() - 1; } while (value >= limit);
            return (int)(value % (uint)count);
        }
        public PassiveAbility[] DrawThree(PassiveAbility[] owned)
        {
            var pool = new List<PassiveAbility>();
            foreach (PassiveAbility value in Enum.GetValues(typeof(PassiveAbility)))
                if (owned == null || Array.IndexOf(owned, value) < 0) pool.Add(value);
            if (pool.Count < 3) throw new InvalidOperationException("Reward pool has fewer than three unowned abilities.");
            var result = new PassiveAbility[3];
            for (int i = 0; i < result.Length; i++)
            {
                int index = Index(pool.Count);
                result[i] = pool[index];
                pool.RemoveAt(index);
            }
            return result;
        }
    }
}

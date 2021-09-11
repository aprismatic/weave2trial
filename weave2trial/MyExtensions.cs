using System;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;

namespace weave2trial
{
    public static class MyExtensions
    {
        /// <summary>
        /// Returns a rng BigInteger that is within a specified range.
        /// The lower bound is inclusive, and the upper bound is exclusive.
        /// </summary>
        public static BigInteger NextBigInteger(this RandomNumberGenerator rng, BigInteger minValue, BigInteger maxValue)
        {
            if (minValue > maxValue) throw new ArgumentException();
            if (minValue == maxValue) return minValue;
            var zeroBasedUpperBound = maxValue - 1 - minValue; // Inclusive
            Debug.Assert(zeroBasedUpperBound.Sign >= 0);
            var bytes = zeroBasedUpperBound.ToByteArray();
            Debug.Assert(bytes.Length > 0);
            Debug.Assert((bytes[^1] & 0b10000000) == 0);

            // Search for the most significant non-zero bit
            byte lastByteMask = 0b11111111;
            for (byte mask = 0b10000000; mask > 0; mask >>= 1, lastByteMask >>= 1)
            {
                if ((bytes[^1] & mask) == mask) break; // We found it
            }

            while (true)
            {
                rng.GetBytes(bytes);
                bytes[^1] &= lastByteMask;
                var result = new BigInteger(bytes);
                Debug.Assert(result.Sign >= 0);
                if (result <= zeroBasedUpperBound) return result + minValue;
            }
        }

        public static BigInteger NextBigInteger(this RandomNumberGenerator rng, BigInteger maxValue) =>
            NextBigInteger(rng, BigInteger.Zero, maxValue);
        public static BigInteger NextBigInteger(this RandomNumberGenerator rng) =>
            NextBigInteger(rng, BigInteger.Zero, new BigInteger(Int32.MaxValue));
    }
}
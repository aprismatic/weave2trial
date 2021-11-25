using System;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using Aprismatic;

namespace weave2trial
{
    public static class MyExtensions
    {
        /// <summary>
        /// Returns a RNG BigInteger that is within a specified range.
        /// The lower bound is inclusive, and the upper bound is exclusive.
        /// </summary>
        public static BigInteger NextBigInteger(this RandomNumberGenerator rng, BigInteger minValue, BigInteger maxValue)
        {
            if (minValue > maxValue) throw new ArgumentException();
            if (minValue == maxValue) return minValue;
            var zeroBasedUpperBound = maxValue - BigInteger.One - minValue; // Inclusive
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

        public static BigFraction NextBigFraction(this RandomNumberGenerator rng, BigFraction minValue, BigFraction maxValue) {
            if (minValue > maxValue) throw new ArgumentException($"{nameof(minValue)} must be less or equal to {nameof(maxValue)}");
            if (minValue == maxValue) return minValue;
            var zeroBasedUpperBound = maxValue - minValue;
            zeroBasedUpperBound.Simplify();
            Debug.Assert(zeroBasedUpperBound.Sign > 0);

            var normFactor = zeroBasedUpperBound.Denominator;
            zeroBasedUpperBound *= normFactor;
            zeroBasedUpperBound.Simplify();
            Debug.Assert(zeroBasedUpperBound.Denominator == BigInteger.One);

            var denom = rng.NextBigInteger(zeroBasedUpperBound.ToBigInteger());
            zeroBasedUpperBound *= denom;
            var numer = rng.NextBigInteger(zeroBasedUpperBound.ToBigInteger());

            var res = new BigFraction(numer, denom);
            res /= normFactor;
            res.Simplify();
            Debug.Assert(res >= minValue);
            Debug.Assert(res <= maxValue);

            return res;
        }

        public static BigFraction NextBigFraction(this RandomNumberGenerator rng, BigFraction maxValue) =>
            NextBigFraction(rng, BigFraction.Zero, maxValue);
        public static BigFraction NextBigFraction(this RandomNumberGenerator rng) =>
            NextBigFraction(rng, BigFraction.Zero, new BigInteger(Int32.MaxValue));
    }
}

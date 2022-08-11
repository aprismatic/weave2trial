using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Aprismatic;

namespace weave2trial
{
    public readonly struct LinearShard
    {
        public readonly BigFraction S;

        public LinearShard(BigFraction s) {
            S = s;
        }

        public static LinearShard operator +(LinearShard a, LinearShard b) => new(a.S + b.S);

        public override string ToString() => $"LinearShard <{S}>";
    }

    public static class LinearSecretSharing
    {
        private static readonly BigInteger DefaultUpperBound = BigInteger.Pow(2, Globals.SCALE_EXPONENT);

        public static List<LinearShard> CreateSecretSharing(BigFraction value, int n, RandomNumberGenerator rng) {
            var res = new List<LinearShard>(n);

            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n), "Must be >= 2");

            for (var x = 0; x < n - 1; x++) {
                res.Add(new LinearShard(rng.NextBigInteger(-DefaultUpperBound, DefaultUpperBound)));
            }

            var curSum = res.Aggregate(BigFraction.Zero, (acc, item) => acc + item.S);

            res.Add(new LinearShard(value - curSum));

            return res;
        }

        public static BigFraction RecoverSecret(IEnumerable<LinearShard> shards) {
            return shards.Aggregate(BigFraction.Zero, (a, x) => a + x.S).Simplify();
        }
    }
}

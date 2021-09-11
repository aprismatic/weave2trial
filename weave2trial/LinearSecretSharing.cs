using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace weave2trial
{
    public readonly struct LinearShard
    {
        public LinearShard(BigInteger s)
        {
            this.s = s;
        }

        public readonly BigInteger s;
    }

    public static class LinearSecretSharing
    {
        private static readonly BigInteger DefaultUpperBound = BigInteger.Pow(2, 2048);

        public static List<LinearShard> CreateSecretSharing(BigInteger value, int n, RandomNumberGenerator rng)
        {
            var res = new List<LinearShard>(n);

            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n), "Must be >= 2");

            for (var x = 0; x < n-1; x++)
            {
                res.Add(new LinearShard(rng.NextBigInteger(-DefaultUpperBound, DefaultUpperBound)));
            }

            var curSum = res.Aggregate(BigInteger.Zero, (acc, item) => acc + item.s);

            res.Add(new LinearShard(value - curSum));

            return res;
        }
    }
}
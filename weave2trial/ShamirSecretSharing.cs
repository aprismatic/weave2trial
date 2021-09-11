using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace weave2trial
{
    public readonly struct ShamirShard
    {
        public ShamirShard(BigInteger x, BigInteger y)
        {
            this.x = x;
            this.y = y;
        }

        public readonly BigInteger x;
        public readonly BigInteger y;
    }

    public static class ShamirSecretSharing
    {
        public static List<ShamirShard> CreateSecretSharing(BigInteger value, int n, int t, RandomNumberGenerator rng)
        {
            var res = new List<ShamirShard>();

            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n), "Must be >= 2");
            if (t < 2) throw new ArgumentOutOfRangeException(nameof(t), "Must be >= 2");

            var poly = new Polynomial(t - 1, rng);
            poly[poly.Order] = value;

            //Console.WriteLine($"ShamirSecretSharing poly = {poly}");

            for (var x = 1; x <= n; x++)
            {
                res.Add(new ShamirShard(x, poly.Eval(x)));
            }

            return res;
        }

        public static (BigInteger num, BigInteger div) LagrangianElement(List<BigInteger> Xs, int j, BigInteger x)
        {
            if (j < 0 || j >= Xs.Count)
                throw new ArgumentOutOfRangeException(nameof(j), "j must be within the index bounds of Xs");

            var num = BigInteger.One;
            var div = BigInteger.One;

            for (var m = 0; m < Xs.Count; m++)
            {
                if (m == j) continue;
                num *= x - Xs[m];
                div *= Xs[j] - Xs[m];
            }

            return (num, div);
        }

        public static BigInteger RecoverSecret(List<ShamirShard> shards)
        {
            var Xs = shards.Select(a => a.x).ToList();
            var Ys = shards.Select(a => a.y).ToList();
            var nums = new List<BigInteger>(shards.Count);
            var divs = new List<BigInteger>(shards.Count);
            for (var j = 0; j < shards.Count; j++)
            {
                var (num, div) = LagrangianElement(Xs, j, 0);
                nums.Add(num);
                divs.Add(div);
            }
            var AllDivs = divs.Aggregate(BigInteger.One, (a, b) => a * b);

            var res = BigInteger.Zero;

            for (var j = 0; j < shards.Count; j++)
            {
                res += (Ys[j] * nums[j] * AllDivs) / divs[j];
            }

            res /= AllDivs;

            return res;
        }
    }
}
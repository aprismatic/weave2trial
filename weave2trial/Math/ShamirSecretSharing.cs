using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using Aprismatic;

namespace weave2trial
{
    public readonly struct ShamirShard
    {
        public readonly BigInteger X;
        public readonly BigFraction Y;

        public ShamirShard(BigInteger x, BigFraction y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() {
            return $"ShamirShard <{X}, {Y}>";
        }
    }

    public static class ShamirSecretSharing
    {
        public static List<ShamirShard> CreateSecretSharing(BigFraction value, int n, int t, RandomNumberGenerator rng) {
            var res = new List<ShamirShard>();

            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n), "Must be >= 2");
            if (t < 2) throw new ArgumentOutOfRangeException(nameof(t), "Must be >= 2");

            var poly = new Polynomial(t - 1, rng);
            poly[poly.Order] = value;

            Log.Info($"ShamirSecretSharing poly = {poly}");

            for (var x = 1; x <= n; x++)
                res.Add(new ShamirShard(x, poly.Eval(x)));

            return res;
        }

        public static BigFraction LagrangianElement(IList<BigInteger> Xs, int j)
        {
            if (j < 0 || j >= Xs.Count)
                throw new ArgumentOutOfRangeException(nameof(j), "j must be within the index bounds of Xs");

            var num = BigInteger.One;
            var div = BigInteger.One;

            for (var m = 0; m < Xs.Count; m++)
            {
                if (m == j) continue;
                num *= Xs[m];
                div *= Xs[m] - Xs[j];
            }

            return new(num, div);
        }

        public static BigFraction AdditiveElement(IList<BigInteger> Xs, int j, BigFraction Y) => LagrangianElement(Xs, j) * Y;

        public static BigFraction RecoverSecret(IEnumerable<ShamirShard> shards) {
            List<BigInteger> Xs = new();
            List<BigFraction> Ys = new();

            foreach (var s in shards) {
                Xs.Add(s.X);
                Ys.Add(s.Y);
            }

            var res = BigFraction.Zero;

            for (var j = 0; j < Xs.Count; j++)
                res += AdditiveElement(Xs, j, Ys[j]);

            return res.Simplify();
        }
    }
}

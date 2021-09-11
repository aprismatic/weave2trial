using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using NUnit.Framework;
using weave2trial;

namespace tests
{
    public class ShamirTests
    {
        private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        private static readonly Random rnd = new();

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ShamirSecretSharingStressTest()
        {
            var smallUpperBound = Int16.MaxValue;
            for (var v = 0; v < smallUpperBound; v += rnd.Next(1,128))
            {
                var n = rnd.Next(3, 512);
                var t = rnd.Next(3, n + 1);
                var sss = ShamirSecretSharing.CreateSecretSharing(v, n, t, rng);
                var rec = ShamirSecretSharing.RecoverSecret(sss.OrderBy(x => rnd.Next()).Take(t).ToList());
                Assert.AreEqual(new BigInteger(v), rec);
            }

            var largeUpperBound = BigInteger.Pow(2, 2048);
            for (var i = 0; i < 10; i++)
            {
                var v = rng.NextBigInteger(largeUpperBound);
                var n = rnd.Next(3, 512);
                var t = rnd.Next(3, n + 1);
                var sss = ShamirSecretSharing.CreateSecretSharing(v, n, t, rng);
                var rec = ShamirSecretSharing.RecoverSecret(sss.OrderBy(_ => rnd.Next()).Take(t).ToList());
                Assert.AreEqual(v, rec);
            }
        }
    }
}

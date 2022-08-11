using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Xunit;
using weave2trial;

namespace Tests
{
    public class ShamirTests
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        private static readonly Random Rnd = new();

        [Fact(DisplayName = "SHAMIR: Simple")]
        public void ShamirSecretSharingSimpleTest() {
            var n = 6;
            var t = 4;
            var v = 10;
            var sss = ShamirSecretSharing.CreateSecretSharing(v, n, t, Rng);
            var rec = ShamirSecretSharing.RecoverSecret(sss.OrderBy(x => Rnd.Next()).Take(t));
            Assert.Equal(new BigInteger(v), rec);
        }

        [Fact(DisplayName = "SHAMIR: Stress Test", Skip = "Slow stress test")]
        public void ShamirSecretSharingStressTest() {
            var smallUpperBound = Int16.MaxValue;
            for (var v = 0; v < smallUpperBound; v += Rnd.Next(1, 128)) {
                var n = Rnd.Next(3, 512);
                var t = Rnd.Next(3, n + 1);
                var sss = ShamirSecretSharing.CreateSecretSharing(v, n, t, Rng);
                var rec = ShamirSecretSharing.RecoverSecret(sss.OrderBy(x => Rnd.Next()).Take(t));
                Assert.Equal(new BigInteger(v), rec);
            }

            var largeUpperBound = BigInteger.Pow(2, 2048);
            for (var i = 0; i < 10; i++) {
                var v = Rng.NextBigInteger(largeUpperBound);
                var n = Rnd.Next(3, 512);
                var t = Rnd.Next(3, n + 1);
                var sss = ShamirSecretSharing.CreateSecretSharing(v, n, t, Rng);
                var rec = ShamirSecretSharing.RecoverSecret(sss.OrderBy(_ => Rnd.Next()).Take(t));
                Assert.Equal(v, rec);
            }
        }
    }
}

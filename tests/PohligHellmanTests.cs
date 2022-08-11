using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aprismatic.PohligHellman;
using weave2trial;
using Xunit;

namespace Tests;

public class PHRandomOrderTests
{
    [Fact(DisplayName = "Pohlig-Hellman - Random Order")]
    public void TestPHRandomOrder() {
        var secret = new BigInteger(987654);

        var h = new IntegerElGamal();

        var venc = h.Encrypt(secret);

        var e_list = new List<BigInteger>(); // List of PH encryption keys
        for (var i = 0; i < 15; i++) {
            var ph = new PohligHellman(h.Modulus);
            e_list.Add(new BigInteger(ph.ExportParameters().E));
        }

        BigInteger ProcessRandomOrder(IntegerElGamal h, ReadOnlySpan<byte> venc, List<BigInteger> e_list) {
            var eglist = new List<byte[]>(e_list.Count); // list of venc encrypted with keys from e_list

            foreach (var e in e_list.OrderBy(x => Random.Shared.Next()))
                eglist.Add(h.Power(venc, e).ToArray());

            var res = h.Encrypt(BigInteger.One);

            foreach (var egenc in eglist)
                res = h.MultiplyIntegers(res, egenc);

            return h.Decrypt(res);
        }

        var res = ProcessRandomOrder(h, venc, e_list);

        for (var i = 0; i < 20; i++) {
            var cur = ProcessRandomOrder(h, venc, e_list);
            Assert.Equal(res, cur);
        }
    }
}

using System;
using System.Numerics;
using Aprismatic.ElGamal;
using Aprismatic.ElGamal.Homomorphism;

namespace weave2trial;

public class IntegerElGamal
{
    public int Bits => eg.KeySize;
    public BigInteger Modulus => eg.P;

    public string PublicKey => eg.ToXmlString(false);
    public string PrivateKey => eg.ToXmlString(true);

    private readonly ElGamal eg;
    private readonly int biblock;
    private readonly int halfbiblock;
    private readonly byte[] modulusByteArray;

    public IntegerElGamal(int bits = 512) {
        eg = new ElGamal(bits);
        biblock = eg.CiphertextLength / 2;
        halfbiblock = biblock / 2;
        modulusByteArray = eg.P.ToByteArray();
    }

    public byte[] Encrypt(BigInteger m) {
        var res = new byte[biblock];
        eg.Encryptor.ProcessBigInteger(m, res);
        return res;
    }

    public BigInteger Decrypt(Span<byte> c) {
        return eg.Decryptor.ProcessByteBlock(c.Slice(0, halfbiblock), c.Slice(halfbiblock, halfbiblock));
    }

    public byte[] Power(ReadOnlySpan<byte> c, BigInteger m) {
        var res = new byte[biblock];
        eg.PlaintextPowBigInteger(c, m, res);
        return res;
    }

    public byte[] MultiplyIntegers(ReadOnlySpan<byte> c1, ReadOnlySpan<byte> c2) {
        var res = new byte[biblock];
        ElGamalHomomorphism.MultiplyIntegers(c1, c2, modulusByteArray, res);
        return res;
    }
}

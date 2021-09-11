﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace weave2trial
{
    class Polynomial
    {
        private List<BigInteger> coeffs;

        public int Order => coeffs.Count - 1;

        private static readonly BigInteger DefaultUpperBound = BigInteger.Pow(2, 2048);

        public Polynomial(int ord, RandomNumberGenerator rng) : this(ord, rng, DefaultUpperBound) { }

        public Polynomial(int ord, RandomNumberGenerator rng, BigInteger upperBound)
        {
            if (ord < 0)
                throw new ArgumentOutOfRangeException(nameof(ord), "Must be >= 0");
            coeffs = new List<BigInteger>(ord + 1);
            
            for (var i = 0; i < ord + 1; i++)
            {
                coeffs.Add(rng.NextBigInteger(0, upperBound));
            }
        }

        public BigInteger Eval(BigInteger x)
        {
            var lx = BigInteger.One;
            var res = BigInteger.Zero;
            for (var i = Order; i >= 0; i--)
            {
                res += coeffs[i] * lx;
                lx *= x;
            }
            return res;
        }

        private string _numToSuperscript(int p)
        {
            var res = new StringBuilder();
            const char minus = '⁻';
            char[] nums = { '⁰','¹','²','³','⁴','⁵','⁶','⁷','⁸','⁹' };
            if (p < 0)
                res.Append(minus);
            p = Math.Abs(p);
            var st = new Stack<char>();
            while (p > 10)
            {
                var r = p % 10;
                st.Push(nums[r]);
                p /= 10;
            }
            res.Append(st.ToArray());
            res.Append(nums[p]);
            return res.ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < coeffs.Count; i++)
            {
                sb.Append(coeffs[i]);
                var p = Order - i;
                if (p > 0)
                {
                    sb.Append('x');
                    if (p > 1)
                        sb.Append(_numToSuperscript(p));
                    sb.Append('+');
                }
            }
            return sb.ToString();
        }

        public BigInteger this[int i]
        {
            get => coeffs[i];
            set => coeffs[i] = value;
        }
    }
}
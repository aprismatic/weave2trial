using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aprismatic;

namespace weave2trial
{
    public class Polynomial
    {
        private readonly List<BigFraction> _coeffs;

        public int Order => _coeffs.Count - 1;

        private static readonly BigInteger DefaultUpperBound = BigInteger.Pow(2, Globals.SCALE_EXPONENT);

        public Polynomial(int ord, RandomNumberGenerator rng) : this(ord, rng, DefaultUpperBound) { }

        public Polynomial(int ord, RandomNumberGenerator rng, BigInteger upperBound) {
            if (ord < 0)
                throw new ArgumentOutOfRangeException(nameof(ord), "Must be >= 0");
            _coeffs = new(ord + 1);

            for (var i = 0; i < ord + 1; i++)
                _coeffs.Add(rng.NextBigInteger(0, upperBound));
        }

        public BigFraction Eval(BigFraction x) {
            var lx = BigFraction.One;
            var res = BigFraction.Zero;
            for (var i = Order; i >= 0; i--) {
                res += _coeffs[i] * lx;
                lx *= x;
            }

            return res.Simplify();
        }

        private static string NumToSuperscript(int p) {
            var res = new StringBuilder();
            const char minus = '⁻';
            char[] nums = { '⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹' };
            if (p < 0)
                res.Append(minus);
            p = Math.Abs(p);
            var st = new Stack<char>();
            while (p >= 10) {
                var r = p % 10;
                st.Push(nums[r]);
                p /= 10;
            }

            res.Append(st.ToArray());
            res.Append(nums[p]);
            return res.ToString();
        }

        public override string ToString() {
            var sb = new StringBuilder();
            for (var i = 0; i < _coeffs.Count; i++) {
                sb.Append(_coeffs[i]);
                var p = Order - i;
                if (p > 0) {
                    sb.Append('x');
                    if (p > 1)
                        sb.Append(NumToSuperscript(p));
                    sb.Append('+');
                }
            }

            return sb.ToString();
        }

        public BigFraction this[int i] {
            get => _coeffs[i];
            set => _coeffs[i] = value;
        }
    }
}

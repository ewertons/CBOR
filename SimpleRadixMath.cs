/*
Written in 2013 by Peter O.
Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://peteroupc.github.io/CBOR/
 */
using System;

namespace PeterO {
    /// <summary>Implements the simplified arithmetic in Appendix A of
    /// the General Decimal Arithmetic Specification.</summary>
    /// <typeparam name='T'>Data type for a numeric value in a particular
    /// radix.</typeparam>
  internal sealed class SimpleRadixMath<T> : IRadixMath<T> {
    private IRadixMath<T> wrapper;

    public SimpleRadixMath(IRadixMath<T> wrapper) {
      this.wrapper = wrapper;
    }

    private static PrecisionContext GetContextWithFlags(PrecisionContext ctx) {
      if (ctx == null) {
        return ctx;
      }
      return ctx.WithBlankFlags();
    }

    private T SignalInvalid(PrecisionContext ctx) {
      if (this.GetHelper().GetArithmeticSupport() == BigNumberFlags.FiniteOnly) {
        throw new ArithmeticException("Invalid operation");
      }
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |= PrecisionContext.FlagInvalid;
      }
      return this.GetHelper().CreateNewWithFlags(BigInteger.Zero, BigInteger.Zero, BigNumberFlags.FlagQuietNaN);
    }

    private T PostProcess(T thisValue, PrecisionContext ctxDest, PrecisionContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, false, false);
    }

    private T PostProcessAfterDivision(T thisValue, PrecisionContext ctxDest, PrecisionContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, true, false);
    }

    private T PostProcessAfterQuantize(T thisValue, PrecisionContext ctxDest, PrecisionContext ctxSrc) {
      return this.PostProcessEx(thisValue, ctxDest, ctxSrc, false, true);
    }

    private T PostProcessEx(T thisValue, PrecisionContext ctxDest, PrecisionContext ctxSrc, bool afterDivision, bool afterQuantize) {
      int thisFlags = this.GetHelper().GetFlags(thisValue);
      if (ctxDest != null && ctxSrc != null) {
        if (ctxDest.HasFlags) {
          if ((ctxSrc.Flags & PrecisionContext.FlagUnderflow) != 0) {
            ctxSrc.Flags &= ~PrecisionContext.FlagClamped;
          }
          ctxDest.Flags |= ctxSrc.Flags;
          if ((ctxSrc.Flags & PrecisionContext.FlagSubnormal) != 0) {
            // Treat subnormal numbers as underflows
            int newflags = PrecisionContext.FlagUnderflow | PrecisionContext.FlagInexact |
                              PrecisionContext.FlagRounded;
            ctxDest.Flags |= newflags;
          }
        }
      }
      if ((thisFlags & BigNumberFlags.FlagSpecial) != 0) {
        if (ctxDest.Flags == 0) {
          return this.SignalInvalid(ctxDest);
        }
        return thisValue;
      }
      BigInteger mant = BigInteger.Abs(this.GetHelper().GetMantissa(thisValue));
      if (mant.IsZero) {
        if (afterQuantize) {
          return this.GetHelper().CreateNewWithFlags(mant, this.GetHelper().GetExponent(thisValue), 0);
        }
        return this.GetHelper().ValueOf(0);
      }
      if (afterQuantize) {
        return thisValue;
      }
      BigInteger exp = this.GetHelper().GetExponent(thisValue);
      if (exp.Sign > 0) {
        FastInteger fastExp = FastInteger.FromBig(exp);
        if (ctxDest == null || !ctxDest.HasMaxPrecision) {
          mant = this.GetHelper().MultiplyByRadixPower(mant, fastExp);
          return this.GetHelper().CreateNewWithFlags(mant, BigInteger.Zero, thisFlags);
        }
        if (!ctxDest.ExponentWithinRange(exp)) {
          return thisValue;
        }
        FastInteger prec = FastInteger.FromBig(ctxDest.Precision);
        FastInteger digits = this.GetHelper().CreateShiftAccumulator(mant).GetDigitLength();
        prec.Subtract(digits);
        if (prec.Sign > 0 && prec.CompareTo(fastExp) >= 0) {
          mant = this.GetHelper().MultiplyByRadixPower(mant, fastExp);
          return this.GetHelper().CreateNewWithFlags(mant, BigInteger.Zero, thisFlags);
        }
        if (afterDivision) {
          mant = DecimalUtility.ReduceTrailingZeros(mant, fastExp, this.GetHelper().GetRadix(), null, null, null);
          thisValue = this.GetHelper().CreateNewWithFlags(mant, fastExp.AsBigInteger(), thisFlags);
        }
      } else if (afterDivision && exp.Sign < 0) {
        FastInteger fastExp = FastInteger.FromBig(exp);
        mant = DecimalUtility.ReduceTrailingZeros(mant, fastExp, this.GetHelper().GetRadix(), null, null, new FastInteger(0));
        thisValue = this.GetHelper().CreateNewWithFlags(mant, fastExp.AsBigInteger(), thisFlags);
      }
      return thisValue;
    }

    private T ReturnQuietNaN(T thisValue, PrecisionContext ctx) {
      BigInteger mant = BigInteger.Abs(this.GetHelper().GetMantissa(thisValue));
      bool mantChanged = false;
      if (!mant.IsZero && ctx != null && ctx.HasMaxPrecision) {
        BigInteger limit = this.GetHelper().MultiplyByRadixPower(BigInteger.One, FastInteger.FromBig(ctx.Precision));
        if (mant.CompareTo(limit) >= 0) {
          mant %= (BigInteger)limit;
          mantChanged = true;
        }
      }
      int flags = this.GetHelper().GetFlags(thisValue);
      if (!mantChanged && (flags & BigNumberFlags.FlagQuietNaN) != 0) {
        return thisValue;
      }
      flags &= BigNumberFlags.FlagNegative;
      flags |= BigNumberFlags.FlagQuietNaN;
      return this.GetHelper().CreateNewWithFlags(mant, BigInteger.Zero, flags);
    }

    private T HandleNotANumber(T thisValue, T other, PrecisionContext ctx) {
      int thisFlags = this.GetHelper().GetFlags(thisValue);
      int otherFlags = this.GetHelper().GetFlags(other);
      // Check this value then the other value for signaling NaN
      if ((thisFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(thisValue, ctx);
      }
      if ((otherFlags & BigNumberFlags.FlagSignalingNaN) != 0) {
        return this.SignalingNaNInvalid(other, ctx);
      }
      // Check this value then the other value for quiet NaN
      if ((thisFlags & BigNumberFlags.FlagQuietNaN) != 0) {
        return this.ReturnQuietNaN(thisValue, ctx);
      }
      if ((otherFlags & BigNumberFlags.FlagQuietNaN) != 0) {
        return this.ReturnQuietNaN(other, ctx);
      }
      return default(T);
    }

    private T SignalingNaNInvalid(T value, PrecisionContext ctx) {
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |= PrecisionContext.FlagInvalid;
      }
      return this.ReturnQuietNaN(value, ctx);
    }

    private T CheckNotANumber1(T val, PrecisionContext ctx) {
      return this.HandleNotANumber(val, val, ctx);
    }

    private T CheckNotANumber2(T val, T val2, PrecisionContext ctx) {
      return this.HandleNotANumber(val, val2, ctx);
    }

    private T RoundBeforeOp(T val, PrecisionContext ctx) {
      if (ctx == null || !ctx.HasMaxPrecision) {
        return val;
      }
      PrecisionContext ctx2 = ctx.WithUnlimitedExponents().WithBlankFlags().WithTraps(0);
      val = this.wrapper.RoundToPrecision(val, ctx2);
      // the only time rounding can signal an invalid
      // operation is if an operand is signaling NaN, but
      // this was already checked beforehand
      #if DEBUG
      if (!((ctx2.Flags & PrecisionContext.FlagInvalid) == 0)) {
        throw new ArgumentException("doesn't satisfy (ctx2.Flags&PrecisionContext.FlagInvalid)==0");
      }
      #endif
      if ((ctx2.Flags & PrecisionContext.FlagInexact) != 0) {
        if (ctx.HasFlags) {
          int newflags = PrecisionContext.FlagLostDigits | PrecisionContext.FlagInexact |
            PrecisionContext.FlagRounded;
          ctx.Flags |= newflags;
        }
      }
      if ((ctx2.Flags & PrecisionContext.FlagRounded) != 0) {
        if (ctx.HasFlags) {
          ctx.Flags |= PrecisionContext.FlagRounded;
        }
      }
      return val;
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='divisor'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T DivideToIntegerNaturalScale(T thisValue, T divisor, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.DivideToIntegerNaturalScale(thisValue, divisor, ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='divisor'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T DivideToIntegerZeroScale(T thisValue, T divisor, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.DivideToIntegerZeroScale(thisValue, divisor, ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='value'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Abs(T value, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(value, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      value = this.RoundBeforeOp(value, ctx2);
      value = this.wrapper.Abs(value, ctx2);
      return this.PostProcess(value, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='value'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Negate(T value, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(value, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      value = this.RoundBeforeOp(value, ctx2);
      value = this.wrapper.Negate(value, ctx2);
      return this.PostProcess(value, ctx, ctx2);
    }

    /// <summary>Finds the remainder that results when dividing two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The remainder of the two objects.</returns>
    public T Remainder(T thisValue, T divisor, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.Remainder(thisValue, divisor, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='divisor'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RemainderNear(T thisValue, T divisor, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.RemainderNear(thisValue, divisor, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Pi(PrecisionContext ctx) {
      return this.wrapper.Pi(ctx);
    }

    /*
    private T PowerIntegral(
      T thisValue,
      BigInteger powIntBig,
      PrecisionContext ctx) {
      int sign = powIntBig.Sign;
      T one = this.GetHelper().ValueOf(1);

      if (sign == 0) {
        // however 0 to the power of 0 is undefined
        return this.wrapper.RoundToPrecision(one, ctx);
      } else if (powIntBig.Equals(BigInteger.One)) {
        return this.wrapper.RoundToPrecision(thisValue, ctx);
      }
      bool retvalNeg = (this.GetHelper().GetFlags(thisValue) &
                        BigNumberFlags.FlagNegative) != 0 && !powIntBig.IsEven;
      FastInteger error = this.GetHelper().CreateShiftAccumulator(
        BigInteger.Abs(powIntBig)).GetDigitLength();
      error.AddInt(6);
      BigInteger bigError = error.AsBigInteger();
      PrecisionContext ctxdiv = ctx.WithBigPrecision(ctx.Precision + (BigInteger)bigError)
        .WithRounding(this.GetHelper().GetRadix() == 2 ?
                      Rounding.HalfEven : Rounding.ZeroFiveUp).WithBlankFlags();
      bool negativeSign = sign < 0;
      if (negativeSign) {
        powIntBig = -powIntBig;
      }
      // Console.WriteLine("pow=" + powIntBig + " negsign=" + negativeSign);
      T r = one;
      bool first = true;
      int b = powIntBig.bitLength();
      bool inexact = false;
      // Console.WriteLine("starting pow prec=" + ctxdiv.Precision);
      for (int i = b - 1; i >= 0; --i) {
        bool bit = powIntBig.testBit(i);
        if (bit) {
          ctxdiv.Flags = 0;
          if (first) {
            r = thisValue;
            first = false;
          } else {
            ctxdiv.Flags = 0;
            r = this.wrapper.Multiply(r, thisValue, ctxdiv);
            // Console.WriteLine("mult " + r);
            if ((ctxdiv.Flags & PrecisionContext.FlagInexact) != 0) {
              inexact = true;
            }
            if ((ctxdiv.Flags & PrecisionContext.FlagOverflow) != 0) {
              // Avoid multiplying too huge numbers with
              // limited exponent range
              return this.SignalOverflow2(ctx, retvalNeg);
            }
          }
        }
        if (i > 0 && !first) {
          ctxdiv.Flags = 0;
          r = this.wrapper.Multiply(r, r, ctxdiv);
          // Console.WriteLine("sqr " + r);
          if ((ctxdiv.Flags & PrecisionContext.FlagInexact) != 0) {
            inexact = true;
          }
          if ((ctxdiv.Flags & PrecisionContext.FlagOverflow) != 0) {
            // Avoid multiplying too huge numbers with
            // limited exponent range
            return this.SignalOverflow2(ctx, retvalNeg);
          }
        }
      }
      if (negativeSign) {
        // Use the reciprocal for negative powers
        ctxdiv.Flags = 0;
        r = this.wrapper.Divide(one, r, ctx);
        // Console.WriteLine("Flags=" + ctxdiv.Flags);
        if ((ctxdiv.Flags & PrecisionContext.FlagOverflow) != 0) {
          return this.SignalOverflow2(ctx, retvalNeg);
        }
        // Console.WriteLine("Exp=" + this.GetHelper().GetExponent(r) + " Prec=" + ctx.Precision + " Digits=" + (this.GetHelper().CreateShiftAccumulator(
        // BigInteger.Abs(powIntBig)).GetDigitLength()));
        if (ctx != null && ctx.HasFlags) {
          if (inexact) {
            ctx.Flags |= PrecisionContext.FlagRounded;
          }
        }
        return r;
      } else {
        return this.wrapper.RoundToPrecision(r, ctx);
      }
    }
*/
 /*
    private T SignalOverflow2(PrecisionContext pc, bool neg) {
      if (pc != null) {
        Rounding roundingOnOverflow = pc.Rounding;
        if (pc.HasFlags) {
          pc.Flags |= PrecisionContext.FlagOverflow | PrecisionContext.FlagInexact | PrecisionContext.FlagRounded;
        }
        if (pc.HasMaxPrecision && pc.HasExponentRange &&
            (roundingOnOverflow == Rounding.Down || roundingOnOverflow == Rounding.ZeroFiveUp ||
             (roundingOnOverflow == Rounding.Ceiling && neg) || (roundingOnOverflow == Rounding.Floor && !neg))) {
          // Set to the highest possible value for
          // the given precision
          BigInteger overflowMant = BigInteger.Zero;
          FastInteger fastPrecision = FastInteger.FromBig(pc.Precision);
          overflowMant = this.GetHelper().MultiplyByRadixPower(BigInteger.One, fastPrecision);
          overflowMant -= BigInteger.One;
          FastInteger clamp = FastInteger.FromBig(pc.EMax).Increment().Subtract(fastPrecision);
          return this.GetHelper().CreateNewWithFlags(overflowMant, clamp.AsBigInteger(), neg ? BigNumberFlags.FlagNegative : 0);
        }
      }
      return this.GetHelper().GetArithmeticSupport() == BigNumberFlags.FiniteOnly ?
        default(T) : this.GetHelper().CreateNewWithFlags(BigInteger.Zero, BigInteger.Zero, (neg ? BigNumberFlags.FlagNegative : 0) | BigNumberFlags.FlagInfinity);
    }
    */
    /*

    private T NegateRaw(T val) {
      if (val == null) {
        return val;
      }
      int sign = this.GetHelper().GetFlags(val) & BigNumberFlags.FlagNegative;
      return this.GetHelper().CreateNewWithFlags(
        this.GetHelper().GetMantissa(val),
        this.GetHelper().GetExponent(val),
        sign == 0 ? BigNumberFlags.FlagNegative : 0);
    }
*/

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='pow'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Power(T thisValue, T pow, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, pow, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      pow = this.RoundBeforeOp(pow, ctx2);
      int powSign = this.GetHelper().GetSign(pow);
      if (powSign == 0 && this.GetHelper().GetSign(thisValue) == 0) {
        thisValue = this.GetHelper().ValueOf(1);
      } else {
        // Console.WriteLine("was " + thisValue);
        // BigInteger powExponent = this.GetHelper().GetExponent(pow);
        // BigInteger powInteger = BigInteger.Abs(this.GetHelper().GetMantissa(pow));
        {
          thisValue = this.wrapper.Power(thisValue, pow, ctx2);
        }
      }
      // Console.WriteLine("was " + thisValue);
      thisValue = this.PostProcessAfterDivision(thisValue, ctx, ctx2);
      // Console.WriteLine("now " + thisValue);
      return thisValue;
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Log10(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.Log10(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Ln(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      // Console.WriteLine("was: " + thisValue);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      // Console.WriteLine("now: " + thisValue);
      thisValue = this.wrapper.Ln(thisValue, ctx2);
      // Console.WriteLine("result: " + thisValue);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <returns>An IRadixMathHelper(T) object.</returns>
    public IRadixMathHelper<T> GetHelper() {
      return this.wrapper.GetHelper();
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Exp(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.Exp(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T SquareRoot(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.SquareRoot(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextMinus(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.NextMinus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='otherValue'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextToward(T thisValue, T otherValue, PrecisionContext ctx) {
      throw new NotImplementedException();
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextPlus(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.NextPlus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='divisor'>A T object. (3).</param>
    /// <param name='desiredExponent'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T DivideToExponent(T thisValue, T divisor, BigInteger desiredExponent, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.DivideToExponent(thisValue, divisor, desiredExponent, ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    /// <summary>Divides two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The quotient of the two objects.</returns>
    public T Divide(T thisValue, T divisor, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, divisor, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      divisor = this.RoundBeforeOp(divisor, ctx2);
      thisValue = this.wrapper.Divide(thisValue, divisor, ctx2);
      return this.PostProcessAfterDivision(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='a'>A T object. (2).</param>
    /// <param name='b'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T MinMagnitude(T a, T b, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      a = this.RoundBeforeOp(a, ctx2);
      b = this.RoundBeforeOp(b, ctx2);
      a = this.wrapper.MinMagnitude(a, b, ctx2);
      return this.PostProcess(a, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='a'>A T object. (2).</param>
    /// <param name='b'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T MaxMagnitude(T a, T b, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      a = this.RoundBeforeOp(a, ctx2);
      b = this.RoundBeforeOp(b, ctx2);
      a = this.wrapper.MaxMagnitude(a, b, ctx2);
      return this.PostProcess(a, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='a'>A T object. (2).</param>
    /// <param name='b'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Max(T a, T b, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      a = this.RoundBeforeOp(a, ctx2);
      b = this.RoundBeforeOp(b, ctx2);
      // choose the left operand if both are equal
      a = (this.CompareTo(a, b) >= 0) ? a : b;
      return this.PostProcess(a, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='a'>A T object. (2).</param>
    /// <param name='b'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Min(T a, T b, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(a, b, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      a = this.RoundBeforeOp(a, ctx2);
      b = this.RoundBeforeOp(b, ctx2);
      // choose the left operand if both are equal
      a = (this.CompareTo(a, b) <= 0) ? a : b;
      return this.PostProcess(a, ctx, ctx2);
    }

    /// <summary>Multiplies two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='other'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The product of the two objects.</returns>
    public T Multiply(T thisValue, T other, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, other, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      other = this.RoundBeforeOp(other, ctx2);
      thisValue = this.wrapper.Multiply(thisValue, other, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='multiplicand'>A T object. (3).</param>
    /// <param name='augend'>A T object. (4).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T MultiplyAndAdd(T thisValue, T multiplicand, T augend, PrecisionContext ctx) {
      return this.SignalInvalid(ctx);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToBinaryPrecision(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.RoundToBinaryPrecision(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Plus(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.Plus(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToPrecision(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.RoundToPrecision(thisValue, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='otherValue'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Quantize(T thisValue, T otherValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      otherValue = this.RoundBeforeOp(otherValue, ctx2);
      thisValue = this.wrapper.Quantize(thisValue, otherValue, ctx2);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='expOther'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToExponentExact(T thisValue, BigInteger expOther, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentExact(thisValue, expOther, ctx);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='expOther'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToExponentSimple(T thisValue, BigInteger expOther, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentSimple(thisValue, expOther, ctx2);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='exponent'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToExponentNoRoundedFlag(T thisValue, BigInteger exponent, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.RoundToExponentNoRoundedFlag(thisValue, exponent, ctx);
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Reduce(T thisValue, PrecisionContext ctx) {
      T ret = this.CheckNotANumber1(thisValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      thisValue = this.wrapper.Reduce(thisValue, ctx);
      return this.PostProcessAfterQuantize(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='other'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Add(T thisValue, T other, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, other, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      PrecisionContext ctx2 = GetContextWithFlags(ctx);
      thisValue = this.RoundBeforeOp(thisValue, ctx2);
      other = this.RoundBeforeOp(other, ctx2);
      bool zeroA = this.GetHelper().GetSign(thisValue) == 0;
      bool zeroB = this.GetHelper().GetSign(other) == 0;
      if (zeroA) {
        thisValue = zeroB ? this.GetHelper().ValueOf(0) : other;
        thisValue = this.RoundToPrecision(thisValue, ctx2);
      } else if (!zeroB) {
        thisValue = this.wrapper.AddEx(thisValue, other, ctx2, true);
      } else {
        thisValue = this.RoundToPrecision(thisValue, ctx2);
      }
      return this.PostProcess(thisValue, ctx, ctx2);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='other'>A T object. (3).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <param name='roundToOperandPrecision'>A Boolean object.</param>
    /// <returns>A T object.</returns>
    public T AddEx(T thisValue, T other, PrecisionContext ctx, bool roundToOperandPrecision) {
      // NOTE: Ignores roundToOperandPrecision
      return this.Add(thisValue, other, ctx);
    }

    /// <summary>Compares a T object with this instance.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='otherValue'>A T object. (2).</param>
    /// <param name='treatQuietNansAsSignaling'>A Boolean object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>Zero if the values are equal; a negative number if this instance
    /// is less, or a positive number if this instance is greater.</returns>
    public T CompareToWithContext(T thisValue, T otherValue, bool treatQuietNansAsSignaling, PrecisionContext ctx) {
      T ret = this.CheckNotANumber2(thisValue, otherValue, ctx);
      if ((object)ret != (object)default(T)) {
        return ret;
      }
      thisValue = this.RoundBeforeOp(thisValue, ctx);
      otherValue = this.RoundBeforeOp(otherValue, ctx);
      return this.wrapper.CompareToWithContext(
        thisValue,
        otherValue,
        treatQuietNansAsSignaling,
        ctx);
    }

    /// <summary>Compares a T object with this instance.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='otherValue'>A T object. (2).</param>
    /// <returns>Zero if the values are equal; a negative number if this instance
    /// is less, or a positive number if this instance is greater.</returns>
    public int CompareTo(T thisValue, T otherValue) {
      return this.wrapper.CompareTo(thisValue, otherValue);
    }

    /// <summary>Not documented yet.</summary>
    /// <param name='thisValue'>A T object. (2).</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToPrecisionRaw(T thisValue, PrecisionContext ctx) {
      // Console.WriteLine("toprecraw " + thisValue);
      return this.wrapper.RoundToPrecisionRaw(thisValue, ctx);
    }
  }
}
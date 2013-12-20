package com.upokecenter.test;
/*
Written in 2013 by Peter O.
Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://peteroupc.github.io/CBOR/
 */

import org.junit.Assert;


//import java.math.*;
import com.upokecenter.util.*;
import java.io.*;

  final class TestCommon {
private TestCommon(){}

    public static void AssertBigIntegersEqual(String a, BigInteger b) {
      Assert.assertEquals(a,b.toString());
      BigInteger a2=BigInteger.fromString(a);
      Assert.assertEquals(a2,b);
      AssertEqualsHashCode(a2,b);
    }
    public static void DoTestDivide(String dividend, String divisor, String result) {
      BigInteger bigintA=BigInteger.fromString(dividend);
      BigInteger bigintB=BigInteger.fromString(divisor);
      if(bigintB.signum()==0){
        try { bigintA.divide(bigintB); Assert.fail("Expected divide by 0 error");
        } catch(Exception ex){ }
      } else {
        AssertBigIntegersEqual(result,bigintA.divide(bigintB));
      }
    }
    public static void DoTestRemainder(String dividend, String divisor, String result) {
      BigInteger bigintA=BigInteger.fromString(dividend);
      BigInteger bigintB=BigInteger.fromString(divisor);
      if(bigintB.signum()==0){
        try { bigintA.remainder(bigintB); Assert.fail("Expected divide by 0 error");
        } catch(Exception ex){ }
      } else {
        AssertBigIntegersEqual(result,(bigintA.remainder(bigintB)));
      }
    }
    public static void DoTestDivideAndRemainder(String dividend, String divisor, String result, String rem) {
      BigInteger bigintA=BigInteger.fromString(dividend);
      BigInteger bigintB=BigInteger.fromString(divisor);
      BigInteger rembi;
      if(bigintB.signum()==0){
        try {
          BigInteger quo;
BigInteger[] divrem=(bigintA).divideAndRemainder(bigintB);
quo=divrem[0];
rembi=divrem[1];
          Assert.fail("Expected divide by 0 error");
        } catch(Exception ex){ }
      } else {
        BigInteger quo;
BigInteger[] divrem=(bigintA).divideAndRemainder(bigintB);
quo=divrem[0];
rembi=divrem[1];
        AssertBigIntegersEqual(result,quo);
        AssertBigIntegersEqual(rem,rembi);
      }
    }
    public static void DoTestMultiply(String m1, String m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      BigInteger bigintB=BigInteger.fromString(m2);
      AssertBigIntegersEqual(result,(bigintA.multiply(bigintB)));
    }
    public static void DoTestAdd(String m1, String m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      BigInteger bigintB=BigInteger.fromString(m2);
      AssertBigIntegersEqual(result,(bigintA.add(bigintB)));
    }
    public static void DoTestSubtract(String m1, String m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      BigInteger bigintB=BigInteger.fromString(m2);
      AssertBigIntegersEqual(result,(bigintA.subtract(bigintB)));
    }
    public static void DoTestPow(String m1, int m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      AssertBigIntegersEqual(result,(bigintA.pow(m2)));
    }
    public static void DoTestShiftLeft(String m1, int m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      AssertBigIntegersEqual(result,(bigintA.shiftLeft(m2)));
      AssertBigIntegersEqual(result,(bigintA.shiftRight(-m2)));
    }
    public static void DoTestShiftRight(String m1, int m2, String result) {
      BigInteger bigintA=BigInteger.fromString(m1);
      AssertBigIntegersEqual(result,(bigintA.shiftRight(m2)));
      AssertBigIntegersEqual(result,(bigintA.shiftLeft(-m2)));
    }
    
    public static void AssertDecFrac(DecimalFraction d3, String output) {
      if(output==null && d3!=null)Assert.fail("d3 must be null");
      if(output!=null && !d3.toString().equals(output)){
        DecimalFraction d4=DecimalFraction.FromString(output);
        Assert.assertEquals(output,d3.toString(),(
          "expected: ["+(d4.getMantissa()).toString()+","+(d4.getExponent()).toString()+"]\\n"+
          "but was: ["+(d3.getMantissa()).toString()+","+(d3.getExponent()).toString()+"]"
         ));   }
    }
    
    public static BigInteger BigIntParse(String str) {
      return BigInteger.fromString(str);
    }
    
    public static void AssertFlags(int expected, int actual) {
      if(expected==actual)return;
      Assert.assertEquals("Inexact",(expected&PrecisionContext.FlagInexact)!=0,(actual&PrecisionContext.FlagInexact)!=0);
      Assert.assertEquals("Rounded",(expected&PrecisionContext.FlagRounded)!=0,(actual&PrecisionContext.FlagRounded)!=0);
      Assert.assertEquals("Subnormal",(expected&PrecisionContext.FlagSubnormal)!=0,(actual&PrecisionContext.FlagSubnormal)!=0);
      Assert.assertEquals("Overflow",(expected&PrecisionContext.FlagOverflow)!=0,(actual&PrecisionContext.FlagOverflow)!=0);
      Assert.assertEquals("Underflow",(expected&PrecisionContext.FlagUnderflow)!=0,(actual&PrecisionContext.FlagUnderflow)!=0);
      Assert.assertEquals("Clamped",(expected&PrecisionContext.FlagClamped)!=0,(actual&PrecisionContext.FlagClamped)!=0);
    }
    
    private static CBORObject FromBytesA(byte[] b) {
      return CBORObject.DecodeFromBytes(b);
    }
    private static CBORObject FromBytesB(byte[] b) {
      java.io.ByteArrayInputStream ms=null;
try {
ms=new ByteArrayInputStream(b);
int startingAvailable=ms.available();

        CBORObject o = CBORObject.Read(ms);
        if ((startingAvailable-ms.available()) != startingAvailable)
          throw new CBORException("not at EOF");
        return o;
}
finally {
try { if(ms!=null)ms.close(); } catch(IOException ex){}
}
    }
    //
    //  Tests the equivalence of the FromBytes and Read methods.
    //
    public static CBORObject FromBytesTestAB(byte[] b) {
      CBORObject oa = FromBytesA(b);
      CBORObject ob = FromBytesB(b);
      if (!oa.equals(ob))
        Assert.assertEquals(oa, ob);
      return oa;
    }
    public static void AssertEqualsHashCode(Object o, Object o2) {
      if (o.equals(o2)) {
        if (!o2.equals(o))
          Assert.fail(
            String.format(java.util.Locale.US,"%s equals %s, but not vice versa", o, o2));
        // Test for the guarantee that equal objects
        // must have equal hash codes
        if (o2.hashCode() != o.hashCode()) {
          // Don't use Assert.assertEquals directly because it has
          // quite a lot of overhead
          Assert.fail(
            String.format(java.util.Locale.US,"%s and %s don't have equal hash codes", o, o2));
        }
      } else {
        if (o2.equals(o))
          Assert.fail(
            String.format(java.util.Locale.US,"%s does not equal %s, but not vice versa", o, o2));
      }
    }
    
    public static void TestNumber(CBORObject o) {
      if(o.getType()!= CBORType.Number){
        return;
      }
      if(o.IsPositiveInfinity() || o.IsNegativeInfinity() ||
         o.IsNaN()){
        try { o.AsByte(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsInt16(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsInt32(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsInt64(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsSingle(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsDouble(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsBigFloat(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsBigInteger(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        try { o.AsDecimalFraction(); } catch(ArithmeticException ex){ } catch(Exception ex){ Assert.fail("Object: "+o+", "+ex.toString()); }
        return;
      }
      BigInteger df=o.AsDecimalFraction().ToBigInteger();
      try { o.AsBigInteger(); } catch(Exception ex){ Assert.fail("Object: "+o+", int: "+df+", "+ex.toString()); }
      try { o.AsBigFloat(); } catch(Exception ex){ Assert.fail("Object: "+o+", int: "+df+", "+ex.toString()); }
      try { o.AsSingle(); } catch(Exception ex){ Assert.fail("Object: "+o+", int: "+df+", "+ex.toString()); }
      try { o.AsDouble(); } catch(Exception ex){ Assert.fail("Object: "+o+", int: "+df+", "+ex.toString()); }
    }
    
    public static void AssertRoundTrip(CBORObject o) {
      CBORObject o2 = FromBytesTestAB(o.EncodeToBytes());
      if (o2.getType() == CBORType.Map && o.getType() == CBORType.Map) {
        // Skip because key order may be different
      } else {
        if (!o.toString().equals(o2.toString()))
          Assert.assertEquals("o2 is not equal to o",o.toString(),o2.toString());
      }
      TestNumber(o);
      AssertEqualsHashCode(o, o2);
    }
    public static void AssertSer(CBORObject o, String s) {
      if (!s.equals(o.toString()))
        Assert.assertEquals("o is not equal to s",s,o.toString());
      // Test round-tripping
      CBORObject o2 = FromBytesTestAB(o.EncodeToBytes());
      if (!s.equals(o2.toString()))
        Assert.assertEquals("o2 is not equal to s",s,o2.toString());
      TestNumber(o);
      AssertEqualsHashCode(o, o2);
    }
  }
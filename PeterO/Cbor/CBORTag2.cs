/*
Written in 2014 by Peter O.
Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://upokecenter.dreamhosters.com/articles/donate-now-2/
 */
using System;
using PeterO;

namespace PeterO.Cbor {
  internal class CBORTag2 : ICBORTag
  {
    public CBORTypeFilter GetTypeFilter() {
      return CBORTypeFilter.ByteString;
    }

    private static CBORObject FromObjectAndInnerTags(
object objectValue,
CBORObject objectWithTags) {
      CBORObject newObject = CBORObject.FromObject(objectValue);
      if (!objectWithTags.IsTagged) {
        return newObject;
      }
      objectWithTags = objectWithTags.UntagOne();
      if (!objectWithTags.IsTagged) {
        return newObject;
      }
      BigInteger[] tags = objectWithTags.GetTags();
      for (int i = tags.Length - 1; i >= 0; --i) {
        newObject = CBORObject.FromObjectAndTag(newObject, tags[i]);
      }
      return newObject;
    }

    internal static CBORObject ConvertToBigNum(CBORObject o, bool negative) {
      if (o.Type != CBORType.ByteString) {
        throw new CBORException("Byte array expected");
      }
      byte[] data = o.GetByteString();
      if (data.Length <= 7) {
        long x = 0;
        for (var i = 0; i < data.Length; ++i) {
          x <<= 8;
          x |= ((long)data[i]) & 0xff;
        }
        if (negative) {
          x = -x;
          x -= 1L;
        }
        return FromObjectAndInnerTags(x, o);
      }
      int neededLength = data.Length;
      byte[] bytes;
      BigInteger bi;
      bool extended = false;
      if (((data[0] >> 7) & 1) != 0) {
        // Increase the needed length
        // if the highest bit is set, to
        // distinguish negative and positive
        // values
        ++neededLength;
        extended = true;
      }
      bytes = new byte[neededLength];
      for (var i = 0; i < data.Length; ++i) {
        bytes[i] = data[data.Length - 1 - i];
        if (negative) {
          bytes[i] = (byte)((~((int)bytes[i])) & 0xff);
        }
      }
      if (extended) {
          bytes[bytes.Length - 1] = negative ? (byte)0xff : (byte)0;
      }
      bi = BigInteger.fromByteArray(bytes, true);
      // NOTE: Here, any tags are discarded; when called from
      // the Read method, "o" will have no tags anyway (beyond tag 2),
      // and when called from FromObjectAndTag, we prefer
      // flexibility over throwing an error if the input
      // object contains other tags. The tag 2 is also discarded
      // because we are returning a "natively" supported CBOR object.
      return CBORObject.FromObject(bi);
    }

    public CBORObject ValidateObject(CBORObject obj) {
      return ConvertToBigNum(obj, false);
    }
  }
}
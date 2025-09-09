using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace bhl
{

public static class Extensions
{
  public static string GetFullMessage(this Exception ex)
  {
    return ex.InnerException == null
      ? ex.Message
      : ex.Message + " --> " + ex.InnerException.GetFullMessage();
  }

  public static Stream ToStream(this string str)
  {
    MemoryStream stream = new MemoryStream();
    StreamWriter writer = new StreamWriter(stream);
    writer.Write(str);
    writer.Flush();
    stream.Position = 0;
    return stream;
  }

  public static void SetData(this MemoryStream ms, byte[] b, int offset, int blen)
  {
    if(ms.Capacity < b.Length)
      ms.Capacity = b.Length;

    ms.SetLength(0);
    ms.Write(b, offset, blen);
    ms.Position = 0;
  }

  public static int CopyTo(this Stream src, Stream dest)
  {
    int size = (src.CanSeek) ? Math.Min((int)(src.Length - src.Position), 0x2000) : 0x2000;
    byte[] buffer = new byte[size];
    int total = 0;
    int n;
    do
    {
      n = src.Read(buffer, 0, buffer.Length);
      dest.Write(buffer, 0, n);
      total += n;
    } while (n != 0);

    return total;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Val PopRelease(this ValStack stack)
  {
    var val = stack.Pop();
    val.Release();
    return val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void PushRetain(this ValStack stack, Val val)
  {
    val.Retain();
    stack.Push(val);
  }

  public static void Assign(this ValStack stack, VM vm, int idx, Val val)
  {
    var curr = stack[idx];
    if(curr != null)
    {
      for(int i = 0; i < curr._refs; ++i)
      {
        val._refc?.Retain();
        curr._refc?.Release();
      }

      curr.ValueCopyFrom(val);
    }
    else
    {
      curr = Val.New(vm);
      curr.ValueCopyFrom(val);
      curr._refc?.Retain();
      stack[idx] = curr;
    }
  }

  public static FuncSignatureAttrib ToFuncSignatureAttrib(this FuncAttrib attrib)
  {
    return (FuncSignatureAttrib)((byte)FuncSignatureAttrib.FuncAttribMask & (byte)attrib);
  }

  public static void SetFuncAttrib(this FuncSignatureAttrib attrib, ref byte other)
  {
    //NOTE: we want to force FuncSignatureAttrib bits only, for this we mask out
    //      these bits in the target value and 'or' the bits we want to set
    other = (byte)((other & (byte)~FuncSignatureAttrib.FuncAttribMask) | (byte)attrib);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe int SizeOf<T>() where T : unmanaged
  {
    return sizeof(T);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe ref T UsafeAsRef<T>(void* source) where T : unmanaged
  {
    return ref *(T*)source;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe ref TTo UnsafeAs<TFrom, TTo>(ref TFrom source) where TTo : unmanaged where TFrom : unmanaged
  {
    fixed(void* p = &source)
    {
      return ref UsafeAsRef<TTo>(p);
    }
  }
}

}
#define UNSAFE_BITOPS
using System.Runtime.CompilerServices;
using System;
using System.IO;

namespace bhl {
  
public class Bytecode
{
  public ushort Position { get { return (ushort)stream.Position; } }

  MemoryStream stream = new MemoryStream();

  public void Reset(byte[] buffer, int size)
  {
    stream.SetLength(0);
    stream.Write(buffer, 0, size);
    stream.Position = 0;
  }

  public byte[] GetBytes()
  {
    return stream.ToArray();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static byte Decode8(byte[] bytecode, ref int ip)
  {
    return bytecode[++ip];
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe byte Decode8(byte* bytecode, ref int ip)
  {
    return bytecode[++ip];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static ushort Decode16(byte[] bytecode, ref int ip)
  {
#if UNSAFE_BITOPS
    unsafe 
    {
      fixed(byte* p = bytecode)
      {
        ushort val = (ushort)
        ((uint)p[ip+1] | 
         ((uint)p[ip+2]) << 8
         );
        ip += 2;
        return val;
      }
    }
#else
    ushort val = (ushort)
      ((uint)bytecode[ip+1] | 
       ((uint)bytecode[ip+2]) << 8
       );
    ip += 2;
    return val;
#endif
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe uint Decode16(byte* p, ref int ip)
  {
    uint val = 
    ((uint)p[ip+1] | 
     ((uint)p[ip+2]) << 8
     );
    ip += 2;
    return val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static uint Decode24(byte[] bytecode, ref int ip)
  {
#if UNSAFE_BITOPS
    unsafe 
    {
      fixed(byte* p = bytecode)
      {
        uint val =
        ((uint)p[ip+1]         | 
         ((uint)p[ip+2]) << 8  |
         ((uint)p[ip+3]) << 16
         );
        ip += 3;
        return val;
      }
    }
#else
    uint val =
      ((uint)bytecode[ip+1]        | 
       ((uint)bytecode[ip+2]) << 8 |
       ((uint)bytecode[ip+3]) << 16
       );
    ip += 3;
    return val;
#endif
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe uint Decode24(byte* p, ref int ip)
  {
    uint val =
    ((uint)p[ip+1]         | 
     ((uint)p[ip+2]) << 8  |
     ((uint)p[ip+3]) << 16
     );
    ip += 3;
    return val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static uint Decode32(byte[] bytecode, ref int ip)
  {
#if UNSAFE_BITOPS
    unsafe 
    {
      fixed(byte* p = bytecode)
      {
        uint val = (uint)
        ((uint)p[ip+1]         | 
         ((uint)p[ip+2]) << 8  |
         ((uint)p[ip+3]) << 16 |
         ((uint)p[ip+4]) << 24
         );
        ip += 4;
        return val;
      }
    }
#else
    uint val = (uint)
      ((uint)bytecode[ip+1]         | 
       ((uint)bytecode[ip+2] << 8)  |
       ((uint)bytecode[ip+3] << 16) |
       ((uint)bytecode[ip+4] << 24)
       );
    ip += 4;
    return val;
#endif
  }
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe uint Decode32(byte* p, ref int ip)
  {
     uint val =
     ((uint)p[ip+1]         | 
      ((uint)p[ip+2]) << 8  |
      ((uint)p[ip+3]) << 16 |
      ((uint)p[ip+4]) << 24
      );
     ip += 4;
     return val;
  }

  public static uint Decode(byte[] bytecode, int num_bytes, ref int ip)
  {
    if(num_bytes < 1 || num_bytes > 4)
      throw new Exception("Invalid amount of bytes: " + num_bytes);

    uint val = 0;

    if(num_bytes >= 1)
      val |= (uint)bytecode[ip+1];

    if(num_bytes >= 2)
      val |= ((uint)bytecode[ip+2]) << 8;

    if(num_bytes >= 3)
      val |= ((uint)bytecode[ip+3]) << 16;

    if(num_bytes == 4)
      val |= ((uint)bytecode[ip+4]) << 24;

    ip += num_bytes;

    return val;
  }

  public int Write(byte value)
  {
    return Write8(value);
  }

  public int Write(ushort value)
  {
    return Write16(value);
  }

  public int Write(uint value)
  {
    return Write32(value);
  }

  public int Write(bool value)
  {
    stream.WriteByte((byte)(value ? 1 : 0));
    return 1;
  }

  public int Write8(uint value)
  {
    stream.WriteByte((byte)(value & 0xFF));
    return 1;
  }

  public int Write16(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    return 2;
  }

  public int Write24(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    Write8((byte)((value >> 16) & 0xFF));
    return 3;
  }

  public int Write32(uint value)
  {
    Write8((byte)(value & 0xFF));
    Write8((byte)((value >> 8) & 0xFF));
    Write8((byte)((value >> 16) & 0xFF));
    Write8((byte)((value >> 24) & 0xFF));
    return 4;
  }

  public int Write(uint value, int num_bytes)
  {
    if(num_bytes < 1 || num_bytes > 4)
      throw new Exception("Invalid amount of bytes: " + num_bytes);

    if(num_bytes >= 1)
      Write8((byte)(value & 0xFF));
    if(num_bytes >= 2)
      Write8((byte)((value >> 8) & 0xFF));
    if(num_bytes >= 3)
      Write8((byte)((value >> 16) & 0xFF));
    if(num_bytes == 4)
      Write8((byte)((value >> 24) & 0xFF));

    return num_bytes;
  }

  public void Write(Bytecode buffer)
  {
    buffer.WriteTo(stream);
  }

  void WriteTo(MemoryStream buffer_stream)
  {
    stream.WriteTo(buffer_stream);
  }

  public void PatchAt(int pos, uint value, int num_bytes)
  {
    long orig_pos = stream.Position;
    stream.Position = pos;
    Write(value, num_bytes);
    stream.Position = orig_pos;
  }
}

}

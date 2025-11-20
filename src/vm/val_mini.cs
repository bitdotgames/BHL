#if !USE_VAL_MINI
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using bhl;

namespace bhl
{

[StructLayout(LayoutKind.Explicit)]
public struct Val
{
  [FieldOffset(0)] public long _long0;
  [FieldOffset(8)] public long _long1;

  [FieldOffset(0)] public double _double0;
  [FieldOffset(8)] public double _double1;

  [FieldOffset(0)] public int _int0;
  [FieldOffset(4)] public int _int1;
  [FieldOffset(8)] public int _int2;
  [FieldOffset(12)] public int _int3;

  [FieldOffset(0)] public float _float0;
  [FieldOffset(4)] public float _float1;
  [FieldOffset(8)] public float _float2;
  [FieldOffset(12)] public float _float3;

  [FieldOffset(0)] public float num;
  [FieldOffset(4)] public float _num2;
  [FieldOffset(8)] public float _num3;
  [FieldOffset(12)] public float _num4;

  [FieldOffset(16)] public IntPtr object_ref;
  [FieldOffset(24)] public uint type_id;

  public string str
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => throw new NotImplementedException();
  }

  public IRefcounted _refc
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => null; // TODO
    set { }
  }

  public IRefcounted obj
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => null; // TODO
    set { }
  }

  public bool bval
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => _int0 == 1;
  }

  public IType type
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => SymbolFactory.TypeById(type_id);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator double(Val v)
  {
    return v._double0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(double v)
  {
    return NewFlt(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(float v)
  {
    return NewFlt(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator float(Val v)
  {
    //TODO: are we sure it's allowed?
    return v.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator int(Val v)
  {
    return v._int0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(int v)
  {
    return NewInt(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator string(Val v)
  {
    return v.str;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(string v)
  {
    return NewStr(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator bool(Val v)
  {
    return v._int0 == 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(bool v)
  {
    return NewBool(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref Val Unref()
  {
    // var val_ref = (ValRef)_refc;
    // val_ref.Release();
    // return ref val_ref.val;
    throw new NotImplementedException();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Reset()
  {
    type_id = 0;
    num = 0;
    _num2 = 0;
    _num3 = 0;
    _num4 = 0;
    object_ref = IntPtr.Zero;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Val Clone()
  {
    var copy = this;
    // copy._refc?.Retain();
    return copy;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RetainData()
  {
    // _refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ReleaseData()
  {
    // _refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static unsafe public Val NewStr(string s)
  {
    throw new NotImplementedException();
    return new Val
    {
      type_id = Types.String.ClassId(),
      object_ref = (IntPtr)Unsafe.AsPointer(ref s), // TODO: wont work in CLR GC
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public unsafe void SetStr(string s)
  {
    throw new NotImplementedException();
    Reset();
    type_id = Types.String.ClassId();
    object_ref = (IntPtr)Unsafe.AsPointer(ref s); // TODO: wont work in CLR GC
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewNum(long n)
  {
    return new Val
    {
      type_id = Types.Int.ClassId(),
      _long0 = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetNum(long n)
  {
    Reset();
    type_id = Types.Int.ClassId();
    _long0 = n;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewInt(int n)
  {
    return new Val
    {
      type_id = Types.Int.ClassId(),
      _int0 = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetInt(int n)
  {
    Reset();
    type_id = Types.Int.ClassId();
    _int0 = n;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewFlt(double n)
  {
    return new Val
    {
      type_id = Types.Float.ClassId(),
      _double0 = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetFlt(double n)
  {
    Reset();
    type_id = Types.Float.ClassId();
    _double0 = n;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewBool(bool b)
  {
    return new Val
    {
      type_id = Types.Bool.ClassId(),
      _int0 = b ? 1 : 0,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetBool(bool b)
  {
    Reset();
    type_id = Types.Bool.ClassId();
    _int0 = b ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewObj(object o, IType type)
  {
    throw new NotImplementedException();
    // return new Val
    // {
    //   type = type,
    //   obj = o,
    //   _refc = o as IRefcounted,
    // };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewObj(IRefcounted o, IType type)
  {
    return default;
    // throw new NotImplementedException();
    // return new Val
    // {
    //   type = type,
    //   obj = o,
    //   _refc = o
    // };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObj(object o, IType type)
  {
    throw new NotImplementedException();
    // Reset();
    // this.type = type;
    // obj = o;
    // _refc = o as IRefcounted;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsDataEqual(ref Val o)
  {
    bool res =
        type_id == o.type_id
        && _long0 == o._long0
        && _long1 == o._long1
        && object_ref == o.object_ref
      ;

    return res;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int GetDataHashCode()
  {
    return
      num.GetHashCode()
      ^ _num2.GetHashCode()
      ^ _num3.GetHashCode()
      ^ _num4.GetHashCode()
      ^ object_ref.GetHashCode()
      ;
  }

  public override string ToString()
  {
    string str = "";
    if(type != null)
      str += "(" + type.GetName() + ")";
    else
      str += "(?)";
    str += " num:" + num;
    str += " num2:" + _num2;
    str += " num3:" + _num3;
    str += " num4:" + _num4;
    str += " obj:" + object_ref;

    return str; // + " " + GetHashCode();//for extra debug
  }
}

#endif

}

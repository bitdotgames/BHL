using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace bhl
{
public struct Val
{
  public IType type;

  //NOTE: below members are semi-public, one can use them for
  //      fast access in case you know what you are doing

  public double num;

  public object obj;

  //NOTE: it's a cached version of obj cast to IValRefcounted for
  //      less casting in refcounting routines
  public IRefcounted _refc;

  //NOTE: extra values below are for efficient encoding of small structs,
  //      e.g Vector3, Color, etc
  public double _num2;
  public double _num3;
  public double _num4;

  public string str
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get { return (string)obj; }
  }

  public bool bval
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get { return num == 1; }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator double(Val v)
  {
    return v.num;
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
    return (float)v.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator int(Val v)
  {
    return (int)v.num;
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
    return v.num == 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static implicit operator Val(bool v)
  {
    return NewBool(v);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref Val Unref()
  {
    var val_ref = (ValRef)_refc;
    val_ref.Release();
    return ref val_ref.val;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Val Clone()
  {
    var copy = this;
    copy._refc?.Retain();
    return copy;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RetainData()
  {
    _refc?.Retain();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ReleaseData()
  {
    _refc?.Release();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewStr(string s)
  {
    return new Val
    {
      type = Types.String,
      obj = s,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetStr(string s)
  {
    this = NewStr(s);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewNum(long n)
  {
    return new Val
    {
      type = Types.Int,
      num = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetNum(long n)
  {
    this = NewInt(n);
  }

  //NOTE: it's caller's responsibility to ensure 'int precision'
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewInt(double n)
  {
    return new Val
    {
      type = Types.Int,
      num = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetInt(double n)
  {
    this = NewInt(n);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewFlt(double n)
  {
    return new Val
    {
      type = Types.Float,
      num = n,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetFlt(double n)
  {
    this = NewFlt(n);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewBool(bool b)
  {
    return new Val
    {
      type = Types.Bool,
      num = b ? 1 : 0,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetBool(bool b)
  {
    this = NewBool(b);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewObj(object o, IType type)
  {
    return new Val
    {
      type = type,
      obj = o,
      _refc = o as IRefcounted,
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewObj(IRefcounted o, IType type)
  {
    return new Val
    {
      type = type,
      obj = o,
      _refc = o
    };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObj(object o, IType type)
  {
    this = NewObj(o, type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObj(IRefcounted o, IType type)
  {
    this = NewObj(o, type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsDataEqual(ref Val o)
  {
    bool res =
        num == o.num &&
        _num2 == o._num2 &&
        _num3 == o._num3 &&
        _num4 == o._num4 &&
        (obj?.Equals(o.obj) ?? obj == o.obj)
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
      ^ (obj?.GetHashCode() ?? 0)
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
    str += " obj:" + obj;
    str += " obj.type:" + obj?.GetType().Name;
    str += " (refcs:" + _refc?.refs + ")";

    return str; // + " " + GetHashCode();//for extra debug
  }
}

public class ValStack
{
  public Val[] vals;
  //NOTE: sp always point to the position of the next value, sp - 1 points to the stack top
  public int sp;

  public ValStack(int init_capacity)
  {
    vals = new Val[init_capacity];
    sp = 0;
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public ref Val Push()
  {
    if(sp == vals.Length)
      Array.Resize(ref vals, sp << 1);

    return ref vals[sp++];
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public void Push(Val v)
  {
    if(sp == vals.Length)
      Array.Resize(ref vals, sp << 1);

    vals[sp++] = v;
  }

  //NOTE: extraction version which cleans the stack value
  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public void Pop(out Val res)
  {
    ref var tmp = ref vals[--sp];
    res = tmp; //making a copy
    tmp._refc = null; //cleaning up stack value
    tmp.obj = null; //cleaning up stack value
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public Val PopRelease()
  {
    ref var tmp = ref vals[--sp];
    var res = tmp; //making a copy
    tmp._refc = null; //cleaning up stack value
    res._refc?.Release();
    tmp.obj = null; //cleaning up stack value
    return res;
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public Val Pop()
  {
    ref var tmp = ref vals[--sp];
    var res = tmp; //making a copy
    tmp._refc = null; //cleaning up stack value
    tmp.obj = null; //cleaning up stack value
    return res;
  }

  //NOTE: super light version for cases when you know what you are doing
  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public ref Val PopFast()
  {
    return ref vals[--sp];
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public ref Val Peek()
  {
    return ref vals[sp - 1];
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public ref Val Peek(int n)
  {
    return ref vals[sp - n];
  }

  [MethodImpl (MethodImplOptions.AggressiveInlining)]
  public void Reserve(int num)
  {
    int needed = num - (vals.Length - sp);
    if(needed > 0)
      Array.Resize(ref vals, vals.Length + needed);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ClearAndRelease()
  {
    while(sp > 0)
    {
      Pop(out var val);
      val._refc?.Release();
    }
  }
}

public interface IRefcounted
{
  int refs { get ; }
  void Retain();
  void Release();
}

public class ValRef : IRefcounted
{
  //NOTE: below members are semi-public, one can use them for
  //      fast access in case you know what you are doing

  //NOTE: -1 means it's in released state
  public int _refs;

  public int refs => _refs;

  public VM vm;

  public Val val;

  //NOTE: use New() instead
  internal ValRef(VM vm)
  {
    this.vm = vm;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public ValRef New(VM vm)
  {
    ValRef vr;
    if(vm.vrefs_pool.stack.Count == 0)
    {
      ++vm.vrefs_pool.miss;
      vr = new ValRef(vm);
    }
    else
    {
      ++vm.vrefs_pool.hits;
      vr = vm.vrefs_pool.stack.Pop();
    }

    vr._refs = 1;
    vr.val = default;
    return vr;
  }

  static void Del(ValRef vr)
  {
    //NOTE: we don't Reset immediately, giving a caller
    //      a chance to access its properties
    if(vr._refs != 0)
      throw new Exception("Deleting invalid object, refs " + vr._refs);
    vr._refs = -1;

    vr.vm.vrefs_pool.stack.Push(vr);
    if(vr.vm.vrefs_pool.stack.Count > vr.vm.vrefs_pool.miss)
      throw new Exception("Unbalanced New/Del " + vr.vm.vrefs_pool.stack.Count + " " + vr.vm.vrefs_pool.miss);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Retain()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");

    ++_refs;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    else if(_refs == 0)
      throw new Exception("Double free(0)");

    --_refs;

    if(_refs == 0)
    {
      val._refc?.Release();
      Del(this);
    }
  }
}

}

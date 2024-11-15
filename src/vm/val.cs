//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl {

public struct RefOp
{
  public const int INC                  = 1;
  public const int DEC                  = 2;
  public const int USR_INC              = 4;
  public const int USR_DEC              = 8;
}

public interface IValRefcounted
{
  int refs { get ; }
  void Retain();
  void Release();
}

public class Val
{
  public IType type;

  //NOTE: below members are semi-public, one can use them for 
  //      fast access in case you know what you are doing
  //NOTE: -1 means it's in released state
  public int _refs;
  public double _num;
  public object _obj;
  //it's a cached version of _obj cast to IValRefcounted for less casting 
  //in refcounting routines
  public IValRefcounted _refc;
  //NOTE: extra values below are for efficient encoding of small structs,
  //      e.g Vector, Color, etc
  public double _num2;
  public double _num3;
  public double _num4;

  public double num {
    get {
      return _num;
    }
    set {
      SetFlt(value);
    }
  }

  public string str {
    get {
      return (string)_obj;
    }
    set {
      SetStr(value);
    }
  }

  public object obj {
    get {
      return _obj;
    }
  }

  public bool bval {
    get {
      return _num == 1;
    }
    set {
      SetBool(value);
    }
  }

  public bool is_null {
    get {
      return this == vm.Null;
    }
  }

  public VM vm;

  //NOTE: use New() instead
  internal Val(VM vm)
  {
    this.vm = vm;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val New(VM vm)
  {
    Val dv;
    if(vm.vals_pool.stack.Count == 0)
    {
      //for debug
      //if(vm.vals_pool.miss > 200)
      //{
      //  if(vm.last_fiber != null)
      //    Util.Debug(vm.last_fiber.GetStackTrace());
      //}

      ++vm.vals_pool.miss;
      dv = new Val(vm);
#if DEBUG_REFS
      vm.vals_pool.debug_track.Add(
        new VM.ValPool.Tracking() {
          v = dv,
          stack_trace = Environment.StackTrace
        }
      );
      Console.WriteLine("NEW: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    else
    {
      ++vm.vals_pool.hits;
      dv = vm.vals_pool.stack.Pop();
#if DEBUG_REFS
      Console.WriteLine("HIT: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    dv._refs = 1;
    dv.Reset();
    return dv;
  }

  static void Del(Val dv)
  {
    //NOTE: we don't Reset Val immediately, giving a caller
    //      a chance to access its properties
    if(dv._refs != 0)
      throw new Exception("Deleting invalid object, refs " + dv._refs);
    dv._refs = -1;

    dv.vm.vals_pool.stack.Push(dv);
    if(dv.vm.vals_pool.stack.Count > dv.vm.vals_pool.miss)
      throw new Exception("Unbalanced New/Del " + dv.vm.vals_pool.stack.Count + " " + dv.vm.vals_pool.miss);
  }

  //NOTE: refcount is not reset
  void Reset()
  {
    type = null;
    _num = 0;
    _num2 = 0;
    _num3 = 0;
    _num4 = 0;
    _obj = null;
    _refc = null;
  }

  //NOTE: doesn't affect refcounting
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ValueCopyFrom(Val dv)
  {
    type = dv.type;
    _num = dv._num;
    _num2 = dv._num2;
    _num3 = dv._num3;
    _num4 = dv._num4;
    _obj = dv._obj;
    _refc = dv._refc;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Val CloneValue()
  {
    var copy = Val.New(vm);
    copy.ValueCopyFrom(this);
    copy.RefMod(RefOp.USR_INC);
    return copy;
  }

  //NOTE: see RefOp for constants
  public void RefMod(int op)
  {
    if(_refc != null)
    {
      if((op & RefOp.USR_INC) != 0)
      {
        _refc.Retain();
      }
      else if((op & RefOp.USR_DEC) != 0)
      {
        _refc.Release();
      }
    }

    if((op & RefOp.INC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state(-1)");

      ++_refs;
#if DEBUG_REFS
      Console.WriteLine("INC: " + _refs + " " + this + " " + GetHashCode() + vm.last_fiber?.GetStackTrace()/* + " " + Environment.StackTrace*/);
#endif
    } 
    else if((op & RefOp.DEC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state(-1)");
      else if(_refs == 0)
        throw new Exception("Double free(0)");

      --_refs;
#if DEBUG_REFS
      Console.WriteLine("DEC: " + _refs + " " + this + " " + GetHashCode() + vm.last_fiber?.GetStackTrace()/* + " " + Environment.StackTrace*/);
#endif

      if(_refs == 0)
        Del(this);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Retain()
  {
    RefMod(RefOp.USR_INC | RefOp.INC);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
    RefMod(RefOp.USR_DEC | RefOp.DEC);
  }

  static public Val NewStr(VM vm, string s)
  {
    Val dv = New(vm);
    dv.SetStr(s);
    return dv;
  }

  public void SetStr(string s)
  {
    Reset();
    type = Types.String;
    _obj = s;
    _refc = null;
  }

  static public Val NewNum(VM vm, long n)
  {
    Val dv = New(vm);
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(long n)
  {
    Reset();
    type = Types.Int;
    _num = n;
  }

  //NOTE: it's caller's responsibility to ensure 'int precision'
  static public Val NewInt(VM vm, double n)
  {
    Val dv = New(vm);
    dv.SetInt(n);
    return dv;
  }

  static public Val NewFlt(VM vm, double n)
  {
    Val dv = New(vm);
    dv.SetFlt(n);
    return dv;
  }

  //NOTE: it's caller's responsibility to ensure 'int precision'
  public void SetInt(double n)
  {
    Reset();
    type = Types.Int;
    _num = n;
  }

  public void SetFlt(double n)
  {
    Reset();
    type = Types.Float;
    _num = n;
  }

  static public Val NewBool(VM vm, bool b)
  {
    Val dv = New(vm);
    dv.SetBool(b);
    return dv;
  }

  public void SetBool(bool b)
  {
    Reset();
    type = Types.Bool;
    _num = b ? 1 : 0;
  }

  static public Val NewObj(VM vm, object o, IType type)
  {
    Val dv = New(vm);
    dv.SetObj(o, type);
    return dv;
  }

  public void SetObj(object o, IType type)
  {
    Reset();
    this.type = type;
    _obj = o;
    _refc = o as IValRefcounted;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsValueEqual(Val o)
  {
    bool res =
      _num == o._num &&
      _num2 == o._num2 &&
      _num3 == o._num3 &&
      _num4 == o._num4 &&
      (_obj != null ? _obj.Equals(o._obj) : _obj == o._obj)
      ;

    return res;
  }

  public int GetValueHashCode()
  {
    return 
      _num.GetHashCode()
      ^ _num2.GetHashCode()
      ^ _num3.GetHashCode()
      ^ _num4.GetHashCode()
      ^ (int)(_obj == null ? 0 : _obj.GetHashCode())
      ;
  }

  public override string ToString() 
  {
    string str = "";
    if(type != null)
      str += "(" + type.GetName() + ")";
    else
      str += "(?)";
    str += " num:" + _num;
    str += " num2:" + _num2;
    str += " num3:" + _num3;
    str += " num4:" + _num4;
    str += " obj:" + _obj;
    str += " obj.type:" + _obj?.GetType().Name;
    str += " (refs:" + _refs + ", refcs:" + _refc?.refs + ")";

    return str;// + " " + GetHashCode();//for extra debug
  }
}

public class ValStack : FixedStack<Val>
{
  public ValStack(int max_capacity)
    : base(max_capacity)
  {}
}

}

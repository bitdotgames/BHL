//#define DEBUG_REFS

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace bhl
{

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

  //NOTE: it's a cached version of _obj cast to IValRefcounted for
  //      less casting in refcounting routines
  public IValRefcounted _refc;

  //NOTE: extra values below are for efficient encoding of small structs,
  //      e.g Vector3, Color, etc
  public double _num2;
  public double _num3;
  public double _num4;

  //NOTE: indicates that _obj is byte[] rented from pool and must be copied
  //      and properly released
  public int _blob_size;

  public double num
  {
    get { return _num; }
    set { SetFlt(value); }
  }

  public string str
  {
    get { return (string)_obj; }
    set { SetStr(value); }
  }

  public object obj
  {
    get { return _obj; }
  }

  public bool bval
  {
    get { return _num == 1; }
    set { SetBool(value); }
  }

  public bool is_null
  {
    get { return this == vm.Null; }
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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static public Val NewNoReset(VM vm)
  {
    Val dv;
    if(vm.vals_pool.stack.Count == 0)
    {
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
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void Reset()
  {
    type = null;
    _num = 0;
    _num2 = 0;
    _num3 = 0;
    _num4 = 0;
    _refc = null;

    if(_blob_size > 0)
    {
      ArrayPool<byte>.Shared.Return((byte[])_obj);
      _blob_size = 0;
    }

    _obj = null;
  }

  //NOTE: doesn't affect refcounting
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void ValueCopyFrom(Val o)
  {
    type = o.type;
    _num = o._num;
    _num2 = o._num2;
    _num3 = o._num3;
    _num4 = o._num4;
    _refc = o._refc;

    if(o._blob_size > 0)
    {
      CopyBlobDataFrom(o);
    }
    else if(_blob_size > 0)
    {
      ArrayPool<byte>.Shared.Return((byte[])_obj);
      _obj = o._obj;
      _blob_size = 0;
    }
    else
      _obj = o._obj;
  }

  void CopyBlobDataFrom(Val src)
  {
    var src_data = (byte[])src._obj;

    if(_blob_size > 0)
    {
      var data = (byte[])_obj;

      //let's check if our current buffer has enough capacity
      if(data.Length >= src._blob_size)
        Array.Copy(src_data, data, src._blob_size);
      else
      {
        ArrayPool<byte>.Shared.Return(data);
        var new_data = ArrayPool<byte>.Shared.Rent(src._blob_size);
        Array.Copy(src_data, new_data, src._blob_size);
        _obj = new_data;
      }
    }
    else
    {
      var new_data = ArrayPool<byte>.Shared.Rent(src._blob_size);
      Array.Copy(src_data, new_data, src._blob_size);
      _obj = new_data;
    }

    _blob_size = src._blob_size;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  bool IsBlobEqual(Val dv)
  {
    if(_blob_size != dv._blob_size)
      return false;

    ReadOnlySpan<byte> a = (byte[])_obj;
    ReadOnlySpan<byte> b = (byte[])dv._obj;

    return a.SequenceEqual(b);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Val CloneValue()
  {
    var copy = Val.New(vm);
    copy.ValueCopyFrom(this);
    copy._refc?.Retain();
    return copy;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Retain()
  {
    _refc?.Retain();

    if(_refs == -1)
      throw new Exception("Invalid state(-1)");

    ++_refs;
#if DEBUG_REFS
    Console.WriteLine("INC: " + _refs + " " + this + " " + GetHashCode() + vm.last_fiber?.GetStackTrace()/* + " " + Environment.StackTrace*/);
#endif
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Release()
  {
    _refc?.Release();

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

  static public Val NewStr(VM vm, string s)
  {
    Val dv = NewNoReset(vm);
    dv.SetStr(s);
    return dv;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetStr(string s)
  {
    Reset();
    type = Types.String;
    _obj = s;
    _refc = null;
  }

  static public Val NewNum(VM vm, long n)
  {
    Val dv = NewNoReset(vm);
    dv.SetNum(n);
    return dv;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetNum(long n)
  {
    Reset();
    type = Types.Int;
    _num = n;
  }

  //NOTE: it's caller's responsibility to ensure 'int precision'
  static public Val NewInt(VM vm, double n)
  {
    Val dv = NewNoReset(vm);
    dv.SetInt(n);
    return dv;
  }

  static public Val NewFlt(VM vm, double n)
  {
    Val dv = NewNoReset(vm);
    dv.SetFlt(n);
    return dv;
  }

  //NOTE: it's caller's responsibility to ensure 'int precision'
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetInt(double n)
  {
    Reset();
    type = Types.Int;
    _num = n;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetFlt(double n)
  {
    Reset();
    type = Types.Float;
    _num = n;
  }

  static public Val NewBool(VM vm, bool b)
  {
    Val dv = NewNoReset(vm);
    dv.SetBool(b);
    return dv;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetBool(bool b)
  {
    Reset();
    type = Types.Bool;
    _num = b ? 1 : 0;
  }

  static public Val NewObj(VM vm, object o, IType type)
  {
    Val dv = NewNoReset(vm);
    dv.SetObj(o, type);
    return dv;
  }

  static public Val NewObj(VM vm, IValRefcounted o, IType type)
  {
    Val dv = NewNoReset(vm);
    dv.SetObj(o, type);
    return dv;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObj(object o, IType type)
  {
    Reset();
    this.type = type;
    _obj = o;
    _refc = o as IValRefcounted;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObjNoRefc(object o, IType type)
  {
    Reset();
    this.type = type;
    _obj = o;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetObj(IValRefcounted o, IType type)
  {
    Reset();
    this.type = type;
    _obj = o;
    _refc = o;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetBlob<T>(ref T val, IType type) where T : unmanaged
  {
    Reset();

    int size = Extensions.SizeOf<T>();

    var data = ArrayPool<byte>.Shared.Rent(size);

    Extensions.UnsafeAs<byte, T>(ref data[0]) = val;

    this.type = type;
    _blob_size = size;
    _obj = data;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void SetBlob<T>(T val, IType type) where T : unmanaged
  {
    SetBlob(ref val, type);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T GetBlob<T>() where T : unmanaged
  {
    byte[] data = (byte[])_obj;
    return ref Extensions.UnsafeAs<byte, T>(ref data[0]);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsValueEqual(Val o)
  {
    bool res =
        _num == o._num &&
        _num2 == o._num2 &&
        _num3 == o._num3 &&
        _num4 == o._num4 &&
        ((_blob_size > 0 || o._blob_size > 0) ? IsBlobEqual(o) : (_obj != null ? _obj.Equals(o._obj) : _obj == o._obj))
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

    return str; // + " " + GetHashCode();//for extra debug
  }
}

public class ValStack : FixedStack<Val>
{
  public ValStack(int max_capacity)
    : base(max_capacity)
  {
  }
}

}
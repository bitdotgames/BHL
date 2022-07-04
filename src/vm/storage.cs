//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;

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

  public VM vm;

  //NOTE: use New() instead
  internal Val(VM vm)
  {
    this.vm = vm;
  }

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
      ++vm.vals_pool.hit;
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
  }

  public void ValueCopyFrom(Val dv)
  {
    type = dv.type;
    _num = dv._num;
    _num2 = dv._num2;
    _num3 = dv._num3;
    _num4 = dv._num4;
    _obj = dv._obj;
  }

  //NOTE: see RefOp for constants
  public void RefMod(int op)
  {
    if(_obj != null && _obj is IValRefcounted _refc)
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
      Console.WriteLine("INC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
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
      Console.WriteLine("DEC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif

      if(_refs == 0)
        Del(this);
    }
  }

  public void Retain()
  {
    RefMod(RefOp.USR_INC | RefOp.INC);
  }

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
  }

  static public Val NewNum(VM vm, int n)
  {
    Val dv = New(vm);
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(int n)
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
  }

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
      ^ (int)_obj?.GetHashCode()
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
    str += " (refs:" + _refs + ")";

    return str;// + " " + GetHashCode();//for extra debug
  }
}

public class ValList : IList<Val>, IValRefcounted
{
  //NOTE: Exposed to allow low-level optimal manipulations. 
  //      Use with caution.
  public List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  public VM vm;

  //////////////////IList//////////////////

  public int Count { get { return lst.Count; } }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }
  public bool IsSynchronized { get { throw new NotImplementedException(); } }
  public object SyncRoot { get { throw new NotImplementedException(); } }

  public void Add(Val dv)
  {
    dv.RefMod(RefOp.INC | RefOp.USR_INC);
    lst.Add(dv);
  }

  public void AddRange(IList<Val> list)
  {
    for(int i=0; i<list.Count; ++i)
      Add(list[i]);
  }

  public void RemoveAt(int idx)
  {
    var dv = lst[idx];
    dv.RefMod(RefOp.DEC | RefOp.USR_DEC);
    lst.RemoveAt(idx); 
  }

  public void Clear()
  {
    for(int i=0;i<Count;++i)
      lst[i].RefMod(RefOp.DEC | RefOp.USR_DEC);

    lst.Clear();
  }

  public Val this[int i]
  {
    get {
      return lst[i];
    }
    set {
      var prev = lst[i];
      prev.RefMod(RefOp.DEC | RefOp.USR_DEC);
      value.RefMod(RefOp.INC | RefOp.USR_INC);
      lst[i] = value;
    }
  }

  public int IndexOf(Val dv)
  {
    return lst.IndexOf(dv);
  }

  public bool Contains(Val dv)
  {
    return IndexOf(dv) >= 0;
  }

  public bool Remove(Val dv)
  {
    int idx = IndexOf(dv);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(Val[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public void Insert(int pos, Val o)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<Val> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  ///////////////////////////////////////

  public void Retain()
  {
    //Console.WriteLine("== RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    ++refs;
  }

  public void Release()
  {
    //Console.WriteLine("== RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);

    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    if(refs == 0)
      throw new Exception("Double free(0)");

    --refs;
    if(refs == 0)
      Del(this);
  }

  ///////////////////////////////////////

  public void CopyFrom(ValList lst)
  {
    Clear();
    for(int i=0;i<lst.Count;++i)
      Add(lst[i]);
  }

  ///////////////////////////////////////

  //NOTE: use New() instead
  internal ValList(VM vm)
  {
    this.vm = vm;
  }

  public static ValList New(VM vm)
  {
    ValList lst;
    if(vm.vlsts_pool.stack.Count == 0)
    {
      ++vm.vlsts_pool.miss;
      lst = new ValList(vm);
    }
    else
    {
      ++vm.vlsts_pool.hit;
      lst = vm.vlsts_pool.stack.Pop();

      if(lst.refs != -1)
        throw new Exception("Expected to be released, refs " + lst.refs);
    }
    lst.refs = 1;

    return lst;
  }

  static void Del(ValList lst)
  {
    if(lst.refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst.refs);

    lst.refs = -1;
    lst.Clear();
    lst.vm.vlsts_pool.stack.Push(lst);

    if(lst.vm.vlsts_pool.stack.Count > lst.vm.vlsts_pool.miss)
      throw new Exception("Unbalanced New/Del");
  }
}

public class ValMap : IDictionary<Val,Val>, IValRefcounted
{
  //NOTE: since we track the lifetime of the key as well as of a value
  //      we need to efficiently access the added key, for this reason
  //      we store the key alongside with the value in a KeyValuePair
  Dictionary<Val,KeyValuePair<Val, Val>> map = new Dictionary<Val,KeyValuePair<Val,Val>>(new Comparer());

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  public VM vm;

  //////////////////IDictionary//////////////////

  public int Count { get { return map.Count; } }

  public bool IsReadOnly { get { return false; } }

  public ICollection<Val> Keys { get { return map.Keys; } }

  public ICollection<Val> Values { get { throw new NotImplementedException(); } }

  public void Add(KeyValuePair<Val,Val> p)
  {
    throw new NotImplementedException();
  }

  public void Add(Val k, Val v)
  {
    throw new NotImplementedException();
  }

  public void Clear()
  {
    foreach(var kv in map)
    {
      kv.Key.RefMod(RefOp.DEC | RefOp.USR_DEC);
      kv.Value.Value.RefMod(RefOp.DEC | RefOp.USR_DEC);
    }
    map.Clear();
  }

  public Val this[Val k]
  {
    get {
      return map[k].Value;
    }
    set {
      KeyValuePair<Val,Val> prev;
      if(!map.TryGetValue(k, out prev))
        k.RefMod(RefOp.INC | RefOp.USR_INC);
      else
        prev.Value.RefMod(RefOp.DEC | RefOp.USR_DEC);
      value.RefMod(RefOp.INC | RefOp.USR_INC);
      map[k] = new KeyValuePair<Val,Val>(k, value);
    }
  }

  public bool TryGetValue(Val k, out Val v)
  {
    KeyValuePair<Val, Val> p;
    bool yes = map.TryGetValue(k, out p);
    v = p.Value;
    return yes;
  }

  public bool Contains(KeyValuePair<Val, Val> p)
  {
    throw new NotImplementedException();
  }

  public bool ContainsKey(Val k)
  {
    return map.ContainsKey(k);
  }

  public bool Remove(Val k)
  {
    KeyValuePair<Val,Val> prev;
    bool existed = map.TryGetValue(k, out prev);
    bool removed = map.Remove(k);
    if(existed)
    {
      prev.Key.RefMod(RefOp.DEC | RefOp.USR_DEC);
      prev.Value.RefMod(RefOp.DEC | RefOp.USR_DEC);
    }
    return removed;
  }

  public bool Remove(KeyValuePair<Val,Val> p)
  {
    throw new NotImplementedException();
  }

  public void CopyTo(KeyValuePair<Val,Val>[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<KeyValuePair<Val,Val>> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  ///////////////////////////////////////

  public void Retain()
  {
    //Console.WriteLine("== RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    ++refs;
  }

  public void Release()
  {
    //Console.WriteLine("== RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);

    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    if(refs == 0)
      throw new Exception("Double free(0)");

    --refs;
    if(refs == 0)
      Del(this);
  }

  ///////////////////////////////////////

  //NOTE: use New() instead
  internal ValMap(VM vm)
  {
    this.vm = vm;
  }

  public static ValMap New(VM vm)
  {
    ValMap map;
    if(vm.vmaps_pool.stack.Count == 0)
    {
      ++vm.vmaps_pool.miss;
      map = new ValMap(vm);
    }
    else
    {
      ++vm.vmaps_pool.hit;
      map = vm.vmaps_pool.stack.Pop();

      if(map.refs != -1)
        throw new Exception("Expected to be released, refs " + map.refs);
    }
    map.refs = 1;

    return map;
  }

  static void Del(ValMap map)
  {
    if(map.refs != 0)
      throw new Exception("Freeing invalid object, refs " + map.refs);

    map.refs = -1;
    map.Clear();
    map.vm.vmaps_pool.stack.Push(map);

    if(map.vm.vmaps_pool.stack.Count > map.vm.vmaps_pool.miss)
      throw new Exception("Unbalanced New/Del");
  }

  class Comparer : IEqualityComparer<Val>
  {
    public bool Equals(Val a, Val b)
    {
      if(a == null && b == null)
        return true;
      else if(a == null || b == null)
        return false;

      return a.IsValueEqual(b);
    }

    public int GetHashCode(Val v)
    {
      return v.GetValueHashCode();
    }
  }
}

} //namespace bhl

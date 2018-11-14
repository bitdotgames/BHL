//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public struct RefOp
{
  public const int INC                  = 1;
  public const int DEC                  = 2;
  public const int DEC_NO_DEL           = 4;
  public const int TRY_DEL              = 8;
  public const int USR_INC              = 16;
  public const int USR_DEC              = 32;
  public const int USR_DEC_NO_DEL       = 64;
  public const int USR_TRY_DEL          = 128;
}

public interface DynValRefcounted
{
  void Retain();
  void Release(bool can_del = true);
  bool TryDel();
}

public class DynVal
{
  public const byte NONE      = 0;
  public const byte NUMBER    = 1;
  public const byte BOOL      = 2;
  public const byte STRING    = 3;
  public const byte OBJ       = 4;
  public const byte NIL       = 5;
  public const byte ENCODED   = 6; //used for small value type objects encoded directly into DynVal

  public bool IsEmpty { get { return type == NONE; } }

  public byte type { get { return _type; } }

  public double num {
    get {
      return _num;
    }
    set {
      SetNum(value);
    }
  }

  public string str {
    get {
      return _str;
    }
    set {
      SetStr(value);
    }
  }

  public object obj {
    get {
      return _obj;
    }
    set {
      SetObj(value);
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

  //NOTE: -1 means it's in released state
  public int _refs;
  public int refs { get { return _refs; } } 

  DynValRefcounted _refc;

  //NOTE: below members are semi-public, one can use them for 
  //      fast access or non-allocating storage of structs(e.g vectors, quaternions)
  public byte _type;
  public double _num;
  public double _num2;
  public double _num3;
  public double _num4;
  public double _num5;
  public string _str;
  public object _obj;

  static Queue<DynVal> pool = new Queue<DynVal>(64);
  static int pool_miss;
  static int pool_hit;

  //NOTE: use New() instead
  internal DynVal()
  {}

  static public DynVal New()
  {
    DynVal dv;
    if(pool.Count == 0)
    {
      ++pool_miss;
      dv = new DynVal();
#if DEBUG_REFS
      Console.WriteLine("NEW: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    else
    {
      ++pool_hit;
      dv = pool.Dequeue();
#if DEBUG_REFS
      Console.WriteLine("HIT: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
      //NOTE: DynVal is Reset here instead of Del, see notes below 
      dv.Reset();
      dv._refs = 0;
    }
    return dv;
  }

  static public void Del(DynVal dv)
  {
    //NOTE: we don't Reset DynVal immediately, giving a caller
    //      a chance to access its properties
    if(dv._refs != 0)
      throw new Exception("Deleting invalid object, refs " + dv._refs);
    dv._refs = -1;

    //NOTE: we'd like to ensure there are some spare values before 
    //      the released one, this way it will be possible to call 
    //      safely New() right after Del() since it won't return
    //      just deleted object
    if(pool.Count == 0)
    {
      ++pool_miss;
      var tmp = new DynVal(); 
      pool.Enqueue(tmp);
      //Console.WriteLine("NEW3: " + tmp.GetHashCode()/* + " " + Environment.StackTrace*/);
    }
    pool.Enqueue(dv);
    //Console.WriteLine("DEL: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del " + pool.Count + " " + pool_miss);
  }

  //For proper assignments, like: dst = src (taking into account ref.counting)
  static public DynVal Assign(DynVal dst, DynVal src)
  {
    if(dst != null)
      dst.RefMod(RefOp.DEC | RefOp.USR_DEC);
    if(src != null) 
      src.RefMod(RefOp.INC | RefOp.USR_INC);
    return src;
  }

  //NOTE: refcount is not reset
  void Reset()
  {
    _type = NONE;
    _num = 0;
    _num2 = 0;
    _num3 = 0;
    _num4 = 0;
    _num5 = 0;
    _str = "";
    _obj = null;
    _refc = null;
  }

  public void ValueCopyFrom(DynVal dv)
  {
    _type = dv._type;
    _num = dv._num;
    _num2 = dv._num2;
    _num3 = dv._num3;
    _num4 = dv._num4;
    _num5 = dv._num5;
    _str = dv._str;
    _obj = dv._obj;
    _refc = dv._refc;
  }

  public DynVal ValueClone()
  {
    DynVal dv = New();
    dv.ValueCopyFrom(this);
    return dv;
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
        _refc.Release(true);
      }
      else if((op & RefOp.USR_DEC_NO_DEL) != 0)
      {
        _refc.Release(false);
      }
      else if((op & RefOp.USR_TRY_DEL) != 0)
      {
        _refc.TryDel();
      }
    }

    if((op & RefOp.INC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");

      ++_refs;
#if DEBUG_REFS
      Console.WriteLine("INC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    } 
    else if((op & RefOp.DEC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");
      else if(_refs == 0)
        throw new Exception("Double free");

      --_refs;
#if DEBUG_REFS
      Console.WriteLine("DEC: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif

      if(_refs == 0)
        Del(this);
    }
    else if((op & RefOp.DEC_NO_DEL) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");
      else if(_refs == 0)
        throw new Exception("Double free");

      --_refs;
#if DEBUG_REFS
      Console.WriteLine("DCN: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
#endif
    }
    else if((op & RefOp.TRY_DEL) != 0)
    {
#if DEBUG_REFS
      Console.WriteLine("TDL: " + _refs + " " + this + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
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

  public void TryDel()
  {
    RefMod(RefOp.TRY_DEL);
  }

  public void RetainReference()
  {
    RefMod(RefOp.INC);
  }

  public void ReleaseReference()
  {
    RefMod(RefOp.DEC);
  }

  static public DynVal NewStr(string s)
  {
    DynVal dv = New();
    dv.SetStr(s);
    return dv;
  }

  public void SetStr(string s)
  {
    Reset();
    _type = STRING;
    _str = s;
  }

  static public DynVal NewNum(int n)
  {
    DynVal dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(int n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public DynVal NewNum(double n)
  {
    DynVal dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(double n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public DynVal NewBool(bool b)
  {
    DynVal dv = New();
    dv.SetBool(b);
    return dv;
  }

  public void SetBool(bool b)
  {
    Reset();
    _type = BOOL;
    _num = b ? 1.0f : 0.0f;
  }

  static public DynVal NewObj(object o)
  {
    DynVal dv = New();
    dv.SetObj(o);
    return dv;
  }

  public void SetObj(object o)
  {
    Reset();
    _type = o == null ? NIL : OBJ;
    _obj = o;
    _refc = _obj as DynValRefcounted;
  }

  static public DynVal NewNil()
  {
    DynVal dv = New();
    dv.SetNil();
    return dv;
  }

  public void SetNil()
  {
    Reset();
    _type = NIL;
  }

  public bool IsEqual(DynVal o)
  {
    bool res =
      _type == o.type &&
      _num == o._num &&
      _num2 == o._num2 &&
      _num3 == o._num3 &&
      _num4 == o._num4 &&
      _num5 == o._num5 &&
      _str == o._str &&
      _obj == o._obj
      ;

    //TODO: not sure if we need to support such an ill behavior
    ////NOTE: null special case for overriden Equals
    //if(!res)
    //{
    //  if(_type == NIL && o._obj != null)
    //    res = o._obj.Equals(null); 
    //  else if(o._type == NIL && _obj != null)
    //    res = _obj.Equals(null); 
    //}

    return res;
  }

  public override string ToString() 
  {
    if(type == NUMBER)
      return _num + ":<NUMBER>";
    else if(type == BOOL)
      return bval + ":<BOOL>";
    else if(type == STRING)
      return _str + ":<STRING>";
    else if(type == OBJ)
      return _obj.GetType().Name + ":<OBJ>";
    else if(type == NIL)
      return "<NIL>";
    else if(type == ENCODED)
      return "<ENCODED>";
    else
      return "DYNVAL: type:"+type;
  }

  public object ToAny() 
  {
    if(type == NUMBER)
      return (object)_num;
    else if(type == BOOL)
      return (object)bval;
    else if(type == STRING)
      return(object)_str;
    else if(type == OBJ)
      return _obj;
    else if(type == NIL)
      return null;
    else if(type == ENCODED)
      return this;
    else
      throw new Exception("ToAny(): please support type: " + type);
  }

  static public void PoolAlloc(int num)
  {
    for(int i=0;i<num;++i)
    {
      ++pool_miss;
      var tmp = new DynVal(); 
      pool.Enqueue(tmp);
    }
  }

  static public void PoolClear()
  {
    pool_hit = 0;
    pool_miss = 0;
    pool.Clear();
  }

  static public string PoolDump()
  {
    string res = "=== POOL ===\n";
    res += "total:" + PoolCount + " free:" + PoolCountFree + "\n";
    foreach(var v in pool)
    {
      res += v + " " + v.GetHashCode() + "\n"; 
    }
    return res;
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }
}

public class DynValList : IList<DynVal>, DynValRefcounted
{
  //NOTE: exposed to allow manipulations like Reverse(). Use with caution.
  public readonly List<DynVal> lst = new List<DynVal>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  //////////////////IList//////////////////

  public int Count { get { return lst.Count; } }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }
  public bool IsSynchronized { get { throw new NotImplementedException(); } }
  public object SyncRoot { get { throw new NotImplementedException(); } }

  public static implicit operator DynVal(DynValList lst)
  {
    return DynVal.NewObj(lst);
  }

  public void Add(DynVal dv)
  {
    dv.RefMod(RefOp.INC | RefOp.USR_INC);
    lst.Add(dv);
  }

  public void AddRange(IList<DynVal> list)
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
      lst[i].RefMod(RefOp.USR_DEC | RefOp.DEC);

    lst.Clear();
  }

  public DynVal this[int i]
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

  public int IndexOf(DynVal dv)
  {
    return lst.IndexOf(dv);
  }

  public bool Contains(DynVal dv)
  {
    return IndexOf(dv) >= 0;
  }

  public bool Remove(DynVal dv)
  {
    int idx = IndexOf(dv);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(DynVal[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public void Insert(int pos, DynVal o)
  {
    throw new NotImplementedException();
  }

  public IEnumerator<DynVal> GetEnumerator()
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
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;
    //Console.WriteLine("RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release(bool can_del = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;
    //Console.WriteLine("RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
    if(can_del)
      TryDel();
  }

  public bool TryDel()
  {
    if(refs != 0)
      return false;

    //Console.WriteLine("DEL " + GetHashCode());
    
    Del(this);

    return true;
  }

  public void CopyFrom(DynValList lst)
  {
    Clear();
    for(int i=0;i<lst.Count;++i)
      Add(lst[i]);
  }

  ///////////////////////////////////////

  static public Stack<DynValList> pool = new Stack<DynValList>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal DynValList()
  {}

  public static DynValList New()
  {
    DynValList lst;
    if(pool.Count == 0)
    {
      ++pool_miss;
      lst = new DynValList();
    }
    else
    {
      ++pool_hit;
      lst = pool.Pop();

      if(lst.refs != -1)
        throw new Exception("Expected to be released, refs " + lst.refs);
      lst.refs = 0;
    }

    return lst;
  }

  static public void Del(DynValList lst)
  {
    if(lst.refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst.refs);

    lst.refs = -1;
    lst.Clear();
    pool.Push(lst);

    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del");
  }

  static public void PoolClear()
  {
    pool_miss = 0;
    pool_hit = 0;
    pool.Clear();
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }
}

public class DynValDict : DynValRefcounted
{
  public Dictionary<ulong, DynVal> vars = new Dictionary<ulong, DynVal>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  static public Stack<DynValDict> pool = new Stack<DynValDict>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal DynValDict()
  {}

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;

    //Console.WriteLine("FREF INC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release(bool can_del = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;

    //Console.WriteLine("FREF DEC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);

    if(can_del)
      TryDel();
  }

  public bool TryDel()
  {
    if(refs != 0)
      return false;
    
    Del(this);
    return true;
  }

  public static DynValDict New()
  {
    DynValDict tb;
    if(pool.Count == 0)
    {
      ++pool_miss;
      tb = new DynValDict();
    }
    else
    {
      ++pool_hit;
      tb = pool.Pop();

      if(tb.refs != -1)
        throw new Exception("Expected to be released, refs " + tb.refs);
      tb.refs = 0;
    }

    return tb;
  }

  static public void PoolClear()
  {
    pool_miss = 0;
    pool_hit = 0;
    pool.Clear();
  }

  static public int PoolHits
  {
    get { return pool_hit; } 
  }

  static public int PoolMisses
  {
    get { return pool_miss; } 
  }

  static public int PoolCount
  {
    get { return pool_miss; }
  }

  static public int PoolCountFree
  {
    get { return pool.Count; }
  }

  static public void Del(DynValDict tb)
  {
    if(tb.refs != 0)
      throw new Exception("Freeing invalid object, refs " + tb.refs);

    tb.refs = -1;
    tb.Clear();
    pool.Push(tb);

    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del");
  }

  public void Clear()
  {
    var enm = vars.GetEnumerator();
    try
    {
      while(enm.MoveNext())
      {
        var val = enm.Current.Value;
        val.RefMod(RefOp.USR_DEC | RefOp.DEC);
      }
    }
    finally
    {
      enm.Dispose();
    }

    vars.Clear();
  }

  public void Set(HashedName key, DynVal val)
  {
    ulong k = key.n; 
    DynVal prev;
    if(vars.TryGetValue(k, out prev))
    {
      for(int i=0;i<prev._refs;++i)
      {
        val.RefMod(RefOp.USR_INC);
        prev.RefMod(RefOp.USR_DEC);
      }
      prev.ValueCopyFrom(val);
      //Console.WriteLine("VAL SET2 " + prev.GetHashCode());
    }
    else
    {
      //Console.WriteLine("VAL SET1 " + val.GetHashCode());
      vars[k] = val;
      val.RefMod(RefOp.USR_INC | RefOp.INC);
    }
  }

  public bool TryGet(HashedName key, out DynVal val)
  {
    return vars.TryGetValue(key.n, out val);
  }

  public DynVal Get(HashedName key)
  {
    return vars[key.n];
  }

  public void CopyFrom(DynValDict o)
  {
    var enm = o.vars.GetEnumerator();
    try
    {
      while(enm.MoveNext())
      {
        var key = enm.Current.Key;
        var val = enm.Current.Value;
        Set(key, val);
      }
    }
    finally
    {
      enm.Dispose();
    }
  }
}

} //namespace bhl

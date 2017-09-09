using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using game;

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
  private DynVal()
  {}

  static public DynVal New()
  {
    DynVal dv;
    if(pool.Count == 0)
    {
      ++pool_miss;
      dv = new DynVal();
      //Console.WriteLine("NEW: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
    }
    else
    {
      ++pool_hit;
      dv = pool.Dequeue();
      //Console.WriteLine("NEW2: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
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
      //Console.WriteLine("INC: " + _refs + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
    } 
    else if((op & RefOp.DEC) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state");
      else if(_refs == 0)
        throw new Exception("Double free");

      --_refs;
      //Console.WriteLine("DEC: " + _refs + " " + GetHashCode()/* + " " + Environment.StackTrace*/);

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
      //Console.WriteLine("DEC: " + _refs + " " + GetHashCode()/* + " " + Environment.StackTrace*/);
    }
    else if((op & RefOp.TRY_DEL) != 0)
    {
      if(_refs == 0)
        Del(this);
    }
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
    _refc = o as DynValRefcounted;
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
    return this == o || (
      _type == o.type &&
      _num == o._num &&
      _num2 == o._num2 &&
      _num3 == o._num3 &&
      _num4 == o._num4 &&
      _num5 == o._num5 &&
      _str == o._str &&
      _obj == o._obj
      );
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

public interface DynValRefcounted
{
  void Retain();
  void Release(bool can_del = true);
  bool TryDel();
}

public class MemoryScope
{
  public Dictionary<ulong, DynVal> vars = new Dictionary<ulong, DynVal>();

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

  public void CopyFrom(MemoryScope o)
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

public class ClassStorage : MemoryScope, DynValRefcounted
{
  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  static public Stack<ClassStorage> pool = new Stack<ClassStorage>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  private ClassStorage()
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

  public static ClassStorage New()
  {
    ClassStorage cs;
    if(pool.Count == 0)
    {
      ++pool_miss;
      cs = new ClassStorage();
    }
    else
    {
      ++pool_hit;
      cs = pool.Pop();

      if(cs.refs != -1)
        throw new Exception("Expected to be released, refs " + cs.refs);
      cs.refs = 0;
    }

    return cs;
  }

  static public void Del(ClassStorage cs)
  {
    if(cs.refs != 0)
      throw new Exception("Freeing invalid object, refs " + cs.refs);

    cs.refs = -1;
    cs.Clear();
    pool.Push(cs);

    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del");
  }
}

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim node);
  public abstract void DoVisit(AST_Import node);
  public abstract void DoVisit(AST_Module node);
  public abstract void DoVisit(AST_VarDecl node);
  public abstract void DoVisit(AST_FuncDecl node);
  public abstract void DoVisit(AST_LambdaDecl node);
  public abstract void DoVisit(AST_ClassDecl node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_PopValue node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(AST_Base node)
  {
    if(node == null)
      throw new Exception("NULL node");

    if(node is AST_Interim)
      DoVisit(node as AST_Interim);
    else if(node is AST_Block)
      DoVisit(node as AST_Block);
    else if(node is AST_Literal)
      DoVisit(node as AST_Literal);
    else if(node is AST_Call)
      DoVisit(node as AST_Call);
    else if(node is AST_VarDecl)
      DoVisit(node as AST_VarDecl);
    else if(node is AST_LambdaDecl)
      DoVisit(node as AST_LambdaDecl);
    //NOTE: base class must be handled after AST_LambdaDecl
    else if(node is AST_FuncDecl)
      DoVisit(node as AST_FuncDecl);
    else if(node is AST_ClassDecl)
      DoVisit(node as AST_ClassDecl);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
    else if(node is AST_PopValue)
      DoVisit(node as AST_PopValue);
    else if(node is AST_BinaryOpExp)
      DoVisit(node as AST_BinaryOpExp);
    else if(node is AST_UnaryOpExp)
      DoVisit(node as AST_UnaryOpExp);
    else if(node is AST_New)
      DoVisit(node as AST_New);
    else if(node is AST_JsonObj)
      DoVisit(node as AST_JsonObj);
    else if(node is AST_JsonArr)
      DoVisit(node as AST_JsonArr);
    else if(node is AST_JsonPair)
      DoVisit(node as AST_JsonPair);
    else if(node is AST_Import)
      DoVisit(node as AST_Import);
    else if(node is AST_Module)
      DoVisit(node as AST_Module);
    else 
      throw new Exception("Not known type: " + node.GetType().Name);
  }

  public void VisitChildren(AST node)
  {
    if(node == null)
      return;
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
  }
}

public class FuncCtx : DynValRefcounted
{
  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  public FuncSymbol fs;
  //NOTE: this memory scope used for 'use' variables, it's then
  //      copied to concrete memory scope of the function
  public MemoryScope mem = new MemoryScope();
  public FuncNode fnode;

  private FuncCtx(FuncSymbol fs)
  {
    this.fs = fs;
  }

  public FuncNode EnsureNode()
  {
    if(fnode != null)
      return fnode;

    if(fs is FuncBindSymbol)
      fnode = new FuncNodeBinding(fs as FuncBindSymbol, this);
    else if(fs is LambdaSymbol)
      fnode = new FuncNodeLambda(this);
    else
      fnode = new FuncNodeAST((fs as FuncSymbolAST).decl, this);

    return fnode;
  }

  public FuncCtx Clone()
  {
    if(refs == -1)
      throw new Exception("Invalid state");

    var dup = FuncCtx.New(fs);

    //NOTE: need to properly set use params
    if(fs is LambdaSymbol)
    {
      var ldecl = (fs as LambdaSymbol).decl as AST_LambdaDecl;
      for(int i=0;i<ldecl.useparams.Count;++i)
      {
        var up = ldecl.useparams[i];
        var val = mem.Get(up.Name());
        dup.mem.Set(up.Name(), up.IsRef() ? val : val.ValueClone());
      }
    }

    return dup;
  }

  public FuncCtx AutoClone()
  {
    if(fnode != null)
      return Clone(); 
    return this;
  }

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

  //////////////////////////////////////////////

  public struct PoolItem
  {
    public bool used;
    public FuncCtx fct;
  }

  static List<PoolItem> pool = new List<PoolItem>();
  static int pool_hit;
  static int pool_miss;

  public static FuncCtx New(FuncSymbol fs)
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(!item.used && item.fct.fs == fs)
      {
        ++pool_hit;

        //Util.Debug("FTX REQUEST " + item.fct.GetHashCode());

        item.used = true;
        pool[i] = item;
        if(item.fct.refs != -1)
          throw new Exception("Expected to be released, refs " + item.fct.refs);
        item.fct.refs = 0;
        return item.fct;
      }
    }

    {
      ++pool_miss;

      var fct = new FuncCtx(fs);
      //Util.Debug("FTX REQUEST2 " + fct.GetHashCode());
      var item = new PoolItem();
      item.fct = fct;
      item.used = true;
      pool.Add(item);
      return fct;
    }
  }

  static public void Del(FuncCtx fct)
  {
    if(fct.refs != 0)
      throw new Exception("Freeing invalid object, refs " + fct.refs);

    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(item.fct == fct)
      {
        //Util.Debug("FTX RELEASE " + fct.GetHashCode());
        item.fct.refs = -1;
        item.fct.mem.Clear();
        item.fct.fnode = null;
        item.used = false;
        pool[i] = item;
        break;
      }
    }

  }

  static public void PoolClear()
  {
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
    get { return pool.Count; }
  }

  static public int PoolCountFree
  {
    get {
      int free = 0;
      for(int i=0;i<pool.Count;++i)
      {
        if(!pool[i].used)
          ++free;
      }
      return free;
    }
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
  private DynValList()
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

public class Interpreter : AST_Visitor
{
  static Interpreter _instance;
  public static Interpreter instance 
  {
    get {
      if(_instance == null)
        _instance = new Interpreter();
      return _instance;
    }
  }

  public class ReturnException : Exception {}
  public class BreakException : Exception {}
  //not supported
  //public class ContinueException : Exception {}

  public delegate void ClassCreator(ref DynVal res);
  public delegate void FieldGetter(DynVal v, ref DynVal res);
  public delegate void FieldSetter(ref DynVal v, DynVal nv);
  public delegate BehaviorTreeNode FuncNodeCreator(); 
  public delegate void ConfigGetter(BehaviorTreeNode n, ref DynVal v, bool reset);

  FastStack<BehaviorTreeInternalNode> node_stack = new FastStack<BehaviorTreeInternalNode>(128);
  BehaviorTreeInternalNode curr_node;

  public DynVal last_member_ctx;

  FastStack<MemoryScope> mstack = new FastStack<MemoryScope>(128);
  MemoryScope curr_mem;

  public struct JsonCtx
  {
    public const int OBJ = 1;
    public const int ARR = 2;

    public int type;
    public uint scope_type;
    public uint name_or_idx;

    public JsonCtx(int type, uint scope_type, uint name_or_idx)
    {
      this.type = type;
      this.scope_type = scope_type;
      this.name_or_idx = name_or_idx;
    }
  }
  FastStack<JsonCtx> jcts = new FastStack<JsonCtx>(128);

  public IModuleLoader module_loader;
  //NOTE: key is a module id, value is a file path
  public Dictionary<ulong,string> loaded_modules = new Dictionary<ulong,string>();

  public BaseScope symbols;

  public FastStack<DynVal> stack = new FastStack<DynVal>(256);
  //NOTE: func marks are used in order to clean non-consumed values 
  //      from the stack. This may happen due to runtime failures.
  FastStack<AST_Call> stack_marks = new FastStack<AST_Call>(256);

  public FastStack<AST_Call> call_stack = new FastStack<AST_Call>(128);

  public void Init(BaseScope symbols, IModuleLoader module_loader)
  {
    node_stack.Clear();
    curr_node = null;
    last_member_ctx = null;
    mstack.Clear();
    curr_mem = null;
    loaded_modules.Clear();
    stack.Clear();
    call_stack.Clear();

    this.symbols = symbols;
    this.module_loader = module_loader;
  }

  public struct Result
  {
    public BHS status;
    public DynVal[] vals;

    public DynVal val 
    {
      get {
        return vals != null ? vals[0] : null;
      }
    }
  }

  public Result ExecNode(BehaviorTreeNode node, int ret_vals = 1)
  {
    Result res = new Result();

    res.status = BHS.NONE;
    while(true)
    {
      res.status = node.run();
      if(res.status != BHS.RUNNING)
        break;
    }
    if(ret_vals > 0)
    {
      res.vals = new DynVal[ret_vals];
      for(int i=0;i<ret_vals;++i)
        res.vals[i] = PopValue();
    }
    else
      res.vals = null;
    return res;
  }

  public void Interpret(AST_Module ast)
  {
    Visit(ast);
  }

  public void LoadModule(HashedName mod_id)
  {
    if(mod_id.n == 0)
      return;

    if(loaded_modules.ContainsKey(mod_id.n))
      return;

    if(module_loader == null)
      throw new Exception("Module loader is not set");

    var mod_ast = module_loader.LoadModule(mod_id);
    if(mod_ast == null)
      throw new Exception("Could not load module: " + mod_id);

    loaded_modules.Add(mod_id.n, mod_ast.name);

    Interpret(mod_ast);
  }

  public void LoadModule(string module)
  {
    LoadModule(Util.GetModuleId(module));
  }

  public FuncNode GetFuncNode(AST_Call ast)
  {
    if(ast.type == EnumCall.FUNC)
      return GetFuncNode(ast.Name());
    else if(ast.type == EnumCall.MFUNC)
      return GetMFuncNode(ast.scope_ntype, ast.Name());
    else
      throw new Exception("Bad func call type");
  }

  public FuncNode GetFuncNode(HashedName name)
  {
    var s = symbols.resolve(name);

    if(s is FuncBindSymbol)
      return new FuncNodeBinding(s as FuncBindSymbol, null);
    else if(s is FuncSymbolAST)
      return new FuncNodeAST((s as FuncSymbolAST).decl, null);
    else
      throw new Exception("Not a func symbol: " + name);
  }

  public FuncNode GetFuncNode(string module_name, string func_name)
  {
    LoadModule(module_name);
    return GetFuncNode(Util.GetFuncId(module_name, func_name));
  }

  public FuncNode GetMFuncNode(HashedName class_type, HashedName name)
  {
    var cl = symbols.resolve(class_type) as ClassSymbol;
    if(cl == null)
      throw new Exception("Class binding not found: " + class_type); 

    var cl_member = cl.ResolveMember(name);
    if(cl_member == null)
      throw new Exception("Member not found: " + name);

    var func_symb = cl_member as FuncBindSymbol;
    if(func_symb != null)
      return new FuncNodeBinding(func_symb, null);

    throw new Exception("Not a func symbol: " + name);
  }

  //NOTE: usually used in symbols
  public int GetFuncArgsNum()
  {
    return call_stack.Peek().cargs_num;
  }

  public void JumpReturn()
  {
    throw new ReturnException();
  }

  public void JumpBreak()
  {
    throw new BreakException();
  }

  //TODO: implement some day
  //public void JumpContinue()
  //{
  //  throw new ContinueException();
  //}

  public void PushScope(MemoryScope mem)
  {
    //Console.WriteLine("PUSH MEM");
    mstack.Push(mem);
    curr_mem = mem;
  }

  public void PopScope()
  {
    //Console.WriteLine("POP MEM");
    mstack.Pop();
    curr_mem = mstack.Count > 0 ? mstack.Peek() : null;
  }

  public void SetScopeValue(HashedName name, DynVal val)
  {
    //Console.WriteLine("MEM SET " + name + " " + val);
    curr_mem.Set(name, val);
  }

  public DynVal GetScopeValue(HashedName name)
  {
    DynVal val;
    bool ok = curr_mem.TryGet(name, out val);
    if(!ok)
    {
      //Console.WriteLine("MEM GET ERR " + name);
      throw new Exception("No such variable " + name + " in scope");
    }
    //Console.WriteLine("MEM GET " + name);
    return val;
  }

  public void PushNode(BehaviorTreeInternalNode node, bool attach_as_child = true)
  {
    node_stack.Push(node);
    if(curr_node != null && attach_as_child)
      curr_node.addChild(node);
    curr_node = node;
  }

  public BehaviorTreeInternalNode PopNode()
  {
    var node = node_stack.Pop();
    curr_node = (node_stack.Count > 0 ? node_stack.Peek() : null); 
    return node;
  }

  public void PushValue(DynVal v)
  {
    v.RefMod(RefOp.INC | RefOp.USR_INC);
    stack.Push(v);
    //NOTE: marking pushed value with current func call if it's present
    stack_marks.Push(call_stack.Count > 0 ? call_stack.Peek() : null);
  }

  public DynVal PopValue()
  {
    var v = stack.PopFast();
    v.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);
    stack_marks.DecFast();
    return v;
  }

  public DynVal PopRef()
  {
    var v = stack.PopFast();
    v.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC_NO_DEL);
    stack_marks.DecFast();
    return v;
  }

  public void CleanFuncStackValues(AST_Call call)
  {
    for(int i=stack_marks.Count;i-- > 0;)
    {
      var mark = stack_marks[i];
      if(mark == call)
      {
        var dv = stack[i];
        dv.RefMod(RefOp.USR_DEC_NO_DEL | RefOp.DEC);

        stack.RemoveAtFast(i);
        stack_marks.RemoveAtFast(i);
      }
    }
  }

  public bool PeekValue(ref DynVal res)
  {
    if(stack.Count == 0)
      return false;

    res = stack.Peek();
    return true;
  }

  public DynVal PeekValue()
  {
    return stack.Peek();
  }

  ///////////////////////////////////////////////////////////

  public override void DoVisit(AST_Interim node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_Module node)
  {
    VisitChildren(node);
  }

  public override void DoVisit(AST_Import node)
  {
    for(int i=0;i<node.modules.Count;++i)
      LoadModule(node.modules[i]);
  }

  void CheckFuncIsUnique(HashedName name)
  {
    var s = symbols.resolve(name) as FuncSymbol;
    if(s != null)
      throw new Exception("Function is already defined: " + name);
  }

  void CheckClassIsUnique(HashedName name)
  {
    var s = symbols.resolve(name) as ClassSymbol;
    if(s != null)
      throw new Exception("Class is already defined: " + name);
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    var name = node.Name(); 
    CheckFuncIsUnique(name);

    var fn = new FuncSymbolAST(symbols, node);
    symbols.define(fn);
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    //if there's such a lambda symbol already we re-use it
    var name = node.Name(); 
    var lmb = symbols.resolve(name) as LambdaSymbol;
    if(lmb == null)
    {
      CheckFuncIsUnique(name);
      lmb = new LambdaSymbol(symbols, node);
      symbols.define(lmb);
    }

    curr_node.addChild(new PushFuncCtxNode(lmb));
  }

  public override void DoVisit(AST_ClassDecl node)
  {
    var name = node.Name();
    CheckClassIsUnique(name);

    var cl = new ClassSymbolAST(name, node);
    symbols.define(cl);

    for(int i=0;i<node.children.Count;++i)
    {
      var child = node.children[i];
      var vd = child as AST_VarDecl;
      if(vd != null)
      {
        cl.define(new FieldSymbolAST(vd.name));
      }
    }
  }

  public override void DoVisit(AST_Block node)
  {
    if(node.type == EnumBlock.FUNC)
    {
      if(!(curr_node is FuncNode))
        throw new Exception("Current node is not a func");
      VisitChildren(node);
    }
    else if(node.type == EnumBlock.SEQ)
    {
      PushNode(new SequentialNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.GROUP)
    {
      PushNode(new GroupNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.SEQ_)
    {
      PushNode(new SequentialNode_());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.PARAL)
    {
      PushNode(new ParallelNode(BhvPolicy.SUCCEED_ON_ONE));
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.PARAL_ALL)
    {
      PushNode(new ParallelNode(BhvPolicy.SUCCEED_ON_ALL));
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.UNTIL_FAILURE)
    {
      PushNode(new MonitorFailureNode(BHS.FAILURE));
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.UNTIL_FAILURE_)
    {
      PushNode(new MonitorFailureNode(BHS.SUCCESS));
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.UNTIL_SUCCESS)
    {
      PushNode(new MonitorSuccessNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.NOT)
    {
      PushNode(new InvertNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.PRIO)
    {
      PushNode(new PriorityNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.FOREVER)
    {
      PushNode(new ForeverNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.DEFER)
    {
      PushNode(new DeferNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.IF)
    {
      PushNode(new IfNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.WHILE)
    {
      PushNode(new LoopNode());
      VisitChildren(node);
      PopNode();
    }
    else if(node.type == EnumBlock.EVAL)
    {
      PushNode(new EvalNode());
      VisitChildren(node);
      PopNode();
    }
    else
      throw new Exception("Unknown block type: " + node.type);
  }

  public override void DoVisit(AST_TypeCast node)
  {
    VisitChildren(node);
    curr_node.addChild(new TypeCastNode(node));
  }

  public override void DoVisit(AST_New node)
  {
    curr_node.addChild(new ConstructNode(node.Name()));
  }

  BehaviorTreeNode CreateConfNode(uint ntype)
  {
    var bnd = symbols.resolve(ntype) as ConfNodeSymbol;
    if(bnd == null)
      throw new Exception("Could not find class binding: " + ntype);
    return bnd.func_creator();
  }

  public override void DoVisit(AST_Call node)
  {           
    if(node.type == EnumCall.VAR)
    {
      curr_node.addChild(new VarAccessNode(node.Name()));
    }
    else if(node.type == EnumCall.VARW)
    {
      curr_node.addChild(new VarAccessNode(node.Name(), VarAccessNode.WRITE));
    }
    else if(node.type == EnumCall.MVAR)
    {
      curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name()));
    }
    else if(node.type == EnumCall.MVARW)
    {
      curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name(), MVarAccessNode.WRITE));
    }
    else if(node.type == EnumCall.FUNC || node.type == EnumCall.MFUNC)
    {
      AddFuncCallNode(node);
    }
    else if(node.type == EnumCall.FUNC_PTR || node.type == EnumCall.FUNC_PTR_POP)
    {
      curr_node.addChild(new CallFuncPtr(node));
    }
    else if(node.type == EnumCall.FUNC2VAR)
    {
      var s = symbols.resolve(node.nname()) as FuncSymbol;
      if(s == null)
        throw new Exception("Could not find func:" + node.Name());
      curr_node.addChild(new PushFuncCtxNode(s));
    }
    else if(node.type == EnumCall.ARR_IDX)
    {
      var bnd = symbols.resolve(node.scope_ntype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + node.scope_ntype);

      curr_node.addChild(bnd.Create_At());
    }
    else if(node.type == EnumCall.ARR_IDXW)
    {
      var bnd = symbols.resolve(node.scope_ntype) as ArrayTypeSymbol;
      if(bnd == null)
        throw new Exception("Could not find class binding: " + node.scope_ntype);

      curr_node.addChild(bnd.Create_SetAt());
    }
    else 
      throw new Exception("Unsupported call type: " + node.type);
  }

  void AddFuncCallNode(AST_Call ast)
  {
    var func_symb = symbols.resolve(ast.nname()) as FuncSymbol;

    var fbind_symb = func_symb as FuncBindSymbol;
    var conf_symb = func_symb as ConfNodeSymbol;

    //special case for config node
    if(conf_symb != null)
    {
      var conf_node = conf_symb.func_creator();
      //1. if there are sub-nodes tweaking config, add them
      if(ast.children.Count > 0)
      {
        bool can_be_precalculated = CheckIfConfigTweaksAreConstant(ast);

        var group = new GroupNode();

        var rcn = new ResetConfigNode(conf_symb, conf_node, true/*push config*/); 
        group.addChild(rcn);

        PushNode(group, false);
        VisitChildren(ast.children[0] as AST);

        var last_child = group.children[group.children.Count-1];
        //1.1. changing write mode or popping config
        if(last_child is MVarAccessNode)
          (last_child as MVarAccessNode).mode = MVarAccessNode.WRITE_INV_ARGS;
        else
          group.addChild(new PopValueNode());

        //1.2 processing extra args
        for(int i=1;i<ast.cargs_num;++i)
          Visit(ast.children[i]);
        PopNode();

        if(can_be_precalculated)
        {
          group.run();
          curr_node.addChild(conf_node);
        }
        else
        {
          group.addChild(conf_node);
          curr_node.addChild(group);
        }
      }
      //2. in a simpler case just reset the config
      else
      {
        var dv = DynVal.New();
        conf_symb.conf_getter(conf_node, ref dv, true/*reset*/);
        DynVal.Del(dv);
        curr_node.addChild(conf_node);
      }
    }
    else if(fbind_symb != null && ast.cargs_num == 0 && fbind_symb.def_args_num == 0)
    {
      var node = fbind_symb.func_creator();
      curr_node.addChild(node);
    }
    else
      curr_node.addChild(new FuncCallNode(ast));  
  }

  bool IsCallToSelf(AST_Call ast, uint ntype)
  {
    //Console.WriteLine("CALL TYPE: " + ast.type + " " + ast.Name());

    if(ast.type == EnumCall.MVAR && ast.scope_ntype == ntype)
      return true;

    if(ast.type == EnumCall.MFUNC && ast.scope_ntype == ntype)
      return true;

    return false;
  }

  bool CheckIfConfigTweaksAreConstant(AST_Base ast, uint json_ctx_type = 0)
  {
    if(ast is AST_JsonObj)
      json_ctx_type = (ast as AST_JsonObj).ntype;
    else if(ast is AST_JsonArr)
      json_ctx_type = (ast as AST_JsonArr).ntype;

    var children = ast.GetChildren();

    for(int i=0;children != null && i<children.Count;++i)
    {
      var c = children[i];

      //Console.WriteLine(c.GetType().Name + " " + json_ctx_type);

      if(! (c is AST_JsonObj  ||
            c is AST_JsonPair ||
            c is AST_JsonArr ||
            c is AST_Literal  ||
            c is AST_Interim  ||
            (c is AST_Call &&
              IsCallToSelf((AST_Call)c, json_ctx_type)
            ) 
          )
        )
        return false;

      if(!CheckIfConfigTweaksAreConstant(c, json_ctx_type))
        return false;
    }
    return true;
  }

  public override void DoVisit(AST_Return node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new ReturnNode());
  }

  public override void DoVisit(AST_Break node)
  {
    curr_node.addChild(new BreakNode());
  }

  public override void DoVisit(AST_PopValue node)
  {
    curr_node.addChild(new PopValueNode());
  }

  public override void DoVisit(AST_Literal node)
  {
    curr_node.addChild(new LiteralNode(node));
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    //NOTE: checking if it's a short circuit expression
    if((int)node.type < 3)
    {
      PushNode(new LogicOpNode(node));
        PushNode(new GroupNode());
        Visit(node.children[0]);
        PopNode();
        PushNode(new GroupNode());
        Visit(node.children[1]);
        PopNode();
      PopNode();
    }
    else
    {
      //NOTE: expression comes first
      VisitChildren(node);
      curr_node.addChild(new BinaryOpNode(node));
    }
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new UnaryOpNode(node));
  }

  public override void DoVisit(AST_VarDecl node)
  {
    //NOTE: expression comes first
    VisitChildren(node);
    curr_node.addChild(new VarAccessNode(node.Name(), VarAccessNode.DECL));
  }

  public override void DoVisit(bhl.AST_JsonObj node)
  {
    if(jcts.Count > 0 && jcts.Peek().type == JsonCtx.OBJ)
    {
      var jc = jcts.Peek();
      curr_node.addChild(new MVarAccessNode(jc.scope_type, jc.name_or_idx, MVarAccessNode.READ_PUSH_CTX));
    }
    else
      curr_node.addChild(new ConstructNode(new HashedName(node.ntype)));

    VisitChildren(node);
  }

  public override void DoVisit(bhl.AST_JsonArr node)
  {
    var bnd = symbols.resolve(node.ntype) as ArrayTypeSymbol;
    if(bnd == null)
      throw new Exception("Could not find class binding: " + node.ntype);

    if(jcts.Count > 0 && jcts.Peek().type == JsonCtx.OBJ)
    {
      var jc = jcts.Peek();
      curr_node.addChild(new MVarAccessNode(jc.scope_type, jc.name_or_idx, MVarAccessNode.READ_PUSH_CTX));
    }
    else
      curr_node.addChild(bnd.Create_New());

    for(int i=0;i<node.children.Count;++i)
    {
      var c = node.children[i];

      //checking if there's an explicit add to array operand
      if(c is AST_JsonArrAddItem)
      {
        var n = (Array_AddNode)bnd.Create_Add(); 
        n.push_arr = true;
        curr_node.addChild(n);
      }
      else
      {
        var jc = new JsonCtx(JsonCtx.ARR, node.ntype, (uint)i);
        jcts.Push(jc);
        Visit(c);
        jcts.Pop();
      }
    }

    //adding last item item
    if(node.children.Count > 0)
    {
      var n = (Array_AddNode)bnd.Create_Add(); 
      n.push_arr = true;
      curr_node.addChild(n);
    }
  }

  public override void DoVisit(bhl.AST_JsonPair node)
  {
    var jc = new JsonCtx(JsonCtx.OBJ, node.scope_ntype, node.nname);
    jcts.Push(jc);

    VisitChildren(node);
    curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name(), MVarAccessNode.WRITE_PUSH_CTX));

    jcts.Pop();
  }
}

public class UserBindings
{
  public virtual void Register(GlobalScope globs) {}
}

public class EmptyUserBindings : UserBindings {}

public interface IModuleLoader
{
  //NOTE: must return null if no such module
  AST_Module LoadModule(HashedName id);
}

public class ModuleLoader : IModuleLoader
{
  const byte FMT_BIN = 0;
  const byte FMT_LZ4 = 1;

  Stream source;
  MsgPackDataReader reader;
  Lz4DecoderStream decoder = new Lz4DecoderStream();
  MemoryStream mod_stream = new MemoryStream();
  MsgPackDataReader mod_reader;
  MemoryStream lz_stream = new MemoryStream();
  MemoryStream lz_dst_stream = new MemoryStream();
  bool strict;

  public class Entry
  {
    public byte format;
    public long stream_pos;
  }

  Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();

  public ModuleLoader(Stream source, bool strict = true)
  {
    this.strict = strict;
    Load(source);
  }

  void Load(Stream source_)
  {
    entries.Clear();

    source = source_;
    source.Position = 0;

    mod_reader = new MsgPackDataReader(mod_stream);

    reader = new MsgPackDataReader(source);

    int total_modules = 0;

    Util.Verify(reader.ReadI32(ref total_modules) == MetaIoError.SUCCESS);
    //Util.Debug("Total modules: " + total_modules);
    while(total_modules-- > 0)
    {
      int format = 0;
      Util.Verify(reader.ReadI32(ref format) == MetaIoError.SUCCESS);

      uint id = 0;
      Util.Verify(reader.ReadU32(ref id) == MetaIoError.SUCCESS);

      var ent = new Entry();
      ent.format = (byte)format;
      ent.stream_pos = source.Position;
      if(entries.ContainsKey(id))
        Util.Verify(false, "Key already exists: " + id);
      entries.Add(id, ent);

      //skipping binary blob
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      Util.Verify(reader.ReadRaw(ref tmp_buf, ref tmp_buf_len) == MetaIoError.SUCCESS);
      TempBuffer.Update(tmp_buf);
    }
  }

  public AST_Module LoadModule(HashedName id)
  {
    Entry ent;
    if(!entries.TryGetValue(id.n, out ent))
      return null;

    byte[] res = null;
    int res_len = 0;
    DecodeBin(ent, ref res, ref res_len);

    mod_stream.SetData(res, 0, res_len);
    mod_reader.setPos(0);

    Util.SetupAutogenFactory();

    var ast = new AST_Module();

    var ok = ast.read(mod_reader) == MetaIoError.SUCCESS;
    if(strict && !ok)
      Util.Verify(false, "Can't load module " + id);

    Util.RestoreAutogenFactory();

    return ast;
  }

  void DecodeBin(Entry ent, ref byte[] res, ref int res_len)
  {
    if(ent.format == FMT_BIN)
    {
      var tmp_buf = TempBuffer.Get();
      int tmp_buf_len = 0;
      reader.setPos(ent.stream_pos);
      Util.Verify(reader.ReadRaw(ref tmp_buf, ref tmp_buf_len) == MetaIoError.SUCCESS);
      TempBuffer.Update(tmp_buf);
      res = tmp_buf;
      res_len = tmp_buf_len;
    }
    else if(ent.format == FMT_LZ4)
    {
      var lz_buf = TempBuffer.Get();
      int lz_buf_len = 0;
      reader.setPos(ent.stream_pos);
      Util.Verify(reader.ReadRaw(ref lz_buf, ref lz_buf_len) == MetaIoError.SUCCESS);
      TempBuffer.Update(lz_buf);

      var dst_buf = TempBuffer.Get();
      var lz_size = (int)BitConverter.ToUInt32(lz_buf, 0);
      if(lz_size > dst_buf.Length)
        Array.Resize(ref dst_buf, lz_size);
      TempBuffer.Update(dst_buf);

      lz_dst_stream.SetData(dst_buf, 0, dst_buf.Length);
      //NOTE: uncompressed size is only added by PHP implementation
      //taking into account first 4 bytes which store uncompressed size
      //lz_stream.SetData(lz_buf, 4, lz_buf_len-4);
      lz_stream.SetData(lz_buf, 0, lz_buf_len);
      decoder.Reset(lz_stream);
      decoder.CopyTo(lz_dst_stream);
      res = lz_dst_stream.GetBuffer();
      res_len = (int)lz_dst_stream.Position;
    }
    else
      throw new Exception("Unknown format");
  }
}

public class ExtensibleModuleLoader : IModuleLoader
{
  public List<IModuleLoader> loaders = new List<IModuleLoader>();

  public AST_Module LoadModule(HashedName id)
  {
    for(int i=0;i<loaders.Count;++i)
    {
      var ld = loaders[i];
      var ast = ld.LoadModule(id);
      if(ast != null)
        return ast;
    }
    return null;
  }
}

} //namespace bhl

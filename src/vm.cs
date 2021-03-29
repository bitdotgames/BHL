using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public interface ValRefcounted
{
  void Retain();
  void Release(bool can_del = true);
  bool TryDel();
}

public class Val
{
  public const byte NONE      = 0;
  public const byte NUMBER    = 1;
  public const byte BOOL      = 2;
  public const byte STRING    = 3;
  public const byte OBJ       = 4;
  public const byte NIL       = 5;

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

  ValRefcounted _refc;

  //NOTE: below members are semi-public, one can use them for 
  //      fast access or non-allocating storage of structs(e.g vectors, quaternions)
  public byte _type;
  public double _num;
  public object _obj;

  static Queue<Val> pool = new Queue<Val>(64);
  static int pool_miss;
  static int pool_hit;

  //NOTE: use New() instead
  internal Val()
  {}

  static public Val New()
  {
    Val dv;
    if(pool.Count == 0)
    {
      ++pool_miss;
      dv = new Val();
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
      //NOTE: Val is Reset here instead of Del, see notes below 
      dv.Reset();
      dv._refs = 0;
    }
    return dv;
  }

  static public void Del(Val dv)
  {
    //NOTE: we don't Reset Val immediately, giving a caller
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
      var tmp = new Val(); 
      pool.Enqueue(tmp);
      //Console.WriteLine("NEW3: " + tmp.GetHashCode()/* + " " + Environment.StackTrace*/);
    }
    pool.Enqueue(dv);
    //Console.WriteLine("DEL: " + dv.GetHashCode()/* + " " + Environment.StackTrace*/);
    if(pool.Count > pool_miss)
      throw new Exception("Unbalanced New/Del " + pool.Count + " " + pool_miss);
  }

  //For proper assignments, like: dst = src (taking into account ref.counting)
  static public Val Assign(Val dst, Val src)
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
    _obj = null;
    _refc = null;
  }

  public void ValueCopyFrom(Val dv)
  {
    _type = dv._type;
    _num = dv._num;
    _obj = dv._obj;
    _refc = dv._refc;
  }

  public Val ValueClone()
  {
    Val dv = New();
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

  static public Val NewStr(string s)
  {
    Val dv = New();
    dv.SetStr(s);
    return dv;
  }

  public void SetStr(string s)
  {
    Reset();
    _type = STRING;
    _obj = s;
  }

  static public Val NewNum(int n)
  {
    Val dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(int n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public Val NewNum(double n)
  {
    Val dv = New();
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(double n)
  {
    Reset();
    _type = NUMBER;
    _num = n;
  }

  static public Val NewBool(bool b)
  {
    Val dv = New();
    dv.SetBool(b);
    return dv;
  }

  public void SetBool(bool b)
  {
    Reset();
    _type = BOOL;
    _num = b ? 1.0f : 0.0f;
  }

  static public Val NewObj(object o)
  {
    Val dv = New();
    dv.SetObj(o);
    return dv;
  }

  public void SetObj(object o)
  {
    Reset();
    _type = o == null ? NIL : OBJ;
    _obj = o;
    _refc = _obj as ValRefcounted;
  }

  static public Val NewNil()
  {
    Val dv = New();
    dv.SetNil();
    return dv;
  }

  public void SetNil()
  {
    Reset();
    _type = NIL;
  }

  public bool IsEqual(Val o)
  {
    bool res =
      _type == o.type &&
      _num == o._num &&
      _obj == o._obj
      ;

    return res;
  }

  public override string ToString() 
  {
    string str = "";
    if(type == NUMBER)
      str = _num + ":<NUMBER>";
    else if(type == BOOL)
      str = bval + ":<BOOL>";
    else if(type == STRING)
      str = this.str + ":<STRING>";
    else if(type == OBJ)
      str = _obj.GetType().Name + ":<OBJ>";
    else if(type == NIL)
      str = "<NIL>";
    else
      str = "DYNVAL: type:"+type;

    return str;// + " " + GetHashCode();//for extra debug
  }

  public object ToAny() 
  {
    if(type == NUMBER)
      return (object)_num;
    else if(type == BOOL)
      return (object)bval;
    else if(type == STRING)
      return (string)_obj;
    else if(type == OBJ)
      return _obj;
    else if(type == NIL)
      return null;
    else
      throw new Exception("ToAny(): please support type: " + type);
  }

  static public void PoolAlloc(int num)
  {
    for(int i=0;i<num;++i)
    {
      ++pool_miss;
      var tmp = new Val(); 
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
    var dvs = new Val[pool.Count];
    pool.CopyTo(dvs, 0);
    for(int i=dvs.Length;i-- > 0;)
    {
      var v = dvs[i];
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

public class ValList : IList<Val>, ValRefcounted
{
  //NOTE: exposed to allow manipulations like Reverse(). Use with caution.
  public readonly List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  //////////////////IList//////////////////

  public int Count { get { return lst.Count; } }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }
  public bool IsSynchronized { get { throw new NotImplementedException(); } }
  public object SyncRoot { get { throw new NotImplementedException(); } }

  public static implicit operator Val(ValList lst)
  {
    return Val.NewObj(lst);
  }

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
      lst[i].RefMod(RefOp.USR_DEC | RefOp.DEC);

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

  public void CopyFrom(ValList lst)
  {
    Clear();
    for(int i=0;i<lst.Count;++i)
      Add(lst[i]);
  }

  ///////////////////////////////////////

  static public Stack<ValList> pool = new Stack<ValList>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal ValList()
  {}

  public static ValList New()
  {
    ValList lst;
    if(pool.Count == 0)
    {
      ++pool_miss;
      lst = new ValList();
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

  static public void Del(ValList lst)
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

public class ValDict : ValRefcounted
{
  public Dictionary<ulong, Val> vars = new Dictionary<ulong, Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  static public Stack<ValDict> pool = new Stack<ValDict>();
  static int pool_hit;
  static int pool_miss;

  //NOTE: use New() instead
  internal ValDict()
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

  public static ValDict New()
  {
    ValDict tb;
    if(pool.Count == 0)
    {
      ++pool_miss;
      tb = new ValDict();
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

  static public void Del(ValDict tb)
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

  public void Set(HashedName key, Val val)
  {
    ulong k = key.n; 
    Val prev;
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

  public bool TryGet(HashedName key, out Val val)
  {
    return vars.TryGetValue(key.n, out val);
  }

  public Val Get(HashedName key)
  {
    return vars[key.n];
  }

  public void CopyFrom(ValDict o)
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

public class VM
{
  public class Frame
  {
    public uint ip;
    public Stack<Val> num_stack;
    public List<Val> locals;

    public Frame()
    {
      ip = 0;
      num_stack = new Stack<Val>();
      locals = new List<Val>();
    }
  }

  List<Const> constants;
  byte[] instructions;
  Dictionary<string, uint> func2ip;

  //public for inspection purposes only
  public Stack<Val> stack = new Stack<Val>();

  //public for inspection purposes only
  public Stack<Frame> frames = new Stack<Frame>();
  public Frame curr_frame;

  public VM(byte[] instructions, List<Const> constants, Dictionary<string, uint> func2ip)
  {
    this.instructions = instructions;
    this.constants = constants;
    this.func2ip = func2ip;
  }

  public void Exec(string func)
  {
    var fr = new Frame();
    fr.ip = func2ip[func];
    frames.Push(fr);
    Run();
  }

  public void Run()
  {
    Opcodes opcode;
    curr_frame = frames.Peek();
    while(frames.Count > 0)
    {
      opcode = (Opcodes)instructions[curr_frame.ip];

      switch(opcode)
      {
        case Opcodes.Constant:
          ++curr_frame.ip;
          int const_idx = WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);

          if(const_idx >= constants.Count)
            throw new Exception("Index out of constant pool: " + const_idx);

          var cn = constants[const_idx];
          curr_frame.num_stack.Push(cn.ToVal());
        break;
        case Opcodes.New:
          curr_frame.num_stack.Push(Val.NewObj(ValList.New()));
          ++curr_frame.ip;
        break;
        case Opcodes.Add:
        case Opcodes.Sub:
        case Opcodes.Div:
        case Opcodes.Mod:
        case Opcodes.Mul:
        case Opcodes.And:
        case Opcodes.Or:
        case Opcodes.BitAnd:
        case Opcodes.BitOr:
        case Opcodes.Equal:
        case Opcodes.NotEqual:
        case Opcodes.Greather:
        case Opcodes.Less:
        case Opcodes.GreatherOrEqual:
        case Opcodes.LessOrEqual:
          ExecuteBinaryOperation(opcode);
        break;
        case Opcodes.UnaryNot:
        case Opcodes.UnaryNeg:
          ExecuteUnaryOperation(opcode);
        break;
        case Opcodes.SetVar:
        case Opcodes.GetVar:
          ExecuteVariablesOperation(opcode);
        break;
        case Opcodes.ReturnVal:
          var ret_val = curr_frame.num_stack.Peek();
          frames.Pop();
          if(frames.Count > 0)
          {
            curr_frame = frames.Peek();
            curr_frame.num_stack.Push(ret_val);
          }
          else
          {
            stack.Push(ret_val);
          }
        break;
        case Opcodes.MethodCall:
          ++curr_frame.ip;
          var builtin = WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);

          ExecuteBuiltInArrayFunc((BuiltInArray) builtin);
        break;
        case Opcodes.IdxGet:// move to -At- method?
          var idx = curr_frame.num_stack.Pop();
          var arr = curr_frame.num_stack.Pop();
          var lst = GetAsList(arr);

          var res = lst[(int)idx.num]; 
          curr_frame.num_stack.Push(res);
          //NOTE: this can be an operation for the temp. array,
          //      we need to try del the array if so
          lst.TryDel();
        break;
        case Opcodes.FuncCall:
          ++curr_frame.ip;

          var fr = new Frame();
          fr.ip = (uint)WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);

          ++curr_frame.ip;

          var args_info = new FuncArgsInfo((uint)WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip));
          for(int i = 0; i < args_info.CountArgs(); ++i)
            fr.num_stack.Push(curr_frame.num_stack.Pop());

          frames.Push(fr);
          curr_frame = frames.Peek();
        continue;
        case Opcodes.Jump:
          ++curr_frame.ip;
          curr_frame.ip = curr_frame.ip + (uint)WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);
        break;
        case Opcodes.LoopJump:
          ++curr_frame.ip;
          curr_frame.ip = curr_frame.ip - (uint)WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);
        break;
        case Opcodes.CondJump:
          ++curr_frame.ip;
          if(curr_frame.num_stack.Pop().bval == false)
            curr_frame.ip = curr_frame.ip + (uint)WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);
          ++curr_frame.ip;
        continue;
        case Opcodes.DefArg:
          ++curr_frame.ip; //need add check for args more than 1 byte long
          var arg = WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);
          if(curr_frame.num_stack.Count > 0)
            curr_frame.ip = curr_frame.ip + (uint)arg;
          ++curr_frame.ip;
        continue;
      }
      ++curr_frame.ip;
    }
  }

  void ExecuteVariablesOperation(Opcodes op)
  {
    ++curr_frame.ip;
    int local_idx = WriteBuffer.DecodeBytes(instructions, ref curr_frame.ip);
    switch(op)
    {
      case Opcodes.SetVar:
        if(local_idx < curr_frame.locals.Count)
          curr_frame.locals[local_idx] = curr_frame.num_stack.Pop();
        else 
          if(curr_frame.num_stack.Count > 0)
            curr_frame.locals.Add(curr_frame.num_stack.Pop());
          else
            curr_frame.locals.Add(Val.New());
      break;
      case Opcodes.GetVar:
        if(local_idx >= curr_frame.locals.Count)
          throw new Exception("Index out of locals pool: " + local_idx);
        curr_frame.num_stack.Push(curr_frame.locals[local_idx]);
      break;
    }
  }

  void ExecuteUnaryOperation(Opcodes op)
  {
    var operand = curr_frame.num_stack.Pop();
    switch(op)
    {
      case Opcodes.UnaryNot:
        curr_frame.num_stack.Push(Val.NewBool(operand.num != 1));
      break;
      case Opcodes.UnaryNeg:
        curr_frame.num_stack.Push(Val.NewNum(operand.num * -1));
      break;
    }
  }

  ValList GetAsList(Val arr)
  {
    var lst = arr.obj as ValList;
    if(lst == null)
      throw new UserError("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  void ExecuteBuiltInArrayFunc(BuiltInArray func)
  {
    Val idx;
    Val arr;
    Val val;
    ValList lst;
    switch(func)
    {
      case BuiltInArray.Add:
        val = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Peek();
        lst = GetAsList(arr);

        lst.Add(val);
      break;
      case BuiltInArray.RemoveAt:
        idx = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Pop();
        lst = GetAsList(arr);

        lst.RemoveAt((int)idx.num); 
      break;
      case BuiltInArray.SetAt:
        idx = curr_frame.num_stack.Pop();
        arr = curr_frame.num_stack.Pop();
        val = curr_frame.num_stack.Pop();
        lst = GetAsList(arr);

        lst[(int)idx.num] = val;
      break;
      case BuiltInArray.Count:
        arr = curr_frame.num_stack.Peek();
        lst = GetAsList(arr);
        curr_frame.num_stack.Push(Val.NewNum(lst.Count));
      break;
      default:
        throw new Exception("Unknown method -> " + func);
    }
  }

  void ExecuteBinaryOperation(Opcodes op)
  {
    var stk = curr_frame.num_stack;

    var r_opertand = stk.Pop();
    var l_opertand = stk.Pop();

    switch(op)
    {
      case Opcodes.Add:
        if((r_opertand._type == Val.STRING) && (l_opertand._type == Val.STRING))
          stk.Push(Val.NewStr((string)l_opertand._obj + (string)r_opertand._obj));
        else
          stk.Push(Val.NewNum(l_opertand._num + r_opertand._num));
      break;
      case Opcodes.Sub:
        stk.Push(Val.NewNum(l_opertand._num - r_opertand._num));
      break;
      case Opcodes.Div:
        stk.Push(Val.NewNum(l_opertand._num / r_opertand._num));
      break;
      case Opcodes.Mul:
        stk.Push(Val.NewNum(l_opertand._num * r_opertand._num));
      break;
      case Opcodes.Equal:
        stk.Push(Val.NewBool(l_opertand._num == r_opertand._num));
      break;
      case Opcodes.NotEqual:
        stk.Push(Val.NewBool(l_opertand._num != r_opertand._num));
      break;
      case Opcodes.Greather:
        stk.Push(Val.NewBool(l_opertand._num > r_opertand._num));
      break;
      case Opcodes.Less:
        stk.Push(Val.NewBool(l_opertand._num < r_opertand._num));
      break;
      case Opcodes.GreatherOrEqual:
        stk.Push(Val.NewBool(l_opertand._num >= r_opertand._num));
      break;
      case Opcodes.LessOrEqual:
        stk.Push(Val.NewBool(l_opertand._num <= r_opertand._num));
      break;
      case Opcodes.And:
        stk.Push(Val.NewBool(l_opertand._num == 1 && r_opertand._num == 1));
      break;
      case Opcodes.Or:
        stk.Push(Val.NewBool(l_opertand._num == 1 || r_opertand._num == 1));
      break;
      case Opcodes.BitAnd:
        stk.Push(Val.NewNum((int)l_opertand._num & (int)r_opertand._num));
      break;
      case Opcodes.BitOr:
        stk.Push(Val.NewNum((int)l_opertand._num | (int)r_opertand._num));
      break;
      case Opcodes.Mod:
        stk.Push(Val.NewNum((int)l_opertand._num % (int)r_opertand._num));
      break;
    }
  }

  public Val GetStackTop()
  {
    return stack.Peek();
  }

  public void ShowFullStack()
  {
    Console.WriteLine(" VM STACK :");
    foreach(var v in stack)
    {
      Console.WriteLine("\t" + v);
    }
  }
}

} //namespace bhl

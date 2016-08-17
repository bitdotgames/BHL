using System;
using System.IO;
using System.Collections.Generic;
using game;

namespace bhl {

//NOTE: it's a struct
public struct DynVal
{
  public const int NONE   = 0;
  public const int NUMBER = 1;
  public const int BOOL   = 2;
  public const int STRING = 3;
  public const int USER   = 4;

  public const int REF_INC          =  1;
  public const int REF_DEC          = -1;
  public const int REF_TRY_RELEASE  = -2;

  public delegate void RefsCb(object obj, int r);

  public bool IsEmpty { get { return type == NONE; } }

  public int type { get { return _type; } }

  public double num {
    get {
      return _num;
    }
    set {
      Set(value);
    }
  }

  public string str {
    get {
      return _str;
    }
    set {
      Set(value);
    }
  }

  public object obj {
    get {
      return _obj;
    }
    set {
      Set(value);
    }
  }

  public bool bval {
    get {
      return _num == 1;
    }
    set {
      Set(value);
    }
  }

  //NOTE: even though it's public it's not intended for direct assigning,
  //      except in cases you know what you are doing
  public int _type;
  //NOTE: semi-private, don't use directly 
  public double _num;
  //NOTE: for non-allocating storage of structs(e.g vectors, quaternions)
  public double num2;
  public double num3;
  public double num4;
  //NOTE: semi-private, don't use directly 
  public string _str;
  //NOTE: semi-private, don't use directly 
  public object _obj;
  public bool use_refc;

  public DynVal(string s)
  {
    _type = NONE;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;

    Set(s);
  }

  public void Set(string s)
  {
    _type = STRING;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = s;
    use_refc = false;
  }

  public DynVal(int n)
  {
    _type = NONE;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;

    Set(n);
  }

  public void Set(int n)
  {
    _type = NUMBER;
    _num = n;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;
  }

  public DynVal(double n)
  {
    _type = NONE;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;

    Set(n);
  }

  public void Set(double n)
  {
    _type = NUMBER;
    _num = n;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;
  }

  public DynVal(bool b)
  {
    _type = NONE;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;

    Set(b);
  }

  public void Set(bool b)
  {
    _type = BOOL;
    _num = b ? 1.0f : 0.0f;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = null;
    _str = "";
    use_refc = false;
  }

  public void Set(object o)
  {
    _type = USER;
    _num = 0;
    num2 = 0;
    num3 = 0;
    num4 = 0;
    _obj = o;
    _str = "";
    use_refc = o is DynValRefcounted;
  }

  public bool IsEqual(DynVal o)
  {
    return
      _type == o.type &&
      _num == o.num &&
      num2 == o.num2 &&
      num3 == o.num3 &&
      num4 == o.num4 &&
      _str == o.str &&
      _obj == o.obj;
  }

  public override string ToString() 
  {
    if(type == NUMBER)
      return _num  + ":<NUMBER>";
    else if(type == BOOL)
      return bval + ":<BOOL>";
    else if(type == STRING)
      return _str + ":<STRING>";
    else if(type == USER)
      return _obj.GetType().Name + ":<USER>";
    else
      return "DYNVAL: type:"+type;
  }

  public void IncRefs()
  {
    if(!use_refc)
      return;

    (_obj as DynValRefcounted).RefCountEvent(REF_INC);
  }

  public void DecRefs()
  {
    if(!use_refc)
      return;

    (_obj as DynValRefcounted).RefCountEvent(REF_DEC);
  }

  public void TryRelease()
  {
    if(!use_refc)
      return;

    (_obj as DynValRefcounted).RefCountEvent(REF_TRY_RELEASE);
  }
}

public interface DynValRefcounted
{
  void RefCountEvent(int r);
}

public class MemoryScope
{
  public Dictionary<uint, DynVal> vars = new Dictionary<uint, DynVal>();

  public void Clear()
  {
    var enm = vars.GetEnumerator();
    try
    {
      while(enm.MoveNext())
      {
        var val = enm.Current.Value;
        val.DecRefs();
        val.TryRelease();
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
    if(vars.ContainsKey((uint)key.n))
    {
      vars[(uint)key.n] = val;
    }
    else
    {
      val.IncRefs();
      vars[(uint)key.n] = val;
    }
  }

  public bool TryGet(HashedName key, out DynVal val)
  {
    return vars.TryGetValue((uint)key.n, out val);
  }

  public void Unset(HashedName key)
  {
    DynVal val;
    if(vars.TryGetValue((uint)key.n, out val))
    {
      val.DecRefs();
      val.TryRelease();
      vars.Remove((uint)key.n);
    }
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

public struct FuncRef
{
  public AST_FuncDecl decl;
  public FuncBindSymbol fbnd;

  public FuncRef(AST_FuncDecl decl, FuncBindSymbol fbnd)
  {
    this.decl = decl;
    this.fbnd = fbnd;
  }

  public bool IsEqual(FuncRef o)
  {
    return decl == o.decl && fbnd == o.fbnd;
  }

  public HashedName Name()
  {
    if(decl != null)
      return decl.Name();
    else if(fbnd != null)
      return fbnd.Name();
    else
      return new HashedName(0, "?");
  }
}

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim node);
  public abstract void DoVisit(AST_Import node);
  public abstract void DoVisit(AST_Module node);
  public abstract void DoVisit(AST_VarDecl node);
  public abstract void DoVisit(AST_Assign node);
  public abstract void DoVisit(AST_FuncDecl node);
  public abstract void DoVisit(AST_LambdaDecl node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(AST node)
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
    else if(node is AST_Assign)
      DoVisit(node as AST_Assign);
    else if(node is AST_LambdaDecl)
      DoVisit(node as AST_LambdaDecl);
    //NOTE: base class must be handled after AST_LambdaDecl
    else if(node is AST_FuncDecl)
      DoVisit(node as AST_FuncDecl);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
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
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
  }
}

public class FuncCtx : DynValRefcounted
{
  public int refs;

  public FuncRef fr;
  public MemoryScope mem = new MemoryScope();
  public FuncNode fnode;

  FuncCtx(FuncRef fr)
  {
    this.fr = fr;
  }

  public FuncNode GetNode()
  {
    if(fnode != null)
      return fnode;

    if(fr.fbnd != null)
      fnode = new FuncNodeBinding(fr.fbnd);
    else if(fr.decl is AST_LambdaDecl)
      fnode = new FuncNodeLambda(this);
    else
      fnode = new FuncNodeAST(fr.decl);

    return fnode;
  }

  public void RefCountEvent(int r)
  {
    //Console.WriteLine("REFS CB: " + self.refs + " " + r);

    if(r == DynVal.REF_INC)
      IncRefs();
    else if(r == DynVal.REF_DEC)
      DecRefs(false);
    else if(r == DynVal.REF_TRY_RELEASE)
      TryRelease();
  }

  public FuncCtx IncRefsOrDup()
  {
    if(refs == 1)
    {
      IncRefs();
      return this;
    }
    else if(refs > 1)
    {
      var dup = FuncCtx.PoolRequest(fr);
      dup.mem.CopyFrom(mem);
      dup.IncRefs();
      return dup;
    }
    else
    {
      throw new Exception("Invalid state: " + refs);
    }
  }

  public void IncRefs()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;
  }

  public void DecRefs(bool try_release = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;
    if(try_release)
      TryRelease();
  }

  public void TryRelease()
  {
    if(refs == 0)
    {
      PoolRelease(this);
      refs = -1;
    }
  }

  //////////////////////////////////////////////

  public struct PoolItem
  {
    public bool used;
    public FuncCtx fct;
  }

  static public List<PoolItem> pool = new List<PoolItem>();
  static int pool_hit;
  static int pool_miss;

  public static FuncCtx PoolRequest(FuncRef fr)
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(!item.used && item.fct.fr.IsEqual(fr))
      {
        ++pool_hit;

        //Util.Debug("FTX REQUEST " + item.fct.GetHashCode());

        item.used = true;
        pool[i] = item;
        item.fct.refs = 0;
        return item.fct;
      }
    }

    {
      ++pool_miss;

      var fct = new FuncCtx(fr);
      //Util.Debug("FTX REQUEST2 " + fct.GetHashCode());
      var item = new PoolItem();
      item.fct = fct;
      item.used = true;
      pool.Add(item);
      return fct;
    }
  }

  static public void PoolRelease(FuncCtx fct)
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(item.fct == fct)
      {
        //Util.Debug("FTX RELEASE " + fct.GetHashCode());
        item.fct.mem.Clear();
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

public class DynValList : List<DynVal>, DynValRefcounted
{
  public int refs;

  public void RefCountEvent(int r)
  {
    if(r == DynVal.REF_INC)
      IncRefs();
    else if(r == DynVal.REF_DEC)
      DecRefs(false);
    else if(r == DynVal.REF_TRY_RELEASE)
      TryRelease();
  }

  //TODO: results in boxing?
  //public static implicit operator DynVal(DynValList lst)
  //{
  //  return lst.ToDynVal();
  //}

  public DynVal ToDynVal()
  {
    var dv = new DynVal();
    dv.obj = this;
    return dv;
  }

  public void IncRefs()
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    ++refs;
  }

  public void DecRefs(bool try_release = true)
  {
    if(refs == -1)
      throw new Exception("Invalid state");
    if(refs == 0)
      throw new Exception("Double free");

    --refs;
    if(try_release)
      TryRelease();
  }

  public void TryRelease()
  {
    if(refs == 0)
    {
      for(int i=0;i<Count;++i)
      {
        this[i].DecRefs();
        this[i].TryRelease();
      }
      Clear();

      PoolRelease(this);
      refs = -1;
    }
  }

  ///////////////////////////////////////

  public struct PoolItem
  {
    public bool used;
    public DynValList lst;
  }

  static public List<PoolItem> pool = new List<PoolItem>();
  static int pool_hit;
  static int pool_miss;

  public static DynValList PoolRequest()
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(!item.used)
      {
        ++pool_hit;

        item.used = true;
        pool[i] = item;
        item.lst.refs = 0;
        return item.lst;
      }
    }

    {
      ++pool_miss;

      var lst = new DynValList();
      var item = new PoolItem();
      item.lst = lst;
      item.used = true;
      pool.Add(item);
      return lst;
    }
  }

  static public void PoolRelease(DynValList lst)
  {
    for(int i=0;i<pool.Count;++i)
    {
      var item = pool[i];
      if(item.lst == lst)
      {
        item.lst.Clear();
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
  /////////////////////////////////////////
  
  public static void Decode(DynVal dv, ref List<string> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add(tmp.str);
    }
  }

  public static void Encode(List<string> dst, ref DynVal dv)
  {
    var lst = PoolRequest();
    for(int i=0;i<dst.Count;++i)
      lst.Add(new DynVal(dst[i]));
    dv = lst.ToDynVal();
  }

  public static void Decode(DynVal dv, ref List<uint> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((uint)tmp.num);
    }
  }

  public static void Encode(List<uint> dst, ref DynVal dv)
  {
    var lst = PoolRequest();
    for(int i=0;i<dst.Count;++i)
      lst.Add(new DynVal(dst[i]));
    dv = lst.ToDynVal();
  }

  public static void Decode(DynVal dv, ref List<int> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((int)tmp.num);
    }
  }

  public static void Encode(List<int> dst, ref DynVal dv)
  {
    var lst = PoolRequest();
    for(int i=0;i<dst.Count;++i)
      lst.Add(new DynVal(dst[i]));
    dv = lst.ToDynVal();
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

  FastStack<int> func_args_stack = new FastStack<int>(128);

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

  IModuleLoader module_loader;
  public Dictionary<uint,bool> loaded_modules = new Dictionary<uint,bool>();

  Dictionary<ulong,AST_FuncDecl> func_decls = new Dictionary<ulong,AST_FuncDecl>();
  Dictionary<ulong,AST_LambdaDecl> lmb_decls = new Dictionary<ulong,AST_LambdaDecl>();

  public GlobalScope bindings;

  FastStack<DynVal> stack = new FastStack<DynVal>(256);

  public void Init(GlobalScope bindings, IModuleLoader module_loader)
  {
    node_stack.Clear();
    curr_node = null;
    last_member_ctx = new DynVal();
    mstack.Clear();
    curr_mem = null;
    loaded_modules.Clear();
    func_decls.Clear();
    lmb_decls.Clear();
    stack.Clear();

    this.bindings = bindings;
    this.module_loader = module_loader;
  }

  public struct Result
  {
    public BHS status;
    public DynVal val;
  }

  public Result ExecNode(BehaviorTreeNode node, bool ret_value = true, object agent = null)
  {
    Result res = new Result();

    res.status = BHS.NONE;
    while(true)
    {
      res.status = node.run(agent);
      if(res.status != BHS.RUNNING)
        break;
    }
    res.val = ret_value ? PopValue() : new DynVal();
    return res;
  }

  public void Interpret(AST_Module ast)
  {
    Visit(ast);
  }

  public void LoadModule(uint mod_id)
  {
    if(mod_id == 0)
      return;

    if(loaded_modules.ContainsKey(mod_id))
      return;
    loaded_modules.Add(mod_id, true);

    if(module_loader == null)
      throw new Exception("Module loader is not set");

    var mod_ast = module_loader.LoadModule(mod_id);
    Interpret(mod_ast);
  }

  public void LoadModule(string module)
  {
    LoadModule(Util.GetModuleId(module));
  }

  public FuncRef GetFuncRef(HashedName name)
  {
    var fr = new FuncRef();

    var fbnd = bindings.FindBinding<FuncBindSymbol>(name.n);
    if(fbnd != null)
    {
      fr.fbnd = fbnd;
      return fr;
    }

    var func_decl = FetchFuncDecl(name.n);
    if(func_decl != null)
    {
      fr.decl = func_decl;
      return fr;
    }

    throw new Exception("No such function: " + name);
  }

  AST_FuncDecl FetchFuncDecl(ulong name)
  {
    AST_FuncDecl func_decl;
    if(func_decls.TryGetValue(name, out func_decl))
      return func_decl;
    return null;
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
    var fr = GetFuncRef(name);

    if(fr.fbnd != null)
      return new FuncNodeBinding(fr.fbnd);
    else
      return new FuncNodeAST(fr.decl);
  }

  public FuncNode GetFuncNode(string module_name, string func_name)
  {
    LoadModule(module_name);
    return GetFuncNode(Util.GetFuncId(module_name, func_name));
  }

  public FuncNode GetMFuncNode(uint class_type, HashedName name)
  {
    var bnd = bindings.FindBinding<ClassSymbol>(class_type);
    if(bnd == null)
      throw new Exception("Class binding not found: " + class_type); 

    var bnd_member = bnd.findMember(name.n);
    if(bnd_member == null)
      throw new Exception("Member not found: " + name);

    var func_symb = bnd_member as FuncBindSymbol;
    if(func_symb != null)
      return new FuncNodeBinding(func_symb);

    throw new Exception("Not a func symbol: " + name);
  }

  public void PushFuncArgsNum(int args_num)
  {
    func_args_stack.Push(args_num);
  }

  public void PopFuncArgsNum()
  {
    func_args_stack.Pop();
  }

  public int GetFuncArgsNum()
  {
    return func_args_stack.Peek();
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
    v.IncRefs();
    stack.Push(v);
  }

  public DynVal PopValue(bool dec_refs = true)
  {
    var v = stack.PopFast();
    if(dec_refs)
      v.DecRefs();
    return v;
  }

  public bool PeekValue(ref DynVal res)
  {
    if(stack.Count == 0)
      return false;

    res = stack.Peek();
    return true;
  }

  public int StackCount()
  {
    return stack.Count;
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
    {
      LoadModule(node.modules[i]);
    }
  }

  void CheckFuncIsUnique(ulong nname, string name)
  {
    if(func_decls.ContainsKey(nname))
      throw new Exception("Func decl is already defined: " + name + "(" + nname + ")");
    else if(bindings.FindBinding<FuncBindSymbol>(nname) != null)
      throw new Exception("Func binding already defined: " + name + "(" + nname + ")");
    else if(lmb_decls.ContainsKey(nname))
      throw new Exception("Lambda already defined: " + name + "(" + nname + ")");
  }

  public override void DoVisit(AST_FuncDecl node)
  {
    CheckFuncIsUnique(node.nname(), node.name);

    //Util.Debug("Adding func " + node.name + "(" + node.nname + ")");
    func_decls.Add(node.nname(), node);
  }

  public override void DoVisit(AST_LambdaDecl node)
  {
    if(!lmb_decls.ContainsKey(node.nname()))
    {
      CheckFuncIsUnique(node.nname(), node.name);
      lmb_decls.Add(node.nname(), node);
    }

    curr_node.addChild(new PushFuncCtxNode(node, null));
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
    var bnd = bindings.FindBinding<ConfNodeSymbol>(ntype);
    if(bnd == null)
      throw new Exception("Could not find class binding: " + ntype);
    return bnd.func_creator();
  }

  public override void DoVisit(AST_Call node)
  {           
    if(node.type == EnumCall.VAR)
    {
      curr_node.addChild(new VarAccessNode(node.Name()));
      VisitChildren(node);
    }
    else if(node.type == EnumCall.MVAR)
    {
      curr_node.addChild(new MVarAccessNode(node.scope_ntype, node.Name()));
      VisitChildren(node);
    }
    else if(node.type == EnumCall.FUNC || node.type == EnumCall.MFUNC)
    {
      AddFuncCallNode(node);

      //rest of the call chain
      for(int i=node.cargs_num;i<node.children.Count;++i)
        Visit(node.children[i]);
    }
    else if(node.type == EnumCall.VAR2FUNC)
    {
      curr_node.addChild(new CallVarFuncPtr(node.Name()));
    }
    else if(node.type == EnumCall.FUNC2VAR)
    {
      var func_decl = FetchFuncDecl(node.nname());
      if(func_decl != null)
      {
        curr_node.addChild(new PushFuncCtxNode(func_decl, null));
      }
      else
      {
        var bnd = bindings.FindBinding<FuncBindSymbol>(node.nname());
        if(bnd == null)
          throw new Exception("Could not find func decl:" + node.Name());

        curr_node.addChild(new PushFuncCtxNode(null, bnd));
      }
    }
    else if(node.type == EnumCall.ARR_IDX)
    {
      Visit(node.children[0]);
      Visit(node.children[1]);

      var bnd = bindings.FindBinding<ArrayTypeSymbol>(node.scope_ntype);
      if(bnd == null)
        throw new Exception("Could not find class binding: " + node.scope_ntype);

      curr_node.addChild(bnd.Create_At());

      //rest
      for(int i=2;i<node.children.Count;++i)
        Visit(node.children[i]);
    }
    else 
      throw new Exception("Unsupported call type: " + node.type);
  }

  void AddFuncCallNode(AST_Call ast)
  {
    var func_symb = bindings.FindBinding<FuncSymbol>(ast.nname());

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
        VisitChildren(ast.children[0]);
        PopNode();

        var last_child = group.children[group.children.Count-1];
        //1.1. changing write mode or popping config
        if(last_child is MVarAccessNode)
          (last_child as MVarAccessNode).mode = MVarAccessNode.WRITE2;
        else
          group.addChild(new PopValueNode());

        if(can_be_precalculated)
        {
          group.run(null);
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
        var dv = new DynVal();
        conf_symb.conf_getter(conf_node, ref dv, true/*reset*/);
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

  bool CheckIfConfigTweaksAreConstant(AST ast, uint json_ctx_type = 0)
  {
    if(ast is AST_JsonObj)
      json_ctx_type = (ast as AST_JsonObj).ntype;
    else if(ast is AST_JsonArr)
      json_ctx_type = (ast as AST_JsonArr).ntype;

    for(int i=0;i<ast.children.Count;++i)
    {
      var c = ast.children[i];

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
    VisitChildren(node);
    curr_node.addChild(new ReturnNode());
  }

  public override void DoVisit(AST_Break node)
  {
    curr_node.addChild(new BreakNode());
  }

  public override void DoVisit(AST_Literal node)
  {
    curr_node.addChild(new LiteralNode(node));
  }

  public override void DoVisit(AST_BinaryOpExp node)
  {
    VisitChildren(node);
    curr_node.addChild(new BinaryOpNode(node));
  }

  public override void DoVisit(AST_UnaryOpExp node)
  {
    VisitChildren(node);
    curr_node.addChild(new UnaryOpNode(node));
  }

  public override void DoVisit(AST_Assign node)
  {
    //1. calc final value
    Visit(node.children[1]);
    //2. eval expression
    Visit(node.children[0]);
    //3. let's tune eval expression
    var last_child = curr_node.children[curr_node.children.Count-1];
    if(last_child is MVarAccessNode)
      (last_child as MVarAccessNode).mode = MVarAccessNode.WRITE;
    else if(last_child is VarAccessNode)
      (last_child as VarAccessNode).mode = VarAccessNode.WRITE;
    else
      throw new Exception("Not supported target node: " + last_child.GetType());
  }

  public override void DoVisit(AST_VarDecl node)
  {
    if(node.children.Count > 0)
    {
      VisitChildren(node);
      curr_node.addChild(new VarAccessNode(node.Name(), VarAccessNode.WRITE));
    }
    else
    {
      curr_node.addChild(new VarAccessNode(node.Name(), VarAccessNode.DECL));
    }
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
    var bnd = bindings.FindBinding<ArrayTypeSymbol>(node.ntype);
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

      var jc = new JsonCtx(JsonCtx.ARR, node.ntype, (uint)i);
      jcts.Push(jc);

      Visit(c);

      jcts.Pop();

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
  AST_Module LoadModule(uint id);
}

public class ModuleLoader : IModuleLoader
{
  const int FMT_BIN = 0;
  const int FMT_LZ4 = 1;

  Stream source;
  MsgPackDataReader reader;
  Lz4DecoderStream decoder = new Lz4DecoderStream();
  MemoryStream mod_stream = new MemoryStream();
  MsgPackDataReader mod_reader;
  MemoryStream lz_stream = new MemoryStream();
  MemoryStream lz_dst_stream = new MemoryStream();

  public class Entry
  {
    public long stream_pos;
    public int format;
  }

  Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();

  public ModuleLoader(Stream source)
  {
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
    //Log.Debug("Total modules: " + total_modules);
    while(total_modules-- > 0)
    {
      int format = 0;
      Util.Verify(reader.ReadI32(ref format) == MetaIoError.SUCCESS);

      uint id = 0;
      Util.Verify(reader.ReadU32(ref id) == MetaIoError.SUCCESS);

      var ent = new Entry();
      ent.format = format;
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

  public AST_Module LoadModule(uint id)
  {
    Entry ent;
    if(!entries.TryGetValue(id, out ent))
      Util.Verify(false, "Entry not found: " + id);

    byte[] res = null;
    int res_len = 0;
    DecodeBin(ent, ref res, ref res_len);

    mod_stream.SetData(res, 0, res_len);
    mod_reader.setPos(0);

    Util.SetupAutogenFactory();

    var ast = new AST_Module();
    Util.Verify(ast.read(mod_reader) == MetaIoError.SUCCESS);

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

} //namespace bhl

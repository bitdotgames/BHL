using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public enum Opcodes
{
  Constant         ,
  Add              ,
  Sub              ,
  Div              ,
  Mul              ,
  SetVar           ,
  GetVar           ,
  DeclVar          ,
  ArgVar           ,
  SetGVar          ,
  GetGVar          ,
  SetGVarImported  ,
  GetGVarImported  ,
  Return           ,
  ReturnVal        ,
  Jump             ,
  JumpZ            ,
  JumpPeekZ        ,
  JumpPeekNZ       ,
  Break            ,
  Continue         ,
  Pop              ,
  Call             ,
  CallNative       ,
  CallImported     ,
  CallMethod       ,
  CallMethodNative ,
  CallPtr          ,
  GetFunc          ,
  GetFuncNative    ,
  GetFuncFromVar   ,
  GetFuncImported  ,
  FuncPtrToTop     ,
  GetAttr          ,
  RefAttr          ,
  SetAttr          ,
  SetAttrInplace   ,
  ArgRef           ,
  UnaryNot         ,
  UnaryNeg         ,
  And              ,
  Or               ,
  Mod              ,
  BitOr            ,
  BitAnd           ,
  Equal            ,
  NotEqual         ,
  LT               ,
  LTE              ,
  GT               ,
  GTE              ,
  DefArg           , 
  TypeCast         ,
  Block            ,
  New              ,
  Lambda           ,
  UseUpval         ,
  InitFrame        ,
  Inc              ,
  Dec              ,
  Func             ,
  ClassBegin       ,
  ClassMember      ,
  ClassEnd         ,
  Import           ,
}

public class Const
{
  static public readonly Const Nil = new Const(EnumLiteral.NIL, 0, "");

  public EnumLiteral type;
  public double num;
  public string str;

  public Const(EnumLiteral type, double num, string str)
  {
    this.type = type;
    this.num = num;
    this.str = str;
  }

  public Const(AST_Literal lt)
  {
    type = lt.type;
    num = lt.nval;
    str = lt.sval;
  }

  public Const(double num)
  {
    type = EnumLiteral.NUM;
    this.num = num;
    str = "";
  }

  public Const(string str)
  {
    type = EnumLiteral.STR;
    this.str = str;
    num = 0;
  }

  public Const(bool v)
  {
    type = EnumLiteral.BOOL;
    num = v ? 1 : 0;
    this.str = "";
  }

  public Val ToVal(VM vm)
  {
    if(type == EnumLiteral.NUM)
      return Val.NewNum(vm, num);
    else if(type == EnumLiteral.BOOL)
      return Val.NewBool(vm, num == 1);
    else if(type == EnumLiteral.STR)
      return Val.NewStr(vm, str);
    else if(type == EnumLiteral.NIL)
      return Val.NewNil(vm);
    else
      throw new Exception("Bad type");
  }

  public bool IsEqual(Const o)
  {
    return type == o.type && 
           num == o.num && 
           str == o.str;
  }
}

public class VM
{
  public const int MAX_IP = int.MaxValue;
  public const int STOP_IP = MAX_IP - 1;

  public struct FrameContext
  {
    public Frame frame;
    public bool is_call;
    public int min_ip;
    public int max_ip;

    public FrameContext(Frame frame, bool is_call, int min_ip = -1, int max_ip = MAX_IP)
    {
      this.frame = frame;
      this.is_call = is_call;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }

    public bool IsFuncCall()
    {
      return min_ip == -1 && max_ip == MAX_IP;
    }
  }

  public class Fiber
  {
    public VM vm;

    internal int id;
    internal int tick;

    internal int ip;
    internal ICoroutine coroutine;
    internal FixedStack<FrameContext> ctx_frames = new FixedStack<FrameContext>(256);

    public FixedStack<Val> stack = new FixedStack<Val>(32);
    
    public BHS status;

    static public Fiber New(VM vm)
    {
      Fiber fb;
      if(vm.fibers_pool.stack.Count == 0)
      {
        ++vm.fibers_pool.miss;
        fb = new Fiber(vm);
      }
      else
      {
        ++vm.fibers_pool.hit;
        fb = vm.fibers_pool.stack.Pop();
      }

      return fb;
    }

    static public void Del(Fiber fb)
    {
      fb.Clear();
      fb.vm.fibers_pool.stack.Push(fb);
    }

    //NOTE: use New() instead
    internal Fiber(VM vm)
    {
      this.vm = vm;
    }

    internal void Clear()
    {
      if(coroutine != null)
      {
        CoroutinePool.Del(ctx_frames.Peek().frame, coroutine);
        coroutine = null;
      }

      for(int i=ctx_frames.Count;i-- > 0;)
      {
        var frm = ctx_frames[i].frame;
        frm.ExitScope(frm);
        frm.Release();
      }
      ctx_frames.Clear();
      tick = 0;
    }

    public bool IsStopped()
    {
      return ip >= STOP_IP;
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      var calls = new List<VM.Frame>();
      for(int i=0;i<ctx_frames.Count;++i)
        if(ctx_frames[i].is_call)
          calls.Add(ctx_frames[i].frame);

      for(int i=0;i<calls.Count;++i)
      {
        var frm = calls[i];

        var item = new TraceItem(); 

        //NOTE: information about frame ip is taken from the 'next' frame, however 
        //      for the last frame we have a special case. In this case there's no
        //      'next' frame and we should consider taking ip from Fiber or an active
        //      coroutine
        if(i == calls.Count-1)
        {
          if(!TryGetCoroutineIP(coroutine, out item.ip))
            item.ip = frm.fb.ip;
        }
        else
        {
          //NOTE: retrieving last ip for the current Frame which 
          //      turns out to be return_ip assigned to the next Frame
          var next = calls[i+1];
          item.ip = next.return_ip;
        }

        if(frm.module != null)
        {
          item.file = frm.module.name + ".bhl";
          item.func = TraceItem.MapIp2Func(frm.module.name, calls[i].start_ip, frm.vm.func2addr);
          frm.module.ip2src_line.TryGetValue(item.ip, out item.line);
        }
        else
        {
          item.file = "?";
          item.func = "?";
        }

        info.Insert(0, item);
      }
    }

    static bool TryGetCoroutineIP(ICoroutine i, out int ip)
    {
      ip = 0;
      if(i is SeqBlock si)
      {
        if(!TryGetCoroutineIP(si.coroutine, out ip))
          ip = si.ip;
        return true;
      }
      else if(i is ParalBranchBlock bi)
      {
        if(!TryGetCoroutineIP(bi.coroutine, out ip))
          ip = bi.ip;
        return true;
      }
      else if(i is ParalBlock pi)
        return TryGetCoroutineIP(pi.branches[pi.i], out ip);
      else if(i is ParalAllBlock pai)
        return TryGetCoroutineIP(pai.branches[pai.i], out ip);
      else
        return false;
    }
  }

  public class Frame : IExitableScope
  {
    public const int MAX_LOCALS = 64;
    public const int MAX_STACK = 32;
    
    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    public VM vm;
    public Fiber fb;
    public CompiledModule module;

    public byte[] bytecode;
    public List<Const> constants;
    public FixedStack<Val> locals = new FixedStack<Val>(MAX_LOCALS);
    public FixedStack<Val> stack = new FixedStack<Val>(MAX_STACK);
    public int start_ip;
    public int return_ip;
    public List<DeferBlock> defers;

    static public Frame New(VM vm)
    {
      Frame frm;
      if(vm.frames_pool.stack.Count == 0)
      {
        ++vm.frames_pool.miss;
        frm = new Frame(vm);
      }
      else
      {
        ++vm.frames_pool.hit;
        frm = vm.frames_pool.stack.Pop();

        if(frm.refs != -1)
          throw new Exception("Expected to be released, refs " + frm.refs);
      }

      frm.refs = 1;

      return frm;
    }

    static void Del(Frame frm)
    {
      if(frm.refs != 0)
        throw new Exception("Freeing invalid object, refs " + frm.refs);

      //Console.WriteLine("DEL " + frm.GetHashCode() + " " + Environment.StackTrace);
      frm.refs = -1;

      frm.Clear();
      frm.vm.frames_pool.stack.Push(frm);
    }

    //NOTE: use New() instead
    internal Frame(VM vm)
    {
      this.vm = vm;
    }

    public void Init(Frame origin, int start_ip)
    {
      Init(
        origin.fb, 
        origin.module, 
        origin.constants, 
        origin.bytecode, 
        start_ip
      );
    }

    public void Init(Fiber fb, CompiledModule module, int start_ip)
    {
      Init(
        fb, 
        module, 
        module.constants, 
        module.bytecode, 
        start_ip
      );
    }

    internal void Init(Fiber fb, CompiledModule module, List<Const> constants, byte[] bytecode, int start_ip)
    {
      this.fb = fb;
      this.module = module;
      this.constants = constants;
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      this.return_ip = -1;
    }

    public void Clear()
    {
      for(int i=locals.Count;i-- > 0;)
      {
        var val = locals[i];
        if(val != null)
          val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      locals.Clear();

      for(int i=stack.Count;i-- > 0;)
      {
        var val = stack[i];
        val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      stack.Clear();
    }

    public void RegisterDefer(DeferBlock cb)
    {
      if(defers == null)
        defers = new List<DeferBlock>();
      defers.Add(cb);
    }

    public void ExitScope(VM.Frame frm)
    {
      DeferBlock.ExitScope(frm, defers, ref fb.ip);
    }

    public void Retain()
    {
      //Console.WriteLine("RTN " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

    public void Release()
    {
      //Console.WriteLine("REL " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      if(refs == 0)
        throw new Exception("Double free(0)");

      --refs;
      if(refs == 0)
        Del(this);
    }
  }

  public class FuncPtr : IValRefcounted
  {
    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    public VM vm;

    public CompiledModule module;
    public int func_ip;
    public FuncSymbolNative native;
    public FixedStack<Val> upvals = new FixedStack<Val>(Frame.MAX_LOCALS);

    static public FuncPtr New(VM vm)
    {
      FuncPtr ptr;
      if(vm.ptrs_pool.stack.Count == 0)
      {
        ++vm.ptrs_pool.miss;
        ptr = new FuncPtr(vm);
      }
      else
      {
        ++vm.ptrs_pool.hit;
        ptr = vm.ptrs_pool.stack.Pop();

        if(ptr.refs != -1)
          throw new Exception("Expected to be released, refs " + ptr.refs);
      }

      ptr.refs = 1;

      return ptr;
    }

    static void Del(FuncPtr ptr)
    {
      if(ptr.refs != 0)
        throw new Exception("Freeing invalid object, refs " + ptr.refs);

      //Console.WriteLine("DEL " + ptr.GetHashCode() + " " + Environment.StackTrace);
      ptr.refs = -1;

      ptr.Clear();
      ptr.vm.ptrs_pool.stack.Push(ptr);
    }

    //NOTE: use New() instead
    internal FuncPtr(VM vm)
    {
      this.vm = vm;
    }

    public void Init(Frame origin, int func_ip)
    {
      this.module = origin.module;
      this.func_ip = func_ip;
      this.native = null;
    }

    public void Init(CompiledModule module, int func_ip)
    {
      this.module = module;
      this.func_ip = func_ip;
      this.native = null;
    }

    public void Init(FuncSymbolNative native)
    {
      this.module = null;
      this.func_ip = -1;
      this.native = native;
    }

    void Clear()
    {
      this.module = null;
      this.func_ip = -1;
      this.native = null;
      for(int i=upvals.Count;i-- > 0;)
      {
        var val = upvals[i];
        if(val != null)
          val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      upvals.Clear();
    }

    public void Retain()
    {
      //Console.WriteLine("RTN " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

    public void Release()
    {
      //Console.WriteLine("REL " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      if(refs == 0)
        throw new Exception("Double free(0)");

      --refs;
      if(refs == 0)
        Del(this);
    }

    public Frame MakeFrame(VM vm, Frame curr_frame)
    {
      var fr = Frame.New(vm);
      if(module != null)
        fr.Init(curr_frame.fb, module, func_ip);
      else
        fr.Init(curr_frame, func_ip);

      for(int i=0;i<upvals.Count;++i)
      {
        var upval = upvals[i];
        if(upval != null)
        {
          fr.locals.Resize(i+1);
          upval.Retain();
          fr.locals[i] = upval;
        }
      }
      return fr;
    }
  }

  public struct TraceItem
  {
    public string file;
    public string func;
    public int line;
    public int ip; 

    static public string MapIp2Func(string module_name, int ip, Dictionary<string, ModuleAddr> func2addr)
    {
      foreach(var kv in func2addr)
      {
        if(kv.Value.module.name == module_name && kv.Value.ip == ip)
          return kv.Key;
      }
      return "?";
    }
  }

  public class Error : Exception
  {
    public List<TraceItem> trace;

    public Error(List<TraceItem> trace, Exception e)
      : base(ToString(trace), e)
    {
      this.trace = trace;
    }

    static public string ToString(List<TraceItem> trace)
    {
      string s = "\n";
      foreach(var t in trace)
        s += "at " + t.func + "(..) in " + t.file + ":" + t.line + ", ip " + t.ip + "\n";
      return s;
    }
  }

  public class Pool<T> where T : class
  {
    public Stack<T> stack = new Stack<T>();
    public int hit;
    public int miss;

    public int Allocs
    {
      get { return miss; }
    }

    public int Free
    {
      get { return stack.Count; }
    }
  }

  Dictionary<string, CompiledModule> modules = new Dictionary<string, CompiledModule>();

  GlobalScope globs;
  public GlobalScope Globs {
    get {
      return globs;
    }
  }

  Scope symbols;
  public Scope Symbols {
    get {
      return symbols;
    }
  }

  public struct ModuleAddr
  {
    public CompiledModule module;
    public int ip;
  }

  Dictionary<string, ModuleAddr> func2addr = new Dictionary<string, ModuleAddr>();

  int fibers_ids = 0;
  List<Fiber> fibers = new List<Fiber>();

  IModuleImporter importer;

  public delegate void ClassCreator(VM.Frame frm, ref Val res);
  public delegate void FieldGetter(Val v, ref Val res);
  public delegate void FieldSetter(ref Val v, Val nv);
  public delegate void FieldRef(Val v, out Val res);

  public class ValPool : Pool<Val>
  {
    //NOTE: used for debug tracking of not-freed Vals
    internal struct Tracking
    {
      internal Val v;
      internal string stack_trace;
    }
    internal List<Tracking> debug_track = new List<Tracking>();

    public void Alloc(VM vm, int num)
    {
      for(int i=0;i<num;++i)
      {
        ++miss;
        var tmp = new Val(vm); 
        stack.Push(tmp);
      }
    }

    public string Dump()
    {
      string res = "=== Val POOL ===\n";
      res += "total:" + Allocs + " free:" + Free + "\n";

      var dvs = new Val[stack.Count];
      stack.CopyTo(dvs, 0);
      for(int i=dvs.Length;i-- > 0;)
      {
        var v = dvs[i];
        res += v + " (refs:" + v._refs + ") " + v.GetHashCode() + "\n"; 
      }

      if(debug_track.Count > 0)
      {
        var dangling = new List<Tracking>();
        foreach(var t in debug_track)
          if(t.v._refs != -1)
            dangling.Add(t);

        res += "== dangling:" + dangling.Count + " ==\n";
        foreach(var t in dangling)
          res += t.v + " (refs:" + t.v._refs + ") " + t.v.GetHashCode() + "\n" + t.stack_trace + "\n<<<<<\n"; 
      }

      return res;
    }
  }

  public ValPool vals_pool = new ValPool();
  public Pool<ValList> vlsts_pool = new Pool<ValList>();
  public Pool<Frame> frames_pool = new Pool<Frame>();
  public Pool<Fiber> fibers_pool = new Pool<Fiber>();
  public Pool<FuncPtr> ptrs_pool = new Pool<FuncPtr>();
  public CoroutinePool coro_pool = new CoroutinePool();

  //fake frame used for module's init code
  Frame init_frame;

  public VM(GlobalScope globs = null, IModuleImporter importer = null)
  {
    if(globs == null)
      globs = TypeSystem.CreateBuiltins();
    this.globs = globs;
    this.importer = importer;
    symbols = new Scope(globs);

    init_frame = new Frame(this);
  }

  public void LoadModule(string module_name)
  {
    var imported_module = importer.Import(module_name);
    RegisterModule(imported_module);
  }

  public void RegisterModule(CompiledModule m)
  {
    if(modules.ContainsKey(m.name))
      return;
    modules.Add(m.name, m);

    ExecInit(m);
  }

  public void UnloadModule(string module_name)
  {
    CompiledModule m;
    if(!modules.TryGetValue(module_name, out m))
      return;

    //NOTE: we can't modify dictionary during traversal,
    //      for this reason we have to collect removed keys 
    //      into a seperate list and remove them below
    var keys_to_remove = new List<string>();
    foreach(var kv in func2addr)
      if(kv.Value.module.name == module_name)
        keys_to_remove.Add(kv.Key);

    foreach(var name in keys_to_remove)
      func2addr.Remove(name);

    for(int i=0;i<m.gvars.Count;++i)
    {
      var val = m.gvars[i];
      if(val != null)
        val.RefMod(RefOp.DEC | RefOp.USR_DEC);
    }
    m.gvars.Clear();

    modules.Remove(module_name);
  }

  public void UnloadModules()
  {
    var keys = new List<string>();
    foreach(var kv in modules)
      keys.Add(kv.Key);
    foreach(string key in keys)
      UnloadModule(key);
  }

  void ExecInit(CompiledModule module)
  {
    var bytecode = module.initcode;
    var constants = module.constants;
    var stack = init_frame.stack;

    int ip = 0;
    AST_ClassDecl curr_class_decl = null;

    while(ip < bytecode.Length)
    {
      var opcode = (Opcodes)bytecode[ip];
      switch(opcode)
      {
        case Opcodes.Import:
        {
          int module_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          string module_name = constants[module_idx].str;

          LoadModule(module_name);
        }
        break;
        case Opcodes.DeclVar:
        {
          int var_idx = (int)Bytecode.Decode8(bytecode, ref ip);
          byte type = (byte)Bytecode.Decode8(bytecode, ref ip);

          module.gvars.Resize(var_idx+1);
          module.gvars[var_idx] = MakeValByType(type);
        }
        break;
        case Opcodes.SetVar:
        {
          int var_idx = (int)Bytecode.Decode8(bytecode, ref ip);

          module.gvars.Resize(var_idx+1);
          var new_val = init_frame.stack.Pop();
          module.gvars.Assign(this, var_idx, new_val);
          new_val.Release();
        }
        break;
        case Opcodes.Constant:
        {
          int const_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          var cn = constants[const_idx];
          stack.Push(cn.ToVal(this));
        }
        break;
        case Opcodes.New:
        {
          int type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          string type = constants[type_idx].str;
          HandleNew(init_frame, type);
        }
        break;
        case Opcodes.SetAttrInplace:
        {
          int class_type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          string class_type = constants[class_type_idx].str;
          int fld_idx = (int)Bytecode.Decode16(bytecode, ref ip);
          var class_symb = symbols.Resolve(class_type) as ClassSymbol;
          //TODO: this check must be in dev.version only
          if(class_symb == null)
            throw new Exception("Class type not found: " + class_type);

          var val = stack.Pop();
          var obj = stack.Peek();
          var field_symb = (FieldSymbol)class_symb.members[fld_idx];
          field_symb.setter(ref obj, val);
          val.Release();
        }
        break;
        //TODO: it's used for array init, maybe it should not be here
        case Opcodes.CallMethodNative:
        {
          int func_idx = (int)Bytecode.Decode16(bytecode, ref ip);
          int class_type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          uint args_bits = Bytecode.Decode32(bytecode, ref ip); 

          string class_type = constants[class_type_idx].str; 
          var class_symb = (ClassSymbol)symbols.Resolve(class_type);

          BHS status;
          ICoroutine coroutine = null;
          CallNative(init_frame, (FuncSymbolNative)class_symb.members[func_idx], args_bits, out status, ref coroutine);
        }
        break;
        case Opcodes.Func:
        {
          int name_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          int ip_addr = (int)Bytecode.Decode24(bytecode, ref ip);

          //TODO: define symbol as well?
          var fdecl = new AST_FuncDecl();
          fdecl.name = constants[name_idx].str;
          fdecl.ip_addr = ip_addr;
          if(curr_class_decl == null)
            func2addr.Add(fdecl.name, new ModuleAddr() { module = module, ip = ip_addr });
          else
            curr_class_decl.children.Add(fdecl);
        }
        break;
        case Opcodes.ClassBegin:
        {
          int type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          int parent_type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          curr_class_decl = new AST_ClassDecl();
          curr_class_decl.name = constants[type_idx].str;
          if(parent_type_idx != -1)
            curr_class_decl.parent = constants[parent_type_idx].str;
        }
        break;
        case Opcodes.ClassMember:
        {
          int type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          int name_idx = (int)Bytecode.Decode32(bytecode, ref ip);

          var mdecl = new AST_VarDecl();
          mdecl.type = constants[type_idx].str;
          mdecl.name = constants[name_idx].str;
          mdecl.symb_idx = (uint)curr_class_decl.children.Count;
          curr_class_decl.children.Add(mdecl);
        }
        break;
        case Opcodes.ClassEnd:
        {
          //TODO: add parent support
          ClassSymbolScript super_class = null;
          var curr_class = new ClassSymbolScript(curr_class_decl.name, curr_class_decl, super_class);
          for(int i=0;i<curr_class_decl.children.Count;++i)
          {
            var child = curr_class_decl.children[i];
            if(child is AST_VarDecl vd)
              curr_class.Define(new FieldSymbolScript(vd.name, vd.type, (int)vd.symb_idx));
            else if(child is AST_FuncDecl fd)
              curr_class.Define(new FuncSymbolScript(fd));
          }
          symbols.Define(curr_class);
          curr_class = null;
          curr_class_decl = null;
        }
        break;
        default:
          throw new Exception("Not supported opcode: " + opcode);
      }
      ++ip;
    }
  }

  public Fiber Start(string func, params Val[] args)
  {
    return Start(func, 0, args);
  }

  public Fiber Start(string func, FuncArgsInfo args_info, params Val[] args)
  {
    return Start(func, args_info.bits, args);
  }

  public Fiber Start(string func, uint cargs_bits, params Val[] args)
  {
    ModuleAddr addr;
    if(!func2addr.TryGetValue(func, out addr))
      return null;

    var fb = Fiber.New(this);
    Register(fb);

    var frame = Frame.New(this);
    frame.Init(fb, addr.module, addr.ip);

    for(int i=args.Length;i-- > 0;)
    {
      var arg = args[i];
      frame.stack.Push(arg);
    }
    //cargs bits
    frame.stack.Push(Val.NewNum(this, cargs_bits));

    Attach(fb, frame);

    return fb;
  }

  //NOTE: adding special bytecode which makes the fake Frame to exit
  //      after executing the coroutine: the first opcode is used
  //      if execution doesn't produce a stateful coroutine,
  //      and the second one if it does (this is how VM works)
  static byte[] RETURN_BYTES = new byte[] {(byte)Opcodes.Return, (byte)Opcodes.Return};

  public Fiber Start(FuncPtr ptr, Frame curr_frame)
  {
    uint cargs_bits = 0;

    var fb = Fiber.New(this);
    Register(fb);

    //checking native call
    if(ptr.native != null)
    {
      //let's create a fake frame for a native call
      var frame = Frame.New(this);
      frame.Init(fb, null, null, RETURN_BYTES, 0);
      Attach(fb, frame);
      fb.coroutine = ptr.native.cb(curr_frame, new FuncArgsInfo(cargs_bits), ref fb.status);
    }
    else
    {
      var frame = ptr.MakeFrame(this, curr_frame);
      Attach(fb, frame);
      //cargs bits
      frame.stack.Push(Val.NewNum(this, cargs_bits));
    }

    return fb;
  }

  public void Detach(Fiber fb)
  {
    fibers.Remove(fb);
  }

  void Attach(Fiber fb, Frame fr)
  {
    fb.ip = fr.start_ip;
    fr.fb = fb;
    fb.ctx_frames.Push(new FrameContext(fr, true));
  }

  void Register(Fiber fb)
  {
    fb.id = ++fibers_ids;
    fibers.Add(fb);
  }

  public void Stop(Fiber fb)
  {
    if(fb.IsStopped())
      return;

    Fiber.Del(fb);
    //NOTE: we assing Fiber ip to a special value which is just one value before MAX_IP,
    //      this way Fiber breaks its current Frame execution loop.
    fb.ip = STOP_IP;
  }

  public void Stop(int fid)
  {
    var fb = FindFiber(fid);
    if(fb == null)
      return;
    Stop(fb);
  }

  public void Stop()
  {
    for(int i=fibers.Count;i-- > 0;)
    {
      Stop(fibers[i]);
      fibers.RemoveAt(i);
    }
  }

  Fiber FindFiber(int fid)
  {
    for(int i=0;i<fibers.Count;++i)
      if(fibers[i].id == fid)
        return fibers[i];
    return null;
  }

  public void GetStackTrace(Dictionary<Fiber, List<TraceItem>> info)
  {
    for(int i=0;i<fibers.Count;++i)
    {
      var fb = fibers[i];
      var trace = new List<TraceItem>();
      fb.GetStackTrace(trace);
      info[fb] = trace;
    }
  }

  internal BHS Execute(
    ref int ip,
    FixedStack<FrameContext> ctx_frames, 
    ref ICoroutine coroutine, 
    IExitableScope defer_scope,
    int frames_limit = 0
  )
  {
    var status = BHS.SUCCESS;
    IExitableScope tmp_defer_scope = null;
    int init_ctx_num = ctx_frames.Count;
    int tmp_ctx_num = 0;
    while((tmp_ctx_num = ctx_frames.Count) > frames_limit && status == BHS.SUCCESS)
    {
      //NOTE: we need to restore the original defer scope
      //      once we pop all frames which were generated during execution 
      if(tmp_ctx_num == init_ctx_num)
        tmp_defer_scope = defer_scope;
      status = ExecuteOnce(
        ref ip, ctx_frames,
        ref coroutine,
        ref tmp_defer_scope
      );
    }
    return status;
  }

  BHS ExecuteOnce(
    ref int ip, 
    FixedStack<FrameContext> frames, 
    ref ICoroutine coroutine, 
    ref IExitableScope defer_scope
  )
  { 
    var ctx = frames.Peek();

    if(ip <= ctx.min_ip || ip >= ctx.max_ip)
    {
      frames.Pop();
      return BHS.SUCCESS;
    }

    var curr_frame = ctx.frame;

    //Console.WriteLine("EXEC TICK " + curr_frame.fb.tick + " (" + curr_frame.fb.id + ") IP " + ip + "(min:" + ctx.min_ip + ", max:" + ctx.max_ip + ")" +  " OP " + (Opcodes)curr_frame.bytecode[ip] + " CORO " + coroutine?.GetType().Name + "(" + coroutine?.GetHashCode() + ")" + " SCOPE " + defer_scope?.GetType().Name + "(" + defer_scope?.GetHashCode() + ")"/* + " " + Environment.StackTrace*/);

    //NOTE: if there's an active coroutine it has priority over simple 'code following' via ip
    if(coroutine != null)
      return ExecuteCoroutine(ref ip, ref coroutine, curr_frame, frames);

    var opcode = (Opcodes)curr_frame.bytecode[ip];
    //Console.WriteLine("OP " + opcode + " " + ip);
    switch(opcode)
    {
      case Opcodes.Constant:
      {
        int const_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var cn = curr_frame.constants[const_idx];
        curr_frame.stack.Push(cn.ToVal(this));
      }
      break;
      case Opcodes.TypeCast:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string cast_type = curr_frame.constants[cast_type_idx].str;

        HandleTypeCast(curr_frame, cast_type);
      }
      break;
      case Opcodes.Inc:
      {
        int var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        ++curr_frame.locals[var_idx]._num;
      }
      break;
      case Opcodes.Dec:
      {
        int var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        --curr_frame.locals[var_idx]._num;
      }
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
      case Opcodes.LT:
      case Opcodes.LTE:
      case Opcodes.GT:
      case Opcodes.GTE:
      {
        ExecuteBinaryOp(opcode, curr_frame);
      }
      break;
      case Opcodes.UnaryNot:
      case Opcodes.UnaryNeg:
      {
        ExecuteUnaryOp(opcode, curr_frame);
      }
      break;
      case Opcodes.GetVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        curr_frame.stack.PushRetain(curr_frame.locals[local_idx]);
      }
      break;
      case Opcodes.SetVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        var new_val = curr_frame.stack.Pop();
        curr_frame.locals.Assign(this, local_idx, new_val);
        new_val.Release();
      }
      break;
      case Opcodes.ArgVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        var arg_val = curr_frame.stack.Pop();
        var loc_var = Val.New(this);
        loc_var.ValueCopyFrom(arg_val);
        loc_var.RefMod(RefOp.USR_INC);
        curr_frame.locals[local_idx] = loc_var;
        arg_val.Release();
      }
      break;
      case Opcodes.ArgRef:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        curr_frame.locals[local_idx] = curr_frame.stack.Pop();
      }
      break;
      case Opcodes.DeclVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        byte type = (byte)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        var curr = curr_frame.locals[local_idx];
        //NOTE: handling case when variables are 're-declared' within the nested loop
        if(curr != null)
          curr.Release();
        curr_frame.locals[local_idx] = MakeValByType(type);
      }
      break;
      case Opcodes.GetAttr:
      {
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string class_type = curr_frame.constants[class_type_idx].str;
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var class_symb = (ClassSymbol)symbols.Resolve(class_type);
        var obj = curr_frame.stack.Pop();
        var res = Val.New(this);
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.getter(obj, ref res);
        //NOTE: we retain only the payload since we make the copy of the value 
        //      and the new res already has refs = 1 while payload's refcount 
        //      is not incremented
        res.RefMod(RefOp.USR_INC);
        curr_frame.stack.Push(res);
        obj.Release();
      }
      break;
      case Opcodes.RefAttr:
      {
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string class_type = curr_frame.constants[class_type_idx].str;
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var class_symb = symbols.Resolve(class_type) as ClassSymbol;
        //TODO: this check must be in dev.version only
        if(class_symb == null)
          throw new Exception("Class type not found: " + class_type);

        var obj = curr_frame.stack.Pop();
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        Val res;
        field_symb.getref(obj, out res);
        curr_frame.stack.PushRetain(res);
        obj.Release();
      }
      break;
      case Opcodes.SetAttr:
      {
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string class_type = curr_frame.constants[class_type_idx].str;
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var class_symb = symbols.Resolve(class_type) as ClassSymbol;
        //TODO: this check must be in dev.version only
        if(class_symb == null)
          throw new Exception("Class type not found: " + class_type);

        var obj = curr_frame.stack.Pop();
        var val = curr_frame.stack.Pop();
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.setter(ref obj, val);
        val.Release();
        obj.Release();
      }
      break;
      case Opcodes.SetAttrInplace:
      {
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string class_type = curr_frame.constants[class_type_idx].str;
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var class_symb = symbols.Resolve(class_type) as ClassSymbol;
        //TODO: this check must be in dev.version only
        if(class_symb == null)
          throw new Exception("Class type not found: " + class_type);

        var val = curr_frame.stack.Pop();
        var obj = curr_frame.stack.Peek();
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.setter(ref obj, val);
        val.Release();
      }
      break;
      case Opcodes.GetGVar:
      {
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        curr_frame.stack.PushRetain(curr_frame.module.gvars[var_idx]);
      }
      break;
      case Opcodes.GetGVarImported:
      {
        int module_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        string module_name = curr_frame.constants[module_idx].str;
        var module = curr_frame.vm.modules[module_name];
        curr_frame.stack.PushRetain(module.gvars[var_idx]);
      }
      break;
      case Opcodes.SetGVar:
      {
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        var new_val = curr_frame.stack.Pop();
        curr_frame.module.gvars.Assign(this, var_idx, new_val);
        new_val.Release();
      }
      break;
      case Opcodes.SetGVarImported:
      {
        int module_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        string module_name = curr_frame.constants[module_idx].str;
        var module = curr_frame.vm.modules[module_name];
        var new_val = curr_frame.stack.Pop();
        module.gvars.Assign(this, var_idx, new_val);
        new_val.Release();
      }
      break;
      case Opcodes.Return:
      {
        curr_frame.ExitScope(curr_frame);
        ip = curr_frame.return_ip;
        curr_frame.Clear();
        curr_frame.Release();
        //Console.WriteLine("RET IP " + ip + " FRAMES " + frames.Count);
        frames.Pop();
        if(frames.Count > 0)
          defer_scope = frames.Peek().frame;
      }
      break;
      case Opcodes.ReturnVal:
      {
        int ret_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

        var ret_stack = frames.Count == 1 ? curr_frame.fb.stack : frames[frames.Count-2].frame.stack;  
        //TODO: make it more efficient?
        int stack_offset = curr_frame.stack.Count; 
        for(int i=0;i<ret_num;++i)
        {
          ret_stack.Push(curr_frame.stack[stack_offset-ret_num+i]);
          curr_frame.stack.Dec();
        }

        ip = curr_frame.return_ip;
        curr_frame.ExitScope(curr_frame);
        curr_frame.Clear();
        curr_frame.Release();
        frames.Pop();
        if(frames.Count > 0)
          defer_scope = frames.Peek().frame;
      }
      break;
      case Opcodes.GetFunc:
      {
        int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var ptr = FuncPtr.New(this);
        ptr.Init(curr_frame, func_ip);
        curr_frame.stack.Push(Val.NewObj(this, ptr));
      }
      break;
      case Opcodes.GetFuncNative:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var func_symb = (FuncSymbolNative)globs.GetMembers()[func_idx];
        var ptr = FuncPtr.New(this);
        ptr.Init(func_symb);
        curr_frame.stack.Push(Val.NewObj(this, ptr));
      }
      break;
      case Opcodes.GetFuncImported:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        string func_name = curr_frame.constants[func_idx].str;
        var maddr = func2addr[func_name];

        var ptr = FuncPtr.New(this);
        ptr.Init(maddr.module, maddr.ip);
        curr_frame.stack.Push(Val.NewObj(this, ptr));
      }
      break;
      case Opcodes.GetFuncFromVar:
      {
        int local_var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        var val = curr_frame.locals[local_var_idx];
        val.Retain();
        curr_frame.stack.Push(val);
      }
      break;
      case Opcodes.FuncPtrToTop:
      {
        //NOTE: we need to move ptr to the top of the stack
        //      so that it fullfills Opcode.Call requirements 
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 
        var args_info = new FuncArgsInfo(args_bits);
        int ptr_idx = curr_frame.stack.Count-args_info.CountArgs()-1; 
        var ptr = curr_frame.stack[ptr_idx];
        curr_frame.stack.RemoveAt(ptr_idx);
        curr_frame.stack.Push(ptr);
      }
      break;
      case Opcodes.Call:
      {
        int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip); 
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        var fr = Frame.New(this);
        fr.Init(curr_frame, func_ip);
        Call(curr_frame, frames, fr, args_bits, ref ip, ref defer_scope);
      }
      break;
      case Opcodes.CallNative:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        var native = (FuncSymbolNative)globs.GetMembers()[func_idx];

        BHS status;
        if(CallNative(curr_frame, native, args_bits, out status, ref coroutine))
          return status;
      }
      break;
      case Opcodes.CallImported:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        string func_name = curr_frame.constants[func_idx].str;
        var maddr = func2addr[func_name];

        var fr = Frame.New(this);
        fr.Init(curr_frame.fb, maddr.module, maddr.ip);
        Call(curr_frame, frames, fr, args_bits, ref ip, ref defer_scope);
      }
      break;
      case Opcodes.CallMethodNative:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        string class_type = curr_frame.constants[class_type_idx].str; 
        var class_symb = (ClassSymbol)symbols.Resolve(class_type);

        BHS status;
        if(CallNative(curr_frame, (FuncSymbolNative)class_symb.members[func_idx], args_bits, out status, ref coroutine))
          return status;
      }
      break;
      case Opcodes.CallMethod:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        string class_type = curr_frame.constants[class_type_idx].str; 
        var class_symb = (ClassSymbol)symbols.Resolve(class_type);

        var field_symb = (FuncSymbolScript)class_symb.members[func_idx];
        int func_ip = field_symb.decl.ip_addr;

        var self = curr_frame.stack.Pop();

        var fr = Frame.New(this);
        fr.Init(curr_frame, func_ip);

        fr.locals[0] = self;

        Call(curr_frame, frames, fr, args_bits, ref ip, ref defer_scope);
      }
      break;
      case Opcodes.CallPtr:
      {
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        var val_ptr = curr_frame.stack.Pop();
        var ptr = (FuncPtr)val_ptr._obj;

        //checking native call
        if(ptr.native != null)
        {
          BHS status;
          bool return_status = CallNative(curr_frame, ptr.native, args_bits, out status, ref coroutine);
          val_ptr.Release();
          if(return_status)
            return status;
        }
        else
        {
          var fr = ptr.MakeFrame(this, curr_frame);
          val_ptr.Release();
          Call(curr_frame, frames, fr, args_bits, ref ip, ref defer_scope);
        }
      }
      break;
      case Opcodes.InitFrame:
      {
        int local_vars_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        var args_bits = curr_frame.stack.Pop(); 
        curr_frame.locals.Resize(local_vars_num);
        //NOTE: we need to store arg info bits locally so that
        //      this information will be available to func 
        //      args related opcodes
        curr_frame.locals[local_vars_num-1] = args_bits;
      }
      break;
      case Opcodes.Lambda:
      {             
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var ptr = FuncPtr.New(this);
        ptr.Init(curr_frame, ip+1);
        curr_frame.stack.Push(Val.NewObj(this, ptr));

        ip += offset;
      }
      break;
      case Opcodes.UseUpval:
      {
        int up_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

        var addr = (FuncPtr)curr_frame.stack.Peek()._obj;

        //TODO: amount of local variables must be known ahead and
        //      initialized during Frame initialization
        //NOTE: we need to reflect the updated max amount of locals,
        //      otherwise they might not be cleared upon Frame exit
        addr.upvals.Resize(local_idx+1);

        var upval = curr_frame.locals[up_idx];
        upval.Retain();
        addr.upvals[local_idx] = upval;

      }
      break;
      case Opcodes.Pop:
      {
        curr_frame.stack.PopRelease();
      }
      break;
      case Opcodes.Jump:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        ip += offset;
      }
      break;
      case Opcodes.JumpZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref ip);
        if(curr_frame.stack.PopRelease().bval == false)
          ip += offset;
      }
      break;
      case Opcodes.JumpPeekZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var v = curr_frame.stack.Peek();
        if(v.bval == false)
          ip += offset;
      }
      break;
      case Opcodes.JumpPeekNZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var v = curr_frame.stack.Peek();
        if(v.bval == true)
          ip += offset;
      }
      break;
      case Opcodes.Break:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        ip += offset;
        curr_frame.fb.ip = ip;
      }
      break;
      case Opcodes.Continue:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        ip += offset;
        curr_frame.fb.ip = ip;
      }
      break;
      case Opcodes.DefArg:
      {
        byte def_arg_idx = (byte)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        int jump_pos = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var args_info = new FuncArgsInfo((uint)curr_frame.locals[curr_frame.locals.Count-1]._num);
        //Console.WriteLine("DEF ARG: " + def_arg_idx + ", jump pos " + jump_pos + ", used " + args_info.IsDefaultArgUsed(def_arg_idx));
        //NOTE: if default argument is not used we need to jump out of default argument calculation code
        if(!args_info.IsDefaultArgUsed(def_arg_idx))
          ip += jump_pos;
      }
      break;
      case Opcodes.Block:
      {
        var new_coroutine = VisitBlock(ref ip, curr_frame, defer_scope);
        if(new_coroutine != null)
        {
          //NOTE: since there's a new coroutine we want to skip ip incrementing
          //      which happens below and proceed right to the execution of 
          //      the new coroutine
          coroutine = new_coroutine;
          return BHS.SUCCESS;
        }
      }
      break;
      case Opcodes.New:
      {
        int type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        string type = curr_frame.constants[type_idx].str;
        HandleNew(curr_frame, type);
      }
      break;
      default:
        throw new Exception("Not supported opcode: " + opcode);
    }

    ++ip;
    return BHS.SUCCESS;
  }

  Val MakeValByType(byte type)
  {
    if(type == Val.NUMBER)
      return Val.NewNum(this, 0);
    else if(type == Val.STRING)
      return Val.NewStr(this, "");
    else if(type == Val.BOOL)
      return Val.NewBool(this, false);
    else
      return Val.NewObj(this, null);
  }

  void Call(Frame curr_frame, FixedStack<FrameContext> ctx_frames, Frame new_frame, uint args_bits, ref int ip, ref IExitableScope defer_scope)
  {
    var args_info = new FuncArgsInfo(args_bits);
    for(int i = 0; i < args_info.CountArgs(); ++i)
      new_frame.stack.Push(curr_frame.stack.Pop());
    new_frame.stack.Push(Val.NewNum(this, args_bits));

    //let's remember ip to return to
    new_frame.return_ip = ip;
    ctx_frames.Push(new FrameContext(new_frame, true));
    //since ip will be incremented below we decrement it intentionally here
    ip = new_frame.start_ip - 1; 

    defer_scope = new_frame;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  static bool CallNative(Frame curr_frame, FuncSymbolNative native, uint args_bits, out BHS status, ref ICoroutine coroutine)
  {
    var args_info = new FuncArgsInfo(args_bits);
    for(int i = 0; i < args_info.CountArgs(); ++i)
      curr_frame.stack.Push(curr_frame.stack.Pop());

    status = BHS.SUCCESS;
    var new_coroutine = native.cb(curr_frame, args_info, ref status);

    if(new_coroutine != null)
    {
      //NOTE: since there's a new coroutine we want to skip ip incrementing
      //      which happens below and proceed right to the execution of 
      //      the new coroutine
      coroutine = new_coroutine;
      return true;
    }
    else if(status != BHS.SUCCESS)
      return true;
    else 
      return false;
  }

  static BHS ExecuteCoroutine(
    ref int ip, 
    ref ICoroutine coroutine, 
    Frame curr_frame,
    FixedStack<FrameContext> ctx_frames
  )
  {
    var status = BHS.SUCCESS;
    coroutine.Tick(curr_frame, ref status);

    if(status == BHS.RUNNING)
      return status;
    else if(status == BHS.FAILURE)
    {
      CoroutinePool.Del(curr_frame, coroutine);
      coroutine = null;

      curr_frame.ExitScope(curr_frame);
      ip = curr_frame.return_ip;
      curr_frame.Release();
      ctx_frames.Pop();

      return status;
    }
    else if(status == BHS.SUCCESS)
    {
      CoroutinePool.Del(curr_frame, coroutine);
      coroutine = null;
      
      //NOTE: after coroutine successful execution we might be in a situation  
      //      that coroutine has already exited the current frame (e.g. after 'return')
      //      and it's released, for example in the following case:
      //
      //      paral {
      //         seq {
      //           return
      //         }
      //         seq {
      //          ...
      //         }
      //      }
      //
      //    ...after 'return' is executed the current frame will be in the released state 
      //    and we should take this into account
      //
      if(curr_frame.refs == -1)
        ctx_frames.Pop();

      //NOTE: we must increment the ip upon coroutine completion
      ++ip;

      return status;
    }
    else
      throw new Exception("Bad status: " + status);
  }

  //TODO: make it more universal and robust
  void HandleTypeCast(Frame curr_frame, string cast_type)
  {
    var new_val = Val.New(this);
    var val = curr_frame.stack.PopRelease();

    if(cast_type == "int")
      new_val.SetNum((int)val.num);
    else if(cast_type == "string" && val.type != Val.STRING)
      new_val.SetStr(val.num.ToString());
    else
    {
      new_val.ValueCopyFrom(val);
      new_val.RefMod(RefOp.USR_INC);
    }

    curr_frame.stack.Push(new_val);
  }

  void HandleNew(Frame curr_frame, string class_type)
  {
    var cls = symbols.Resolve(class_type) as ClassSymbol;
    //TODO: this check must be in dev.version only
    if(cls == null)
      throw new Exception("Could not find class symbol: " + class_type);

    var val = Val.New(this); 
    cls.creator(curr_frame, ref val);
    curr_frame.stack.Push(val);
  }

  static void ReadBlockHeader(ref int ip, Frame curr_frame, out EnumBlock type, out int size)
  {
    type = (EnumBlock)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
  }

  ICoroutine TryMakeBlockCoroutine(ref int ip, Frame curr_frame, out int size, IExitableScope defer_scope)
  {
    EnumBlock type;
    ReadBlockHeader(ref ip, curr_frame, out type, out size);

    if(type == EnumBlock.PARAL)
    {
      var paral = CoroutinePool.New<ParalBlock>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == EnumBlock.PARAL_ALL) 
    {
      var paral = CoroutinePool.New<ParalAllBlock>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == EnumBlock.SEQ)
    {
      if(defer_scope is IBranchyCoroutine)
      {
        var br = CoroutinePool.New<ParalBranchBlock>(this);
        br.Init(curr_frame, ip + 1, ip + size);
        return br;
      }
      else
      {
        var seq = CoroutinePool.New<SeqBlock>(this);
        seq.Init(curr_frame, ip + 1, ip + size);
        return seq;
      }
    }
    else if(type == EnumBlock.DEFER)
    {
      var d = new DeferBlock(curr_frame, ip + 1, ip + size);
      defer_scope.RegisterDefer(d);
      //we need to skip defer block
      //Console.WriteLine("DEFER SKIP " + ip + " " + (ip+size) + " " + Environment.StackTrace);
      ip += size;
      return null;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  ICoroutine VisitBlock(ref int ip, Frame curr_frame, IExitableScope defer_scope)
  {
    int block_size;
    var block_coro = TryMakeBlockCoroutine(ref ip, curr_frame, out block_size, defer_scope);

    //Console.WriteLine("BLOCK CORO " + block_coro?.GetType().Name);
    if(block_coro is IBranchyCoroutine bi) 
    {
      int tmp_ip = ip;
      while(tmp_ip < (ip + block_size))
      {
        ++tmp_ip;

        int tmp_size;
        var branch = TryMakeBlockCoroutine(ref tmp_ip, curr_frame, out tmp_size, (IExitableScope)block_coro);

       //Console.WriteLine("BRANCH INST " + tmp_ip + " " + branch?.GetType().Name);

        //NOTE: branch == null is a special case for defer {..} block
        if(branch != null)
        {
          bi.Attach(branch);
          tmp_ip += tmp_size;
        }
      }
    }
    return block_coro; 
  }

  public bool Tick()
  {
    return Tick(fibers);
  }

  public bool Tick(List<Fiber> fibers)
  {
    for(int i=0;i<fibers.Count;++i)
    {
      var fb = fibers[i];

      //let's check if this Fiber was already stopped
      if(fb.IsStopped())
        continue;

      try
      {
        ++fb.tick;
        fb.status = Execute(
          ref fb.ip, fb.ctx_frames, 
          ref fb.coroutine, 
          fb.ctx_frames[0].frame
        );
        
        if(fb.status != BHS.RUNNING)
          Stop(fb);
      }
      catch(Exception e)
      {
        var trace = new List<VM.TraceItem>();
        fb.GetStackTrace(trace);
        throw new Error(trace, e); 
      }
    }

    for(int i=fibers.Count;i-- > 0;)
    {
      var fb = fibers[i];
      if(fb.IsStopped())
        fibers.RemoveAt(i);
    }

    return fibers.Count != 0;
  }

  void ExecuteUnaryOp(Opcodes op, Frame curr_frame)
  {
    var operand = curr_frame.stack.PopRelease().num;
    switch(op)
    {
      case Opcodes.UnaryNot:
        curr_frame.stack.Push(Val.NewBool(this, operand != 1));
      break;
      case Opcodes.UnaryNeg:
        curr_frame.stack.Push(Val.NewNum(this, operand * -1));
      break;
    }
  }

  void ExecuteBinaryOp(Opcodes op, Frame curr_frame)
  {
    var r_operand = curr_frame.stack.Pop();
    var l_operand = curr_frame.stack.Pop();

    switch(op)
    {
      case Opcodes.Add:
        //TODO: add Opcodes.Concat?
        if((r_operand._type == Val.STRING) && (l_operand._type == Val.STRING))
          curr_frame.stack.Push(Val.NewStr(this, (string)l_operand._obj + (string)r_operand._obj));
        else
          curr_frame.stack.Push(Val.NewNum(this, l_operand._num + r_operand._num));
      break;
      case Opcodes.Sub:
        curr_frame.stack.Push(Val.NewNum(this, l_operand._num - r_operand._num));
      break;
      case Opcodes.Div:
        curr_frame.stack.Push(Val.NewNum(this, l_operand._num / r_operand._num));
      break;
      case Opcodes.Mul:
        curr_frame.stack.Push(Val.NewNum(this, l_operand._num * r_operand._num));
      break;
      case Opcodes.Equal:
        curr_frame.stack.Push(Val.NewBool(this, l_operand.IsValueEqual(r_operand)));
      break;
      case Opcodes.NotEqual:
        curr_frame.stack.Push(Val.NewBool(this, !l_operand.IsValueEqual(r_operand)));
      break;
      case Opcodes.LT:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num < r_operand._num));
      break;
      case Opcodes.LTE:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num <= r_operand._num));
      break;
      case Opcodes.GT:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num > r_operand._num));
      break;
      case Opcodes.GTE:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num >= r_operand._num));
      break;
      case Opcodes.And:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num == 1 && r_operand._num == 1));
      break;
      case Opcodes.Or:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num == 1 || r_operand._num == 1));
      break;
      case Opcodes.BitAnd:
        curr_frame.stack.Push(Val.NewNum(this, (int)l_operand._num & (int)r_operand._num));
      break;
      case Opcodes.BitOr:
        curr_frame.stack.Push(Val.NewNum(this, (int)l_operand._num | (int)r_operand._num));
      break;
      case Opcodes.Mod:
        curr_frame.stack.Push(Val.NewNum(this, (int)l_operand._num % (int)r_operand._num));
      break;
    }

    r_operand.Release();
    l_operand.Release();
  }
}

public class CompiledModule
{
  public const int MAX_GLOBALS = 128;

  public string name;
  public byte[] initcode;
  public byte[] bytecode;
  public List<Const> constants;
  public FixedStack<Val> gvars = new FixedStack<Val>(MAX_GLOBALS);
  public Dictionary<int, int> ip2src_line;

  public CompiledModule(
    string name,
    byte[] bytecode, 
    List<Const> constants, 
    byte[] initcode,
    Dictionary<int, int> ip2src_line = null
  )
  {
    this.name = name;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.constants = constants;
    this.ip2src_line = ip2src_line;
  }
}

public interface ICoroutine
{
  void Tick(VM.Frame frm, ref BHS status);
  void Cleanup(VM.Frame frm);
}

public class CoroutinePool
{
  Dictionary<System.Type, VM.Pool<ICoroutine>> all = new Dictionary<System.Type, VM.Pool<ICoroutine>>(); 

  static public T New<T>(VM vm) where T : ICoroutine, new()
  {
    var t = typeof(T); 
    VM.Pool<ICoroutine> pool;
    if(!vm.coro_pool.all.TryGetValue(t, out pool))
    {
      pool = new VM.Pool<ICoroutine>();
      vm.coro_pool.all.Add(t, pool);
    }

    ICoroutine coro = null;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      coro = new T();
    }
    else
    {
      ++pool.hit;
      coro = pool.stack.Pop();
    }

    //Console.WriteLine("NEW " + typeof(T).Name + " " + coro.GetHashCode()/* + " " + Environment.StackTrace*/);

    return (T)coro;
  }

  static public void Del(VM.Frame frm, ICoroutine coro)
  {
    //Console.WriteLine("DEL " + coro.GetType().Name + " " + coro.GetHashCode()/* + " " + Environment.StackTrace*/);

    coro.Cleanup(frm);

    var t = coro.GetType();

    VM.Pool<ICoroutine> pool;
    //ignoring coroutine whch were not allocated via pool 
    if(!frm.vm.coro_pool.all.TryGetValue(t, out pool))
      return;

    pool.stack.Push(coro);

    if(pool.stack.Count > pool.miss)
      throw new Exception("Unbalanced New/Del " + pool.stack.Count + " " + pool.miss);
  }

  static public void Dump(ICoroutine coro, int level = 0)
  {
    if(level == 0)
      Console.WriteLine("<<<<<<<<<<<<<<<");

    string str = new String(' ', level);
    Console.WriteLine(str + coro.GetType().Name + " " + coro.GetHashCode());

    if(coro is IInspectableCoroutine ti)
    {
      for(int i=0;i<ti.Count;++i)
        Dump(ti.At(i), level + 1);
    }

    if(level == 0)
      Console.WriteLine(">>>>>>>>>>>>>>>");
  }

  public int Allocs
  {
    get {
      int total = 0;
      foreach(var kv in all) 
        total += kv.Value.Allocs;
      return total;
    }
  }

  public int Free
  {
    get {
      int total = 0;
      foreach(var kv in all) 
        total += kv.Value.Free;
      return total;
    }
  }
}

public interface IExitableScope
{
  void RegisterDefer(DeferBlock cb);
  void ExitScope(VM.Frame frm);
}

public interface IBranchyCoroutine : ICoroutine
{
  void Attach(ICoroutine ex);
}

public interface IInspectableCoroutine 
{
  int Count { get; }
  ICoroutine At(int i);
}

class CoroutineSuspend : ICoroutine
{
  public static readonly ICoroutine Instance = new CoroutineSuspend();

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = BHS.RUNNING;
  }

  public void Cleanup(VM.Frame frm)
  {}
}

class CoroutineYield : ICoroutine
{
  bool first_time = true;

  public void Tick(VM.Frame frm, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      first_time = false;
    }
  }

  public void Cleanup(VM.Frame frm)
  {
    first_time = true;
  }
}

public struct DeferBlock
{
  public VM.Frame frm;
  public int ip;
  public int end_ip;

  public DeferBlock(VM.Frame frm, int ip, int end_ip)
  {
    this.frm = frm;
    this.ip = ip;
    this.end_ip = end_ip;
  }

  BHS Execute(ref ICoroutine coro, ref int ip)
  {
    int ip_copy = ip;
    ip = this.ip;

    frm.fb.ctx_frames.Push(new VM.FrameContext(frm, false, ip-1, end_ip+1));
    //Console.WriteLine("ENTER SCOPE " + ip + " " + (end_ip + 1) + " " + frames.Count);
    var status = frm.vm.Execute(
      ref ip, frm.fb.ctx_frames, 
      ref coro, 
      null,
      frm.fb.ctx_frames.Count-1
    );
    if(status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + status);
    //Console.WriteLine("~EXIT SCOPE " + ip + " " + (end_ip + 1));

    ip = ip_copy;

    return status;
  }

  static internal void ExitScope(VM.Frame frm, List<DeferBlock> defers, ref int ip)
  {
    if(defers == null)
      return;

    for(int i=defers.Count;i-- > 0;)
    {
      var d = defers[i];
      ICoroutine dummy = null;
      //TODO: do we need ensure that status is SUCCESS?
      d.Execute(ref dummy, ref ip);
    }
    defers.Clear();
  }

  static internal void DelCoroutines(VM.Frame frm, List<ICoroutine> coros)
  {
    for(int i=0;i<coros.Count;++i)
      CoroutinePool.Del(frm, coros[i]);
    coros.Clear();
  }
}

public class SeqBlock : ICoroutine, IExitableScope, IInspectableCoroutine
{
  public int ip;
  public int bgn_ip;
  public int end_ip;
  public ICoroutine coroutine;
  public List<DeferBlock> defers;
  public int frames_idx;

  public int Count {
    get {
      return 0;
    }
  }

  public ICoroutine At(int i) 
  {
    return coroutine;
  }

  public void Init(VM.Frame frm, int bgn_ip, int end_ip)
  {
    this.bgn_ip = bgn_ip;
    this.end_ip = end_ip;
    this.ip = bgn_ip;
    this.frames_idx = frm.fb.ctx_frames.Count;
    frm.fb.ip = bgn_ip;
    frm.fb.ctx_frames.Push(new VM.FrameContext(frm, false, bgn_ip-1, end_ip+1));
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = frm.vm.Execute(
      ref ip, frm.fb.ctx_frames, 
      ref coroutine, 
      this,
      frames_idx
    );
      
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the block end ip
    if(status == BHS.SUCCESS && ip >= bgn_ip && ip <= (end_ip+1))
      frm.fb.ip = end_ip;
  }

  public void Cleanup(VM.Frame frm)
  {
    if(coroutine != null)
    {
      CoroutinePool.Del(frm, coroutine);
      coroutine = null;
    }

    ExitScope(frm);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers, ref ip);

    //NOTE: Let's release frames which were allocated but due to 
    //      some control flow abruption (e.g return) should be 
    //      explicitely released. Top frame is released 'above'.
    for(int i=frm.fb.ctx_frames.Count;i-- > frames_idx;)
    {
      if(i > frames_idx + 1)
        frm.fb.ctx_frames[i].frame.Release();
      frm.fb.ctx_frames.RemoveAt(i);
    }
  }
}

public class ParalBranchBlock : ICoroutine, IExitableScope, IInspectableCoroutine
{
  public int ip;
  public int bgn_ip;
  public int end_ip;
  public ICoroutine coroutine;
  public FixedStack<VM.FrameContext> ctx_frames = new FixedStack<VM.FrameContext>(256);
  public List<DeferBlock> defers;

  public int Count {
    get {
      return 0;
    }
  }

  public ICoroutine At(int i) 
  {
    return coroutine;
  }

  public void Init(VM.Frame frm, int bgn_ip, int end_ip)
  {
    this.bgn_ip = bgn_ip;
    this.end_ip = end_ip;
    this.ip = bgn_ip;
    ctx_frames.Push(new VM.FrameContext(frm, false, bgn_ip-1, end_ip+1));
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = frm.vm.Execute(
      ref ip, ctx_frames, 
      ref coroutine, 
      this
    );
      
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the block end ip
    if(status == BHS.SUCCESS && ip >= bgn_ip && ip <= (end_ip+1))
      frm.fb.ip = end_ip;
  }

  public void Cleanup(VM.Frame frm)
  {
    if(coroutine != null)
    {
      CoroutinePool.Del(frm, coroutine);
      coroutine = null;
    }

    ExitScope(frm);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers, ref ip);

    //NOTE: Let's release frames which were allocated but due to 
    //      some control flow abruption (e.g paral exited) should be 
    //      explicitely released. We start from index 1 on purpose
    //      since the frame at index 0 will be released 'above'.
    for(int i=ctx_frames.Count;i-- > 1;)
      ctx_frames[i].frame.Release();
    ctx_frames.Clear();
  }
}

public class ParalBlock : IBranchyCoroutine, IExitableScope, IInspectableCoroutine
{
  public int bgn_ip;
  public int end_ip;
  public int i;
  public List<ICoroutine> branches = new List<ICoroutine>();
  public List<DeferBlock> defers;

  public int Count {
    get {
      return branches.Count;
    }
  }

  public ICoroutine At(int i) 
  {
    return branches[i];
  }

  public void Init(int bgn_ip, int end_ip)
  {
    this.bgn_ip = bgn_ip;
    this.end_ip = end_ip;
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    frm.fb.ip = bgn_ip;

    status = BHS.RUNNING;

    for(i=0;i<branches.Count;++i)
    {
      var branch = branches[i];
      branch.Tick(frm, ref status);
      if(status != BHS.RUNNING)
      {
        CoroutinePool.Del(frm, branch);
        branches.RemoveAt(i);
        //if the execution didn't "jump out" of the block (e.g. break) proceed to the block end ip
        if(frm.fb.ip >= bgn_ip && frm.fb.ip <= (end_ip+1))
          frm.fb.ip = end_ip;
        break;
      }
    }
  }

  public void Cleanup(VM.Frame frm)
  {
    DeferBlock.DelCoroutines(frm, branches);
    ExitScope(frm);
  }

  public void Attach(ICoroutine coro)
  {
    branches.Add(coro);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers, ref frm.fb.ip);
  }
}

public class ParalAllBlock : IBranchyCoroutine, IExitableScope, IInspectableCoroutine
{
  public int bgn_ip;
  public int end_ip;
  public int i;
  public List<ICoroutine> branches = new List<ICoroutine>();
  public List<DeferBlock> defers;

  public int Count {
    get {
      return branches.Count;
    }
  }

  public ICoroutine At(int i) 
  {
    return branches[i];
  }

  public void Init(int bgn_ip, int end_ip)
  {
    this.bgn_ip = bgn_ip;
    this.end_ip = end_ip;
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    frm.fb.ip = bgn_ip;

    for(i=0;i<branches.Count;)
    {
      var branch = branches[i];
      branch.Tick(frm, ref status);
      //let's check if we "jumped out" of the block (e.g return, break)
      if(frm.refs == -1 /*return executed*/ || frm.fb.ip < bgn_ip || frm.fb.ip > end_ip)
      {
        CoroutinePool.Del(frm, branch);
        branches.RemoveAt(i);
        status = BHS.SUCCESS;
        return;
      }
      if(status == BHS.SUCCESS)
      {
        CoroutinePool.Del(frm, branch);
        branches.RemoveAt(i);
      }
      else if(status == BHS.FAILURE)
      {
        CoroutinePool.Del(frm, branch);
        branches.RemoveAt(i);
        return;
      }
      else
        ++i;
    }

    if(branches.Count > 0)
      status = BHS.RUNNING;
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the block end ip
    else if(frm.fb.ip >= bgn_ip && frm.fb.ip <= (end_ip+1))
      frm.fb.ip = end_ip;
  }

  public void Cleanup(VM.Frame frm)
  {
    DeferBlock.DelCoroutines(frm, branches);
    ExitScope(frm);
  }

  public void Attach(ICoroutine coro)
  {
    branches.Add(coro);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers, ref frm.fb.ip);
  }
}

} //namespace bhl

using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

using marshall;

public enum Opcodes
{
  Constant          = 1,
  Add               = 2,
  Sub               = 3,
  Div               = 4,
  Mul               = 5,
  SetVar            = 6,
  GetVar            = 7,
  DeclVar           = 8,
  ArgVar            = 9,
  SetGVar           = 10,
  GetGVar           = 11,
  SetGVarImported   = 12,
  GetGVarImported   = 13,
  Return            = 14,
  ReturnVal         = 15,
  Jump              = 16,
  JumpZ             = 17,
  JumpPeekZ         = 18,
  JumpPeekNZ        = 19,
  Break             = 20,
  Continue          = 21,
  Pop               = 22,
  Call              = 23,
  CallNative        = 24,
  CallImported      = 25,
  CallMethod        = 26,
  CallMethodNative  = 27,
  CallMethodVirt    = 28,
  CallPtr           = 38,
  GetFunc           = 39,
  GetFuncNative     = 40,
  GetFuncFromVar    = 41,
  GetFuncImported   = 42,
  LastArgToTop      = 43,
  GetAttr           = 44,
  RefAttr           = 45,
  SetAttr           = 46,
  SetAttrInplace    = 47,
  ArgRef            = 48,
  UnaryNot          = 49,
  UnaryNeg          = 50,
  And               = 51,
  Or                = 52,
  Mod               = 53,
  BitOr             = 54,
  BitAnd            = 55,
  Equal             = 56,
  NotEqual          = 57,
  LT                = 59,
  LTE               = 60,
  GT                = 61,
  GTE               = 62,
  DefArg            = 63, 
  TypeCast          = 64,
  TypeAs            = 65,
  TypeIs            = 66,
  Block             = 75,
  New               = 76,
  Lambda            = 77,
  UseUpval          = 78,
  InitFrame         = 79,
  Inc               = 80,
  Dec               = 81,
  ArrIdx            = 82,
  ArrIdxW           = 83,
  ArrAddInplace     = 84,  //TODO: used for json alike array initialization,   
                           //      can be replaced with more low-level opcodes?
  Import            = 85,
}

public enum BlockType 
{
  FUNC      = 0,
  SEQ       = 1,
  DEFER     = 2,
  PARAL     = 3,
  PARAL_ALL = 4,
  IF        = 7,
  WHILE     = 8,
  FOR       = 9,
  DOWHILE   = 10,
}

public enum ConstType 
{
  INT        = 1,
  FLT        = 2,
  BOOL       = 3,
  STR        = 4,
  NIL        = 5,
  TPROXY     = 6,
}

public class Const
{
  static public readonly Const Nil = new Const(ConstType.NIL, 0, "");

  public ConstType type;
  public double num;
  public string str;
  public TypeProxy tproxy;

  public Const(ConstType type, double num, string str)
  {
    this.type = type;
    this.num = num;
    this.str = str;
  }

  public Const(int num)
  {
    type = ConstType.INT;
    this.num = num;
    str = "";
  }

  public Const(double num)
  {
    type = ConstType.FLT;
    this.num = num;
    str = "";
  }

  public Const(string str)
  {
    type = ConstType.STR;
    this.str = str;
    num = 0;
  }

  public Const(bool v)
  {
    type = ConstType.BOOL;
    num = v ? 1 : 0;
    this.str = "";
  }

  public Const(TypeProxy tp)
  {
    type = ConstType.TPROXY;
    this.tproxy = tp;
  }

  public Val ToVal(VM vm)
  {
    if(type == ConstType.INT)
      return Val.NewInt(vm, num);
    else if(type == ConstType.FLT)
      return Val.NewFlt(vm, num);
    else if(type == ConstType.BOOL)
      return Val.NewBool(vm, num == 1);
    else if(type == ConstType.STR)
      return Val.NewStr(vm, str);
    else if(type == ConstType.NIL)
      return vm.Null;
    else if(type == ConstType.TPROXY)
      return Val.NewObj(vm, tproxy, Types.Any/*TODO: must be Types.Type*/);
    else
      throw new Exception("Bad type");
  }

  public bool IsEqual(Const o)
  {
    return type == o.type && 
           num == o.num && 
           str == o.str &&
           tproxy.name == o.tproxy.name
           ;
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
    public IExitableScope ex_scope;
    //NOTE: if current ip is not within *inclusive* range of these values 
    //      the frame context execution is considered to be done
    public int min_ip;
    public int max_ip;

    public FrameContext(Frame frame, IExitableScope ex_scope, bool is_call, int min_ip = -1, int max_ip = MAX_IP)
    {
      this.frame = frame;
      this.is_call = is_call;
      this.ex_scope = ex_scope;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }
  }

  public class Fiber
  {
    public VM vm;

    internal int id;
    public int Id {
      get {
        return id;
      }
    }

    internal int tick;

    internal int ip;
    public int IP {
      get {
        return ip;
      }
    }

    internal ICoroutine coroutine;
    internal FixedStack<FrameContext> ctx_frames = new FixedStack<FrameContext>(256);

    public VM.Frame frame0 {
      get {
        return ctx_frames[0].frame;
      }
    }
    public FixedStack<Val> result = new FixedStack<Val>(Frame.MAX_STACK);
    
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

      //0 index frame used for return values consistency
      fb.ctx_frames.Push(new VM.FrameContext(Frame.New(vm), null, is_call: false));

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
        CoroutinePool.Del(ctx_frames.Peek().frame, ref ip, ctx_frames, coroutine);
        coroutine = null;
      }

      //we need to copy 0 index frame returned values 
      {
        result.Clear();
        for(int c=0;c<frame0.stack.Count;++c)
          result.Push(frame0.stack[c]);
        //let's clear the frame's stack so that values 
        //won't be released below
        frame0.stack.Clear();
      }

      for(int i=ctx_frames.Count;i-- > 0;)
      {
        //let's ignore temporary ctx.frames which are not calls
        if(!ctx_frames[i].is_call)
          continue;
        var frm = ctx_frames[i].frame;
        frm.ExitScope(frm, ref ip, ctx_frames);
        frm.Release();
      }

      //let's explicitely release 0 index frame
      frame0.Release();

      ctx_frames.Clear();

      tick = 0;
    }

    public bool IsStopped()
    {
      return ip >= STOP_IP;
    }

    static void GetCalls(FixedStack<VM.FrameContext> ctx_frames, List<VM.Frame> calls)
    {
      for(int i=0;i<ctx_frames.Count;++i)
        if(ctx_frames[i].is_call)
          calls.Add(ctx_frames[i].frame);
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      var calls = new List<VM.Frame>();
      int coroutine_ip = -1; 
      GetCalls(ctx_frames, calls);
      TryGetTraceInfo(coroutine, ref coroutine_ip, calls);

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
          item.ip = coroutine_ip == -1 ? frm.fb.ip : coroutine_ip;
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
          var fsymb = TryMapIp2Func(frm.module, calls[i].start_ip);

          item.func = fsymb == null ? "?" : fsymb.name;
          item.line = frm.module.ip2src_line.TryMap(item.ip);
        }
        else
        {
          item.file = "?";
          item.func = "?";
        }

        info.Insert(0, item);
      }
    }

    public string GetStackTrace()
    {
      var trace = new List<TraceItem>();
      GetStackTrace(trace);
      return Error.ToString(trace);
    }

    static bool TryGetTraceInfo(ICoroutine i, ref int ip, List<VM.Frame> calls)
    {
      if(i is SeqBlock si)
      {
        if(!TryGetTraceInfo(si.coroutine, ref ip, calls))
          ip = si.ip;
        return true;
      }
      else if(i is ParalBranchBlock bi)
      {
        GetCalls(bi.ctx_frames, calls);
        if(!TryGetTraceInfo(bi.coroutine, ref ip, calls))
          ip = bi.ip;
        return true;
      }
      else if(i is ParalBlock pi && pi.i < pi.branches.Count)
        return TryGetTraceInfo(pi.branches[pi.i], ref ip, calls);
      else if(i is ParalAllBlock pai && pai.i < pai.branches.Count)
        return TryGetTraceInfo(pai.branches[pai.i], ref ip, calls);
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
    public Frame origin;
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
        origin,
        origin.module, 
        origin.constants, 
        origin.bytecode, 
        start_ip
      );
    }

    public void Init(Fiber fb, Frame origin, CompiledModule module, int start_ip)
    {
      Init(
        fb, 
        origin,
        module, 
        module.constants, 
        module.bytecode, 
        start_ip
      );
    }

    internal void Init(Fiber fb, Frame origin, CompiledModule module, List<Const> constants, byte[] bytecode, int start_ip)
    {
      this.fb = fb;
      this.origin = origin;
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

      if(defers != null)
        defers.Clear();
    }

    public void RegisterDefer(DeferBlock cb)
    {
      if(defers == null)
        defers = new List<DeferBlock>();

      defers.Add(cb);
      //for debug
      //if(cb.frm != this)
      //  throw new Exception("INVALID DEFER BLOCK: mine " + GetHashCode() + ", other " + cb.frm.GetHashCode() + " " + fb.GetStackTrace());
    }

    public void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
    {
      DeferBlock.ExitScope(frm, defers, ref ip, ctx_frames);
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
      var frm = Frame.New(vm);
      if(module != null)
        frm.Init(curr_frame.fb, curr_frame, module, func_ip);
      else
        frm.Init(curr_frame, func_ip);

      for(int i=0;i<upvals.Count;++i)
      {
        var upval = upvals[i];
        if(upval != null)
        {
          frm.locals.Resize(i+1);
          upval.Retain();
          frm.locals[i] = upval;
        }
      }
      return frm;
    }
  }

  public struct TraceItem
  {
    public string file;
    public string func;
    public int line;
    public int ip; 
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
        s += "at " + t.func + "(..) in " + t.file + ":" + t.line + " (ip: " + t.ip + ")\n";
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

  Types types;
  public Types Types {
    get {
      return types;
    }
  }

  public struct FuncAddr
  {
    public CompiledModule module;
    public FuncSymbolScript fs;
    public int ip;
  }

  int fibers_ids = 0;
  List<Fiber> fibers = new List<Fiber>();
  public Fiber last_fiber = null;

  IModuleLoader loader;

  public delegate void ClassCreator(VM.Frame frm, ref Val res, IType type);

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

  //special case 'null' value
  Val null_val = null;
  public Val Null {
    get {
      null_val.Retain();
      return null_val;
    }
  }

  public VM(Types types = null, IModuleLoader loader = null)
  {
    if(types == null)
      types = new Types();
    this.types = types;
    this.loader = loader;

    init_frame = new Frame(this);

    null_val = new Val(this);
    null_val.SetObj(null, Types.Any);
    //NOTE: we don't want to store it in the values pool,
    //      still we need to retain it so that it's never 
    //      accidentally released when pushed/popped
    null_val.Retain();
  }

  public bool LoadModule(string module_name)
  {
    var loaded = loader.Load(module_name);
    if(loaded == null)
      return false;
    RegisterModule(loaded);
    return true;
  }

  void LoadModule(CompiledModule module, string module_name)
  {
    var loaded = loader.Load(module_name);
    if(loaded == null)
      throw new Exception("Module '" + module_name + "' not found");
    RegisterModule(loaded);

    module.symbols.AddImport(loaded.symbols);
  }

  public void RegisterModule(CompiledModule cm)
  {
    if(modules.ContainsKey(cm.name))
      return;
    modules.Add(cm.name, cm);

    types.AddSource(cm.symbols);

    ExecInit(cm);
  }

  public void UnloadModule(string module_name)
  {
    CompiledModule m;
    if(!modules.TryGetValue(module_name, out m))
      return;

    for(int i=0;i<m.gvars.Count;++i)
    {
      var val = m.gvars[i];
      if(val != null)
        val.RefMod(RefOp.DEC | RefOp.USR_DEC);
    }
    m.gvars.Clear();

    types.RemoveSource(m.symbols);

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
    if(bytecode == null || bytecode.Length == 0)
      return;

    var constants = module.constants;
    var stack = init_frame.stack;

    int ip = 0;

    while(ip < bytecode.Length)
    {
      var opcode = (Opcodes)bytecode[ip];
      switch(opcode)
      {
        case Opcodes.Import:
        {
          int module_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          string module_name = constants[module_idx].str;

          LoadModule(module, module_name);
        }
        break;
        case Opcodes.DeclVar:
        {
          int var_idx = (int)Bytecode.Decode8(bytecode, ref ip);
          int type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          var type = constants[type_idx].tproxy.Get();

          module.gvars.Resize(var_idx+1);
          module.gvars[var_idx] = MakeDefaultVal(type);
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
          var cv = cn.ToVal(this);
          stack.Push(cv);
        }
        break;
        case Opcodes.New:
        {
          int type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          IType type = constants[type_idx].tproxy.Get();
          HandleNew(init_frame, type);
        }
        break;
        case Opcodes.SetAttrInplace:
        {
          int fld_idx = (int)Bytecode.Decode16(bytecode, ref ip);
          var val = stack.Pop();
          var obj = stack.Peek();
          var class_symb = (ClassSymbol)obj.type;
          var field_symb = (FieldSymbol)class_symb.members[fld_idx];
          field_symb.setter(init_frame, ref obj, val, field_symb);
          val.Release();
        }
        break;
        case Opcodes.ArrAddInplace:
        {
          var self = stack[stack.Count - 2];
          self.Retain();
          var class_type = ((ArrayTypeSymbol)self.type);
          var status = BHS.SUCCESS;
          ((FuncSymbolNative)class_type.members[0]).cb(init_frame, new FuncArgsInfo(), ref status);
          stack.Push(self);
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
    FuncAddr addr;
    if(!TryFindFuncAddr(func, out addr))
      return null;

    var fb = Fiber.New(this);
    Register(fb);

    var frame = Frame.New(this);
    frame.Init(fb, fb.frame0, addr.module, addr.ip);

    for(int i=args.Length;i-- > 0;)
    {
      var arg = args[i];
      frame.stack.Push(arg);
    }
    //cargs bits
    frame.stack.Push(Val.NewFlt(this, cargs_bits));

    Attach(fb, frame);

    return fb;
  }

  bool TryFindFuncAddr(string name, out FuncAddr addr)
  {
    addr = default(FuncAddr);

    var fs = types.Resolve(name) as FuncSymbolScript;
    if(fs == null)
      return false;

    var cm = modules[((ModuleScope)fs.scope).module_name];

    addr = new FuncAddr() {
      module = cm,
      fs = fs,
      ip = fs.ip_addr
    };

    return true;
  }

  //TODO: add caching?
  FuncAddr GetFuncAddr(string name)
  {
    var fs = (FuncSymbolScript)types.Resolve(name);
    var cm = modules[((ModuleScope)fs.scope).module_name];
    return new FuncAddr() {
      module = cm,
      fs = fs,
      ip = fs.ip_addr
    };
  }

  static FuncSymbol TryMapIp2Func(CompiledModule cm, int ip)
  {
    for(int i=0;i<cm.symbols.members.Count; ++i)
    {
      var fsymb = cm.symbols.members[i] as FuncSymbolScript;
      if(fsymb != null && fsymb.ip_addr == ip)
        return fsymb;
    }
    return null;
  }

  //NOTE: adding special bytecode which makes the fake Frame to exit
  //      after executing the coroutine
  static byte[] RETURN_BYTES = new byte[] {(byte)Opcodes.Return};

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
      frame.Init(fb, curr_frame, null, null, RETURN_BYTES, 0);
      Attach(fb, frame);
      fb.coroutine = ptr.native.cb(curr_frame, new FuncArgsInfo(cargs_bits), ref fb.status);
      //NOTE: before executing a coroutine VM will increment ip optimistically
      //      but we need it to remain at the same position so that it points at
      //      the fake return opcode
      if(fb.coroutine != null)
        --fb.ip;
    }
    else
    {
      var frame = ptr.MakeFrame(this, curr_frame);
      Attach(fb, frame);
      //cargs bits
      frame.stack.Push(Val.NewFlt(this, cargs_bits));
    }

    return fb;
  }

  public void Detach(Fiber fb)
  {
    fibers.Remove(fb);
  }

  void Attach(Fiber fb, Frame frm)
  {
    fb.ip = frm.start_ip;
    frm.fb = fb;
    fb.ctx_frames.Push(new FrameContext(frm, frm, is_call: true));
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
    int frames_waterline_idx = 0
  )
  {
    var status = BHS.SUCCESS;
    int ctx_num = 0;
    while((ctx_num = ctx_frames.Count) > frames_waterline_idx && status == BHS.SUCCESS)
    {
      status = ExecuteOnce(
        ref ip, ctx_frames,
        ref coroutine
      );
    }
    return status;
  }

  BHS ExecuteOnce(
    ref int ip, 
    FixedStack<FrameContext> ctx_frames, 
    ref ICoroutine coroutine
  )
  { 
    var ctx = ctx_frames.Peek();

    if(ip < ctx.min_ip || ip > ctx.max_ip)
    {
      ctx_frames.Pop();
      return BHS.SUCCESS;
    }

    var curr_frame = ctx.frame;

    //Util.Debug("EXEC TICK " + curr_frame.fb.tick + " (" + curr_frame.GetHashCode() + "," + curr_frame.fb.id + ") IP " + ip + "(min:" + ctx.min_ip + ", max:" + ctx.max_ip + ")" + (ip > -1 && ip < curr_frame.bytecode.Length ? " OP " + (Opcodes)curr_frame.bytecode[ip] : " OP ? ") + " CORO " + coroutine?.GetType().Name + "(" + coroutine?.GetHashCode() + ")" + " EX.SCOPE " + ctx.ex_scope?.GetType().Name + "(" + ctx.ex_scope?.GetHashCode() + ") " + curr_frame.bytecode.Length /* + " " + curr_frame.fb.GetStackTrace()*/ /* + " " + Environment.StackTrace*/);

    //NOTE: if there's an active coroutine it has priority over simple 'code following' via ip
    if(coroutine != null)
      return ExecuteCoroutine(ref ip, ref coroutine, curr_frame, ctx_frames);

    var opcode = (Opcodes)curr_frame.bytecode[ip];
    //Console.WriteLine("OP " + opcode + " " + ip);
    switch(opcode)
    {
      case Opcodes.Constant:
      {
        int const_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var cn = curr_frame.constants[const_idx];
        var cv = cn.ToVal(this);
        curr_frame.stack.Push(cv);
      }
      break;
      case Opcodes.TypeCast:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var cast_type = curr_frame.constants[cast_type_idx].tproxy.Get();

        HandleTypeCast(curr_frame, cast_type);
      }
      break;
      case Opcodes.TypeAs:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var as_type = curr_frame.constants[cast_type_idx].tproxy.Get();

        HandleTypeAs(curr_frame, as_type);
      }
      break;
      case Opcodes.TypeIs:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var as_type = curr_frame.constants[cast_type_idx].tproxy.Get();

        HandleTypeIs(curr_frame, as_type);
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
      case Opcodes.ArrIdx:
      {
        var self = curr_frame.stack[curr_frame.stack.Count - 2];
        var class_type = ((ArrayTypeSymbol)self.type);
        var status = BHS.SUCCESS;
        class_type.FuncArrIdx.cb(curr_frame, new FuncArgsInfo(), ref status);
      }
      break;
      case Opcodes.ArrIdxW:
      {
        var self = curr_frame.stack[curr_frame.stack.Count - 2];
        var class_type = ((ArrayTypeSymbol)self.type);
        var status = BHS.SUCCESS;
        class_type.FuncArrIdxW.cb(curr_frame, new FuncArgsInfo(), ref status);
      }
      break;
      case Opcodes.ArrAddInplace:
      {
        var self = curr_frame.stack[curr_frame.stack.Count - 2];
        self.Retain();
        var class_type = ((ArrayTypeSymbol)self.type);
        var status = BHS.SUCCESS;
        ((FuncSymbolNative)class_type.members[0]).cb(curr_frame, new FuncArgsInfo(), ref status);
        curr_frame.stack.Push(self);
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
        int type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var type = curr_frame.constants[type_idx].tproxy.Get();

        var curr = curr_frame.locals[local_idx];
        //NOTE: handling case when variables are 're-declared' within the nested loop
        if(curr != null)
          curr.Release();
        curr_frame.locals[local_idx] = MakeDefaultVal(type);
      }
      break;
      case Opcodes.GetAttr:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var obj = curr_frame.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var res = Val.New(this);
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.getter(curr_frame, obj, ref res, field_symb);
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
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var obj = curr_frame.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        Val res;
        field_symb.getref(curr_frame, obj, out res, field_symb);
        curr_frame.stack.PushRetain(res);
        obj.Release();
      }
      break;
      case Opcodes.SetAttr:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);

        var obj = curr_frame.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var val = curr_frame.stack.Pop();
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.setter(curr_frame, ref obj, val, field_symb);
        val.Release();
        obj.Release();
      }
      break;
      case Opcodes.SetAttrInplace:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        var val = curr_frame.stack.Pop();
        var obj = curr_frame.stack.Peek();
        var class_symb = (ClassSymbol)obj.type;
        var field_symb = (FieldSymbol)class_symb.members[fld_idx];
        field_symb.setter(curr_frame, ref obj, val, field_symb);
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
        curr_frame.ExitScope(curr_frame, ref ip, ctx_frames);
        ip = curr_frame.return_ip;
        curr_frame.Clear();
        curr_frame.Release();
        ctx_frames.Pop();
      }
      break;
      case Opcodes.ReturnVal:
      {
        int ret_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

        int stack_offset = curr_frame.stack.Count; 
        for(int i=0;i<ret_num;++i)
        {
          curr_frame.origin.stack.Push(curr_frame.stack[stack_offset-ret_num+i]);
          curr_frame.stack.Dec();
        }

        ip = curr_frame.return_ip;
        curr_frame.ExitScope(curr_frame, ref ip, ctx_frames);
        curr_frame.Clear();
        curr_frame.Release();
        ctx_frames.Pop();
      }
      break;
      case Opcodes.GetFunc:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var func_symb = (FuncSymbolScript)curr_frame.module.symbols.members[func_idx];
        var ptr = FuncPtr.New(this);
        ptr.Init(curr_frame, func_symb.ip_addr);
        curr_frame.stack.Push(Val.NewObj(this, ptr, func_symb.GetSignature()));
      }
      break;
      case Opcodes.GetFuncNative:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        var func_symb = (FuncSymbolNative)types.globs.members[func_idx];
        var ptr = FuncPtr.New(this);
        ptr.Init(func_symb);
        curr_frame.stack.Push(Val.NewObj(this, ptr, func_symb.GetSignature()));
      }
      break;
      case Opcodes.GetFuncImported:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

        string func_name = curr_frame.constants[func_idx].str;
        var faddr = GetFuncAddr(func_name);

        var ptr = FuncPtr.New(this);
        ptr.Init(faddr.module, faddr.ip);
        curr_frame.stack.Push(Val.NewObj(this, ptr, faddr.fs.GetSignature()));
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
      case Opcodes.LastArgToTop:
      {
        //NOTE: we need to move arg (e.g. func ptr) to the top of the stack
        //      so that it fullfills Opcode.Call requirements 
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK); 
        int arg_idx = curr_frame.stack.Count - args_num - 1; 
        var arg = curr_frame.stack[arg_idx];
        curr_frame.stack.RemoveAt(arg_idx);
        curr_frame.stack.Push(arg);
      }
      break;
      case Opcodes.Call:
      {
        int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip); 
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        var frm = Frame.New(this);
        frm.Init(curr_frame, func_ip);
        Call(curr_frame, ctx_frames, frm, args_bits, ref ip);
      }
      break;
      case Opcodes.CallNative:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        var native = (FuncSymbolNative)types.globs.members[func_idx];

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
        
        var maddr = GetFuncAddr(func_name);

        var frm = Frame.New(this);
        frm.Init(curr_frame.fb, curr_frame, maddr.module, maddr.ip);
        Call(curr_frame, ctx_frames, frm, args_bits, ref ip);
      }
      break;
      case Opcodes.CallMethodNative:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK); 
        int self_idx = curr_frame.stack.Count - args_num - 1;
        var self = curr_frame.stack[self_idx];

        var class_type = ((ClassSymbol)self.type);

        BHS status;
        if(CallNative(curr_frame, (FuncSymbolNative)class_type.members[func_idx], args_bits, out status, ref coroutine))
          return status;
      }
      break;
      case Opcodes.CallMethod:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        //TODO: use a simpler schema where 'self' is passed on the top
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK); 
        int self_idx = curr_frame.stack.Count - args_num - 1;
        var self = curr_frame.stack[self_idx];
        curr_frame.stack.RemoveAt(self_idx);

        var class_type = ((ClassSymbol)self.type);

        var field_symb = (FuncSymbolScript)class_type.members[func_idx];
        int func_ip = field_symb.ip_addr;

        var frm = Frame.New(this);
        frm.Init(curr_frame, func_ip);

        frm.locals[0] = self;

        Call(curr_frame, ctx_frames, frm, args_bits, ref ip);
      }
      break;
      case Opcodes.CallMethodVirt:
      {
        int iface_func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        int iface_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

        //TODO: use a simpler schema where 'self' is passed on the top
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK); 
        int self_idx = curr_frame.stack.Count - args_num - 1;
        var self = curr_frame.stack[self_idx];
        curr_frame.stack.RemoveAt(self_idx);

        var iface_symb = (InterfaceSymbol)curr_frame.constants[iface_idx].tproxy.Get(); 
        var class_type = (ClassSymbol)self.type;
        int func_idx = class_type.vtable[iface_symb][iface_func_idx];

        var field_symb = (FuncSymbolScript)class_type.members[func_idx];
        int func_ip = field_symb.ip_addr;

        var frm = Frame.New(this);
        frm.Init(curr_frame, func_ip);

        frm.locals[0] = self;

        Call(curr_frame, ctx_frames, frm, args_bits, ref ip);
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
          var frm = ptr.MakeFrame(this, curr_frame);
          val_ptr.Release();
          Call(curr_frame, ctx_frames, frm, args_bits, ref ip);
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
        curr_frame.stack.Push(Val.NewObj(this, ptr, Types.Any));

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
      }
      break;
      case Opcodes.Continue:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        ip += offset;
      }
      break;
      case Opcodes.DefArg:
      {
        byte def_arg_idx = (byte)Bytecode.Decode8(curr_frame.bytecode, ref ip);
        int jump_pos = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
        uint args_bits = (uint)curr_frame.locals[curr_frame.locals.Count-1]._num; 
        var args_info = new FuncArgsInfo(args_bits);
        //Console.WriteLine("DEF ARG: " + def_arg_idx + ", jump pos " + jump_pos + ", used " + args_info.IsDefaultArgUsed(def_arg_idx) + " " + args_bits);
        //NOTE: if default argument is not used we need to jump out of default argument calculation code
        if(!args_info.IsDefaultArgUsed(def_arg_idx))
          ip += jump_pos;
      }
      break;
      case Opcodes.Block:
      {
        var new_coroutine = VisitBlock(ref ip, ctx_frames, curr_frame, ctx.ex_scope);
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
        IType type = curr_frame.constants[type_idx].tproxy.Get();
        HandleNew(curr_frame, type);
      }
      break;
      default:
        throw new Exception("Not supported opcode: " + opcode);
    }

    ++ip;
    return BHS.SUCCESS;
  }

  internal Val MakeDefaultVal(IType type)
  {
    var v = Val.New(this);
    //TODO: make type responsible for default initialization 
    //      of the value
    if(type == Types.Int)
      v.SetNum(0);
    else if(type == Types.Float)
      v.SetFlt((double)0);
    else if(type == Types.String)
      v.SetStr("");
    else if(type == Types.Bool)
      v.SetBool(false);
    else
      v.type = type;

    return v;
  }

  void Call(Frame curr_frame, FixedStack<FrameContext> ctx_frames, Frame new_frame, uint args_bits, ref int ip)
  {
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK); 
    for(int i = 0; i < args_num; ++i)
      new_frame.stack.Push(curr_frame.stack.Pop());
    new_frame.stack.Push(Val.NewFlt(this, args_bits));

    //let's remember ip to return to
    new_frame.return_ip = ip;
    ctx_frames.Push(new FrameContext(new_frame, new_frame, is_call: true));
    //since ip will be incremented below we decrement it intentionally here
    ip = new_frame.start_ip - 1; 
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  static bool CallNative(Frame curr_frame, FuncSymbolNative native, uint args_bits, out BHS status, ref ICoroutine coroutine)
  {
    status = BHS.SUCCESS;
    var new_coroutine = native.cb(curr_frame, new FuncArgsInfo(args_bits), ref status);

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
    //NOTE: optimistically stepping forward so that for simple  
    //      bindings you won't forget to do it
    ++ip;
    coroutine.Tick(curr_frame, ref ip, ctx_frames, ref status);

    if(status == BHS.RUNNING)
    {
      --ip;
      return status;
    }
    else if(status == BHS.FAILURE)
    {
      CoroutinePool.Del(curr_frame, ref ip, ctx_frames, coroutine);
      coroutine = null;

      curr_frame.ExitScope(curr_frame, ref ip, ctx_frames);
      ip = curr_frame.return_ip;
      curr_frame.Release();
      ctx_frames.Pop();

      return status;
    }
    else if(status == BHS.SUCCESS)
    {
      CoroutinePool.Del(curr_frame, ref ip, ctx_frames, coroutine);
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

      return status;
    }
    else
      throw new Exception("Bad status: " + status);
  }

  //TODO: make it more universal and robust
  void HandleTypeCast(Frame curr_frame, IType cast_type)
  {
    var new_val = Val.New(this);
    var val = curr_frame.stack.PopRelease();

    if(cast_type == Types.Int)
      new_val.SetNum((int)val.num);
    else if(cast_type == Types.String && val.type != Types.String)
      new_val.SetStr(val.num.ToString());
    else
    {
      new_val.ValueCopyFrom(val);
      new_val.RefMod(RefOp.USR_INC);
    }

    curr_frame.stack.Push(new_val);
  }

  void HandleTypeAs(Frame curr_frame, IType type)
  {
    var val = curr_frame.stack.PopRelease();

    if(type != null && val.type != null && Types.Is(val.type, type))
    {
      var new_val = Val.New(this);
      new_val.ValueCopyFrom(val);
      new_val.RefMod(RefOp.USR_INC);
      curr_frame.stack.Push(new_val);
    }
    else
      curr_frame.stack.Push(Val.NewObj(this, null, Types.Any));
  }

  void HandleTypeIs(Frame curr_frame, IType type)
  {
    var val = curr_frame.stack.PopRelease();
    curr_frame.stack.Push(Val.NewBool(this, 
          type != null && 
          val.type != null && 
          Types.Is(val.type, type)
        )
    );
  }

  void HandleNew(Frame curr_frame, IType type)
  {
    var cls = type as ClassSymbol;
    if(cls == null)
      throw new Exception("Not a class symbol: " + type);

    var val = Val.New(this); 
    cls.creator(curr_frame, ref val, cls);
    curr_frame.stack.Push(val);
  }

  static void ReadBlockHeader(ref int ip, Frame curr_frame, out BlockType type, out int size)
  {
    type = (BlockType)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
  }

  ICoroutine TryMakeBlockCoroutine(ref int ip, FixedStack<VM.FrameContext> ctx_frames, Frame curr_frame, out int size, IExitableScope ex_scope)
  {
    BlockType type;
    ReadBlockHeader(ref ip, curr_frame, out type, out size);

    if(type == BlockType.PARAL)
    {
      var paral = CoroutinePool.New<ParalBlock>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == BlockType.PARAL_ALL) 
    {
      var paral = CoroutinePool.New<ParalAllBlock>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == BlockType.SEQ)
    {
      if(ex_scope is IBranchyCoroutine)
      {
        var br = CoroutinePool.New<ParalBranchBlock>(this);
        br.Init(curr_frame, ip + 1, ip + size);
        return br;
      }
      else
      {
        var seq = CoroutinePool.New<SeqBlock>(this);
        seq.Init(curr_frame, ip + 1, ip + size, ref ip, ctx_frames);
        return seq;
      }
    }
    else if(type == BlockType.DEFER)
    {
      var d = new DeferBlock(curr_frame, ip + 1, ip + size);
      ex_scope.RegisterDefer(d);
      //we need to skip defer block
      //Console.WriteLine("DEFER SKIP " + ip + " " + (ip+size) + " " + Environment.StackTrace);
      ip += size;
      return null;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  ICoroutine VisitBlock(ref int ip, FixedStack<VM.FrameContext> ctx_frames, Frame curr_frame, IExitableScope ex_scope)
  {
    int block_size;
    var block_coro = TryMakeBlockCoroutine(ref ip, ctx_frames, curr_frame, out block_size, ex_scope);

    //Console.WriteLine("BLOCK CORO " + block_coro?.GetType().Name + " " + block_coro?.GetHashCode());
    if(block_coro is IBranchyCoroutine bi) 
    {
      int tmp_ip = ip;
      while(tmp_ip < (ip + block_size))
      {
        ++tmp_ip;

        int tmp_size;
        var branch = TryMakeBlockCoroutine(ref tmp_ip, ctx_frames, curr_frame, out tmp_size, (IExitableScope)block_coro);

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
      Tick(fb);
    }

    for(int i=fibers.Count;i-- > 0;)
    {
      var fb = fibers[i];
      if(fb.IsStopped())
        fibers.RemoveAt(i);
    }

    return fibers.Count != 0;
  }

  public bool Tick(Fiber fb)
  {
    if(fb.IsStopped())
      return false;

    try
    {
      last_fiber = fb;

      ++fb.tick;
      fb.status = Execute(
        ref fb.ip, fb.ctx_frames, 
        ref fb.coroutine, 
        //NOTE: we exclude the special case 0 frame
        1
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
    return !fb.IsStopped();
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
        curr_frame.stack.Push(Val.NewFlt(this, operand * -1));
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
        if((r_operand.type == Types.String) && (l_operand.type == Types.String))
          curr_frame.stack.Push(Val.NewStr(this, (string)l_operand._obj + (string)r_operand._obj));
        else
          curr_frame.stack.Push(Val.NewFlt(this, l_operand._num + r_operand._num));
      break;
      case Opcodes.Sub:
        curr_frame.stack.Push(Val.NewFlt(this, l_operand._num - r_operand._num));
      break;
      case Opcodes.Div:
        curr_frame.stack.Push(Val.NewFlt(this, l_operand._num / r_operand._num));
      break;
      case Opcodes.Mul:
        curr_frame.stack.Push(Val.NewFlt(this, l_operand._num * r_operand._num));
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
        curr_frame.stack.Push(Val.NewFlt(this, l_operand._num % r_operand._num));
      break;
    }

    r_operand.Release();
    l_operand.Release();
  }
}

public class Ip2SrcLine
{
  public List<int> ips = new List<int>();
  public List<int> lines = new List<int>();

  public void Add(int ip, int line)
  {
    ips.Add(ip);
    lines.Add(line);
  }

  public int TryMap(int ip)
  {
    int idx = Search(ip, 0, ips.Count-1);
    if(idx == -1)
      return 0;
    return lines[idx];
  }

  int Search(int ip, int l, int r)
  {
    if(r >= l)
    {
      int mid = l + (r - l) / 2;

      //checking for IP range
      if(ip <= ips[mid] && (mid == 0 || ip > ips[mid-1]))
        return mid;

      if(ips[mid] > ip)
        return Search(ip, l, mid - 1);
      else
        return Search(ip, mid + 1, r);
    }
    return -1;
  }
}

public class CompiledModule
{
  const uint HEADER_VERSION = 1;
  public const int MAX_GLOBALS = 128;

  public uint id;
  public string name;
  public ModuleScope symbols;
  public byte[] initcode;
  public byte[] bytecode;
  public List<Const> constants;
  public FixedStack<Val> gvars = new FixedStack<Val>(MAX_GLOBALS);
  public Ip2SrcLine ip2src_line;

  public CompiledModule(
    string name,
    ModuleScope symbols,
    List<Const> constants, 
    byte[] initcode,
    byte[] bytecode, 
    Ip2SrcLine ip2src_line = null
  )
  {
    this.name = name;
    this.symbols = symbols;
    this.constants = constants;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.ip2src_line = ip2src_line;
  }

  static public CompiledModule FromStream(Types types, Stream src, bool add_symbols_to_types = false)
  {
    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8, true/*leave open*/))
    {
      //TODO: add better support for version
      uint version = r.ReadUInt32();
      if(version != HEADER_VERSION)
        throw new Exception("Unsupported version: " + version);

      string name = r.ReadString();

      int symb_len = r.ReadInt32();
      var symb_bytes = r.ReadBytes(symb_len);
      var symbols = new ModuleScope(name, types.globs);
      var symb_factory = new SymbolFactory(types);
      if(add_symbols_to_types)
        types.AddSource(symbols);
      Marshall.Stream2Obj(new MemoryStream(symb_bytes), symbols, symb_factory);

      byte[] initcode = null;
      int initcode_len = r.ReadInt32();
      if(initcode_len > 0)
        initcode = r.ReadBytes(initcode_len);

      byte[] bytecode = null;
      int bytecode_len = r.ReadInt32();
      if(bytecode_len > 0)
        bytecode = r.ReadBytes(bytecode_len);

      var constants = new List<Const>();
      int constants_len = r.ReadInt32();
      for(int i=0;i<constants_len;++i)
      {
        Const cn = null;

        var cn_type = (ConstType)r.Read();

        if(cn_type == ConstType.STR)
          cn = new Const(r.ReadString());
        else if(cn_type == ConstType.FLT || 
                cn_type == ConstType.INT ||
                cn_type == ConstType.BOOL ||
                cn_type == ConstType.NIL)
          cn = new Const(cn_type, r.ReadDouble(), "");
        else if(cn_type == ConstType.TPROXY)
        {
          var tp = Marshall.Stream2Obj<TypeProxy>(src, symb_factory);
          if(string.IsNullOrEmpty(tp.name))
            throw new Exception("Missing name");
          cn = new Const(tp);
        }
        else
          throw new Exception("Unknown type: " + cn_type);

        constants.Add(cn);
      }

      var ip2src_line = new Ip2SrcLine();
      int ip2src_line_len = r.ReadInt32();
      for(int i=0;i<ip2src_line_len;++i)
        ip2src_line.Add(r.ReadInt32(), r.ReadInt32());

      return new 
        CompiledModule(
          name, 
          symbols, 
          constants, 
          initcode, 
          bytecode, 
          ip2src_line
       );
    }
  }

  static public void ToStream(CompiledModule cm, Stream dst)
  {
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8))
    {
      //TODO: add better support for version
      //TODO: introduce header info with offsets to data
      w.Write(HEADER_VERSION);

      w.Write(cm.name);

      var symb_bytes = Marshall.Obj2Bytes(cm.symbols);
      w.Write(symb_bytes.Length);
      w.Write(symb_bytes, 0, symb_bytes.Length);

      w.Write(cm.initcode == null ? (int)0 : cm.initcode.Length);
      if(cm.initcode != null)
        w.Write(cm.initcode, 0, cm.initcode.Length);

      w.Write(cm.bytecode == null ? (int)0 : cm.bytecode.Length);
      if(cm.bytecode != null)
        w.Write(cm.bytecode, 0, cm.bytecode.Length);

      w.Write(cm.constants.Count);
      foreach(var cn in cm.constants)
      {
        w.Write((byte)cn.type);
        if(cn.type == ConstType.STR)
          w.Write(cn.str);
        else if(cn.type == ConstType.FLT || 
                cn.type == ConstType.INT ||
                cn.type == ConstType.BOOL ||
                cn.type == ConstType.NIL)
          w.Write(cn.num);
        else if(cn.type == ConstType.TPROXY)
        {
          Marshall.Obj2Stream(cn.tproxy, dst);
        }
        else
          throw new Exception("Unknown type: " + cn.type);
      }

      //TODO: add this info only for development builds
      w.Write(cm.ip2src_line.ips.Count);
      for(int i=0;i<cm.ip2src_line.ips.Count;++i)
      {
        w.Write(cm.ip2src_line.ips[i]);
        w.Write(cm.ip2src_line.lines[i]);
      }
    }
  }

  static public void ToFile(CompiledModule cm, string file)
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      ToStream(cm, wfs);
    }
  }
}

public interface ICoroutine
{
  void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames, ref BHS status);
  void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames);
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

  static public void Del(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ICoroutine coro)
  {
    //Console.WriteLine("DEL " + coro.GetType().Name + " " + coro.GetHashCode()/* + " " + Environment.StackTrace*/);

    coro.Cleanup(frm, ref ip, frames);

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
  void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames);
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

  public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ref BHS status)
  {
    status = BHS.RUNNING;
  }

  public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
  {}
}

class CoroutineYield : ICoroutine
{
  bool first_time = true;

  public void Tick(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames, ref BHS status)
  {
    if(first_time)
    {
      status = BHS.RUNNING;
      first_time = false;
    }
  }

  public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> frames)
  {
    first_time = true;
  }
}

public struct DeferBlock
{
  public VM.Frame frm;
  public int ip;
  public int max_ip;

  public DeferBlock(VM.Frame frm, int ip, int max_ip)
  {
    this.frm = frm;
    this.ip = ip;
    this.max_ip = max_ip;
  }

  BHS Execute(ref ICoroutine coro, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    //1. let's remeber the original ip in order to restore it once
    //   the execution of this block is done (defer block can be 
    //   located anywhere in the code)
    int ip_orig = ip;
    ip = this.ip;

    //2. let's create the execution context
    ctx_frames.Push(new VM.FrameContext(frm, null, is_call: false, min_ip: ip, max_ip: max_ip));
    //Console.WriteLine("ENTER SCOPE " + ip + " " + end_ip + " " + ctx_frames.Count);
    //3. and execute it
    var status = frm.vm.Execute(
      ref ip, ctx_frames, 
      ref coro, 
      ctx_frames.Count-1
    );
    if(status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + status);
    //Console.WriteLine("~EXIT SCOPE " + ip + " " + end_ip);

    ip = ip_orig;

    return status;
  }

  static internal void ExitScope(VM.Frame frm, List<DeferBlock> defers, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    if(defers == null)
      return;

    for(int i=defers.Count;i-- > 0;)
    {
      var d = defers[i];
      ICoroutine dummy = null;
      //TODO: do we need ensure that status is SUCCESS?
      d.Execute(ref dummy, ref ip, ctx_frames);
    }
    defers.Clear();
  }

  static internal void DelCoroutines(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames, List<ICoroutine> coros)
  {
    for(int i=0;i<coros.Count;++i)
      CoroutinePool.Del(frm, ref ip, ctx_frames, coros[i]);
    coros.Clear();
  }
}

public class SeqBlock : ICoroutine, IExitableScope, IInspectableCoroutine
{
  public int ip;
  public ICoroutine coroutine;
  public List<DeferBlock> defers;
  public int waterline_idx;

  public int Count {
    get {
      return 0;
    }
  }

  public ICoroutine At(int i) 
  {
    return coroutine;
  }

  public void Init(VM.Frame frm, int min_ip, int max_ip, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames)
  {
    this.ip = min_ip;
    ext_ip = ip;
    this.waterline_idx = ext_frames.Count;
    ext_frames.Push(new VM.FrameContext(frm, this, is_call: false, min_ip: min_ip, max_ip: max_ip));
  }

  public void Tick(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames, ref BHS status)
  {
    status = frm.vm.Execute(
      ref ip, ext_frames, 
      ref coroutine, 
      waterline_idx
    );
    ext_ip = ip;
  }

  public void Cleanup(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames)
  {
    if(coroutine != null)
    {
      CoroutinePool.Del(frm, ref ip, ext_frames, coroutine);
      coroutine = null;
    }

    ExitScope(frm, ref ip, ext_frames);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.ExitScope(frm, defers, ref ip, ctx_frames);

    //NOTE: Let's release frames which were allocated but due to 
    //      some control flow abruption (e.g return) should be 
    //      explicitely released. Top frame is released 'above'.
    for(int i=ctx_frames.Count;i-- > waterline_idx;)
    {
      if(i > waterline_idx + 1)
        ctx_frames[i].frame.Release();
      ctx_frames.RemoveAt(i);
    }
  }
}

public class ParalBranchBlock : ICoroutine, IExitableScope, IInspectableCoroutine
{
  public int ip;
  public int min_ip;
  public int max_ip;
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

  public void Init(VM.Frame frm, int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    this.ip = min_ip;
    ctx_frames.Push(new VM.FrameContext(frm, this, is_call: false, min_ip: min_ip, max_ip: max_ip));
  }

  public void Tick(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames, ref BHS status)
  {
    status = frm.vm.Execute(
      ref ip, ctx_frames, 
      ref coroutine
    );

    if(status == BHS.SUCCESS)
    {
      //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after block
      if(ip > min_ip && ip < max_ip)
        ext_ip = max_ip + 1;
      //otherwise just assign ext_ip the last ip result (this is needed for break, continue) 
      else
        ext_ip = ip;
    }
  }

  public void Cleanup(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames)
  {
    if(coroutine != null)
    {
      CoroutinePool.Del(frm, ref ip, ctx_frames, coroutine);
      coroutine = null;
    }

    ExitScope(frm, ref ip, ctx_frames);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.ExitScope(frm, defers, ref ip, ctx_frames);

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
  public int min_ip;
  public int max_ip;
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

  public void Init(int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    i = 0;
    branches.Clear();
    if(defers != null)
      defers.Clear();
  }

  public void Tick(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames, ref BHS status)
  {
    ext_ip = min_ip;

    status = BHS.RUNNING;

    for(i=0;i<branches.Count;++i)
    {
      var branch = branches[i];
      branch.Tick(frm, ref ext_ip, ext_frames, ref status);
      if(status != BHS.RUNNING)
      {
        CoroutinePool.Del(frm, ref ext_ip, ext_frames, branch);
        branches.RemoveAt(i);
        //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after the block
        if(ext_ip > min_ip && ext_ip < max_ip)
          ext_ip = max_ip + 1;
        break;
      }
    }
  }

  public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.DelCoroutines(frm, ref ip, ctx_frames, branches);
    ExitScope(frm, ref ip, ctx_frames);
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

  public void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.ExitScope(frm, defers, ref ip, ctx_frames);
  }
}

public class ParalAllBlock : IBranchyCoroutine, IExitableScope, IInspectableCoroutine
{
  public int min_ip;
  public int max_ip;
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

  public void Init(int min_ip, int max_ip)
  {
    this.min_ip = min_ip;
    this.max_ip = max_ip;
    i = 0;
    branches.Clear();
    if(defers != null)
      defers.Clear();
  }

  public void Tick(VM.Frame frm, ref int ext_ip, FixedStack<VM.FrameContext> ext_frames, ref BHS status)
  {
    ext_ip = min_ip;
    
    for(i=0;i<branches.Count;)
    {
      var branch = branches[i];
      branch.Tick(frm, ref ext_ip, ext_frames, ref status);
      //let's check if we "jumped out" of the block (e.g return, break)
      if(frm.refs == -1 /*return executed*/ || ext_ip < (min_ip-1) || ext_ip > (max_ip+1))
      {
        CoroutinePool.Del(frm, ref ext_ip, ext_frames, branch);
        branches.RemoveAt(i);
        status = BHS.SUCCESS;
        return;
      }
      if(status == BHS.SUCCESS)
      {
        CoroutinePool.Del(frm, ref ext_ip, ext_frames, branch);
        branches.RemoveAt(i);
      }
      else if(status == BHS.FAILURE)
      {
        CoroutinePool.Del(frm, ref ext_ip, ext_frames, branch);
        branches.RemoveAt(i);
        return;
      }
      else
        ++i;
    }

    if(branches.Count > 0)
      status = BHS.RUNNING;
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the ip after this block
    else if(ext_ip > min_ip && ext_ip < max_ip)
      ext_ip = max_ip + 1;
  }

  public void Cleanup(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.DelCoroutines(frm, ref ip, ctx_frames, branches);
    ExitScope(frm, ref ip, ctx_frames);
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

  public void ExitScope(VM.Frame frm, ref int ip, FixedStack<VM.FrameContext> ctx_frames)
  {
    DeferBlock.ExitScope(frm, defers, ref ip, ctx_frames);
  }
}

} //namespace bhl

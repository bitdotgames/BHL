//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public enum Opcodes
{
  Constant        = 0x1,
  Add             = 0x2,
  Sub             = 0x3,
  Div             = 0x4,
  Mul             = 0x5,
  SetVar          = 0x6,
  GetVar          = 0x7,
  DeclVar         = 0x8,
  ArgVar          = 0x9,
  GetAttr         = 0xA,
  RefAttr         = 0xB,
  Return          = 0xC,
  ReturnVal       = 0xD,
  Jump            = 0xE,
  Pop             = 0xF,
  Call            = 0x10,
  GetFunc         = 0x12,
  GetFuncNative   = 0x13,
  GetFuncFromVar  = 0x14,
  GetFuncImported = 0x15,
  GetMethodNative = 0x16,
  GetLambda       = 0x17,
  CondJump        = 0x18,
  LongJump        = 0x19,
  SetAttr         = 0x20,
  SetAttrInplace  = 0x21,
  ArgRef          = 0x22,
  UnaryNot        = 0x31,
  UnaryNeg        = 0x32,
  And             = 0x33,
  Or              = 0x34,
  Mod             = 0x35,
  BitOr           = 0x36,
  BitAnd          = 0x37,
  Equal           = 0x38,
  NotEqual        = 0x39,
  LT              = 0x3A,
  LTE             = 0x3B,
  GT              = 0x3C,
  GTE             = 0x3D,
  DefArg          = 0x3E, 
  TypeCast        = 0x3F,
  Block           = 0x40,
  New             = 0x41,
  Lambda          = 0x42,
  UseUpval        = 0x43,
  InitFrame       = 0x44,
  Inc             = 0x45,
  Dec             = 0x46,
  ClassBegin      = 0x48,
  ClassMember     = 0x49,
  ClassEnd        = 0x4A,
  Import          = 0x4B,
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

  public const int CALL_USER   = 0;
  public const int CALL_NATIVE = 1;

  public struct Context
  {
    public Frame frame;
    public int min_ip;
    public int max_ip;

    public Context(Frame frame, int min_ip = -1, int max_ip = MAX_IP)
    {
      this.frame = frame;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }
  }

  public class Fiber
  {
    internal VM vm;

    internal int id;
    internal int tick;
    internal int ip;
    internal IInstruction instruction;
    internal FixedStack<Context> ctxs = new FixedStack<Context>(256);

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
      if(instruction != null)
      {
        Instructions.Del(ctxs.Peek().frame, instruction);
        instruction = null;
      }

      for(int i=ctxs.Count;i-- > 0;)
      {
        var frm = ctxs[i].frame;
        frm.ExitScope(frm);
        frm.Release();
      }
      ctxs.Clear();
      tick = 0;
    }

    public bool IsStopped()
    {
      return ip >= STOP_IP;
    }

    public void GetStackTrace(List<VM.TraceItem> info)
    {
      for(int i=0;i<ctxs.Count;++i)
      {
        var frm = ctxs[i].frame;

        var item = new TraceItem(); 
        item.file = frm.module.name + ".bhl";
        item.func = TraceItem.MapIp2Func(frm.start_ip, frm.module.func2ip);

        if(i == ctxs.Count-1)
        {
          if(!TryGetInstructionIP(instruction, out item.ip))
            item.ip = frm.fb.ip;

          frm.module.ip2src_line.TryGetValue(item.ip, out item.line);
        }
        else
        {
          var next = ctxs[i+1].frame;
          //NOTE: retrieving last ip for the current Frame which 
          //      turns out to be return_ip assigned to the next Frame
          item.ip = next.return_ip;
          frm.module.ip2src_line.TryGetValue(item.ip, out item.line);
        }

        info.Insert(0, item);
      }
    }

    static bool TryGetInstructionIP(IInstruction i, out int ip)
    {
      ip = 0;
      if(i is SeqInstruction si)
      {
        if(!TryGetInstructionIP(si.instruction, out ip))
          ip = si.ip;
        return true;
      }
      else if(i is ParalInstruction pi)
        return TryGetInstructionIP(pi.branches[pi.i], out ip);
      else if(i is ParalAllInstruction pai)
        return TryGetInstructionIP(pai.branches[pai.i], out ip);
      else
        return false;
    }
  }

  public class Frame : IExitableScope, IValRefcounted
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

    public void Init(Fiber fb, CompiledModule module, int start_ip)
    {
      this.fb = fb;
      this.module = module;
      constants = module.constants;
      Init(module.bytecode, start_ip);
    }

    public void Init(Frame origin, int start_ip)
    {
      fb = origin.fb;
      module = origin.module;
      constants = origin.constants;
      Init(origin.bytecode, start_ip);
    }

    void Init(byte[] bytecode, int start_ip)
    {
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      this.return_ip = -1;
    }

    public void Clear()
    {
      for(int i=locals.Count;i-- > 0;)
      {
        var val = locals[i];
        //for now allowing null gaps?
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

    public void SetLocal(int idx, Val val)
    {
      var curr = locals[idx];
      if(curr != null)
      {
        for(int i=0;i<curr._refs;++i)
        {
          val.RefMod(RefOp.USR_INC);
          curr.RefMod(RefOp.USR_DEC);
        }
        curr.ValueCopyFrom(val);
      }
      else
      {
        curr = Val.New(vm);
        curr.ValueCopyFrom(val);
        curr.RefMod(RefOp.USR_INC);
        locals[idx] = curr;
      }
    }

    public void RegisterDefer(DeferBlock cb)
    {
      if(defers == null)
        defers = new List<DeferBlock>();
      defers.Add(cb);
    }

    public void ExitScope(VM.Frame frm)
    {
      DeferBlock.ExitScope(frm, defers);
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

  public struct TraceItem
  {
    public string file;
    public string func;
    public int line;
    public int ip; 

    static public string MapIp2Func(int ip, Dictionary<string, int> func2ip)
    {
      foreach(var kv in func2ip)
      {
        if(kv.Value == ip)
          return kv.Key;
      }
      return "?";
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

  internal struct ModuleAddr
  {
    internal CompiledModule module;
    internal int ip;
  }

  Dictionary<string, CompiledModule> modules = new Dictionary<string, CompiledModule>();

  GlobalScope globs;
  public GlobalScope Globs {
    get {
      return globs;
    }
  }

  LocalScope symbols;
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
  public Pool<ValDict> vdicts_pool = new Pool<ValDict>();
  public Pool<Frame> frames_pool = new Pool<Frame>();
  public Pool<Fiber> fibers_pool = new Pool<Fiber>();
  public Instructions instr_pool = new Instructions();

  public VM(GlobalScope globs = null, IModuleImporter importer = null)
  {
    if(globs == null)
      globs = SymbolTable.VM_CreateBuiltins();
    this.globs = globs;
    this.importer = importer;
    symbols = new LocalScope(globs);
  }

  public void ImportModule(string module_name)
  {
    var imported_module = importer.Import(module_name);
    RegisterModule(imported_module);
  }

  public void RegisterModule(CompiledModule m)
  {
    if(modules.ContainsKey(m.name))
      return;
    modules.Add(m.name, m);

    foreach(var kv in m.func2ip)
    {
      func2addr.Add(kv.Key, new ModuleAddr() {
            module = m,
            ip = kv.Value
          });
    }

    if(m.initcode != null && m.initcode.Length != 0)
      ExecInit(m);
  }

  void ExecInit(CompiledModule module)
  {
    byte[] bytecode = module.initcode;
    int ip = 0;
    AST_ClassDecl curr_decl = null;
    while(ip < bytecode.Length)
    {
      var opcode = (Opcodes)bytecode[ip];
      switch(opcode)
      {
        case Opcodes.Import:
        {
          int module_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          string module_name = module.constants[module_idx].str;
          ImportModule(module_name);
        }
        break;
        case Opcodes.ClassBegin:
        {
          int type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          int parent_type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          curr_decl = new AST_ClassDecl();
          curr_decl.name = module.constants[type_idx].str;
          if(parent_type_idx != -1)
            curr_decl.parent = module.constants[parent_type_idx].str;
        }
        break;
        case Opcodes.ClassMember:
        {
          int type_idx = (int)Bytecode.Decode32(bytecode, ref ip);
          int name_idx = (int)Bytecode.Decode32(bytecode, ref ip);

          var mdecl = new AST_VarDecl();
          mdecl.type = module.constants[type_idx].str;
          mdecl.name = module.constants[name_idx].str;
          mdecl.symb_idx = (uint)curr_decl.children.Count;
          curr_decl.children.Add(mdecl);
        }
        break;
        case Opcodes.ClassEnd:
        {
          //TODO: add parent support
          ClassSymbolScript parent = null;
          var curr_class = new ClassSymbolScript(new HashedName(curr_decl.name), curr_decl, parent);
          for(int i=0;i<curr_decl.children.Count;++i)
          {
            var mdecl = (AST_VarDecl)curr_decl.children[i];
            curr_class.Define(new FieldSymbolScript(mdecl.name, mdecl.type, (int)mdecl.symb_idx));
          }
          symbols.Define(curr_class);
          curr_class = null;
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
    var fr = Frame.New(this);
    fr.Init(fb, addr.module, addr.ip);
    Register(fb, fr);

    for(int i=args.Length;i-- > 0;)
    {
      var arg = args[i];
      fr.stack.Push(arg);
    }
    //cargs bits
    fr.stack.Push(Val.NewNum(this, cargs_bits));

    return fb;
  }

  public Fiber Start(Frame fr)
  {
    var fb = Fiber.New(this);
    Register(fb, fr);
    //cargs bits
    fr.stack.Push(Val.NewNum(this, 0));
    return fb;
  }

  void Register(Fiber fb, Frame fr)
  {
    fb.id = ++fibers_ids;
    fb.ip = fr.start_ip;

    fr.fb = fb;
    fb.ctxs.Push(new Context(fr));

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
    FixedStack<Context> ctxs, 
    ref IInstruction instruction, 
    IExitableScope defer_scope,
    int ctx_min_count = 0
  )
  {
    while(ctxs.Count > ctx_min_count)
    {
      var instruction_status = BHS.SUCCESS;

      var res = ExecuteOnce(
        ref ip, ctxs,
        ref instruction, ref instruction_status,
        defer_scope
      );

      if(res == ExecuteResult.OutOfRange)
        return BHS.SUCCESS;
      else if(res == ExecuteResult.CheckInstruction)
      {
        if(instruction_status != BHS.SUCCESS)
          return instruction_status;
      }
    }
    return BHS.SUCCESS;
  }

  internal enum ExecuteResult
  {
    Ok,
    OutOfRange,
    NewInstruction,
    CheckInstruction,
  }

  ExecuteResult ExecuteOnce(
    ref int ip, 
    FixedStack<Context> ctxs, 
    ref IInstruction instruction, 
    ref BHS instruction_status,
    IExitableScope defer_scope
  )
  { 
    var ctx = ctxs.Peek();

    if(ip <= ctx.min_ip || ip >= ctx.max_ip)
      return ExecuteResult.OutOfRange;

    var curr_frame = ctx.frame;

    //Console.WriteLine("EXEC TICK " + curr_frame.fb.tick + " IP " + ip + "(min:" + ctx.min_ip + ", max:" + ctx.max_ip + ")" +  " OP " + (Opcodes)curr_frame.bytecode[ip] + " INST " + instruction?.GetType().Name + "(" + instruction?.GetHashCode() + ")" + " SCOPE " + defer_scope?.GetType().Name + "(" + defer_scope?.GetHashCode() + ")"/* + " " + Environment.StackTrace*/);

    //NOTE: if there's an active instruction it has priority over simple 'code following' via ip
    if(instruction != null)
    {
      instruction_status = ExecuteInstruction(ref instruction, ref ip, ctxs);
      return ExecuteResult.CheckInstruction; 
    }

    var opcode = (Opcodes)curr_frame.bytecode[ip];
    //Console.WriteLine("OP " + opcode + " " + ip);
    switch(opcode)
    {
      case Opcodes.Constant:
        {
          int const_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

          if(const_idx >= curr_frame.constants.Count)
            throw new Exception("Index out of constants: " + const_idx + ", total: " + curr_frame.constants.Count);

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
      case Opcodes.SetVar:
        {
          int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
          var new_val = curr_frame.stack.Pop();
          curr_frame.SetLocal(local_idx, new_val);
          new_val.Release();
        }
        break;
      case Opcodes.GetVar:
        {
          int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
          curr_frame.stack.PushRetain(curr_frame.locals[local_idx]);
        }
        break;
        //TODO: this one looks pretty much like SetVar
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
          Val v;
          if(type == Val.NUMBER)
            v = Val.NewNum(this, 0);
          else if(type == Val.STRING)
            v = Val.NewStr(this, "");
          else if(type == Val.BOOL)
            v = Val.NewBool(this, false);
          else
            v = Val.NewObj(this, null);
          curr_frame.locals[local_idx] = v;
        }
        break;
      case Opcodes.GetAttr:
        {
          int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
          string class_type = curr_frame.constants[class_type_idx].str;
          int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
          var class_symb = symbols.Resolve(class_type) as ClassSymbol;
          //TODO: this check must be in dev.version only
          if(class_symb == null)
            throw new Exception("Class type not found: " + class_type);

          var obj = curr_frame.stack.Pop();
          var res = Val.New(this);
          var field_symb = (FieldSymbol)class_symb.members[fld_idx];
          field_symb.VM_getter(obj, ref res);
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
          field_symb.VM_getref(obj, out res);
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
          field_symb.VM_setter(ref obj, val);
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
          field_symb.VM_setter(ref obj, val);
          val.Release();
        }
        break;
      case Opcodes.Return:
        {
          curr_frame.ExitScope(curr_frame);
          ip = curr_frame.return_ip;
          curr_frame.Clear();
          curr_frame.Release();
          //Console.WriteLine("RET IP " + ip + " FRAMES " + frames.Count);
          ctxs.Pop();
        }
        break;
      case Opcodes.ReturnVal:
        {
          int ret_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

          var ret_stack = ctxs.Count == 1 ? curr_frame.fb.stack : ctxs[ctxs.Count-2].frame.stack;  
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
          ctxs.Pop();
        }
        break;
      case Opcodes.GetFunc:
        {
          int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
          var func_frame = Frame.New(this);
          func_frame.Init(curr_frame, func_ip);
          curr_frame.stack.Push(Val.NewObj(this, func_frame));
        }
        break;
      case Opcodes.GetFuncNative:
        {
          int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
          var func_symb = (FuncSymbolNative)globs.GetMembers()[func_idx];
          var fn_val = Val.NewObj(this, func_symb);
          fn_val._num = CALL_NATIVE;
          curr_frame.stack.Push(fn_val);
        }
        break;
      case Opcodes.GetMethodNative:
        {
          int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);

          int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
          string class_type = curr_frame.constants[class_type_idx].str; 

          var class_symb = (ClassSymbol)symbols.Resolve(class_type);
          var func_symb = (FuncSymbolNative)class_symb.members[func_idx];
          var fn_val = Val.NewObj(this, func_symb);
          fn_val._num = CALL_NATIVE;
          curr_frame.stack.Push(fn_val);
        }
        break;
      case Opcodes.GetFuncFromVar:
        {
          int local_var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
          var val = curr_frame.locals[local_var_idx];
          if(val._obj is Frame frm)
          {
            //NOTE: we need to make an authentic copy of the original Frame stored in a var 
            //      in case it's already being executed. The simplest (but not the most smart one)
            //      way to do that is to check ref.counter.
            if(frm.refs > 1)
            {
              var frm_clone = Frame.New(this);
              frm_clone.Init(frm, frm.start_ip);
              curr_frame.stack.Push(Val.NewObj(this, frm_clone));
            }
            else
            {
              //NOTE: we need to call an extra Retain since Release will be called for this frame 
              //      during its execution of Opcode.Return, however since this frame is stored in a var 
              //      and this var will be released at some point we want to avoid 'double free' situation 
              frm.Retain();
              curr_frame.stack.Push(Val.NewObj(this, frm));
            }
          }
          else
          {
            var func_symb = (FuncSymbolNative)val._obj;
            var fn_val = Val.NewObj(this, func_symb);
            //marking it a native call
            fn_val._num = 1;
            curr_frame.stack.Push(fn_val);
          }
        }
        break;
      case Opcodes.GetFuncImported:
        {
          int module_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
          int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);

          string module_name = curr_frame.constants[module_idx].str;
          string func_name = curr_frame.constants[func_idx].str;

          var module = modules[module_name];
          //TODO: during postprocessing retrieve func_ip and encode it into the opcode
          //      so that there will be two Dictionary fetches less
          int func_ip = module.func2ip[func_name];

          var func_frame = Frame.New(this);
          func_frame.Init(curr_frame.fb, module, func_ip);
          curr_frame.stack.Push(Val.NewObj(this, func_frame));
        }
        break;
      case Opcodes.GetLambda:
        {
          //NOTE: geting rid of Frame on the stack left after Opcode.Lambda.
          //      Since lambda is called 'inplace' we need to generate proper 
          //      opcode sequence required for Opcode.Call
          uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 
          var args_info = new FuncArgsInfo(args_bits);
          int fr_idx = curr_frame.stack.Count-args_info.CountArgs()-1; 
          var fr = curr_frame.stack[fr_idx];
          curr_frame.stack.RemoveAt(fr_idx);
          curr_frame.stack.Push(fr);
        }
        break;
      case Opcodes.Call:
        {
          uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

          var val = curr_frame.stack.Pop();
          //checking if it's a userland or native func call
          if(val._num == CALL_USER)
          {
            var fr = (Frame)val._obj;
            //NOTE: it will be released once return is invoked
            fr.Retain();
            val.Release();

            var args_info = new FuncArgsInfo(args_bits);
            for(int i = 0; i < args_info.CountArgs(); ++i)
              fr.stack.Push(curr_frame.stack.Pop());
            fr.stack.Push(Val.NewNum(this, args_bits));

            //let's remember ip to return to
            fr.return_ip = ip;
            ctxs.Push(new Context(fr));
            //since ip will be incremented below we decrement it intentionally here
            ip = fr.start_ip - 1; 
          }
          else
          {
            var func_symb = (FuncSymbolNative)val._obj;
            val.Release();

            var args_info = new FuncArgsInfo(args_bits);
            for(int i = 0; i < args_info.CountArgs(); ++i)
              curr_frame.stack.Push(curr_frame.stack.Pop());

            var new_instruction = func_symb.VM_cb(curr_frame, args_info, ref instruction_status);
            if(new_instruction != null)
              AttachInstruction(ref instruction, new_instruction);

            if(instruction != null)
              return ExecuteResult.NewInstruction;
            else if(instruction_status != BHS.SUCCESS)
              return ExecuteResult.CheckInstruction;
          }
        }
        break;
      case Opcodes.InitFrame:
        {
          int local_vars_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
          var args_bits = curr_frame.stack.Pop(); 
          curr_frame.locals.SetHead(local_vars_num);
          //NOTE: we need to store arg info bits locally so that
          //      this information will be available to func 
          //      args related opcodes
          curr_frame.locals[local_vars_num-1] = args_bits;
        }
        break;
      case Opcodes.Lambda:
        {             
          short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref ip);
          var fr = Frame.New(this);
          fr.Init(curr_frame, ip+1/*func address*/);
          curr_frame.stack.Push(Val.NewObj(this, fr));

          ip += offset;
        }
        break;
      case Opcodes.UseUpval:
        {
          int up_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
          int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

          var lmb = (Frame)curr_frame.stack.Peek()._obj;

          //TODO: amount of local variables must be known ahead and
          //      initialized during Frame initialization
          //NOTE: we need to reflect the updated max amount of locals,
          //      otherwise they might not be cleared upon Frame exit
          lmb.locals.SetHead(local_idx+1);

          var up_val = curr_frame.locals[up_idx];
          up_val.Retain();
          if(lmb.locals[local_idx] != null)
            lmb.locals[local_idx].Release();
          lmb.locals[local_idx] = up_val;

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
      case Opcodes.CondJump:
        {
          ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref ip);
          //we need to jump only in case of false
          if(curr_frame.stack.PopRelease().bval == false)
            ip += offset;
        }
        break;
      case Opcodes.LongJump:
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
          var new_instr = VisitBlock(ref ip, curr_frame, defer_scope);
          if(new_instr != null)
          {
            AttachInstruction(ref instruction, new_instr);
            //NOTE: since there's a new instruction we want to skip ip incrementing
            //      which happens below and proceed right to the execution of 
            //      the new instruction in the beginning of the loop. If we don't 
            //      skip it we simply might exit the loop without executing the
            //      new instruction at all because we'll hit max_ip limit.
            return ExecuteResult.NewInstruction;
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
    return ExecuteResult.Ok;
  }

  static BHS ExecuteInstruction(
    ref IInstruction instruction, 
    ref int ip, 
    FixedStack<Context> ctxs
  )
  {
    var status = BHS.SUCCESS;

    var curr_frame = ctxs.Peek().frame;

    instruction.Tick(curr_frame, ref status);

    if(status == BHS.RUNNING)
      return status;
    else if(status == BHS.FAILURE)
    {
      Instructions.Del(curr_frame, instruction);
      instruction = null;

      curr_frame.ExitScope(curr_frame);
      ip = curr_frame.return_ip;
      curr_frame.Release();
      ctxs.Pop();

      return status;
    }
    else if(status == BHS.SUCCESS)
    {
      Instructions.Del(curr_frame, instruction);
      instruction = null;
      
      //NOTE: after instruction successful execution we might be in a situation  
      //      that instruction has already exited the current frame (e.g. after 'return')
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
        ctxs.Pop();

      //NOTE: we must increment the ip upon instruction completion
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
    cls.VM_creator(curr_frame, ref val);
    curr_frame.stack.Push(val);
  }

  static void AttachInstruction(ref IInstruction instruction, IInstruction candidate)
  {
    if(instruction != null)
    {
      if(instruction is IBranchyInstruction bi)
      {
        //Console.WriteLine("ATTACH " + mi.GetHashCode() + " " + candidate.GetHashCode());
        bi.Attach(candidate);
      }
      else
        throw new Exception("Can't attach to current instruction");
    }
    else
    {
      //Console.WriteLine("REPL " + candidate.GetHashCode());
      instruction = candidate;
    }
  }

  static void ReadBlockHeader(ref int ip, Frame curr_frame, out EnumBlock type, out int size)
  {
    type = (EnumBlock)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
  }

  IInstruction TryMakeBlockInstruction(ref int ip, Frame curr_frame, out int size, IExitableScope defer_scope)
  {
    EnumBlock type;
    ReadBlockHeader(ref ip, curr_frame, out type, out size);

    if(type == EnumBlock.PARAL)
    {
      var paral = Instructions.New<ParalInstruction>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == EnumBlock.PARAL_ALL) 
    {
      var paral = Instructions.New<ParalAllInstruction>(this);
      paral.Init(ip + 1, ip + size);
      return paral;
    }
    else if(type == EnumBlock.SEQ)
    {
      var seq = Instructions.New<SeqInstruction>(this);
      seq.Init(curr_frame, ip + 1, ip + size);
      return seq;
    }
    else if(type == EnumBlock.DEFER)
    {
      var d = new DeferBlock(curr_frame, ip + 1, ip + size);
      if(defer_scope != null)
        defer_scope.RegisterDefer(d);
      else 
        curr_frame.RegisterDefer(d);
      //we need to skip defer block
      //Console.WriteLine("DEFER SKIP " + ip + " " + (ip+size) + " " + Environment.StackTrace);
      ip += size;
      return null;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  IInstruction VisitBlock(ref int ip, Frame curr_frame, IExitableScope defer_scope)
  {
    int block_size;
    var block_inst = TryMakeBlockInstruction(ref ip, curr_frame, out block_size, defer_scope);

    //Console.WriteLine("BLOCK INST " + block_inst?.GetType().Name);
    if(block_inst is IBranchyInstruction bi) 
    {
      int tmp_ip = ip;
      while(tmp_ip < (ip + block_size))
      {
        ++tmp_ip;

        int tmp_size;
        var branch = TryMakeBlockInstruction(ref tmp_ip, curr_frame, out tmp_size, (IExitableScope)block_inst);

       //Console.WriteLine("BRANCH INST " + tmp_ip + " " + branch?.GetType().Name);

        //NOTE: branch == null is a special case for defer {..} block
        if(branch != null)
        {
          if(!(branch is IInstruction))
            throw new Exception("Invalid block branch instruction");
           bi.Attach(branch);
           tmp_ip += tmp_size;
        }
      }
    }
    return block_inst; 
  }

  public bool Tick()
  {
    for(int i=0;i<fibers.Count;++i)
    {
      var fb = fibers[i];

      //let's check if this Fiber was already stopped
      if(fb.IsStopped())
        continue;

      ++fb.tick;
      fb.status = Execute(
        ref fb.ip, fb.ctxs, 
        ref fb.instruction, 
        null
      );
      
      if(fb.status != BHS.RUNNING)
        Stop(fb);
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
        curr_frame.stack.Push(Val.NewBool(this, l_operand.IsEqual(r_operand)));
      break;
      case Opcodes.NotEqual:
        curr_frame.stack.Push(Val.NewBool(this, !l_operand.IsEqual(r_operand)));
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
  public string name;
  public byte[] initcode;
  public byte[] bytecode;
  public List<Const> constants;
  public Dictionary<string, int> func2ip;
  public Dictionary<int, int> ip2src_line;

  public CompiledModule(
    string name,
    byte[] bytecode, 
    List<Const> constants, 
    Dictionary<string, int> func2ip,
    byte[] initcode = null,
    Dictionary<int, int> ip2src_line = null
  )
  {
    this.name = name;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.constants = constants;
    this.func2ip = func2ip;
    this.ip2src_line = ip2src_line;
  }
}

public interface IInstruction
{
  void Tick(VM.Frame frm, ref BHS status);
  void Cleanup(VM.Frame frm);
}

public class Instructions
{
  Dictionary<System.Type, VM.Pool<IInstruction>> all = new Dictionary<System.Type, VM.Pool<IInstruction>>(); 

  static public T New<T>(VM vm) where T : IInstruction, new()
  {
    var t = typeof(T); 
    VM.Pool<IInstruction> pool;
    if(!vm.instr_pool.all.TryGetValue(t, out pool))
    {
      pool = new VM.Pool<IInstruction>();
      vm.instr_pool.all.Add(t, pool);
    }

    IInstruction inst = null;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      inst = new T();
    }
    else
    {
      ++pool.hit;
      inst = pool.stack.Pop();
    }

    //Console.WriteLine("NEW " + typeof(T).Name + " " + inst.GetHashCode()/* + " " + Environment.StackTrace*/);

    return (T)inst;
  }

  static public void Del(VM.Frame frm, IInstruction inst)
  {
    //Console.WriteLine("DEL " + inst.GetType().Name + " " + inst.GetHashCode()/* + " " + Environment.StackTrace*/);

    inst.Cleanup(frm);

    var t = inst.GetType();

    VM.Pool<IInstruction> pool;
    //ignoring instructions whch were not allocated via pool 
    if(!frm.vm.instr_pool.all.TryGetValue(t, out pool))
      return;

    pool.stack.Push(inst);

    if(pool.stack.Count > pool.miss)
      throw new Exception("Unbalanced New/Del " + pool.stack.Count + " " + pool.miss);
  }

  static public void Dump(IInstruction instruction, int level = 0)
  {
    if(level == 0)
      Console.WriteLine("<<<<<<<<<<<<<<<");

    string str = new String(' ', level);
    Console.WriteLine(str + instruction.GetType().Name + " " + instruction.GetHashCode());

    if(instruction is IInspectableInstruction ti)
    {
      foreach(var part in ti.Browse)
        Dump(part, level + 1);
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

public interface IBranchyInstruction : IInstruction
{
  void Attach(IInstruction ex);
}

public interface IInspectableInstruction 
{
  IList<IInstruction> Browse {get;}
}

class CoroutineSuspend : IInstruction
{
  public static readonly IInstruction Instance = new CoroutineSuspend();

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = BHS.RUNNING;
  }

  public void Cleanup(VM.Frame frm)
  {}
}

class CoroutineYield : IInstruction
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

  BHS Execute(ref IInstruction instruction)
  {
    //Console.WriteLine("EXIT SCOPE " + ip + " " + (end_ip + 1));
    frm.fb.ctxs.Push(new VM.Context(frm, ip - 1, end_ip + 1));
    var status = frm.vm.Execute(
      ref ip, frm.fb.ctxs, 
      ref instruction, 
      null,
      frm.fb.ctxs.Count-1
    );
    if(status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + status);
    frm.fb.ctxs.Pop();
    //Console.WriteLine("EXIT SCOPE~ " + ip + " " + (end_ip + 1));
    return status;
  }

  static internal void ExitScope(VM.Frame frm, List<DeferBlock> defers)
  {
    if(defers == null)
      return;

    for(int i=defers.Count;i-- > 0;)
    {
      var d = defers[i];
      IInstruction dummy = null;
      //TODO: do we need ensure that status is SUCCESS?
      d.Execute(ref dummy);
    }
    defers.Clear();
  }

  static internal void DelInstructions(VM.Frame frm, List<IInstruction> iis)
  {
    for(int i=0;i<iis.Count;++i)
      Instructions.Del(frm, iis[i]);
    iis.Clear();
  }
}

public class SeqInstruction : IInstruction, IExitableScope, IInspectableInstruction
{
  public int ip;
  public int bgn_ip;
  public int end_ip;
  public IInstruction instruction;
  public FixedStack<VM.Context> ctxs = new FixedStack<VM.Context>(256);
  public List<DeferBlock> defers;

  public IList<IInstruction> Browse {
    get {
      if(instruction != null)
        return new List<IInstruction>() { instruction };
      else
        return new List<IInstruction>();
    }
  }

  public void Init(VM.Frame frm, int bgn_ip, int end_ip)
  {
    //Console.WriteLine("NEW SEQ [" + bgn_ip + " " + end_ip + "] " + GetHashCode());
    this.bgn_ip = bgn_ip;
    this.end_ip = end_ip;
    this.ip = bgn_ip;
    ctxs.Push(new VM.Context(frm, bgn_ip-1, end_ip+1));
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = frm.vm.Execute(
      ref ip, ctxs, 
      ref instruction, 
      this
    );
      
    //if the execution didn't "jump out" of the block (e.g. break) proceed to the block end ip
    if(status == BHS.SUCCESS && ip >= bgn_ip && ip <= (end_ip+1))
      frm.fb.ip = end_ip;
  }

  public void Cleanup(VM.Frame frm)
  {
    if(instruction != null)
    {
      Instructions.Del(frm, instruction);
      instruction = null;
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
    DeferBlock.ExitScope(frm, defers);

    //NOTE: Let's release frames which were allocated but due to 
    //      some control flow abruption (e.g paral exited) should be 
    //      explicitely released. We start from index 1 on purpose
    //      since the frame at index 0 will be released 'above'.
    for(int i=1;i<ctxs.Count;i++)
      ctxs[i].frame.Release();
    ctxs.Clear();
  }
}

public class ParalInstruction : IBranchyInstruction, IExitableScope, IInspectableInstruction
{
  public int bgn_ip;
  public int end_ip;
  public int i;
  public List<IInstruction> branches = new List<IInstruction>();
  public List<DeferBlock> defers;

  public IList<IInstruction> Browse {
    get {
      return branches;
    }
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
        Instructions.Del(frm, branch);
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
    DeferBlock.DelInstructions(frm, branches);
    ExitScope(frm);
  }

  public void Attach(IInstruction inst)
  {
    branches.Add(inst);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers);
  }
}

public class ParalAllInstruction : IBranchyInstruction, IExitableScope, IInspectableInstruction
{
  public int bgn_ip;
  public int end_ip;
  public int i;
  public List<IInstruction> branches = new List<IInstruction>();
  public List<DeferBlock> defers;

  public IList<IInstruction> Browse {
    get {
      return branches;
    }
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
        Instructions.Del(frm, branch);
        branches.RemoveAt(i);
        status = BHS.SUCCESS;
        return;
      }
      if(status == BHS.SUCCESS)
      {
        Instructions.Del(frm, branch);
        branches.RemoveAt(i);
      }
      else if(status == BHS.FAILURE)
      {
        Instructions.Del(frm, branch);
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
    DeferBlock.DelInstructions(frm, branches);
    ExitScope(frm);
  }

  public void Attach(IInstruction inst)
  {
    branches.Add(inst);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM.Frame frm)
  {
    DeferBlock.ExitScope(frm, defers);
  }
}

public interface IValRefcounted
{
  void Retain();
  void Release();
}

public class Val
{
  public const byte NONE      = 0;
  public const byte NUMBER    = 1;
  public const byte BOOL      = 2;
  public const byte STRING    = 3;
  public const byte OBJ       = 4;

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

  //NOTE: below members are semi-public, one can use them for 
  //      fast access or non-allocating storage of structs(e.g vectors, quaternions)
  //NOTE: -1 means it's in released state
  public int _refs;
  public byte _type;
  public double _num;
  public object _obj;

  internal VM vm;

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
    _type = NONE;
    _num = 0;
    _obj = null;
  }

  public void ValueCopyFrom(Val dv)
  {
    _type = dv._type;
    _num = dv._num;
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
    else if((op & RefOp.DEC_NO_DEL) != 0)
    {
      if(_refs == -1)
        throw new Exception("Invalid state(-1)");
      else if(_refs == 0)
        throw new Exception("Double free(0)");

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

  static public Val NewStr(VM vm, string s)
  {
    Val dv = New(vm);
    dv.SetStr(s);
    return dv;
  }

  public void SetStr(string s)
  {
    Reset();
    _type = STRING;
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
    _type = NUMBER;
    _num = n;
  }

  static public Val NewNum(VM vm, double n)
  {
    Val dv = New(vm);
    dv.SetNum(n);
    return dv;
  }

  public void SetNum(double n)
  {
    Reset();
    _type = NUMBER;
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
    _type = BOOL;
    _num = b ? 1.0f : 0.0f;
  }

  static public Val NewObj(VM vm, object o)
  {
    Val dv = New(vm);
    dv.SetObj(o);
    return dv;
  }

  public void SetObj(object o)
  {
    Reset();
    _type = OBJ;
    _obj = o;
  }

  static public Val NewNil(VM vm)
  {
    Val dv = New(vm);
    dv.SetNil();
    return dv;
  }

  public void SetNil()
  {
    Reset();
    _type = OBJ;
  }

  public bool IsEqual(Val o)
  {
    bool res =
      _type == o.type &&
      _num == o._num &&
      (_type == STRING ? (string)_obj == (string)o._obj : _obj == o._obj)
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
      str = _obj?.GetType().Name + ":<OBJ>";
    else if(type == NONE)
      str = "<NONE>";
    else
      str = "Val: type:"+type;

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
    else
      throw new Exception("ToAny(): please support type: " + type);
  }
}

public class ValList : IList<Val>, IValRefcounted
{
  //NOTE: exposed to allow manipulations like Reverse(). Use with caution.
  public readonly List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  internal int refs;

  internal VM vm;

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

public class ValDict : IValRefcounted
{
  public Dictionary<ulong, Val> vars = new Dictionary<ulong, Val>();

  //NOTE: -1 means it's in released state,
  //      public only for inspection
  public int refs;

  internal VM vm;

  //NOTE: use New() instead
  internal ValDict(VM vm)
  {
    this.vm = vm;
  }

  public void Retain()
  {
    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    ++refs;

    //Console.WriteLine("FREF INC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release()
  {
    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    if(refs == 0)
      throw new Exception("Double free(0)");

    --refs;

    //Console.WriteLine("FREF DEC: " + refs + " " + this.GetHashCode() + " " + Environment.StackTrace);

    if(refs == 0)
      Del(this);
  }

  public static ValDict New(VM vm)
  {
    ValDict tb;
    if(vm.vdicts_pool.stack.Count == 0)
    {
      ++vm.vdicts_pool.miss;
      tb = new ValDict(vm);
    }
    else
    {
      ++vm.vdicts_pool.hit;
      tb = vm.vdicts_pool.stack.Pop();

      if(tb.refs != -1)
        throw new Exception("Expected to be released, refs " + tb.refs);
    }
    tb.refs = 1;

    return tb;
  }

  static void Del(ValDict tb)
  {
    if(tb.refs != 0)
      throw new Exception("Freeing invalid object, refs " + tb.refs);

    tb.refs = -1;
    tb.Clear();
    tb.vm.vdicts_pool.stack.Push(tb);

    if(tb.vm.vdicts_pool.stack.Count > tb.vm.vdicts_pool.miss)
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

} //namespace bhl

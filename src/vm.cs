//#define DEBUG_REFS
using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class VM
{
  public class Fiber
  {
    internal VM vm;

    internal int id;
    internal int ip;
    internal IInstruction instruction;
    internal FixedStack<Frame> frames = new FixedStack<Frame>(256);

    public FixedStack<Val> stack = new FixedStack<Val>(32);

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
        Instructions.Del(vm, instruction);
        instruction = null;
      }

      for(int i=frames.Count;i-- > 0;)
      {
        var frm = frames[i];
        frm.ExitScope(vm);
        frm.Release();
      }
      frames.Clear();
    }

    public void SetArgs(params Val[] args)
    {
      var frm = frames.Peek();
      for(int i=0;i<args.Length;++i)
      {
        var arg = args[i];
        frm.stack.Push(arg);
      }
    }

    public void GetCallStackInfo(List<VM.CallStackItem> info)
    {
      for(int i=frames.Count;i-- > 0;)
      {
        var frm = frames[i];
        var item = new CallStackItem(); 
        item.module_name = frm.module.name;
        if(i == frames.Count-1)
        {
          item.ip = frm.fb.ip;
          frm.module.ip2src_line.TryGetValue(item.ip, out item.line_num);
          item.func_name = "";
        }
        else
        {
          item.ip = frm.return_ip;
          //frm.module.ip2src_line.TryGetValue(item.ip, out item.line_num);
          //var caller = frames[i-1];
          //caller.module.ip2src_line.TryGetValue(item.ip, out item.line_num);
          var callee = frames[i+1];
          item.func_name = CallStackItem.MapIp2Func(callee.start_ip, callee.module.func2ip);
        }

        info.Add(item);
      }
    }
  }

  public class Frame : IExitableScope, IValRefcounted
  {
    public const int MAX_LOCALS = 64;
    public const int MAX_TEMPS = 32;

    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    public VM vm;
    public Fiber fb;
    public CompiledModule module;

    public byte[] bytecode;
    public List<Const> constants;
    public FixedStack<Val> stack = new FixedStack<Val>(MAX_LOCALS + MAX_TEMPS);
    int _locals_num;
    public int locals_num {
      get {
        return _locals_num; 
      }
      set {
        if(value > MAX_LOCALS)
          throw new Exception("Too many local variables: " + value);
        _locals_num = value; 
      }
    }
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
      this.locals_num = 0;
      stack.Advance(MAX_LOCALS);
    }

    void Clear()
    {
      for(int i=stack.Count;i-- > 0;)
      {
        var val = stack[i];
        if(val != null)
          val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      stack.Clear();
      locals_num = 0;
    }

    public void SetLocal(int idx, Val v)
    {
      if(stack[idx] != null)
      {
        var prev = stack[idx];
        for(int i=0;i<prev._refs;++i)
        {
          v.RefMod(RefOp.USR_INC);
          prev.RefMod(RefOp.USR_DEC);
        }
        prev.ValueCopyFrom(v);
      }
      else
      {
        v.RefMod(RefOp.INC | RefOp.USR_INC);
        stack[idx] = v;
      }
    }

    public void RegisterDefer(DeferBlock cb)
    {
      if(defers == null)
        defers = new List<DeferBlock>();
      defers.Add(cb);
    }

    public void ExitScope(VM vm)
    {
      DeferBlock.ExitScope(vm, defers);
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
      //Console.WriteLine("REL " + GetHashCode());

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      if(refs == 0)
        throw new Exception("Double free(0)");

      --refs;
      if(refs == 0)
        Del(this);
    }
  }

  public struct CallStackItem
  {
    public string module_name;
    public string func_name;
    public int line_num;
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
  internal Fiber curr_fiber;

  IModuleImporter importer;

  public delegate void ClassCreator(ref Val res);
  public delegate void FieldGetter(Val v, ref Val res);
  public delegate void FieldSetter(ref Val v, Val nv);
  public delegate void FieldRef(Val v, out Val res);

  public class ValPool : Pool<Val>
  {
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

  public Fiber Start(string func)
  {
    ModuleAddr addr;
    if(!func2addr.TryGetValue(func, out addr))
      return null;

    var fb = Fiber.New(this);
    var fr = Frame.New(this);
    fr.Init(fb, addr.module, addr.ip);
    Register(fb, fr);
    return fb;
  }

  public Fiber Start(Frame fr)
  {
    var fb = Fiber.New(this);
    Register(fb, fr);
    return fb;
  }

  void Register(Fiber fb, Frame fr)
  {
    fb.id = ++fibers_ids;
    fb.ip = fr.start_ip;

    fr.fb = fb;
    fb.frames.Push(fr);

    fibers.Add(fb);
  }

  public void Stop(Fiber fb)
  {
    Fiber.Del(fb);
    fibers.Remove(fb);
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

  internal BHS Execute(ref int ip, Frame curr_frame, FixedStack<Frame> frames, ref IInstruction instruction, int max_ip, IExitableScope defer_scope)
  { 
    while(curr_frame != null && ip < max_ip)
    {
      //Console.WriteLine("EXECUTE " + frames.Count + ", IP " + ip + " MAX " + max_ip);

      var status = BHS.SUCCESS;

      //NOTE: if there's an active instruction it has priority over simple 'code following' via ip
      if(instruction != null)
      {
        instruction.Tick(curr_frame, ref status);

        if(status == BHS.RUNNING)
          return status;
        else if(status == BHS.FAILURE)
        {
          Instructions.Del(this, instruction);
          instruction = null;

          curr_frame.ExitScope(this);
          ip = curr_frame.return_ip;
          curr_frame.Release();
          frames.Pop();
          return status;
        }
        else
        {
          Instructions.Del(this, instruction);
          instruction = null;
        }
      }

      {
        var opcode = (Opcodes)curr_frame.bytecode[ip];
        //Console.WriteLine("OP " + opcode + " @ " + string.Format("0x{0:x2} {0} {1}", ip, curr_frame.module.name));
        switch(opcode)
        {
          case Opcodes.Nop:
          break;
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

            //TODO: make it more universal and robust
            if(cast_type == "string")
              curr_frame.stack.Push(Val.NewStr(this, curr_frame.stack.PopRelease().num.ToString()));
            else if(cast_type == "int")
              curr_frame.stack.Push(Val.NewNum(this, curr_frame.stack.PopRelease().num));
            else
              throw new Exception("Not supported typecast type: " + cast_type);
          }
          break;
          case Opcodes.Inc:
          {
            int var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            ++curr_frame.stack[var_idx]._num;
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
          case Opcodes.Less:
          case Opcodes.LessOrEqual:
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
            var stack = curr_frame.stack;
            if(stack[local_idx] != null)
              stack[local_idx].Release();
            var new_val = stack.Pop();
            stack[local_idx] = new_val;
          }
          break;
          case Opcodes.GetVar:
          {
            int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            curr_frame.stack.PushRetain(curr_frame.stack[local_idx]);
          }
          break;
          case Opcodes.ArgVar:
          {
            int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            curr_frame.stack[local_idx] = curr_frame.stack.Pop();
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
            curr_frame.stack[local_idx] = v;
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
            curr_frame.stack.Push(res);
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
            curr_frame.ExitScope(this);
            ip = curr_frame.return_ip;
            curr_frame.Release();
            //Console.WriteLine("RET IP " + ip + " FRAMES " + frames.Count);
            frames.Pop();
            if(frames.Count > 0)
              curr_frame = frames.Peek();
            else 
              curr_frame = null;
          }
          break;
          case Opcodes.ReturnVal:
          {
            var ret_val = curr_frame.stack.Pop();
            ip = curr_frame.return_ip;
            curr_frame.ExitScope(this);
            curr_frame.Release();
            //Console.WriteLine("RETVAL IP " + ip + " FRAMES " + frames.Count + " " + curr_frame.GetHashCode());
            frames.Pop();
            if(frames.Count > 0)
            {
              curr_frame = frames.Peek();
              curr_frame.stack.Push(ret_val);
            }
            else
            {
              curr_frame.fb.stack.Push(ret_val);
              curr_frame = null;
            }
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
            curr_frame.stack.Push(Val.NewObj(this, func_symb));
          }
          break;
          case Opcodes.GetMethodNative:
          {
            int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);

            int class_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
            string class_type = curr_frame.constants[class_type_idx].str; 

            var class_symb = (ClassSymbol)symbols.Resolve(class_type);
            var func_symb = (FuncSymbolNative)class_symb.members[func_idx];
            curr_frame.stack.Push(Val.NewObj(this, func_symb));
          }
          break;
          case Opcodes.GetFuncFromVar:
          {
            int local_var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            var val = curr_frame.stack[local_var_idx];
            var frm = (Frame)val._obj; 
            //NOTE: we need to call an extra Retain since Release will be called for this frame 
            //      during its execution of Opcode.Return, however since this frame is stored in a var 
            //      and this var will be released at some point we want to avoid 'double free' situation 
            frm.Retain();
            curr_frame.stack.Push(Val.NewObj(this, frm));
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
          case Opcodes.Call:
          {
            uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 

            var val = curr_frame.stack.Pop();
            var fr = (Frame)val._obj;
            //NOTE: it will be released once return is invoked
            fr.Retain();
            val.Release();

            var args_info = new FuncArgsInfo(args_bits);
            for(int i = 0; i < args_info.CountArgs(); ++i)
              fr.stack.Push(curr_frame.stack.Pop());
            if(args_info.HasDefaultUsedArgs())
              fr.stack.Push(Val.NewNum(this, args_bits));

            //let's remember ip to return to
            fr.return_ip = ip;
            frames.Push(fr);
            curr_frame = fr;
            //since ip will be incremented below we decrement it intentionally here
            ip = fr.start_ip - 1; 
          }
          break;
          case Opcodes.CallNative:
          {
            uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref ip); 
            var val = curr_frame.stack.PopRelease();
            var func_symb = (FuncSymbolNative)val._obj;

            var args_info = new FuncArgsInfo(args_bits);
            for(int i = 0; i < args_info.CountArgs(); ++i)
              curr_frame.stack.Push(curr_frame.stack.Pop());

            var sub_instruction = func_symb.VM_cb(curr_frame, ref status);
            if(sub_instruction != null)
              AttachInstruction(ref instruction, sub_instruction);
            //NOTE: checking if new instruction was added and if so executing it immediately
            if(instruction != null)
              instruction.Tick(curr_frame, ref status);
          }
          break;
          case Opcodes.InitFrame:
          {
            int local_vars_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            curr_frame.locals_num = local_vars_num;
          }
          break;
          case Opcodes.Lambda:
          {
            int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref ip);
            int local_vars_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

            var fr = Frame.New(this);
            fr.Init(curr_frame, func_ip);
            fr.locals_num = local_vars_num;
            var frval = Val.NewObj(this, fr);
            frval._num = func_ip;

            curr_frame.stack.Push(frval);
          }
          break;
          case Opcodes.UseUpval:
          {
            int up_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref ip);

            var frval = curr_frame.stack.Peek();
            var fr = (Frame)frval._obj;

            //TODO: amount of local variables must be known ahead 
            int gaps = local_idx - fr.locals_num + 1;
            for(int i=0;i<gaps;++i)
              fr.stack[fr.locals_num + i] = Val.New(this); 
            fr.locals_num += gaps;

            var up_val = curr_frame.stack[up_idx];
            up_val.Retain();

            if(fr.stack[local_idx] != null)
              fr.stack[local_idx].Release();
            fr.stack[local_idx] = up_val;
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
            //we need to jump only in case of false
            if(curr_frame.stack.PopRelease().bval == false)
            {
              ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref ip);
              ip += offset;
            }
            else
              ++ip;
          }
          break;
          case Opcodes.DefArg:
          {
            byte def_arg_idx = (byte)Bytecode.Decode8(curr_frame.bytecode, ref ip);
            int jump_pos = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
            var args_info = new FuncArgsInfo((uint)curr_frame.stack[curr_frame.locals_num-1]._num);
            //Console.WriteLine("DEF ARG: " + def_arg_idx + ", jump pos " + jump_pos + ", used " + args_info.IsDefaultArgUsed(def_arg_idx));
            //NOTE: if default argument is not used we need to jump out of default argument calculation code
            if(!args_info.IsDefaultArgUsed(def_arg_idx))
              ip += jump_pos;
          }
          break;
          case Opcodes.Block:
          {
            VisitBlock(ref ip, curr_frame, ref instruction, defer_scope);
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
      }

      if(status == BHS.RUNNING || status == BHS.FAILURE)
        return status;
      else if(status == BHS.SUCCESS)
        ++ip;
    }

    return BHS.SUCCESS;
  }

  void HandleNew(Frame curr_frame, string class_type)
  {
    var cls = symbols.Resolve(class_type) as ClassSymbol;
    //TODO: this check must be in dev.version only
    if(cls == null)
      throw new Exception("Could not find class symbol: " + class_type);

    var val = Val.New(this); 
    cls.VM_creator(ref val);
    curr_frame.stack.Push(val);
  }

  static void AttachInstruction(ref IInstruction instruction, IInstruction candidate)
  {
    if(instruction != null)
    {
      if(instruction is IMultiInstruction mi)
        mi.Attach(candidate);
      else
        throw new Exception("Can't attach to current instruction");
    }
    else
      instruction = candidate;
  }

  IInstruction VisitBlock(ref int ip, Frame curr_frame, ref IInstruction instruction, IExitableScope defer_scope)
  {
    var type = (EnumBlock)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    int size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);

    if(type == EnumBlock.PARAL || type == EnumBlock.PARAL_ALL) 
    {
      IMultiInstruction paral = null;
      if(type == EnumBlock.PARAL)
        paral = Instructions.New<ParalInstruction>(this);
      else
        paral = Instructions.New<ParalAllInstruction>(this);

      AttachInstruction(ref instruction, paral);
      int tmp_ip = ip;
      while(tmp_ip < (ip + size))
      {
        ++tmp_ip;
        var opcode = (Opcodes)curr_frame.bytecode[tmp_ip]; 
        if(opcode != Opcodes.Block)
          throw new Exception("Expected PushBlock got " + opcode);
        IInstruction dummy = null;
        var sub = VisitBlock(ref tmp_ip, curr_frame, ref dummy, (IExitableScope)paral);
        if(sub != null)
          paral.Attach(sub);
      }
      ip += size;
      return paral;
    }
    else if(type == EnumBlock.SEQ)
    {
      var seq = Instructions.New<SeqInstruction>(this);
      seq.Init(curr_frame, ip + 1, ip + size);

      AttachInstruction(ref instruction, seq);
      ip += size;
      return seq;
    }
    else if(type == EnumBlock.DEFER)
    {
      var cb = new DeferBlock(curr_frame, ip + 1, ip + size);
      if(defer_scope != null)
        defer_scope.RegisterDefer(cb);
      else 
        curr_frame.RegisterDefer(cb);
      ip += size;
      return null;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }

  public BHS Tick()
  {
    for(int i=0;i<fibers.Count;)
    {
      curr_fiber = fibers[i];

      var status = Execute(ref curr_fiber.ip, curr_fiber.frames.Peek(), curr_fiber.frames, ref curr_fiber.instruction, int.MaxValue, null);
      
      if(status != BHS.RUNNING)
        Stop(curr_fiber);
      else
        ++i;
    }

    return fibers.Count == 0 ? BHS.SUCCESS : BHS.RUNNING;
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
      case Opcodes.Less:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num < r_operand._num));
      break;
      case Opcodes.LessOrEqual:
        curr_frame.stack.Push(Val.NewBool(this, l_operand._num <= r_operand._num));
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

  public CompiledModule(ModuleCompiler c)
    : this(
        c.Module.name, 
        c.GetByteCode(), 
        c.Constants, 
        c.Func2Ip, 
        c.GetInitCode(),
        c.Ip2SrcLine
      )
  {}
}

public interface IInstruction
{
  void Tick(VM.Frame frm, ref BHS status);
  void Cleanup(VM vm);
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

    //Console.WriteLine("NEW " + typeof(T).Name + " " + inst.GetHashCode());

    return (T)inst;
  }

  static public void Del(VM vm, IInstruction inst)
  {
    //Console.WriteLine("DEL " + inst.GetType().Name + " " + inst.GetHashCode());

    inst.Cleanup(vm);

    var t = inst.GetType();

    VM.Pool<IInstruction> pool;
    //ignoring instructions whch were not allocated via pool 
    if(!vm.instr_pool.all.TryGetValue(t, out pool))
      return;

    pool.stack.Push(inst);

    if(pool.stack.Count > pool.miss)
      throw new Exception("Unbalanced New/Del " + pool.stack.Count + " " + pool.miss);
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
  void ExitScope(VM vm);
}

public interface IMultiInstruction : IInstruction
{
  void Attach(IInstruction ex);
}

class CoroutineSuspend : IInstruction
{
  public static readonly IInstruction Instance = new CoroutineSuspend();

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = BHS.RUNNING;
  }

  public void Cleanup(VM vm)
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

  public void Cleanup(VM vm)
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

  public BHS Execute(VM vm, ref IInstruction instruction)
  {
    return vm.Execute(ref ip, frm, null, ref instruction, max_ip + 1, null);
  }

  static internal void ExitScope(VM vm, List<DeferBlock> defers)
  {
    if(defers == null)
      return;

    for(int i=defers.Count;i-- > 0;)
    {
      var d = defers[i];
      IInstruction dummy = null;
      //TODO: do we need ensure that status is SUCCESS?
      d.Execute(vm, ref dummy);
    }
    defers.Clear();
  }

  static internal void DelInstructions(VM vm, List<IInstruction> iis)
  {
    for(int i=0;i<iis.Count;++i)
      Instructions.Del(vm, iis[i]);
    iis.Clear();
  }
}

public class SeqInstruction : IInstruction, IExitableScope
{
  public int ip;
  public int max_ip;
  public FixedStack<VM.Frame> frames = new FixedStack<VM.Frame>(256);
  public IInstruction instruction;
  public List<DeferBlock> defers;

  public void Init(VM.Frame frm, int ip, int max_ip)
  {
    //Console.WriteLine("NEW SEQ " + ip + " " + max_ip + " " + GetHashCode());
    this.ip = ip;
    this.max_ip = max_ip;
    frames.Push(frm);
  }

  public void Tick(VM.Frame frm, ref BHS status)
  {
    //Console.WriteLine("TICK SEQ " + ip + " " + GetHashCode());
    status = frm.vm.Execute(ref ip, frames.Peek(), frames, ref instruction, max_ip + 1, this);
  }

  public void Cleanup(VM vm)
  {
    if(instruction != null)
    {
      Instructions.Del(vm, instruction);
      instruction = null;
    }

    ExitScope(vm);
  }

  public void RegisterDefer(DeferBlock cb)
  {
    if(defers == null)
      defers = new List<DeferBlock>();
    defers.Add(cb);
  }

  public void ExitScope(VM vm)
  {
    DeferBlock.ExitScope(vm, defers);

    //NOTE: Let's release frames which were allocated but due to 
    //      some control flow abruption (e.g paral exited) should be 
    //      explicitely released. We start from index 1 on purpose
    //      since the frame at index 0 will be released as expected.
    for(int i=1;i<frames.Count;i++)
      frames[i].Release();
    frames.Clear();
  }
}

public class ParalInstruction : IMultiInstruction, IExitableScope
{
  public List<IInstruction> branches = new List<IInstruction>();
  public List<DeferBlock> defers;

  public void Tick(VM.Frame frm, ref BHS status)
  {
    status = BHS.RUNNING;

    for(int i=0;i<branches.Count;++i)
    {
      var branch = branches[i];
      branch.Tick(frm, ref status);
      //Console.WriteLine("CHILD " + i + " " + status + " " + child.GetType().Name);
      if(status != BHS.RUNNING)
      {
        Instructions.Del(frm.vm, branch);
        branches.RemoveAt(i);
        break;
      }
    }
  }

  public void Cleanup(VM vm)
  {
    DeferBlock.DelInstructions(vm, branches);
    ExitScope(vm);
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

  public void ExitScope(VM vm)
  {
    DeferBlock.ExitScope(vm, defers);
  }
}

public class ParalAllInstruction : IMultiInstruction, IExitableScope
{
  public List<IInstruction> branches = new List<IInstruction>();
  public List<DeferBlock> defers;

  public void Tick(VM.Frame frm, ref BHS status)
  {
    for(int i=0;i<branches.Count;)
    {
      var branch = branches[i];
      branch.Tick(frm, ref status);
      if(status == BHS.SUCCESS)
      {
        Instructions.Del(frm.vm, branch);
        branches.RemoveAt(i);
      }
      else if(status == BHS.FAILURE)
      {
        Instructions.Del(frm.vm, branch);
        branches.RemoveAt(i);
        return;
      }
      else
        ++i;
    }

    if(branches.Count > 0)
      status = BHS.RUNNING;
  }

  public void Cleanup(VM vm)
  {
    DeferBlock.DelInstructions(vm, branches);
    ExitScope(vm);
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

  public void ExitScope(VM vm)
  {
    DeferBlock.ExitScope(vm, defers);
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
      throw new Exception("Invalid state(-1)");
    ++refs;
    //Console.WriteLine("RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
  }

  public void Release()
  {
    if(refs == -1)
      throw new Exception("Invalid state(-1)");
    if(refs == 0)
      throw new Exception("Double free(0)");

    --refs;
    //Console.WriteLine("RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
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

using System;

namespace bhl {
  
public enum UpvalMode
{
  STRONG = 0,
  COPY   = 1
}
    
public partial class VM : INamedResolver
{
  //NOTE: why -2? we reserve some space before int.MaxValue so that
  //      increasing some ip couple of times after it was assigned
  //      a 'STOP_IP' value won't overflow int.MaxValue
  public const int STOP_IP = int.MaxValue - 2;

  public delegate void ClassCreator(VM.Frame frm, ref Val res, IType type);
  
  public struct Region
  {
    public Frame frame;
    public IDeferSupport defer_support;
    //NOTE: if current ip is not within *inclusive* range of these values
    //      the frame context execution is considered to be done
    public int min_ip;
    public int max_ip;

    public Region(Frame frame, IDeferSupport defer_support, int min_ip = -1, int max_ip = STOP_IP)
    {
      this.frame = frame;
      this.defer_support = defer_support;
      this.min_ip = min_ip;
      this.max_ip = max_ip;
    }
  }

  public class ExecState
  {
    internal int ip;
    internal Coroutine coroutine;
    internal FixedStack<Region> regions = new FixedStack<Region>(32);
    internal FixedStack<Frame> frames = new FixedStack<Frame>(256);
    public ValStack stack;
  }
  
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
  
  internal BHS Execute(ExecState exec, int exec_waterline_idx = 0)
  {
    var status = BHS.SUCCESS;
    while(exec.regions.Count > exec_waterline_idx && status == BHS.SUCCESS)
    {
      status = ExecuteOnce(exec);
    }
    return status;
  }

  BHS ExecuteOnce(ExecState exec)
  {
    var item = exec.regions.Peek();

    var curr_frame = item.frame;

#if BHL_TDEBUG
    Console.WriteLine("EXEC TICK " + curr_frame.fb.tick + " " + exec.GetHashCode() + ":" + exec.regions.Count + ":" + exec.frames.Count + " (" + curr_frame.GetHashCode() + "," + curr_frame.fb.id + ") IP " + exec.ip + "(min:" + item.min_ip + ", max:" + item.max_ip + ")" + (exec.ip > -1 && exec.ip < curr_frame.bytecode.Length ? " OP " + (Opcodes)curr_frame.bytecode[exec.ip] : " OP ? ") + " CORO " + exec.coroutine?.GetType().Name + "(" + exec.coroutine?.GetHashCode() + ")" + " DEFERABLE " + item.defer_support?.GetType().Name + "(" + item.defer_support?.GetHashCode() + ") " + curr_frame.bytecode.Length /* + " " + curr_frame.fb.GetStackTrace()*/ /* + " " + Environment.StackTrace*/);
#endif

    //NOTE: if there's an active coroutine it has priority over simple 'code following' via ip
    if(exec.coroutine != null)
      return ExecuteCoroutine(curr_frame, exec);

    if(exec.ip < item.min_ip || exec.ip > item.max_ip)
    {
      exec.regions.Pop();
      return BHS.SUCCESS;
    }

    var opcode = (Opcodes)curr_frame.bytecode[exec.ip];
    //Console.WriteLine("OP " + opcode + " " + ip);
    switch(opcode)
    {
      case Opcodes.Constant:
      {
        int const_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var cn = curr_frame.constants[const_idx];
        var cv = cn.ToVal(this);
        exec.stack.Push(cv);
      }
      break;
      case Opcodes.TypeCast:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        bool force_type = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip) == 1;

        var cast_type = curr_frame.type_refs[cast_type_idx].Get();

        HandleTypeCast(exec, cast_type, force_type);
      }
      break;
      case Opcodes.TypeAs:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var as_type = curr_frame.type_refs[cast_type_idx].Get();
        bool force_type = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip) == 1;

        HandleTypeAs(exec, as_type, force_type);
      }
      break;
      case Opcodes.TypeIs:
      {
        int cast_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var as_type = curr_frame.type_refs[cast_type_idx].Get();

        HandleTypeIs(exec, as_type);
      }
      break;
      case Opcodes.Typeof:
      {
        int type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var type = curr_frame.type_refs[type_idx].Get();

        exec.stack.Push(Val.NewObj(this, type, Types.Type));
      }
      break;
      case Opcodes.Inc:
      {
        int var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        ++curr_frame.locals[var_idx]._num;
      }
      break;
      case Opcodes.Dec:
      {
        int var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        --curr_frame.locals[var_idx]._num;
      }
      break;
      case Opcodes.ArrIdx:
      {
        var self = exec.stack[exec.stack.Count - 2];
        var class_type = ((ArrayTypeSymbol)self.type);
        
        int idx = (int)exec.stack.PopRelease().num;
        var arr = exec.stack.Pop();
        
        var res = class_type.ArrGetAt(arr, idx);
        
        exec.stack.Push(res);
        arr.Release();
      }
      break;
      case Opcodes.ArrIdxW:
      {
        var self = exec.stack[exec.stack.Count - 2];
        var class_type = ((ArrayTypeSymbol)self.type);
        
        int idx = (int)exec.stack.PopRelease().num;
        var arr = exec.stack.Pop();
        var val = exec.stack.Pop();

        class_type.ArrSetAt(arr, idx, val);
        
        val.Release();
        arr.Release();
      }
      break;
      case Opcodes.ArrAddInplace:
      {
        var self = exec.stack[exec.stack.Count - 2];
        self.Retain();
        var class_type = ((ArrayTypeSymbol)self.type);
        var status = BHS.SUCCESS;
        //NOTE: Add must be at 0 index
        ((FuncSymbolNative)class_type._all_members[0]).cb(curr_frame, exec.stack, new FuncArgsInfo(), ref status);
        exec.stack.Push(self);
      }
      break;
      case Opcodes.MapIdx:
      {
        var self = exec.stack[exec.stack.Count - 2];
        var class_type = ((MapTypeSymbol)self.type);
        
        var key = exec.stack.Pop();
        var map = exec.stack.Pop();
        
        class_type.MapTryGet(map, key, out var res);
        
        exec.stack.PushRetain(res);
        key.Release();
        map.Release();
      }
      break;
      case Opcodes.MapIdxW:
      {
        var self = exec.stack[exec.stack.Count - 2];
        var class_type = ((MapTypeSymbol)self.type);
        
        var key = exec.stack.Pop();
        var map = exec.stack.Pop();
        var val = exec.stack.Pop();
        
        class_type.MapSet(map, key, val);
        
        key.Release();
        val.Release();
        map.Release();
      }
      break;
      case Opcodes.MapAddInplace:
      {
        var self = exec.stack[exec.stack.Count - 3];
        self.Retain();
        var class_type = ((MapTypeSymbol)self.type);
        var status = BHS.SUCCESS;
        //NOTE: Add must be at 0 index
        ((FuncSymbolNative)class_type._all_members[0]).cb(curr_frame, exec.stack, new FuncArgsInfo(), ref status);
        exec.stack.Push(self);
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
        ExecuteBinaryOp(opcode, exec.stack);
      }
      break;
      case Opcodes.UnaryNot:
      case Opcodes.UnaryNeg:
      {
        ExecuteUnaryOp(opcode, exec.stack);
      }
      break;
      case Opcodes.GetVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        exec.stack.PushRetain(curr_frame.locals[local_idx]);
      }
      break;
      case Opcodes.SetVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        var new_val = exec.stack.Pop();
        curr_frame.locals.Assign(this, local_idx, new_val);
        new_val.Release();
      }
      break;
      case Opcodes.ArgVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        var arg_val = exec.stack.Pop();
        var loc_var = Val.New(this);
        loc_var.ValueCopyFrom(arg_val);
        loc_var.RefMod(RefOp.USR_INC);
        curr_frame.locals[local_idx] = loc_var;
        arg_val.Release();
      }
      break;
      case Opcodes.ArgRef:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        curr_frame.locals[local_idx] = exec.stack.Pop();
      }
      break;
      case Opcodes.DeclVar:
      {
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        int type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var type = curr_frame.type_refs[type_idx].Get();

        var curr = curr_frame.locals[local_idx];
        //NOTE: handling case when variables are 're-declared' within the nested loop
        if(curr != null)
          curr.Release();
        curr_frame.locals[local_idx] = MakeDefaultVal(type);
      }
      break;
      case Opcodes.GetAttr:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var obj = exec.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var res = Val.New(this);
        var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
        field_symb.getter(curr_frame, obj, ref res, field_symb);
        //NOTE: we retain only the payload since we make the copy of the value
        //      and the new res already has refs = 1 while payload's refcount
        //      is not incremented
        res.RefMod(RefOp.USR_INC);
        exec.stack.Push(res);
        obj.Release();
      }
      break;
      case Opcodes.RefAttr:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var obj = exec.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
        Val res;
        field_symb.getref(curr_frame, obj, out res, field_symb);
        exec.stack.PushRetain(res);
        obj.Release();
      }
      break;
      case Opcodes.SetAttr:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);

        var obj = exec.stack.Pop();
        var class_symb = (ClassSymbol)obj.type;
        var val = exec.stack.Pop();
        var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
        field_symb.setter(curr_frame, ref obj, val, field_symb);
        val.Release();
        obj.Release();
      }
      break;
      case Opcodes.SetAttrInplace:
      {
        int fld_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var val = exec.stack.Pop();
        var obj = exec.stack.Peek();
        var class_symb = (ClassSymbol)obj.type;
        var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
        field_symb.setter(curr_frame, ref obj, val, field_symb);
        val.Release();
      }
      break;
      case Opcodes.GetGVar:
      {
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);

        exec.stack.PushRetain(curr_frame.module.gvar_vals[var_idx]);
      }
      break;
      case Opcodes.SetGVar:
      {
        int var_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);

        var new_val = exec.stack.Pop();
        curr_frame.module.gvar_vals.Assign(this, var_idx, new_val);
        new_val.Release();
      }
      break;
      case Opcodes.ExitFrame:
      {
        exec.ip = curr_frame.return_ip;
        exec.stack = curr_frame.origin_stack;
        curr_frame.ExitScope(null, exec);
        curr_frame.Release();
        exec.frames.Pop();
        exec.regions.Pop();
      }
      break;
      case Opcodes.Return:
      {
        exec.ip = curr_frame.exit_ip - 1;
      }
      break;
      case Opcodes.ReturnVal:
      {
        int ret_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);

        int stack_offset = exec.stack.Count;
        for(int i=0;i<ret_num;++i)
          curr_frame.origin_stack.Push(exec.stack[stack_offset-ret_num+i]);
        exec.stack.head -= ret_num;

        exec.ip = curr_frame.exit_ip - 1;
      }
      break;
      case Opcodes.PopReturnVals:
      {
        for(int i=0;i<exec.stack.Count;++i)
          curr_frame.origin_stack.Push(exec.stack[i]);
        exec.stack.head = 0;
      }
      break;
      case Opcodes.GetFuncLocalPtr:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);

        var func_symb = curr_frame.module.func_index.index[func_idx];

        var ptr = FuncPtr.New(this);
        ptr.Init(curr_frame.module, func_symb.ip_addr);
        exec.stack.Push(Val.NewObj(this, ptr, func_symb.signature));
      }
      break;
      case Opcodes.GetFuncPtr:
      {
        int import_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);

        var func_mod = curr_frame.module._imported[import_idx];
        var func_symb = func_mod.func_index.index[func_idx];

        var ptr = FuncPtr.New(this);
        ptr.Init(func_mod, func_symb.ip_addr);
        exec.stack.Push(Val.NewObj(this, ptr, func_symb.signature));
      }
      break;
      case Opcodes.GetFuncNativePtr:
      {
        int import_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);

        //NOTE: using convention where built-in global module is always at index 0
        //      and imported modules are at (import_idx + 1)
        var func_mod = import_idx == 0 ? types.module : curr_frame.module._imported[import_idx-1];
        var nfunc_symb = func_mod.nfunc_index.index[func_idx];

        var ptr = FuncPtr.New(this);
        ptr.Init(nfunc_symb);
        exec.stack.Push(Val.NewObj(this, ptr, nfunc_symb.signature));
      }
      break;
      case Opcodes.GetFuncPtrFromVar:
      {
        int local_var_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        var val = curr_frame.locals[local_var_idx];
        val.Retain();
        exec.stack.Push(val);
      }
      break;
      case Opcodes.LastArgToTop:
      {
        //NOTE: we need to move arg (e.g. func ptr) to the top of the stack
        //      so that it fullfills Opcode.Call requirements
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
        int arg_idx = exec.stack.Count - args_num - 1;
        var arg = exec.stack[arg_idx];
        exec.stack.RemoveAt(arg_idx);
        exec.stack.Push(arg);
      }
      break;
      case Opcodes.CallLocal:
      {
        int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        var frm = Frame.New(this);
        frm.Init(curr_frame, exec.stack, func_ip);
        Call(curr_frame, exec, frm, args_bits);
      }
      break;
      case Opcodes.CallGlobNative:
      {
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        var nfunc_symb = types.module.nfunc_index[func_idx];

        BHS status;
        if(CallNative(curr_frame, exec.stack, nfunc_symb, args_bits, out status, ref exec.coroutine))
          return status;
      }
      break;
      case Opcodes.CallNative:
      {
        int import_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int func_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        //NOTE: using convention where built-in global module is always at index 0
        //      and imported modules are at (import_idx + 1)
        var func_mod = import_idx == 0 ? types.module : curr_frame.module._imported[import_idx-1];
        var nfunc_symb = func_mod.nfunc_index[func_idx];

        BHS status;
        if(CallNative(curr_frame, exec.stack, nfunc_symb, args_bits, out status, ref exec.coroutine))
          return status;
      }
      break;
      case Opcodes.Call:
      {
        int import_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int func_ip = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        var func_mod = curr_frame.module._imported[import_idx];

        var frm = Frame.New(this);
        frm.Init(curr_frame.fb, curr_frame, exec.stack, func_mod, func_ip);
        Call(curr_frame, exec, frm, args_bits);
      }
      break;
      case Opcodes.CallMethod:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        //TODO: use a simpler schema where 'self' is passed on the top
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
        int self_idx = exec.stack.Count - args_num - 1;
        var self = exec.stack[self_idx];
        exec.stack.RemoveAt(self_idx);

        var class_type = (ClassSymbolScript)self.type;
        var func_symb = (FuncSymbolScript)class_type._all_members[func_idx];

        var frm = Frame.New(this);
        frm.Init(curr_frame.fb, curr_frame, exec.stack, func_symb._module, func_symb.ip_addr);

        frm.locals.head = 1;
        frm.locals[0] = self;

        Call(curr_frame, exec, frm, args_bits);
      }
      break;
      case Opcodes.CallMethodNative:
      {
        int func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
        int self_idx = exec.stack.Count - args_num - 1;
        var self = exec.stack[self_idx];

        var class_type = (ClassSymbol)self.type;
        var func_symb = (FuncSymbolNative)class_type._all_members[func_idx];

        BHS status;
        if(CallNative(curr_frame, exec.stack, func_symb, args_bits, out status, ref exec.coroutine))
          return status;
      }
      break;
      case Opcodes.CallMethodVirt:
      {
        int virt_func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        //TODO: use a simpler schema where 'self' is passed on the top
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
        int self_idx = exec.stack.Count - args_num - 1;
        var self = exec.stack[self_idx];
        exec.stack.RemoveAt(self_idx);

        var class_type = (ClassSymbol)self.type;
        var func_symb = (FuncSymbolScript)class_type._vtable[virt_func_idx];

        var frm = Frame.New(this);
        frm.Init(curr_frame.fb, curr_frame, exec.stack, func_symb._module, func_symb.ip_addr);

        frm.locals.head = 1;
        frm.locals[0] = self;

        Call(curr_frame, exec, frm, args_bits);
      }
      break;
      case Opcodes.CallMethodIface:
      {
        int iface_func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int iface_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        //TODO: use a simpler schema where 'self' is passed on the top
        int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
        int self_idx = exec.stack.Count - args_num - 1;
        var self = exec.stack[self_idx];
        exec.stack.RemoveAt(self_idx);

        var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx].Get();
        var class_type = (ClassSymbol)self.type;
        var func_symb = (FuncSymbolScript)class_type._itable[iface_symb][iface_func_idx];

        var frm = Frame.New(this);
        frm.Init(curr_frame.fb, curr_frame, exec.stack, func_symb._module, func_symb.ip_addr);

        frm.locals.head = 1;
        frm.locals[0] = self;

        Call(curr_frame, exec, frm, args_bits);
      }
      break;
      case Opcodes.CallMethodIfaceNative:
      {
        int iface_func_idx = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        int iface_type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        var iface_symb = (InterfaceSymbol)curr_frame.type_refs[iface_type_idx].Get();
        var func_symb = (FuncSymbolNative)iface_symb.members[iface_func_idx];

        BHS status;
        if(CallNative(curr_frame, exec.stack, func_symb, args_bits, out status, ref exec.coroutine))
          return status;
      }
      break;
      case Opcodes.CallFuncPtr:
      {
        uint args_bits = Bytecode.Decode32(curr_frame.bytecode, ref exec.ip);

        var val_ptr = exec.stack.Pop();
        var ptr = (FuncPtr)val_ptr._obj;

        //checking if it's a native call
        if(ptr.native != null)
        {
          BHS status;
          bool return_status = CallNative(curr_frame, exec.stack, ptr.native, args_bits, out status, ref exec.coroutine);
          val_ptr.Release();
          if(return_status)
            return status;
        }
        else
        {
          var frm = ptr.MakeFrame(this, curr_frame, exec.stack);
          val_ptr.Release();
          Call(curr_frame, exec, frm, args_bits);
        }
      }
      break;
      case Opcodes.InitFrame:
      {
        int local_vars_num = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        int frame_end_offset = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        curr_frame.exit_ip = exec.ip + frame_end_offset;
        var args_bits = exec.stack.Pop();
        curr_frame.locals.Resize(local_vars_num);
        //NOTE: we need to store arg info bits locally so that
        //      this information will be available to func
        //      args related opcodes
        curr_frame.locals[local_vars_num-1] = args_bits;
      }
      break;
      case Opcodes.Lambda:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var ptr = FuncPtr.New(this);
        ptr.Init(curr_frame, exec.ip+1);
        exec.stack.Push(Val.NewObj(this, ptr, Types.Any/*TODO: should be a FuncPtr type*/));

        exec.ip += offset;
      }
      break;
      case Opcodes.UseUpval:
      {
        int up_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        int local_idx = (int)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        var mode = (UpvalMode)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);

        var addr = (FuncPtr)exec.stack.Peek()._obj;

        //TODO: amount of local variables must be known ahead and
        //      initialized during Frame initialization
        //NOTE: we need to reflect the updated max amount of locals,
        //      otherwise they might not be cleared upon Frame exit
        addr.upvals.Resize(local_idx+1);

        var upval = curr_frame.locals[up_idx];
        if(mode == UpvalMode.COPY)
        {
          var copy = Val.New(this);
          copy.ValueCopyFrom(upval);
          addr.upvals[local_idx] = copy;
        }
        else
        {
          upval.RefMod(RefOp.USR_INC | RefOp.INC);
          addr.upvals[local_idx] = upval;
        }
      }
      break;
      case Opcodes.Pop:
      {
        exec.stack.PopRelease();
      }
      break;
      case Opcodes.Jump:
      {
        short offset = (short)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        exec.ip += offset;
      }
      break;
      case Opcodes.JumpZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        if(exec.stack.PopRelease().bval == false)
          exec.ip += offset;
      }
      break;
      case Opcodes.JumpPeekZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var v = exec.stack.Peek();
        if(v.bval == false)
          exec.ip += offset;
      }
      break;
      case Opcodes.JumpPeekNZ:
      {
        ushort offset = Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        var v = exec.stack.Peek();
        if(v.bval == true)
          exec.ip += offset;
      }
      break;
      case Opcodes.DefArg:
      {
        byte def_arg_idx = (byte)Bytecode.Decode8(curr_frame.bytecode, ref exec.ip);
        int jump_pos = (int)Bytecode.Decode16(curr_frame.bytecode, ref exec.ip);
        uint args_bits = (uint)curr_frame.locals[curr_frame.locals.Count-1]._num;
        var args_info = new FuncArgsInfo(args_bits);
        //Console.WriteLine("DEF ARG: " + def_arg_idx + ", jump pos " + jump_pos + ", used " + args_info.IsDefaultArgUsed(def_arg_idx) + " " + args_bits);
        //NOTE: if default argument is not used we need to jump out of default argument calculation code
        if(!args_info.IsDefaultArgUsed(def_arg_idx))
          exec.ip += jump_pos;
      }
      break;
      case Opcodes.Block:
      {
        var new_coroutine = VisitBlock(exec, curr_frame, item.defer_support);
        if(new_coroutine != null)
        {
          //NOTE: since there's a new coroutine we want to skip ip incrementing
          //      which happens below and proceed right to the execution of
          //      the new coroutine
          exec.coroutine = new_coroutine;
          return BHS.SUCCESS;
        }
      }
      break;
      case Opcodes.New:
      {
        int type_idx = (int)Bytecode.Decode24(curr_frame.bytecode, ref exec.ip);
        var type = curr_frame.type_refs[type_idx].Get();
        HandleNew(curr_frame, exec.stack, type);
      }
      break;
      default:
        throw new Exception("Not supported opcode: " + opcode);
    }

    ++exec.ip;
    return BHS.SUCCESS;
  }
  
  void ExecInitCode(Module module)
  {
    var bytecode = module.compiled.initcode;
    if(bytecode == null || bytecode.Length == 0)
      return;

    var constants = module.compiled.constants;
    var type_refs = module.compiled.type_refs.all;

    int ip = 0;

    while(ip < bytecode.Length)
    {
      var opcode = (Opcodes)bytecode[ip];
      //Util.Debug("EXEC INIT " + opcode);
      switch(opcode)
      {
        //NOTE: operates on global vars
        case Opcodes.DeclVar:
        {
          int var_idx = (int)Bytecode.Decode8(bytecode, ref ip);
          int type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          var type = type_refs[type_idx].Get();

          InitDefaultVal(type, module.gvar_vals[var_idx]);
        }
        break;
        //NOTE: operates on global vars
        case Opcodes.SetVar:
        {
          int var_idx = (int)Bytecode.Decode8(bytecode, ref ip);

          var new_val = init_frame._stack.Pop();
          module.gvar_vals.Assign(this, var_idx, new_val);
          new_val.Release();
        }
        break;
        case Opcodes.Constant:
        {
          int const_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          var cn = constants[const_idx];
          var cv = cn.ToVal(this);
          init_frame._stack.Push(cv);
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
          ExecuteBinaryOp(opcode, init_frame._stack);
        }
        break;
        case Opcodes.UnaryNot:
        case Opcodes.UnaryNeg:
        {
          ExecuteUnaryOp(opcode, init_frame._stack);
        }
        break;
        case Opcodes.New:
        {
          int type_idx = (int)Bytecode.Decode24(bytecode, ref ip);
          var type = type_refs[type_idx].Get();
          HandleNew(init_frame, init_frame._stack, type);
        }
        break;
        case Opcodes.SetAttrInplace:
        {
          int fld_idx = (int)Bytecode.Decode16(bytecode, ref ip);
          var val = init_frame._stack.Pop();
          var obj = init_frame._stack.Peek();
          var class_symb = (ClassSymbol)obj.type;
          var field_symb = (FieldSymbol)class_symb._all_members[fld_idx];
          field_symb.setter(init_frame, ref obj, val, field_symb);
          val.Release();
        }
        break;
        case Opcodes.ArrAddInplace:
        {
          var self = init_frame._stack[init_frame._stack.Count - 2];
          self.Retain();
          var class_type = ((ArrayTypeSymbol)self.type);
          var status = BHS.SUCCESS;
          ((FuncSymbolNative)class_type._all_members[0]).cb(init_frame, init_frame._stack, new FuncArgsInfo(), ref status);
          init_frame._stack.Push(self);
        }
        break;
        case Opcodes.MapAddInplace:
        {
          var self = init_frame._stack[init_frame._stack.Count - 3];
          self.Retain();
          var class_type = ((MapTypeSymbol)self.type);
          var status = BHS.SUCCESS;
          ((FuncSymbolNative)class_type._all_members[0]).cb(init_frame, init_frame._stack, new FuncArgsInfo(), ref status);
          init_frame._stack.Push(self);
        }
        break;
        default:
          throw new Exception("Not supported opcode: " + opcode);
      }
      ++ip;
    }
  }

  void ExecModuleInitFunc(Module module)
  {
    if(module.compiled.init_func_idx == -1)
      return;

    var fs = (FuncSymbolScript)module.ns.members[module.compiled.init_func_idx];
    var addr = new FuncAddr() {
      module = module,
      fs = fs,
      ip = fs.ip_addr
    };
    var fb = Start(addr);
    if(Tick(fb))
      throw new Exception("Module '" + module.name + "' init function is still running");
  }

  void ExecuteUnaryOp(Opcodes op, ValStack stack)
  {
    var operand = stack.PopRelease().num;
    switch(op)
    {
      case Opcodes.UnaryNot:
        stack.Push(Val.NewBool(this, operand != 1));
      break;
      case Opcodes.UnaryNeg:
        stack.Push(Val.NewFlt(this, operand * -1));
      break;
    }
  }

  void ExecuteBinaryOp(Opcodes op, ValStack stack)
  {
    var r_operand = stack.Pop();
    var l_operand = stack.Pop();

    switch(op)
    {
      case Opcodes.Add:
      {
        //TODO: add Opcodes.Concat?
        if((r_operand.type == Types.String) && (l_operand.type == Types.String))
          stack.Push(Val.NewStr(this, (string)l_operand._obj + (string)r_operand._obj));
        else
          stack.Push(Val.NewFlt(this, l_operand._num + r_operand._num));
      }
      break;
      case Opcodes.Sub:
        stack.Push(Val.NewFlt(this, l_operand._num - r_operand._num));
      break;
      case Opcodes.Div:
        stack.Push(Val.NewFlt(this, l_operand._num / r_operand._num));
      break;
      case Opcodes.Mul:
        stack.Push(Val.NewFlt(this, l_operand._num * r_operand._num));
      break;
      case Opcodes.Equal:
        stack.Push(Val.NewBool(this, l_operand.IsValueEqual(r_operand)));
      break;
      case Opcodes.NotEqual:
        stack.Push(Val.NewBool(this, !l_operand.IsValueEqual(r_operand)));
      break;
      case Opcodes.LT:
        stack.Push(Val.NewBool(this, l_operand._num < r_operand._num));
      break;
      case Opcodes.LTE:
        stack.Push(Val.NewBool(this, l_operand._num <= r_operand._num));
      break;
      case Opcodes.GT:
        stack.Push(Val.NewBool(this, l_operand._num > r_operand._num));
      break;
      case Opcodes.GTE:
        stack.Push(Val.NewBool(this, l_operand._num >= r_operand._num));
      break;
      case Opcodes.And:
        stack.Push(Val.NewBool(this, l_operand._num == 1 && r_operand._num == 1));
      break;
      case Opcodes.Or:
        stack.Push(Val.NewBool(this, l_operand._num == 1 || r_operand._num == 1));
      break;
      case Opcodes.BitAnd:
        stack.Push(Val.NewNum(this, (int)l_operand._num & (int)r_operand._num));
      break;
      case Opcodes.BitOr:
        stack.Push(Val.NewNum(this, (int)l_operand._num | (int)r_operand._num));
      break;
      case Opcodes.Mod:
        stack.Push(Val.NewFlt(this, l_operand._num % r_operand._num));
      break;
    }

    r_operand.Release();
    l_operand.Release();
  }
  
  Coroutine VisitBlock(ExecState exec, Frame curr_frame, IDeferSupport defer_support)
  {
    int block_size;
    var block_coro = TryMakeBlockCoroutine(ref exec.ip, curr_frame, exec, out block_size, defer_support);

    //Console.WriteLine("BLOCK CORO " + block_coro?.GetType().Name + " " + block_coro?.GetHashCode());
    if(block_coro is IBranchyCoroutine bi)
    {
      int tmp_ip = exec.ip;
      while(tmp_ip < (exec.ip + block_size))
      {
        ++tmp_ip;

        int tmp_size;
        var branch = TryMakeBlockCoroutine(ref tmp_ip, curr_frame, exec, out tmp_size, (IDeferSupport)block_coro);

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
  
  Coroutine TryMakeBlockCoroutine(ref int ip, Frame curr_frame, ExecState exec, out int size, IDeferSupport defer_support)
  {
    BlockType type;
    ReadBlockHeader(ref ip, curr_frame, out type, out size);

    if(type == BlockType.SEQ)
    {
      if(defer_support is IBranchyCoroutine)
      {
        var br = CoroutinePool.New<ParalBranchBlock>(this);
        br.Init(curr_frame, ip + 1, ip + size);
        return br;
      }
      else
      {
        var seq = CoroutinePool.New<SeqBlock>(this);
        seq.Init(curr_frame, exec.stack, ip + 1, ip + size);
        return seq;
      }
    }
    else if(type == BlockType.PARAL)
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
    else if(type == BlockType.DEFER)
    {
      var d = new DeferBlock(curr_frame, ip + 1, ip + size);
      defer_support.RegisterDefer(d);
      //NOTE: we need to skip defer block
      //Console.WriteLine("DEFER SKIP " + ip + " " + (ip+size) + " " + Environment.StackTrace);
      ip += size;
      return null;
    }
    else
      throw new Exception("Not supported block type: " + type);
  }
  
  void Call(Frame curr_frame, ExecState exec, Frame new_frame, uint args_bits)
  {
    int args_num = (int)(args_bits & FuncArgsInfo.ARGS_NUM_MASK);
    for(int i = 0; i < args_num; ++i)
      new_frame._stack.Push(exec.stack.Pop());
    new_frame._stack.Push(Val.NewInt(this, args_bits));

    //let's remember ip to return to
    new_frame.return_ip = exec.ip;
    exec.stack = new_frame._stack;
    exec.frames.Push(new_frame);
    exec.regions.Push(new Region(new_frame, new_frame));
    //since ip will be incremented below we decrement it intentionally here
    exec.ip = new_frame.start_ip - 1;
  }

  //NOTE: returns whether further execution should be stopped and status returned immediately (e.g in case of RUNNING or FAILURE)
  static bool CallNative(Frame curr_frame, ValStack curr_stack, FuncSymbolNative native, uint args_bits, out BHS status, ref Coroutine coroutine)
  {
    status = BHS.SUCCESS;
    var new_coroutine = native.cb(curr_frame, curr_stack, new FuncArgsInfo(args_bits), ref status);

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

  static BHS ExecuteCoroutine(Frame curr_frame, ExecState exec)
  {
    var status = BHS.SUCCESS;
    //NOTE: optimistically stepping forward so that for simple
    //      bindings you won't forget to do it
    ++exec.ip;
    exec.coroutine.Tick(curr_frame, exec, ref status);

    if(status == BHS.RUNNING)
    {
      --exec.ip;
      return status;
    }
    else if(status == BHS.FAILURE)
    {
      CoroutinePool.Del(curr_frame, exec, exec.coroutine);
      exec.coroutine = null;

      exec.ip = curr_frame.exit_ip - 1;
      exec.regions.Pop();

      return status;
    }
    else if(status == BHS.SUCCESS)
    {
      CoroutinePool.Del(curr_frame, exec, exec.coroutine);
      exec.coroutine = null;

      return status;
    }
    else
      throw new Exception("Bad status: " + status);
  }

  //TODO: make it more universal and robust
  void HandleTypeCast(ExecState exec, IType cast_type, bool force_type)
  {
    var new_val = Val.New(this);
    var val = exec.stack.Pop();

    if(cast_type == Types.Int)
      new_val.SetNum((long)val._num);
    else if(cast_type == Types.String && val.type != Types.String)
      new_val.SetStr(val.num.ToString(System.Globalization.CultureInfo.InvariantCulture));
    else
    {
      if(val._obj != null && cast_type is IInstantiable && !Types.Is(val, cast_type))
        throw new Exception("Invalid type cast: type '" + val.type + "' can't be cast to '" + cast_type + "'");
      new_val.ValueCopyFrom(val);
      if(force_type)
        new_val.type = cast_type;
      new_val.RefMod(RefOp.USR_INC);
    }

    val.Release();

    exec.stack.Push(new_val);
  }

  void HandleTypeAs(ExecState exec, IType cast_type, bool force_type)
  {
    var val = exec.stack.Pop();

    if(Types.Is(val, cast_type))
    {
      var new_val = Val.New(this);
      new_val.ValueCopyFrom(val);
      if(force_type)
        new_val.type = cast_type;
      new_val.RefMod(RefOp.USR_INC);
      exec.stack.Push(new_val);
    }
    else
      exec.stack.Push(Null);

    val.Release();
  }

  void HandleTypeIs(ExecState exec, IType type)
  {
    var val = exec.stack.Pop();
    exec.stack.Push(Val.NewBool(this, Types.Is(val, type)));
    val.Release();
  }

  void HandleNew(Frame curr_frame, ValStack stack, IType type)
  {
    var cls = type as ClassSymbol;
    if(cls == null)
      throw new Exception("Not a class symbol: " + type);

    var val = Val.New(this);
    cls.creator(curr_frame, ref val, cls);
    stack.Push(val);
  }

  static void ReadBlockHeader(ref int ip, Frame curr_frame, out BlockType type, out int size)
  {
    type = (BlockType)Bytecode.Decode8(curr_frame.bytecode, ref ip);
    size = (int)Bytecode.Decode16(curr_frame.bytecode, ref ip);
  }

  internal Val MakeDefaultVal(IType type)
  {
    var v = Val.New(this);
    InitDefaultVal(type, v);
    return v;
  }

  internal void InitDefaultVal(IType type, Val v)
  {
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
  }
}

}
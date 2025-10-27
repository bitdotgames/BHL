using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public class FuncArgSymbol : VariableSymbol
{
  new public const uint CLASS_ID = 19;

  public bool is_ref;

  public FuncArgSymbol(Origin origin, string name, ProxyType type, bool is_ref = false)
    : base(origin, name, type)
  {
    this.is_ref = is_ref;
  }

  //convenience version where we don't care about exact origin (e.g native
  //func bindings)
  public FuncArgSymbol(string name, ProxyType type, bool is_ref = false)
    : this(new Origin(), name, type, is_ref)
  {
  }

  //marshall factory version
  public FuncArgSymbol()
    : this(null, "", new ProxyType())
  {
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref is_ref);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

//TODO: add support for arbitrary amount of default arguments
public struct FuncArgsInfo
{
  //NOTE: 6 bits are used for a total number of args passed (max 63),
  //      26 bits are reserved for default args set bits (max 26 default args)
  public const int ARGS_NUM_BITS = 6;
  public const uint ARGS_NUM_MASK = ((1 << ARGS_NUM_BITS) - 1);
  public const int MAX_ARGS = (int)ARGS_NUM_MASK;
  public const int MAX_DEFAULT_ARGS = 32 - ARGS_NUM_BITS;

  public uint bits;

  //NOTE: setting all bits
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FuncArgsInfo(uint bits)
  {
    this.bits = bits;
  }

  //NOTE: setting only amount of arguments, bits will be calculated
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public FuncArgsInfo(int num_args)
  {
    bits = 0;

    if(!SetArgsNum(num_args))
      throw new Exception("Not supported amount of arguments: " + num_args);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static uint GetBits(int num_args)
  {
    var info = new FuncArgsInfo(num_args);
    return info.bits;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int CountArgs()
  {
    return (int)(bits & ARGS_NUM_MASK);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool HasDefaultUsedArgs()
  {
    return (bits & ~ARGS_NUM_MASK) > 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int CountRequiredArgs()
  {
    return CountArgs() - CountUsedDefaultArgs();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public int CountUsedDefaultArgs()
  {
    int c = 0;
    for(int i = 0; i < MAX_DEFAULT_ARGS; ++i)
      if(IsDefaultArgUsed(i))
        ++c;
    return c;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool SetArgsNum(int num)
  {
    if(num < 0 || num > MAX_ARGS)
      return false;
    bits = (bits & ~ARGS_NUM_MASK) | (uint)num;
    return true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IncArgsNum()
  {
    uint num = bits & ARGS_NUM_MASK;
    ++num;
    if(num > MAX_ARGS)
      return false;
    bits = (bits & ~ARGS_NUM_MASK) | num;
    return true;
  }

  //NOTE: idx starts from 0, it's the idx of the default argument *within* default arguments,
  //      e.g: func Foo(int a, int b = 1, int c = 2) { .. }
  //           b is 0 default arg idx, c is 1
  public bool UseDefaultArg(int idx, bool flag)
  {
    if(idx >= MAX_DEFAULT_ARGS)
      return false;
    uint mask = 1u << (idx + ARGS_NUM_BITS);
    if(flag)
      bits |= mask;
    else
      bits &= ~mask;
    return true;
  }

  //NOTE: idx starts from 0
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsDefaultArgUsed(int idx)
  {
    return (bits & (1u << (idx + ARGS_NUM_BITS))) != 0;
  }
}

[System.Flags]
public enum FuncSignatureAttrib : byte
{
  None           = 0,
  Coro           = 1,
  VariadicArgs   = 2,

  //it's a mask which includes all enum bits,
  //can be used e.g to convert FuncAttrib into FuncSignatureAttrib
  FuncAttribMask = 3
}

//NOTE: it's a superset of FuncSignatureAttrib
[System.Flags]
public enum FuncAttrib : byte
{
  None         = 0,
  Coro         = 1,
  VariadicArgs = 2,
  Virtual      = 4,
  Override     = 8,
  Static       = 16,
}

public abstract class FuncSymbol : Symbol, ITyped, IScope,
  IScopeIndexed, IModuleIndexed, IEnumerable<Symbol>
{
  FuncSignature _signature;

  public FuncSignature signature
  {
    get { return _signature; }
    set
    {
      _signature = value;
      //NOTE: a bit ugly, we set func attributes once the signature changes
      _signature.attribs.SetFuncAttrib(ref _attribs);
    }
  }

  internal SymbolsStorage members;

  //NOTE: if the function is owned (one of overrides) by virtual function this attribute
  //      will point to the virtual function. This is convenient during opcode emitting
  //      phase
  internal FuncSymbolVirtual _virtual;

  int _module_idx = -1;

  public int module_idx
  {
    get { return _module_idx; }
    set { _module_idx = value; }
  }

  int _scope_idx = -1;

  public int scope_idx
  {
    get { return _scope_idx; }
    set { _scope_idx = value; }
  }

  protected byte _attribs = 0;

  public FuncAttrib attribs
  {
    get { return (FuncAttrib)_attribs; }
    set
    {
      _attribs = (byte)value;
      signature.attribs = value.ToFuncSignatureAttrib();
    }
  }

  public FuncSymbol(
    Origin origin,
    string name,
    FuncSignature sig
  )
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
    this.signature = sig;
  }

  public IType GetIType()
  {
    return signature;
  }

  public virtual Symbol Resolve(string name)
  {
    return members.Find(name);
  }

  public virtual void Define(Symbol sym)
  {
    members.Add(sym);
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return members.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  internal struct EnforceThisForInstanceMembersScope : IScope
  {
    ClassSymbol cs;

    internal EnforceThisForInstanceMembersScope(ClassSymbolScript cs)
    {
      this.cs = cs;
    }

    public Symbol Resolve(string name)
    {
      var symb = cs.Resolve(name);

      //NOTE: instance members must be called via 'this'
      if(symb is VariableSymbol vs && vs.scope is ClassSymbol && !vs.IsStatic())
        return null;
      else if (symb is FuncSymbol fs && fs.scope is ClassSymbol && !fs.IsStatic())
        return null;

      return symb;
    }

    public void Define(Symbol sym)
    {
      throw new NotImplementedException();
    }

    public IScope GetFallbackScope()
    {
      return cs.scope;
    }
  }

  public IScope GetFallbackScope()
  {
    //NOTE: If declared as a class method we force the fallback
    //      scope to be special wrapper scope. This way we
    //      force the class members to be prefixed with 'this.'
    if(scope is ClassSymbolScript cs)
      return new EnforceThisForInstanceMembersScope(cs);
    else
      return this.scope;
  }

  public IType GetReturnType()
  {
    return signature.ret_type.Get();
  }

  public int GetTotalArgsNum()
  {
    return signature.arg_types.Count;
  }

  public abstract int GetDefaultArgsNum();

  public int GetRequiredArgsNum()
  {
    return GetTotalArgsNum() - GetDefaultArgsNum();
  }

  public bool HasDefaultArgAt(int i)
  {
    if(i >= GetTotalArgsNum())
      return false;
    return i >= (GetDefaultArgsNum() - GetDefaultArgsNum());
  }

  public FuncArgSymbol TryGetArg(int idx)
  {
    idx += GetThisArgOffset();
    if(idx < 0 || idx >= members.Count)
      return null;
    return (FuncArgSymbol)members[idx];
  }

  public FuncArgSymbol GetArg(int idx)
  {
    return (FuncArgSymbol)members[GetThisArgOffset() + idx];
  }

  public int FindArgIdx(string name)
  {
    int idx = members.IndexOf(name);
    if(idx == -1)
      return idx;
    return idx - GetThisArgOffset();
  }

  int GetThisArgOffset()
  {
    return (scope is ClassSymbolScript && !attribs.HasFlag(FuncAttrib.Static))
      ? 1
      : 0;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(_signature);
    refs.Index(members.list);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref _attribs);
    marshall.Marshall.Sync(ctx, ref _signature);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
    marshall.Marshall.Sync(ctx, ref members);
  }

  public override string ToString()
  {
    string buf =
      "func " + signature.ret_type + " " + name + "(";
    if(signature.attribs.HasFlag(FuncSignatureAttrib.Coro))
      buf = "coro " + buf;
    for(int i = 0; i < signature.arg_types.Count; ++i)
    {
      if(i > 0)
        buf += ",";
      if(signature.attribs.HasFlag(FuncSignatureAttrib.VariadicArgs) && i == signature.arg_types.Count - 1)
        buf += "...";
      buf += signature.arg_types[i] + " " + members[i].name;
    }

    buf += ")";

    return buf;
  }
}

public class FuncSymbolScript : FuncSymbol
{
  public const uint CLASS_ID = 13;

  //cached value of Module, it's set upon module loading in VM and
  internal Module _module;

  //used for up values resolving during parsing
  internal LocalScope _current_scope;

  public int local_vars_num;
  public int default_args_num;
  public int ip_addr;

  public FuncSymbolScript(
    Origin origin,
    FuncSignature sig,
    string name,
    int default_args_num = 0,
    int ip_addr = -1
  )
    : base(origin, name, sig)
  {
    this.name = name;
    this.default_args_num = default_args_num;
    this.ip_addr = ip_addr;
  }

  //symbol factory version
  public FuncSymbolScript()
    : base(null, null, new FuncSignature())
  {
  }

  public void ReserveThisArgument(ClassSymbolScript class_scope)
  {
    var this_symb = new FuncArgSymbol("this", new ProxyType(class_scope));
    Define(this_symb);
  }

  public override void Define(Symbol sym)
  {
    if(!(sym is FuncArgSymbol))
      throw new Exception("Only func arguments allowed, got: " + sym.GetType().Name);

    ++local_vars_num;

    base.Define(sym);
  }

  public override int GetDefaultArgsNum()
  {
    return default_args_num;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref local_vars_num);
    marshall.Marshall.Sync(ctx, ref default_args_num);
    marshall.Marshall.Sync(ctx, ref ip_addr);
  }
}

public class FuncSymbolVirtual : FuncSymbol
{
  protected int default_args_num;

  public List<FuncSymbol> overrides = new List<FuncSymbol>();
  public List<ProxyType> owners = new List<ProxyType>();

  public FuncSymbolVirtual(Origin origin, string name, FuncSignature signature, int default_args_num = 0)
    : base(origin, name, signature)
  {
    this.default_args_num = default_args_num;
  }

  public FuncSymbolVirtual(FuncSymbolScript proto)
    : this(proto.origin, proto.name, proto.signature, proto.default_args_num)
  {
    //NOTE: directly adding arguments avoiding Define
    for(int m = 0; m < proto.members.Count; ++m)
      members.Add(proto.members[m]);
    this.origin = proto.origin;
  }

  public override int GetDefaultArgsNum()
  {
    return default_args_num;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Define(Symbol sym)
  {
    throw new NotImplementedException();
  }

  public void AddOverride(ClassSymbol owner, FuncSymbol fs)
  {
    //NOTE: we call fs.signature(signature) but not signature.Equals(fs.signature) on purpose,
    //      since we want to allow 'non-coro' functions to be a subset(compatible) of 'coro' ones
    if(!fs.signature.Equals(signature))
      throw new SymbolError(fs,
        "virtual method signatures don't match, base: '" + signature + "', override: '" + fs.signature + "'");

    fs._virtual = this;

    owners.Add(new ProxyType(owner));
    overrides.Add(fs);
  }

  public FuncSymbol FindOverride(ClassSymbol owner)
  {
    for(int i = 0; i < owners.Count; ++i)
    {
      if(owners[i].Get() == owner)
        return overrides[i];
    }

    return null;
  }

  public FuncSymbol GetTopOverride()
  {
    return overrides[overrides.Count - 1];
  }
}

public class FuncSymbolNative : FuncSymbol
{
  public delegate Coroutine Cb(VM.FrameOld frm, ValOldStack stack, FuncArgsInfo args_info, ref BHS status);

  public Cb cb;

  int default_args_num;

  public FuncSymbolNative(
    Origin cinfo,
    string name,
    ProxyType ret_type,
    Cb cb,
    params FuncArgSymbol[] args
  )
    : this(cinfo, name, ret_type, 0, cb, args)
  {
  }

  public FuncSymbolNative(
    Origin cinfo,
    string name,
    ProxyType ret_type,
    int def_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  )
    : this(cinfo, name, FuncAttrib.None, ret_type, def_args_num, cb, args)
  {
  }

  public FuncSymbolNative(
    Origin origin,
    string name,
    FuncAttrib attribs,
    ProxyType ret_type,
    int default_args_num,
    Cb cb,
    params FuncArgSymbol[] args
  )
    : base(origin, name, new FuncSignature(attribs.ToFuncSignatureAttrib(), ret_type))
  {
    this.attribs = attribs;

    this.cb = cb;
    this.default_args_num = default_args_num;

    foreach(var arg in args)
    {
      base.Define(arg);
      signature.AddArg(arg.type);
    }
  }

  public override int GetDefaultArgsNum()
  {
    return default_args_num;
  }

  public override void Define(Symbol sym)
  {
    throw new Exception("Defining symbols here is not allowed");
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }

  public override string ToString()
  {
    return "[native] " + base.ToString();
  }
}

#if BHL_FRONT
//NOTE: there's no special lambda symbol during runtime,
//      maybe there should actually be one for consistency?
public class LambdaSymbol : FuncSymbolScript
{
  List<AST_UpVal> upvals;

  List<FuncSymbolScript> fdecl_stack;

  Dictionary<VariableSymbol, UpvalMode> captures;

  public LambdaSymbol(
    AnnotatedParseTree parsed,
    string name,
    FuncSignature sig,
    List<AST_UpVal> upvals,
    Dictionary<VariableSymbol, UpvalMode> captures,
    List<FuncSymbolScript> fdecl_stack
  )
    : base(parsed, sig, name, 0, -1)
  {
    this.upvals = upvals;
    this.captures = captures;
    this.fdecl_stack = fdecl_stack;
  }

  VariableSymbol AddUpValue(VariableSymbol src)
  {
    var local = new VariableSymbol(src.origin, src.name, src.type);

    local._upvalue = src;

    //NOTE: we want to avoid possible recursion during resolve
    //      checks that's why we use a non-checking version
    this._current_scope.DefineWithoutEnclosingChecks(local);

    var upval = new AST_UpVal(
      local.name,
      local.scope_idx,
      src.scope_idx,
      //TODO: should be the line of its usage
      //(in case of 'this' there's no associated parse tree,
      // so we pass a line number)
      src.origin.source_line
    );
    upval.mode = DetectCaptureMode(src);
    upvals.Add(upval);

    return local;
  }

  UpvalMode DetectCaptureMode(VariableSymbol s)
  {
    UpvalMode m;
    captures.TryGetValue(s, out m);
    return m;
  }

  public override Symbol Resolve(string name)
  {
    var s = base.Resolve(name);
    if(s != null)
      return s;

    return ResolveUpValue(name);
  }

  Symbol ResolveUpValue(string name)
  {
    int my_idx = FindMyIdxInStack();
    if(my_idx == -1)
      return null;

    for(int i = my_idx; i-- > 0;)
    {
      var fdecl = fdecl_stack[i];

      if(fdecl._current_scope != null)
      {
        //let's try to find a variable inside a function
        var res = fdecl._current_scope.ResolveWithFallback(name);
        //checking if it's a variable and not a global one
        if(res is VariableSymbol vs && !(vs.scope is Namespace))
        {
          var local = AssignUpValues(vs, i + 1, my_idx);
          //NOTE: returning local instead of an original variable since we need
          //      proper index of the local variable in the local frame
          return local;
        }
      }
    }

    return null;
  }

  int FindMyIdxInStack()
  {
    for(int i = 0; i < fdecl_stack.Count; ++i)
    {
      if(fdecl_stack[i] == this)
        return i;
    }

    return -1;
  }

  VariableSymbol AssignUpValues(VariableSymbol vs, int from_idx, int to_idx)
  {
    VariableSymbol most_nested = null;
    //now let's put this result into all nested lambda scopes
    for(int j = from_idx; j <= to_idx; ++j)
    {
      if(fdecl_stack[j] is LambdaSymbol lmb)
      {
        //NOTE: for nested lambdas a 'local' variable may become an 'upvalue'
        vs = lmb.AddUpValue(vs);
        if(most_nested == null)
          most_nested = vs;
      }
    }

    return most_nested;
  }
}
#endif

}

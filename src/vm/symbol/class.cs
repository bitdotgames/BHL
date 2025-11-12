using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace bhl
{

public abstract class ClassSymbol : Symbol, IInstantiable, IEnumerable<Symbol>
{
  public ClassSymbol super_class
  {
    get { return (ClassSymbol)_super_class.Get(); }
  }

  internal ProxyType _super_class;

  public TypeSet<InterfaceSymbol> implements = new TypeSet<InterfaceSymbol>();

  //contains mapping of implemented interface methods indices
  //to actual class methods indices:
  //  [IFoo][0=>3,1=>1,1=>0]
  //  [IBar][0=>2,1=>0]
  internal Dictionary<InterfaceSymbol, List<FuncSymbol>> _itable;
  internal Dictionary<int, FuncSymbol> _vtable;

  HashSet<IInstantiable> related_types;

  //contains only 'local' members without taking into account inheritance
  internal SymbolsStorage members;

  //all 'flattened' members including all parents
  internal Symbol[] _all_members;

  // we want to prevent resolving of attributes and methods at some point
  // since they might collide with types. For example:
  // class Foo {
  //   a.A a <-- here attribute 'a' will prevent proper resolving of 'a.A' type
  // }
  // setting this attribute controls this behavior
  internal bool _resolve_only_decl_members = false;

  public VM.ClassCreator creator;

  public ClassSymbol(
    Origin origin,
    string name,
    VM.ClassCreator creator = null
  )
    : base(origin, name)
  {
    this.members = new SymbolsStorage(this);

    this.creator = creator;
  }

  public void SetSuperClassAndInterfaces(ClassSymbol super_class, IList<InterfaceSymbol> implements = null)
  {
    if (super_class != null)
      _super_class = new ProxyType(super_class);

    if (implements != null && implements.Count > 0)
      SetImplementedInterfaces(implements);
  }

  //NOTE: only once the class is Setup we have valid members iterator
  public IEnumerator<Symbol> GetEnumerator()
  {
    if (_all_members != null)
      return ((IEnumerable<Symbol>)_all_members).GetEnumerator();
    return Enumerable.Empty<Symbol>().GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public IScope GetFallbackScope()
  {
    return this.scope;
  }

  public string GetNativeStaticFieldGetFuncName(FieldSymbol fld)
  {
    return "$__" + ((Symbol)this).GetFullTypePath() + "_get_" + fld.name;
  }

  public string GetNativeStaticFieldSetFuncName(FieldSymbol fld)
  {
    return "$__" + ((Symbol)this).GetFullTypePath() + "_set_" + fld.name;
  }

  static public bool IsBinaryOp(string op)
  {
    if (op == "+")
      return true;
    else if (op == "-")
      return true;
    else if (op == "==")
      return true;
    else if (op == "!=")
      return true;
    else if (op == "*")
      return true;
    else if (op == "/")
      return true;
    else if (op == "%")
      return true;
    else if (op == ">")
      return true;
    else if (op == ">=")
      return true;
    else if (op == "<")
      return true;
    else if (op == "<=")
      return true;
    else
      return false;
  }

  public void Define(Symbol sym)
  {
    if (sym is FuncSymbol fs)
    {
      if (IsBinaryOp(fs.name))
        CheckBinaryOpOverload(fs);

      if (fs is FuncSymbolScript fss)
        this.GetModule().func_index.Index(fss);
      //NOTE: for now indexing at the module level only static native methods
      //      due to possible unavailability of module for 'generic classes'
      //      created on the fly (array, map)
      else if (fs is FuncSymbolNative fsn && fs.attribs.HasFlag(FuncAttrib.Static))
        this.GetModule().nfunc_index.Index(fsn);
    }
    else if (sym is FieldSymbol fld && fld.attribs.HasFlag(FieldAttrib.Static))
    {
      if (this is ClassSymbolNative)
      {
        //let's create fake static get/set native functions
        var static_get = new FuncSymbolNative(
          new Origin(),
          GetNativeStaticFieldGetFuncName(fld), fld.type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            throw new NotImplementedException();
            var res = new Val();
            //fld.getter(null, null, ref res, fld);
            exec.stack.Push(res);
            return null;
          });
        this.GetModule().nfunc_index.Index(static_get);

        var static_set = new FuncSymbolNative(
          new Origin(),
          GetNativeStaticFieldSetFuncName(fld), fld.type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            throw new NotImplementedException();
            Val ctx = null;
            exec.stack.Pop(out var val);
            //fld.setter(null, ref ctx, val, fld);
            val._refc?.Release();
            return null;
          });
        this.GetModule().nfunc_index.Index(static_set);
      }
      else
        this.GetModule().gvar_index.Index(fld);
    }

    //NOTE: we don't check if there are any parent symbols with the same name,
    //      they will be checked once the class is finally setup
    members.Add(sym);
  }

  static void CheckBinaryOpOverload(FuncSymbol fs)
  {
    if (!fs.attribs.HasFlag(FuncAttrib.Static))
      throw new SymbolError(fs, "operator overload must be static");

    if (fs.GetTotalArgsNum() != 2)
      throw new SymbolError(fs, "operator overload must have exactly 2 arguments");

    if (fs.GetReturnType() == Types.Void)
      throw new SymbolError(fs, "operator overload return value can't be void");
  }

  public Symbol Resolve(string name)
  {
    var sym = DoResolve(name);
    if (_resolve_only_decl_members && (sym is VariableSymbol || sym is FuncSymbol))
      return null;
    return sym;
  }

  Symbol DoResolve(string name)
  {
    var sym = members.Find(name);
    if (sym != null)
      return sym;
    if (super_class != null)
    {
      var super_sym = super_class.Resolve(name);
      if (super_sym != null)
        return super_sym;
    }

    return null;
  }

  internal void SetImplementedInterfaces(IList<InterfaceSymbol> implements)
  {
    this.implements.Clear();
    foreach (var imp in implements)
      this.implements.Add(imp);
  }

  public virtual void Setup()
  {
    ValidateInterfaces();

    SetupAllMembers();

    SetupVirtTables();
  }

  void ValidateInterfaces()
  {
    for (int i = 0; i < implements.Count; ++i)
      ValidateImplementation(implements[i]);
  }

  void ValidateImplementation(InterfaceSymbol iface)
  {
    var set = iface.GetAllRelatedTypesSet();
    foreach (var item in set)
    {
      var item_if = (InterfaceSymbol)item;
      for (int i = 0; i < item_if.members.Count; ++i)
      {
        var m = (FuncSymbol)item_if.members[i];
        var func_symb = Resolve(m.name) as FuncSymbol;
        if (func_symb == null || !Types.Is(func_symb.signature, m.signature))
          throw new SymbolError(this,
            "class '" + name + "' doesn't implement interface '" + iface.name + "' method '" + m + "'");
      }
    }
  }

  void SetupAllMembers()
  {
    if (_all_members != null)
      return;

    var all_members = new SymbolsStorage(this);
    DoSetupMembers(this, all_members);

    _all_members = all_members.list.ToArray();
  }

  void DoSetupMembers(ClassSymbol curr_class, SymbolsStorage all_members)
  {
    var super_class = curr_class.super_class;
    if (super_class != null)
      DoSetupMembers(super_class, all_members);

    for (int i = 0; i < curr_class.members.Count; ++i)
    {
      var sym = curr_class.members[i];

      //for weird problems assert
      //if(sym is ITyped typed && typed.GetIType() == null)
      //  throw new SymbolError(sym, "type for member '"+sym.name+"' was not resolved in class '"+name+"'");

      //NOTE: we need to recalculate attribute index taking account all
      //      parent classes
      if (sym is IScopeIndexed si && !sym.IsStatic())
        si.scope_idx = all_members.Count;

      if (sym is FuncSymbolScript fss)
      {
        if (fss.attribs.HasFlag(FuncAttrib.Virtual))
        {
          if (fss._default_args_num > 0)
            throw new SymbolError(sym,
              "virtual methods are not allowed to have default arguments in class '" + name + "'");

          var vsym = new FuncSymbolVirtual(fss);
          vsym.AddOverride(curr_class, fss);
          all_members.Add(vsym);
        }
        else if (fss.attribs.HasFlag(FuncAttrib.Override))
        {
          if (fss._default_args_num > 0)
            throw new SymbolError(sym,
              "virtual methods are not allowed to have default arguments in class '" + name + "'");

          var vsym = all_members.Find(sym.name) as FuncSymbolVirtual;
          if (vsym == null)
            throw new SymbolError(sym, "no base virtual method to override in class '" + name + "'");

          vsym.AddOverride(curr_class, fss);
        }
        else
          all_members.Add(fss);
      }
      else
        all_members.Add(sym);
    }
  }

  void SetupVirtTables()
  {
    if (_vtable != null)
      return;

    //virtual methods lookup table
    _vtable = new Dictionary<int, FuncSymbol>();

    for (int i = 0; i < _all_members.Length; ++i)
    {
      if (_all_members[i] is FuncSymbolVirtual fssv)
        _vtable[i] = fssv.GetTopOverride();
    }

    //interfaces lookup table
    _itable = new Dictionary<InterfaceSymbol, List<FuncSymbol>>();

    var all = new HashSet<IInstantiable>();
    if (super_class != null)
    {
      for (int i = 0; i < super_class.implements.Count; ++i)
        all.UnionWith(super_class.implements[i].GetAllRelatedTypesSet());
    }

    for (int i = 0; i < implements.Count; ++i)
      all.UnionWith(implements[i].GetAllRelatedTypesSet());

    foreach (var imp in all)
    {
      var ifs2fn = new List<FuncSymbol>();
      var ifs = (InterfaceSymbol)imp;
      _itable.Add(ifs, ifs2fn);

      for (int midx = 0; midx < ifs.members.Count; ++midx)
      {
        var m = ifs.members[midx];
        var symb = Resolve(m.name) as FuncSymbol;
        if (symb == null)
          throw new Exception("No such method '" + m.name + "' in class '" + this.name + "'");
        ifs2fn.Add(symb);
      }
    }
  }

  public HashSet<IInstantiable> GetAllRelatedTypesSet()
  {
    if (related_types == null)
      related_types = CollectAllRelatedTypesSet();
    return related_types;
  }

  protected virtual HashSet<IInstantiable> CollectAllRelatedTypesSet()
  {
    var related_types = new HashSet<IInstantiable>();
    related_types.Add(this);
    if (super_class != null)
      related_types.UnionWith(super_class.GetAllRelatedTypesSet());
    for (int i = 0; i < implements.Count; ++i)
    {
      if (!related_types.Contains(implements[i]))
        related_types.UnionWith(implements[i].GetAllRelatedTypesSet());
    }

    return related_types;
  }
}

public class ClassSymbolScript : ClassSymbol
{
  public const uint CLASS_ID = 11;

  public ClassSymbolScript(Origin origin, string name)
    : base(origin, name)
  {
    this.creator = ClassCreator;
  }

  //marshall factory version
  public ClassSymbolScript()
    : this(null, null)
  {
  }

  void ClassCreator(VM.ExecState exec, ref Val instance, IType type)
  {
    //NOTE: object's raw data is a list
    var vl = ValList.New(exec.vm);
    instance.SetObj(vl, type);

    for(int i = 0; i < _all_members.Length; ++i)
    {
      var m = _all_members[i];
      //NOTE: Members contain all kinds of symbols: methods and attributes,
      //      however we need to properly setup attributes only.
      //      Other members will be initialized with special case 'nil' value.
      //TODO:
      //      Maybe we should store attributes and methods separately someday.
      if(m is VariableSymbol vs)
      {
        var mtype = vs.type.Get();
        var v = new Val();
        VM.InitDefaultVal(mtype, ref v);
        vl.Add(v);
      }
      else
        vl.Add(VM.Null);
    }
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(_super_class);
    refs.Index(members.list);
    refs.Index(implements.list);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.SyncTypeRef(ctx, ref _super_class);
    marshall.Marshall.Sync(ctx, ref members);
    marshall.Marshall.Sync(ctx, ref implements);
  }
}

public class ClassSymbolNative : ClassSymbol, INativeType
{
  //NOTE: during Setup() routine will be resolved
  ProxyType tmp_super_class;
  IList<ProxyType> tmp_implements;

  System.Type native_type;
  Func<Val, object> native_object_getter;

  public ClassSymbolNative(
    Origin origin,
    string name,
    VM.ClassCreator creator = null,
    System.Type native_type = null,
    Func<Val, object> native_object_getter = null
  )
    : this(
      origin, name,
      new ProxyType(), null,
      creator,
      native_type, native_object_getter)
  {
  }

  public ClassSymbolNative(
    Origin origin,
    string name,
    IList<ProxyType> proxy_implements,
    VM.ClassCreator creator = null,
    System.Type native_type = null,
    Func<Val, object> native_object_getter = null
  )
    : this(
      origin, name,
      new ProxyType(), proxy_implements,
      creator,
      native_type, native_object_getter)
  {
  }

  public ClassSymbolNative(
    Origin origin,
    string name,
    ProxyType proxy_super_class,
    VM.ClassCreator creator = null,
    System.Type native_type = null,
    Func<Val, object> native_object_getter = null
  )
    : this(origin, name,
      proxy_super_class, null,
      creator,
      native_type, native_object_getter)
  {
  }

  public ClassSymbolNative(
    Origin origin,
    string name,
    ProxyType proxy_super_class,
    IList<ProxyType> proxy_implements,
    VM.ClassCreator creator = null,
    System.Type native_type = null,
    Func<Val, object> native_object_getter = null
  )
    : base(origin, name, creator)
  {
    this.tmp_super_class = proxy_super_class;
    this.tmp_implements = proxy_implements;
    this.native_type = native_type;
    this.native_object_getter = native_object_getter;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }

  public object GetNativeObject(Val v)
  {
    return native_object_getter?.Invoke(v) ?? v._obj;
  }

  public override void Setup()
  {
    ResolveTmpSuperClassAndInterfaces(this);

    base.Setup();
  }

  static void ResolveTmpSuperClassAndInterfaces(ClassSymbolNative curr_class)
  {
    if (curr_class.tmp_super_class.IsEmpty() && curr_class.tmp_implements == null)
      return;

    ClassSymbol super_class = null;
    if (!curr_class.tmp_super_class.IsEmpty())
    {
      super_class = curr_class.tmp_super_class.Get() as ClassSymbol;
      if (super_class == null)
        throw new Exception("Parent class is not found: " + curr_class.tmp_super_class);

      //let's reset tmp member once it's resolved
      curr_class.tmp_super_class.Clear();
    }

    List<InterfaceSymbol> implements = null;
    if (curr_class.tmp_implements != null)
    {
      implements = new List<InterfaceSymbol>();
      foreach (var pi in curr_class.tmp_implements)
      {
        var iface = pi.Get() as InterfaceSymbol;
        if (iface == null)
          throw new Exception("Implemented interface not found: " + pi);
        implements.Add(iface);
      }

      //let's reset tmp member once it's resolved
      curr_class.tmp_implements = null;
    }

    if (super_class != null)
      ResolveTmpSuperClassAndInterfaces((ClassSymbolNative)super_class);

    curr_class.SetSuperClassAndInterfaces(super_class, implements);
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
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

}

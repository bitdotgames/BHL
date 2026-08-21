namespace bhl
{

public partial class VM : INamedResolver
{
  //NOTE: mirrors ClassSymbolScript.ClassCreator, resolved by path instead of a compiled
  //      type ref - no user .init()-style method is called automatically
  public Val NewInstance(string class_path)
  {
    if(ResolveNamedByPath(class_path) is not ClassSymbolScript cls)
      throw new System.Exception($"Class '{class_path}' not found");

    var vl = ValList.New(this);

    for(int i = 0; i < cls._all_members.Length; ++i)
    {
      var m = cls._all_members[i];
      if(m is VariableSymbol vs)
      {
        var v = new Val();
        InitDefaultVal(vs.type.Get(), ref v);
        vl.Add(v);
      }
      else
        vl.Add(Null);
    }

    var instance = new Val();
    instance.SetObj(vl, cls);
    return instance;
  }

  //NOTE: a linear scan - call once and cache the result rather than every call/frame.
  //      Null if no such method exists.
  public FuncSymbolScript FindMethod(Val self, string method_name)
  {
    if(self.type is not ClassSymbolScript cls)
      throw new System.Exception($"Not a class instance: {self.type}");

    for(int i = 0; i < cls._all_members.Length; ++i)
    {
      if(cls._all_members[i] is FuncSymbolScript fss && fss.name == method_name)
        return fss;
    }

    return null;
  }

  //NOTE: a real Fiber, so the call may genuinely yield. Null if no such method exists.
  public Fiber CallMethod(ref Val self, string method_name, StackList<Val> args, FiberOptions opts = 0)
  {
    return CallMethod(ref self, FindMethod(self, method_name), args, opts);
  }

  //NOTE: skips the by-name scan - pass a FuncSymbolScript already resolved via
  //      FindMethod. Re-resolve after MigrateInstance(): a reloaded class is a fresh
  //      object, so a symbol cached from before the reload no longer belongs to it.
  public Fiber CallMethod(ref Val self, FuncSymbolScript func_symb, StackList<Val> args, FiberOptions opts = 0)
  {
    if(func_symb == null)
      return null;

    //NOTE: self becomes local slot 0 (like OpcodeCallMethod); retain to balance ReleaseLocals()
    self.RetainData();

    var call_args = new StackList<Val>();
    call_args.Add(self);
    for(int i = 0; i < args.Count; ++i)
      call_args.Add(args[i]);

    return this.Start(func_symb, call_args, opts);
  }

  //NOTE: like CallMethod, but runs synchronously via Execute() instead of starting a
  //      Fiber - cheaper, but only safe for a non-coroutine method. Check
  //      func_symb.attribs.HasFlag(FuncAttrib.Coro) before choosing this over
  //      CallMethod; if the method tries to suspend anyway, Execute() throws.
  public ValStack ExecuteMethod(ref Val self, FuncSymbolScript func_symb, StackList<Val> args)
  {
    self.RetainData();

    var call_args = new StackList<Val>();
    call_args.Add(self);
    for(int i = 0; i < args.Count; ++i)
      call_args.Add(args[i]);

    return this.Execute(func_symb, call_args);
  }

  //NOTE: doesn't consume `instance` - returns a fresh Val the caller must ReleaseData()
  public Val GetFieldValue(Val instance, string field_name)
  {
    var field_symb = FindField(instance, field_name);

    var exec = new ExecState { vm = this };
    var res = new Val();
    field_symb.getter(exec, instance, ref res, field_symb);
    res.RetainData();
    return res;
  }

  //NOTE: mirrors OpcodeSetAttr's refcount contract - pass an already-owned `v`, it's
  //      released here; `instance` isn't released since the caller keeps it (by ref, not popped)
  public void SetFieldValue(ref Val instance, string field_name, Val v)
  {
    var field_symb = FindField(instance, field_name);

    var exec = new ExecState { vm = this };
    field_symb.setter(exec, ref instance, v, field_symb);
    v.ReleaseData();
  }

  static FieldSymbol FindField(Val instance, string field_name)
  {
    if(instance.type is not ClassSymbol cls)
      throw new System.Exception($"Not a class instance: {instance.type}");

    for(int i = 0; i < cls._all_members.Length; ++i)
    {
      if(cls._all_members[i] is FieldSymbol fs && fs.name == field_name)
        return fs;
    }

    throw new System.Exception($"No such field '{field_name}' on '{((Symbol)cls).GetFullTypePath()}'");
  }
}

}

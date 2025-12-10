using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public partial class VM : INamedResolver
{
  Types types;

  public struct FuncAddr
  {
    public Module module;

    public FuncSymbolScript fs;
    public int ip;

    public FuncSymbolNative fsn;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FuncAddr(FuncSymbolScript fss)
    {
      return new FuncAddr(fss);
    }

    public bool is_native
    {
      get { return fsn != null; }
    }

    FuncSymbol _symbol;

    public FuncSymbol symbol
    {
      get
      {
        if(_symbol == null)
          _symbol = FindSymbol();
        return _symbol;
      }
    }

    //TODO: add ctor for FuncSymbolNative?
    public FuncAddr(FuncSymbolScript fss)
    {
      module = fss._module;
      fs = fss;
      ip = fss._ip_addr;
      fsn = null;
      _symbol = fs;
    }

    FuncSymbol FindSymbol()
    {
      if(fs != null)
        return fs;
      else if(fsn != null)
        return fsn;
      else if(module != null)
        return module.TryMapIp2Func(ip);

      return null;
    }
  }

  public struct VarAddr
  {
    public Module module;
    public VariableSymbol vs;
    public ValRef val_ref;
  }

  public struct SymbolSpec : IEquatable<SymbolSpec>
  {
    public string module;
    public NamePath path;

    public SymbolSpec(string module, NamePath path)
    {
      this.module = module;
      this.path = path;
    }

    public bool Equals(SymbolSpec other)
    {
      return module == other.module && path.Equals(other.path);
    }

    public override bool Equals(object obj)
    {
      return obj is SymbolSpec other && Equals(other);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(module, path);
    }

    public override string ToString()
    {
      return path + "(" + module + ")";
    }
  }

  public VM(Types types = null, IModuleLoader loader = null)
  {
    if(types == null)
      types = new Types();
    this.types = types;
    this.loader = loader;

    //NOTE: trying to fix some weird IL2CPP bug which doesn't allow to
    //      achieve the same using initialization parameters
    script_executors = new StackArray<ExecState>(2);
    script_executors.Values[0] = new ExecState();
    script_executors.Values[1] = new ExecState();
  }

  public bool TryFindFuncAddr(NamePath path, out FuncAddr addr)
  {
    addr = default(FuncAddr);

    var fs = ResolveNamedByPath(path) as FuncSymbol;
    if(fs == null)
      return false;

    var fss = fs as FuncSymbolScript;
    registered_modules.TryGetValue(((Namespace)fs.scope).module.name, out var module);

    addr = new FuncAddr()
    {
      module = module,
      fs = fss,
      ip = fss?._ip_addr ?? -1,
      fsn = fss != null ? null : fs as FuncSymbolNative,
    };

    return true;
  }

  public bool TryFindVarAddr(NamePath path, out VarAddr addr)
  {
    addr = default(VarAddr);

    var vs = ResolveNamedByPath(path) as VariableSymbol;
    if(vs == null)
      return false;

    var cm = registered_modules[((Namespace)vs.scope).module.name];

    addr = new VarAddr()
    {
      module = cm,
      vs = vs,
      val_ref = (ValRef)cm.gvars.vals[vs.scope_idx]._refc
    };

    return true;
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    foreach(var kv in registered_modules)
    {
      var s = kv.Value.ns.ResolveSymbolByPath(path);
      if(s != null)
        return s;
    }

    return null;
  }

  public void Stop()
  {
    for(int i = fibers.Count; i-- > 0;)
    {
      var fiber = fibers[i];
      fiber.Stop();
      fiber.Release();
      fibers.RemoveAt(i);
    }
  }

  public void GetStackTrace(Dictionary<ExecState, List<TraceItem>> info)
  {
    for(int i = 0; i < fibers.Count; ++i)
    {
      var fb = fibers[i];
      var trace = new List<TraceItem>();
      fb.GetStackTrace(trace);
      info[fb.exec] = trace;
    }

    for(int i = 0; i <= script_executor_idx; ++i)
    {
      var exec = script_executors.Values[i];
      var trace = new List<TraceItem>();
      exec.GetStackTrace(trace);
      info[exec] = trace;
    }
  }

  public bool Tick()
  {
    return Tick(fibers, ref last_fiber);
  }

  //NOTE: this version assumes that stopped fibers are released and removed
  static public bool Tick(List<Fiber> fibers, ref Fiber last_fiber)
  {
    for(int i = 0; i < fibers.Count; ++i)
    {
      var fiber = fibers[i];
      last_fiber = fiber;
      fiber.Tick();
    }

    for(int i = fibers.Count; i-- > 0;)
    {
      var fiber = fibers[i];
      if(fiber.IsStopped())
      {
        fiber.Release();
        fibers.RemoveAt(i);
      }
    }

    return fibers.Count != 0;
  }
}

}

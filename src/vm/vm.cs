using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  Types types;
  
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
        s += "at " + t.func + "(..) +" + t.ip + " in " + t.file + ":" + t.line + "\n";
      return s;
    }
  }

  public struct FuncAddr
  {
    public Module module;
    
    public FuncSymbolScript fs;
    public int ip;

    public FuncSymbolNative fsn;

    FuncSymbol _symbol;
    public FuncSymbol symbol
    {
      get {
        if(_symbol == null)
          _symbol = FindSymbol();
        return _symbol;
      }
    } 
    
    public FuncSymbol FindSymbol()
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
    public Val val;
  }

  public struct SymbolSpec : IEquatable<SymbolSpec>
  {
    public string module;
    public string path;

    public SymbolSpec(string module, string path)
    {
      this.module = module;
      this.path = path;
    }

    public bool Equals(SymbolSpec other)
    {
      return module == other.module && path == other.path;
    }

    public override bool Equals(object obj)
    {
      return obj is SymbolSpec other && Equals(other);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(module, path);
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
    null_val.SetObj(null, Types.Null);
    //NOTE: we don't want to store it in the values pool,
    //      still we need to retain it so that it's never
    //      accidentally released when pushed/popped
    null_val.Retain();
  }

  public bool TryFindFuncAddr(string path, out FuncAddr addr)
  {
    addr = default(FuncAddr);

    var fs = ResolveNamedByPath(path) as FuncSymbol;
    if(fs == null)
      return false;
    
    var fss = fs as FuncSymbolScript;
    registered_modules.TryGetValue(((Namespace)fs.scope).module.name, out var module);
    
    addr = new FuncAddr() {
      module = module,
      
      fs = fss,
      ip = fss?.ip_addr ?? -1,
      
      fsn = fss != null ? null : fs as FuncSymbolNative,
    };

    return true;
  }

  [Obsolete("Use TryFindFuncAddr(string path, out FuncAddr addr) instead.")]
  public bool TryFindFuncAddr(string path, out FuncAddr addr, out FuncSymbolScript fs)
  {
    bool yes = TryFindFuncAddr(path, out addr);
    fs = addr.fs;
    return yes;
  }

  public bool TryFindVarAddr(string path, out VarAddr addr)
  {
    addr = default(VarAddr);

    var vs = ResolveNamedByPath(path) as VariableSymbol;
    if(vs == null)
      return false;

    var cm = registered_modules[((Namespace)vs.scope).module.name];

    addr = new VarAddr() {
      module = cm,
      vs = vs,
      val = cm.gvar_vals[vs.scope_idx]
    };

    return true;
  }

  public INamed ResolveNamedByPath(string path)
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
    for(int i=fibers.Count;i-- > 0;)
    {
      Stop(fibers[i]);
      fibers.RemoveAt(i);
    }
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

      fb.Retain();

      ++fb.tick;
      fb.status = Execute(fb.exec);

      fb.Release();

      if(fb.status != BHS.RUNNING)
        _Stop(fb);
    }
    catch(Exception e)
    {
      var trace = new List<VM.TraceItem>();
      try
      {
        fb.GetStackTrace(trace);
      }
      catch(Exception) 
      {}
      throw new Error(trace, e); 
    }
    return !fb.IsStopped();
  }
}

}

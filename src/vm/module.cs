using System;
using System.Collections.Generic;

namespace bhl {

public class ModulePath
{
  public string name;
  public string file_path;

  public ModulePath(string name, string file_path)
  {
    this.name = name;
    this.file_path = file_path;
  }
}

//NOTE: represents a module which can be registered in Types
public class Module
{
  public string name {
    get {
      return path.name;
    }
  }
  public string file_path {
    get {
      return path.file_path;
    }
  }
  public ModulePath path;

  //used for assigning incremental indexes to module global vars,
  //contains imported variables as well
  public VarIndexer gvars = new VarIndexer();
  //used for assigning incremental indexes to native funcs
  public NativeFuncIndexer nfuncs;

  //TODO: probably we need script functions per module indexer, like gvars?
  //setup once the module is loaded to find functions by their ip
  internal Dictionary<int, FuncSymbolScript> _ip2func = new Dictionary<int, FuncSymbolScript>();

  //if set this mark is the index starting from which 
  //*imported* module variables are stored in gvars
  public int local_gvars_mark = -1;

  //an amount of *local* to this module global variables 
  //stored in gvars
  public int local_gvars_num {
    get {
      return local_gvars_mark == -1 ? gvars.Count : local_gvars_mark;
    }
  }
  public Types ts;
  public Namespace ns;

  public Module(Types ts, ModulePath path)
    : this(ts, path, new Namespace())
  {}

  public Module(Types ts, string name = "", string file_path = "")
    : this(ts, new ModulePath(name, file_path), new Namespace())
  {}

  public Module(
    Types ts,
    ModulePath path,
    Namespace ns
  )
  {
    this.ts = ts;
    nfuncs = ts.nfunc_index;
    //let's setup the link
    ns.module = this;
    this.path = path;
    this.ns = ns;
  }
}

} //namespace bhl

using System;
using System.IO;
using System.Collections.Generic;

namespace bhl {

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
  const uint HEADER_VERSION = 2;
  public const int MAX_GLOBALS = 128;

  public Module module;

  public string name {
    get {
      return module.name;
    }
  }

  public Namespace ns {
    get {
      return module.ns;
    }
  }

  public byte[] initcode;
  public byte[] bytecode;
  //NOTE: normalized module names, not actual import paths
  public List<string> imports;
  //NOTE: filled during runtime module setup procedure since
  //      until this moment we don't know about other modules 
  internal CompiledModule[] _imported;
  public List<Const> constants;
  public FixedStack<Val> gvars = new FixedStack<Val>(MAX_GLOBALS);
  public Ip2SrcLine ip2src_line;
  public int init_func_idx = -1;

  public CompiledModule(
    Module module,
    int init_func_idx,
    int total_gvars_num,
    List<string> imports,
    List<Const> constants, 
    byte[] initcode,
    byte[] bytecode, 
    Ip2SrcLine ip2src_line
  )
  {
    this.module = module;
    this.init_func_idx = init_func_idx;
    this.imports = imports;
    this.constants = constants;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.ip2src_line = ip2src_line;

    gvars.Resize(total_gvars_num);
  }
  
  //convenience version for tests
  public CompiledModule(Module m)
    : this(
      m, 
      -1, 
      0, 
      new List<string>(),
      new List<Const>(),
      new byte[0],
      new byte[0],
      new Ip2SrcLine()
      )
  {}

  static public CompiledModule FromStream(
    Types types, 
    Stream src, 
    INamedResolver resolver = null, 
    System.Action<string, string> on_import = null
  )
  {
    var module = new Module(types);

    //NOTE: if resolver (used for type proxies resolving) is not
    //      passed we use the namespace itself
    if(resolver == null)
      resolver = module.ns.R();

    var symb_factory = new SymbolFactory(types, resolver);

    string name = "";
    string file_path = "";
    int init_func_idx = -1;
    var imports = new List<string>();
    int constants_len = 0;
    int total_gvars_num = 0;
    int local_gvars_num = 0;
    byte[] constant_bytes = null;
    var constants = new List<Const>();
    byte[] symb_bytes = null;
    byte[] initcode = null;
    byte[] bytecode = null;
    var ip2src_line = new Ip2SrcLine();

    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8, true/*leave open*/))
    {
      //TODO: add better support for version
      uint version = r.ReadUInt32();
      if(version != HEADER_VERSION)
        throw new Exception("Unsupported version: " + version);

      name = r.ReadString();
      file_path = r.ReadString();
      module.path = new ModulePath(name, file_path);

      init_func_idx = r.ReadInt32();

      int imports_len = r.ReadInt32();
      for(int i=0;i<imports_len;++i)
        imports.Add(r.ReadString());

      int symb_len = r.ReadInt32();
      symb_bytes = r.ReadBytes(symb_len);

      int initcode_len = r.ReadInt32();
      if(initcode_len > 0)
        initcode = r.ReadBytes(initcode_len);

      int bytecode_len = r.ReadInt32();
      if(bytecode_len > 0)
        bytecode = r.ReadBytes(bytecode_len);

      constants_len = r.ReadInt32();
      if(constants_len > 0)
        constant_bytes = r.ReadBytes(constants_len);

      total_gvars_num = r.ReadInt32();
      local_gvars_num = r.ReadInt32();

      int ip2src_line_len = r.ReadInt32();
      for(int i=0;i<ip2src_line_len;++i)
        ip2src_line.Add(r.ReadInt32(), r.ReadInt32());
    }

    foreach(var import in imports)
      on_import?.Invoke(name, import);

    marshall.Marshall.Stream2Obj(new MemoryStream(symb_bytes), module.ns, symb_factory);

    //NOTE: we link native namespace after our own namespace was loaded,
    //      this way we make sure namespace members are properly linked
    //      and there are no duplicates
    module.ns.Link(types.ns);
    module.local_gvars_mark = local_gvars_num;

    if(constants_len > 0)
      ReadConstants(symb_factory, constant_bytes, constants);

    return new 
      CompiledModule(
        module,
        init_func_idx,
        total_gvars_num,
        imports,
        constants, 
        initcode, 
        bytecode, 
        ip2src_line
     );
  }

  public void Setup(Func<string, CompiledModule> name2module)
  {
    int gvars_offset = module.local_gvars_num;

    _imported = new CompiledModule[imports.Count];
    
    for(int i=0; i<imports.Count; ++i)
    {
      //TODO: what about 'native' modules?
      var imported_module = name2module(imports[i]);
      if(imported_module == null)
        continue;

      _imported[i] = imported_module;
      
      //NOTE: taking only local imported module's gvars
      for(int g=0;g<imported_module.module.local_gvars_num;++g)
      {
        var imp_gvar = imported_module.gvars[g];
        imp_gvar.Retain();
        gvars[gvars_offset] = imp_gvar;
        ++gvars_offset;
      }
    }

    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(s is Namespace ns)
          ns.module = module;
        else if(s is ClassSymbol cs)
        {
          cs.Setup();
          
          foreach(var kv in cs._vtable)
          {
            if(kv.Value is FuncSymbolScript vfs)
              PrepareFuncSymbol(vfs, name2module);
          }
        }
        else if(s is VariableSymbol vs && vs.scope is Namespace)
          module.gvars.index.Add(vs);
        else if(s is FuncSymbolScript fs)
          PrepareFuncSymbol(fs, name2module);
      }
    );
  }
  
  void PrepareFuncSymbol(FuncSymbolScript fss, Func<string, CompiledModule> name2module)
  {
    if(fss.ip_addr == -1)
      throw new Exception("Func ip_addr is not set: " + fss.GetFullPath());
    
    module._ip2func[fss.ip_addr] = fss;

    if(!(fss.scope is InterfaceSymbol))
    {
      module.funcs.index.Add(fss);
      
      if(fss._module == null)
      {
        var mod_name = fss.GetModule().name;
        if (!string.IsNullOrEmpty(mod_name))
        {
          fss._module = name2module(mod_name);
          if (fss._module == null)
            throw new Exception("Module '" + mod_name + "' not found");
        }
      }
    }
  }

  static void ReadConstants(SymbolFactory symb_factory, byte[] constant_bytes, List<Const> constants)
  {
    var src = new MemoryStream(constant_bytes); 
    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8))
    {
      int constants_num = r.ReadInt32();
      for(int i=0;i<constants_num;++i)
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
        else if(cn_type == ConstType.INAMED)
        {
          var tp = marshall.Marshall.Stream2Obj<Proxy<INamed>>(src, symb_factory);
          if(string.IsNullOrEmpty(tp.path))
            throw new Exception("Missing path");
          cn = new Const(tp);
        }
        else if(cn_type == ConstType.ITYPE)
        {
          var tp = marshall.Marshall.Stream2Obj<Proxy<IType>>(src, symb_factory);
          if(string.IsNullOrEmpty(tp.path))
            throw new Exception("Missing path");
          cn = new Const(tp);
        }
        else
          throw new Exception("Unknown type: " + cn_type);

        constants.Add(cn);
      }
    }
  }

  static public void ToStream(CompiledModule cm, Stream dst, bool leave_open = false)
  {
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8, leave_open))
    {
      //TODO: add better support for version
      //TODO: introduce header info with offsets to data
      w.Write(HEADER_VERSION);

      w.Write(cm.module.name);
      w.Write(cm.module.file_path);
      
      w.Write(cm.init_func_idx);

      w.Write(cm.imports.Count);
      foreach(var import in cm.imports)
        w.Write(import);

      var symb_bytes = marshall.Marshall.Obj2Bytes(cm.module.ns);
      w.Write(symb_bytes.Length);
      w.Write(symb_bytes, 0, symb_bytes.Length);

      w.Write(cm.initcode == null ? (int)0 : cm.initcode.Length);
      if(cm.initcode != null)
        w.Write(cm.initcode, 0, cm.initcode.Length);

      w.Write(cm.bytecode == null ? (int)0 : cm.bytecode.Length);
      if(cm.bytecode != null)
        w.Write(cm.bytecode, 0, cm.bytecode.Length);

      var constant_bytes = WriteConstants(cm.constants);
      w.Write(constant_bytes.Length);
      if(constant_bytes.Length > 0)
        w.Write(constant_bytes, 0, constant_bytes.Length);

      w.Write(cm.module.gvars.Count);
      w.Write(cm.module.local_gvars_num);

      //TODO: add this info only for development builds
      w.Write(cm.ip2src_line.ips.Count);
      for(int i=0;i<cm.ip2src_line.ips.Count;++i)
      {
        w.Write(cm.ip2src_line.ips[i]);
        w.Write(cm.ip2src_line.lines[i]);
      }
    }
  }

  static byte[] WriteConstants(List<Const> constants)
  {
    var dst = new MemoryStream();
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8))
    {
      w.Write(constants.Count);
      foreach(var cn in constants)
      {
        w.Write((byte)cn.type);
        if(cn.type == ConstType.STR)
          w.Write(cn.str);
        else if(cn.type == ConstType.FLT || 
            cn.type == ConstType.INT ||
            cn.type == ConstType.BOOL ||
            cn.type == ConstType.NIL)
          w.Write(cn.num);
        else if(cn.type == ConstType.INAMED)
          marshall.Marshall.Obj2Stream(cn.inamed, dst);
        else if(cn.type == ConstType.ITYPE)
          marshall.Marshall.Obj2Stream(cn.itype, dst);
        else
          throw new Exception("Unknown type: " + cn.type);
      }
    }
    return dst.GetBuffer();
  }

  static public void ToFile(CompiledModule cm, string file)
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      ToStream(cm, wfs);
    }
  }

  static public CompiledModule FromFile(string file, Types types)
  {
    using(FileStream rfs = new FileStream(file, FileMode.Open, System.IO.FileAccess.Read))
    {
      return FromStream(types, rfs);
    }
  }
}

} //namespace bhl

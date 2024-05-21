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
  
  public static CompiledModule None = new CompiledModule();
  
  //normalized imported module names
  public List<string> imports;
  public int total_gvars_num;
  public byte[] initcode;
  public byte[] bytecode;
  public List<Const> constants;
  public Ip2SrcLine ip2src_line;
  public int init_func_idx = -1;

  public CompiledModule(
    int init_func_idx,
    List<string> imports,
    int total_gvars_num,
    List<Const> constants,
    byte[] initcode,
    byte[] bytecode,
    Ip2SrcLine ip2src_line)
  {
    this.init_func_idx = init_func_idx;
    this.imports = imports;
    this.total_gvars_num = total_gvars_num;
    this.constants = constants;
    this.initcode = initcode;
    this.bytecode = bytecode;
    this.ip2src_line = ip2src_line;
  }
  
  public CompiledModule()
    : this(
      -1,
      new List<string>(),
      0,
      new List<Const>(),
      new byte[0],
      new byte[0],
      new Ip2SrcLine()
    )
  {}

  static public Module FromStream(
    Types types, 
    Stream src, 
    INamedResolver resolver = null
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

    marshall.Marshall.Stream2Obj(new MemoryStream(symb_bytes), module.ns, symb_factory);

    //NOTE: we link native namespace after our own namespace was loaded,
    //      this way we make sure namespace members are properly linked
    //      and there are no duplicates
    module.ns.Link(types.ns);
    module.local_gvars_mark = local_gvars_num;

    if(constants_len > 0)
      ReadConstants(symb_factory, constant_bytes, constants);

    var compiled = new CompiledModule(
        init_func_idx,
        imports,
        total_gvars_num,
        constants, 
        initcode, 
        bytecode, 
        ip2src_line
      );
    
    module.InitWithCompiled(compiled);
     
    return module;
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

  static public void ToStream(Module module, Stream dst, bool leave_open = false)
  {
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8, leave_open))
    {
      //TODO: add better support for version
      //TODO: introduce header info with offsets to data
      w.Write(HEADER_VERSION);

      w.Write(module.name);
      w.Write(module.file_path);
      
      w.Write(module.compiled.init_func_idx);

      w.Write(module.compiled.imports.Count);
      foreach(var import in module.compiled.imports)
        w.Write(import);

      var symb_bytes = marshall.Marshall.Obj2Bytes(module.ns);
      w.Write(symb_bytes.Length);
      w.Write(symb_bytes, 0, symb_bytes.Length);

      w.Write(module.compiled.initcode == null ? (int)0 : module.compiled.initcode.Length);
      if(module.compiled.initcode != null)
        w.Write(module.compiled.initcode, 0, module.compiled.initcode.Length);

      w.Write(module.compiled.bytecode == null ? (int)0 : module.compiled.bytecode.Length);
      if(module.compiled.bytecode != null)
        w.Write(module.compiled.bytecode, 0, module.compiled.bytecode.Length);

      var constant_bytes = WriteConstants(module.compiled.constants);
      w.Write(constant_bytes.Length);
      if(constant_bytes.Length > 0)
        w.Write(constant_bytes, 0, constant_bytes.Length);

      w.Write(module.gvar_index.Count);
      w.Write(module.local_gvars_num);

      //TODO: add this info only for development builds
      w.Write(module.compiled.ip2src_line.ips.Count);
      for(int i=0;i<module.compiled.ip2src_line.ips.Count;++i)
      {
        w.Write(module.compiled.ip2src_line.ips[i]);
        w.Write(module.compiled.ip2src_line.lines[i]);
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
        else if(cn.type == ConstType.ITYPE)
          marshall.Marshall.Obj2Stream(cn.itype, dst);
        else
          throw new Exception("Unknown type: " + cn.type);
      }
    }
    return dst.GetBuffer();
  }

  static public void ToFile(Module module, string file)
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, System.IO.FileAccess.Write))
    {
      ToStream(module, wfs);
    }
  }

  static public Module FromFile(string file, Types types)
  {
    using(FileStream rfs = new FileStream(file, FileMode.Open, System.IO.FileAccess.Read))
    {
      return FromStream(types, rfs);
    }
  }
}

}

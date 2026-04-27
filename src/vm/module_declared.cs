using System;
using System.Buffers;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using bhl.marshall;

namespace bhl
{

public class ModuleDeclared : INamedResolver
{
  public int id;

  static int _seq_id = 0;
  internal static int NextId() => Interlocked.Increment(ref _seq_id);

  public string name;
  public string file_path;
  public Namespace ns;

  public FuncNativeModuleIndexer nfunc_index = new FuncNativeModuleIndexer();
  public VarScopeIndexer gvar_index = new VarScopeIndexer();
  public FuncModuleIndexer func_index = new FuncModuleIndexer();

  //TODO: this probably must be more generic since native modules might have imports as well
  public List<string> imports => compiled.imports;
  public int total_gvars_num => compiled.total_gvars_num;

  public int local_gvars_mark = -1;
  public int local_gvars_num => local_gvars_mark == -1 ? gvar_index.Count : local_gvars_mark;

  //non-null for script (compiled) modules once compiled/loaded
  public CompiledModule compiled = CompiledModule.Empty;
  public bool is_native => compiled == CompiledModule.Empty;

  bool is_fully_setup;

  public ModuleDeclared(string name = "", string file_path = "")
  {
    this.name = name;
    this.file_path = file_path;
    this.ns = new Namespace(this, "");
  }

  public ModuleDeclared(ModulePath path)
    : this(path.name, path.file_path)
  {}

  public void InitWithCompiled(CompiledModule compiled)
  {
    this.compiled = compiled;
  }

  public void AssignId()
  {
    if(id == 0)
      id = NextId();
  }

  public void AddImportedGlobalVars(ModuleDeclared imported)
  {
    //NOTE: let's add imported global vars to module's global vars index
    if(local_gvars_mark == -1)
      local_gvars_mark = gvar_index.Count;

    var idx = imported.gvar_index;
    int num = imported.local_gvars_num;

    //NOTE: adding directly without indexing
    for(int i = 0; i < num; ++i)
      gvar_index.index.Add(idx[i]);
  }

  [System.Flags]
  public enum SetupFlags
  {
    All        = 255,
    Imports    = 1,
    Funcs      = 2,
    Gvars      = 4,
    Classes    = 8,
    Namespaces = 16,
  }

  public void Setup(Func<string, ModuleDeclared> import2module)
  {
    lock(this)
    {
      if(is_fully_setup)
        return;
      Setup(import2module, SetupFlags.All);
      is_fully_setup = true;
    }
  }

  internal void Setup(Func<string, ModuleDeclared> import2module, SetupFlags flags)
  {
    if(flags.HasFlag(SetupFlags.Imports))
    {
      foreach(var imp in compiled.imports)
        ns.Link(import2module(imp).ns);
    }

    ns.ForAllLocalSymbols(delegate(Symbol s)
      {
        if(flags.HasFlag(SetupFlags.Funcs) && s is FuncSymbolScript fs)
          SetupFuncSymbol(fs);
        else if(flags.HasFlag(SetupFlags.Funcs) && s is FuncSymbolVirtual fssv &&
                fssv.GetTopOverride() is FuncSymbolScript vsf)
          SetupFuncSymbol(vsf);
        else if(flags.HasFlag(SetupFlags.Gvars) && s is VariableSymbol vs && vs.scope is Namespace)
          gvar_index.index.Add(vs);
        else if(flags.HasFlag(SetupFlags.Classes) && s is ClassSymbol cs)
          cs.Setup();
        else if(flags.HasFlag(SetupFlags.Namespaces) && s is Namespace sns)
          sns.module = this;
      }
    );

    compiled.ResolveTypeRefs();
  }

  public FuncSymbolScript TryMapIp2Func(int ip)
  {
    FuncSymbolScript fsymb = null;
    ns.ForAllLocalSymbols(delegate(Symbol s)
    {
      if(s is FuncSymbolScript ftmp && ftmp._ip_addr == ip)
        fsymb = ftmp;
      else if(s is FuncSymbolVirtual fsv && fsv.GetTopOverride() is FuncSymbolScript fssv && fssv._ip_addr == ip)
        fsymb = fssv;
    });
    return fsymb;
  }

  void SetupFuncSymbol(FuncSymbolScript fss)
  {
    //NOTE: the func symbol might be from another module (e.g in case of class inheritance with the base class
    //      located in a different module). Here we check if module was already set up and if so ignore the symbol
    if(fss._module != null)
      return;

    //interface methods are skipped
    if(fss.scope is InterfaceSymbol)
      return;

    //NOTE: let's ignore not 'our' functions, e.g methods from other base classes located in other modules.
    //      This is especially important for cases when the cached module is being setup during the compilation,
    //      while the module which actually contains the func symbol is not yet parsed and the function is not
    //      assigned ip_addr yet
    if(fss.GetModule()?.name != name)
      return;

    if(fss._ip_addr == -1)
      throw new Exception("Func " + fss.GetFullTypePath() + " ip_addr is not set, module '" + name + "'");

    //caching declared data; serves as a guard that the symbol was set up
    fss._module = this;

    //TODO: there's definitely questionable code duplication - we add script functions
    //      to index when defining them in the scope them and here, when setting up the module
    func_index.index.Add(fss);
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return ns.ResolveSymbolByPath(path);
  }

  const uint STREAM_VERSION = 2;

  public void ToStream(Stream dst, bool leave_open = false)
  {
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8, leave_open))
    {
      w.Write(STREAM_VERSION);

      w.Write(name);
      w.Write(file_path);

      w.Write(compiled.init_func_idx);

      w.Write(compiled.imports.Count);
      foreach(var import in compiled.imports)
        w.Write(import);

      compiled.type_refs.Index(ns);

      var type_ref_bytes =
        Marshall.WriteTypeRefs(compiled.type_refs, out var type_ref_offsets);
      w.Write(type_ref_offsets.Count);
      foreach(int offset in type_ref_offsets)
        w.Write(offset);
      w.Write(type_ref_bytes.Length);
      w.Write(type_ref_bytes, 0, type_ref_bytes.Length);

      var symb_bytes = Marshall.Obj2Bytes(ns, compiled.type_refs);
      w.Write(symb_bytes.Length);
      w.Write(symb_bytes, 0, symb_bytes.Length);

      w.Write(compiled.initcode == null ? (int)0 : compiled.initcode.Length);
      if(compiled.initcode != null)
        w.Write(compiled.initcode, 0, compiled.initcode.Length);

      w.Write(compiled.bytecode == null ? (int)0 : compiled.bytecode.Length);
      if(compiled.bytecode != null)
        w.Write(compiled.bytecode, 0, compiled.bytecode.Length);

      var constant_bytes = WriteConstants(compiled.constants);
      w.Write(constant_bytes.Length);
      if(constant_bytes.Length > 0)
        w.Write(constant_bytes, 0, constant_bytes.Length);

      w.Write(gvar_index.Count);
      w.Write(local_gvars_num);

      w.Write(compiled.ip2src_line.ips.Count);
      for(int i = 0; i < compiled.ip2src_line.ips.Count; ++i)
      {
        w.Write(compiled.ip2src_line.ips[i]);
        w.Write(compiled.ip2src_line.lines[i]);
      }
    }
  }

  public void ToFile(string file)
  {
    using(FileStream wfs = new FileStream(file, FileMode.Create, FileAccess.Write))
      ToStream(wfs);
  }

  public static ModuleDeclared FromStream(Types types, Stream src, INamedResolver resolver = null)
  {
    var module = new ModuleDeclared();

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
    var type_refs = new TypeRefIndex();
    List<int> type_refs_offsets = new List<int>();
    byte[] type_refs_bytes = null;
    byte[] symb_bytes = null;
    byte[] initcode = null;
    byte[] bytecode = null;
    var ip2src_line = new Ip2SrcLine();

    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8, true /*leave open*/))
    {
      uint version = r.ReadUInt32();
      if(version != STREAM_VERSION)
        throw new Exception("Unsupported version: " + version);

      name = r.ReadString();
      file_path = r.ReadString();
      module.name = name;
      module.file_path = file_path;

      init_func_idx = r.ReadInt32();

      int imports_len = r.ReadInt32();
      for(int i = 0; i < imports_len; ++i)
        imports.Add(r.ReadString());

      int type_refs_offsets_len = r.ReadInt32();
      for(int i = 0; i < type_refs_offsets_len; ++i)
        type_refs_offsets.Add(r.ReadInt32());

      int type_refs_len = r.ReadInt32();
      type_refs_bytes = ArrayPool<byte>.Shared.Rent(type_refs_len);
      r.Read(type_refs_bytes, 0, type_refs_len);

      int symb_len = r.ReadInt32();
      symb_bytes = ArrayPool<byte>.Shared.Rent(symb_len);
      r.Read(symb_bytes, 0, symb_len);

      int initcode_len = r.ReadInt32();
      if(initcode_len > 0)
        initcode = r.ReadBytes(initcode_len);

      int bytecode_len = r.ReadInt32();
      if(bytecode_len > 0)
        bytecode = r.ReadBytes(bytecode_len);

      constants_len = r.ReadInt32();
      if(constants_len > 0)
      {
        constant_bytes = ArrayPool<byte>.Shared.Rent(constants_len);
        r.Read(constant_bytes, 0, constants_len);
      }

      total_gvars_num = r.ReadInt32();
      local_gvars_num = r.ReadInt32();

      int ip2src_line_len = r.ReadInt32();
      ip2src_line.EnsureCapacity(ip2src_line_len);
      for(int i = 0; i < ip2src_line_len; ++i)
        ip2src_line.Add(r.ReadInt32(), r.ReadInt32());
    }

    var reader = new MsgPackDataReader(new MemoryStream(symb_bytes));
    var ctx = SyncContext.NewReader(reader);
    ctx.factory = symb_factory;
    ctx.type_refs = type_refs;
    ctx.type_refs_reader = new MsgPackDataReader(new MemoryStream(type_refs_bytes));
    ctx.type_refs_offsets = type_refs_offsets;

    Marshall.ReadTypeRefs(ctx);

    var module_ns = module.ns;
    Marshall.Sync(ctx, ref module_ns);
    module.ns = module_ns;

    module.ns.Link(types.ns);
    module.local_gvars_mark = local_gvars_num;

    Const[] constants;
    if(constants_len > 0)
    {
      ReadConstants(constant_bytes, out constants);
      ArrayPool<byte>.Shared.Return(constant_bytes);
    }
    else
      constants = Array.Empty<Const>();

    ArrayPool<byte>.Shared.Return(symb_bytes);
    ArrayPool<byte>.Shared.Return(type_refs_bytes);

    var compiled = new CompiledModule(
      init_func_idx,
      imports,
      total_gvars_num,
      constants,
      type_refs,
      initcode,
      bytecode,
      ip2src_line
    );

    module.InitWithCompiled(compiled);
    module.AssignId();

    return module;
  }

  public static ModuleDeclared FromFile(string file, Types types)
  {
    using(FileStream rfs = new FileStream(file, FileMode.Open, FileAccess.Read))
      return FromStream(types, rfs);
  }

  static void ReadConstants(byte[] constant_bytes, out Const[] constants)
  {
    var src = new MemoryStream(constant_bytes);
    using(BinaryReader r = new BinaryReader(src, System.Text.Encoding.UTF8))
    {
      int constants_num = r.ReadInt32();
      constants = new Const[constants_num];
      for(int i = 0; i < constants_num; ++i)
      {
        Const cn = default;
        var cn_type = (ConstType)r.Read();
        if(cn_type == ConstType.STR)
          cn = new Const(r.ReadString());
        else if(cn_type == ConstType.FLT ||
                cn_type == ConstType.INT ||
                cn_type == ConstType.BOOL ||
                cn_type == ConstType.NIL)
          cn = new Const(cn_type, r.ReadDouble(), "");
        else
          throw new Exception("Unknown type: " + cn_type);
        constants[i] = cn;
      }
    }
  }

  static byte[] WriteConstants(Const[] constants)
  {
    var dst = new MemoryStream();
    using(BinaryWriter w = new BinaryWriter(dst, System.Text.Encoding.UTF8))
    {
      w.Write(constants.Length);
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
        else
          throw new Exception("Unknown type: " + cn.type);
      }
    }
    return dst.GetBuffer();
  }
}

}

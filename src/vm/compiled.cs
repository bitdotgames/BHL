using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace bhl
{

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
    int idx = Search(ip, 0, ips.Count - 1);
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
      if(ip <= ips[mid] && (mid == 0 || ip > ips[mid - 1]))
        return mid;

      if(ips[mid] > ip)
        return Search(ip, l, mid - 1);
      else
        return Search(ip, mid + 1, r);
    }

    return -1;
  }

  public void EnsureCapacity(int capacity)
  {
    ips.Capacity = capacity;
    lines.Capacity = capacity;
  }
}

public class CompiledModule
{
  public static CompiledModule Empty = new CompiledModule();

  //normalized imported module names
  public List<string> imports;
  public int total_gvars_num;

  public byte[] initcode;
  public GCHandle initcode_handle;
  public byte[] bytecode;
  public GCHandle bytecode_handle;
  public unsafe byte* bytecode_ptr;
  public unsafe byte* initcode_ptr;

  public Const[] constants;
  public TypeRefIndex type_refs;
  public IType[] type_refs_resolved;

  public Ip2SrcLine ip2src_line;
  public int init_func_idx = -1;

  public unsafe CompiledModule(
    int init_func_idx,
    List<string> imports,
    int total_gvars_num,
    Const[] constants,
    TypeRefIndex type_refs,
    byte[] initcode,
    byte[] bytecode,
    Ip2SrcLine ip2src_line)
  {
    this.init_func_idx = init_func_idx;
    this.imports = imports;
    this.total_gvars_num = total_gvars_num;
    this.constants = constants;
    this.type_refs = type_refs;
    this.initcode = initcode;
    this.initcode_handle = GCHandle.Alloc(this.initcode, GCHandleType.Pinned);
    this.initcode_ptr = (byte*)initcode_handle.AddrOfPinnedObject();
    this.bytecode = bytecode;
    this.bytecode_handle = GCHandle.Alloc(this.bytecode, GCHandleType.Pinned);
    this.bytecode_ptr = (byte*)bytecode_handle.AddrOfPinnedObject();
    this.ip2src_line = ip2src_line;
  }

  //NOTE: sentinel ctor for CompiledModule.Empty
  public CompiledModule()
  {
    imports = new List<string>();
    constants = Array.Empty<Const>();
    type_refs = new TypeRefIndex();
    initcode = new byte[0];
    bytecode = new byte[0];
    ip2src_line = new Ip2SrcLine();
    init_func_idx = -1;
  }

  ~CompiledModule()
  {
    if(bytecode_handle.IsAllocated)
      bytecode_handle.Free();
    if(initcode_handle.IsAllocated)
      initcode_handle.Free();
  }

  public void ResolveTypeRefs()
  {
    if(type_refs_resolved == null)
    {
      type_refs_resolved = new IType[type_refs.Count];
      for (int i = 0; i < type_refs.Count; ++i)
        type_refs_resolved[i] = type_refs.all[i].Get();
    }
  }
}

}

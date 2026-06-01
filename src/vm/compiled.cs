using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace bhl
{

public class LocalVarTable
{
  Dictionary<int, Dictionary<int, string>> _data = new Dictionary<int, Dictionary<int, string>>();

  public void Add(int func_ip, int slot_idx, string name)
  {
    if(!_data.TryGetValue(func_ip, out var slots))
      _data[func_ip] = slots = new Dictionary<int, string>();
    slots[slot_idx] = name;
  }

  public string TryGet(int func_ip, int slot_idx)
  {
    if(_data.TryGetValue(func_ip, out var slots) && slots.TryGetValue(slot_idx, out var name))
      return name;
    return null;
  }

  public void Write(System.IO.BinaryWriter w)
  {
    w.Write(_data.Count);
    foreach(var kv in _data)
    {
      w.Write(kv.Key);
      w.Write(kv.Value.Count);
      foreach(var slot in kv.Value)
      {
        w.Write(slot.Key);
        w.Write(slot.Value);
      }
    }
  }

  public static LocalVarTable Read(System.IO.BinaryReader r)
  {
    var t = new LocalVarTable();
    int func_count = r.ReadInt32();
    for(int i = 0; i < func_count; ++i)
    {
      int func_ip = r.ReadInt32();
      int slot_count = r.ReadInt32();
      for(int j = 0; j < slot_count; ++j)
        t.Add(func_ip, r.ReadInt32(), r.ReadString());
    }
    return t;
  }
}

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

  // ip2src_line stores the LAST byte of each instruction (to support TryMap's range search).
  // Breakpoints however must be set at the FIRST byte (the opcode), which is what exec.ip
  // holds when the dispatch hook fires. The first byte of instruction i is ips[i-1]+1 for i>0,
  // or 0 for i=0.
  public int FindIpForLine(int line)
  {
    for(int i = 0; i < lines.Count; i++)
      if(lines[i] == line)
        return i == 0 ? 0 : ips[i - 1] + 1;
    return -1;
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
  public LocalVarTable local_var_table;
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

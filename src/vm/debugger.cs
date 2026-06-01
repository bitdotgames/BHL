using System;
using System.Collections.Generic;

namespace bhl
{

public class VMDebugger
{
  public struct BreakpointHit
  {
    public VM.ExecState exec;
    public Module module;
    public int ip;
    public int line => module?.decl.compiled.ip2src_line.TryMap(ip) ?? 0;
  }

  HashSet<int> _breakpoints = new HashSet<int>();
  // per source file: file_path → set of active IPs
  Dictionary<string, HashSet<int>> _by_source = new Dictionary<string, HashSet<int>>();

  public Action<BreakpointHit> OnBreakpoint;

  public void AddBreakpoint(int ip)
  {
    _breakpoints.Add(ip);
  }

  //NOTE: returns false if no opcode maps to that source line
  public bool AddBreakpoint(Module module, int line)
  {
    int ip = module.decl.compiled.ip2src_line.FindIpForLine(line);
    if(ip < 0)
      return false;
    _breakpoints.Add(ip);
    if(!_by_source.TryGetValue(module.file_path, out var ips))
      _by_source[module.file_path] = ips = new HashSet<int>();
    ips.Add(ip);
    return true;
  }

  // Sets all breakpoints for a source file, replacing any previously set for it.
  // Returns the resolved IP for each requested line (-1 if the line has no opcode).
  public List<int> SetBreakpointsForSource(Module module, IEnumerable<int> lines)
  {
    var path = module.file_path;

    // remove old IPs for this source
    if(_by_source.TryGetValue(path, out var old_ips))
      foreach(var old_ip in old_ips)
        _breakpoints.Remove(old_ip);

    var new_ips = new HashSet<int>();
    var resolved = new List<int>();

    foreach(var line in lines)
    {
      int ip = module.decl.compiled.ip2src_line.FindIpForLine(line);
      resolved.Add(ip);
      if(ip >= 0)
      {
        _breakpoints.Add(ip);
        new_ips.Add(ip);
      }
    }

    _by_source[path] = new_ips;
    return resolved;
  }

  public void RemoveBreakpoint(int ip) => _breakpoints.Remove(ip);

  public void ClearBreakpoints()
  {
    _breakpoints.Clear();
    _by_source.Clear();
  }

  internal void TryFire(VM.ExecState exec, int ip)
  {
    if(!_breakpoints.Contains(ip))
      return;

    ref var frame = ref exec.frames[exec.regions[exec.regions_count - 1].frame_idx];
    if(frame.module == null)
      return;

    // IPs are local to each module's bytecode, so verify the hit belongs to this module.
    if(!_by_source.TryGetValue(frame.module.file_path, out var src_ips) || !src_ips.Contains(ip))
      return;

    OnBreakpoint?.Invoke(new BreakpointHit
    {
      exec   = exec,
      module = frame.module,
      ip     = ip,
    });
  }
}

}

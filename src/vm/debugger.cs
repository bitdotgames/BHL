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
    return true;
  }

  public void RemoveBreakpoint(int ip) => _breakpoints.Remove(ip);

  public void ClearBreakpoints() => _breakpoints.Clear();

  internal void TryFire(VM.ExecState exec, int ip)
  {
    if(!_breakpoints.Contains(ip))
      return;

    ref var frame = ref exec.frames[exec.regions[exec.regions_count - 1].frame_idx];
    OnBreakpoint?.Invoke(new BreakpointHit
    {
      exec   = exec,
      module = frame.module,
      ip     = ip,
    });
  }
}

}

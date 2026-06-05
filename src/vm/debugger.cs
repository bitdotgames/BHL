using System;
using System.Collections.Generic;

namespace bhl
{

public class VMDebugger
{
  public enum StepMode { None, Into, Over, Out }

  public struct BreakpointHit
  {
    public VM.ExecState exec;
    public Module module;
    public int ip;
    public string reason; // "breakpoint" or "step"
    public int line => module?.decl.compiled.ip2src_line.TryMap(ip) ?? 0;
  }

  HashSet<int> _breakpoints = new HashSet<int>();
  // per source file: file_path → set of active IPs
  Dictionary<string, HashSet<int>> _by_source = new Dictionary<string, HashSet<int>>();

  StepMode _step_mode = StepMode.None;
  VM.ExecState _step_exec;
  int _step_start_line;
  int _step_start_frames;

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

  public void StartStep(StepMode mode, VM.ExecState exec, int ip)
  {
    _step_mode         = mode;
    _step_exec         = exec;
    _step_start_frames = exec.frames_count;

    ref var frame = ref exec.frames[exec.regions[exec.regions_count - 1].frame_idx];
    _step_start_line = frame.module?.decl.compiled.ip2src_line.TryMap(ip) ?? 0;
  }

  public void ClearStep()
  {
    _step_mode = StepMode.None;
    _step_exec = null;
  }

  internal void TryFire(VM.ExecState exec, int ip)
  {
    TryFireBreakpoint(exec, ip);
    if(_step_mode != StepMode.None)
      TryFireStep(exec, ip);
  }

  void TryFireBreakpoint(VM.ExecState exec, int ip)
  {
    if(!_breakpoints.Contains(ip))
      return;

    ref var frame = ref exec.frames[exec.regions[exec.regions_count - 1].frame_idx];
    if(frame.module == null)
      return;

    // IPs are local to each module's bytecode, so verify the hit belongs to this module.
    if(!_by_source.TryGetValue(frame.module.file_path, out var src_ips) || !src_ips.Contains(ip))
      return;

    _step_mode = StepMode.None;
    OnBreakpoint?.Invoke(new BreakpointHit
    {
      exec   = exec,
      module = frame.module,
      ip     = ip,
      reason = "breakpoint",
    });
  }

  // Opcodes that are compiler-generated lambda setup machinery and should
  // never be step targets — the user has no source line to step to here.
  static bool IsLambdaSetupOpcode(Module module, int ip)
  {
    var bytecode = module?.decl.compiled.bytecode;
    if(bytecode == null || ip < 0 || ip >= bytecode.Length)
      return false;
    var op = (Opcodes)bytecode[ip];
    return op == Opcodes.GetFuncIpPtr || op == Opcodes.SetUpval;
  }

  void TryFireStep(VM.ExecState exec, int ip)
  {
    // StepOver/Out must stay within the original fiber.
    // StepInto follows execution into child coroutines (yield someCoroutine()).
    if(_step_mode != StepMode.Into && exec != _step_exec)
      return;

    ref var frame = ref exec.frames[exec.regions[exec.regions_count - 1].frame_idx];
    if(frame.module == null)
      return;

    if(IsLambdaSetupOpcode(frame.module, ip))
      return;

    int current_line   = frame.module.decl.compiled.ip2src_line.TryMap(ip);
    int current_frames = exec.frames_count;

    bool should_stop = _step_mode switch
    {
      StepMode.Into => current_line > 0 && current_line != _step_start_line,
      StepMode.Over => current_line > 0 && current_line != _step_start_line && current_frames <= _step_start_frames,
      StepMode.Out  => current_frames < _step_start_frames,
      _             => false,
    };

    if(!should_stop)
      return;

    _step_mode = StepMode.None;
    OnBreakpoint?.Invoke(new BreakpointHit
    {
      exec   = exec,
      module = frame.module,
      ip     = ip,
      reason = "step",
    });
  }
}

}

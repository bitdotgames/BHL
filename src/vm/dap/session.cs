using System;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace bhl.dap
{

// Manages the debug state for one DAP connection.
// The VM is driven externally (e.g. Unity game loop); this class only
// attaches a VMDebugger to it and handles the pause/resume handshake.
public class DebugSession
{
  public VM vm { get; private set; }
  public VMDebugger debugger { get; private set; }

  // Non-null while the VM is paused at a breakpoint.
  public VMDebugger.BreakpointHit? stopped_hit { get; private set; }

  readonly Transport _transport;
  readonly SemaphoreSlim _resume = new SemaphoreSlim(0, 1);
  CancellationToken _ct;

  // Optional hooks for platform-specific pause/resume (e.g. EditorApplication.isPaused).
  // Set before the first breakpoint fires.
  public System.Action OnPause;
  public System.Action OnResume;

  public DebugSession(VM vm, Transport transport)
  {
    this.vm = vm;
    _transport = transport;

    debugger = new VMDebugger();
    vm.debugger = debugger;

    // Fires on the VM thread (e.g. Unity main thread); blocks it until Continue().
    debugger.OnBreakpoint = hit =>
    {
      stopped_hit = hit;
      OnPause?.Invoke();

      _ = _transport.SendEventAsync("stopped", new JObject
      {
        ["reason"]            = "breakpoint",
        ["threadId"]          = 1,
        ["allThreadsStopped"] = true,
      });

      _resume.Wait(_ct);
      OnResume?.Invoke();
      stopped_hit = null;
    };
  }

  public void SetCancellationToken(CancellationToken ct) => _ct = ct;

  // Resume the VM thread paused at a breakpoint.
  public void Continue()
  {
    if(stopped_hit != null)
      _resume.Release();
  }

  // Detach the debugger and unblock the VM thread if paused.
  public void Detach()
  {
    vm.debugger = null;
    try { _resume.Release(); } catch(SemaphoreFullException) { }
  }

  // -----------------------------------------------------------------------
  // State inspection (only valid while stopped_hit != null)

  public JArray BuildStackFrames()
  {
    var frames = new JArray();
    if(stopped_hit == null)
      return frames;

    var exec = stopped_hit.Value.exec;
    for(int i = exec.frames_count - 1; i >= 0; --i)
    {
      var frame = exec.frames[i];
      if(frame.module == null) continue;

      // top frame uses the captured breakpoint IP; others use the return address
      int ip = (i == exec.frames_count - 1)
        ? stopped_hit.Value.ip
        : exec.frames[i + 1].return_ip;

      int line  = frame.module.decl.compiled.ip2src_line.TryMap(ip);
      var fsymb = frame.module.decl.TryMapIp2Func(frame.start_ip);

      frames.Add(new JObject
      {
        ["id"]     = i,
        ["name"]   = fsymb?.name ?? "?",
        ["line"]   = line,
        ["column"] = 1,
        ["source"] = new JObject
        {
          ["name"] = frame.module.name,
          ["path"] = frame.module.file_path,
        },
      });
    }
    return frames;
  }

  public JArray BuildLocals(int frame_idx)
  {
    var vars = new JArray();
    if(stopped_hit == null) return vars;

    var exec = stopped_hit.Value.exec;
    if(frame_idx < 0 || frame_idx >= exec.frames_count) return vars;

    var frame  = exec.frames[frame_idx];
    int offset = frame.locals_offset;
    int count  = frame.locals_vars_num;

    for(int i = 0; i < count; ++i)
    {
      ref var v = ref frame.locals.vals[offset + i];
      vars.Add(new JObject
      {
        ["name"]               = $"local_{i}",
        ["value"]              = ValToString(v),
        ["variablesReference"] = 0,
      });
    }
    return vars;
  }

  static string ValToString(Val v)
  {
    if(v.type == Types.Int)    return ((long)v.num).ToString();
    if(v.type == Types.Float)  return v.num.ToString();
    if(v.type == Types.Bool)   return v.num != 0 ? "true" : "false";
    if(v.type == Types.String) return $"\"{v.str}\"";
    if(v.obj != null)          return v.obj.GetType().Name;
    return "null";
  }
}

}

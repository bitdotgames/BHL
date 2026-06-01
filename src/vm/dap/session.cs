using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace bhl.dap
{

// Manages the debug state for one DAP connection.
// The VM is driven externally (e.g. Unity game loop); this class only
// attaches a VMDebugger to it and handles the pause/resume handshake.
//
// While paused, the main (VM) thread does not simply block — it spins on a
// work queue so that the DAP background thread can safely dispatch variable
// inspection calls back onto the main thread.  This is required in hosts like
// Unity where accessing scene objects from a background thread is forbidden.
public class DebugSession
{
  public VM vm { get; private set; }
  public VMDebugger debugger { get; private set; }

  // Non-null while the VM is paused at a breakpoint.
  public VMDebugger.BreakpointHit? stopped_hit { get; private set; }

  readonly Transport _transport;
  CancellationToken _ct;

  // Optional hooks for platform-specific pause/resume (e.g. EditorApplication.isPaused).
  // Set before the first breakpoint fires.
  public System.Action OnPause;
  public System.Action OnResume;

  // -----------------------------------------------------------------------
  // Main-thread dispatch

  class WorkItem
  {
    public Func<object> action;
    public object result;
    public Exception error;
    public readonly ManualResetEventSlim done = new ManualResetEventSlim(false);
  }

  readonly ConcurrentQueue<WorkItem> _work_queue = new ConcurrentQueue<WorkItem>();
  readonly SemaphoreSlim _trigger = new SemaphoreSlim(0);
  volatile bool _resume_requested;

  // Called from the DAP background thread; runs action on the main thread and
  // blocks until it completes.
  T RunOnMainThread<T>(Func<T> action)
  {
    var item = new WorkItem { action = () => action() };
    _work_queue.Enqueue(item);
    _trigger.Release();
    item.done.Wait(_ct);
    if(item.error != null)
      throw new Exception(item.error.Message, item.error);
    return (T)item.result;
  }

  // -----------------------------------------------------------------------
  // Variable-tree registry — populated lazily; cleared on Continue()

  Dictionary<int, Func<JArray>> _var_registry = new Dictionary<int, Func<JArray>>();
  int _next_var_ref = 10000;

  int AllocVarRef(Func<JArray> builder)
  {
    int id = _next_var_ref++;
    _var_registry[id] = builder;
    return id;
  }

  void ClearVarRegistry()
  {
    _var_registry.Clear();
    _next_var_ref = 10000;
  }

  // -----------------------------------------------------------------------

  public DebugSession(VM vm, Transport transport)
  {
    this.vm = vm;
    _transport = transport;

    debugger = new VMDebugger();
    vm.debugger = debugger;

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

      // Spin on the work queue so the DAP thread can safely inspect VM state
      // on this (main) thread while the VM is paused.
      _resume_requested = false;
      while(!_resume_requested)
      {
        _trigger.Wait(_ct);
        while(_work_queue.TryDequeue(out var item))
        {
          try   { item.result = item.action(); }
          catch(Exception e) { item.error = e; }
          item.done.Set();
        }
      }

      OnResume?.Invoke();
      stopped_hit = null;
    };
  }

  public void SetCancellationToken(CancellationToken ct) => _ct = ct;

  public void Continue()
  {
    ClearVarRegistry();
    _resume_requested = true;
    _trigger.Release();
  }

  public void Detach()
  {
    vm.debugger = null;
    _resume_requested = true;
    _trigger.Release();
  }

  // -----------------------------------------------------------------------
  // Public API — all dispatched to the main thread

  public JArray BuildStackFrames() => RunOnMainThread(BuildStackFramesInternal);
  public JArray BuildLocals(int frame_idx) => RunOnMainThread(() => BuildLocalsInternal(frame_idx));
  public JArray BuildVarChildren(int var_ref) => RunOnMainThread(() => BuildVarChildrenInternal(var_ref));

  public string BuildLocalsDebugInfo(int frame_idx) => RunOnMainThread(() =>
  {
    if(stopped_hit == null) return "stopped_hit=null";
    var exec = stopped_hit.Value.exec;
    if(frame_idx < 0 || frame_idx >= exec.frames_count) return $"frame_idx={frame_idx} out of range ({exec.frames_count})";
    var frame = exec.frames[frame_idx];
    var table = frame.module?.decl.compiled.local_var_table;
    if(table == null)
      return $"local_var_table=null module={frame.module?.name ?? "null"} start_ip={frame.start_ip}";
    return $"local_var_table present module={frame.module.name} start_ip={frame.start_ip}";
  });

  // -----------------------------------------------------------------------
  // Implementations — run on main thread only

  JArray BuildStackFramesInternal()
  {
    var frames = new JArray();
    if(stopped_hit == null) return frames;

    var exec = stopped_hit.Value.exec;
    for(int i = exec.frames_count - 1; i >= 0; --i)
    {
      var frame = exec.frames[i];
      if(frame.module == null) continue;

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

  JArray BuildLocalsInternal(int frame_idx)
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
      var v    = frame.locals.vals[offset + i];
      var name = frame.module?.decl.compiled.local_var_table?.TryGet(frame.start_ip, i) ?? $"local_{i}";
      vars.Add(ValToVar(name, v));
    }
    return vars;
  }

  JArray BuildVarChildrenInternal(int var_ref)
  {
    if(_var_registry.TryGetValue(var_ref, out var builder))
      return builder();
    return new JArray();
  }

  // -----------------------------------------------------------------------
  // Variable tree builders — all run on main thread via Build*Internal

  JObject ValToVar(string name, Val v)
  {
    string display;
    string type_name = v.type?.GetName() ?? "";
    int var_ref = 0;

    if(v.type == Types.Int)
      display = ((long)v.num).ToString();
    else if(v.type == Types.Float)
      display = v.num.ToString("G");
    else if(v.type == Types.Bool)
      display = v.num != 0 ? "true" : "false";
    else if(v.type == Types.String)
      display = $"\"{v.str}\"";
    else if(v.obj == null)
      display = "null";
    else if(v.type is ArrayTypeSymbol && v.obj is ValList arr)
    {
      display = $"[{arr.Count}]";
      var cap = arr;
      var_ref = AllocVarRef(() => BuildArrayChildren(cap));
    }
    else if(v.obj is ValMap map)
    {
      display = $"{{{map.Count}}}";
      var cap = map;
      var_ref = AllocVarRef(() => BuildMapChildren(cap));
    }
    else if(v.type is ClassSymbolScript css && v.obj is ValList fields)
    {
      display = $"{{{css.name}}}";
      var css_cap    = css;
      var fields_cap = fields;
      var_ref = AllocVarRef(() => BuildScriptClassChildren(css_cap, fields_cap));
    }
    else if(v.type is ClassSymbolNative csn)
    {
      var native = csn.GetNativeObject(v);
      type_name = native?.GetType().Name ?? csn.name;
      display   = native != null ? $"<{type_name}>" : "null";
      if(native != null)
      {
        var cap = native;
        var_ref = AllocVarRef(() => BuildNativeChildren(cap));
      }
    }
    else
      display = v.obj.GetType().Name;

    return new JObject
    {
      ["name"]               = name,
      ["value"]              = display,
      ["type"]               = type_name,
      ["variablesReference"] = var_ref,
    };
  }

  JArray BuildArrayChildren(ValList arr)
  {
    var result = new JArray();
    for(int i = 0; i < arr.Count; ++i)
      result.Add(ValToVar($"[{i}]", arr[i]));
    return result;
  }

  JArray BuildMapChildren(ValMap map)
  {
    var result = new JArray();
    foreach(var kv in map)
      result.Add(ValToVar(ValDisplay(kv.Key), kv.Value));
    return result;
  }

  JArray BuildScriptClassChildren(ClassSymbolScript css, IList<Val> fields)
  {
    var result = new JArray();
    foreach(var m in css)
    {
      if(!(m is FieldSymbolScript fld)) continue;
      if(fld.attribs.HasFlag(FieldAttrib.Static)) continue;
      if(fld.scope_idx < 0 || fld.scope_idx >= fields.Count) continue;
      result.Add(ValToVar(fld.name, fields[fld.scope_idx]));
    }
    return result;
  }

  JArray BuildNativeChildren(object native)
  {
    var result = new JArray();
    var type   = native.GetType();

    foreach(var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      if(!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
      try
      {
        var val = prop.GetValue(native);
        result.Add(NativeLeaf(prop.Name, prop.PropertyType.Name, val));
      }
      catch(Exception e) { result.Add(NativeLeaf(prop.Name, prop.PropertyType.Name, $"<{e.Message}>")); }
    }

    foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
      try
      {
        var val = field.GetValue(native);
        result.Add(NativeLeaf(field.Name, field.FieldType.Name, val));
      }
      catch(Exception e) { result.Add(NativeLeaf(field.Name, field.FieldType.Name, $"<{e.Message}>")); }
    }

    return result;
  }

  static JObject NativeLeaf(string name, string type_name, object val)
  {
    string display;
    try   { display = val?.ToString() ?? "null"; }
    catch { display = $"<{val?.GetType().Name ?? type_name}>"; }
    return new JObject
    {
      ["name"]               = name,
      ["value"]              = display,
      ["type"]               = type_name,
      ["variablesReference"] = 0,
    };
  }

  static string ValDisplay(Val v)
  {
    if(v.type == Types.Int)    return ((long)v.num).ToString();
    if(v.type == Types.Float)  return v.num.ToString("G");
    if(v.type == Types.Bool)   return v.num != 0 ? "true" : "false";
    if(v.type == Types.String) return $"\"{v.str}\"";
    if(v.obj != null)          return v.obj.GetType().Name;
    return "null";
  }
}

}

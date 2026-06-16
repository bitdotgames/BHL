using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace bhl.dap
{

// Embedded TCP DAP server for Unity (or any external VM host).
// Usage:
//   var server = new BHLDebugServer(vm);
//   server.StartListening(7777);   // non-blocking
//   // Unity game loop calls vm.Tick() as usual
//   // On shutdown:
//   server.Stop();
public class BHLDebugServer
{
  readonly VM _vm;
  TcpListener _listener;
  CancellationTokenSource _cts;
  DebugSession _session;
  Transport _transport;

  // Signaled when configurationDone is received; used by WaitForClient().
  readonly SemaphoreSlim _configuration_done = new SemaphoreSlim(0, 1);

  // Optional platform hooks wired by the host (e.g. EditorApplication.isPaused in Unity).
  public System.Action OnPause;
  public System.Action OnResume;

  struct PendingBreakpoint
  {
    public int id;
    public int line;
  }

  // Breakpoints received before the module was loaded: file_path → list of (id, line).
  readonly Dictionary<string, List<PendingBreakpoint>> _pending_bps =
    new Dictionary<string, List<PendingBreakpoint>>();
  readonly object _pending_lock = new object();
  int _next_bp_id = 1;

  public BHLDebugServer(VM vm)
  {
    _vm = vm;
  }

  // Starts the TCP listener on a background thread; returns immediately.
  public void StartListening(int port = 7777)
  {
    _vm.OnModuleLoaded += OnModuleLoaded;
    _cts = new CancellationTokenSource();
    _listener = new TcpListener(IPAddress.Loopback, port);
    _listener.Start();
    Task.Run(() => AcceptLoopAsync(_cts.Token));
  }

  // Blocks the calling thread until a DAP client connects and sends
  // configurationDone (i.e. all initial breakpoints are registered).
  // Call this before starting BHL execution to avoid the attach race.
  // Returns true if a client attached in time, false on timeout.
  public bool WaitForClient(int timeout_ms = Timeout.Infinite)
  {
    return _configuration_done.Wait(timeout_ms);
  }

  public void Stop()
  {
    _vm.OnModuleLoaded -= OnModuleLoaded;
    _cts?.Cancel();
    _session?.Detach();
    _listener?.Stop();
  }

  async Task AcceptLoopAsync(CancellationToken ct)
  {
    while(!ct.IsCancellationRequested)
    {
      TcpClient client;
      try { client = await _listener.AcceptTcpClientAsync(); }
      catch { break; }

      var stream    = client.GetStream();
      _transport        = new Transport(stream, stream);
      _session          = new DebugSession(_vm, _transport);
      _session.OnPause  = OnPause;
      _session.OnResume = OnResume;
      _session.OnLog    = msg => _ = _transport.SendEventAsync("output", new JObject
      {
        ["category"] = "console",
        ["output"]   = "[bhl] " + msg + "\n",
      });
      _session.SetCancellationToken(ct);

      try { await RunSessionAsync(_transport, ct); }
      finally
      {
        _session.Detach();
        _session   = null;
        _transport = null;
        client.Close();
      }
    }
  }

  async Task RunSessionAsync(Transport transport, CancellationToken ct)
  {
    while(!ct.IsCancellationRequested)
    {
      var msg = await transport.ReadAsync(ct);
      if(msg == null) break;

      await DispatchAsync(transport, msg, ct);
    }
  }

  async Task DispatchAsync(Transport transport, JObject msg, CancellationToken ct)
  {
    var command = msg["command"]?.ToString();
    try
    {
      switch(command)
      {
        case "initialize":       await OnInitialize(transport, msg); break;
        case "attach":           await OnAttach(transport, msg); break;
        case "setBreakpoints":   await OnSetBreakpoints(transport, msg); break;
        case "configurationDone":await OnConfigurationDone(transport, msg); break;
        case "threads":          await OnThreads(transport, msg); break;
        case "stackTrace":       await OnStackTrace(transport, msg); break;
        case "scopes":           await OnScopes(transport, msg); break;
        case "variables":        await OnVariables(transport, msg); break;
        case "continue":         await OnContinue(transport, msg); break;
        case "next":             await OnNext(transport, msg); break;
        case "stepIn":           await OnStepIn(transport, msg); break;
        case "stepOut":          await OnStepOut(transport, msg); break;
        case "evaluate":         await OnEvaluate(transport, msg); break;
        case "disconnect":       await OnDisconnect(transport, msg, ct); break;
        default:
          await transport.SendResponseAsync(msg, true); // ack unknown requests
          break;
      }
    }
    catch(Exception e)
    {
      await transport.SendResponseAsync(msg, false, new JObject
      {
        ["error"] = new JObject { ["id"] = 1, ["format"] = $"{command}: {e.Message}" }
      });
    }
  }

  // Called on the VM/main thread when a module is registered.
  void OnModuleLoaded(Module module)
  {
    List<PendingBreakpoint> pending;
    lock(_pending_lock)
    {
      if(!_pending_bps.TryGetValue(module.file_path, out pending))
        return;
      _pending_bps.Remove(module.file_path);
    }

    var session   = _session;
    var transport = _transport;
    if(session == null || transport == null)
      return;

    var lines        = new List<int>(pending.Count);
    foreach(var p in pending) lines.Add(p.line);
    var resolved_ips = session.debugger.SetBreakpointsForSource(module, lines);

    for(int i = 0; i < pending.Count; ++i)
    {
      bool verified = resolved_ips[i] >= 0;
      _ = transport.SendEventAsync("breakpoint", new JObject
      {
        ["reason"] = "changed",
        ["breakpoint"] = new JObject
        {
          ["id"]       = pending[i].id,
          ["verified"] = verified,
          ["line"]     = pending[i].line,
        }
      });
    }
  }

  // -----------------------------------------------------------------------

  async Task OnInitialize(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true, new JObject
    {
      ["supportsConfigurationDoneRequest"] = true,
      ["supportsTerminateRequest"]         = true,
      ["supportsStepBack"]                 = false,
      ["supportsEvaluateForHovers"]        = true,
    });
    await t.SendEventAsync("initialized");
  }

  async Task OnAttach(Transport t, JObject req)
  {
    // Fresh session: VS Code will re-send all breakpoints; clear stale pending state.
    lock(_pending_lock) { _pending_bps.Clear(); }
    await t.SendResponseAsync(req, true);
  }

  async Task OnSetBreakpoints(Transport t, JObject req)
  {
    var args        = req["arguments"] as JObject;
    var source_path = args?["source"]?["path"]?.ToString();
    var bp_arr      = args?["breakpoints"] as JArray ?? new JArray();

    var result_bps = new JArray();

    var module = source_path != null ? _vm.FindModuleByPath(source_path) : null;
    if(module != null)
    {
      var lines = new List<int>();
      foreach(var bp in bp_arr)
        lines.Add(bp["line"]?.Value<int>() ?? 0);

      var resolved_ips = _session.debugger.SetBreakpointsForSource(module, lines);

      for(int i = 0; i < lines.Count; ++i)
      {
        bool verified = resolved_ips[i] >= 0;
        result_bps.Add(new JObject
        {
          ["id"]       = _next_bp_id++,
          ["verified"] = verified,
          ["line"]     = lines[i],
        });
      }
    }
    else
    {
      // Module not loaded yet — store as pending and report unverified.
      var pending = new List<PendingBreakpoint>(bp_arr.Count);
      foreach(var bp in bp_arr)
      {
        int line  = bp["line"]?.Value<int>() ?? 0;
        int bp_id = _next_bp_id++;
        pending.Add(new PendingBreakpoint { id = bp_id, line = line });
        result_bps.Add(new JObject { ["id"] = bp_id, ["verified"] = false, ["line"] = line });
      }
      if(source_path != null)
        lock(_pending_lock) { _pending_bps[source_path] = pending; }
    }

    await t.SendResponseAsync(req, true, new JObject { ["breakpoints"] = result_bps });
  }

  async Task OnConfigurationDone(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true);
    try { _configuration_done.Release(); } catch(SemaphoreFullException) { }
  }

  async Task OnThreads(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true, new JObject
    {
      ["threads"] = new JArray
      {
        new JObject { ["id"] = 1, ["name"] = "BHL Main" }
      }
    });
  }

  async Task OnStackTrace(Transport t, JObject req)
  {
    var frames = _session.BuildStackFrames();
    await t.SendResponseAsync(req, true, new JObject
    {
      ["stackFrames"] = frames,
      ["totalFrames"] = frames.Count,
    });
  }

  async Task OnScopes(Transport t, JObject req)
  {
    int frame_id = req["arguments"]?["frameId"]?.Value<int>() ?? 0;
    // encode frame_id into variablesReference so OnVariables knows which frame
    int var_ref = frame_id * 1000 + 1;
    await t.SendResponseAsync(req, true, new JObject
    {
      ["scopes"] = new JArray
      {
        new JObject
        {
          ["name"]               = "Locals",
          ["variablesReference"] = var_ref,
          ["expensive"]          = false,
        }
      }
    });
  }

  async Task OnVariables(Transport t, JObject req)
  {
    int var_ref = req["arguments"]?["variablesReference"]?.Value<int>() ?? 0;
    JArray vars;
    if(var_ref >= 10000)
    {
      vars = _session.BuildVarChildren(var_ref);
    }
    else
    {
      int frame_id = (var_ref - 1) / 1000;
      vars = _session.BuildLocals(frame_id);
      await t.SendEventAsync("output", new JObject
      {
        ["category"] = "console",
        ["output"]   = "[bhl] " + _session.BuildLocalsDebugInfo(frame_id) + "\n",
      });
    }
    await t.SendResponseAsync(req, true, new JObject { ["variables"] = vars });
  }

  async Task OnEvaluate(Transport t, JObject req)
  {
    var args     = req["arguments"] as JObject;
    var expr     = args?["expression"]?.ToString() ?? "";
    int frame_id = args?["frameId"]?.Value<int>() ?? 0;

    var result = _session.EvalExpression(expr, frame_id);
    await t.SendResponseAsync(req, true, result);
  }

  async Task OnContinue(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true, new JObject { ["allThreadsContinued"] = true });
    await t.SendEventAsync("continued", new JObject { ["threadId"] = 1, ["allThreadsContinued"] = true });
    _session.Continue();
  }

  async Task OnNext(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true);
    await t.SendEventAsync("continued", new JObject { ["threadId"] = 1, ["allThreadsContinued"] = true });
    _session.StepOver();
  }

  async Task OnStepIn(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true);
    await t.SendEventAsync("continued", new JObject { ["threadId"] = 1, ["allThreadsContinued"] = true });
    _session.StepInto();
  }

  async Task OnStepOut(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true);
    await t.SendEventAsync("continued", new JObject { ["threadId"] = 1, ["allThreadsContinued"] = true });
    _session.StepOut();
  }

  async Task OnDisconnect(Transport t, JObject req, CancellationToken ct)
  {
    await t.SendResponseAsync(req, true);
    _session.Detach();
    _cts?.Cancel();
  }
}

}

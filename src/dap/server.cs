using System;
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

  // Optional platform hooks wired by the host (e.g. EditorApplication.isPaused in Unity).
  public System.Action OnPause;
  public System.Action OnResume;

  public BHLDebugServer(VM vm)
  {
    _vm = vm;
  }

  // Starts the TCP listener on a background thread; returns immediately.
  public void StartListening(int port = 7777)
  {
    _cts = new CancellationTokenSource();
    _listener = new TcpListener(IPAddress.Loopback, port);
    _listener.Start();
    Task.Run(() => AcceptLoopAsync(_cts.Token));
  }

  public void Stop()
  {
    _cts?.Cancel();
    _session?.Detach();
    _listener?.Stop();
  }

  async Task AcceptLoopAsync(CancellationToken ct)
  {
    while(!ct.IsCancellationRequested)
    {
      TcpClient client;
      try { client = await _listener.AcceptTcpClientAsync(ct); }
      catch { break; }

      var stream    = client.GetStream();
      var transport = new Transport(stream, stream);
      _session          = new DebugSession(_vm, transport);
      _session.OnPause  = OnPause;
      _session.OnResume = OnResume;
      _session.SetCancellationToken(ct);

      try { await RunSessionAsync(transport, ct); }
      finally
      {
        _session.Detach();
        _session = null;
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
      case "disconnect":       await OnDisconnect(transport, msg, ct); break;
      default:
        await transport.SendResponseAsync(msg, true); // ack unknown requests
        break;
    }
  }

  // -----------------------------------------------------------------------

  async Task OnInitialize(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true, new JObject
    {
      ["supportsConfigurationDoneRequest"] = true,
      ["supportsTerminateRequest"]         = true,
    });
    await t.SendEventAsync("initialized");
  }

  async Task OnAttach(Transport t, JObject req)
  {
    // VM is already running externally; nothing to launch.
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
      var lines = new System.Collections.Generic.List<int>();
      foreach(var bp in bp_arr)
        lines.Add(bp["line"]?.Value<int>() ?? 0);

      var resolved_ips = _session.debugger.SetBreakpointsForSource(module, lines);

      for(int i = 0; i < lines.Count; ++i)
      {
        bool verified = resolved_ips[i] >= 0;
        result_bps.Add(new JObject
        {
          ["verified"] = verified,
          ["line"]     = lines[i],
        });
      }
    }
    else
    {
      // source not loaded yet — report all as unverified
      foreach(var bp in bp_arr)
        result_bps.Add(new JObject { ["verified"] = false, ["line"] = bp["line"] });
    }

    await t.SendResponseAsync(req, true, new JObject { ["breakpoints"] = result_bps });
  }

  async Task OnConfigurationDone(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true);
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
    int var_ref  = req["arguments"]?["variablesReference"]?.Value<int>() ?? 0;
    int frame_id = (var_ref - 1) / 1000;
    var vars     = _session.BuildLocals(frame_id);
    await t.SendResponseAsync(req, true, new JObject { ["variables"] = vars });
  }

  async Task OnContinue(Transport t, JObject req)
  {
    await t.SendResponseAsync(req, true, new JObject { ["allThreadsContinued"] = true });
    await t.SendEventAsync("continued", new JObject { ["threadId"] = 1, ["allThreadsContinued"] = true });
    _session.Continue();
  }

  async Task OnDisconnect(Transport t, JObject req, CancellationToken ct)
  {
    await t.SendResponseAsync(req, true);
    _session.Detach();
    _cts?.Cancel();
  }
}

}

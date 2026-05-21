using System.Collections.Generic;
using UnityEngine;
using bhl;
using bhl.dap;

// Attach this MonoBehaviour to any GameObject in your scene.
// In the Editor, the BHL debug server starts automatically and
// listens for VS Code on port 7777.
//
// VS Code launch.json:
// {
//   "type": "bhl",
//   "request": "attach",
//   "name": "Attach to Unity BHL",
//   "host": "localhost",
//   "port": 7777
// }
public class BHLDebugExample : MonoBehaviour
{
  [Header("BHL")]
  public int debug_port = 7777;

  VM _vm;

#if BHL_DEBUGGER
  BHLDebugServer _debug_server;
#endif

  void Awake()
  {
    // --- 1. Build types and register your game bindings ---
    var types = new Types();
    // types.module.ns.Define(new FuncSymbolNative(...));  // your bindings here

    // --- 2. Compile and load BHL modules ---
    // In a real project you'd load pre-compiled .bhc files from StreamingAssets.
    // Here we compile inline for demonstration.
    var vm = BuildVM(types);
    if(vm == null)
    {
      Debug.LogError("BHL: failed to compile");
      return;
    }
    _vm = vm;

    // --- 3. Start the debug server (Editor-only by default) ---
#if BHL_DEBUGGER && UNITY_EDITOR
    _debug_server = new BHLDebugServer(_vm);

    // Pause/resume Unity Editor alongside the BHL breakpoint so the
    // toolbar reflects the paused state and the timeline stops.
    _debug_server.OnPause  = () => UnityEditor.EditorApplication.isPaused = true;
    _debug_server.OnResume = () => UnityEditor.EditorApplication.isPaused = false;

    _debug_server.StartListening(debug_port);
    Debug.Log($"BHL debug server listening on port {debug_port}");
#endif

    // --- 4. Kick off the entry-point BHL function ---
    if(_vm.Start("main") == null)
      Debug.LogWarning("BHL: no 'main' function found");
  }

  void Update()
  {
    if(_vm == null) return;

    // OnBreakpoint fires here (on the main thread).
    // The callback blocks this Update() call until VS Code sends "continue".
    // EditorApplication.isPaused is set inside the callback so the Editor
    // properly reflects the paused state in its toolbar.
    _vm.Tick();
  }

  void OnDestroy()
  {
#if BHL_DEBUGGER
    _debug_server?.Stop();
    _debug_server = null;
#endif
    _vm = null;
  }

  // Minimal inline compile — replace with your actual project's compilation step.
  static VM BuildVM(Types types)
  {
    string bhl_src = @"
func void main() {
  int x = 1
  int y = 2
  int z = x + y
}
";
    // Compile to an in-memory module.
    var proc = ANTLR_Processor.ParseAndMakeProcessor(
      new ModuleDeclared("game", "game.bhl"),
      null,
      new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(bhl_src)),
      types,
      CompileErrorsHub.MakeStandard("game.bhl"),
      null,
      out var _
    );

    ANTLR_Processor.ProcessAll(
      new ProjectCompilationStateBundle(types,
        new Dictionary<string, ANTLR_Processor> { { "game.bhl", proc } })
    );

    if(proc.result.errors.Count > 0)
    {
      foreach(var err in proc.result.errors)
        Debug.LogError($"BHL compile error: {err.text}");
      return null;
    }

    var compiler = new ModuleCompiler(proc.result);
    var module   = compiler.Compile();

    var vm = new VM(types);
    vm.LoadModule(new Module(module));
    return vm;
  }
}

# DAP — Debugger

BHL ships an embedded TCP debug adapter (`src/vm/dap/`) that implements the
[Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/).
VS Code (and any other DAP-capable editor) can connect to it and debug BHL
scripts running inside Unity or any other host.

## Architecture

```
Unity Editor                         VS Code
─────────────────────────────        ──────────────────────
MonoBehaviour.Update()               bhl-debug extension
  └─ vm.Tick()                         └─ DebugAdapterServer
       └─ breakpoint fires                  (built-in DAP client)
            └─ OnBreakpoint()                    │
                 ├─ EditorApplication             │  DAP over TCP
                 │    .isPaused = true ◄──────────┘
                 ├─ send "stopped" event ─────────►
                 └─ block (SemaphoreSlim)         │
                      ▲                  stackTrace/variables
                      │  "continue" ◄────────────
                      └─ resume, isPaused = false
```

- The `BHLDebugServer` runs a TCP listener on a background thread.
- BHL scripts run on Unity's main thread (game loop) as normal.
- When a breakpoint fires the main thread is blocked inside `vm.Tick()` and
  `EditorApplication.isPaused` is set to `true` so the Editor toolbar reflects
  the paused state.
- The background TCP thread handles all DAP requests while the main thread waits.
- On `continue` the semaphore is released and `isPaused` is cleared.

## Supported DAP requests

| Request | Description |
|---|---|
| `initialize` | Exchange capabilities |
| `attach` | Attach to an already-running VM |
| `setBreakpoints` | Set/replace breakpoints for a source file |
| `configurationDone` | Signal that initial configuration is complete |
| `threads` | Returns one thread: "BHL Main" |
| `stackTrace` | Current call stack with source locations |
| `scopes` | Variable scopes for a frame (Locals) |
| `variables` | Local variable values at the current frame |
| `evaluate` | Evaluate an expression in the current frame (watch/hover) |
| `continue` | Resume execution |
| `next` | Step over |
| `stepIn` | Step into |
| `stepOut` | Step out |
| `disconnect` | Detach debugger |

## Unity integration

Create a `BHLDebugServer` around your `VM` and start it listening:

```csharp
#if UNITY_EDITOR
_debug_server = new BHLDebugServer(_vm);
_debug_server.OnPause  = () => UnityEditor.EditorApplication.isPaused = true;
_debug_server.OnResume = () => UnityEditor.EditorApplication.isPaused = false;
_debug_server.StartListening(7777);  // 7777 is the default
#endif
```

`StartListening()` is non-blocking — Unity's game loop continues normally until
a breakpoint is hit.

> **Note**: the adapter lives in `src/vm/dap/` and is compiled into the VM
> assemblies unconditionally. Nothing runs until you construct a
> `BHLDebugServer`, so guard that construction with your own define (as the
> `UNITY_EDITOR` check above does) to keep it out of production builds.

## VS Code client

The client extension and a Unity example script used to live under
`example/dap/` in this repo; they were moved to a separate repository (see
commit `0ee6c82e`). Any DAP-capable client works — VS Code's built-in
`DebugAdapterServer` speaks the protocol, so the extension needs no npm
dependencies.

**Configure your project:**

Add this to your project's `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "bhl",
      "request": "attach",
      "name": "Attach to BHL (Unity)",
      "host": "localhost",
      "port": 7777
    }
  ]
}
```

**Debug session:**

1. Press **Play** in Unity, so your `BHLDebugServer` starts listening.
2. Press **F5** in VS Code (or open the Run & Debug panel and click **Attach to BHL (Unity)**).
3. Set breakpoints in `.bhl` files by clicking the gutter.
4. When execution reaches a breakpoint, Unity freezes and VS Code shows the call stack and local variables.
5. Step with **F10** / **F11** / **Shift+F11**, inspect or watch expressions, and press **Continue** (F5) to resume.

> Line mapping is always emitted, so breakpoints and stepping work on any
> compiled module. Compiling with `--debug-info` additionally emits local
> variable *names*, without which the `variables` request has nothing to label.

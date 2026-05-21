# DAP — Debugger

BHL ships an embedded TCP debug adapter (`src/dap/`) that implements the
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
| `continue` | Resume execution |
| `disconnect` | Detach debugger |

## Unity integration

Add the `BHLDebugServer` to your project (requires `BHL_DEBUGGER` defined and
`bhl_dap.dll` included). See `example/dap/unity/BHLDebugExample.cs` for a
complete working script:

```csharp
#if BHL_DEBUGGER && UNITY_EDITOR
_debug_server = new BHLDebugServer(_vm);
_debug_server.OnPause  = () => UnityEditor.EditorApplication.isPaused = true;
_debug_server.OnResume = () => UnityEditor.EditorApplication.isPaused = false;
_debug_server.StartListening(7777);
#endif
```

`StartListening()` is non-blocking — Unity's game loop continues normally until
a breakpoint is hit.

> **Note**: `BHL_DEBUGGER` must be defined when building `bhl_front` (it is by
> default) but intentionally **not** defined in `bhl_runtime` (the slim Unity
> assembly) so production builds have zero overhead.

## VS Code client

A minimal VS Code extension lives in `example/dap/vscode/`. It has no npm
dependencies — VS Code's built-in `DebugAdapterServer` handles the DAP protocol.

**Install (development):**

1. Open `example/dap/vscode/` in VS Code.
2. Press **F5** — an Extension Development Host window opens with the extension loaded.

**Configure your project:**

Copy `example/dap/vscode/sample.launch.json` to your project's `.vscode/launch.json`:

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

1. Press **Play** in Unity — the console prints `BHL debug server listening on port 7777`.
2. In the Extension Development Host, press **F5** (or open the Run & Debug panel and click **Attach to BHL (Unity)**).
3. Set breakpoints in `.bhl` files by clicking the gutter.
4. When execution reaches a breakpoint, Unity freezes and VS Code shows the call stack and local variables.
5. Press **Continue** (F5) to resume.

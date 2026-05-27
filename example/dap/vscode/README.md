# BHL Debug — VS Code Extension

A minimal VS Code extension that connects to the BHL debug server over TCP using the [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/).

No npm dependencies — VS Code's built-in `DebugAdapterServer` handles all DAP protocol framing.

## Files

| File | Purpose |
|---|---|
| `extension.js` | Extension entry point; registers the `BHLDebugAdapterDescriptorFactory` |
| `package.json` | Extension manifest; declares the `bhl` debug type and attach configuration |
| `sample.launch.json` | Ready-to-copy `.vscode/launch.json` for your project |

## Install

```sh
cd example/dap/vscode && make
```

This packs the extension with `vsce` and installs it via `code --install-extension`. Then **Reload Window** in VS Code (Cmd+Shift+P → "Reload Window").

| Target | Description |
|---|---|
| `make` / `make install` | Pack and install |
| `make pack` | Only build the `.vsix` |
| `make clean` | Remove the built `.vsix` |

## Development install

1. Open `example/dap/vscode/` in VS Code.
2. Press **F5** — an Extension Development Host opens with the extension loaded.

## Usage

1. Copy `sample.launch.json` to your project's `.vscode/launch.json`.
2. Press **Play** in Unity — the console prints `BHL debug server listening on port 7777`.
3. In VS Code open the Run & Debug panel and click **Attach to BHL (Unity)**.
4. Set breakpoints in `.bhl` files by clicking the gutter.
5. When execution reaches a breakpoint, Unity freezes and VS Code shows the call stack and local variables.
6. Press **Continue** (F5) to resume.

## Configuration

| Option | Default | Description |
|---|---|---|
| `host` | `localhost` | Host where the BHL debug server is running |
| `port` | `7777` | Port the BHL debug server listens on |

## Requirements

- The Unity project must have `BHL_DEBUGGER` defined and `bhl_dap.dll` included.
- See `example/dap/unity/` for the Unity-side integration example.

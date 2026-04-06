# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
make build        # Build the bhl CLI tool (dotnet build bhl.csproj)
make test         # Run all tests (cd tests && dotnet test)
make publish      # Publish bhl + LSP server
make bench        # Run benchmarks
make geng         # Regenerate ANTLR grammar (grammar/*.g4 → src/g/)
make examples     # Run the example project
```

Run a single test by name:
```sh
cd tests && dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

Environment variables:
- `BHL_REBUILD=1` — force full rebuild, bypass cache
- `BHL_VERBOSE=<level>` — compilation verbosity

## Project Structure

Four C# projects:

- **`bhl.csproj`** (Exe) — the `bhl` CLI tool; entry point is `src/taskman/`
- **`bhl_front.csproj`** (Library) — compiler frontend + VM (everything in `src/compile/` and `src/vm/`)
- **`bhl_runtime.csproj`** (Library) — VM only (`src/vm/`), for embedding in Unity without the compiler
- **`bhl_lsp.csproj`** (Library) — LSP server (`src/lsp/`)

Build outputs go to `build/<ProjectName>/` (configured in `Directory.Build.props`).

## Architecture

### Compilation pipeline (`src/compile/`)

1. **ANTLR parsing** (`src/g/`) — generated lexer/parser from `grammar/*.g4`; also a preprocessor grammar for `#if`/`#define`
2. **`ANTLR_Processor`** (`antlr_proc.cs` + `antlr_proc.*.cs`) — walks the parse tree, performs type checking, builds a typed AST; split across multiple files as `partial class`
3. **`ModuleCompiler`** (`compiler.cs`) — `AST_Visitor` that walks the typed AST and emits bytecode
4. **`CompilationExecutor`** (`executor.cs`) — drives the full pipeline with parallelism (up to 6 threads), file-level caching keyed on content hashes, and writes `.bhc` files
5. **`IFrontPostProcessor`** (`postproc.cs`) — optional user-supplied DLL hook to patch the AST before compilation
6. **Project config** (`proj_conf.cs`) — `bhl.proj` JSON files control source dirs, bindings DLL, threads, cache, output format

### VM / Runtime (`src/vm/`)

- **`VM`** (`vm.cs`, `vm.exec.cs`) — loads modules, manages fibers, the bytecode interpreter loop (opcode dispatch in `ExecState`)
- **`VM.Fiber`** (`vm.fiber.cs`) — cooperative execution unit, implements `ITask`, object-pooled
- **`Val`** (`val.cs`) — universal value struct (`double num`, `object obj`, plus `_num2/_num3/_num4` for efficient struct encoding); objects stored inside `Val` may implement `IRefcounted`
- **Coroutine support** — `coro.cs` (yield/suspend/wait primitives), `paral.cs` (`paral`/`paral_all`), `defer.cs`
- **Symbol/Type system** — `symbol/`, `scope/`, `type/` subdirectories
- **Stdlib** — `std/prelude.cs` (fiber ops, yield, start), `std/std.cs`
- **Binary format** — `FMT_BIN` or `FMT_BIN_GZ` (LZ4), MessagePack-based serialization in `marshall/`

### Native bindings

User C# code implements `IUserBindings` (exposes native functions/classes to BHL scripts) and loads it as a DLL at runtime via `DllBindings`. Path is configured in `bhl.proj`.

### Task runner (`src/taskman/`)

`Taskman` discovers `[Task]`-annotated static methods via reflection. Core tasks: `compile`, `run` (compiles + ticks VM at 60fps), `lsp`, `bench`, `clean`, `version`.

### LSP server (`src/lsp/`)

Built on OmniSharp. Handlers in `handlers/` cover document sync, semantic tokens, go-to-definition, find references, and hover. VS Code extension in `src/lsp/vsclient/` launches `bhl lsp` and connects via stdio.

## Testing

Tests live in `tests/`. Each test class extends `BHL_TestBase` (defined in `test_shared.cs`), which provides `Compile()`, `MakeVM()`, `AssertEqual()`, `CommonChecks()`, and helpers for registering native bindings. Tests use xUnit `[Fact]` and FluentAssertions. Tests run sequentially (configured in `xunit.runner.json`).

Tests typically verify both bytecode output (opcode-level assertions via `AssertEqual`) and runtime behavior.

## Key Conventions

- `VM`, `ANTLR_Processor`, and `Tasks` are split across multiple files using `partial class` / `partial static class`
- Heavy use of object pooling (fibers, `Val`, coroutines) to minimize GC pressure — avoid unnecessary allocations in hot paths
- `Val` is a plain struct; ref-counting applies to `IRefcounted` objects stored inside it — always pair `Retain()`/`Release()` calls on those correctly

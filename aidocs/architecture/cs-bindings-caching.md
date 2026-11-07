# Caching the `.cs` bindings parser

`CsSourceBindings` reads C# bindings registration sources *without compiling
them* (`src/front/cs_bindings.cs`): it lexes/parses them into a
`CsBindingsProgram`, then interprets the `Register` entry against a `Types`
through `CsInterpreter`. Both halves used to be paid in full on every build,
which raised the recurring question: can the result be cached?

## What is cached today

**The parse, per process.** `CsSourceBindings.Prog` memoizes
`CsBindingsProgram.Read`. A build asks for the declared versions and then
registers; the sources can't change in between, so this halves the cost. An
LSP reload gets the same benefit (it also calls both).

**The declared `(name, version)` pairs, across processes.** An incremental
build with nothing to recompile needs *only* the declared versions — it never
registers — so parsing the sources just for them used to dominate such a build.
They now live in `<tmp_dir>/cs_bindings.declared`, keyed on a CRC32 of the
source contents plus `Defines`. A 20k-symbol binding took a no-op build from
520 ms to 135 ms.

The file is written temp-then-rename: a build and the LSP share it (both get
`tmp_dir` from the same `bhl.proj`, `use_cache` defaults to `true`) and may run
at once. A plain write lets a reader see a truncated *yet well formed* file —
i.e. silently fewer declared versions, surfacing as a bogus "does not declare a
version" error.

A malformed or missing cache falls back to parsing, so the cache can never make
a build fail; it is skipped entirely when a name/version contains a separator.

## What is deliberately *not* cached: `Register()`

Caching the registration output means persisting the symbols it defines. Every
native symbol type is explicitly listed in `NOT_PERSISTED`
(`tests/test_marshall.cs`) — `FuncSymbolNative`, `ClassSymbolNative`,
`EnumSymbolNative`, `FieldSymbol`, `InterfaceSymbolNative` — with the rationale
*"never written into a compiled module, native ones come from bindings"*. A
`.bhc` references natives by name and re-resolves them against the live
bindings at load time, and `TestClassIdCreatesTheSameClass` enforces it.

It is *feasible*: the symbols are declaration-only, since the interpreter never
has real native code — a delegate argument becomes `null` or a static
placeholder (`FieldSymbol.PendingGetter`, `ClassSymbolNative.PendingCreator`,
see `CsInterpreter.Placeholder`). But it would require giving all of those a
`CLASS_ID` + `Sync`, removing them from `NOT_PERSISTED`, and re-resolving the
native `System.Type` bindings by reflection on load.

**It was measured and rejected.** Per build that recompiles at least one module:

| native symbols | parse | run |
|---|---|---|
| 500 | 11 ms | ~15 ms |
| 2 000 | 31 ms | 33 ms |
| 5 000 | 66 ms | 117 ms |

Symbol caching removes both columns — tens of milliseconds at a realistic
binding size, in exchange for inverting a core marshalling invariant. Note the
cost scales with the number of *registered symbols*, not source lines; a
synthetic 20k-symbol binding (1.4 s) badly overstates a real one.

## What was optimized instead

The interpreter enumerated reflection per invocation: constructors and methods
per call, and `FindImplicit` per argument per candidate overload. Memoizing the
three (`_ctors`, `_methods`, `_implicits`) cut `Run` by ~30% at every size with
no cache and no invariant touched.

The remaining superlinearity in `Run` is GC pressure from allocating the
symbols themselves — which a cache would not avoid either. `SymbolsStorage.Find`
is dictionary-backed, so `Namespace.Define` is *not* the quadratic culprit (cf.
[`CheckUniqueSymbols`](symbol-type-consistency.md)).

## Related gotcha

`ProjectConf.NormalizePath` only resolves a path against the `bhl.proj`
directory when it starts with `.` — a bare `"bindings.cs"` is returned
unchanged and so interpreted relative to the cwd. That reads fine, but setting
`dll` on such an entry writes a generated `.csproj` into `tmp_dir` carrying the
relative path verbatim, and MSBuild resolves it against the *csproj* directory:
`error CS2001: source file not found`. All the examples use `"./bindings.cs"`.

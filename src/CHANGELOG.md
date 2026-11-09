# Changelog

All notable changes to the BHL Unity package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0-beta22]

### Changed
- Bindings discovery no longer reflects over every loaded assembly. `BindingsRegistry` walked
  `AppDomain.CurrentDomain.GetAssemblies()` and called `GetTypes()` on each, which materializes
  every type in the assembly - under IL2CPP that metadata is initialized from
  `global-metadata.dat` and never released, since there is no class or assembly unloading, so
  the walk turned a lazy design into "all of it, resident for the process lifetime". It also ran
  unconditionally: `Types()`'s constructor asks the registry for the prelude, so merely
  constructing a `Types` walked everything.

  Implementing `IUserBindings` requires a *direct* reference to the assembly declaring it, so
  the assembly reference table - which costs no type loading to read - rules out everything
  else. An assembly whose references can't be inspected is scanned anyway, so a runtime without
  that support is no worse off than before, and no binding can be silently missed. On a trivial
  CLI compile, 37 assemblies and 10470 types in 61ms became 2 assemblies and 689 types in 5ms.
- `BindingsRegistry.GetForAssembly` reflects the assembly it's given rather than walking the
  whole domain and filtering afterwards. Its only caller, `DllBindings`, passes the assembly it
  just loaded. The assembly is now also honoured if it never landed in
  `AppDomain.CurrentDomain`, where before it yielded nothing and surfaced as
  "IUserBindings instance not found".

### Documentation
- `docs/lsp.md` and `docs/dap.md` corrected against the implementations: the DAP request table
  was missing all of stepping (`next`, `stepIn`, `stepOut`) plus `evaluate`; the `example/dap/`
  paths it referenced were moved to another repo; the `BHL_DEBUGGER` define it described doesn't
  exist and the adapter is compiled into the VM assemblies unconditionally. `docs/lsp.md`
  described missing/unused import handling as happening on save, one of them "as a workspace
  command" - they are the "Add missing imports" and "Organize imports" code actions, and the
  only registered command is `bhl.reload`.

## [3.0.0-beta21]

Documentation only - the shipped binaries are identical to beta20.

### Documentation
- `using` is documented in `docs/namespaces.md`: both forms, how they compose with full
  qualification, that they are scoped to the declaring file and not re-exported, and the
  errors with their actual messages.
- Fixed docs that didn't match the compiler, found by compiling all 110 `bhl` code blocks in
  `docs/`: `trace()` is a test-only binding and six docs called it (now `std.io.WriteLine`);
  `docs/operator-overloading.md` documented a BHL syntax for declaring operators that doesn't
  parse, they're definable only on a C# bound class; `docs/oop.md` used `new X { f = v }`
  instead of `X x = {f: v}` and documented map literals, which don't exist - maps are created
  empty and filled by assignment; plus a braceless `if`, an unparenthesized call as an `if`
  condition, and `func coro void ptr` instead of `coro func() ptr`.
- `docs/imports.md` no longer claims a duplicate import is silently ignored - it's an error,
  only a self-import is ignored.

## [3.0.0-beta20]

### Added
- `using <namespace>` brings a namespace's members into the module unqualified, and
  `using <alias> = <namespace>` binds a short name to it instead. Only a consumer *outside* a
  namespace ever had to spell the full path - inside one, unqualified access to its own
  members, relative access downward and enclosing scope access upward all already worked. The
  workaround was to reopen `namespace foo { }` in the consuming file, which works but silently
  puts anything declared there into that namespace.

  Both forms are private to the module declaring them, so neither is re-exported to modules
  importing it - a module is a file in bhl, so this is the same scoping as C#'s file level
  `using`. A `using` colliding with a local symbol is reported as `already defined symbol`.
  The alias form cannot collide with the namespace's contents at all, which makes it the safer
  choice for a large namespace.

  **`using` is a new keyword**, so a script using it as an identifier no longer parses.

## [3.0.0-beta19]

### Fixed
- A trailing wildcard in `src_dirs` matches directories. `Glob` resolves its final segment with
  `GetFiles`, which is right for a bindings `sources` pattern like `autobind/*.cs` but not for a
  source dir one: `libs/*` returned the files under `libs`, which `Setup()` then dropped as
  non-directories. The result was an empty build, and a silent one - a wildcard matching nothing
  is deliberately quiet, since an absent unversioned package dir is legitimate. A wildcard in a
  middle segment (`pkgs/*/Runtime`) always worked, as did a plain dir, which recurses anyway.

## [3.0.0-beta18]

Follow-ups to the library `name` introduced in beta17 - worth taking if you adopted it.

### Fixed
- A library declaring a `name` may no longer also declare `inc_dirs`. They were redundant (its
  `src_dirs` are already on the include path under its name) and actively harmful: the entries
  carried its name onto whatever they covered, including the including project's own files, and
  their mere presence made that project's `inc_dirs` non-empty - suppressing the `src_dirs`
  fallback that put its own sources on the include path. A project including such a library
  could no longer import its own modules. A library without a `name` may still declare
  `inc_dirs`, which is how it made its files addressable before.
- `import "/mylib/util"` reaches a named library. A leading separator has always been tolerated
  and meaningless - the combined path normalizes the same - but the new prefix match saw it and
  refused, so the two spellings disagreed.

## [3.0.0-beta17]

### Added
- A `bhl.proj` pulled in through `includes` may declare a `name`, and every module it
  contributes is then named `<name>/<path under its dirs>` whatever the including project does
  with its `inc_dirs`. Module identity used to come from stripping the first matching include
  path prefix, so it was decided by the consumer rather than the library: two libraries each
  rooted at their own `inc_dir` collided on any shared filename, and a library's own absolute
  imports were only valid under one particular consumer configuration. A BHL `namespace` never
  helped, since symbol namespacing and module identity are independent mechanisms. A named
  library is reachable only through its name, its declared name wins over a prefix-less
  `inc_dir` covering the same files, and its dirs are always on the include path so a consumer
  can't make it unreachable. Two libraries claiming one name is an error naming both projects.
  Declaring no name keeps the previous behaviour, so this is opt-in - but note that adopting a
  name changes that library's module names, which key `.bhc` entries, the build cache and any
  runtime `LoadModule(...)` call.

### Fixed
- A module is named by an include dir that actually contains it. The prefix match was a plain
  string test, so an inc dir `<root>/src` also covered `<root>/srcx/b.bhl` and named that
  module `x/b` - the same name as a real `<root>/src/x/b.bhl`. Two unrelated files in unrelated
  directories then failed the build as an ambiguous module, with no way out short of renaming a
  directory.
- Each source file is collected once. `src_dirs` may overlap, directly or after `includes`
  folded in another project's dirs, and the same file twice was reported as a module ambiguous
  with itself - naming the very same path on both sides - and threw on a duplicate key while
  the LSP indexed it.
- A `src_dir` that doesn't exist is reported. It used to be dropped silently, so a typo quietly
  built a project missing whatever that directory held; the guard meant to catch it ran after
  that filtering and could never fire. It's a warning rather than an error, since listing a
  generated dir that isn't there yet is a legitimate setup. A wildcard matching nothing stays
  quiet, as before.
- A file under no include dir at all says what to do about it, instead of reporting that it
  "was not normalized according to include path".

## [3.0.0-beta16]

### Added
- A `src_dirs` entry starting with `!` excludes a subtree instead of adding one, and the source
  walk no longer descends into it. Sources gathering enumerated every file under every `src_dir`
  and then kept the `.bhl` ones, so assets interleaved with sources were rescanned on every
  build. On a project with 31530 level files sitting beside 291 modules, a no-op build went
  0.68s -> 0.16s and a one file change 1.26s -> 0.72s. An exclusion matching nothing is a
  warning, same as an unresolved wildcard include.

### Changed
- An incremental build only materializes the cached modules it needs - the stale files plus
  their transitive imports - instead of deserializing every cached module's symbols. Touching
  one file in a 9000 module project took 1.73s instead of 0.60s, three quarters of it spent on
  modules that hadn't changed. Modules outside that set keep their compiled bytes, which still
  go into the result and the cache blob. The build cache gained each module's project wide
  declarations so the cross-module duplicate check still covers a module that was never
  materialized, and its format version was bumped accordingly: **the first build after
  upgrading does one full rebuild**, then behaves as usual.

### Fixed
- A `bhl.proj` warning is reported once rather than twice.

## [3.0.0-beta15]

### Added
- `bhl run` resolves imports through a project: it takes `--proj`, and without one looks for the
  nearest `bhl.proj` walking up from the script. A script with an import could not run at all
  before - the import resolved to nothing and every symbol from it was unresolved. The project's
  bindings are loaded too, so a script using native symbols runs, not just one with imports.

### Fixed
- A chain member is resolved in the scope the chain narrowed to, never the enclosing ones.
  `f.helper()` found an unrelated global `helper` and then died downstream with an internal
  `Not instance type: helper` reported at `0:0`; `Foo.helper()` found it too and was blamed on a
  static/instance mismatch when the class simply has no such member.
- A name left over at the end of a lambda call chain is resolved:
  `func Foo() { return new Foo }().x` reported "incompatible types: 'int' and 'Foo'" instead of
  compiling, and gave an unknown member the same message.
- A method taken as a value on a lambda tail is rejected instead of crashing the VM:
  `func int() x = func Foo() { return new Foo }().m` compiled and then threw "Unable to cast
  object of type 'bhl.FuncSignature' to type 'bhl.ClassSymbol'". The same thing through a named
  function has always been rejected as "method pointers not supported".
- Editing a `.cs` binding invalidates the modules compiled against it. A sources-only entry has
  no dll to watch, so an edit left every module cached against symbols that no longer existed and
  the build reported success.
- A `bindings` entry combining a relative source path with `dll` builds: the generated `.csproj`
  is written into `tmp_dir` and carried that path verbatim, so MSBuild resolved it against the
  csproj's own directory (`error CS2001: source file not found`).

### Changed
- **Breaking for `IFrontPostProcessor`:** `AST_Call` and `AST_Block` are type hierarchies now and
  the `EnumCall`/`BlockType` enums are gone. `is AST_Call` and `tree.children` still work, but a
  post-processor switching on either enum has to move to type tests.
- Checking for duplicate globals no longer relinks every module: it merged each one into a single
  accumulating namespace that gained a link per module, making the check quadratic in module
  count on every build. 2.14s -> 0.01s at 12k modules.
- `.cs` bindings are parsed once per build instead of twice, and an incremental build with nothing
  to recompile no longer parses them at all - the declared versions it needs are cached in
  `tmp_dir`. A no-op build with a 20k-symbol binding went 520ms -> 135ms.
- A func parameter's default value is flagged by the frontend instead of inferred from its node
  having children. That held only because the default expression is the one thing ever attached
  to a param, so an `IFrontPostProcessor` attaching a child for any other reason would have
  silently changed the `DefArg` bytecode. The counts are unchanged.

## [3.0.0-beta14]

### Fixed
- The debugger stops on a `break` and on a `continue` again: their jump was emitted without a
  source line, so nothing mapped to those lines - stepping skipped straight over them and a
  breakpoint set on one could not resolve an address at all. Needs a recompile to take effect.
- A breakpoint can be set on a `defer`, `paral` and `paral_all` line: the opcode each of them
  emits carried no source line either, so only the statements inside the block had an address.

## [3.0.0-beta13]

### Fixed
- Resolving a type no longer races with the other compile threads: a `Types` hands the same
  interned `ProxyType` to all of them, and resolving one nulled its resolver reference while
  another thread was about to use it, so a compile could fail with a `NullReferenceException`
  that never reproduced. The reference is kept now and read once.

## [3.0.0-beta12]

### Changed
- Two native list types with the same item type but different names are different types now: they
  are backed by different native containers and conversions, so one is not assignable to the other
  any more. They used to compare equal while hashing apart, so a set of types missed them as well.
- An inheritance cycle longer than one step is a compile error, "inheritance cycle is not allowed":
  only direct self-inheritance was caught before, so `class A : B` with `class B : A` recursed until
  the stack overflowed and took the process down with it, while the interface equivalent was accepted
  silently. Both are rejected the same way now, for a chain of any length.

### Fixed
- `var a = null` is rejected whatever the expression looks like: the check compared the source text
  against "=null", so `var a = (null)` went through and the variable silently got the `null` type.
  It is checked against the type of the expression now, and the error points at the expression
  rather than at `var`.
- The check for a symbol declared in two modules no longer throws a `NullReferenceException` when the
  conflicting symbol is a namespace the check merged for itself, which belongs to no single module:
  it reports the conflict as intended, naming such a module `?`.

## [3.0.0-beta11]

### Changed
- The symbol factory no longer reads the class id of a native list type as a generic array: a native
  list type is written by its name and never by a class id, a module with it is an error now instead
  of a different type.

## [3.0.0-beta10]

### Added
- `NativeListTypeSymbol<T>.CopyTo(src, ref list)` copies a BHL array into a native list kept by the
  caller, a class (`List<T>`, `IList<T>`) or a struct (`HList<T>`): updated in place, created if
  null, null for a null array; the items read from the array are released after their conversion.

## [3.0.0-beta9]

### Changed
- `ValMap.GetEnumerator()` returns a struct enumerator, `foreach` over a `ValMap` in C# doesn't allocate.
- The message of a `VM.Error` starts with the message of the error itself, followed by the BHL stack
  trace; it used to be the stack trace alone.
- An item written into an array (`Add`, `Insert`, `a[i] = ..`) and an entry put into a map are
  checked against the item, key and value types at runtime, by `Types.IsStorableAs`: a typed
  container written through a variable of a wider type (`[]any`, `[any]any`) used to accept
  anything, so `[]int` could end up holding a string. Scalars are checked as scalars only, values
  without a type or of `any` (made by native code) are not checked.
- A cast to `int`, `float`, `bool`, an enum or `string` checks the value is a scalar: `(int)a` of an
  `any` holding a string or an object used to give `0` (`(float)a` left the string as is), now it's
  an "Invalid type cast" error. Values without a type or of `any` (made by native code) are not
  checked.
- `(bool)x` is a `bool` (`x != 0`), it used to keep the number and its type, so `(bool)5 == true` was
  false; `(float)x` is tagged `float`.

### Fixed
- A value overwritten during a `foreach` over its map (`m[k] = ..`) no longer throws "Collection was
  modified" on Unity's Mono (.NET Core never did): `ValMap` replaces values in its entries, not in the
  dictionary.
- A BHL `foreach` over a map no longer boxes each key and value (about 144 bytes per entry).
- `ValMap.DefaultComparer` never finds a scalar key equal to a non scalar one: in an `[any]T` map
  `0` was equal to `"x"` but not the other way round.
- `ValMap.Remove` releases the key stored in the map, not the given one (they differ for keys equal
  by a native comparer).

## [3.0.0-beta8]

### Added
- Hand-written C# bindings are read as is: anonymous delegates and lambdas passed as native
  code and `typeof(...)` as native types are accepted as arguments; constructors and fields with
  several variables are skipped.
- `NativeListTypeSymbol.DefineOnce<T>(ns, name, val2native, native2val, item_type)` defines a native
  list type unless it's already defined and returns it; `DefineOnce(ns, name, item_type)` is the one
  for C# bindings sources read by the compiler.
- `NativeListTypeSymbol<T>.SetList(ref val, list)` sets a native list of the type, or the BHL array
  itself when the list is a `ValList<T>` adapter of one; `ValList<T>.New(..)` takes the array's type
  for that, `Source`/`SourceType` expose them.

### Changed
- The examples use their C# bindings (`bindings.cs`) as the bindings sources instead of `.bhl`
  bindings declaring the same symbols.
- `BHL_REBUILD` cleans the Release build of the `bhl` tool, the one it runs, instead of Debug,
  so the tool is rebuilt from scratch.

### Fixed
- `ValList<T>.IndexOf`/`Contains` compare by `EqualityComparer<T>.Default`: no boxing of a struct
  item, a null item no longer throws.
- `ValList<T>.CopyTo(array, index)` copies to `array` from `index`, as `ICollection<T>` defines it,
  instead of copying the items from `index` to the start of `array`.

## [3.0.0-beta7]

### Added
- C# bindings sources read without compiling them (`CsSourceBindings`): a `bindings` entry
  listing `.cs` sources without a `dll` builds its symbols from the sources as if compiled with
  `BHL_FRONT`; the LSP watches and reloads them. The sources must be in a strict generated
  shape, see `docs/csharp-bindings.md`.
- The entry of C# bindings sources is `Register` of the class implementing `IUserBindings` (e.g.
  the project's runtime registration combining several generated modules, read as is), else the
  only `Register` of the sources.
- `Types.SetupType(name)`: sets up a native class or interface declared by bindings (used by
  `std.bind` and by generated C# bindings).

### Changed
- **Breaking:** `.cs` bindings sources without an explicit `dll` are read instead of being
  built into an auto-derived dll under `tmp_dir`. Set `dll` to keep building them.

## [3.0.0] - 2026-07-17

Initial release published to OpenUPM.

### Added
- BHL runtime for embedding in Unity — executes compiled `.bhc` bytecode.
- Optional in-editor compiler front-end, enabled via the `BHL_PARSER` / `BHL_FRONT`
  scripting defines when the ANTLR plugin is present.

### Packaging
- Tracked `.meta` files for stable GUIDs across installs.
- Declared `com.unity.nuget.newtonsoft-json` as a dependency.
- Minimum supported Unity version: 2021.3.

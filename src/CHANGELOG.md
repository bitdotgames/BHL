# Changelog

All notable changes to the BHL Unity package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

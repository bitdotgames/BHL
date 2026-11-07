# Module identity and `inc_dirs`

How a `.bhl` file gets its module name, why every library must live under a
uniquely named directory, and what that costs.

## How a module gets its name

A module's name is its file path with the first matching include path prefix
stripped (`IncludePath._FilePath2ModuleName`, `src/vm/util/include_path.cs`).
So `<inc_dir>/a/util.bhl` becomes module `a/util`.

`inc_dirs` is an ordered, flat search path, `-I` style. An absolute
`import "a/util"` walks it in order and takes the first `<inc_dir>/a/util.bhl`
that exists (`TryIncludePaths`); a relative `import "./util"` resolves against
the importing file's own directory instead (`ResolveImportPath`).

When `inc_dirs` is empty it defaults to `src_dirs` (`ProjectConf.Setup`). When
it is *not* empty, a `src_dir` outside all of them is a hard error — every
compiled file must be namable.

Two separate collision checks exist, and they catch different things:

- `CompilationExecutor.CheckModuleNamesCollision` — two files resolving to the
  same **module name**.
- `Namespace.CheckUniqueSymbols` — the same **symbol path** declared in two
  modules (see [symbol-type-consistency.md](symbol-type-consistency.md)).

## Why a library needs its own uniquely named directory

Because module identity is purely positional, two libraries each rooted at
their own `inc_dir` collide on any shared filename:

```
libs/A/util.bhl   (namespace alib)   inc_dirs: ["./A", "./B"]
libs/B/util.bhl   (namespace blib)
```

→ `module 'util' ambiguous resolving: .../A/util.bhl and .../B/util.bhl`

**A BHL `namespace` does not help.** Symbol namespacing and module identity are
independent mechanisms; the two files above are in different namespaces and
still collide. The discipline cannot be expressed in the language, only in the
directory layout — hence "ship your files under a directory named after the
library", with the consumer's `inc_dir` pointing at its *parent*.

This convention is what every search-path based system uses: C (`include/libname/`),
Python (packages on `sys.path`), Java (reverse-DNS packages). It is not unusual.

## Where positional naming is weaker than those

This is what a library gets when it declares no name (see the next section for
the fix). The convention is unwritten and unenforceable, and three consequences
follow.

**1. Module identity is decided by the consumer, not the library.** The same
file is `a/util` or `A/a/util` depending on where the consumer points
`inc_dirs`. In Go, Rust and npm a package declares its own identity.

**2. A library's internal absolute imports are not self-contained.** Verified:
a library whose `A/a/main.bhl` does `import "a/util"` compiles under
`inc_dirs: ["./A"]` and fails with 3 errors under `inc_dirs: ["./"]` — identical
source, the consumer's config decides.

**3. Relative imports escape #2 but not #1.** The same library using
`import "./util"` builds under both configurations. Its module *names* still
differ between them (the resulting bundle differs in size), but its internal
wiring survives any consumer layout.

`includes` sharpens all of this: it folds every included project's `inc_dirs`
into one ordered flat list where first-match-wins decides names. Two libraries
are one careless layout away from colliding, and `module 'util' ambiguous
resolving` does not hint that the fix is to restructure directories.

## A library declares its own name

A `bhl.proj` pulled in through `includes` may declare `"name"`. Every module it
contributes is then named `<name>/<path under its dirs>`, whatever the including
project did with its own `inc_dirs`:

```json
// pkg/bhl.proj
{ "name": "mylib", "src_dirs": ["./"] }
```

`pkg/util.bhl` becomes `mylib/util`, imported as `import "mylib/util"`.

Rules, all enforced:

- **A named library is reachable only through its name.** `import "util"` does
  not find `mylib/util` even if some `inc_dir` happens to cover that tree
  (`IncludePath.TryIncludePaths`).
- **A declared name beats a prefix-less dir covering the same files.** If the
  consumer points an `inc_dir` straight into the library, the module is still
  `mylib/util`, not `util` (`_FilePath2ModuleName` prefers a prefixed entry).
- **A named library's dirs are always on the include path**, whether or not the
  including project declares `inc_dirs` of its own — otherwise the consumer
  could make the library unreachable just by having its own include path.
- **Two libraries claiming one name is an error** naming both `bhl.proj` files.
- **No name means today's behaviour exactly**, so this is opt-in.

Verified: the same project compiles to a byte-identical bundle under three
different consumer `inc_dirs` configurations, and two libraries may both ship a
`util.bhl` — which positional naming could not express at all.

Implementation: `ProjectConf.name` + `dir2prefix`/`prefix2proj` recorded by
`ExpandIncludes`, carried onto `inc_path` in `Setup()` (including through
wildcard expansion of `src_dirs`); `IncludePath` entries became `(dir, prefix)`.

**Migration:** adopting a name changes the module names of that library, which
key `.bhc` entries, the build cache and any runtime `LoadModule("...")` call.
Expect one full rebuild and update of explicit module-name references.

## Still positional without a name

The weaknesses above are only fixed for libraries that declare one. An
unnamed library keeps positional identity, so the directory convention and the
advice still apply to it:

- use **relative imports internally**, so its wiring is position independent;
- point the consumer's `inc_dir` at the library's **parent**, so the library
  name is part of every module name.

Decoupling module identity from file paths entirely (Rust/C# style, where a
module declares its own name in source) remains the principled end state and
remains unimplemented — module names key `.bhc` entries, imports, the LSP index
and the build cache.

## Gotcha: prefix matching is by path boundary

The prefix match requires a path boundary, so an `inc_dir` of `<root>/src` does
*not* cover `<root>/srcx/b.bhl`. Before that fix the latter was named `x/b`,
colliding with a real `<root>/src/x/b.bhl`, and the project could not be built
without renaming a directory. An `inc_dir` written with a trailing separator
(`"../"` and the like, common in a real `bhl.proj`) already carries the
boundary and is handled.

## Related

- [cs-bindings-caching.md](cs-bindings-caching.md) — `NormalizePath` resolves a
  `bhl.proj` path against the project dir only when it starts with `.`
- `docs/` — human-facing language reference for `import` syntax

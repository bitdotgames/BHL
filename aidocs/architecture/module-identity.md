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

## Where BHL is weaker than those

The convention is unwritten and unenforceable, and three consequences follow.

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

## Assessment

Keep the resolution model. Decoupling module identity from file paths
(Rust/C# style, where a module declares its own name) is the principled fix but
a very large one: module names key `.bhc` entries, imports, the LSP index and
the build cache.

The cheap middle option is to make the discipline explicit rather than
discovered, and `includes` already provides the unit to hang it on: an included
`bhl.proj` *is* a library. It contributes its own `src_dirs`/`inc_dirs`
(`ExpandIncludes`), is identified by its canonical file path, is folded in only
once for a diamond graph, and already has fields it is forbidden to declare
(`defines`, the legacy `bindings_*` ones, postproc) — so "an included project
may declare X" is established precedent.

There is already a precedent for this exact shape elsewhere in `bhl.proj`:
a `bindings` entry **must** declare a `name`, and `LoadBindings()` rejects an
entry whose declared `(name, version)` doesn't match what the binding actually
announces (`ProjectConf.DiscoverDeclaredBindings`). Native bindings therefore
already declare their identity and have it verified; libraries do not. Applying
the same rule to an included project is consistency, not a new concept.

Sketch, **not designed or implemented**:

- a library ships its own `bhl.proj` - which is already how it is consumed -
  and that file declares the library's `name` (the root project's is empty,
  i.e. today's behaviour);
- `ExpandIncludes` records it alongside each `src_dir`/`inc_dir` it folds in,
  so `IncludePath` holds `(dir, prefix)` rather than a bare dir;
- a module is then named `<prefix>/<path relative to that library's dir>`,
  which makes identity a property of the library instead of the consumer's
  `inc_dirs`, fixing weaknesses 1 and 2 above;
- an absolute `import "libA/util"` strips the declared prefix and searches only
  that library's dirs, instead of scanning one flat ordered list.

Two libraries declaring the same prefix is then simply an error at `Setup()`
with both project paths named — no ordering question arises, because order
stops being what decides a name. Without a declared prefix everything behaves
exactly as today, so it can land opt-in.

The work lands in `IncludePath` (per-item prefixes) and `ExpandIncludes`
(recording them) — contained, but it touches the naming of every module, so it
wants a migration story for projects already relying on positional names.

Advice that applies today, with no code change:

- libraries should use **relative imports internally**, so their wiring is
  position independent;
- a consumer's `inc_dir` should point at the library's **parent**, so the
  library name is part of every module name.

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

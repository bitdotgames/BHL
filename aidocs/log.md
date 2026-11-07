# aidocs log

Append-only. Entry format: `## [YYYY-MM-DD] <type> | <title>`. Recent entries: `grep "^## \[" aidocs/log.md | tail -5`.

## [2026-09-03] init | LLM Wiki bootstrap

Initialized the LLM Wiki methodology for this project: `aidocs/AGENTS.md`
(schema), `aidocs/index.md` (empty catalog), this log, and an always-on
"Documentation" section in the root `CLAUDE.md`. Categories seeded for this
project's domain: architecture, lsp, decisions, gotchas, testing, tooling.
No pages ingested yet — `docs/` (human language reference) was left as-is,
not duplicated into the wiki.

## [2026-11-04] query | Symbol/type system consistency audit

Audited `src/vm/symbol/`, `src/vm/type/`, `src/vm/scope/` (~6.3k LOC) for
internal inconsistencies, against `8d3c0abf`. New page:
`architecture/symbol-type-consistency.md`.

Six open defects, headline ones: indirect class-inheritance cycle recurses to
StackOverflow and kills the process (uncatchable — takes the LSP server down),
while the interface sibling silently *accepts* the same cycle; an NRE in the
two-day-old `Namespace.CheckUniqueSymbols` (`?.module.name` guards the cast,
not `module`); `Equals`/`GetHashCode` inverted on array types. Plus latent
items: `ProxyType`'s hash mutates on comparison (not live — nothing keys on it),
and `CLASS_ID` 7/24 are absent from `SymbolFactory` but unreachable, with 23
never used at all.

Also recorded the verified-sound list (Sync↔IndexTypeRefs pairing, no
Equals-without-GetHashCode, Setup idempotence) so future sessions don't
re-derive it.

## [2026-11-04] ingest | Fixed audit findings #1-#3

`CheckUniqueSymbols` NRE fixed (`faf873d8`), then the inheritance cycles:
`InheritsFrom` walks added to `antlr_proc.pass.cs` for both classes (linear
`super_class` chain) and interfaces (`inherits` graph, with a `seen` set), so
an indirect cycle is now a normal `inheritance cycle is not allowed` compile
error instead of a process-killing StackOverflow / a silent accept.

Repro note worth keeping: a class cycle only reproduces when both classes live
in the **same module** — split across two modules with no `import`, neither
name resolves and you get "not a class or an interface" instead. An earlier
cross-module repro attempt wrongly suggested the bug wasn't real.

Page updated: findings #1-#3 moved to a "Fixed" section; #4-#6 and the latent
items remain open.

## [2026-11-04] ingest | Fixed audit findings #4-#6

Finding #4 was reframed twice before it was right, worth remembering: it was
first written as "array Equals/GetHashCode are inverted", then as "composite
names aren't canonical", and both were wrong. A probe showed names *are*
normalized (the frontend resolves the proxy before building the composite), so
generic arrays/maps/tuples/refs/signatures are self-consistent. The real defect
was `NativeListTypeSymbol` alone, whose name is caller-supplied: measured a
`HashSet` missing an object its own `Equals` called equal. Fixed by including
the name in `Equals` - a behaviour change, `Types.Is(List_a, List_b)` is false
now.

#5 `HasDefaultArgAt` -> `i >= GetRequiredArgsNum()`, no behaviour change at its
one guarded call site. #6 dropped the `GetText() == "=null"` text comparison
for a check on the evaluated type, so `(null)` and deeper nestings are caught;
the caret moved from `var` to the expression.

Lesson recorded for future audits: a finding's *framing* deserves as much
verification as its existence. Two of the six here were real but described
wrongly, and acting on the first description would have made the code worse.

## [2026-11-04] query | Default-arguments limit

Asked whether the default-args cap could be lifted. New page:
`architecture/default-args-limit.md`. Decided no, for now.

Two things worth not re-deriving: the cap is **26**, not 28
(`32 - ARGS_NUM_BITS`), and it is not a constant to raise — `cargs_bits` is a
declared 4-byte operand on ~12 call opcodes, so any change bumps
`COMPILE_FMT` and invalidates every `.bhc`, and `FuncArgsInfo` is public API
reaching every native binding.

Costed three options. Widening the operand pipeline is out: 677 explicit
`new int[]{…}` literals in tests don't convert to `long[]`. Of the rest, the
smaller diff (two 4-byte operands) was rejected in favour of wide call-opcode
variants, which keep the hot path and all 42 existing call-opcode assertions
untouched — smallest permanent cost beats smallest diff.

## [2026-11-07] ingest | Caching the .cs bindings parser

Asked whether `CsSourceBindings` results can be cached so unchanged bindings
aren't re-parsed every compile. Landed two caches: the parse memoized per
process (it ran twice per build — once for the declared versions, once to
register), and the declared `(name, version)` pairs persisted in `tmp_dir`
keyed on source content. A no-op build with a 20k-symbol binding went 520ms →
135ms. The declared cache is written temp-then-rename because a build and the
LSP share the file and a torn read would look well formed with fewer entries.

Caching `Register()` itself was costed and rejected. Every native symbol type
is in `NOT_PERSISTED` by design — `.bhc` resolves natives by name against live
bindings at load — so caching them inverts that invariant. Measured prize at a
realistic binding size is tens of ms per recompiling build. Memoized the
interpreter's per-invocation reflection instead: ~30% off `Run`, no invariant
touched.

Also found while benchmarking: `NormalizePath` leaves a bare relative path
alone, which silently broke the generated `.csproj` when `dll` is set - fixed
by emitting absolute paths from `MakeLibraryCSProj` rather than changing the
long-standing `./`-prefix convention.

## [2026-11-07] ingest | Module identity and inc_dirs

Asked for a take on the `inc_dirs` design and the observation that every
library must sit under a uniquely named directory. Verified the constraint is
real: two libraries each rooted at their own `inc_dir`, each in its own BHL
`namespace`, still fail with `module 'util' ambiguous resolving` - symbol
namespacing and module identity are independent mechanisms.

The convention itself is standard (C, Python, Java all require it). What's
weaker is that it's unwritten and unenforceable: module identity is decided by
the consumer's `inc_dirs` rather than declared by the library, so the same
library source compiles under `inc_dirs: ["./A"]` and fails with 3 errors under
`inc_dirs: ["./"]`. Relative imports make a library's internal wiring position
independent but don't stabilise its module names.

Recommendation recorded: keep the resolution model, make the discipline
explicit via a `module_prefix` declared by an included bhl.proj - `includes`
already treats an included project as a unit with its own dirs and its own
forbidden fields. Sketch only, not implemented.

## [2026-11-07] ingest | A library bhl.proj declares its name

Implemented the sketch from the previous entry. An included bhl.proj may
declare `name`, and every module it contributes is named `<name>/<rest>`
regardless of the including project's inc_dirs. A named library is reachable
only through its name, its declared name beats a prefix-less inc_dir covering
the same files, and its dirs are always on the include path. Two libraries
claiming one name is an error naming both projects. No name means today's
behaviour, so it's opt-in.

Verified a project compiles to a byte-identical bundle under three different
consumer inc_dirs configurations, and that two libraries may both ship a
util.bhl - which positional naming couldn't express at all. The drm project
(no names declared) builds byte-identically to before.

`IncludePath` entries became `(dir, prefix)`; `ExpandIncludes` records which
library contributed each dir; `Setup()` carries that onto inc_path, including
through wildcard expansion of src_dirs.


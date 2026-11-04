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

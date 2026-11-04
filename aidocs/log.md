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

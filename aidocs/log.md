# aidocs log

Append-only. Entry format: `## [YYYY-MM-DD] <type> | <title>`. Recent entries: `grep "^## \[" aidocs/log.md | tail -5`.

## [2026-09-03] init | LLM Wiki bootstrap

Initialized the LLM Wiki methodology for this project: `aidocs/AGENTS.md`
(schema), `aidocs/index.md` (empty catalog), this log, and an always-on
"Documentation" section in the root `CLAUDE.md`. Categories seeded for this
project's domain: architecture, lsp, decisions, gotchas, testing, tooling.
No pages ingested yet — `docs/` (human language reference) was left as-is,
not duplicated into the wiki.

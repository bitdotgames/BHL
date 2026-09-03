# aidocs/ — LLM Wiki schema

This folder is a persistent, agent-maintained wiki for the `bhl` project (BHL
language compiler + VM + LSP). It is distinct from `docs/`, which is the
human-facing language reference (getting started, language features, stdlib).
`aidocs/` captures things a human doc wouldn't: architecture notes, decisions,
gotchas, and reusable answers to non-trivial questions about the codebase — so
the agent doesn't re-derive them from scratch every session.

## Layers

1. **Source of truth** — the code (`src/`, `lsp/`, `tests/`, `grammar/`) and
   the existing human docs (`docs/`). Immutable from the wiki's point of view;
   the agent only reads them. On conflict between a wiki page and the code,
   the code wins — the page gets corrected or flagged stale.
2. **Wiki** — the markdown files in `aidocs/`. The agent owns these fully.
3. **Schema** — this file, describing wiki structure and operations.

## Service files (do not multiply these)

- **`aidocs/AGENTS.md`** — this file: schema, categories, conventions, operations.
- **`aidocs/index.md`** — catalog of all wiki pages by category, link + one-line description. Entry point for answering questions.
- **`aidocs/log.md`** — append-only journal. Entry format: `## [YYYY-MM-DD] <type> | <title>`. Recent entries: `grep "^## \[" aidocs/log.md | tail -5`.

## Categories

Pages live in `aidocs/<category>/<page>.md`. Current categories (extend as needed, keep `index.md` in sync):

- **`architecture/`** — compilation pipeline, VM/runtime internals, symbol/type system, binary format, object pooling design.
- **`lsp/`** — LSP server internals, OmniSharp integration, handler-specific notes.
- **`decisions/`** — design decisions and tradeoffs with rationale (e.g. hot-reload approach, generic-vs-special-cased choices).
- **`gotchas/`** — non-obvious pitfalls (e.g. `Val` ref-counting rules, opcode dispatch quirks, caching invalidation edge cases).
- **`testing/`** — test conventions, `BHL_TestBase` patterns, how to debug a failing test.
- **`tooling/`** — `bhl.proj`, `taskman` tasks, build/publish/bench workflow notes.

A page is a normal markdown file with a short title, cross-links to related
pages (`[[relative/path.md]]`-style or plain relative links), and links out to
the actual source files/lines it documents, so it stays anchored to ground truth.

## Operations

- **Ingest** — a source/doc was added or materially changed → read it, create/update the relevant page(s), update `aidocs/index.md`, fix cross-links in related pages, append an entry to `aidocs/log.md`.
- **Query** — a non-trivial question about the project → read `aidocs/index.md` first, then the relevant pages, synthesize an answer with links. If the answer is reusable (not one-off), write it up as a new/updated page (ingest it) so the knowledge compounds.
- **Lint** — on request: look for contradictions, stale content, orphan pages (not linked from anywhere or not in `index.md`), missing cross-links. Summarize findings in `aidocs/log.md`.

## Constraints

- Only generate wiki content on explicit user request, or as a natural byproduct of ingest/query operations described above — don't proactively write pages for unrelated work.
- Don't duplicate `docs/` (human language reference) — link to it instead of restating it.
- Don't duplicate content across wiki pages — link to the existing page instead.
- On conflict between a page and the code, the code wins; correct or flag the page.
- Extend `index.md` / `log.md` / this file rather than creating parallel service files.

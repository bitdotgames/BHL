# aidocs index

Catalog of all wiki pages, by category. See `aidocs/AGENTS.md` for the schema
and conventions. Empty categories below are placeholders — filled in as pages
get ingested.

## architecture/

- [Why a function is capped at 26 default arguments](architecture/default-args-limit.md) — FuncArgsInfo's 32 bits, why lifting it is a bytecode format change, three options costed, and why it was left alone.
- [Caching the `.cs` bindings parser](architecture/cs-bindings-caching.md) — what's cached (the parse, the declared versions) and why `Register()` isn't: native symbols are NOT_PERSISTED by design, costed against the measured win.
- [Symbol/type system — known inconsistencies](architecture/symbol-type-consistency.md) — audit of symbol/type/scope: inheritance-cycle crash, a ProxyType race across compile threads, native-list equality, plus what's verified sound.

## lsp/

_(none yet)_

## decisions/

_(none yet)_

## gotchas/

_(none yet)_

## testing/

_(none yet)_

## tooling/

_(none yet)_

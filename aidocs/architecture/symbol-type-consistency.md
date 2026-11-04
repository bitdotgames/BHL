# Symbol/type system — known inconsistencies

Audit of `src/vm/symbol/`, `src/vm/type/`, `src/vm/scope/` (~6.3k LOC) for
internal inconsistencies. Findings are anchored to file:line; on any conflict
the code wins — re-verify before acting.

Audited 2026-11-04 against `8d3c0abf`.

## Fixed

### 1. Indirect class-inheritance cycle → StackOverflow (process death) — FIXED

`ClassSymbol.GetAllRelatedTypesSet` (`src/vm/symbol/class.cs:370-374`) assigns
the `related_types` memo **after** `CollectAllRelatedTypesSet()` returns, so a
cycle re-enters with the field still `null` and recurses unbounded.
`DoSetupMembers` (`:272-276`) walks `super_class` with no cycle guard either.

The frontend rejected only *direct* self-inheritance, never an indirect cycle,
so `class A : B {} class B : A {}` reached this code. `StackOverflowException`
is uncatchable in .NET → the whole process died: the `bhl lsp` server exited
outright, and the CLI could not report it as a normal compile error.

**Both classes must be in the same module** to reproduce — across two modules
with no `import`, neither name resolves and you get "not a class or an
interface" instead.

Fixed by `InheritsFrom` walks in `src/front/antlr_proc.pass.cs`, which reject
the cycle as a normal `inheritance cycle is not allowed` compile error.
Whichever symbol of the cycle is processed last sees the already established
links and trips the check. The underlying `DoSetupMembers` /
`CollectAllRelatedTypesSet` recursion is still unguarded — it's simply no
longer reachable from source. A hand-corrupted `.bhc` could still hit it.

### 2. Interface sibling silently *accepted* the same cycle — FIXED

`InterfaceSymbol.GetAllRelatedTypesSet` (`src/vm/symbol/interface.cs:85-100`)
publishes `related_types` into the field **before** recursing, so a cycle
re-enters, sees non-null, and returns a partially-built set.
`interface IA : IB {} interface IB : IA {}` compiled with no error at all
(verified: produced a valid 208-byte `.bhc`).

Same method shape as #1, same input shape, opposite outcome — class crashed,
interface accepted. Fixed together with #1; both now report `inheritance cycle
is not allowed`. The interface walk carries an explicit `seen` set because
`inherits` is a graph (diamonds are legal).

Note the memoization asymmetry itself was left as-is — with cycles rejected
upfront it is no longer load-bearing, but it remains a latent trap for anyone
constructing these symbols programmatically.

### 3. NullReferenceException in `Namespace.CheckUniqueSymbols` — FIXED

`src/vm/symbol/namespace.cs:206-207`:

```csharp
var other_module = (conflict.other.scope as Namespace)?.module.name;
var local_module = (conflict.local.scope as Namespace)?.module.name;
```

The `?.` guards the *cast*, not `module` being null — `.module.name` is an
unguarded chain. `TryLink` synthesizes namespaces whose `module` is null
(`:152`, merge root at `:201`), and `IsModuleLocal()` returns false for a
`Namespace` (`src/vm/scope/extension.cs:195-203`), so the guard at `:205`
doesn't stop it. Throws instead of producing the intended conflict diagnostic.

Widened by `8d3c0abf`, which added a second caller in `lsp/workspace.cs:319`
alongside the CLI path. Fixed via a `ModuleName` helper using
`?.module?.name ?? "?"` — the fallback is honest, a synthesized namespace
belongs to no single module.

## Open defects

### 4. `Equals`/`GetHashCode` are inverted on array types

`src/vm/type/array.cs:312-324` (`GenericArrayTypeSymbol`) and `:431-447`
(`NativeListTypeSymbol`):

- `Equals(o)` → `item_type.Equals(o.item_type)` — compares **only item type**
- `GetHashCode()` → `name.GetHashCode()` — hashes **only name**

Exactly inverted, so equal objects can hash differently. Reachable: these land
in `HashSet<IInstantiable>` via `CollectAllRelatedTypesSet` (`:369-377`), which
`Types.Is()` consumes through `IsSupersetOf`
(`src/vm/type/type_system.rules.cs:71-74`).

### 5. `HasDefaultArgAt` is a tautology

`src/vm/symbol/func.cs:356-361`:

```csharp
return i >= (GetDefaultArgsNum() - GetDefaultArgsNum());   // always i >= 0
```

`GetRequiredArgsNum()` directly above is `GetTotalArgsNum() -
GetDefaultArgsNum()` — evidently what was meant. Latent: the only caller
(`src/front/antlr_proc.cs:716`) is already range-guarded, so correct and buggy
expressions coincide there. Any new caller inherits a silently-true predicate.

### 6. `var x = (null)` bypasses the null-assignment guard

`src/front/antlr_proc.cs:2847` matches **source text**:

```csharp
else if(is_auto_var && assign_exp?.GetText() == "=null")
```

`var x = (null)` yields text `"=(null)"`, misses the match, and compiles with
0 errors (local type becomes `Types.Null`). Module-level globals and fields are
rejected by a separate, correct path. Harmless for marshalling today — local
var types aren't serialized — but the guard plainly intends to reject this.

## Latent / cosmetic

### `ProxyType` has a mutating hash code

`src/vm/type/proxy.cs:170-176` hashes `path` when unresolved and `resolved`
once resolved; `Equals()` calls `Get()` at `:162`, which resolves and rewrites
`path` (`SetResolved`, `:93-106`). So *comparing* a proxy changes its hash, and
`Symbol` never overrides `GetHashCode` so the resolved hash is reference
identity. **Not currently live** — `ProxyType` is never used as a
dictionary/set key anywhere (only as a `Func<ProxyType,T,Val>` parameter). A
landmine for whoever first keys on it. The `//TODO: some smelly code below` at
`:96` already flags the underlying normalization hack.

### `CLASS_ID` registry gaps are signaling-only

IDs run 1–24 with **23 never used** (confirmed via `git log -G` — a skipped
number, not a retired ID; contiguity is not required, only uniqueness and
stability).

Two IDs are absent from `SymbolFactory.CreateById`
(`src/vm/symbol/factory.cs`):

- `NullSymbol` = 7 (`src/vm/symbol/builtins.cs:189`) — overrides `ClassId()`,
  i.e. advertises serializability the factory can't honor. Unreachable: never
  `Define`d into a namespace, and it's a `ClassSymbolScript` (not
  `IEphemeralType`), so it would be written by path. If it ever did serialize,
  the failure is **not** `BAD_CLASS_ID` but silent null — path `"null"` resolves
  in no namespace.
- `NativeListTypeSymbol` = 24 (`src/vm/type/array.cs:331`) — the class is
  `abstract`, so the factory could never construct it. Effectively dead code.

Why they can't currently reach the stream: `ProxyType.Sync` (`proxy.cs:119-133`)
writes types **by name/path** unless `IEphemeralType`, and all 5 ephemeral types
(`TupleType`, `GenericArrayTypeSymbol`, `FuncSignature`, `GenericMapTypeSymbol`,
`RefType`) *are* registered. The other `SyncGeneric` path,
`SymbolsStorage.Sync` (`storage.cs:120`), only ever sees a module's own `ns`
(`module_declared.cs:202`); the global `types.ns` is `Link`ed at load.

### `VarSymbol` pairs a throwing `ClassId()` with a silent `Sync()`

`src/vm/symbol/builtins.cs:211-230`. Every other never-serialized symbol
(`ClassSymbolNative`, `FuncSymbolNative`, `InterfaceSymbolNative`,
`FuncSymbolVirtual`, `EnumSymbol`) throws in **both**. `VarSymbol` is also the
only throwing-`ClassId` symbol actually `Define`d into a namespace
(`src/vm/type/type_system.builtins.cs:46`) — safe only because that namespace
is never written.

## Verified sound (don't re-investigate)

- **Sync ↔ IndexTypeRefs pairing** is exact for every Script variant — no
  type-bearing field is synced-but-unindexed. `TypeRefIndex.GetIndex` *throws*
  on an unindexed type (`src/vm/type/index.cs:73-79`), so a regression here
  fails loudly at write time rather than emitting a corrupt `.bhc`.
- **No read/write field-count asymmetry** in any `Sync()`.
- **No `Equals`-without-`GetHashCode`** (or vice versa) anywhere in
  `type/`/`symbol/`/`scope/` — all pairs are complete. No `symbol/` class
  overrides equality at all (consistent reference semantics).
- **`Setup()` is effectively idempotent** — `SetupAllMembers`/`SetupVirtTables`
  guard on `_all_members`/`_vtable`; `ResolveTmpSuperClassAndInterfaces`
  self-disarms.
- **Generic-vs-native dispatch is consistent** — the frontend uniformly tests
  the base `ArrayTypeSymbol`/`MapTypeSymbol`; concrete subtypes appear only
  inside `Equals`.

## Related

- `src/vm/symbol/factory.cs` — the `CLASS_ID` → instance registry.
- `src/vm/marshall/` — `BAD_CLASS_ID` and the `SyncGeneric` machinery.

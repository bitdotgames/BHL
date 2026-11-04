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

### 4. `NativeListTypeSymbol` broke the `Equals`/`GetHashCode` contract — FIXED

**Corrected framing.** This was first written up as "array's `Equals`/
`GetHashCode` are inverted", which is misleading. All five composite types
follow the *same uniform convention*:

| type | `Equals` compares | `GetHashCode` |
|---|---|---|
| `GenericArrayTypeSymbol` (`array.cs:312-324`) | `item_type` | `name` |
| `NativeListTypeSymbol` (`array.cs:431-447`) | `item_type` | `name` |
| `GenericMapTypeSymbol` (`map.cs:368-380`) | `key_type`+`val_type` | `name` |
| `FuncSignature` (`func.cs:127-168`) | return+args | `name` |
| `TupleType` (`tuple.cs:85-101`) | `items` | `name` |
| `RefType` (`ref.cs:59-70`) | `subj` | `name` |

The design intends `name` to *be* the structural key — it's derived from the
components (`"[]" + item_type`, `"ref " + subj`). So changing one type's
`GetHashCode` in isolation would introduce a new inconsistency, not remove one.

**The "name isn't canonical" theory was tested and is wrong.** Composite names
*are* normalized in practice: the frontend resolves a type proxy before
building the composite, and `ProxyType.SetResolved` rewrites `path` to the full
path, so `[]Item` written inside namespace `fns` comes out named `[]fns.Item`,
matching a `[]fns.Item` written elsewhere. Measured: identical names, identical
hashes, `Types.Is` true, source compiles clean.

For `GenericArrayTypeSymbol` the convention is therefore *self-consistent*:
`name` is derived from `item_type`, so `Equals`-equal ⟹ same name ⟹ same hash.
Same for map/tuple/ref/func-signature.

**The real defect is `NativeListTypeSymbol` alone**, because its `name` is
**caller-supplied** (`DefineOnce(ns, "List_a", …)`, `array.cs:341`) and
unrelated to `item_type`, while `Equals` (`:431-447`) still compares *only*
`item_type`. So two native lists over the same item type but with different
names — and genuinely different native backings, i.e. different
`Val2Native`/`Native2Val`/creator — are `Equals`-equal yet hash apart.

Measured with two lists over `int`:

```
a.name=List_a hash=1670586340
b.name=List_b hash=-1599090102
a.Equals(b)    : True
hashes agree   : False      <-- contract broken
set.Add(a); set.Contains(b) : False   <-- HashSet genuinely misses an equal object
Types.Is(a,b)  : True
```

So it is both a `Equals`/`GetHashCode` contract violation *and* a conflation of
operationally distinct types. It stays invisible today only because the one
in-tree hash consumer, `Types.Is` → `IsSupersetOf`
(`type_system.rules.cs:74`), is shortcut by the `type.Equals(dest_type)` test
at `:57`. No `Dictionary<IType,…>`/`HashSet<IType>` exists anywhere else.

Fixed by including identity in `Equals` — `name == o.name &&
item_type.Equals(o.item_type)` — so equal ⟹ same name ⟹ same hash. The
alternative (hashing `item_type` instead of `name`) would have restored the
contract while leaving two differently-backed native lists mutually
substitutable; the conflation was judged the more dangerous half.

**This is a behaviour change:** `Types.Is(List_a, List_b)` was true and is now
false, so assigning between two differently-named native lists over the same
item type no longer compiles. Nothing in-tree relied on it — the pre-existing
`TestNativeListEquality` compares two lists *both* named `List_int` and still
passes untouched — but external Unity-side bindings might.

`GenericArrayTypeSymbol` was deliberately left alone: its `name` is derived
from `item_type`, so it is already self-consistent.

### 5. `HasDefaultArgAt` was a tautology — FIXED

`src/vm/symbol/func.cs:356-361`:

```csharp
return i >= (GetDefaultArgsNum() - GetDefaultArgsNum());   // always i >= 0
```

`GetRequiredArgsNum()` directly above is `GetTotalArgsNum() -
GetDefaultArgsNum()` — evidently what was meant. Latent: the only caller
(`src/front/antlr_proc.cs:716`) is already range-guarded, so correct and buggy
expressions coincide there. Any new caller inherits a silently-true predicate.

Fixed to `return i >= GetRequiredArgsNum();`. No behaviour change at the single
existing call site, which is why nothing in the suite moved.

### 6. `var x = (null)` bypassed the null-assignment guard — FIXED

`src/front/antlr_proc.cs:2847` matches **source text**:

```csharp
else if(is_auto_var && assign_exp?.GetText() == "=null")
```

`var x = (null)` yields text `"=(null)"`, misses the match, and compiles with
0 errors (local type becomes `Types.Null`). Module-level globals and fields are
rejected by a separate, correct path. Harmless for marshalling today — local
var types aren't serialized — but the guard plainly intends to reject this.

Fixed by dropping the text comparison and checking the *evaluated* type where
the auto type is resolved (`ProcAssignToVar`), next to the existing `null`-type
and `void` checks: `auto_type == Types.Null` → "unexpected expression".
Verified against the CLI: `null`, `(null)` and `(((null)))` are all rejected,
`var a = 1` still infers. The message is unchanged but the caret now points at
the expression instead of at `var`, so `TestNullIsNotAllowed`'s `PlaceAssert`
moved.

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

### `CLASS_ID` registry gaps — RESOLVED, by design and now test-enforced

**Corrected.** This section originally framed the factory omissions as a
consistency defect. They are not: `tests/test_marshall.cs` now carries a
`NOT_PERSISTED` set naming every symbol/type deliberately never written into a
compiled module — `NullSymbol` and `VarSymbol` among them, with the note
*"'null' is written by its name, 'var' is resolved before"* — and
`TestClassIdCreatesTheSameClass` reflects over every other
`IMarshallableGeneric`, asserting it has a default ctor and that the factory
builds back its own class from its `ClassId()`. So the invariant is stated and
enforced; absence from the factory is the intended signal for a
written-by-name type, not an oversight.

Original notes kept below for the reasoning they record.

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
  **History:** the factory did briefly carry a `case
  NativeListTypeSymbol.CLASS_ID: return new GenericArrayTypeSymbol();` — worse
  than a plain omission, since reading that id silently produced the *wrong
  type*. Removed in `49344ad7`, which also added a test in
  `tests/test_marshall.cs` asserting each class id builds its own class and
  that no native list id is in the factory. A native list type is written by
  its name, never by a class id.

Why they can't currently reach the stream: `ProxyType.Sync` (`proxy.cs:119-133`)
writes types **by name/path** unless `IEphemeralType`, and all 5 ephemeral types
(`TupleType`, `GenericArrayTypeSymbol`, `FuncSignature`, `GenericMapTypeSymbol`,
`RefType`) *are* registered. The other `SyncGeneric` path,
`SymbolsStorage.Sync` (`storage.cs:120`), only ever sees a module's own `ns`
(`module_declared.cs:202`); the global `types.ns` is `Link`ed at load.

### `VarSymbol` paired a throwing `ClassId()` with a silent `Sync()` — FIXED

`src/vm/symbol/builtins.cs:211-230`. Every other never-serialized symbol
(`ClassSymbolNative`, `FuncSymbolNative`, `InterfaceSymbolNative`,
`FuncSymbolVirtual`, `EnumSymbol`) throws in **both**. `VarSymbol` is also the
only throwing-`ClassId` symbol actually `Define`d into a namespace
(`src/vm/type/type_system.builtins.cs:46`) — safe only because that namespace
is never written.

Fixed: `Sync()` throws `NotImplementedException` like its siblings, so a
misuse is loud instead of silently writing nothing. `NullSymbol` was left
alone — it is in `NOT_PERSISTED` with its own rationale, and its `ClassId()`
is simply never consulted.

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

# Why a function is capped at 26 default arguments

Answers "can we lift the default-arguments limit?" — investigated 2026-11-04,
decided **not** to lift it for now. On any conflict the code wins; re-verify
before acting.

## The limit is 26, not 28

`src/vm/symbol/func.cs:48-58`:

```csharp
public const int ARGS_NUM_BITS   = 6;
public const int MAX_ARGS        = 63;        // (1 << 6) - 1
public const int MAX_DEFAULT_ARGS = 32 - ARGS_NUM_BITS;   // = 26
public uint bits;
```

All 32 bits of `FuncArgsInfo.bits` are spent: 6 for the count of explicitly
passed args, 26 for the bitmask of which default args the caller omitted.
Nothing to reclaim. Exceeding it is the "max default arguments reached" error
at `src/front/antlr_proc.cs:721`.

The mask has to stay a mask: with named arguments the defaulted ones are not
trailing — `foo(a:1, b:2, d:3)` against `func foo(int a, int b, int c = 10, int
d = 20)` defaults `c` in the middle — so it cannot collapse to a count.

## It is a bytecode format change, not a constant

`cargs_bits` is a declared **4-byte operand** on ~12 call opcodes (the
`DeclareOpcode` table at `src/front/compiler.cs:645+`) and is decoded by
`Bytecode.Decode32` at ~10 sites in the VM dispatch loop (`src/vm/vm.exec.cs`).

Every option therefore needs: `bits` → `ulong`, 64-bit popcount constants
(the hand-rolled `0x55555555…` block at `func.cs:118-133`), and a
`ModuleLoader.COMPILE_FMT` bump 2→3 (checked at `src/vm/loader.cs:76`) — which
**rejects every existing `.bhc`**. `FuncArgsInfo` is also public API, handed to
every native binding callback, so external C# bindings recompile too.

## Three ways to 64 bits

| | Approach | Churn | Permanent cost |
|---|---|---|---|
| **A1** | Widen the operand pipeline (`Definition` width 8, `Instruction.operands` `int[]`→`long[]`, add `Write64`/`Decode64`) | ~800 sites — **677 explicit `new int[]{…}` literals in tests** don't convert to `long[]`, plus 98 emit sites | none beyond the format |
| **A2** | Split into two existing 4-byte operands (`4 /*lo*/, 4 /*hi*/`) | ~12 emit sites, ~10 decode sites, **42 call-opcode test assertions** | **+4 bytes on every call** and a second 4-byte read per call in the hottest loop |
| **A3** | Wide variants of the call opcodes, emitted only past the threshold | ~12 new opcodes + decode arms; **no existing test assertion changes** | none — narrow path stays byte-identical |

`Bytecode` currently stops at `Decode32`/`Write32` and `Decode(…)` returns
`uint`, so A1's 64-bit helpers do not exist yet.

## Decision

**A3 if it is ever done.** A2 has the smaller diff but taxes every call in a VM
that is optimised hard elsewhere (pooling, no hot-path allocations, a `make
bench` suite); A3 confines the cost to the feature that needs it. Smallest
permanent cost beats smallest diff here.

**Not done for now**, because the ceiling is only reached at 27+ default
arguments, where the calling code is usually the thing that wants fixing —
passing a class or struct costs nothing and reads better. Weigh that against
making every user recompile.

Note `//TODO: add support for arbitrary amount of default arguments` at
`func.cs:47` reads as if it were a small job. It is not.

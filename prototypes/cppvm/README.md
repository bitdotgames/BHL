# C++ BHL VM prototype

Throwaway prototypes written to answer one question: **would a native C++ runtime for BHL
(for Unity/IL2CPP, Godot, Unreal) be meaningfully faster than the C# VM?**

Nothing here is a real VM. Each variant implements just enough opcodes to run `fib(x)`, and
the bytecode and constants are the real ones the C# compiler emits (dumped from
`ModuleCompiler`), so they execute exactly what the C# VM executes. `VAL_PAD=1` gives the
value the same shape `Val` has (type, num, obj, refc plus three spare doubles - 56 bytes
here), and `IRefcounted::Release` is virtual so the indirect call is modelled rather than
optimized away.

```sh
make bench    # builds and runs every variant
make langs    # same workload in lua/luajit/python/php, skips what isn't installed
```

## Variants

| file | what it changes |
|---|---|
| `vm.cpp` | baseline, `switch` dispatch |
| `vm_table.cpp` | function-pointer table - what the C# VM actually does |
| `vm_goto.cpp` | computed goto (`&&label`) |
| `vm_pre.cpp` | bytecode pre-decoded into a struct array, no operand decoding in the loop |
| `vm_fast.cpp` | frame state hoisted into locals so it stays in registers |
| `vm_si.cpp` | superinstructions, fused runs padded with `Nop` |
| `vm_si2.cpp` | same, `Nop` padding squeezed out and jumps renumbered; fused runs matched by shape, with the operator kept in `Instr::sub` and switched on |
| `vm_si3.cpp` | same matching, but emitting one fused opcode per operator so the loop never branches on it |
| `native.cpp` | plain recursive C++ `fib`, the floor |

## Numbers

fib(15), 50000 iterations, best of 5 (best of 3 for the interpreters), Apple arm64, `-O2`.
C# figures are BenchmarkDotNet on the same machine. Run-to-run spread is around 10%, so
treat anything closer than that as a tie.

| | µs/op |
|---|---|
| native C# | 1.07 |
| native C++ | 1.65 |
| LuaJIT (JIT on) | 2.17 |
| | |
| `vm_si3` (superinstr, one opcode per operator) | **14.15** |
| LuaJIT interpreter (`-joff`) | 14.59 |
| `vm_si2` (superinstr, operator switched on) | 14.96 |
| `vm_si` (superinstr, `Nop` padded) | 21.00 |
| `vm_fast` | 26.64 |
| `vm_pre` | 27.41 |
| `vm` (switch) | 29.30 |
| PHP 8 | 33.01 |
| `vm_goto` (computed goto) | 33.16 |
| Python 3 | 54.27 |
| `vm_table` (fn-pointer table) | 78.00 |
| | |
| C# BHL VM + superinstructions | 84.5 |
| NLua | 100 |
| C# BHL VM | 118.8 |

## What it actually showed

**The headline is a dispatch artifact.** `vm.cpp` at 29.5 against the C# VM's 118.8 looks
like a 4x win for C++, but they don't dispatch the same way. Comparing like for like -
`vm_table`, a function-pointer table, which is what the C# VM uses - gives 80.6 vs 118.8,
so **C++ buys about 1.5x**, not 4x. That is nowhere near enough to justify a second runtime.

**The real wins were portable.** Going 29.5 -> 15.5 came from pre-decoding, superinstructions
and squeezing out the `Nop` padding. None of that is C++-specific. Superinstructions were
subsequently implemented in the C# VM (`superinstructions` branch) and bought 29% on this
same benchmark, with no new runtime to maintain.

**Dispatch strategy does not transfer between the languages.** In C++ the `switch` (29.5)
beats the function-pointer table (80.6) by a wide margin; in C# the opposite holds, and by
more - enabling `BHL_USE_OPCODE_SWITCH` made fib(15) 1295 µs against 122 µs for the
`delegate*` table, 10x worse. Computed goto (34.2) did not beat the switch here at all;
clang compiles this switch well on arm64.

**The interpreter ceiling is about LuaJIT's.** `vm_si2` at 15.5 against LuaJIT's interpreter
at 14.6 is roughly where a well-tuned bytecode interpreter lands. Everything below that -
native C++ at 2.35, LuaJIT's JIT at 2.16 - needs compiled code, not a better interpreter.

**Generality is free in C#, not in C++.** The fused runs started out hardcoded for fib's
operators (`Sub`, `EqualScalar`). Matching by shape instead and keeping the operator in the
instruction costs nothing in the C# VM, where each handler sits behind a `delegate*` and the
indirect call swamps an extra predictable branch. Here it cost about 6% (`vm_si2` 14.96
against 13.99 for the hardcoded version measured the same way), because dispatch over
pre-decoded instructions is already cheap enough that one more branch in the hot case shows
up. Emitting one fused opcode per operator instead (`vm_si3`, 14.15) gets most of it back -
a wider dispatch switch is free, an inner switch is not. So: generalize in the matcher,
specialize in the opcode.

Conclusion: a C++ runtime isn't worth it for speed alone. If one ever happens it should be
for reach (engines without a CLR), not performance.

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
| `vm_si2.cpp` | same, with the `Nop` padding squeezed out and jumps renumbered |
| `native.cpp` | plain recursive C++ `fib`, the floor |

## Numbers

fib(15), 20000 iterations, Apple arm64, `-O2`. C# figures are BenchmarkDotNet on the same
machine.

| | µs/op |
|---|---|
| native C++ | 2.35 |
| LuaJIT (JIT on) | 2.16 |
| native C# | 1.07 |
| | |
| `vm_si2` (superinstr + compaction) | **15.47** |
| LuaJIT interpreter (`-joff`) | 14.55 |
| `vm_si` (superinstr, Nop padded) | 21.96 |
| `vm_fast` | 26.80 |
| `vm_pre` | 27.97 |
| `vm` (switch) | 29.48 |
| PHP 8 | 33.38 |
| `vm_goto` (computed goto) | 34.23 |
| Python 3 | 55.67 |
| `vm_table` (fn-pointer table) | 80.61 |
| | |
| C# BHL VM + superinstructions | 84.5 |
| NLua | 100 |
| C# BHL VM | 118.8 |
| MoonSharp | 350 |

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

Conclusion: a C++ runtime isn't worth it for speed alone. If one ever happens it should be
for reach (engines without a CLR), not performance.

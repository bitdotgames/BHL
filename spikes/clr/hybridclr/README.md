# Measuring HybridCLR

`FibBench.cs` times the two shapes a CLR backend emits, so its output can be put straight next
to the JIT numbers from `bench/bench_fib.cs`.

Measured on this machine, .NET 8 JIT, fib(15):

| | us/op |
|---|---|
| plain C# | 1.19 |
| coro-style (IEnumerator + return channel) | 27.5 |
| BHL VM, for reference | 131.8 |

## What to run

1. A Unity project with HybridCLR installed and a hot-update assembly configured.
2. Put `FibBench.cs` in that hot-update assembly.
3. From the player, call `FibBench.Run()` and log the result.
4. Build **IL2CPP** and run on the target device - Editor numbers are Mono JIT and don't
   answer the question.

## What the answer means

IL2CPP has no JIT, so a CLR backend's output can only be interpreted by HybridCLR on the
shipping platform.

- If the interpreted numbers stay well under the VM's 131.8 us, the direction keeps its speed
  argument.
- If they land near or above it, the backend costs a Unity-specific dependency and buys only
  interop - deleting the bindings layer - which is a different, smaller case.

Worth noting what fib measures: it is call-dense with no suspension, so it is the worst case
for the coro lowering (an iterator allocated per call, nothing gained for it) and the best
case for a method-call-heavy interpreter. Real gameplay coroutines do more work per call.

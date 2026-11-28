# CLR backend spike

Not a compiler. One BHL script, hand written as the C# a CLR backend would have to emit, to
find out whether BHL's concurrency model survives translation to a language with no VM.

The script is `TestParalWithForInABranchAndDefer` from `tests/test_paral.cs`:

```bhl
coro func test()
{
  paral {
    {
      yield()
      yield()
      yield()
    }
    {
      for(int i = 0; i < 3; i++) {
        defer {
          trace((string)i+"~;")
        }
        trace((string)i+";")
        yield()
      }
    }
  }
}
```

It must produce `0;1~;1;2~;2;2~;`.

## What the mapping turns out to be

| BHL | C# |
|---|---|
| a `coro func` / paral branch | `IEnumerator<Tick>` |
| `yield()` | `yield return` |
| `defer { }` | `try { } finally { }` |
| cancelling a branch | `IEnumerator.Dispose()`, which runs pending `finally` blocks |
| `paral` | step each branch once per tick, stop when one finishes, dispose the rest |

`yield return` inside `try/finally` is legal C# (only `try/catch` forbids it), and that's what
makes `defer` work on both paths: normal scope exit *and* cancellation run the same block.

## The one place a naive translation is wrong

`defer` sees the loop variable **already incremented** on normal exit (`1~;` after the `i=0`
iteration) but **not** on cancellation (`2~;` after the `i=2` iteration, not `3~;`).

A plain `for(int i = 0; i < 3; i++) { try { ... } finally { ... } }` gives `0~;` instead,
because C# runs the finally *before* the increment. Matching BHL means lowering the loop so
the increment sits inside the try:

```csharp
int i = 0;
while(i < 3)
{
  try { body; yield return Tick; i++; }
  finally { deferred; }
}
```

So `for` with a `defer` in its body can't lower to a C# `for`. That's the kind of thing this
spike exists to find.

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

## Probe 2: paral_all with an early return

```bhl
coro func int test() {
  paral_all {
    { return 1 }
    yield suspend()
  }
  return 0
}
```

Must give 1, with the other branch suspended forever.

An `IEnumerator` can't return a value to its caller, so `return` inside a branch isn't a C#
`return` - the branch writes into an out parameter the driver carries, and the driver stops
as soon as it's set:

```csharp
if(branches[i].MoveNext()) ++running; else alive[i] = false;
if(ret.has) yield break;
```

Checked after *each* branch step, not once per tick, or a later branch in the same tick would
run after the function had logically returned. `paral_all` otherwise waits for all of them,
which is the only difference from `paral`.

So every BHL function that can return from inside a paral needs a return channel threaded
through its branches - not free, but mechanical.

## Probe 3: defer and return

```bhl
func float foo() { defer { trace("~FOO") } return 3  trace("FOO") }
func test()      { bar(foo()) }
```

`~FOOBAR~BAR` - the defer runs before the value reaches the caller.

This one needs nothing: C# evaluates the return value, runs the `finally`, then returns, which
is the same order. A `return` inside `try/finally` translates as is.

## Probe 4: a native coroutine, and cancelling through a sub coroutine

A native that suspends isn't a C# `yield` at all - it's a `Coroutine` whose `Tick(ExecState)`
sets `status = RUNNING` to be called again. That maps to an `IEnumerator<Tick>` the generated
code drives, since C# has no `yield foreach`:

```csharp
while(co.MoveNext())
  yield return default(Tick);
```

The sharp part is cancellation. From `tests/test_defer.cs`:

```bhl
coro func doer() {
  float k = 1
  defer { if(k == 1) { foo(k) } }
  yield suspend()
  k = 2
}
coro func test() { paral { { yield doer() } { yield() yield() } } }
```

Must trace `1` - the paral ends, `doer` is cancelled while suspended, its defer runs and still
sees `k == 1`.

Driving the sub coroutine as above **loses that**. Disposing the caller runs the caller's own
finally blocks, but nothing disposes the callee, and an abandoned iterator never runs its
finally - there's no finalizer for it. The defer is silently skipped and the trace is empty.

So `yield <coro>` has to emit the guard:

```csharp
try { while(co.MoveNext()) yield return default(Tick); }
finally { co.Dispose(); }
```

Both versions are in the spike: one asserts `1`, the other asserts the empty string that the
naive lowering produces.

## Where that leaves it

Four probes, four different answers:

| construct | cost |
|---|---|
| `defer`, cancellation | free - `try/finally` and `Dispose()` |
| `defer` with `return` | free |
| `for` carrying a `defer` | changed lowering - `while`, increment inside the try |
| `paral_all` with `return` | a return channel the compiler threads through every branch |
| `yield <coro>` | a disposal guard, or cancellation stops propagating |

None failed. The model translates. What it costs is a lowering rule per construct, and each
one is discoverable only by testing against the VM, because these semantics live in the VM's
behaviour rather than in a spec. Two of the four go silently wrong when lowered naively -
which is the actual risk here, not feasibility.

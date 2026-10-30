# C# Bindings in BHL

BHL provides a system for exposing C# classes, functions, and fields to BHL scripts via `IUserBindings`.

## Entry point

Implement `IUserBindings` and register it with the `Types` instance:

```csharp
public class MyBindings : IUserBindings
{
  public void Register(Types types)
  {
    // define symbols here
  }
}

// attach to the VM
var types = new Types();
new MyBindings().Register(types);
var vm = new VM(types);
```

## Binding free functions

Use `FuncSymbolNative`. The callback receives `VM.ExecState exec` — read arguments from `exec.stack` in reverse order (last argument is on top), and push the return value back:

```csharp
var fn = new FuncSymbolNative(new Origin(), "min", Types.Float,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        float b = (float)exec.stack.PopFast().num;
        float a = (float)exec.stack.PopFast().num;
        exec.stack.Push(Math.Min(a, b));
        return null; // return a Coroutine to make the function a coroutine
    },
    new FuncArgSymbol("a", Types.Float),
    new FuncArgSymbol("b", Types.Float)
);
types.ns.Define(fn);
```

```bhl
float result = min(10, 5) // returns 5
```

## Binding classes

```csharp
var cl = new ClassSymbolNative(new Origin(), "Color", null,
    // constructor — called when BHL code does: new Color
    delegate(VM.ExecState exec, ref Val v, IType type)
    {
        v.SetObj(new Color(), type);
    }
);
types.ns.Define(cl);
cl.Setup();
```

### Fields

```csharp
cl.Define(new FieldSymbol(new Origin(), "r", Types.Float,
    // getter
    delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld)
    {
        v.SetFlt(((Color)ctx.obj).r);
    },
    // setter
    delegate(VM.ExecState exec, ref Val ctx, Val nv, FieldSymbol fld)
    {
        ((Color)ctx.obj).r = (float)nv.num;
    }
));
```

### Instance methods

Instance methods are `FuncSymbolNative` members of the class. Access `self` via `exec.GetSelfRef()`:

```csharp
cl.Define(new FuncSymbolNative(new Origin(), "Lerp", Types.Void,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        ref var self = ref exec.GetSelfRef(); // must be called first
        var color    = (Color)self.obj;
        float t      = (float)exec.stack.PopFast().num;
        exec.stack.Pop(); // pop self
        color.r *= t;
        color.g *= t;
        return null;
    },
    new FuncArgSymbol("t", Types.Float)
));
```

### Static methods

Static methods do not access `self`:

```csharp
cl.Define(new FuncSymbolNative(new Origin(), "White", FuncAttrib.Static, Types.Float, 0,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        exec.stack.Push(Val.NewObj(exec.vm, new Color(1, 1, 1), cl));
        return null;
    }
));
```

## Coroutine functions

Return a `Coroutine` object from the callback to make a function a coroutine:

```csharp
var fn = new FuncSymbolNative(new Origin(), "WaitFrames", FuncAttrib.Coro, Types.Void, 0,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        int frames = (int)exec.stack.PopFast().num;
        return CoroutinePool.New<WaitFramesCoroutine>(exec.vm).Init(frames);
    },
    new FuncArgSymbol("n", Types.Int)
);
```

## Important rules

- Always call `cl.Setup()` after all fields and methods have been defined.
- Arguments are popped from `exec.stack` in **reverse order** (last argument first).
- Return `null` from a non-coroutine callback; return a `Coroutine` instance to suspend.
- Use `exec.stack.Push(value)` to return a value to the caller.
- Retain/release `Val` objects that are stored beyond the callback lifetime.

## Example BHL usage

```bhl
Color c = new Color
c.r = 0.5
c.Lerp(0.5)
Color white = Color.White()
```

For the full working example see `example/bindings/bindings.cs`.

## Reading bindings sources without compiling them

A `bindings` entry of `bhl.proj` may list C# registration sources directly. Without an explicit
`dll` they are **read, not compiled**: the compiler and the LSP build the symbols from the
sources themselves, so editing them is picked up at once (the LSP watches them) and no .NET
build is needed. With `dll` set the sources are built into it as before.

```json
"bindings" : [ { "name" : "game", "sources" : ["../autobind/*.cs"] } ]
```

The sources are seen as if compiled with `BHL_FRONT` defined, so native code goes into
`#if !BHL_FRONT` blocks. They are expected in a strict, generated shape, anything else is an
error naming the file and line:

- **Entry:** `Register(Types types)` of the class implementing `IUserBindings` — e.g. the
  project's runtime registration combining several generated modules, read as is — else the
  only `Register(Types types)` of the sources. Methods are called with plain positional
  arguments: unqualified within their class, `Class.Method(...)` (short or namespace-qualified)
  across classes. Overloads are not allowed.
- **Version:** `[BhlBinding("name", "x.y.z")]` on a class declares the entry's (name, version).
- **Statements:** blocks, local variables (`var`, typed or `const`, always initialized), calls,
  and the one conditional `if(<scope>.Resolve(<name>) == null) <statement>` (defines a symbol
  shared by several modules once). No other control flow, no assignments, no lambdas.
- **Expressions:** literals, casts, `new` of bhl types (`FuncSymbolNative`, `ClassSymbolNative`,
  `FieldSymbol`, `EnumSymbolNative`…), arrays `new T[] { … }`, `new List<T>() { … }`, the
  `types.T/TArr/TMap/TTuple/TFunc` helpers, members and methods of bhl objects, enum values
  and `|` of enum flags or integers.
- **Native code:** a local function or method name passed where a delegate is expected (a
  field getter, a class creator) means "there is native code": the symbol gets the front end's
  placeholder (`FieldSymbol.PendingGetter`, `ClassSymbolNative.PendingCreator`…), `null` means
  there is none. Local functions themselves are skipped.
- **Origin:** `new Origin()` points to the line of the registration code it's in.

`tests/cs_bindings/sample_bindings.cs` is a complete sample, read and compiled by the tests.


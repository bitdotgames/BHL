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

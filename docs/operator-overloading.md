# Operator Overloading in BHL

Operator overloading lets you define the behaviour of built-in operators for your own classes. Operators are defined as `static` functions whose name is the operator symbol.

## Supported Operators

- Arithmetic: `+`, `-`, `*`, `/`
- Comparison: `==`, `!=`, `>`, `>=`, `<`, `<=`
- Logical: `&&`, `||`, `!`
- Bitwise: `&`, `|`

## Basic Rules

Operator overloads must:
- Be declared `static`
- Take exactly two arguments (one for unary `!`)
- Return a non-void type

## Defining operators in BHL

```bhl
class Vector2 {
  float x
  float y

  static func Vector2 +(Vector2 a, Vector2 b) {
    Vector2 r
    r.x = a.x + b.x
    r.y = a.y + b.y
    return r
  }

  static func bool ==(Vector2 a, Vector2 b) {
    return a.x == b.x && a.y == b.y
  }
}

func test() {
  Vector2 v1 = {x: 1, y: 2}
  Vector2 v2 = {x: 3, y: 4}
  Vector2 v3 = v1 + v2   // calls the overloaded +
  bool eq    = v1 == v2   // calls the overloaded ==
}
```

## Defining operators via C# bindings

When the class is defined in C#, add the operator as a static `FuncSymbolNative` and define it on the class before calling `Setup()`:

```csharp
var cl = new ClassSymbolNative(new Origin(), "Vector2", null,
    delegate(VM.ExecState exec, ref Val v, IType type)
    {
        v.SetObj(new Vector2(), type);
    }
);

// + operator
cl.Define(new FuncSymbolNative(new Origin(), "+", FuncAttrib.Static, types.T("Vector2"), 0,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        var b = (Vector2)exec.stack.PopFast().obj;
        var a = (Vector2)exec.stack.PopFast().obj;
        exec.stack.Push(Val.NewObj(exec.vm, new Vector2(a.x + b.x, a.y + b.y), cl));
        return null;
    },
    new FuncArgSymbol("a", types.T("Vector2")),
    new FuncArgSymbol("b", types.T("Vector2"))
));

// == operator
cl.Define(new FuncSymbolNative(new Origin(), "==", FuncAttrib.Static, Types.Bool, 0,
    delegate(VM.ExecState exec, FuncArgsInfo args_info)
    {
        var b = (Vector2)exec.stack.PopFast().obj;
        var a = (Vector2)exec.stack.PopFast().obj;
        exec.stack.Push(a.x == b.x && a.y == b.y);
        return null;
    },
    new FuncArgSymbol("a", types.T("Vector2")),
    new FuncArgSymbol("b", types.T("Vector2"))
));

types.ns.Define(cl);
cl.Setup();
```

## Operator precedence

Overloaded operators follow normal precedence rules:

```bhl
Vector2 v3 = v1 + v2 * 2  // equivalent to v1 + (v2 * 2)
```

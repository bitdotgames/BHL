# Operator Overloading in BHL

Operator overloading lets you define the behaviour of built-in operators for a class. An
operator is a `static` function whose name is the operator symbol.

> Operators can only be defined on a class bound from C# — BHL source has no syntax for
> declaring one, since a function declaration requires a name and an operator symbol isn't one.
> Scripts can *use* the operators a bound class defines, as shown below.

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

## Using overloaded operators from BHL

Once a bound class defines them, operators work as they do for built-in types:

```bhl
func test() {
  Vector2 v1 = new Vector2
  Vector2 v2 = new Vector2
  Vector2 v3 = v1 + v2    // calls the overloaded +
  bool eq    = v1 == v2   // calls the overloaded ==
}
```

## Defining operators in C#

Add the operator as a static `FuncSymbolNative` and define it on the class before calling
`Setup()`:

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

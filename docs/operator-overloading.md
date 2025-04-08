# Operator Overloading in BHL

BHL supports operator overloading for classes, but this feature is currently only available through C# bindings. This means you can define custom operator behavior for your classes when implementing them in C#, but not directly in BHL code.

## Implementation

Operator overloading is implemented by defining static functions in your C# class bindings using `FuncSymbolNative`. These functions must follow specific rules for operator overloading.

## Supported Operators

The following operators can be overloaded:
- Arithmetic: `+`, `-`, `*`, `/`
- Comparison: `==`, `!=`, `>`, `>=`, `<`, `<=`
- Logical: `&&`, `||`, `!`
- Bitwise: `&`, `|`

## Basic Rules

1. Operator overloads must be:
   - Defined as static functions using `FuncSymbolNative`
   - Have exactly two arguments
   - Return a non-void type

```csharp
// C# binding code
var cl = BindVector(ts, call_setup: false);

// Overload + operator
var op = new FuncSymbolNative(new Origin(), "+", FuncAttrib.Static, ts.T("Vector"), 0,
  delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var b = (Vector)stack.PopRelease().obj;
    var a = (Vector)stack.PopRelease().obj;

    var result = new Vector();
    result.x = a.x + b.x;
    result.y = a.y + b.y;

    stack.Push(Val.NewObj(frm.vm, result, ts.T("Vector").Get()));
    return null;
  },
  new FuncArgSymbol("a", ts.T("Vector")),
  new FuncArgSymbol("b", ts.T("Vector"))
);
cl.Define(op);
cl.Setup();
```

## Equality Operators

Special handling for equality operators (`==`, `!=`):
- Must return `bool`
- Should handle null comparisons
- Often implemented in pairs

```bhl
class Color {
    float r
    float g

    static func bool ==(Color a, Color b) {
        // Handle null cases
        if (a == null || b == null) {
            return a == b  // true if both null
        }
        return a.r == b.r && a.g == b.g
    }
}
```

## Operator Precedence

Operators maintain their normal precedence rules:

```bhl
func test() {
    var c1 = new Color { r = 1, g = 2 }
    var c2 = new Color { r = 10, g = 20 }
    
    // Multiplication has higher precedence than addition
    var c3 = c1 + c2 * 2  // equivalent to: c1 + (c2 * 2)
}
```

## Common Patterns

### 1. Mathematical Operations

In your C# bindings:

```csharp
// Vector addition
var add_op = new FuncSymbolNative(new Origin(), "+", FuncAttrib.Static, ts.T("Vector"), 0,
  delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var b = (Vector)stack.PopRelease().obj;
    var a = (Vector)stack.PopRelease().obj;

    var result = new Vector();
    result.x = a.x + b.x;
    result.y = a.y + b.y;

    stack.Push(Val.NewObj(frm.vm, result, ts.T("Vector").Get()));
    return null;
  },
  new FuncArgSymbol("a", ts.T("Vector")),
  new FuncArgSymbol("b", ts.T("Vector"))
);

// Then in BHL code:
var v1 = new Vector { x = 1, y = 2 }
var v2 = new Vector { x = 3, y = 4 }
var v3 = v1 + v2  // Uses the overloaded operator
```

### 2. Comparison Operations

```bhl
class Point {
    int x
    int y

    // Compare points by their x coordinate
    static func bool <(Point a, Point b) {
        return a.x < b.x
    }

    static func bool >(Point a, Point b) {
        return a.x > b.x
    }
}
```

## Common Issues

### 1. Invalid Operator Overloads

```bhl
class MyClass {
    // Error: operator overload must be static
    func int +(MyClass other) {
        return 0
    }

    // Error: must have exactly 2 arguments
    static func int *(MyClass a) {
        return 0
    }

    // Error: return type cannot be void
    static func void +(MyClass a, MyClass b) {
    }
}
```

### 2. Type Mismatches

```bhl
class Number {
    int value

    // Error: incompatible types in operation
    static func Number +(Number a, string b) {
        return new Number
    }
}
```

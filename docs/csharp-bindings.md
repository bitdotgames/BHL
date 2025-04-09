# C# Bindings in BHL

BHL provides a robust system for binding C# classes, methods, and fields to make them accessible in BHL scripts.

## Binding Free Functions

Free functions (functions not associated with any class) can be bound using `FuncSymbolNative`:

```csharp
var fn = new FuncSymbolNative(new Origin(), "min", ts.T("float"),
    // Function implementation
    delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
        var b = (float)stack.PopRelease().num;
        var a = (float)stack.PopRelease().num;
        stack.Push(Val.NewFlt(frm.vm, a > b ? b : a)); 
        return null;
    },
    // Function arguments
    new FuncArgSymbol("a", ts.T("float")),
    new FuncArgSymbol("b", ts.T("float"))
);
// Register the function in the type system
ts.ns.Define(fn);
```

Key points for binding free functions:
1. Use `FuncSymbolNative` constructor
2. Specify function name and return type
3. Implement function logic in delegate
4. Define arguments with types
5. Register with `ts.ns.Define()`

The function can then be called from BHL code like any other function:

```bhl
func test() {
    float a = 10
    float b = 5
    float result = min(a, b) // Returns 5
}
```

## Native Class Bindings

### Basic Class Binding

To bind a C# class to BHL:

```csharp
var ts = new Types();
var cl = new ClassSymbolNative(new Origin(), "MyClass", null,
    delegate(VM.Frame frm, ref Val v, IType type) { 
        v.SetObj(new MyClass(), type);
    }
);
ts.ns.Define(cl);
```

### Adding Fields

Fields can be bound with get/set delegates:

```csharp
cl.Define(new FieldSymbol(new Origin(), "myField", Types.Int,
    // Getter
    delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld) {
        var obj = (MyClass)ctx.obj;
        v.SetInt(obj.myField);
    },
    // Setter
    delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld) {
        var obj = (MyClass)ctx.obj;
        obj.myField = (int)v.num;
    }
));
```

### Static Fields

Static fields are bound similarly but don't use the context object:

```csharp
cl.Define(new FieldSymbol(new Origin(), "staticField", Types.Int,
    delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld) {
        v.SetInt(MyClass.staticField);
    },
    delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld) {
        MyClass.staticField = (int)v.num;
    }
));
```

### Methods

Instance methods can be bound with delegates:

```csharp
cl.Define(new FuncSymbolNative(new Origin(), "myMethod",
    delegate(VM.Frame frm, Val ctx, Val[] args, ref Val ret) {
        var obj = (MyClass)ctx.obj;
        ret.SetInt(obj.MyMethod(args[0].num));
        return null;
    },
    Types.Int,  // Return type
    new[] { Types.Int }  // Parameter types
));
```

### Static Methods

Static methods don't require a context object:

```csharp
cl.Define(new FuncSymbolNative(new Origin(), "staticMethod",
    delegate(VM.Frame frm, Val ctx, Val[] args, ref Val ret) {
        ret.SetInt(MyClass.StaticMethod(args[0].num));
        return null;
    },
    Types.Int,
    new[] { Types.Int }
));
```

## Global Variables

Global variables can be bound using `VarSymbolNative`:

```csharp
// C# side
static int _globalCounter = 0;

var counter = new VarSymbolNative(new Origin(), "counter", ts.T("int"),
    delegate(VM.Frame frm, ValStack stack) {
        stack.Push(Val.NewInt(frm.vm, _globalCounter));
        return null;
    }
);
ts.ns.Define(counter);

// Bind function to modify global
var increment = new FuncSymbolNative(new Origin(), "increment", ts.T("void"),
    delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) {
        _globalCounter++;
        return null;
    }
);
ts.ns.Define(increment);
```

In BHL, global variables are accessed using the `static` keyword:

```bhl
static int value = counter  // Get current count
increment()  // Increment counter
```

## Important Rules

1. **Class Setup**
   - Always call `cl.Setup()` after defining all members
   - Define all fields and methods before using the class

2. **Memory Management**
   - Use `PopRelease()` to properly manage stack values
   - Clean up resources in destructors when needed

3. **Type Safety**
   - Always validate types before casting
   - Use proper type conversion methods
   - Handle null values appropriately

## Common Patterns

### Binding Arrays

```csharp
// Define array type
var arrayType = ts.MakeArray(Types.Int);

cl.Define(new FieldSymbol(new Origin(), "numbers", arrayType,
    delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld) {
        var obj = (MyClass)ctx.obj;
        v.SetArray(obj.numbers);
    },
    delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld) {
        var obj = (MyClass)ctx.obj;
        obj.numbers = v.array;
    }
));
```

### Binding Interfaces

```csharp
var iface = new InterfaceSymbolNative(new Origin(), "IMyInterface");
ts.ns.Define(iface);

iface.Define(new FuncSymbol(new Origin(), "interfaceMethod",
    Types.Void,
    new[] { Types.Int }
));
```

### Error Handling

```csharp
delegate(VM.Frame frm, Val ctx, Val[] args, ref Val ret) {
    try {
        var obj = (MyClass)ctx.obj;
        ret.SetInt(obj.RiskyMethod(args[0].num));
    } catch (Exception e) {
        return new Error(e.Message);
    }
    return null;
}
```

For information about the standard library and built-in modules, see [Standard Library](standard-library.md).

## Example Usage in BHL

After binding, the C# class can be used in BHL like this:

```bhl
func test() {
    MyClass obj = new MyClass
    obj.myField = 42
    var result = obj.myMethod(10)
    var static_result = MyClass.staticMethod(20)
}
```

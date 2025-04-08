# BHL Standard Library

BHL includes a standard library (`std`) with built-in functions and modules for common operations.

## Core Module (std)

The core module provides fundamental functionality:

### Type Operations
- `GetType(any o)` - Get type information for any object
- `Is(any o, Type type)` - Type checking, returns true if object matches type

### Coroutines
- `NextTrue()` - Coroutine helper that returns true after first tick

## IO Module (std/io)

The IO module provides input/output operations:

### Console Operations
- `Write(string s)` - Write string to console without newline
- `WriteLine(string s)` - Write string to console with newline

### Example Usage

```bhl
import "std/io"

func test() {
    // Basic console output
    std.io.WriteLine("Hello, World!")
    std.io.Write("No newline")
    
    // Type checking
    any x = 42
    Type t = std.GetType(x)
    if std.Is(x, t) {
        std.io.WriteLine("x is of type " + t)
    }
}
```

## Implementing Standard Library Modules

To create your own standard library module:

```csharp
// Create a new module
var m = new Module(ts, "std/mymodule");

// Create nested namespaces
var mymodule = m.ns.Nest("std").Nest("mymodule");

// Add functions to the module
var fn = new FuncSymbolNative(new Origin(), "MyFunction", Types.Void,
    delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
        var s = stack.PopRelease().str;
        // Implementation here
        return null;
    }, 
    new FuncArgSymbol("s", Types.String)
);
mymodule.Define(fn);
```

### Module Organization
1. Use descriptive module names with the `std/` prefix
2. Create appropriate nested namespaces
3. Document function parameters and return types
4. Follow error handling conventions
5. Register all functions with the module

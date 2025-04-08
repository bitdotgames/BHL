# Advanced Features in BHL

## Namespaces

Namespaces help organize code and avoid naming conflicts:

```bhl
namespace Game.Utils {
    class MathHelper {
        static func float Calculate() {
            // Implementation
        }
    }
}

// Using namespaced items
func test() {
    var result = Game.Utils.MathHelper.Calculate()
}
```

## Fibers and Parallelism

BHL supports concurrent programming through fibers:

```bhl
// Fiber function
coro func DoWork() {
    // Asynchronous work
    yield
    // Continue work
}

// Starting parallel operations
func StartOperations() {
    start(func() {
        // Parallel work
    })
}
```

## Operator Overloading

BHL allows customizing operator behavior for user-defined types:

```bhl
class Vector {
    float x
    float y
    
    // Overloading addition
    func Vector operator+(Vector other) {
        var result = new Vector
        result.x = this.x + other.x
        result.y = this.y + other.y
        return result
    }
}
```

## Defer Statement

The `defer` statement allows scheduling cleanup code:

```bhl
func ProcessFile() {
    var file = OpenFile()
    defer {
        file.Close()  // Will be called when function exits
    }
    // Process file...
}
```

## LSP (Language Server Protocol) Support

BHL includes comprehensive LSP support for IDE integration:

1. Code Intelligence
   - Go to definition
   - Find references
   - Hover information
   - Signature help

2. Code Analysis
   - Semantic tokens
   - Diagnostics
   - Code completion
   - Symbol search

## .NET Interoperability

### Native Class Integration

```bhl
// Using .NET classes
class BHLWrapper : NativeClass {
    func void ProcessData() {
        // Interact with .NET
    }
}
```

### Type Mapping

BHL types map to .NET types:
- `int` → `System.Int32`
- `float` → `System.Single`
- `string` → `System.String`
- `bool` → `System.Boolean`

## Memory Management

### Reference Counting

```bhl
class Resource {
    func void Initialize() {
        // Acquire resources
    }
    
    func void Cleanup() {
        // Release resources
    }
}

func test() {
    var res = new Resource
    // Resource is automatically cleaned up when reference count reaches zero
}
```

### Value Semantics

```bhl
// Value types
struct Point {
    int x
    int y
}

// Reference types
class GameObject {
    string name
}
```


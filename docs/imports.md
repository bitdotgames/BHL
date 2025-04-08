# Imports in BHL

BHL provides a module system that allows you to organize and reuse code across multiple files. The import system helps manage dependencies between modules and provides access to functions, classes, and other symbols defined in external files.

## Basic Usage

```bhl
// Import a module
import "mymodule"

// Use symbols from the imported module
func test() {
    mymodule.someFunction()
}
```

## Import Rules

### 1. Module Names

- Module names are strings that correspond to file names without the `.bhl` extension
- Two types of paths are supported:
  - Relative paths (starting with "./" or "../"): resolved relative to the current file
  - Absolute paths (not starting with "."): resolved using the include paths
- Forward slashes are used for path separation

```bhl
// Relative path - resolved relative to current file
import "./math"
import "../utils/math"

// Absolute paths - resolved using include paths
import "core/logging"  // searches in include paths
import "std/io"       // searches in include paths
```

### 2. Import Resolution

- Relative imports (starting with ".") are resolved relative to the current file
- All other imports are considered absolute and resolved using:
  1. The include paths specified in the project configuration
  2. The standard library paths

### 3. Import Behavior

- Self-imports are ignored (importing the current module)
- Duplicate imports of the same module are not allowed
- Imports must be at the top level of the file
- Commented imports are ignored

### 4. Module Initialization

Each module can define an optional `init()` function that is automatically called when the module is imported:

```bhl
// config.bhl
func init() {
    // Initialization code runs when this module is imported
    setupConfig()
}

// main.bhl
import "config"  // config.init() is called here
```

Key points about `init()`:
- Called automatically when module is imported
- Can be used for module-level setup
- No parameters or return values allowed
- Runs only once per module
- Execution order follows import dependencies:
  ```bhl
  // a.bhl
  import "b"  // b.init() runs first
  func init() { /* runs second */ }

  // b.bhl
  func init() { /* runs first */ }
  ```

## Common Patterns

### 1. Namespace Organization

```bhl
// math.bhl
namespace math {
    func add(int a, int b) {
        return a + b
    }
}

// main.bhl
import "math"

func test() {
    var result = math.add(1, 2)
}
```

### 2. Global Variables

```bhl
// config.bhl
int MAX_RETRIES = 3

// main.bhl
import "config"

func retry() {
    for(int i = 0; i < MAX_RETRIES; i++) {
        // retry logic
    }
}
```

### 3. Type Imports

```bhl
// types.bhl
class Vector {
    float x
    float y
}

// main.bhl
import "types"

func createVector() {
    var v = new Vector
    v.x = 1
    v.y = 2
}
```

## Common Issues

### 1. Invalid Imports
```bhl
// This will fail - module doesn't exist
import "nonexistent"
```

### 2. Duplicate Imports
```bhl
// This will fail - duplicate import
import "module"
import "/module"
```
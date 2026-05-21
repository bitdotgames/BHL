# Imports in BHL

```bhl
import "mymodule"
import "./math"          // relative to current file
import "../utils/math"   // relative to current file
import "core/logging"    // resolved via include paths
import "std/io"          // standard library
```

Imports must appear at the top level of the file. Duplicate imports and self-imports are silently ignored.

## Module initialization

A module may define an optional `init()` function that runs automatically once when the module is first imported:

```bhl
// config.bhl
func init() {
    setupConfig()
}

// main.bhl
import "config"  // config.init() runs here
```

Initialization order follows import dependencies — imported module's `init()` runs before the importer's `init()`.

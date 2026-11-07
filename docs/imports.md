# Imports in BHL

```bhl
import "mymodule"
import "./math"          // relative to current file
import "../utils/math"   // relative to current file
import "core/logging"    // resolved via include paths
import "std/io"          // standard library
```

Imports must appear at the top level of the file. A self-import is silently ignored; importing
the same module twice is an error (`already imported 'x'`).

An import makes a module's namespaces reachable by their full path. To refer to their members
without that prefix, see [`using`](namespaces.md#the-using-directive).

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

# Namespaces in BHL

Namespaces in BHL provide a way to organize code and avoid naming conflicts. They help create hierarchical structures for your code organization.

## Declaring Namespaces

### Basic Namespace Declaration

```bhl
namespace Game.Utils {
    // Code inside namespace
}
```

### Nested Namespaces

You can declare nested namespaces in two ways:

```bhl
// Using dot notation
namespace Game.Utils.Math {
    func float Add(float a, float b) {
        return a + b
    }
}

// Using nested blocks
namespace Game {
    namespace Utils {
        namespace Math {
            func float Multiply(float a, float b) {
                return a * b
            }
        }
    }
}
```

## Referring to Namespace Members

```bhl
// Full qualification always works
Game.Utils.Math.Add(1.0, 2.0)
```

Inside a namespace you can refer to its own members without a prefix, and to a
nested namespace relatively. From outside, either fully qualify, or shorten it
with [`using`](#the-using-directive).

### Multiple Declarations

You can split namespace declarations across multiple files:

```bhl
// file1.bhl
namespace Game.Utils {
    func int Add(int a, int b) {
        return a + b
    }
}

// file2.bhl
namespace Game.Utils {
    func int Multiply(int a, int b) {
        return a * b
    }
}
```

## Classes in Namespaces

### Class Definition

```bhl
namespace Game.Entities {
    class Player {
        string name
        int health
        
        func void TakeDamage(int amount) {
            health -= amount
        }
    }
}
```

### Using Namespaced Classes

```bhl
func test() {
    Game.Entities.Player player = new Game.Entities.Player
    player.name = "Hero"
    player.health = 100
}
```

## Importing Namespaces

### Basic Import

```bhl
// Import a module containing namespaces
import "game_utils"

// Now you can use the imported namespaces
func test() {
    Utils.Math.Add(1, 2)
}
```

### Import Rules

1. Imports must be at the file level
2. Imported symbols are available throughout the file
3. In case of naming conflicts, use fully qualified names or a `using` alias

## The `using` directive

Writing the full path everywhere gets noisy. `using` shortens it for the rest of the file.

### Bringing members in unqualified

```bhl
import "game_utils"
using Game.Utils

func test() {
    Math.Add(1, 2)   // Game.Utils.Math.Add
    Helper()         // Game.Utils.Helper
}
```

Everything inside the namespace becomes visible without a prefix, and nested
namespaces become reachable relatively (`Math` above is `Game.Utils.Math`).
Fully qualified names keep working, so you can always disambiguate.

### Binding a short name

```bhl
import "game_utils"
using m = Game.Utils.Math

func test() {
    m.Add(1, 2)
}
```

The alias form binds one short name instead of bringing everything in. Prefer it
for a large namespace: it cannot collide with anything the namespace holds.

Both forms can appear in the same file, and the target may be nested:

```bhl
using Game.Utils          // members unqualified
using m = Game.Utils.Math // and a short name for a nested one
```

A `using` may also name a namespace declared in the same file, with no import at all.

### Scope

A `using` applies to **the file that declares it** and is not re-exported — a
module importing yours has to declare its own. This is the same scoping as C#'s
file-level `using`.

```bhl
// lib.bhl
import "game_utils"
using Game.Utils

func int lib_helper() {
    return Helper()   // fine, declared here
}

// app.bhl
import "lib"

func test() {
    Helper()          // error: symbol 'Helper' not resolved
}
```

### Errors

```bhl
using Game.Utils
func Helper() {}        // error: already defined symbol 'Helper'

using Game.Utils.Helper // error: 'Game.Utils.Helper' is not a namespace
using No.Such.Thing     // error: namespace 'No.Such.Thing' not found
```

A plain `using` that clashes with one of your own symbols is reported rather than
silently shadowing either side. If you hit that, the alias form avoids it entirely.

## Naming conflicts

When the same name exists in multiple namespaces, use the fully qualified name — or a
`using` alias (see above) — to disambiguate:

```bhl
namespace Game {
    class Entity {}
}

namespace Game.Sub {
    class Entity {}  // different from Game.Entity

    func void test() {
        Entity e        = new Entity      // Game.Sub.Entity
        Game.Entity ge  = new Game.Entity // Game.Entity
    }
}
```

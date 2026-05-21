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

## Using Namespaces

### Accessing Namespace Members

```bhl
// Full qualification
Game.Utils.Math.Add(1.0, 2.0)

// Using namespace members after import
import "math_utils"
Math.Add(1.0, 2.0)
```

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
3. In case of naming conflicts, fully qualified names must be used

## Naming conflicts

When the same name exists in multiple namespaces, use the fully qualified name to disambiguate:

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

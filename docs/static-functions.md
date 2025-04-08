# Static Functions in BHL

BHL supports static functions at the module level, which can be used as utility functions within the module. These functions are declared using the `static` keyword and are not associated with any class.

## Basic Usage

```bhl
// Define a static function
static func int add(int a, int b) {
    return a + b
}

// Use the static function
func test() {
    int result = add(5, 3)  // Call directly without module prefix
}
```

## Key Features

### 1. Module Scope

Static functions are only accessible within the module where they are defined:

```bhl
// math.bhl
static func int multiply(int a, int b) {
    return a * b
}

// main.bhl
import "math"

func test() {
    multiply(2, 3)  // Error: multiply is not accessible
}
```

### 2. Function Naming

- Each static function name must be unique within the module
- Function names cannot be reused even with different signatures:

```bhl
// This will cause an error
static func int process(int a) {
    return a * 2
}

static func string process(string s) {  // Error: process already defined
    return s + s
}
```

### 3. Function Organization

Static functions are useful for:
- Utility functions used within a module
- Helper functions that don't need class context
- Pure functions without side effects

```bhl
static func bool isValid(string input) {
    return input.Count > 0
}

static func string format(string name, int id) {
    return name + "#" + (string)id
}

func test() {
    if isValid("test") {
        trace(format("User", 123))
    }
}
```

## Best Practices

1. Use static functions for:
   - Pure computational operations
   - Validation functions
   - Formatting utilities
   - Helper functions used across the module

2. Avoid static functions when:
   - The function needs instance state
   - The function should be accessible from other modules
   - The function belongs to a specific class

3. Naming conventions:
   - Use clear, descriptive names
   - Follow verb-noun pattern for actions
   - Use adjective-noun pattern for queries

## Common Issues

### 1. Accessibility

```bhl
// module_a.bhl
static func helper() { }  // Only visible in module_a

// module_b.bhl
import "module_a"
func test() {
    helper()  // Error: helper is not accessible
}
```

### 2. Name Conflicts

```bhl
// Error: duplicate function name
static func process() { }
static func process() { }  // Error: already defined

// Error: cannot have same name as class method
class MyClass {
    func process() { }
}
static func process() { }  // Error: name conflict
```

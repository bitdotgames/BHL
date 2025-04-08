# Defer Statement

The `defer` statement in BHL allows you to schedule code execution for when the current scope exits. This is useful for cleanup operations, resource management, and ensuring certain actions are performed regardless of how a function exits.

## Basic Usage

```bhl
func test() {
    defer {
        // This code runs when the function exits
        cleanup()
    }
    
    // Main function code
    doWork()
}
```

## Key Features

### 1. Scope-based Execution

Deferred code executes when its enclosing scope ends:

```bhl
func test() {
    defer {
        trace("outer")  // Executes last
    }
    
    {
        defer {
            trace("inner")  // Executes first
        }
        trace("block")
    }  // inner defer executes here
    
    trace("function")
}  // outer defer executes here

// Output: block, inner, function, outer
```

### 2. Variable Access

Deferred blocks can access and modify variables from their enclosing scope:

```bhl
func test() {
    float value = 1
    defer {
        // Can access and modify variables
        if (value == 2) {
            processValue(value)
        }
    }
    
    value = 2
}
```

### 3. Multiple Defers

You can have multiple defer statements in the same scope. They execute in last-in-first-out (LIFO) order:

```bhl
func test() {
    defer { trace("1") }  // Executes third
    defer { trace("2") }  // Executes second
    defer { trace("3") }  // Executes first
}

// Output: 3, 2, 1
```

### 4. Parallel Execution

Defer works with parallel execution blocks:

```bhl
coro func test() {
    defer {
        trace("cleanup")
    }
    
    paral {
        {
            defer {
                trace("paral cleanup")
            }
            yield()
        }
    }
}
```

## Important Rules

1. **No Returns**
   - Return statements are not allowed in defer blocks
   - However, they are allowed in lambdas within defer blocks

2. **Loop Control**
   - `break` and `continue` can only be used within loops inside defer blocks
   - They cannot break/continue loops outside the defer block

3. **Nesting**
   - Defer blocks cannot be nested directly
   - But they can be used in different scopes and functions

## Best Practices

1. Use defer for:
   - Resource cleanup
   - State restoration

2. Keep defer blocks simple:
   - Focus on cleanup operations
   - Avoid complex logic
   - Use clear, descriptive names

3. Order matters:
   - Place defers near the resources they manage
   - Consider LIFO order when using multiple defers

## Example: Resource Management

```bhl
func processFile() {
    var file = openFile("data.txt")
    defer {
        // Ensures file is closed even if processing fails
        file.close()
    }
    
    // Process file contents
    processContents(file)
}
```

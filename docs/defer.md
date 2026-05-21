# Defer Statement

`defer` schedules a block to run when the current scope exits, regardless of how it exits.

```bhl
func processFile() {
    var file = openFile("data.txt")
    defer { file.close() }
    processContents(file)
}
```

## Execution order

Deferred blocks run in LIFO order (last `defer` executes first):

```bhl
func test() {
    defer { trace("1") }  // executes third
    defer { trace("2") }  // executes second
    defer { trace("3") }  // executes first
}
// Output: 3, 2, 1
```

Scoped `defer` runs when its enclosing block exits:

```bhl
func test() {
    defer { trace("outer") }
    {
        defer { trace("inner") }
        trace("block")
    }  // "inner" runs here
    trace("function")
}  // "outer" runs here
// Output: block, inner, function, outer
```

## Variable access

Deferred blocks capture variables by reference — they see the value at the time they execute:

```bhl
func test() {
    float value = 1
    defer {
        if(value == 2)
            processValue(value)
    }
    value = 2
}  // processValue(2) is called
```

## Restrictions

- `return` is not allowed inside `defer` blocks (it is allowed inside lambdas within them)
- `break` and `continue` only affect loops that are inside the `defer` block itself

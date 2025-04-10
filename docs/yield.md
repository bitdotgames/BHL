# Yield in BHL

The `yield` keyword in BHL is used for coroutine control flow, allowing functions to pause execution and resume later. This is particularly useful for game development, animations, and other scenarios requiring time-based control.

## Basic Usage

### Coroutine Functions

Functions using `yield` must be marked with the `coro` keyword:

```bhl
// Valid: Function with yield is marked as coro
coro func example() {
    yield()  // Pause execution until next tick
}

// Invalid: Will cause compile error
func wrong() {
    yield()  // Error: function with yield calls must be coro
}
```

### Empty Coroutines

Coroutine functions must contain at least one `yield` call:

```bhl
// Invalid: Will cause compile error
coro func empty() {
    // Error: coro functions without yield calls not allowed
}

// Valid: Contains yield
coro func valid() {
    yield()
}
```

## Yield Variants

### 1. Basic Yield

Pauses execution until the next tick:
```bhl
coro func basic() {
    trace("Start")
    yield()  // Pause here
    trace("Resume")
}
```

### 2. Yield While

`yield while` pauses execution while a condition is true. It's commonly used in parallel blocks for synchronization:

```bhl
coro func test() {
    int i = 0
    paral {
        // First branch: wait until i reaches 3
        yield while(i < 3)

        // Second branch: increment i
        while(true) {
            i = i + 1
            yield()
        }
    }
    return i
}

// Multiple yield whiles in parallel
coro func test2() {
    int i = 0
    paral {
        yield while(i < 5)  // First condition
        while(true) {
            yield while(i < 7)  // Second condition
        }
        while(true) {
            i = i + 1
            yield()
        }
    }
    return i
}
```

### 3. Yield with Coroutine Calls

You can yield other coroutine functions:

```bhl
coro func subTask() {
    yield()
    return 42
}

coro func mainTask() {
    int result = yield subTask()  // Waits for subTask to complete
}
```

## Class and Interface Integration

### Methods

```bhl
class Example {
    coro func process() {
        yield()
    }
}

coro func test() {
    var obj = new Example
    yield obj.process()  // Yield class method
}
```

### Inheritance

```bhl
class Base {
    coro func task() {
        yield()
    }
}

class Derived : Base {
    coro func process() {
        yield base.task()  // Yield base class method
    }
}
```

### Interfaces

```bhl
interface IProcessor {
    coro func process()
}

class Processor : IProcessor {
    coro func process() {
        yield()
    }
}
```

## Common Patterns

### 1. Time-based Waiting

```bhl
coro func waitExample() {
    trace("Starting")
    yield wait(5)  // Wait for 5 milliseconds
    trace("Done")
}
```

### 2. Parallel Execution

```bhl
coro func parallelExample() {
    paral {
        {
            yield task1()
        }
        {
            yield task2()
        }
    }
}
```

## Restrictions

### 1. Defer Blocks

Yield is not allowed in defer blocks:

```bhl
coro func wrong() {
    defer {
        yield()  // Error: yield is not allowed in defer block
    }
}
```

### 2. Function Pointers

Only coroutine function pointers can be yielded:

```bhl
coro func example() {
    func () regular = func() {}
    yield regular()  // Error: not a coro function

    coro func () valid = coro func() {
        yield()
    }
    yield valid()  // Valid
}
```

### 3. Type Compatibility

Coroutine function pointers are not compatible with regular function pointers:

```bhl
func test() {
    func int() p = coro func int() {  // Error: incompatible types
        yield()
        return 42
    }
}
```

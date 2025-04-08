# Fibers in BHL

Fibers in BHL provide a lightweight concurrency mechanism for managing coroutines and parallel execution. They are the underlying mechanism that powers BHL's parallel execution features.

## Basic Concepts

### Starting Fibers

Fibers can be started in several ways:

```bhl
// 1. Starting a coroutine directly
coro func foo() {
    yield()
}
start(foo)

// 2. Starting an anonymous coroutine
start(coro func() {
    yield()
})

// 3. Starting with arguments
coro func bar(int value) {
    yield()
}
start(bar, 42)
```

### Fiber Lifecycle

A fiber goes through several states:

1. **Running**: The fiber is actively executing
2. **Suspended**: The fiber is paused via `yield suspend()`
3. **Stopped**: The fiber has completed or been explicitly stopped

```bhl
coro func example() {
    // Running state
    DoWork()
    
    // Suspended state
    yield suspend()
}
```

## Fiber Hierarchy

### Parent-Child Relationships

Fibers can create child fibers, forming a hierarchy:

```bhl
coro func parent() {
    // These become child fibers
    start(child1)
    start(child2)
    yield()
}

coro func child1() {
    yield()
}

coro func child2() {
    yield()
}
```

### Child Management

Parent fibers can control their children:

```bhl
coro func manager() {
    // Start child fibers
    start(worker1)
    start(worker2)
    
    // Parent can check if children are running
    if(fiber_is_running(worker1)) {
        // Do something
    }
    
    // Parent can stop children
    stop(worker1)
}
```

## Function Pointers and Fibers

### Storing Function References

Fibers can work with function pointers for dynamic execution:

```bhl
coro func example() {
    // Store coroutine reference
    func coro void ptr = worker
    
    // Start using function pointer
    start(ptr)
}

coro func worker() {
    yield()
}
```

### Lambda Functions

Fibers can execute lambda functions with captured variables:

```bhl
func example() {
    int counter = 0
    
    // Start lambda that captures counter
    start(coro func() {
        counter = counter + 1
        yield()
        trace((string)counter)
    })
}
```

## Advanced Features

### Deferred Execution

Fibers support deferred execution using `defer`:

```bhl
coro func example() {
    defer {
        // This runs when the fiber stops
        cleanup()
    }
    
    start(worker)
    yield()
}
```

### Fiber Results

Fibers can return values that can be accessed after completion:

```bhl
coro func calculate() {
    yield()
    return 42
}

func example() {
    var fiber = start(calculate)
    // ... later ...
    int result = fiber.result
}
```

## Common Patterns

### Worker Pool

```bhl
coro func worker_pool(int count) {
    for(int i = 0; i < count; i++) {
        start(worker)
    }
    yield()
}

coro func worker() {
    while(true) {
        process_task()
        yield()
    }
}
```

### Event Processing

```bhl
coro func event_processor() {
    while(true) {
        if(has_event()) {
            process_event()
        }
        yield()
    }
}
```

### Timed Operations

```bhl
coro func timed_operation() {
    start_timer()
    yield wait(5.0)  // Wait 5 seconds
    complete_operation()
}
```

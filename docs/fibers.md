# Fibers in BHL

Fibers in BHL provide a lightweight concurrency mechanism for managing coroutines and parallel execution. They are the underlying mechanism that powers BHL's parallel execution features.

## Basic Concepts

### Starting Fibers

`start()` launches a coroutine as an independent fiber and returns a `FiberRef`:

```bhl
coro func foo() { yield() }

FiberRef fb = start(foo)

// anonymous coroutine
start(coro func() { yield() })

// with arguments
coro func bar(int value) { yield() }
start(bar, 42)
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

### Stopping Fibers

Use `stop(FiberRef)` to stop a running fiber. `FiberRef.IsRunning` tells you whether the fiber is still active:

```bhl
coro func manager() {
    FiberRef w1 = start(worker1)
    FiberRef w2 = start(worker2)

    yield wait(1000)
    if(w1.IsRunning) { stop(w1) }
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



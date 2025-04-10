# Parallel Execution in BHL

BHL provides powerful parallel execution capabilities through the `paral` and `paral_all` blocks. These constructs allow you to run multiple code blocks concurrently within coroutines.

## Basic Parallel Execution

### The `paral` Block

The `paral` block executes its child blocks in parallel:

```bhl
coro func test() {
    paral {
        {
            // Block 1
            yield suspend()
        }
        {
            // Block 2
            yield()
            DoSomething()
        }
    }
}
```

### The `paral_all` Block

The `paral_all` block executes all child blocks in parallel and waits for all of them to complete:

```bhl
coro func test() {
    paral_all {
        {
            // Block 1
            yield suspend()
        }
        {
            // Block 2
            yield()
            DoSomething()
        }
    }
}
```

## Control Functions

BHL provides three main functions for controlling parallel execution flow: `yield()`, `suspend()`, and `wait()`.

### yield()

The `yield()` function pauses the current block's execution until the next frame or update cycle. It's commonly used for frame synchronization in game loops and animations:

```bhl
coro func UpdateLoop() {
    paral {
        {
            // This block will run once per frame
            while(true) {
                UpdatePosition()
                yield()  // Pause until next frame
            }
        }
        {
            // This block runs in parallel
            while(true) {
                UpdateAnimation()
                yield()  // Sync with frame rate
            }
        }
    }
}
```

### suspend()

The `suspend()` function completely suspends the current block's execution forever. This is useful for event-driven parallel tasks:

```bhl
coro func ProcessData() {
    paral {
        {
            // This block will suspend forever
            yield suspend()
            // Never will be here
            ProcessData()
        }
        {
            yield PrepareData()
        }
    }
}
```

### wait()

The `wait()` function pauses execution for a specified duration (in seconds). Perfect for timed sequences and delays:

```bhl
coro func TimedSequence() {
    paral {
        {
            // Wait for 2 seconds
            yield wait(2.0)
            ShowMessage("2 seconds passed!")
        }
        {
            // Wait for half a second
            yield wait(0.5)
            PlaySound()
        }
    }
}
```

### Combining Control Functions

You can combine these functions for complex timing and control:

```bhl
coro func ComplexControl() {
    paral {
        {
            while(true) {
                yield wait(1.0)  // Wait one second
                if(shouldSuspend) {
                    yield suspend()  // Suspend if needed
                }
                UpdateState()
                yield()  // Sync with frame
            }
        }
    }
}
```

## Control Flow

### Parallel Block Completion

Parallel blocks have specific completion behavior:

1. For `paral` blocks:
   - When any branch completes its execution, the entire parallel block completes
   - Other branches are terminated at that point

```bhl
coro func test() {
    paral {
        {
            yield wait(1.0)
            trace("Branch 1 done")  // This will print
        }
        {
            yield wait(2.0)  // This branch won't complete
            trace("Branch 2 done")  // This will never print
        }
    }
    trace("After paral")  // Prints after 1 second
}
```

2. For `paral_all` blocks:
   - All branches must complete their execution before the block completes
   - The block only finishes when every branch has finished

```bhl
coro func test() {
    paral_all {
        {
            yield wait(1.0)
            trace("Branch 1 done")  // Prints after 1 second
        }
        {
            yield wait(2.0)
            trace("Branch 2 done")  // Prints after 2 seconds
        }
    }
    trace("After paral_all")  // Prints after 2 seconds
}
```

## Nested Parallel Execution

### Nested Parallel Blocks

You can nest parallel blocks within each other:

```bhl
coro func test() {
    paral {
        {
            paral {
                yield suspend()
                DoSomething()
            }
        }
        {
            yield suspend()
        }
    }
}
```

### Function Calls with Parallel Blocks

Functions can contain parallel blocks and be called from other parallel blocks:

```bhl
func foo() {
    paral {
        DoTask1()
    }
}

func test() {
    paral_all {
        foo()  // Nested parallel execution
    }
}
```

## Coroutines and Parallel Execution

### Yielding in Parallel Blocks

Parallel blocks can contain yield statements:

```bhl
coro func test() {
    paral {
        {
            yield()  // Yields execution
            DoTask1()
        }
        {
            yield suspend()  // Suspends execution
            DoTask2()
        }
    }
}
```

### Automatic Sequence Wrapping

Single statements in parallel blocks are automatically wrapped in sequence blocks:

```bhl
coro func test() {
    paral {
        yield suspend()  // Automatically wrapped
        {
            yield()
            DoSomething()
        }
        yield suspend()  // Automatically wrapped
    }
}
```

## Important Considerations

1. Empty parallel blocks are not allowed:
```bhl
func test() {
    paral {
        // Error: empty paral blocks are not allowed
    }
}
```

2. Parallel blocks can affect variable scope and lifetime:
```bhl
func test() {
    int value = 0
    paral {
        {
            value = 1  // Shared variable access
        }
        {
            DoSomething(value)
        }
    }
}
```

3. Order of execution within parallel blocks is not guaranteed
4. Use `paral_all` when you need to ensure all tasks complete

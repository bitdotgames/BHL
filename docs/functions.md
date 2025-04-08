# Functions in BHL

## Function Declaration

Functions in BHL are declared using the `func` keyword, followed by the return type and function name:

```bhl
func int Add(int a, int b) {
    return a + b
}

func void PrintMessage(string msg) {
    // Implementation
}
```

## Variadic Functions

BHL supports variadic functions that can accept a variable number of arguments:

```bhl
// Basic variadic function
func int sum(...[]int numbers) {
    int total = 0
    foreach(int n in numbers) {
        total += n
    }
    return total
}

// Calling variadic functions
sum(1, 2, 3)       // Returns 6
sum()              // Returns 0
sum(10, 20)        // Returns 30
```

### Combining with Regular Arguments

Variadic parameters can be combined with regular parameters:

```bhl
// Variadic function with regular parameter
func int multiply(int factor, ...[]int numbers) {
    int result = 0
    foreach(int n in numbers) {
        result += factor * n
    }
    return result
}

multiply(2, 1, 2, 3)  // Returns 12 (2*1 + 2*2 + 2*3)
multiply(3)           // Returns 0 (no variadic args)
```

### Spreading Arrays

You can spread an array into variadic arguments:

```bhl
[]int numbers = [1, 2, 3]
sum(...numbers)     // Same as sum(1, 2, 3)
```

### Important Rules

1. Variadic parameter must be the last parameter
2. Cannot be passed by reference
3. Cannot have default values
4. Must maintain type consistency
5. Can be used with async functions and coroutines

## Lambda Functions

### Basic Lambda Syntax

Lambda functions are anonymous functions that can be assigned to variables or passed as arguments:

```bhl
// Lambda function assigned to variable
func int(int) square = func int(int x) { return x * x }

// Immediate lambda invocation
var result = func int(int x) { return x * 2 }(5)
```

### Capturing Variables

Lambdas can capture variables from their enclosing scope:

```bhl
func test() {
    int multiplier = 10
    func int(int) multiply = func int(int x) { return x * multiplier }
    // multiply will use the captured 'multiplier' variable
}
```

## Closures

Closures allow functions to capture and maintain access to variables from their enclosing scope:

```bhl
func func float(float) MakeMultiplier(float factor) {
    return func float(float x) { return x * factor }
}

func test() {
    var times2 = MakeMultiplier(2)
    var times3 = MakeMultiplier(3)
    // times2 and times3 maintain their own copies of factor
}
```

## Async Functions

### Starting Async Operations

The `start` keyword is used to begin asynchronous execution:

```bhl
func AsyncOperation() {
    start(func() {
        // Async code here
    })
}
```

## Function Pointers

Function pointers allow storing and passing functions as values:

```bhl
// Declaring a function pointer type
func int(int, int) operation

// Assigning a function
operation = Add  // Assuming Add is defined above

// Using a function pointer
var result = operation(5, 3)
```



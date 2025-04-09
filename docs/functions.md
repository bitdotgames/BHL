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

## Coroutines

Coroutines are functions that can be suspended and resumed. They are defined using the `coro` keyword and can use `yield` to pause execution:

```bhl
coro func Process() {
    // Do some work
    yield()
    // Continue processing
}

func StartCoroutine() {
    start(Process)  // Start coroutine execution
}
```

Coroutines can also return values between yields:

```bhl
coro func int GenerateNumbers() {
    yield return 1
    yield return 2
    yield return 3
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

## Static Functions

BHL supports static functions at the module level, which can be used as utility functions within the module. These functions are declared using the `static` keyword and are not associated with any class.

### Basic Usage

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

### Key Features

#### 1. Module Scope

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

#### 2. Function Naming

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

#### 3. Function Organization

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

### Common Issues

#### 1. Accessibility

```bhl
// module_a.bhl
static func helper() { }  // Only visible in module_a

// module_b.bhl
import "module_a"
func test() {
    helper()  // Error: helper is not accessible
}
```

#### 2. Name Conflicts

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



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

## Multiple Return Values

BHL functions can return multiple values of different types using comma-separated return types and values:

```bhl
// Function returning multiple values
func float,string,int GetValues() {
    return 100,"foo",3
}

// Assigning multiple return values
func test() {
    float num
    string str
    int val
    num,str,val = GetValues()
    // num = 100, str = "foo", val = 3
}
```

### Important Rules

1. The number of return values must match the number of variables in assignment
2. Return types must match the variable types exactly
3. Return values are assigned in order from left to right
4. All return values must be consumed (assigned to variables)
5. Type mismatches will cause compilation errors

### Common Patterns

```bhl
// Returning multiple values of same type
func float,float GetCoordinates() {
    return 300,100
}

// Using subset of return values
float x,float y = GetCoordinates()

// Mixing with existing variables
string s
float a,s = GetStringAndNumber()  // s is reused
```

## Function Variadic Arguments

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

## Parameter Passing

BHL supports both pass-by-value and pass-by-reference parameter passing:

### Pass by Value (Default)
By default, parameters are passed by value:
- For primitive types (int, float, bool), a copy of the value is passed
- For objects and arrays, a copy of the reference is passed

### Pass by Reference
Use the `ref` keyword to pass parameters by reference, allowing functions to modify the original variables:

```bhl
func void modify(ref float x, float y) {
    x = 20.0    // Modifies the original variable
    y = 30.0    // Only modifies local copy
}

func test() {
    float a = 10.0
    float b = 15.0
    modify(ref a, b)
    // a is now 20.0
    // b is still 15.0
}
```

### Named Arguments with Ref
You can also use named arguments with ref parameters:

```bhl
func void process(ref float value, float factor) {
    value *= factor
}

func test() {
    float x = 5.0
    process(value: ref x, factor: 2.0)
    // x is now 10.0
}
```

### Important Rules for Ref Parameters
1. The `ref` keyword is required both in function declaration and at the call site
2. Ref parameters can be combined with regular value parameters
3. Ref parameters can be used with nested function calls
4. Variables must be initialized before being passed as ref parameters

## Named Arguments

BHL supports named arguments when calling functions. This allows you to specify arguments by their parameter name rather than just by position:

```bhl
func int calculate(float a, float b) {
    return a + b
}

// Regular positional arguments
calculate(5.0, 3.0)

// Named arguments - order doesn't matter
calculate(b: 3.0, a: 5.0)

// Mix positional and named arguments
calculate(5.0, b: 3.0)
```

Named arguments are particularly useful when:
- A function has many parameters
- You want to skip optional parameters
- You want to make the code more readable by explicitly naming the arguments
- Working with reference parameters

### Named Arguments with Ref Parameters

When using reference parameters, the `ref` keyword must come after the parameter name:

```bhl
func void modify(ref float value, float factor) {
    value *= factor
}

func test() {
    float x = 5.0
    modify(value: ref x, factor: 2.0)
}
```

## Default Arguments

BHL supports default values for function parameters. When a parameter has a default value, it becomes optional when calling the function:

```bhl
// Function with default arguments
func float calculate(float base, float multiplier = 1.0, float offset = 0.0) {
    return base * multiplier + offset
}

// Different ways to call the function
calculate(10.0)               // Uses default values: multiplier=1.0, offset=0.0
calculate(10.0, 2.0)          // Uses default value: offset=0.0
calculate(10.0, 2.0, 5.0)     // Specifies all values

// Using named arguments with defaults
calculate(base: 10.0, offset: 5.0)  // Uses default value: multiplier=1.0
```

### Rules for Default Arguments

1. Default arguments must be at the end of the parameter list
```bhl
// CORRECT
func foo(float a, float b = 1.0, float c = 2.0) { }

// ERROR: Non-default parameter after default ones
func bar(float a = 1.0, float b) { }  // Will not compile
```

2. Default values can be expressions, including function calls
```bhl
func float getDefault() { return 42.0 }
func float process(float value = getDefault()) { return value }
```

3. Reference parameters cannot have default values
```bhl
// ERROR: ref parameters cannot have defaults
func modify(ref float value = 10.0) { }  // Will not compile
```

4. There is a limit on the number of default arguments a function can have (currently 26)

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

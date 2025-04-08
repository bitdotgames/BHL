# Getting Started with BHL

## Language Overview

BHL is a strongly-typed programming language implemented in .NET, designed for both general-purpose programming and game development. It features:

- Strong static typing
- Object-oriented programming
- First-class functions and lambdas
- Modern array and collection handling
- .NET interoperability

## Your First BHL Program

### Hello World

Create a file named `hello.bhl`:

```bhl
import "std/io"

func main() {
    string message = "Hello, World!"
    std.io.WriteLine(message)
}
```

### Basic Program Structure

A typical BHL program consists of:

1. Import statements (if needed)
2. Function definitions
3. Class definitions
4. Global declarations

Example:
```bhl
// Imports
import "math"

// Class definition
class Calculator {
    func int Add(int a, int b) {
        return a + b
    }
}

// Main function
func main() {
    var calc = new Calculator
    var result = calc.Add(5, 3)
}
```

## Basic Syntax

### Variables

```bhl
// Type declarations
int number = 42
string text = "Hello"
bool flag = true

// Type inference
var count = 0
var name = "BHL"
```

### Control Flow

```bhl
// If statement
if (condition) {
    // code
} else {
    // code
}

// For loop
for(int i = 0; i < 10; i++) {
    // code
}

// Array iteration
for(var item in array) {
    // code
}
```

### Functions

```bhl
// Basic function
func int Add(int a, int b) {
    return a + b
}

// Function with multiple parameters
func string FormatName(string first, string last) {
    return first + " " + last
}
```

## Next Steps

1. Explore the documentation
   - [Core Features](core-features.md)
   - [Object-Oriented Programming](oop.md)
   - [Functions](functions.md)
   - [Collections](collections.md)

2. Practice with examples
   - Try the sample code
   - Modify existing programs
   - Create your own projects

3. Learn advanced features
   - Study error handling
   - Explore .NET interop
   - Understand memory management

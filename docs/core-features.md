# Core Language Features

## Basic Types

BHL provides several built-in types:

### Numeric Types
- `int`: 64-bit signed integer numbers
- `float`: 64-bit floating-point numbers

### Text Type
- `string`: Text strings with Unicode support
  ```bhl
  // String operations
  string text = "Hello"
  int length = text.Count      // Get string length
  string char = text.At(0)    // Get character at index
  int pos = text.IndexOf("l") // Find substring position
  ```

### Boolean Type
- `bool`: Boolean values (`true`/`false`)
  ```bhl
  bool flag = true
  int num = (int)flag  // Converts to 1
  ```

For detailed information about converting between types, see [Type Casting](type-casting.md).

## Variables and Constants

### Variable Declaration

Variables in BHL are strongly typed and must be initialized before use:

```bhl
// Explicit type declaration
int count = 10
string name = "BHL"
float price = 19.99

// Type inference using 'var'
var total = 100    // Inferred as int
var message = "Hi" // Inferred as string
```

### Static Variables
```bhl
// Module-level static variables
static int counter = 0

// Static variables are not imported by default
static string appName = "MyApp"
```

## Control Flow

### Conditional Statements

```bhl
// Basic if statement
if (condition) {
    // code
} else if (another_condition) {
    // code
} else {
    // code
}

// Condition with type checking
if (value is string) {
    // value is a string
}
```

### Loops

```bhl
// Standard for loop
for(int i = 0; i < 10; i++) {
    // code
}

// Array iteration
for(var item in array) {
    // process item
}

// String iteration
string text = "Hello"
for(int i = 0; i < text.Count; i++) {
    string char = text.At(i)
    // process character
}

// While loop with break
int counter = 0
while (true) {
    counter++
    if (counter >= 5) {
        break  // exit loop when counter reaches 5
    }
}

// While loop with continue
int i = 0
while (i < 10) {
    i++
    if (i % 2 == 0) {
        continue  // skip even numbers
    }
    // process odd numbers
}

// Do-while loop with break
int value = 0
do {
    value++
    if (value == 3) {
        break  // exit when value is 3
    }
    // process values 1 and 2
} while (true)

// For loop with continue
for(int j = 0; j < 5; j++) {
    if (j == 2) {
        continue  // skip when j is 2
    }
    // process other values
}
```

### Parallel Execution

BHL supports parallel execution blocks for concurrent operations. For detailed information, see [Parallel Execution](parallel.md).

```bhl
// Basic parallel block
paral {
    {
        // First concurrent block
        yield()
    }
    {
        // Second concurrent block
        yield()
    }
}
```

## String Operations

### String Manipulation
```bhl
// Concatenation
string first = "Hello"
string second = "World"
string result = first + " " + second

// Special characters
string withNewline = "Line 1\nLine 2"   // Newline
string withTab = "Column1\tColumn2"    // Tab
string withQuotes = "He said \"Hello\"" // Escaped quotes
```

### String Methods
```bhl
// String length
string text = "Hello World"
int length = text.Count

// Character access
string firstChar = text.At(0)    // "H"
string lastChar = text.At(length - 1) // "d"

// Substring search
int position = text.IndexOf("World")  // Returns 6
int notFound = text.IndexOf("xyz")   // Returns -1
```
for(var item in array) {
    // code
}
```

## Type System

BHL features a strong, static type system:

- All variables must have a type
- Type checking occurs at compile time
- Explicit type conversion when required
- Support for user-defined types

## Operators

### Arithmetic Operators
- `+` Addition
- `-` Subtraction
- `*` Multiplication
- `/` Division

### Comparison Operators
- `==` Equal to
- `!=` Not equal to
- `>` Greater than
- `<` Less than
- `>=` Greater than or equal to
- `<=` Less than or equal to

### Logical Operators
- `&&` Logical AND
- `||` Logical OR
- `!` Logical NOT

## Best Practices

1. Always initialize variables before use
2. Use meaningful variable names
3. Follow consistent indentation
4. Use appropriate types for data
5. Comment code when necessary

# Type Casting in BHL

Type casting in BHL allows you to convert values from one type to another. BHL supports both explicit and implicit type casting with strong type safety.

## Basic Type Casting

### Numeric Conversions
```bhl
// Float to integer
float f = 3.14
int i = (int)f    // Float to int: 3

// Boolean to integer
bool flag = true
int boolNum = (int)flag  // true converts to 1

// Integer division with cast
int a = 5
int b = 2
float result = (float)a / b  // 2.5
```

## String Conversions

### Basic String Casting
```bhl
// Numbers to string
int num = 42
float pi = 3.14
string numStr = (string)num    // "42"
string floatStr = (string)pi   // "3.14"

// Boolean to string
bool flag = true
string boolStr = (string)flag  // "true"
```

### Implicit String Casting
```bhl
// String concatenation automatically converts numbers
string result = "Count: " + 42       // "Count: 42"
string math = "Pi is " + 3.14       // "Pi is 3.14"
string status = "Ready: " + true    // "Ready: true"

// Multiple concatenations
int x = 10
float y = 20.5
string coords = "(" + x + ", " + y + ")"  // "(10, 20.5)"
```

## Type Safety

### Type Checking
```bhl
// Using 'is' operator for type checking
if (value is string) {
    // value is a string type
}

if (obj is Animal) {
    // obj is an Animal or derived type
}
```

### Safe Casting
```bhl
// Using 'as' operator for safe casting
Color color = value as Color  // null if cast fails
if (color != null) {
    // Successfully cast to Color
}

// Safe array casting
[]int numbers = anyArray as []int
if(numbers != null) {
    // Successfully cast to int array
}
```

## Class Type Casting

### Class Hierarchy Casting
```bhl
// Base and derived classes
class Animal {}
class Dog : Animal {}

// Upcasting (implicit)
Dog dog = new Dog
Animal animal = dog  // Implicit upcast to base class

// Downcasting (explicit)
Animal someAnimal = GetAnimal()
Dog someDog = (Dog)someAnimal  // Explicit downcast

// Safe downcasting
Dog safeDog = someAnimal as Dog
if (safeDog != null) {
    // Successfully cast to Dog
}
```

## Enum Casting

### Enum Type Conversions
```bhl
// Define an enum
enum State {
    IDLE = 1
    RUNNING = 2
    FINISHED = 3
}

// Enum to integer
State state = State.RUNNING
int stateNum = (int)state     // 2

// Integer to enum
int value = 2
State newState = (State)value  // State.RUNNING

// Enum to string
string stateStr = (string)State.FINISHED  // "3"
```

## Collection Casting

### Array Casting
```bhl
// Casting arrays of primitive types
[]int numbers = [1, 2, 3]
[]any anyArray = ([]any)numbers

// Casting back to typed array
[]int typedArray = ([]int)anyArray

// Safe array casting
[]string strings = anyArray as []string
```

### Map Casting
```bhl
// Casting maps
[int]Color colorMap = []
[any]any anyMap = ([any]any)colorMap

// Casting back to typed map
[int]Color typedMap = ([int]Color)anyMap
```

## The 'any' Type

The `any` type in BHL is a dynamic type that can hold values of any other type. It provides flexibility when working with values of different types while maintaining type safety through explicit casting.

### Declaration and Null Values

```bhl
// Declaring any variables
any value
any initialized = null

// Checking for null
if(value == null) {
    // value is null
}
```

### Working with any Type

```bhl
// Casting class instances to any
Color color = new Color
color.r = 10
color.g = 20

any value = (any)color
Color restored = (Color)value

// Using 'as' operator for safe casting
Color safeColor = value as Color
if(safeColor != null) {
    // Cast succeeded
}
```

### Collections with any

```bhl
// Creating arrays of any type
[]any mixedArray = ["string", 42, true]

// Dynamic array initialization
var dynamicArray = new []any [
    "hello",
    1,
    new []any[10, "nested"],
    200
]

// Maps using any type
[any]any flexibleMap = []

// Casting typed maps to any maps
[int]Color colorMap = []
[any]any anyMap = ([any]any)colorMap
```

### Functions with any

```bhl
// Function accepting any type
func void ProcessValue(any value) {
    if(value as string != null) {
        // Handle string
    } else if(value as int != null) {
        // Handle integer
    }
}

// Generic sorting example
func Sort([]any arr, func bool(int, int) cmp) {
    int len = arr.Count
    for(int i = 1; i <= len - 1; i++) {
        for(int j = 0; j < len - i; j++) {
            if(cmp(j, j + 1)) {
                var temp = arr[j]
                arr[j] = arr[j + 1]
                arr[j + 1] = temp
            }
        }
    }
}

// Using the generic sort
[]int numbers = [10, 100, 1]
Sort(numbers, func bool(int a, int b) { 
    return numbers[a] > numbers[b] 
})
```

## Important Considerations

1. Type Safety
   - Always check types before casting when working with unknown types
   - Use `as` operator for safe casting when possible
   - Handle null cases when using safe casting
   - Explicit casting required for collections and complex types

2. Performance
   - Primitive type casts are optimized
   - Complex object casts may have runtime overhead
   - String concatenation with implicit casts creates temporary objects
   - Any type operations may have additional overhead

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

## Important Considerations

1. Type Safety
   - Always check types before casting when working with unknown types
   - Use `as` operator for safe casting when possible
   - Handle null cases when using safe casting

2. Performance
   - Primitive type casts are optimized
   - Complex object casts may have runtime overhead
   - String concatenation with implicit casts creates temporary objects

3. Common Pitfalls
   - Cannot cast between unrelated classes
   - Array casts require compatible element types
   - Enum casts require valid enum values

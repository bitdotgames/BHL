# The 'any' Type in BHL

The `any` type in BHL is a dynamic type that can hold values of any other type. It provides flexibility when working with values of different types while maintaining type safety through explicit casting.

## Basic Usage

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

### Type Casting

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

## Working with Collections

### Arrays of any

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

// Accessing elements
int number = (int)mixedArray[1]  // 42
```

### Type Casting with Arrays

```bhl
// Casting arrays to any arrays
[]Color colors = [color1, color2]
[]any anyArray = ([]any)colors

// Casting back to typed arrays
[]Color typedArray = ([]Color)anyArray

// Safe array casting
[]int numbers = anyArray as []int
if(numbers != null) {
    // Cast succeeded
}
```

### Maps with any

```bhl
// Maps using any type
[any]any flexibleMap = []

// Casting typed maps to any maps
[int]Color colorMap = []
[any]any anyMap = ([any]any)colorMap

// Casting back to typed maps
[int]Color restoredMap = ([int]Color)anyMap

// Safe map casting
[string]int stringMap = anyMap as [string]int
```

## Type Safety

### Explicit Casting Required

```bhl
[]int numbers = []
[]any anys = []

numbers = anys  // Error: explicit cast required
numbers = ([]int)anys  // OK: explicit cast

[string]int stringMap = []
[any]any anyMap = []

stringMap = anyMap  // Error: explicit cast required
stringMap = ([string]int)anyMap  // OK: explicit cast
```

### Working with Functions

```bhl
// Function accepting any type
func void ProcessValue(any value) {
    if(value as string != null) {
        // Handle string
    } else if(value as int != null) {
        // Handle integer
    }
}

// Returning any type
func any GetValue() {
    return "string" as any
}
```

## Memory Management

The `any` type follows BHL's reference counting rules:

1. Values are properly tracked when cast to and from `any`
2. Collections of `any` maintain proper reference counting
3. Casting between collection types maintains proper ownership
4. Null values are handled safely

## Common Use Cases

1. Generic containers and algorithms
2. Plugin architectures
3. Dynamic data structures
4. Interop with dynamic data formats
5. Generic function parameters

```bhl
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

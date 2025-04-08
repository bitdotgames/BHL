# Collections in BHL

## Arrays

### Array Declaration and Creation

Arrays in BHL are declared using the `[]` syntax:

```bhl
// Array declaration
[]int numbers
[]string names

// Array creation
numbers = new []int
names = new []string

// Array initialization with values
numbers = [1, 2, 3, 4, 5]
names = ["Alice", "Bob", "Charlie"]
```

### Array Operations

```bhl
// Adding elements
numbers.Add(6)

// Accessing elements
var first = numbers[0]

// Removing elements
numbers.RemoveAt(0)

// Getting array length
var count = numbers.Count

// Finding elements
var index = numbers.IndexOf(3)

// Clearing arrays
numbers.Clear()
```

## Lists

BHL provides native list support with .NET integration:

```bhl
// Creating a list
[]int list = new []int

// Adding multiple elements
list.Add(1)
list.Add(2)
list.Add(3)

// Inserting at specific position
list.Insert(1, 10)  // Insert 10 at index 1
```

## Collection Operations

### Iteration

```bhl
// Using for loop
for(int i = 0; i < array.Count; i++) {
    var item = array[i]
    // Process item
}

// Using foreach
for(var item in array) {
    // Process item
}
```

### Sorting and Searching

```bhl
// Array operations
var index = array.IndexOf(searchValue)
```

## Memory Management

BHL uses reference counting for memory management:

### Value Ownership

```bhl
func test() {
    []int numbers = new []int
    numbers.Add(1)  // numbers owns the value
    
    // Array is automatically cleaned up when it goes out of scope
}
```

### Reference Counting

- Objects are automatically managed through reference counting
- Resources are released when the last reference is removed
- Proper cleanup of collections and their elements



# Maps in BHL

Maps in BHL are key-value collections that allow you to store and retrieve values using unique keys.

## Map Declaration

Maps are declared using the `[KeyType]ValueType` syntax:

```bhl
// Map with string keys and integer values
[string]int scores = []

// Map with integer keys and string values
[int]string names = []

// Map with enum keys
[Id]int enumMap = []
```

## Map Operations

### Adding and Updating Elements

```bhl
// Creating and initializing a map
[string]int scores = []

// Adding elements
scores["player1"] = 100
scores["player2"] = 200

// Updating elements
scores["player1"] = 150  // Overwrites previous value
```

### Initializing with JSON-like Syntax

```bhl
// Initialize map with key-value pairs
[string]int scores = [["player1", 100], ["player2", 200]]

// Multiple entries with same key - last one wins
[string]int data = [["key", 1], ["other", 2], ["key", 3]]
// data["key"] will be 3
```

### Accessing Elements

```bhl
[string]int scores = [["player1", 100]]

// Direct access
int score = scores["player1"]

// Using TryGet for safe access
bool exists, int value = scores.TryGet("player2")
if(exists) {
    // Use value
}
```

### Removing Elements

```bhl
[string]int scores = []
scores["player1"] = 100

// Remove an element
scores.Remove("player1")

// Clear all elements
scores.Clear()
```

### Checking Map State

```bhl
[string]int scores = []

// Check if map is null
if(scores == null) {
    scores = []
}

// Get number of elements
int count = scores.Count

// Check if key exists
bool hasKey = scores.Contains("player1")
```

## Iterating Maps

### Using foreach

```bhl
[string]int scores = [["player1", 100], ["player2", 200]]

// Iterate over key-value pairs
foreach(string player, int score in scores) {
    // Use player and score
}
```

## Map Types

Maps support various types as keys and values:

```bhl
// Basic types
[string]int stringToInt = []
[int]string intToString = []
[float]bool floatToBool = []

// Enum keys
enum Id {
    First = 1
    Second = 2
}
[Id]string enumToString = []

// Custom class values
[string]Player playerMap = []
```

## Memory Management

Maps in BHL use reference counting for memory management:

1. Values are cloned when stored in the map
2. Values are properly released when:
   - Removed from the map
   - Overwritten by new values
   - The map is cleared
   - The map itself is released

```bhl
[int]int numbers = []
numbers[1] = 10
numbers[2] = 20

// Values are automatically cleaned up when:
numbers.Remove(1)      // Removing an entry
numbers[2] = 30        // Overwriting a value
numbers.Clear()        // Clearing the map
numbers = null         // Releasing the map
```

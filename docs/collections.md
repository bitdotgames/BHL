# Collections in BHL

## Arrays

```bhl
// declaration
[]int numbers
[]string names

// creation
numbers = new []int
names   = new []string

// inline initialization
numbers = [1, 2, 3, 4, 5]
names   = ["Alice", "Bob", "Charlie"]
```

### Operations

```bhl
numbers.Add(6)
numbers.Insert(1, 10)   // insert 10 at index 1
numbers.RemoveAt(0)
numbers.Clear()

var first = numbers[0]
var count = numbers.Count
var index = numbers.IndexOf(3)
```

### Iteration

```bhl
for(int i = 0; i < numbers.Count; i++) {
    var item = numbers[i]
}

for(var item in numbers) {
    // process item
}
```

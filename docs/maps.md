# Maps in BHL

Maps are declared with `[KeyType]ValueType` and initialized with `[]`:

```bhl
[string]int scores = []
[int]string names   = []
[Id]int enumMap     = []
```

## Operations

```bhl
scores["player1"] = 100   // add / update
scores.Remove("player1")
scores.Clear()

int score      = scores["player1"]
int count      = scores.Count
bool hasKey    = scores.Contains("player1")

bool exists, int value = scores.TryGet("player2")
```

## Inline initialization

```bhl
[string]int scores = [["player1", 100], ["player2", 200]]
```

If the same key appears more than once, the last value wins.

## Iteration

```bhl
foreach(string player, int score in scores) {
    // use player and score
}
```

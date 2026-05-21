# Functions in BHL

## Function Declaration

```bhl
func int Add(int a, int b) {
    return a + b
}

func void PrintMessage(string msg) { }
```

> **Note**: the `void` return type is optional — `func Foo()` and `func void Foo()` are equivalent.

## Multiple Return Values

```bhl
func float,string,int GetValues() {
    return 100,"foo",3
}

func test() {
    float num
    string str
    int val
    num,str,val = GetValues()
}
```

## Variadic Arguments

```bhl
func int sum(...[]int numbers) {
    int total = 0
    foreach(int n in numbers) { total += n }
    return total
}

sum(1, 2, 3)    // 6
sum()           // 0

// spread an array
[]int nums = [1, 2, 3]
sum(...nums)    // same as sum(1, 2, 3)
```

Variadic parameter must be last and cannot be `ref` or have a default value.

## Pass by Reference

```bhl
func void modify(ref float x, float y) {
    x = 20.0    // modifies the original
    y = 30.0    // local copy only
}

func test() {
    float a = 10.0
    float b = 15.0
    modify(ref a, b)
    // a == 20.0, b == 15.0
}
```

`ref` is required at both the declaration and the call site.

## Named Arguments

```bhl
func int calculate(float a, float b) { return a + b }

calculate(b: 3.0, a: 5.0)       // order doesn't matter
calculate(5.0, b: 3.0)          // mix positional and named
modify(value: ref x, factor: 2.0) // named ref argument
```

## Default Arguments

```bhl
func float calculate(float base, float multiplier = 1.0, float offset = 0.0) {
    return base * multiplier + offset
}

calculate(10.0)               // multiplier=1.0, offset=0.0
calculate(10.0, 2.0)          // offset=0.0
calculate(base: 10.0, offset: 5.0)  // multiplier=1.0
```

Default arguments must be at the end of the parameter list. `ref` parameters cannot have defaults. Maximum 26 default arguments per function.

## Lambda Functions

```bhl
func int(int) square = func int(int x) { return x * x }

// capturing outer variables
func test() {
    int multiplier = 10
    func int(int) mul = func int(int x) { return x * multiplier }
}
```

## Closures

```bhl
func func float(float) MakeMultiplier(float factor) {
    return func float(float x) { return x * factor }
}
```

## Coroutines

```bhl
coro func Process() {
    yield()     // suspend, resume on next tick
}

func StartCoroutine() {
    start(Process)
}
```

## Function Pointers

```bhl
func int(int, int) operation
operation = Add
var result = operation(5, 3)
```

## Static Functions

`static` functions are module-private — not accessible from other modules even after `import`:

```bhl
static func bool isValid(string input) { return input.Count > 0 }
static func string format(string name, int id) { return name + "#" + (string)id }

func test() {
    if isValid("test") { trace(format("User", 123)) }
}
```

Name collisions between static functions are a compile error, as is redefining the same name with a different signature.

# VM Usage

This guide explains how to use the BHL Virtual Machine (VM) from C# code.

## Creating and Running Scripts

```csharp
// 1. Create a VM with a simple script
string bhl = @"
func int test() {
    return 42
}
";

var vm = MakeVM(bhl);
var result = Execute(vm, "test").result.PopRelease().num;  // Returns 42

// 2. Running scripts with arguments
string bhl2 = @"
func float test(float k) {
    return k * 2
}
";

var vm2 = MakeVM(bhl2);
var result2 = Execute(vm2, "test", Val.NewNum(vm2, 3)).result.PopRelease().num;  // Returns 6

// 3. Working with multiple files
var files = new Dictionary<string, string>() {
    {"main.bhl", @"
        import ""utils""
        func test() {
            return utils_func()
        }
    "},
    {"utils.bhl", @"
        func utils_func() {
            return 42
        }
    "}
};

var vm3 = MakeVM(files);
vm3.LoadModule("main");  // Load the main module
Execute(vm3, "test");    // Execute the test function
```

## Working with Global Variables

```csharp
// 1. Setting and reading global variables
string bhl = @"
int counter = 0

func int test() {
    counter = counter + 1
    return counter
}
";

var vm = MakeVM(bhl);

// Find global variable by name
if(vm.TryFindVarAddr("counter", out var addr)) {
    addr.val.num = 10;  // Set value
    var value = addr.val.num;  // Get value
}
```

## Coroutines and Async Execution

```csharp
// 1. Running coroutines
string bhl = @"
coro func test() {
    yield()
    trace(""Hello"")
    yield()
    trace(""World"")
}
";

var vm = MakeVM(bhl);
var fiber = vm.Start("test");  // Start coroutine

while(vm.Tick()) {  // Run until complete
    // Process each tick
}

// 2. Checking fiber status
if(fiber.status == BHS.SUCCESS) {
    // Coroutine completed successfully
}
```

## Error Handling

```csharp
try {
    var vm = MakeVM(bhl);
    Execute(vm, "test");
} catch (Exception e) {
    // Handle compilation or runtime errors
    Console.WriteLine(e.Message);
}
```

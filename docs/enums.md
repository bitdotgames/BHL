# Enums in BHL

Enums in BHL provide a way to define a set of named constants. They can be defined both in BHL code and through C# bindings.

## Defining Enums

### In BHL Code

```bhl
enum State {
    Idle = 0
    Running = 1
    Paused = 2
    Stopped = -1
}

// Negative values are fully supported
enum Status {
    Success = 1
    Warning = 0
    Error = -1
    Critical = -2
}
```

Key points:
- Each enum value must be unique
- Values can be any integer (positive, zero, or negative)
- Negative values are fully supported and commonly used for error states
- Duplicate keys or values are not allowed
- Enum names must be unique (cannot conflict with classes or other enums)

### Through C# Bindings

```csharp
// Define an enum in C#
public enum NativeEnum {
    Foo = 1,
    Bar = 2
}

// Bind it to BHL
var en = new EnumSymbolNative(new Origin(), "NativeEnum", typeof(NativeEnum));
ts.ns.Define(en);

en.Define(new EnumItemSymbol(new Origin(), "Foo", (int)NativeEnum.Foo));
en.Define(new EnumItemSymbol(new Origin(), "Bar", (int)NativeEnum.Bar));
```

## Using Enums

### Basic Usage

```bhl
// Assign enum values
State current = State.Running

// Compare enum values
if (current == State.Running) {
    // Do something
}

// Use in switch statements
switch (current) {
    case State.Idle: break
    case State.Running: break
    case State.Paused: break
    case State.Stopped: break
}
```

### Type Conversion

Enums can be cast to integers but not vice versa:

```bhl
// Valid: Cast enum to int
int value = (int)State.Running

// Invalid: Cannot implicitly cast int to enum
State s = 1  // Error

// Invalid: Cannot cast enum to float
float f = State.Running  // Error
```

### Arrays of Enums

Enums can be used in arrays:

```bhl
[]State states = new []State
states.Add(State.Idle)
states.Add(State.Running)
```

## Common Issues

### 1. Duplicate Keys

```bhl
enum Status {
    Active = 1
    Running = 2
    Active = 3  // Error: duplicate key 'Active'
}
```

### 2. Duplicate Values

```bhl
enum Status {
    Active = 1
    Running = 2
    Busy = 1  // Error: duplicate value '1'
}
```

### 3. Name Conflicts

```bhl
enum Status {
    Active = 1
}

class Status {  // Error: already defined symbol 'Status'
}
```

### 4. Invalid Type Conversions

```bhl
enum State {
    On = 1
    Off = 0
}

func test() {
    State s = 1      // Error: cannot implicitly cast int to enum
    float f = State.On  // Error: cannot cast enum to float
}
```

# Standard Library

## std

| Function | Description |
|---|---|
| `GetType(any o)` | Returns the `Type` object for any value |
| `Is(any o, Type t)` | Returns `true` if `o` is of type `t` |
| `NextTrue()` | Coroutine that yields once then returns `true` |

## std/io

```bhl
import "std/io"

std.io.Write("no newline")
std.io.WriteLine("with newline")
```

| Function | Description |
|---|---|
| `Write(string s)` | Write to console without newline |
| `WriteLine(string s)` | Write to console with newline |

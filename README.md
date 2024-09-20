# **B**e**H**avior **L**anguage

![CI](https://github.com/bitdotgames/bhl/workflows/CI/badge.svg?branch=master&event=push)

> **BHL** is a strictly typed programming language specifically tailored for gameplay logic scripting. It combines Behaviour Trees(BT) primitives with familiar imperative coding style. 

First time it was presented at the [nucl.ai](https://nucl.ai/) conference in 2016. Here's the [presentation slides](https://docs.google.com/presentation/d/1As-bw3pY5pLij86j7nf_ycaG0Hb2EqnrwR3R8ID47sQ/edit?usp=sharing). 

Please note that BHL is in beta state and currently targets only C# platform. Nonetheless it has been battle tested in the real world projects and heavily used by BIT.GAMES for mobile games development built with [Unity](https://unity.com/).

## BHL features

* [ANTLR](http://www.antlr.org/) based: C# frontend + C# interpreting backend
* Statically typed
* Cooperative multitasking support
* Built-in support for pseudo parallel code orchestration
* Golang alike *defer*
* Basic types: *float, int, bool, string, enums, arrays, maps*
* Supports imperative style control constructs: *if/else, while, foreach, break, continue, return*
* Allows user defined: *functions, lambdas, classes, interfaces*
* Supports C# bindings to user types and functions
* Passing arguments to function by *ref* like in C#
* Multiple returned values like in Golang
* Strict control over memory allocations 

## Quick example

```go
coro func GoToTarget(Unit u, Unit t) {
  NavPath path
  defer {
    PathRelease(path)
  }
  
  paral {
   yield while(!IsDead(u) && !IsDead(t) && !IsInRange(u, t))
   
   {
     path = yield FindPathTo(u, t)
     yield Wait(1)
   }
   
   {
     yield FollowPath(u, path)
   }
}
```

## Code samples

### Structs

```go
class Color3 {
  float r
  float g
  float b
}

class Color4 : Color3 {
  float a
}

Color4 c = {}
c.r = 0.9
c.g = 0.5
c.b = 0.7
c.a = 1.0
```

### Enums

```go
enum Status {
  None       = 0
  Connecting = 1
  Connected  = 2
}

Status s = Status.Connected

```

### Generic initializers

```go
class Vec3 {
  float x
  float y
  float z
}

Vec3[] vs = [{x: 10}, {y: 100, z: 100}, {y: 1}]
```

### Passing by **ref**

```go

Unit FindTarget(Unit self, ref float dist_to_target) {
...
  dist_to_target = u.position.Sub(self.position).length
  return u
}

float dist_to_target = 0
Unit u = FindTarget(self, ref dist_to_target)
```
### **Multiple returned values**

```go

Unit,float FindTarget(Unit self) {
...
  float dist_to_target = u.position.Sub(self.position).length
  return u,dist_to_target
}

Unit u,float dist_to_target = FindTarget(self)
```

### **Closures**

```go
Unit u = FindTarget()
float distance = 4
u.InjectScript(coro func() {
  paral_all {
    yield PushBack(distance: distance)
    yield Stun(time: 0.4, intensity: 0.15)
  }
})
```

### Function pointers

```go
func bool(int) p = func bool(int b) { return b > 1 }
return p(10)
```

### **defer** support

```go
{
  RimColorSet(color: {r:  0.65, a: 1.0}, power: 1.1)
  defer { RimColorSet(color: {a: 0}, power: 0) }
     ... 
}
```

### Pseudo parallel code execution

```go
coro func Attack(Unit u) {
  Unit t = TargetInRange(u)
  Check(t != null)
  paral_all {
   yield PlayAnim(u, trigger: "Attack")
   SoundPlay(u, sound: "Swoosh")
   {
     yield WaitAnimEvent(u, event: "Hit")
     SoundPlay(u, sound: "Damage")
     yeld HitTarget(u, t, damage: RandRange(1,16))
   }
}
```

### Example of some unit's top behavior

```go
coro func Selector([]coro func bool() fns) {
  foreach(var fn in fns) {
    if(!yield fn()) {
      continue
    } else {
      break
    }
  }
}

coro func UnitScript(Unit u) {
  while(true) {
    paral {
      yield WaitStateChanged(u)
      Selector(
            [
              coro func bool() { return yield FindTarget(u) },
              coro func bool() { return yield AttackTarget(u) },
              coro func bool() { return yield Idle(u) }
            ]
       )
    }
    yield()
  }
}
```

## Architecture

![BHL architecture](https://puu.sh/qEkYv/edf3b678aa.png)

BHL utilizes a standard interpreter architecture with a **frontend** and a **backend**. Frontend is responsible for reading input files, static type checking and bytecode generation. Binary bytecode is post-processed and optimized in a separate stage. Processed byte code can be used by the backend. Backend is a interpreter responsible for runtime bytecode evaluation. Backend can be nicely integrated with [Unity](https://unity.com/). 

### Frontend

In order to use the frontend you can use the **bhl** tool which ships with the code. See the quick build example below for instructions.  
 

## Quick build example

Currently BHL assumes that you have [dotnet]([https://dotnet.microsoft.com/]) installed and its binaries are in your PATH.


Just try running *run.sh* script: 

> cd example && ./run.sh

This example executes the following [ simple script ](example/unit.bhl)

```markdown
Unit starts...
No target in range
Idling 3 sec...
State changed!
Idle interrupted!
Found new target 703! Approaching it.
Attacking target 703
Target 703 is dead!
Found new target 666! Approaching it.
State changed!
Found new target 902! Approaching it.
...
```

Please note that while BHL works fine under Windows the example assumes you are using \*nix platform.     


## Building

BHL comes with its own simple build tool **bhl**. bhl tool is written in C# and should work just fine both on \*nix and Windows platforms. 

It allows you to build frontend dll, backend dll, compile BHL sources into a binary, run unit tests etc. 

You can view all available build tasks with the following command:

> $ bhl help

## Tests

For now there is no any documentation for BHL except presentation slides. However, there are many [unit tests](test.cs) which cover all BHL features.

You can run unit tests by executing the following command:

> $ bhl test

# Roadmap

## Version 3.0
1. Generics support
1. More compact byte code
1. More optimal runtime memory storage layout
1. Weak references semantics
1. Improved debugger support


## Version 2.0

1. ~~Byte code optimization~~
1. ~~More optimal executor (VM)~~
1. ~~Better runtime errors reporting~~
1. ~~More robust type system~~
1. ~~User class methods~~
1. ~~Interfaces support~~
1. ~~Namespaces support~~
1. ~~Polymorphic class methods~~
1. ~~Nested classes~~
1. ~~Nested in classes enums~~
1. ~~Static class members support~~
1. ~~Variadic function arguments~~
1. ~~Maps support~~
1. ~~Built-in strings basic routines~~
1. ~~Implicit variable types using 'var'~~ 
1. LSP integration
1. ~~Basic debugger support~~

## Version 1.0

1. ~~**ref** semantics similar to C#~~
1. ~~Generic functors support~~
1. ~~Generic initializers~~
1. ~~Multiple return values support~~
1. ~~**while** syntax sugar: **for(...) {}** support~~
1. ~~**while** syntax sugar: **foreach(...) {}** support~~
1. ~~Ternary operator support~~
1. ~~User defined structs~~
1. ~~User defined enums~~
1. ~~Postfix increment/decrement~~

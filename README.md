# **B**e**H**avior **L**anguage

![CI](https://github.com/bitdotgames/bhl/workflows/CI/badge.svg?branch=master&event=push)

> **bhl** is a strictly typed programming language specifically tailored for gameplay logic scripting. It combines Behaviour Trees(BT) primitives with familiar imperative style. 

First time it was presented at the [nucl.ai](https://nucl.ai/) conference in 2016. Here's the [presentation slides](https://docs.google.com/presentation/d/1As-bw3pY5pLij86j7nf_ycaG0Hb2EqnrwR3R8ID47sQ/edit?usp=sharing). 

Please note that bhl is in alpha state and currently targets only C# platform. Nonetheless it has been battle tested in the real world projects and heavily used by BIT.GAMES for mobile games development built with [Unity](https://unity.com/).

## bhl features

* [ANTLR](http://www.antlr.org/) based: C# frontend + C# interpreting backend
* Statically typed
* Built-in support for pseudo parallel code orchestration
* Basic types: *float, int, bool, string, enums, arrays, classes*
* Supports imperative style control constructs: *if/else, while, break, return*
* Allows user defined: *functions, lambdas, classes*
* Supports C# bindings to user types and functions
* Golang alike *defer*
* Passing arguments to function by *ref* like in C#
* Multiple returned values like in Golang
* Hot code reload
* Strict control over memory allocations 

## Quick example

```go
func GoToTarget(Unit u, Unit t) {
  NavPath path
  defer {
    PathRelease(path)
  }
  
  paral {
   yield while(!IsDead(u) && !IsDead(t) && !IsInRange(u, t))
   
   {
     path = FindPathTo(u, t)
     Wait(1)
   }
   
   {
     FollowPath(u, path)
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
u.InjectScript(func() {
  paral_all {
    PushBack(distance: distance)
    Stun(time: 0.4, intensity: 0.15)
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
func Attack(Unit u) {
  Unit t = TargetInRange(u)
  Check(t != null)
  paral_all {
   PlayAnim(u, trigger: "Attack")
   SoundPlay(u, sound: "Swoosh")
   seq {
     WaitAnimEvent(u, event: "Hit")
     SoundPlay(u, sound: "Damage")
     HitTarget(u, t, damage: RandRange(1,16))
  }
}
```

### Example of some unit's top behavior

```go
func Selector([]func bool() fns) {
  foreach(func bool() fn in fns) {
    if(!fn()) {
      continue
    } else {
      break
    }
  }
}

func UnitScript(Unit u) {
  while(true) {
    paral {
      WaitStateChanged(u)
      Selector(
            [
              func bool() { return FindTarget(u) },
              func bool() { return AttackTarget(u) },
              func bool() { return Idle(u) }
            ]
       )
    }
    yield()
  }
}
```

## Architecture

![bhl architecture](https://puu.sh/qEkYv/edf3b678aa.png)

bhl utilizes a standard interpreter architecture with a **frontend** and a **backend**. Frontend is responsible for reading input files, static type checking and bytecode generation. Binary bytecode is post-processed and optimized in a separate stage. Processed byte code can be used by the backend. Backend is a interpreter responsible for runtime bytecode evaluation. Backend can be nicely integrated with [Unity](https://unity.com/). 

### Frontend

In order to use the frontend you can use the **bhl** tool which ships with the code. See the quick build example below for instructions.  

### Backend

Before using the backend you have to compile the **bhl_back.dll** and somehow integrate it into your build pipeline. See the quick build example below for instructions.  

## Quick build example

Currently bhl assumes that you have [mono](http://www.mono-project.com/) installed and its binaries are in your PATH.

In the example directory you can find a simple illustration of gluing together **frontend** and **backend**. 

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

Please note that while bhl works fine under Windows the example assumes you are using \*nix platform.     

### Unity engine integration

The example script has also a special Unity compatibility mode. It illustrates how you can build a bhl backend dll (**bhl_back.dll**) for Unity. After that you can put it into Assets/Plugins directory and use bhl for your Unity game development. You can run the example script in this mode just as follows: 

> cd example && ./run.sh -unity

## Building

bhl comes with its own simple build tool **bhl**. bhl tool is written in C# and should work just fine both on \*nix and Windows platforms. 

It allows you to build frontend dll, backend dll, compile bhl sources into a binary, run unit tests etc. 

You can view all available build tasks with the following command:

> $ bhl help

## Tests

For now there is no any documentation for bhl except presentation slides. However, there are many [unit tests](test.cs) which cover all bhl features.

You can run unit tests by executing the following command:

> $ bhl test

# Roadmap

## Version 3.0
1. Generics support
2. More optimal byte code
3. More optimal runtime memory storage layout

## Version 2.0

1. ~~Byte code optimization~~
2. ~~More optimal executor (VM)~~
3. ~~Better runtime errors reporting~~
4. ~~More robust type system~~
5. ~~User class methods~~
6. ~~Interfaces support~~
7. ~~Namespaces support~~
8. ~~Virtual class methods~~
9. Static class members support
10. Maps support
11. Debugger support
12. LSP integration

## Version 1.0

1. ~~**ref** semantics similar to C#~~
2. ~~Generic functors support~~
3. ~~Generic initializers~~
4. ~~Multiple return values support~~
5. ~~**while** syntax sugar: **for(...) {}** support~~
6. ~~**while** syntax sugar: **foreach(...) {}** support~~
7. ~~Ternary operator support~~
8. ~~User defined structs~~
9. ~~User defined enums~~
10. ~~Postfix increment/decrement~~

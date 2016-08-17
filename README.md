# **B**e**H**avior **L**anguage

**bhl** is a programming language specifically tailored for Behavior Trees(BT) coding using familiar imperative style patterns. It was presented at the [nucl.ai](https://nucl.ai/) conference in 2016. Here's the [presentation slides](https://docs.google.com/presentation/d/1Q1wpy9M5XPmY6zU9Kjo2v9YiJQjrDBXdDZaSjcuh71s/edit?usp=sharing). 

Please note that bhl is in pre-alpha state. Nonetheless it has been battle tested in real world project and heavily used by BIT.GAMES for mobile games development.

## bhl features

* [ANTLR](http://www.antlr.org/) based: C# frontend + C# interpreting backend
* Statically typed
* Supports core BT building blocks: *seq, paral, paral_all, prio, not, forever, until_success, until_failure,* etc
* Basic types: *float, int, bool, string, enums, arrays, classes*
* Supports imperative style control constructs: *if/else, while, break, return*
* Allows user defined: *functions, lambdas, classes*
* Supports C# bindings to user types and functions
* Golang alike **defer**
* Hot code reload
* Strict control over memory allocations 

## Code sample

```go
func AlphaAppear(int id, float time_to_appear) {
  float time_start = time()
    paral {
      forever {
        float alpha = clamp01((time()-time_start)/time_to_appear)
          SetObjAlpha(id: id, alpha: alpha)
      }
      Wait(sec: time_to_appear)
    }
}
```
## Architecture

![bhl architecture](https://puu.sh/qEkYv/edf3b678aa.png)

## Quick build example

Currently bhl assumes that you have [mono](http://www.mono-project.com/) installed and its binaries are in your PATH.

In the example directory you can find a simple illustration of gluing together **frontend** and **backend**. Just try running *run.sh* script: 

> cd example && ./run.sh

> ...

> Hello, John Silver

> Hello, John Silver

> Hello, John Silver

> ...

Please note that while bhl works fine under Windows the example assumes you are using \*nix platform.     

## Building

bhl comes with its own simple build tool **bhl**. bhl tool is written in PHP and should work just fine both on \*nix and Windows platforms. 

It allows you to build frontend dll, backend dll, compile bhl sources into a binary, run unit tests etc. 

You can view all available build tasks with the following command:

> $ bhl help

## Tests

For now there is no any documentation for bhl except presentation slides. However, there are many unit tests in the **tests.cs** which cover almost all bhl features.

You can run unit tests by executing the following command:

> $ bhl test

# Some more code samples

## Imperative style only

```go
func Unit FindUnit(Vec3 pos, float radius) {
  Unit[] us = GetUnits()
    int i = 0
    while(i < us.Count) {
      Unit u = us.At(i)
        if(u.position.Sub(pos).len < radius) {
          return u
        } 
      i = i + 1
    }
  return null
}
```

## Lambda support

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

## **defer** support

```go
seq {
  RimColorSet(color: {r:  0.65, a: 1.0}, power: 1.1)
  defer { RimColorSet(color: {a: 0}, power: 0) }
     ... 
}
```

# Roadmap

## Version 0.1

1. **ref** semantics similar to C#
2. Multiple return values support
3. More generic functors support

## Version 1.0

1. Byte code optimization (switch to flatbuffers ?)
2. Ternary operator support
3. **while** syntax sugar: **for(...) {}** support
4. More optimal executor


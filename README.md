# *B*e*H*avior *L*anguage

BHL is specifically tailored for Behavior Trees(BT) programming using familiar imperative style constructs

## BHL features:

* ANTLR based: C# fronted(mono) + C# interpreting backend(Unity3d’s mono)
* Statically typed
* Supports core BT building blocks: *seq, paral, paral_all, prio, not, forever, until_success, until_failure,* etc
* Basic types: *float, int, bool, string, enums, arrays, classes*
* Supports imperative style control constructs: *if/else, while, break, return*
* Allows user defined: *functions, lambdas, classes*
* Supports C# bindings to user types and functions
* Golang alike *defer*
* Code hot reload
* Strict control over memory allocations 

## Code example:

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

## Mixing BT with imperative style:

```go
func ALPHA_APPEAR(int id, float time_to_appear) {
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

## Lambda support:

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

## *defer* support:

```go
seq {
  RimColorSet(color: {r:  0.65, a: 1.0}, power: 1.1)
  defer { RimColorSet(color: {a: 0}, power: 0) }
     ... 
}
```


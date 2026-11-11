using System;

namespace bhl
{

//NOTE: holds a native struct inside a Val's 'obj' slot, for structs that can't be encoded in
//      Val's numeric slots - anything holding a reference (those slots aren't GC scanned) or
//      simply too big for them.
//
//      Val.Clone() copies the numeric slots but shares 'obj', so a struct kept there would get
//      reference semantics: 'b = a' followed by a write to b would be visible through a. To
//      keep it behaving like a struct, a field write copies instead of mutating in place -
//      but only when somebody else can observe it. The refcount already answers that: at one
//      reference nothing else points here, so the write happens in place and costs nothing.
//
//      Bindings go through MutableRef()/Ref() rather than casting Val.obj themselves, so the
//      representation stays an implementation detail and the retain/release around a copy
//      lives in one place (Val.SetObj neither retains the new value nor releases the old).
public class ValHolder<T> : IRefcounted where T : struct
{
  public T v;

  internal VM vm;

  internal int _refs;
  public int refs => _refs;

  //NOTE: use New() instead
  internal ValHolder(VM vm)
  {
    this.vm = vm;
  }

  public static ValHolder<T> New(VM vm)
  {
    var pool = vm.GetHolderPool<T>();

    ValHolder<T> h;
    if(pool.stack.Count == 0)
    {
      ++pool.miss;
      h = new ValHolder<T>(vm);
    }
    else
    {
      ++pool.hits;
      h = pool.stack.Pop();

      if(h._refs != -1)
        throw new Exception("Expected to be released, refs " + h._refs);
    }

    h._refs = 1;
    h.v = default(T);

    return h;
  }

  static void Del(ValHolder<T> h)
  {
    if(h._refs != 0)
      throw new Exception("Freeing invalid object, refs " + h._refs);

    h._refs = -1;
    h.v = default(T);
    h.vm.GetHolderPool<T>().stack.Push(h);
  }

  public void Retain()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    ++_refs;
  }

  public void Release()
  {
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    if(_refs == 0)
      throw new Exception("Double free(0)");

    --_refs;
    if(_refs == 0)
      Del(this);
  }

  ///////////////////////////////////////

  public static Val NewVal(VM vm, IType type, T v)
  {
    var h = New(vm);
    h.v = v;
    return Val.NewObj(h, type);
  }

  //NOTE: reading never copies - the holder is a class, so this is a ref into the heap object
  public static ref T Ref(Val ctx)
  {
    return ref ((ValHolder<T>)ctx.obj).v;
  }

  //NOTE: hands back a struct nobody else observes. Shared means somebody could see the write,
  //      so it gets its own copy first; unshared writes go straight through
  public static ref T MutableRef(ref Val ctx)
  {
    var h = (ValHolder<T>)ctx.obj;

    if(h._refs == 1)
      return ref h.v;

    var fresh = New(h.vm);
    fresh.v = h.v;

    //NOTE: SetObj is 'this = NewObj(..)' - it neither retains what it's given nor releases
    //      what was there, so ctx's own reference has to be handed back by hand
    ctx.SetObj(fresh, ctx.type);
    h.Release();

    return ref fresh.v;
  }
}

}

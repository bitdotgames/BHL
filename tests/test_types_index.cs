using System;
using bhl;
using Xunit;

// Coverage for Types' tag keyed type index - see Types.GetIndexedType/IndexType
public class TestTypesIndex
{
  struct TagFoo {}
  struct TagBar {}
  struct TagNeverIndexed {}
  struct TagNeverFound {}

  static ClassSymbolNative MakeAndRegister(Types types, string module_name, string type_name)
  {
    var module = new ModuleDeclared(module_name);
    var ns = module.ns.Nest(module_name);
    var cl = new ClassSymbolNative(new Origin(), type_name, typeof(object));
    ns.Define(cl);
    cl.Setup();
    types.RegisterModule(module);
    return cl;
  }

  [Fact]
  public void IndexedTypeIsReadBackByItsTag()
  {
    var types = new Types();
    var foo = MakeAndRegister(types, "testmod", "Foo");
    types.IndexType<TagFoo>(foo);

    Assert.Same(foo, types.GetIndexedType<TagFoo>());
  }

  [Fact]
  public void DistinctTagsGetDistinctSlots()
  {
    var types = new Types();
    var foo = MakeAndRegister(types, "testmod_foo", "Foo");
    var bar = MakeAndRegister(types, "testmod_bar", "Bar");

    types.IndexType<TagFoo>(foo);
    types.IndexType<TagBar>(bar);

    Assert.Same(foo, types.GetIndexedType<TagFoo>());
    Assert.Same(bar, types.GetIndexedType<TagBar>());
  }

  //NOTE: the whole point - several Types coexist, their symbols aren't interchangeable
  [Fact]
  public void EachTypesKeepsItsOwnSymbolUnderTheSameTag()
  {
    var types1 = new Types();
    var foo1 = MakeAndRegister(types1, "testmod", "Foo");
    types1.IndexType<TagFoo>(foo1);

    var types2 = new Types();
    var foo2 = MakeAndRegister(types2, "testmod", "Foo");
    types2.IndexType<TagFoo>(foo2);

    Assert.NotSame(foo1, foo2);
    Assert.Same(foo1, types1.GetIndexedType<TagFoo>());
    Assert.Same(foo2, types2.GetIndexedType<TagFoo>());
  }

  //NOTE: no bounds check on Get - an unsure caller uses FindIndexedType
  [Fact]
  public void NeverIndexedTagThrows()
  {
    var types = new Types();
    Assert.Throws<IndexOutOfRangeException>(() => types.GetIndexedType<TagNeverIndexed>());
  }

  [Fact]
  public void FindIndexedTypeIsNullForANeverIndexedTag()
  {
    var types = new Types();
    Assert.Null(types.FindIndexedType<TagNeverFound>());
  }

  [Fact]
  public void FindIndexedTypeMatchesGetWhenIndexed()
  {
    var types = new Types();
    var foo = MakeAndRegister(types, "testmod_find", "Foo");
    types.IndexType<TagFoo>(foo);

    Assert.Same(foo, types.FindIndexedType<TagFoo>());
    Assert.Same(types.GetIndexedType<TagFoo>(), types.FindIndexedType<TagFoo>());
  }

  [Fact]
  public void IndexTypeOverwritesTheSameTag()
  {
    var types = new Types();
    var first = MakeAndRegister(types, "testmod_first", "Foo");
    var second = MakeAndRegister(types, "testmod_second", "Foo");

    types.IndexType<TagFoo>(first);
    types.IndexType<TagFoo>(second);

    Assert.Same(second, types.GetIndexedType<TagFoo>());
  }

  struct TagGrowA {}
  struct TagGrowB {}
  struct TagRevA {}
  struct TagRevB {}

  //NOTE: an earlier tag's slot must survive the resize
  [Fact]
  public void GrowingForALaterTagKeepsTheEarlierOne()
  {
    var types = new Types();
    var a = MakeAndRegister(types, "testmod_grow_a", "A");
    var b = MakeAndRegister(types, "testmod_grow_b", "B");

    //NOTE: first touch allocates, so TagGrowA's index is below TagGrowB's
    types.IndexType<TagGrowA>(a);
    types.IndexType<TagGrowB>(b);

    Assert.Same(a, types.GetIndexedType<TagGrowA>());
    Assert.Same(b, types.GetIndexedType<TagGrowB>());
  }

  //NOTE: the reverse - the higher tag sizes the array, the lower takes the no-resize branch
  [Fact]
  public void IndexingHigherTagFirstStillAcceptsALowerOne()
  {
    var types = new Types();
    var a = MakeAndRegister(types, "testmod_rev_a", "A");
    var b = MakeAndRegister(types, "testmod_rev_b", "B");

    //NOTE: allocates both indices in this order, so TagRevA's is below TagRevB's
    var warmup = new Types();
    warmup.IndexType<TagRevA>(a);
    warmup.IndexType<TagRevB>(b);

    types.IndexType<TagRevB>(b);
    types.IndexType<TagRevA>(a);

    Assert.Same(a, types.GetIndexedType<TagRevA>());
    Assert.Same(b, types.GetIndexedType<TagRevB>());
  }

  [Fact]
  public void NextTypeIndexHandsOutDistinctIndices()
  {
    int a = Types.NextTypeIndex();
    int b = Types.NextTypeIndex();
    Assert.NotEqual(a, b);
  }
}

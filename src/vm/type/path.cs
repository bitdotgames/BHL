using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using bhl.marshall;

namespace bhl
{

// Can be a simple name like 'Foo' but also a fully specified path like 'foo.bar.Cat', or '[]ecs.Item'
public struct NamePath : IEnumerable<string>, IEquatable<NamePath>, IMarshallable
{
  StackList<string> items;

  public int Count => items.Count;

  public string this[int i]
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get { return items[i]; }
    set { items[i] = value; }
  }

  public static implicit operator NamePath(string path)
  {
    return new NamePath(path);
  }

  public NamePath(string path)
  {
    items = default;

    if(path.IndexOf('.') != -1)
    {
      foreach(var item in path.Split('.'))
        Add(item);
    }
    else
      Add(path);
  }

  public override string ToString()
  {
    return Count == 1 ? this[0] : string.Join('.', this);
  }

  public void Sync(SyncContext ctx)
  {
    int count = Count;
    Marshall.Sync(ctx, ref count);
    for(int i = 0; i < count; ++i)
    {
      string item = ctx.is_read ? "" : this[i];
      Marshall.Sync(ctx, ref item);
      if(ctx.is_read)
        Add(item);
    }
  }

  public override bool Equals(object o)
  {
    if(!(o is NamePath))
      return false;
    return this.Equals((NamePath)o);
  }

  public bool Equals(NamePath o)
  {
    if(o.Count != Count)
      return false;

    for(int i = 0; i < o.Count; ++i)
      if(this[i] != o[i])
        return false;

    return true;
  }

  public override int GetHashCode()
  {
    int hc = 0;
    for(int i = 0; i < Count; ++i)
      hc ^= this[i].GetHashCode();
    return hc;
  }

  public IEnumerator<string> GetEnumerator()
  {
    return items.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(string name)
  {
    items.Add(name);
  }

  public void Clear()
  {
    items.Clear();
  }
}

}
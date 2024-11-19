using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using bhl.marshall;

namespace bhl {
    
public struct TypePath : IEnumerable<string>, IEquatable<TypePath>, IMarshallable
{
  StackList<string> items;

  public int Count => items.Count;

  public string this[int i]
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get { return items[i]; }
    set { items[i] = value; }
  }
  
  public static implicit operator TypePath(string path)
  {
    return new TypePath(path);
  }
  
  public TypePath(string path)
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
    return GetFullPath();
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
    if(!(o is TypePath))
      return false;
    return this.Equals((TypePath)o);
  }

  public bool Equals(TypePath o)
  {
    if(o.Count != Count)
      return false;
    
    for(int i=0; i<o.Count; ++i)
      if(this[i] != o[i])
        return false;

    return true;
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

  public string GetFullPath()
  {
    return string.Join('.', this);
  }
}

}
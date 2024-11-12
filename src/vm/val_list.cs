using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {
  
//TODO: implement IList as well?
public class ValList : IList<Val>, IValRefcounted
{
  //NOTE: Exposed to allow low-level optimal manipulations. Use with caution.
  public List<Val> lst = new List<Val>();

  //NOTE: -1 means it's in released state,
  //      public only for quick inspection
  public int _refs;

  public int refs => _refs; 

  public VM vm;

  //////////////////IList//////////////////

  public int Count { get { return lst.Count; } }

  public bool IsFixedSize { get { return false; } }
  public bool IsReadOnly { get { return false; } }
  public bool IsSynchronized { get { throw new NotImplementedException(); } }
  public object SyncRoot { get { throw new NotImplementedException(); } }

  public void Add(Val dv)
  {
    //NOTE: we need to make a copy of the passed Val since
    //      it can be a locally cleared afterwards variable and the same
    //      time we need to increase the user payload refs counter
    lst.Add(dv.CloneValue());
  }

  public void AddRange(IList<Val> list)
  {
    for(int i=0; i<list.Count; ++i)
      Add(list[i]);
  }

  public void RemoveAt(int idx)
  {
    var dv = lst[idx];
    dv.RefMod(RefOp.DEC | RefOp.USR_DEC);
    lst.RemoveAt(idx); 
  }

  public void Clear()
  {
    for(int i=0;i<Count;++i)
      lst[i].RefMod(RefOp.DEC | RefOp.USR_DEC);

    lst.Clear();
  }

  public Val this[int i]
  {
    get {
      return lst[i];
    }
    set {
      var curr = lst[i];
      //NOTE: we are going to re-use the existing Value,
      //      thus we need to decrease/increase user payload
      //      refcounts properly 
      curr.RefMod(RefOp.USR_DEC);
      curr.ValueCopyFrom(value);
      curr.RefMod(RefOp.USR_INC);
    }
  }

  public int IndexOf(Val dv)
  {
    return lst.IndexOf(dv);
  }

  public bool Contains(Val dv)
  {
    return IndexOf(dv) >= 0;
  }

  public bool Remove(Val dv)
  {
    int idx = IndexOf(dv);
    if(idx < 0)
      return false;
    RemoveAt(idx);
    return true;
  }

  public void CopyTo(Val[] arr, int len)
  {
    throw new NotImplementedException();
  }

  public void Insert(int pos, Val dv)
  {
    lst.Insert(pos, dv.CloneValue());
  }

  public IEnumerator<Val> GetEnumerator()
  {
    return lst.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  ///////////////////////////////////////

  public void Retain()
  {
    //Console.WriteLine("== RETAIN " + refs + " " + GetHashCode() + " " + Environment.StackTrace);
    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    ++_refs;
  }

  public void Release()
  {
    //Console.WriteLine("== RELEASE " + refs + " " + GetHashCode() + " " + Environment.StackTrace);

    if(_refs == -1)
      throw new Exception("Invalid state(-1)");
    if(_refs == 0)
      throw new Exception("Double free(0)");

    --_refs;
    if(_refs == 0)
      Del(this);
  }

  ///////////////////////////////////////

  public void CopyFrom(ValList lst)
  {
    Clear();
    for(int i=0;i<lst.Count;++i)
      Add(lst[i]);
  }

  ///////////////////////////////////////

  //NOTE: use New() instead
  internal ValList(VM vm)
  {
    this.vm = vm;
  }

  public static ValList New(VM vm)
  {
    ValList lst;
    if(vm.vlsts_pool.stack.Count == 0)
    {
      ++vm.vlsts_pool.miss;
      lst = new ValList(vm);
    }
    else
    {
      ++vm.vlsts_pool.hits;
      lst = vm.vlsts_pool.stack.Pop();

      if(lst._refs != -1)
        throw new Exception("Expected to be released, refs " + lst._refs);
    }
    lst._refs = 1;

    return lst;
  }

  static void Del(ValList lst)
  {
    if(lst._refs != 0)
      throw new Exception("Freeing invalid object, refs " + lst._refs);

    lst._refs = -1;
    lst.Clear();
    lst.vm.vlsts_pool.stack.Push(lst);

    if(lst.vm.vlsts_pool.stack.Count > lst.vm.vlsts_pool.miss)
      throw new Exception("Unbalanced New/Del");
  }
}

}

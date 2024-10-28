
using System.Runtime.CompilerServices;
using System;

namespace bhl {
  
public class FixedStack<T>
{
  internal T[] storage;
  internal int head = 0;

  public int Count
  {
    get { return head; }
  }

  public FixedStack(int max_capacity)
  {
    storage = new T[max_capacity];
  }

  public T this[int index]
  {
    get { 
      ValidateIndex(index);
      return storage[index]; 
    }
    set { 
      ValidateIndex(index);
      storage[index] = value; 
    }
  }

  //let's validate index during parsing phase only
  [System.Diagnostics.Conditional("BHL_FRONT")]
  void ValidateIndex(int index)
  {
    if(index < 0 || index >= head)
      throw new Exception("Out of bounds: " + index + " vs " + head);
  }

  public bool TryGetAt(int index, out T v)
  {
    if(index < 0 || index >= storage.Length)
    {
      v = default(T);
      return false;
    }
    
    v = storage[index];
    return true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Push(T item)
  {
    storage[head++] = item;
    return item;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Pop()
  {
    return storage[--head];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Pop(T repl)
  {
    --head;
    T retval = storage[head];
    storage[head] = repl;
    return retval;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Peek()
  {
    return storage[head - 1];
  }

  public void SetRelative(int offset, T item)
  {
    storage[head - 1 - offset] = item;
  }

  public void RemoveAt(int idx)
  {
    if(idx == (head-1))
    {
      Dec();
    }
    else
    {
      --head;
      Array.Copy(storage, idx+1, storage, idx, storage.Length-idx-1);
    }
  }

  public void MoveTo(int src, int dst)
  {
    if(src == dst)
      return;

    var tmp = storage[src];

    if(src > dst)
      Array.Copy(storage, dst, storage, dst + 1, src - dst);
    else
      Array.Copy(storage, src + 1, storage, src, dst - src);

    storage[dst] = tmp;
  }

  public void Dec()
  {
    --head;
  }

  public void Resize(int pos)
  {
    head = pos;
  }

  public void Clear()
  {
    Array.Clear(storage, 0, storage.Length);
    head = 0;
  }
}

}

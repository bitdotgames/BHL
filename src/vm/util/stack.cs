using System.Runtime.CompilerServices;
using System;

namespace bhl
{

public class FixedStack<T>
{
  internal T[] storage;
  public int Count = 0;

  public FixedStack(int max_capacity)
  {
    storage = new T[max_capacity];
  }

  public ref T this[int index]
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
      ValidateIndex(index);
      return ref storage[index];
    }
  }

  //let's validate index during parsing phase only
  [System.Diagnostics.Conditional("BHL_FRONT")]
  void ValidateIndex(int index)
  {
    if(index < 0 || index >= Count)
      throw new Exception("Out of bounds: " + index + " vs " + Count);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Push(T item)
  {
    storage[Count++] = item;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Push()
  {
    return ref storage[Count++];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Pop()
  {
    return ref storage[--Count];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Peek()
  {
    return ref storage[Count - 1];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RemoveAt(int idx)
  {
    if(idx == --Count)
      return;
    Array.Copy(storage, idx + 1, storage, idx, storage.Length - idx - 1);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Clear()
  {
    Array.Clear(storage, 0, storage.Length);
    Count = 0;
  }
}

}

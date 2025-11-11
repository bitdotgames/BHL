using System.Runtime.CompilerServices;
using System;

namespace bhl
{

public class StackArray<T>
{
  public T[] Values;
  public int Count;

  public StackArray(int init_size = 32)
  {
    Values = new T[init_size];
    Count = 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Push()
  {
    if(Count == Values.Length)
      Array.Resize(ref Values, Count << 1);

    return ref Values[Count++];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Pop()
  {
    return ref Values[--Count];
  }
}

public class FixedStack<T>
{
  public T[] Values;
  public int Count = 0;

  public FixedStack(int max_capacity)
  {
    Values = new T[max_capacity];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Push(T item)
  {
    Values[Count++] = item;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Push()
  {
    return ref Values[Count++];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Pop()
  {
    return ref Values[--Count];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ref T Peek()
  {
    return ref Values[Count - 1];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RemoveAt(int idx)
  {
    if(idx == --Count)
      return;
    Array.Copy(Values, idx + 1, Values, idx, Values.Length - idx - 1);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Clear()
  {
    Array.Clear(Values, 0, Values.Length);
    Count = 0;
  }
}

}

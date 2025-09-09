using System.Collections.Generic;

namespace bhl
{

public class Pool<T> where T : class
{
  internal Stack<T> stack = new Stack<T>();
  internal int hits;
  internal int miss;

  public int HitCount
  {
    get { return hits; }
  }

  public int MissCount
  {
    get { return miss; }
  }

  public int IdleCount
  {
    get { return stack.Count; }
  }

  public int BusyCount
  {
    get { return miss - IdleCount; }
  }
}

}
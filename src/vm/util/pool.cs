
namespace bhl
{

public class Pool<T> where T : class
{
  internal StackArray<T> stack;
  internal int hits;
  internal int miss;

  //for inspection
  public StackArray<T> Stack => stack;
  public int HitCount => hits;
  public int MissCount => miss;
  public int IdleCount => stack.Count;
  public int BusyCount => miss - IdleCount;

  public Pool(int capacity)
  {
    stack = new StackArray<T>(capacity);
  }
}

}

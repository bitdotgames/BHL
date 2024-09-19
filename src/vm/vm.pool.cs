using System.Collections.Generic;

namespace bhl {
 
public partial class VM : INamedResolver
{
  public class Pool<T> where T : class
  {
    internal Stack<T> stack = new Stack<T>();
    internal int hits;
    internal int miss;

    public int HitCount {
      get { return hits; }
    }

    public int MissCount {
      get { return miss; }
    }

    public int IdleCount {
      get { return stack.Count; }
    }

    public int BusyCount {
      get { return miss - IdleCount; }
    }
  }
  
  public class ValPool : Pool<Val>
  {
    public void Alloc(VM vm, int num)
    {
      for(int i=0;i<num;++i)
      {
        ++miss;
        var tmp = new Val(vm);
        stack.Push(tmp);
      }
    }

    public string Dump()
    {
      string res = "=== Val POOL ===\n";
      res += "busy:" + BusyCount + " idle:" + IdleCount + "\n";

      var dvs = new Val[stack.Count];
      stack.CopyTo(dvs, 0);
      for(int i=dvs.Length;i-- > 0;)
      {
        var v = dvs[i];
        res += v + " (refs:" + v._refs + ") " + v.GetHashCode() + "\n";
      }

      return res;
    }
  }

  public ValPool vals_pool = new ValPool();
  public Pool<ValList> vlsts_pool = new Pool<ValList>();
  public Pool<ValMap> vmaps_pool = new Pool<ValMap>();
  public Pool<Frame> frames_pool = new Pool<Frame>();
  public Pool<Fiber> fibers_pool = new Pool<Fiber>();
  public Pool<FuncPtr> fptrs_pool = new Pool<FuncPtr>();
  public CoroutinePool coro_pool = new CoroutinePool();
}

}

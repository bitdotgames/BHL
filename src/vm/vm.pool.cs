namespace bhl
{

public partial class VM : INamedResolver
{
  public Pool<ValRef> vrefs_pool = new Pool<ValRef>(32);
  public Pool<ValList> vlsts_pool = new Pool<ValList>(32);
  public Pool<ValMap> vmaps_pool = new Pool<ValMap>(32);
  public Pool<Fiber> fibers_pool = new Pool<Fiber>(32);
  public Pool<FuncPtr> fptrs_pool = new Pool<FuncPtr>(32);
  public CoroutinePool coro_pool = new CoroutinePool();

  //NOTE: one pool per held struct type, created on first use. Per VM rather than static, so
  //      two VMs ticking on different threads don't share a stack
  System.Collections.Generic.Dictionary<System.Type, object> holder_pools =
    new System.Collections.Generic.Dictionary<System.Type, object>();

  public Pool<ValHolder<T>> GetHolderPool<T>() where T : struct
  {
    object pool;
    if(!holder_pools.TryGetValue(typeof(T), out pool))
    {
      pool = new Pool<ValHolder<T>>(32);
      holder_pools.Add(typeof(T), pool);
    }
    return (Pool<ValHolder<T>>)pool;
  }
}

}

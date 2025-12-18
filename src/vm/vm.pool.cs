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
}

}

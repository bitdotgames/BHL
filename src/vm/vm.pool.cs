namespace bhl
{

public partial class VM : INamedResolver
{
  public Pool<ValOld> vals_pool = new Pool<ValOld>();
  public Pool<ValRef> vrefs_pool = new Pool<ValRef>();
  public Pool<ValList> vlsts_pool = new Pool<ValList>();
  public Pool<ValMap> vmaps_pool = new Pool<ValMap>();
  public Pool<Fiber> fibers_pool = new Pool<Fiber>();
  public Pool<FuncPtr> fptrs_pool = new Pool<FuncPtr>();
  public CoroutinePool coro_pool = new CoroutinePool();
}

}

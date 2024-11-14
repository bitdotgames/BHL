using System;
using System.Collections.Generic;

namespace bhl {

 public class PoolList<T> : List<T>, IValRefcounted
 {
   internal VM.Pool<PoolList<T>> pool;
   
   //NOTE: -1 means it's in released state,
   //      public only for quick inspection
   internal int _refs;
   
   public int refs => _refs; 
   
   static class PoolHolder<T1>
   {
     public static System.Threading.ThreadLocal<VM.Pool<PoolList<T1>>> pool =
       new System.Threading.ThreadLocal<VM.Pool<PoolList<T1>>>(() =>
       {
         return new VM.Pool<PoolList<T1>>();
       });
   }

   static public PoolList<T> New()
   {
     var pool = PoolHolder<T>.pool.Value;

     PoolList<T> list = null;
     if(pool.stack.Count == 0)
     {
       ++pool.miss;
       list = new PoolList<T>();
     }
     else
     {
       ++pool.hits;
       list = pool.stack.Pop();
     }

     list._refs = 1;
     list.pool = pool;

     return list;
   }
   
   public void Retain()
   {
     if(_refs == -1)
       throw new Exception("Invalid state(-1)");
     ++_refs;
   }

   public void Release()
   {
     if(_refs == -1)
       throw new Exception("Invalid state(-1)");
     if(_refs == 0)
       throw new Exception("Double free(0)");

     --_refs;
     if(_refs == 0)
       Del(this);
   }
   
   static void Del(PoolList<T> lst)
   {
     if(lst._refs != 0)
       throw new Exception("Freeing invalid object, refs " + lst._refs);

     lst._refs = -1;
     lst.Clear();
     
     lst.pool.stack.Push(lst);
   } 
}
    
}

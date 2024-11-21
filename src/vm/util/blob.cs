using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace bhl {

public class Blob<T> : IValRefcounted where T : unmanaged
{
   internal Pool<Blob<T>> pool;
   
   //NOTE: -1 means it's in released state,
   //      public only for quick inspection
   internal int _refs;
   
   public int refs => _refs;

   internal byte[] data;

   static int Size = Marshal.SizeOf<T>();

   static class PoolHolder<T1> where T1 : unmanaged
   {
     public static System.Threading.ThreadLocal<Pool<Blob<T1>>> pool =
       new System.Threading.ThreadLocal<Pool<Blob<T1>>>(() =>
       {
         return new Pool<Blob<T1>>();
       });
   }
   
   internal Blob()
   {}
   
   static public Blob<T> New(ref T val)
   {
     var pool = PoolHolder<T>.pool.Value;

     Blob<T> blob = null;
     if(pool.stack.Count == 0)
     {
       ++pool.miss;
       blob = new Blob<T>();
       blob.data = new byte[Size];
     }
     else
     {
       ++pool.hits;
       blob = pool.stack.Pop();
     }

     Unsafe.As<byte, T>(ref blob.data[0]) = val;

     blob._refs = 1;
     blob.pool = pool;

     return blob;
   }

   public ref T Value => ref Unsafe.As<byte, T>(ref data[0]);

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
   
   static void Del(Blob<T> blob)
   {
     if(blob._refs != 0)
       throw new Exception("Freeing invalid object, refs " + blob._refs);

     blob._refs = -1;
     
     blob.pool.stack.Push(blob);
   } 
}

}

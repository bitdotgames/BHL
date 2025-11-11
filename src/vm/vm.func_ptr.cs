using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace bhl
{

public partial class VM : INamedResolver
{
  public class FuncPtr : IRefcounted
  {
    //NOTE: -1 means it's in released state,
    //      public only for quick inspection
    public int _refs;

    public int refs => _refs;

    public VM vm;

    public Module module;
    public int func_ip;
    public FuncSymbolNative native;

    public struct UpVal
    {
      public Val val;
      public int frame_local_idx;
    }

    public StackArray<UpVal> upvals = new StackArray<UpVal>(8);

    public FuncAddr func_addr
    {
      get { return new FuncAddr() { module = module, ip = func_ip, fsn = native }; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public FuncPtr New(VM vm)
    {
      FuncPtr ptr;
      if(vm.fptrs_pool.stack.Count == 0)
      {
        ++vm.fptrs_pool.miss;
        ptr = new FuncPtr(vm);
      }
      else
      {
        ++vm.fptrs_pool.hits;
        ptr = vm.fptrs_pool.stack.Pop();

        if(ptr._refs != -1)
          throw new Exception("Expected to be released, refs " + ptr._refs);
      }

      ptr._refs = 1;

      return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Del(FuncPtr ptr)
    {
      if(ptr._refs != 0)
        throw new Exception("Freeing invalid object, refs " + ptr._refs);

      ptr._refs = -1;

      ptr.Clear();
      ptr.vm.fptrs_pool.stack.Push(ptr);
    }

    //NOTE: use New() instead
    internal FuncPtr(VM vm)
    {
      this.vm = vm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(Module module, int func_ip)
    {
      this.module = module;
      this.func_ip = func_ip;
      this.native = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(FuncSymbolNative native)
    {
      this.module = null;
      this.func_ip = -1;
      this.native = native;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(FuncAddr addr)
    {
      this.module = addr.module;
      this.func_ip = addr.ip;
      this.native = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Clear()
    {
      this.module = null;
      this.func_ip = -1;
      this.native = null;
      for(int i = upvals.Count; i-- > 0;)
        upvals.Values[i].val._refc?.Release();
      upvals.Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
      if(_refs == -1)
        throw new Exception("Invalid state(-1)");
      ++_refs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InitFrame(VM.ExecState exec, ref Frame origin_frame, ref Frame frame)
    {
      if(native != null)
      {
        frame.InitWithOrigin(origin_frame, VM.EXIT_FRAME_IP);
      }
      else
      {
        frame.InitWithModule(module, func_ip);

        for(int i = 0; i < upvals.Count; ++i)
        {
          ref var upval = ref upvals.Values[i];
          int local_idx = exec.stack.sp - frame.args_info.CountArgs() + upval.frame_local_idx;
          exec.stack.Reserve(local_idx + 1);
          //NOTE: we reserve space but don't increase stack, changing values directly,
          //      this way we don't interfere with passed arguments
          ref var local_var = ref exec.stack.vals[local_idx];
          local_var = upval.val;
          local_var._refc?.Retain();
        }
      }
    }

    public override string ToString()
    {
      return "(FPTR refs:" + _refs + ",upvals:" + upvals.Count + " " + this.GetHashCode() + ")";
    }
  }
}

}

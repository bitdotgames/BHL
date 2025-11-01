using System;
using System.Collections.Generic;

namespace bhl
{

public partial class VM : INamedResolver
{
  public class FuncPtr : IValRefcounted
  {
    //NOTE: -1 means it's in released state,
    //      public only for quick inspection
    public int _refs;

    public int refs => _refs;

    public VM vm;

    public Module module;
    public int func_ip;
    public FuncSymbolNative native;
    public ValOldStack upvals = new ValOldStack(FrameOld.MAX_LOCALS);

    public FuncAddr func_addr
    {
      get { return new FuncAddr() { module = module, ip = func_ip, fsn = native }; }
    }

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

    static void Del(FuncPtr ptr)
    {
      if(ptr._refs != 0)
        throw new Exception("Freeing invalid object, refs " + ptr._refs);

      //Console.WriteLine("DEL " + ptr.GetHashCode() + " " + Environment.StackTrace);
      ptr._refs = -1;

      ptr.Clear();
      ptr.vm.fptrs_pool.stack.Push(ptr);
    }

    //NOTE: use New() instead
    internal FuncPtr(VM vm)
    {
      this.vm = vm;
    }

    public void Init(Module module, int func_ip)
    {
      this.module = module;
      this.func_ip = func_ip;
      this.native = null;
    }

    public void Init(FuncSymbolNative native)
    {
      this.module = null;
      this.func_ip = -1;
      this.native = native;
    }

    public void Init(FuncAddr addr)
    {
      this.module = addr.module;
      this.func_ip = addr.ip;
      this.native = null;
    }

    void Clear()
    {
      this.module = null;
      this.func_ip = -1;
      this.native = null;
      for(int i = upvals.Count; i-- > 0;)
      {
        //NOTE: let's check if it exists
        upvals[i]?.Release();
      }

      upvals.Clear();
    }

    public void Retain()
    {
      //Console.WriteLine("RTN " + GetHashCode() + " " + Environment.StackTrace);

      if(_refs == -1)
        throw new Exception("Invalid state(-1)");
      ++_refs;
    }

    public void Release()
    {
      //Console.WriteLine("REL " + GetHashCode() + " " + Environment.StackTrace);

      if(_refs == -1)
        throw new Exception("Invalid state(-1)");
      if(_refs == 0)
        throw new Exception("Double free(0)");

      --_refs;
      if(_refs == 0)
        Del(this);
    }

    public FrameOld MakeFrameOld(VM vm, Fiber fb, ValOldStack return_stack)
    {
      var frm = FrameOld.New(vm);

      if(native != null)
      {
        frm.Init(fb, return_stack, null, null, null, null, VM.EXIT_FRAME_IP);
      }
      else
      {
        frm.Init(fb, return_stack, module, func_ip);

        for(int i = 0; i < upvals.Count; ++i)
        {
          var upval = upvals[i];
          if(upval != null)
          {
            frm.locals.Count = i + 1;
            upval.Retain();
            frm.locals[i] = upval;
          }
        }
      }

      return frm;
    }

    public void InitFrame(VM.ExecState exec, ref Frame origin_frame, ref Frame frame)
    {
      if(native != null)
      {
        frame.Init(origin_frame, VM.EXIT_FRAME_IP);
      }
      else
      {
        frame.Init(module, func_ip);

        for(int i = 0; i < upvals.Count; ++i)
        {
          throw new NotImplementedException();
          //var upval = upvals[i];
          //if(upval != null)
          //{
          //  frm.locals.Count = i + 1;
          //  upval.Retain();
          //  frm.locals[i] = upval;
          //}
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

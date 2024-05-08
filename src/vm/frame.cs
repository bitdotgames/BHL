using System;
using System.Collections.Generic;

namespace bhl {

public partial class VM : INamedResolver
{
  public class Frame : IDeferSupport
  {
    public const int MAX_LOCALS = 64;
    public const int MAX_STACK = 32;
    
    //NOTE: -1 means it's in released state,
    //      public only for inspection
    public int refs;

    public VM vm;
    public Fiber fb;
    public Module module;

    public byte[] bytecode;
    public List<Const> constants;
    public ValStack locals = new ValStack(MAX_LOCALS);
    public ValStack _stack = new ValStack(MAX_STACK);
    public int start_ip;
    public int return_ip;
    public Frame origin;
    public ValStack origin_stack;
    public List<DeferBlock> defers;

    static public Frame New(VM vm)
    {
      Frame frm;
      if(vm.frames_pool.stack.Count == 0)
      {
        ++vm.frames_pool.miss;
        frm = new Frame(vm);
      }
      else
      {
        ++vm.frames_pool.hits;
        frm = vm.frames_pool.stack.Pop();

        if(frm.refs != -1)
          throw new Exception("Expected to be released, refs " + frm.refs);
      }

      frm.refs = 1;

      return frm;
    }

    static void Del(Frame frm)
    {
      if(frm.refs != 0)
        throw new Exception("Freeing invalid object, refs " + frm.refs);

      //Console.WriteLine("DEL " + frm.GetHashCode() + " "/* + Environment.StackTrace*/);
      frm.refs = -1;

      frm.Clear();
      frm.vm.frames_pool.stack.Push(frm);
    }

    //NOTE: use New() instead
    internal Frame(VM vm)
    {
      this.vm = vm;
    }

    public void Init(Frame origin, ValStack origin_stack, int start_ip)
    {
      Init(
        origin.fb, 
        origin,
        origin_stack,
        origin.module, 
        origin.constants, 
        origin.bytecode, 
        start_ip
      );
    }

    public void Init(Fiber fb, Frame origin, ValStack origin_stack, Module module, int start_ip)
    {
      Init(
        fb, 
        origin,
        origin_stack,
        module, 
        module.compiled.constants, 
        module.compiled.bytecode, 
        start_ip
      );
    }

    internal void Init(Fiber fb, Frame origin, ValStack origin_stack, Module module, List<Const> constants, byte[] bytecode, int start_ip)
    {
      this.fb = fb;
      this.origin = origin;
      this.origin_stack = origin_stack;
      this.module = module;
      this.constants = constants;
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      this.return_ip = -1;
    }

    internal void Clear()
    {
      for(int i=locals.Count;i-- > 0;)
      {
        var val = locals[i];
        if(val != null)
          val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      locals.Clear();

      for(int i=_stack.Count;i-- > 0;)
      {
        var val = _stack[i];
        val.RefMod(RefOp.DEC | RefOp.USR_DEC);
      }
      _stack.Clear();

      if(defers != null)
        defers.Clear();
    }

    public void RegisterDefer(DeferBlock dfb)
    {
      if(defers == null)
        defers = new List<DeferBlock>();

      defers.Add(dfb);
      //for debug
      //if(dfb.frm != this)
      //  throw new Exception("INVALID DEFER BLOCK: mine " + GetHashCode() + ", other " + dfb.frm.GetHashCode() + " " + fb.GetStackTrace());
    }

    public void ExitScope(VM.Frame _, ExecState exec)
    {
      DeferBlock.ExitScope(defers, exec);
    }

    public void Retain()
    {
      //Console.WriteLine("RTN " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

    public void Release()
    {
      //Console.WriteLine("REL " + refs + " " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      if(refs == 0)
        throw new Exception("Double free(0)");

      --refs;
      if(refs == 0)
        Del(this);
    }
  }
}

}

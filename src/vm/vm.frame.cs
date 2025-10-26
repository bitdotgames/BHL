using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public partial class VM : INamedResolver
{
  public class FrameOld
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
    public Const[] constants;
    public IType[] type_refs;
    public ValOldStack locals = new ValOldStack(MAX_LOCALS);
    public ValOldStack stack = new ValOldStack(MAX_STACK);
    public int start_ip;
    public int return_ip;
    public ValOldStack return_stack;
    public List<DeferBlock> defers = new List<DeferBlock>(2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public FrameOld New(VM vm)
    {
      FrameOld frm;
      if(vm.frames_pool.stack.Count == 0)
      {
        ++vm.frames_pool.miss;
        frm = new FrameOld(vm);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Del(FrameOld frm)
    {
      if(frm.refs != 0)
        throw new Exception("Freeing invalid object, refs " + frm.refs);

      //Console.WriteLine("DEL " + frm.GetHashCode() + " "/* + Environment.StackTrace*/);
      frm.refs = -1;

      frm.Clear();
      frm.vm.frames_pool.stack.Push(frm);
    }

    //NOTE: use New() instead
    internal FrameOld(VM vm)
    {
      this.vm = vm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(FrameOld origin, ValOldStack return_stack, int start_ip)
    {
      Init(
        origin.fb,
        return_stack,
        origin.module,
        origin.constants,
        origin.type_refs,
        origin.bytecode,
        start_ip
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(Fiber fb, ValOldStack return_stack, Module module, int start_ip)
    {
      Init(
        fb,
        return_stack,
        module,
        module.compiled.constants,
        module.compiled.type_refs_resolved,
        module.compiled.bytecode,
        start_ip
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Init(
      Fiber fb,
      ValOldStack return_stack,
      Module module,
      Const[] constants,
      IType[] type_refs,
      byte[] bytecode,
      int start_ip)
    {
      this.fb = fb;
      this.return_stack = return_stack;
      this.module = module;
      this.constants = constants;
      this.type_refs = type_refs;
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      this.return_ip = -1;
    }

    internal void Clear()
    {
      for(int i = locals.Count; i-- > 0;)
        locals[i]?.Release();
      locals.Clear();

      for(int i = stack.Count; i-- > 0;)
        stack[i].Release();

      stack.Clear();
      defers.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitScope(VM.FrameOld _, ExecState exec)
    {
      DeferBlock.ExitScopeOld(defers, exec);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
      //Console.WriteLine("RTN " + GetHashCode() + " " + Environment.StackTrace);

      if(refs == -1)
        throw new Exception("Invalid state(-1)");
      ++refs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

  public struct Frame
  {
    public Module module;
    public unsafe byte* bytecode;
    public Const[] constants;
    public IType[] type_refs;
    public int start_ip;
    public int return_ip;

    public uint args_bits;
    public int locals_idx;

    //TODO:
    //public DeferBlock[] defers;
    //public int defers_count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init(ref Frame frame)
    {
      unsafe
      {
        bytecode = frame.bytecode;
      }
      constants = frame.constants;
      type_refs = frame.type_refs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Init(/*Fiber fb*/ Module module, int start_ip)
    {
      Init(
        //fb,
        module,
        module.compiled.constants,
        module.compiled.type_refs_resolved,
        module.compiled.bytecode_ptr,
        start_ip
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Init(Frame origin, int start_ip)
    {
      Init(
        //origin.fb,
        origin.module,
        origin.constants,
        origin.type_refs,
        origin.bytecode,
        start_ip
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe void Init(
      //Fiber fb,
      Module module,
      Const[] constants,
      IType[] type_refs,
      byte* bytecode,
      int start_ip)
    {
      //this.fb = fb;
      this.module = module;
      this.constants = constants;
      this.type_refs = type_refs;
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      this.return_ip = -1;

      //TODO: what about clearing defers here?
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deinit()
    {}
  }
}

}

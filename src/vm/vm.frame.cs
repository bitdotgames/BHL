using System;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace bhl
{
public partial class VM : INamedResolver
{
  public struct Frame
  {
    public Module module;
    public unsafe byte* bytecode;
    public Const[] constants;
    public IType[] type_refs;
    public int start_ip;
    public int return_ip;
    public int regions_mark;

    public FuncArgsInfo args_info;
    public int locals_offset;
    public int return_args_num;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetupForModule(Module module, int start_ip)
    {
      Init(module, module.compiled.bytecode_ptr, start_ip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetupForModuleInit(Module module, int start_ip = 0)
    {
      Init(module, module.compiled.initcode_ptr, start_ip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetupForOrigin(Frame origin, int start_ip)
    {
      Init(origin.module, origin.bytecode, start_ip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe void Init(Module module, byte* bytecode, int start_ip)
    {
      this.module = module;
      this.bytecode = bytecode;
      this.start_ip = start_ip;
      return_ip = -1;
      regions_mark = -1;

      //caching for faster access
      constants = module.compiled.constants;
      type_refs = module.compiled.type_refs_resolved;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Exit(ValStack stack)
    {
      //NOTE: let's consider all values on stack after callback execution
      //      as returned arguments, this way they won't be cleared upon Frame exiting
      //frame.return_args_num = fb.exec.stack.sp;

      int ret_start_offset = stack.sp - return_args_num;
      int local_vars_num = ret_start_offset - locals_offset;

      //releasing all locals
      for(int i = locals_offset; i < ret_start_offset; ++i)
      {
        ref var val = ref stack.vals[i];
        //TODO: what about blob?
        if(val._refc != null)
        {
          val._refc?.Release();
          val._refc = null;
        }
        val._obj = null;
      }

      if(return_args_num > 0)
      {
        //moving returned values up
        //NOTE: returned vals are already retained by GetVar opcode,
        //      no need to do that
        Array.Copy(
          stack.vals,
          ret_start_offset,
          stack.vals,
          locals_offset,
          return_args_num
        );

        //need to clean stack leftover
        int leftover = local_vars_num - return_args_num;
        for(int i = 0; i <= leftover; ++i)
        {
          ref var val = ref stack.vals[ret_start_offset + i];
          //TODO: what about blob?
          val._refc = null;
          val._obj = null;
        }
      }

      //stack pointer now at the last returned value
      stack.sp = locals_offset + return_args_num;
    }
  }
}

}

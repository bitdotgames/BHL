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
    public int locals_vars_num;
    public int locals_offset;
    public ValStack locals;
    public int return_vars_num;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void InitWithModule(Module module, int start_ip)
    {
      Init(module, module.compiled.bytecode_ptr, start_ip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void InitForModuleInit(Module module, int start_ip = 0)
    {
      Init(module, module.compiled.initcode_ptr, start_ip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void InitWithOrigin(Frame origin, int start_ip)
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
    public void CleanLocals(ValStack stack)
    {
      //releasing all locals
      for(int i = locals_offset; i < locals_offset + locals_vars_num; ++i)
      {
        ref var val = ref locals.vals[i];
        //TODO: what about blob?
        if(val._refc != null)
        {
          val._refc?.Release();
          val._refc = null;
        }
        val._obj = null;
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnVars(ValStack stack)
    {
      int ret_start_offset = stack.sp - return_vars_num;

      //moving returned values up
      //NOTE: returned vals are already retained by GetVar opcode,
      //      no need to do that

      //array copy version
      //Array.Copy(
      //  stack.vals,
      //  ret_start_offset,
      //  stack.vals,
      //  locals_offset,
      //  return_vars_num
      //);

      //loop version
      for(int i = 0; i < return_vars_num; ++i)
        stack.vals[locals_offset + i] = stack.vals[ret_start_offset + i];

      //need to clean stack leftover
      int leftover = locals_vars_num - return_vars_num;
      for(int i = 0; i <= leftover; ++i)
      {
        ref var val = ref stack.vals[ret_start_offset + i];
        //TODO: what about blob?
        val._refc = null;
        val._obj = null;
      }
    }
  }
}

}

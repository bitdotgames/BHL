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
    public void CleanLocals()
    {
      //releasing all locals
      for(int i = locals_offset; i < locals_offset + locals_vars_num; ++i)
      {
        ref var val = ref locals.vals[i];
        if(val._refc != null)
        {
          val._refc?.Release();
          val._refc = null;
        }
        val.obj = null;
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnVars(ValStack stack)
    {
      //moving returned values up

      int ret_start_offset = stack.sp - return_vars_num;

      //NOTE: returned vals are already retained by GetVar opcode,
      //      no need to do that

      //it's assumed that this code is called once all locals were
      //cleaned, we need to clean stack 'copy leftover'
      for(int i = 0; i < return_vars_num; ++i)
      {
        int local_idx = locals_offset + i;
        int ret_idx = ret_start_offset + i;
        if(local_idx < ret_idx)
        {
          ref var ret = ref stack.vals[ret_idx];
          //stack.vals[local_idx] = ret;
          //optimized version
          stack.vals[local_idx].num = ret.num;
          //ret._refc = null;
          //ret.obj = null;
        }
      }
    }
  }
}

}

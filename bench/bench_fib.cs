using System;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using bhl;

[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class BenchFibonacciImported : BHL_TestBase
{
  VM vm;
  VM vm_aot;
  FuncSymbolScript fs_simple;
  VM.ExecState.LocalFunc[] funcs_aot = new VM.ExecState.LocalFunc[8];
  Const[] const_aot = new Const[8];

  //FuncSymbolScript fs_imported;
  //FuncSymbolScript fs_class_imported;
  //FuncSymbolScript fs_interface_imported;

  //public BenchFibonacciImported()
  //{
  //  string fib1 = @"
  //  import ""fib2""

  //  func int fib1(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib2(x - 1) + fib2(x - 2)
  //      }
  //    }
  //  }

  //  class Fib1
  //  {
  //    func int fib1(int x, Fib2 f2)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f2.fib2(x - 1, this) + f2.fib2(x - 2, this)
  //        }
  //      }
  //    }
  //  }

  //  interface IFib
  //  {
  //    func int fib(int x, IFib f)
  //  }

  //  class Fib : IFib
  //  {
  //    func int fib(int x, IFib f)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f.fib(x - 1, this) + f.fib(x - 2, this)
  //        }
  //      }
  //    }
  //  }
  //  ";

  //  string fib2 = @"
  //  import ""fib1""

  //  func int fib2(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib1(x - 1) + fib1(x - 2)
  //      }
  //    }
  //  }

  //  class Fib2
  //  {
  //    func int fib2(int x, Fib1 f1)
  //    {
  //      if(x == 0) {
  //        return 0
  //      } else {
  //        if(x == 1) {
  //          return 1
  //        } else {
  //          return f1.fib1(x - 1, this) + f1.fib1(x - 2, this)
  //        }
  //      }
  //    }
  //   }
  //  ";

  //  string test = @"
  //  import ""fib1""
  //  import ""fib2""

  //  func int fib(int x)
  //  {
  //    if(x == 0) {
  //      return 0
  //    } else {
  //      if(x == 1) {
  //        return 1
  //      } else {
  //        return fib(x - 1) + fib(x - 2)
  //      }
  //    }
  //  }

  //  func test_simple()
  //  {
  //    int x = 15
  //    fib(x)
  //  }

  //  func test_imported()
  //  {
  //    int x = 15
  //    fib1(x)
  //  }

  //  func test_class_imported()
  //  {
  //    int x = 15
  //    var f1 = new Fib1
  //    var f2 = new Fib2
  //    f1.fib1(x, f2)
  //  }

  //  func test_interface_imported()
  //  {
  //    int x = 15
  //    IFib f = new Fib
  //    f.fib(x, f)
  //  }
  //  ";

  //  vm = MakeVM(new Dictionary<string, string>() {
  //      {"fib1.bhl", fib1},
  //      {"fib2.bhl", fib2},
  //      {"test.bhl", test},
  //    }
  //  ).GetAwaiter().GetResult();

  //  vm.LoadModule("test");
  //  fs_simple =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_simple").LoadFuncSymbol(vm);
  //  fs_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_imported").LoadFuncSymbol(vm);
  //  fs_class_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_class_imported").LoadFuncSymbol(vm);
  //  fs_interface_imported =
  //    (FuncSymbolScript)new VM.SymbolSpec("test", "test_interface_imported").LoadFuncSymbol(vm);
  //}

  public BenchFibonacciImported()
  {
    string test = @"

    func int fib(int x)
    {
      if(x == 0) {
        return 0
      } else {
        if(x == 1) {
          return 1
        } else {
          return fib(x - 1) + fib(x - 2)
        }
      }
    }

    func test_simple()
    {
      fib(15)
    }
    ";

    vm = MakeVM(new Dictionary<string, string>() {
        {"test.bhl", test},
      }
    ).GetAwaiter().GetResult();

    vm.LoadModule("test");
    fs_simple =
      (FuncSymbolScript)new VM.SymbolSpec("test", "test_simple").LoadFuncSymbol(vm);

    vm_aot = new VM();
    funcs_aot[0] = __fib;
    const_aot[0] = new Const(0);
    const_aot[1] = new Const(1);
    const_aot[2] = new Const(2);
  }

  static int fib(int x)
  {
    if(x == 0) {
      return 0;
    } else {
      if(x == 1) {
        return 1;
      } else {
        return fib(x - 1) + fib(x - 2);
      }
    }
  }

  [Benchmark(Baseline = true)]
  public void FibonacciDotNet()
  {
    fib(15);
  }

  [Benchmark]
  public void FibonacciSimple()
  {
    vm.Execute2(fs_simple);
  }

  [Benchmark]
  public void FibonacciAOT()
  {
    var fb = VM.Fiber.New(vm_aot);
    fb.exec.funcs2 = funcs_aot;
    fb.exec.constants2 = const_aot;

    {
      ref Val2 v = ref fb.exec.stack2.Push();
      v.type = Types.Int;
      v._num = 15;
    }
    {
      //passing args info as a stack variable
      ref Val2 v = ref fb.exec.stack2.Push();
      v._num = 1;
    }
    var region = new VM.Region();
    var status = BHS.SUCCESS;
    //var frame = VM.Frame.New(vm_aot);
    ref var frame2 = ref fb.exec.PushFrame2();
    fb.exec.funcs2[0](vm_aot, fb.exec, ref region, ref frame2, ref status);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __InitFrame(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int local_vars_num
    )
  {
    var stack = exec.stack2;

    //ref Val2 args_bits = ref stack.vals[--stack.sp];
    ref Val2 args_bits = ref stack.vals[stack.sp - 1];

    curr_frame.args_bits2 = (uint)args_bits._num;
    curr_frame.locals_idx2 = stack.sp - local_vars_num;
    stack.Add(local_vars_num);
  }

  static void __fib(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status
    )
  {
    var __status = BHS.NONE;

    __InitFrame(vm, exec, ref region, ref curr_frame, ref __status, 2);
    __ArgVar(vm, exec, ref region, ref curr_frame, ref __status, 0);
    //if x == 0
    __GetVar(vm, exec, ref region, ref curr_frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 0);
    __Equal(vm, exec, ref region, ref curr_frame, ref __status);
    //if x != 0 then jump
    if(__JumpZ(vm, exec, ref region, ref curr_frame, ref __status))
      goto _1_9;
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 0);
    /*18*/__ReturnVal(vm, exec, ref region, ref curr_frame, ref __status, 1);
    goto _exit;
    _1_9:
    //if x == 1
    __GetVar(vm, exec, ref region, ref curr_frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 1);
    __Equal(vm, exec, ref region, ref curr_frame, ref __status);
    if(__JumpZ(vm, exec, ref region, ref curr_frame, ref __status))
      goto _2_9;
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 1);
    /*37*/__ReturnVal(vm, exec, ref region, ref curr_frame, ref __status, 1);
    goto _exit;
    _2_9:
    //else
    __GetVar(vm, exec, ref region, ref curr_frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 1);
    /*48*/__Sub(vm, exec, ref region, ref curr_frame, ref __status);
    __CallLocal(vm, exec, ref region, ref curr_frame, ref __status, 0, 1);
    /*57*/__GetVar(vm, exec, ref region, ref curr_frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref curr_frame, ref __status, 2);
    __Sub(vm, exec, ref region, ref curr_frame, ref __status);
    __CallLocal(vm, exec, ref region, ref curr_frame, ref __status, 0, 1);
    __Add(vm, exec, ref region, ref curr_frame, ref __status);
    /*73*/__ReturnVal(vm, exec, ref region, ref curr_frame, ref __status, 1);
    goto _exit;
    _exit:
      return;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __ArgVar(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int local_idx
  )
  {
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __GetVar(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int local_idx
  )
  {
    ref Val2 v = ref exec.stack2.Push();
    v._num = exec.stack2.vals[curr_frame.locals_idx2 + local_idx]._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Constant(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int const_idx
  )
  {
    var cn = exec.constants2[const_idx];

    ref Val2 v = ref exec.stack2.Push();
    v.type = Types.Int;
    v._num = cn.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Equal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status
  )
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand.type = Types.Bool;
    l_operand._num = l_operand._num == r_operand._num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool __JumpZ(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status
  )
  {
    ref Val2 v = ref exec.stack2.vals[--exec.stack2.sp];
    return v._num == 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __ReturnVal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int ret_num
  )
  {
    var stack = exec.stack2;

    int ret_base = stack.sp - ret_num;
    //releasing all locals
    for(int i = curr_frame.locals_idx2; i < ret_base; ++i)
      stack.vals[i].Release();

    int new_sp = curr_frame.locals_idx2 + ret_num;

    //moving returned values up
    for(int i = 0; i < ret_num; ++i)
      stack.vals[curr_frame.locals_idx2 + i] = stack.vals[ret_base + i];

    stack.sp = new_sp;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Sub(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status
  )
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num -= r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Add(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status
  )
  {
    var stack = exec.stack2;

    ref Val2 r_operand = ref stack.vals[--stack.sp];
    ref Val2 l_operand = ref stack.vals[stack.sp - 1];
    l_operand._num += r_operand._num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __CallLocal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame2 curr_frame,
    ref BHS status,
    int func_ip,
    uint args_bits
    )
  {
    var fn = exec.funcs2[func_ip];

    //var frm = VM.Frame.New(vm);
    //frm.Init(curr_frame, exec.stack, func_ip);

    ref var new_frame = ref exec.PushFrame2();
    new_frame.Init(curr_frame, /*exec.stack,*/ func_ip);

    ref Val2 v = ref exec.stack2.Push();
    v.type = Types.Int;
    v._num = args_bits;

    fn(vm, exec, ref region, ref new_frame, ref status);
  }
}

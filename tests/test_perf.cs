using System;
using System.Runtime.CompilerServices;
using bhl;
using Xunit;

public class TestPerf : BHL_TestBase
{
  [Fact]
  public void TestFibonacci()
  {
    string bhl = @"
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
    ";

    var vm = MakeVM(bhl);
    vm.TryFindFuncAddr("fib", out var addr);

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      double res = vm.Execute(addr.fs, 15).Pop();
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, res);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      double res = vm.Execute(addr.fs, 15).Pop();
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, res);
    }
  }

  //[Fact]
  //public void TestFibonacciAOT()
  //{
  //  var vm = new VM();
  //  var fb = VM.Fiber.New(vm);
  //  fb.exec.funcs2[0] = __fib;
  //  fb.exec.constants[0] = new Const(0);
  //  fb.exec.constants[1] = new Const(1);
  //  fb.exec.constants[2] = new Const(2);

  //  {
  //    ref Val v = ref fb.exec.stack.Push();
  //    v.type = Types.Int;
  //    v._num = 15;
  //  }
  //  {
  //    //passing args info as a stack variable
  //    ref Val v = ref fb.exec.stack.Push();
  //    v._num = 1;
  //  }
  //  var region = new VM.Region();
  //  var status = BHS.SUCCESS;
  //  ref var frame = ref fb.exec.PushFrame();
  //  fb.exec.funcs2[0](vm, fb.exec, ref region, ref frame, ref status);
  //  Assert.Equal(610, fb.exec.stack.PopRelease().num);
  //}

  static void __fib(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status
    )
  {
    var __status = BHS.NONE;

    __InitFrame(vm, exec, ref region, ref frame, ref __status, 2);
    __ArgVar(vm, exec, ref region, ref frame, ref __status, 0);
    //if x == 0
    __GetVar(vm, exec, ref region, ref frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref frame, ref __status, 0);
    __Equal(vm, exec, ref region, ref frame, ref __status);
    //if x != 0 then jump
    if(__JumpZ(vm, exec, ref region, ref frame, ref __status))
      goto _1_9;
    __Constant(vm, exec, ref region, ref frame, ref __status, 0);
    /*18*/__ReturnVal(vm, exec, ref region, ref frame, ref __status, 1);
    goto _exit;
    _1_9:
    //if x == 1
    __GetVar(vm, exec, ref region, ref frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref frame, ref __status, 1);
    __Equal(vm, exec, ref region, ref frame, ref __status);
    if(__JumpZ(vm, exec, ref region, ref frame, ref __status))
      goto _2_9;
    __Constant(vm, exec, ref region, ref frame, ref __status, 1);
    /*37*/__ReturnVal(vm, exec, ref region, ref frame, ref __status, 1);
    goto _exit;
    _2_9:
    //else
    __GetVar(vm, exec, ref region, ref frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref frame, ref __status, 1);
    /*48*/__Sub(vm, exec, ref region, ref frame, ref __status);
    __CallLocal(vm, exec, ref region, ref frame, ref __status, 0, 1);
    /*57*/__GetVar(vm, exec, ref region, ref frame, ref __status, 0);
    __Constant(vm, exec, ref region, ref frame, ref __status, 2);
    __Sub(vm, exec, ref region, ref frame, ref __status);
    __CallLocal(vm, exec, ref region, ref frame, ref __status, 0, 1);
    __Add(vm, exec, ref region, ref frame, ref __status);
    /*73*/__ReturnVal(vm, exec, ref region, ref frame, ref __status, 1);
    goto _exit;
    _exit:
      return;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __InitFrame(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status,
    int local_vars_num
    )
  {
    var stack = exec.stack;

    ref Val args_bits = ref stack.vals[--stack.sp];

    var args_info = new FuncArgsInfo((uint)stack.vals[--stack.sp]);
    frame.args_info = args_info;
    frame.locals_offset = stack.sp - args_info.CountArgs();
    stack.Reserve(local_vars_num);
    //temporary stack lives after local variables
    stack.sp += local_vars_num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __ArgVar(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
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
    ref VM.Frame frame,
    ref BHS status,
    int local_idx
  )
  {
    ref Val v = ref exec.stack.Push();
    v.num = exec.stack.vals[frame.locals_offset + local_idx].num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Constant(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status,
    int const_idx
  )
  {
    throw new NotImplementedException();
    //var cn = exec.constants[const_idx];

    //ref Val v = ref exec.stack.Push();
    //v.type = Types.Int;
    //v._num = cn.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Equal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status
  )
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.type = Types.Bool;
    l_operand.num = l_operand.num == r_operand.num ? 1 : 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool __JumpZ(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status
  )
  {
    ref Val v = ref exec.stack.vals[--exec.stack.sp];
    return v.num == 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __ReturnVal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status,
    int ret_num
  )
  {
    var stack = exec.stack;

    int ret_base = stack.sp - ret_num;
    //releasing all locals
    for(int i = frame.locals_offset; i < ret_base; ++i)
      stack.vals[i].ReleaseData();

    int new_sp = frame.locals_offset + ret_num;

    //moving returned values up
    for(int i = 0; i < ret_num; ++i)
      stack.vals[frame.locals_offset + i] = stack.vals[ret_base + i];

    stack.sp = new_sp;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Sub(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status
  )
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.num -= r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __Add(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status
  )
  {
    var stack = exec.stack;

    ref Val r_operand = ref stack.vals[--stack.sp];
    ref Val l_operand = ref stack.vals[stack.sp - 1];
    l_operand.num += r_operand.num;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void __CallLocal(
    VM vm,
    VM.ExecState exec,
    ref VM.Region region,
    ref VM.Frame frame,
    ref BHS status,
    int func_ip,
    uint args_bits
    )
  {
    //var fn = frame.module.funcs[func_ip];

    //ref var new_frame2 = ref exec.PushFrame();
    //new_frame2.Init(ref frame);

    //ref Val v = ref exec.stack.Push();
    //v.type = Types.Int;
    //v._num = args_bits;

    //fn(vm, exec, ref region, ref new_frame2, ref status);
  }

}

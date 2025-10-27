using System;
using System.Runtime.CompilerServices;
using bhl;
using Xunit;

public class TestPerf : BHL_TestBase
{
  [Fact]
  public void TestFibonacciNew()
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
    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("fib", Val.NewInt(15));
      Assert.False(vm.Tick());
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, fb.Stack.Pop().num);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("fib", Val.NewInt(15));
      Assert.False(vm.Tick());
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
      Assert.Equal(610, fb.Stack.Pop().num);
    }
  }

  [Fact]
  public void TestFibonacciOld()
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

    func int test()
    {
      int x = 15
      return fib(x)
    }
    ";

    var c = Compile(bhl);

    var vm = MakeVM(c);

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      stopwatch.Stop();
      Assert.Equal(610, fb.result_old.PopRelease().num);
      Console.WriteLine("fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var fb = vm.Start("test");
      Assert.False(vm.Tick());
      stopwatch.Stop();
      Assert.Equal(610, fb.result_old.PopRelease().num);
      Console.WriteLine("fib ticks2: {0}", stopwatch.ElapsedTicks);
    }

    CommonChecks(vm);
  }

  [Fact]
  public void TestFibonacciAOT()
  {
    var vm = new VM();
    var fb = VM.Fiber.New(vm);
    fb.exec.funcs2[0] = __fib;
    fb.exec.constants[0] = new Const(0);
    fb.exec.constants[1] = new Const(1);
    fb.exec.constants[2] = new Const(2);

    {
      ref Val v = ref fb.exec.stack.Push();
      v.type = Types.Int;
      v._num = 15;
    }
    {
      //passing args info as a stack variable
      ref Val v = ref fb.exec.stack.Push();
      v._num = 1;
    }
    var region = new VM.Region();
    var status = BHS.SUCCESS;
    ref var frame = ref fb.exec.PushFrame();
    fb.exec.funcs2[0](vm, fb.exec, ref region, ref frame, ref status);
    Assert.Equal(610, fb.exec.stack.PopRelease().num);
  }

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

    //ref Val2 args_bits = ref stack.vals[--stack.sp];
    ref Val args_bits = ref stack.vals[stack.sp - 1];

    frame.args_bits = (uint)args_bits._num;
    frame.locals_offset = stack.sp - local_vars_num;
    stack.Reserve(local_vars_num);
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
    v._num = exec.stack.vals[frame.locals_offset + local_idx]._num;
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
    var cn = exec.constants[const_idx];

    ref Val v = ref exec.stack.Push();
    v.type = Types.Int;
    v._num = cn.num;
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
    l_operand._num = l_operand._num == r_operand._num ? 1 : 0;
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
    return v._num == 0;
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
      stack.vals[i].Release();

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
    l_operand._num -= r_operand._num;
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
    l_operand._num += r_operand._num;
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
    var fn = exec.funcs2[func_ip];

    ref var new_frame2 = ref exec.PushFrame();
    new_frame2.Init(ref frame);
    //var frm = VM.Frame.New(vm);
    //frm.Init(curr_frame, exec.stack, func_ip);

    ref Val v = ref exec.stack.Push();
    v.type = Types.Int;
    v._num = args_bits;

    fn(vm, exec, ref region, ref new_frame2, ref status);
  }

}

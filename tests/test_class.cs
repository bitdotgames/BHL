using System;           
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;

public class TestClasses : BHL_TestBase
{
  [IsTested()]
  public void TestEmptyUserClass()
  {
    {
      string bhl = @"

      class Foo {}
        
      func bool test() 
      {
        Foo f = new Foo
        return f != null
      }
      ";

      var c = Compile(bhl);

      var expected = 
        new ModuleCompiler()
        .UseCode()
        .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
        .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
        .EmitThen(Opcodes.SetVar, new int[] { 0 })
        .EmitThen(Opcodes.GetVar, new int[] { 0 })
        .EmitThen(Opcodes.Constant, new int[] { 1 })
        .EmitThen(Opcodes.NotEqual)
        .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
        .EmitThen(Opcodes.Return)
        ;
      AssertEqual(c, expected);

      var vm = MakeVM(c);
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      AssertTrue(fb.result.PopRelease().bval);
      CommonChecks(vm);
    }

    {
      string bhl = @"

      class Foo { }
        
      func bool test() 
      {
        Foo f = new Foo
        return f != null
      }
      ";

      var vm = MakeVM(bhl);
      var fb = vm.Start("test");
      AssertFalse(vm.Tick());
      AssertTrue(fb.result.PopRelease().bval);
      CommonChecks(vm);
    }
  }

  [IsTested()]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo {}
      
    func test() 
    {
      Foo f = {}
    }
    ";

    var ts = new Types();
    BindFoo(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestSeveralEmptyUserClasses()
  {
    string bhl = @"

    class Foo {}
    class Bar{
    }

    func bool test() 
    {
      Foo f = {}
      Bar b = { 
      }
      return f != null && b != null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSelfInheritanceIsNotAllowed()
  {
    string bhl = @"
    class Foo : Foo {}
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "self inheritance is not allowed"
    );
  }

  [IsTested()]
  public void TestUserClassWithSimpleMembers()
  {
    string bhl = @"

    class Foo { 
      int Int
      float Flt
      string Str
    }
      
    func test() 
    {
      Foo f = new Foo
      f.Int = 10
      f.Flt = 14.2
      f.Str = ""Hey""
      trace((string)f.Int + "";"" + (string)f.Flt + "";"" + f.Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.2) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, c.ns.T("string")) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.gindex.IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.2;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMemberOperations()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      f.b = f.b + 1
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSeveralClassMembersOperations()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

     class Bar {
       float a
     }
      
    func float test() 
    {
      Foo f = { c : 2, b : 101.5 }
      Bar b = { a : 10 }
      f.b = f.b + f.c + b.a
      return f.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(113.5, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInit()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
      string s
    }
      
    func string test() 
    {
      Foo f = {}
      return (string)f.b + "";"" + (string)f.c + "";"" + f.s + "";""
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("0;0;;", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitBool()
  {
    string bhl = @"

    class Foo { 
      bool c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == false
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithArr()
  {
    string bhl = @"

    class Foo { 
      []int a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      return f.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(20, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitArr()
  {
    string bhl = @"

    class Foo { 
      []int a
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.a == null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassArrayClear()
  {
    string bhl = @"

    class Foo { 
      []int a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      f.a.Clear()
      f.a.Add(30)
      return f.a[0]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtr()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    class Foo { 
      func int(int) ptr
    }
      
    func int test() 
    {
      Foo f = {}
      f.ptr = foo
      return f.ptr(2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitFuncPtr()
  {
    string bhl = @"

    class Foo { 
      func void(int) ptr
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.ptr == null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArray()
  {
    string bhl = @"

    func int foo(int a)
    {
      return a + 1
    }

    func int foo2(int a)
    {
      return a + 10
    }

    class Foo { 
      []func int(int) ptrs
    }
      
    func int test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      return f.ptrs[1](2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithFuncPtrsArrayCleanArgsStack()
  {
    string bhl = @"

    func int foo(int a,int b)
    {
      return a + 1
    }

    func int foo2(int a,int b)
    {
      return a + 10
    }

    class Foo { 
      []func int(int,int) ptrs
    }

    func int bar()
    {
      fail()
      return 1
    }
      
    func void test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      f.ptrs[1](2, bar())
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.Count);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitEnum()
  {
    string bhl = @"

    enum Bar {
      NONE = 0
      FOO  = 1
    }

    class Foo { 
      Bar b
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.b == Bar.NONE
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassWithAnotherUserClassMember()
  {
    string bhl = @"

    class Bar {
      []int a
    }

    class Foo { 
      Bar b
    }
      
    func int test() 
    {
      Foo f = {b : { a : [10, 21] } }
      return f.b.a[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(21, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStringConcat()
  {
    string bhl = @"

    class Foo { 
      string s1
      string s2
    }
      
    func string test() 
    {
      Foo f = {}
      return f.s1 + f.s2
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual("", Execute(vm, "test").result.PopRelease().str);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassAny()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      any foo = f
      return foo != null
    }
    ";

    var vm = MakeVM(bhl);
    AssertTrue(Execute(vm, "test").result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassAnyCastBack()
  {
    string bhl = @"

    class Foo { int x }
      
    func int test() 
    {
      Foo f = {x : 10}
      any foo = f
      Foo f1 = (Foo)foo
      return f1.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethod()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA() 
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA()
    }
    ";

    var c = Compile(bhl);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      .EmitThen(Opcodes.InitFrame, new int[] { 1+1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, c.ns.T("Foo")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.CallMethod, new int[] { 1, 0 })
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestSeveralUserClassMethods()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA()
      {
        return this.a
      }

      func int getB() 
      {
        return this.b
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      f.b = 20
      return f.getA() + f.getB()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(30, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArgToClassMethod()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA(int i)
      {
        return this.a + i
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(12, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestPassArgToClassMethodFromAnotherMethod()
  {
    string bhl = @"

    class Foo {
      func Bar() {
        this.Wow(0)
      }
      func Wow(float t) {
      }
    }

    func test()
    {
      Foo f = {}
      f.Bar()
    }
    ";

    var vm = MakeVM(bhl);
    Execute(vm, "test");
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDefaultArgValueInMethod()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA(int i, int c = 10)
      {
        return this.a + i + c
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(22, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestDefaultArgValueInMethod2()
  {
    string bhl = @"

    class Foo {
      
      int a

      func int getA(int i, int c = 10)
      {
        return this.a + i + c
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      return f.getA(2, 100)
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(112, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethodNamedLikeClass()
  {
    string bhl = @"

    class Foo {
      
      func int Foo()
      {
        return 2
      }
    }

    func int test()
    {
      Foo f = {}
      return f.Foo()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassMethodLocalVariableNotDeclared()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Summ()
        {
          a = 1
          return this.a + a
        }
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "symbol not resolved"
    );
  }

  [IsTested()]
  public void TestUserClassMethodLocalVarAndAttribute()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Foo()
        {
          int a = 1
          return this.a + a
        }
      }

      func int test()
      {
        Foo f = {}
        f.a = 10
        return f.Foo()
      }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(11, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildAttributes()
  {
    string bhl = @"
    class Foo {
      int a
      int b
    }

    class Bar : Foo {
      int c
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.a + b.b + b.c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildAttributeConflict()
  {
    string bhl = @"

    class Foo {
      int a
      int b
    }

    class Bar : Foo {
      int b
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'b'"
    );
  }

  [IsTested()]
  public void TestUserChildClassMethod()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSubChildClassMethod()
  {
    string bhl = @"
    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {
      
      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getA() + b.getB() + b.getC()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodAccessesParent()
  {
    string bhl = @"
    class Foo {
      
      int a
      int b

      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA() 
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodsOrderIrrelevant()
  {
    string bhl = @"

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }

    class Bar : Foo {

      func int getSumm() {
        return this.getC() + this.getB() + this.getA() 
      }

      func int getC() {
        return this.c
      }

      int c
    }

    class Foo : Hey {
      
      func int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }

      int a
    }

    class Hey {
      int b
    }

    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserSubChildClassMethodAccessesParent()
  {
    string bhl = @"
    class Base {
      int a

      func int getA() {
        return this.a
      }
    }

    class Foo : Base {
      
      int b

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int c

      func int getC() {
        return this.c
      }

      func int getSumm() {
        return this.getC() + this.getB() + this.getA() 
      }
    }

    func int test()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.c = 100
      return b.getSumm()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(111, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodAlreadyDefined()
  {
    string bhl = @"
    class Foo {
      int a
      func int getA() {
        return this.a
      }
    }

    class Bar : Foo {
      func int getA() {
        return this.a
      }
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"already defined symbol 'getA'"
    );
  }

  [IsTested()]
  public void TestArrayOfUserClasses()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }

    func float test() 
    {
      []Foo fs = [{b:1, c:2}]
      fs.Add({b:10, c:20})

      return fs[0].b + fs[0].c + fs[1].b + fs[1].c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(33, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      Foo f1 = (Foo)a
      return f1.b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCast()
  {
    string bhl = @"

    class Foo { 
      float b
    }
      
    func float test() 
    {
      Foo f = { b : 101 }
      any a = f
      return ((Foo)a).b
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(101, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr()
  {
    string bhl = @"

    class Foo { 
      []int b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b[1]
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(102, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr2()
  {
    string bhl = @"

    class Foo { 
      []int b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b.Count
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClass()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base { 
      float y
    }
      
    func float test() {
      Foo f = { x : 1, y : 2}
      return f.y + f.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassOrderIrrelevant()
  {
    string bhl = @"

    func float test() {
      Foo f = { x : 1, y : 2}
      return f.y + f.x
    }

    class Foo : Base { 
      float y
    }
      
    class Base {
      float x 
    }

    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassDowncast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      return b.x
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(1, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassUpcast()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { x : 1, y : 2}
      Base b = (Foo)f
      Foo f2 = (Foo)b
      return f2.x + f2.y
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(3, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestChildUserClassDefaultInit()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      float y
    }
      
    func float test() 
    {
      Foo f = { y : 2}
      return f.x + f.y
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(2, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassReadDefaultField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo).c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(0, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassReadField()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func int test() 
    {
      return (new Foo{c: 10}).c
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestTmpUserClassWriteFieldNotAllowed()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func test() 
    {
      (new Foo{c: 10}).c = 20
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestChildUserClassAlreadyDefinedMember()
  {
    string bhl = @"

    class Base {
      float x 
    }

    class Foo : Base 
    { 
      int x
    }
      
    func float test() 
    {
      Foo f = { x : 2}
      return f.x
    }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "already defined symbol 'x'"
    );
  }

  [IsTested()]
  public void TestChildUserClassOfNativeClassIsForbidden()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }
    ";

    var ts = new Types();
    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "extending native classes is not supported"
    );
  }

  [IsTested()]
  public void TestUserClassNotAllowedToContainThisMember()
  {
    string bhl = @"
      class Foo {
        int this
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved"
    );
  }

  [IsTested()]
  public void TestUserClassContainThisMethodNotAllowed()
  {
    string bhl = @"
      class Foo {

        func bool this()
        {
          return false
        }
      }
    ";

    AssertError<Exception>(
      delegate() { 
        Compile(bhl);
      },
      "the keyword \"this\" is reserved"
    );
  }

  [IsTested()]
  public void TestBindNativeClass()
  {
    string bhl = @"

    func bool test() 
    {
      Bar b = new Bar
      return b != null
    }
    ";

    var ts = new Types();
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.T("Bar")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstNullIdx(c) })
      .EmitThen(Opcodes.NotEqual)
      .EmitThen(Opcodes.ReturnVal, new int[] { 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    var fb = vm.Start("test");
    AssertFalse(vm.Tick());
    AssertTrue(fb.result.PopRelease().bval);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassWithSimpleMembers()
  {
    string bhl = @"

    func test() 
    {
      Bar o = new Bar
      o.Int = 10
      o.Flt = 14.5
      o.Str = ""Hey""
      trace((string)o.Int + "";"" + (string)o.Flt + "";"" + o.Str)
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    var fn = BindTrace(ts, log);
    BindBar(ts);
    var c = Compile(bhl, ts);

    var expected = 
      new ModuleCompiler()
      .UseCode()
      .EmitThen(Opcodes.InitFrame, new int[] { 1 + 1 /*args info*/})
      .EmitThen(Opcodes.New, new int[] { ConstIdx(c, ts.T("Bar")) }) 
      .EmitThen(Opcodes.SetVar, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 10) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 0 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, 14.5) })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 1 })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, "Hey") })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.SetAttr, new int[] { 2 })
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 0 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")) })
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 1 })
      .EmitThen(Opcodes.TypeCast, new int[] { ConstIdx(c, ts.T("string")) })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.Constant, new int[] { ConstIdx(c, ";") })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.GetVar, new int[] { 0 })
      .EmitThen(Opcodes.GetAttr, new int[] { 2 })
      .EmitThen(Opcodes.Add)
      .EmitThen(Opcodes.CallNative, new int[] { ts.gindex.IndexOf(fn), 1 })
      .EmitThen(Opcodes.Return)
      ;
    AssertEqual(c, expected);

    var vm = MakeVM(c, ts);
    vm.Start("test");
    AssertFalse(vm.Tick());
    AssertEqual("10;14.5;Hey", log.ToString().Replace(',', '.')/*locale issues*/);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassAttributesAccess()
  {
    string bhl = @"
      
    func float test(float k) 
    {
      Color c = new Color
      c.r = k*1
      c.g = k*100
      return c.r + c.g
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 2)).result.PopRelease().num;
    AssertEqual(res, 202);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeClassCallMember()
  {
    string bhl = @"

    func float test(float a)
    {
      return mkcolor(a).r
    }
    ";

    var ts = new Types();
    
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    var res = Execute(vm, "test", Val.NewNum(vm, 10)).result.PopRelease().num;
    AssertEqual(res, 10);
    res = Execute(vm, "test", Val.NewNum(vm, 20)).result.PopRelease().num;
    AssertEqual(res, 20);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestNativeAttributeIsNotAFunction()
  {
    string bhl = @"

    func float r()
    {
      return 0
    }
      
    func test() 
    {
      Color c = new Color
      c.r()
    }
    ";

    var ts = new Types();

    BindColor(ts);

    AssertError<Exception>(
      delegate() { 
        Compile(bhl, ts);
      },
      "symbol is not a function"
    );
  }

  [IsTested()]
  public void TestNullWithClassInstance()
  {
    string bhl = @"
      
    func test() 
    {
      Color c = null
      Color c2 = new Color
      if(c == null) {
        trace(""NULL;"")
      }
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c2 != null) {
        trace(""NOTNULL;"")
      }
      c = c2
      if(c2 == c) {
        trace(""EQ;"")
      }
      if(c2 == null) {
        trace(""NEVER;"")
      }
      if(c != null) {
        trace(""NOTNULL;"")
      }
    }
    ";

    var ts = new Types();
    var log = new StringBuilder();
    BindTrace(ts, log);
    BindColor(ts);

    var vm = MakeVM(bhl, ts);
    Execute(vm, "test");
    AssertEqual("NULL;NOTNULL;EQ;NOTNULL;", log.ToString());
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestCallImportedMethodFromLocalMethod()
  {
    string bhl1 = @"
    class A { 
      func int a() {
        return 10
      }
    }
    ";
      
  string bhl2 = @"
    import ""bhl1""  
    class B { 
      func int b() {
        A a = {}
        return a.a()
      }
    }

    func int test() {
      B b = {}
      return b.b()
    }
    ";

    CleanTestDir();
    var files = new List<string>();
    NewTestFile("bhl1.bhl", bhl1, ref files);
    NewTestFile("bhl2.bhl", bhl2, ref files);

    var ts = new Types();
    var loader = new ModuleLoader(ts, CompileFiles(files, ts));
    var vm = new VM(ts, loader);

    vm.LoadModule("bhl2");
    AssertEqual(10, Execute(vm, "test").result.PopRelease().num);
    CommonChecks(vm);
  }

  [IsTested()]
  public void TestUserChildClassMethodOverridesBase()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func virtual int getA() {
        return this.a
      }

      func int getB() {
        return this.b
      }
    }

    class Bar : Foo {
      int new_a

      func override int getA() {
        return this.new_a
      }
    }

    func int test1()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return b.getA() + b.getB()
    }

    func int test2()
    {
      Bar b = {}
      b.a = 1
      b.b = 10
      b.new_a = 100
      return ((Foo)b).getA() + b.getB()
    }
    ";

    var vm = MakeVM(bhl);
    AssertEqual(110, Execute(vm, "test1").result.PopRelease().num);
    AssertEqual(11, Execute(vm, "test2").result.PopRelease().num);
    CommonChecks(vm);
  }
}

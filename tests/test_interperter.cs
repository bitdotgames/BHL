using System;
using System.IO;
using System.Collections.Generic;
using bhl;

public class BHL_TestInterpreter : BHL_TestBase
{
  public class Color
  {
    public float r;
    public float g;

    public override string ToString()
    {
      return "[r="+r+",g="+g+"]";
    }
  }

  public class ColorAlpha : Color
  {
    public float a;

    public override string ToString()
    {
      return "[r="+r+",g="+g+",a="+a+"]";
    }
  }

  public class ColorNested
  {
    public Color c = new Color();
  }

  public class StringClass
  {
    public string str;
  }

  public class CustomNull
  {
    public bool is_null;

    public static bool operator ==(CustomNull b1, CustomNull b2)
    {
      return b1.Equals(b2);
    }

    public static bool operator !=(CustomNull b1, CustomNull b2)
    {
      return !(b1 == b2);
    }

    public override bool Equals(System.Object obj)
    {
      if(obj == null && is_null)
        return true;
      return false;
    }

    public override int GetHashCode()
    {
      return 0;
    }
  }

  public struct IntStruct
  {
    public int n;
    public int n2;

    public static void Decode(DynVal dv, ref IntStruct dst)
    {
      dst.n = (int)dv._num;
      dst.n2 = (int)dv._num2;
    }

    public static void Encode(DynVal dv, IntStruct dst)
    {
      dv._type = DynVal.ENCODED;
      dv._num = dst.n;
      dv._num2 = dst.n2;
    }
  }

  public struct MasterStruct
  {
    public StringClass child;
    public StringClass child2;
    public IntStruct child_struct;
    public IntStruct child_struct2;
  }

  public class MkColorNode : BehaviorTreeTerminalNode
  {
    public override void init()
    {
      var interp = Interpreter.instance;

      var r = interp.PopValue().num;
      var c = new Color();
      c.r = (float)r;
      var dv = DynVal.NewObj(c);

      interp.PushValue(dv);
    }
  }

  ClassSymbolNative BindColor(GlobalScope globs)
  {
    var cl = new ClassSymbolNative("Color",
      delegate(ref DynVal v) 
      { 
        v.obj = new Color();
      }
    );

    globs.Define(cl);
    globs.Define(new ArrayTypeSymbolT<Color>(globs, new TypeRef(cl), delegate() { return new List<Color>(); } ));
    cl.Define(new FieldSymbol("r", globs.Type("float"),
      delegate(DynVal ctx, ref DynVal v)
      {
        var c = (Color)ctx.obj;
        v.SetNum(c.r);
      },
      delegate(ref DynVal ctx, DynVal v)
      {
        var c = (Color)ctx.obj;
        c.r = (float)v.num; 
        ctx.obj = c;
      }
    ));
    cl.Define(new FieldSymbol("g", globs.Type("float"),
      delegate(DynVal ctx, ref DynVal v)
      {
        var c = (Color)ctx.obj;
        v.SetNum(c.g);
      },
      delegate(ref DynVal ctx, DynVal v)
      {
        var c = (Color)ctx.obj;
        c.g = (float)v.num; 
        ctx.obj = c;
      }
    ));

    {
      var m = new FuncSymbolSimpleNative("Add", globs.Type("Color"),
        delegate()
        {
          var interp = Interpreter.instance;

          var k = (float)interp.PopValue().num;
          var c = (Color)interp.PopValue().obj;

          var newc = new Color();
          newc.r = c.r + k;
          newc.g = c.g + k;

          var dv = DynVal.NewObj(newc);
          interp.PushValue(dv);

          return BHS.SUCCESS;
        }
      );
      m.Define(new FuncArgSymbol("k", globs.Type("float")));

      cl.Define(m);
    }

    {
      var m = new FuncSymbolSimpleNative("mult_summ", globs.Type("float"),
        delegate()
        {
          var interp = Interpreter.instance;

          var k = interp.PopValue().num;
          var c = (Color)interp.PopValue().obj;

          interp.PushValue(DynVal.NewNum((c.r * k) + (c.g * k)));

          return BHS.SUCCESS;
        }
      );
      m.Define(new FuncArgSymbol("k", globs.Type("float")));

      cl.Define(m);
    }

    {
      var fn = new FuncSymbolNative("mkcolor", globs.Type("Color"),
          delegate() { return new MkColorNode(); }
      );
      fn.Define(new FuncArgSymbol("r", globs.Type("float")));

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("mkcolor_null", globs.Type("Color"),
          delegate() { 
            var interp = Interpreter.instance;
            var dv = DynVal.New();
            dv.obj = null;
            interp.PushValue(dv);
            return BHS.SUCCESS;
          }
      );

      globs.Define(fn);
    }

    return cl;
  }

  void BindStringClass(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("StringClass",
        delegate(ref DynVal v) 
        { 
          v.obj = new StringClass();
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("str", globs.Type("string"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (StringClass)ctx.obj;
          v.str = c.str;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (StringClass)ctx.obj;
          c.str = v._str; 
          ctx.obj = c;
        }
      ));
    }
  }

  void BindCustomNull(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("CustomNull",
        delegate(ref DynVal v) 
        { 
          v.obj = new CustomNull();
        }
      );

      globs.Define(cl);
    }
  }

  void BindIntStruct(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("IntStruct",
        delegate(ref DynVal v) 
        { 
          var s = new IntStruct();
          IntStruct.Encode(v, s);
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("n", globs.Type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n = (int)v.num;
          IntStruct.Encode(ctx, s);
        }
      ));

      cl.Define(new FieldSymbol("n2", globs.Type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n2;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n2 = (int)v.num;
          IntStruct.Encode(ctx, s);
        }
      ));
    }
  }

  void BindMasterStruct(GlobalScope globs)
  {
    BindStringClass(globs);
    BindIntStruct(globs);

    {
      var cl = new ClassSymbolNative("MasterStruct",
        delegate(ref DynVal v) 
        { 
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.obj = o;
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("child", globs.Type("StringClass"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child = (StringClass)v._obj; 
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child2", globs.Type("StringClass"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          v.obj = c.child2;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          c.child2 = (StringClass)v.obj; 
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child_struct", globs.Type("IntStruct"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct = s;
          ctx.obj = c;
        }
      ));

      cl.Define(new FieldSymbol("child_struct2", globs.Type("IntStruct"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct2);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct2 = s;
          ctx.obj = c;
        }
      ));

    }
  }

  public class RefC : DynValRefcounted
  {
    public int refs;
    public Stream stream;

    public RefC(Stream stream)
    {
      this.stream = stream;
    }

    public void Retain()
    {
      var sw = new StreamWriter(stream);
      ++refs;
      sw.Write("INC" + refs + ";");
      sw.Flush();
    }

    public void Release(bool can_del = true)
    {
      var sw = new StreamWriter(stream);
      --refs;
      sw.Write("DEC" + refs + ";");
      sw.Flush();
      if(can_del)
        TryDel();
    }

    public bool TryDel()
    {
      var sw = new StreamWriter(stream);
      sw.Write("REL" + refs + ";");
      sw.Flush();
      return refs == 0;
    }
  }

  void BindRefC(GlobalScope globs, MemoryStream mstream)
  {
    {
      var cl = new ClassSymbolNative("RefC",
        delegate(ref DynVal v) 
        { 
          v.obj = new RefC(mstream);
        }
      );
      {
        var vs = new bhl.FieldSymbol("refs", globs.Type("int"),
          delegate(bhl.DynVal ctx, ref bhl.DynVal v)
          {
            v.num = ((RefC)ctx.obj).refs;
          },
          //read only property
          null
        );
        cl.Define(vs);
      }
      globs.Define(cl);
      globs.Define(new GenericArrayTypeSymbol(globs, new TypeRef(cl)));
    }
  }

  public class StartScriptNode : BehaviorTreeDecoratorNode
  {
    FuncCtx fct;

    public override void init()
    {
      var interp = Interpreter.instance;
      var dv = interp.PopValue(); 
      fct = (FuncCtx)dv.obj;
      fct.Retain();

      var func_node = fct.GetCallNode();

      this.setSlave(func_node);

      base.init();
    }

    public override void deinit()
    {
      base.deinit();
      fct.Release();
      fct = null;
    }
  }

  public class TraceNode : BehaviorTreeTerminalNode
  {
    Stream sm;

    public TraceNode(Stream sm)
    {
      this.sm = sm;
    }

    public override void init()
    {
      var interp = Interpreter.instance;
      var s = interp.PopValue();

      //Console.WriteLine("==============\n" + s.str + "\n" + Environment.StackTrace);

      var sw = new StreamWriter(sm);
      sw.Write(s.str);
      sw.Flush();
    }
  }

  public class WaitTicksNode : BehaviorTreeTerminalNode
  {
    int ticks;
    BHS result;

    public override void init()
    {
      var interp = Interpreter.instance;
      var s = interp.PopValue();
      var v = interp.PopValue();
      ticks = (int)v.num;
      result = s.bval ? BHS.SUCCESS : BHS.FAILURE;
    }

    public override BHS execute()
    {
      return --ticks > 0 ? BHS.RUNNING : result;
    }
  }

  void BindTrace(GlobalScope globs, MemoryStream trace_stream)
  {
    {
      var fn = new FuncSymbolNative("trace", globs.Type("void"),
          delegate() { return new TraceNode(trace_stream); } );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }
  }

  void AddString(MemoryStream s, string str)
  {
    var sw = new StreamWriter(s);
    sw.Write(str);
    sw.Flush();
  }

  string GetString(MemoryStream s)
  {
    s.Position = 0;
    var sr = new StreamReader(s);
    return sr.ReadToEnd();
  }

  void BindStartScriptInMgr(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("StartScriptInMgr", globs.Type("void"),
          delegate()
          {
            var interp = Interpreter.instance;
            bool now = interp.PopValue().bval;
            int num = (int)interp.PopValue().num;
            var fct = (FuncCtx)interp.PopValue().obj;

            for(int i=0;i<num;++i)
            {
              fct = fct.AutoClone();
              fct.Retain();
              var node = fct.GetNode();
              //Console.WriteLine("FREFS START: " + fct.GetHashCode() + " " + fct.refs);
              ScriptMgr.instance.add(node, now);
            }

            return BHS.SUCCESS;
          }
      );

      fn.Define(new FuncArgSymbol("script", globs.Type("void^()")));
      fn.Define(new FuncArgSymbol("num", globs.Type("int")));
      fn.Define(new FuncArgSymbol("now", globs.Type("bool")));

      globs.Define(fn);
    }
  }

  public class ScriptMgr : BehaviorTreeInternalNode
  {
    public static ScriptMgr instance = new ScriptMgr();

    List<BehaviorTreeNode> pending_removed = new List<BehaviorTreeNode>();
    bool in_exec = false;

    public List<BehaviorTreeNode> getChildren()
    {
      return children;
    }

    public void add(BehaviorTreeNode node, bool now = false)
    {
      children.Add(node);

      if(now)
        runNode(node);
    }

    public override void init()
    {}

    public override void deinit()
    {}

    public override void defer()
    {}

    public override BHS execute()
    {
      in_exec = true;

      for(int i=0;i<children.Count;i++)
        runAt(i);

      in_exec = false;

      for(int i=0;i<pending_removed.Count;++i)
        doDel(pending_removed[i]);
      pending_removed.Clear();

      return BHS.RUNNING;
    }

    new public void stop()
    {
      for(int i=children.Count;i-- > 0;)
      {
        var node = children[i];
        doDel(node);
      }
    }

    void runAt(int idx)
    {
      var node = children[idx];
      
      if(pending_removed.Count > 0 && pending_removed.Contains(node))
        return;

      var result = node.run();

      if(result != BHS.RUNNING)
        doDel(node);
    }

    void doDel(BehaviorTreeNode node)
    {
      int idx = children.IndexOf(node);
      if(idx == -1)
        return;
      
      if(in_exec)
      {
        if(pending_removed.IndexOf(node) == -1)
          pending_removed.Add(node);
      }
      else
      {
        //removing child ASAP so that it can't be 
        //be removed several times
        children.RemoveAt(idx);

        node.stop();

        var fnode = node as FuncNode;
        if(fnode != null && fnode.fct != null)
          fnode.fct.Release();
      }
    }

    public void runNode(BehaviorTreeNode node)
    {
      int idx = children.IndexOf(node);
      runAt(idx);
    }

    public bool busy()
    {
      return children.Count > 0 || pending_removed.Count > 0;
    }
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithRefs()
  {
    string bhl = @"

    func void test() 
    {
      float a = 0
      float b = 0
      float[] fs = []
      StartScriptInMgr(
        script: func() use(ref b, ref fs) { 
          a = a + 1
          b = b + 1
          fs.Add(b)
          trace((string) a + "","" + (string) b + "","" + (string) fs.Count + "";"") 
          suspend()
        },
        num : 3,
        now : false
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    ScriptMgr.instance.run();

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("1,1,1;1,2,2;1,3,3;", str);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());

    CommonChecks(intp);
  }

  public class Foo_ret_int  : BehaviorTreeTerminalNode
  {
    int ticks;
    int ret;

    public override void init()
    {
      var interp = Interpreter.instance;
      ticks = (int)interp.PopValue().num;
      ret = (int)interp.PopValue().num;
      interp.PopValue();
    }

    public override BHS execute()
    {
      if(ticks-- > 0)
        return BHS.RUNNING;
      Interpreter.instance.PushValue(DynVal.NewNum(ret));
      return BHS.SUCCESS;
    }
  }

  [IsTested()]
  public void TestUserClassMethodDecl()
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

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSeveralUserClassMethodDecl()
  {
    string bhl = @"

    class Foo {
      
      int a
      int b

      func int getA()
      {
        return this.b
      }

      func int getB() 
      {
        return this.a
      }
    }

    func int test()
    {
      Foo f = {}
      f.a = 10
      f.b = 10
      return f.getA() + f.getB()
    }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMethodSameNameLikeClass()
  {
    string bhl = @"

    class Foo {
      
      func int Foo()
      {
        return 0
      }
    }

    func int test()
    {
      Foo f = {}
      return f.Foo()
    }
    ";

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMethodDeclVarLikeClassVar()
  {
    string bhl = @"
      class Foo {
        
        int a

        func int Foo()
        {
          a = 10
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

    var intp = Interpret(bhl, null);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidIsEncodedInUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func Foo test() 
    {
      return {}
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node);

    AssertEqual(res.val.num, (new HashedName("Foo")).n);
    //NOTE: returned value must be manually removed
    DynValDict.Del((res.val.obj as DynValDict));
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidIsEncodedInUserClassInHierarchy()
  {
    string bhl = @"

    class Foo { }
    class Bar : Foo { }
      
    func Bar test() 
    {
      return {}
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExecNode(node);

    AssertEqual(res.val.num, (new HashedName("Bar")).n);
    //NOTE: returned value must be manually removed
    DynValDict.Del((res.val.obj as DynValDict));
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func int test() 
    {
      return typeid(Foo)
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("Foo")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForBuiltinType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int)
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("int")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidForBuiltinArrType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(int[])
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, (new HashedName("int[]")).n);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidEqual()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Foo)
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidNotEqual()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      return typeid(Foo) == typeid(Bar)
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res == false);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidNotEqualArrType()
  {
    string bhl = @"

    func bool test() 
    {
      return typeid(int[]) == typeid(float[])
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res == false);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestTypeidBadType()
  {
    string bhl = @"

    func int test() 
    {
      return typeid(Foo)
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      @"type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestEmptyParenExpression()
  {
    string bhl = @"

    func void test() 
    {
      ().foo
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestGlobalVariableRead()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableWrite()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}

    func float bar()
    {
      return foo.b
    }
      
    func float test() 
    {
      foo.b = 101
      return bar()
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableAlreadyDeclared()
  {
    string bhl = @"

    int foo = 0
    int foo = 1
      
    func int test() 
    {
      return foo
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestLocalVariableHasPriorityOverGlobalOne()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    Foo foo = {b : 100}
      
    func float test() 
    {
      Foo foo = {b : 200}
      return foo.b
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 200);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithSubCall()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    func float bar(float f) {
      return f
    }

    Foo foo = {b : bar(100)}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithComplexSubCall()
  {
    string bhl = @"

    class Foo { 
      Color c
    }

    func any col() {
      Color c = {r: 100}
      return c
    }

    Foo foo = {c : (Color)col()}
      
    func float test() 
    {
      return foo.c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 100);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestGlobalVariableInitWithSubcallFail()
  {
    string bhl = @"

    class Foo { 
      float b
    }

    func float bar() {
      fail()
      return 100
    }

    Foo foo = {b : bar()}
      
    func float test() 
    {
      return foo.b
    }
    ";

    var error = false;
    try
    {
      Interpret(bhl);
    }
    catch(Exception)
    {
      error = true;
    }
    AssertTrue(error);
  }

  [IsTested()]
  public void TestImportMixed()
  {
    string bhl1 = @"
    import ""bhl3""
    func float what(float k)
    {
      return hey(k)
    }

    import ""bhl2""  
    func float test(float k) 
    {
      return bar(k) * what(k)
    }

    ";

    string bhl2 = @"
    func float bar(float k)
    {
      return k
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(2));
    //NodeDump(node);
    var res = ExecNode(node).val;

    AssertEqual(res.num, 4);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportWithCycles()
  {
    string bhl1 = @"
    import ""bhl2""  
    import ""bhl3""  

    func float test(float k) 
    {
      return bar(k)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    func float bar(float k)
    {
      return hey(k)
    }
    ";

    string bhl3 = @"
    import ""bhl2""

    func float hey(float k)
    {
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(23));
    //NodeDump(node);
    var res = ExecNode(node).val;

    AssertEqual(res.num, 23);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float bar() 
    {
      return 1
    }

    func float test() 
    {
      return bar()
    }
    ";

    string bhl2 = @"
    func float bar()
    {
      return 2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
      },
      @"already defined symbol 'bar'"
    );
  }

  [IsTested()]
  public void TestImportClass()
  {
    string bhl1 = @"
    import ""bhl2""  
    func float test(float k) 
    {
      Foo f = { x : k }
      return bar(f)
    }
    ";

    string bhl2 = @"
    import ""bhl3""  

    class Foo
    {
      float x
    }

    func float bar(Foo f)
    {
      return hey(f.x)
    }
    ";

    string bhl3 = @"
    func float hey(float k)
    {
      return k
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(42));
    //NodeDump(node);
    var res = ExecNode(node).val;

    AssertEqual(res.num, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportClassConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    class Bar { }

    func test() { }
    ";

    string bhl2 = @"
    class Bar { }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportEnum()
  {
    string bhl1 = @"
    import ""bhl2""  

    func float test() 
    {
      Foo f = Foo::B
      return bar(f)
    }
    ";

    string bhl2 = @"
    enum Foo
    {
      A = 2
      B = 3
    }

    func int bar(Foo f)
    {
      return (int)f * 10
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    //NodeDump(node);
    var res = ExecNode(node).val;

    AssertEqual(res.num, 30);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportEnumConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    enum Bar { 
      FOO = 1
    }

    func test() { }
    ";

    string bhl2 = @"
    enum Bar { 
      BAR = 2
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
      },
      @"already defined symbol 'Bar'"
    );
  }

  [IsTested()]
  public void TestImportGlobalVar()
  {
    string bhl1 = @"
    import ""bhl3""  
    func float test() 
    {
      return foo.x
    }
    ";

    string bhl2 = @"

    class Foo
    {
      float x
    }

    ";

    string bhl3 = @"
    import ""bhl2""  

    Foo foo = {x : 10}

    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);
    
    var intp = CompileFiles(files, globs);

    var node = intp.GetFuncCallNode("bhl1", "test");
    //NodeDump(node);
    var res = ExecNode(node).val;

    AssertEqual(res.num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportGlobalVarConflict()
  {
    string bhl1 = @"
    import ""bhl2""  

    int foo = 10

    func test() { }
    ";

    string bhl2 = @"
    float foo = 100
    ";

    var globs = SymbolTable.CreateBuiltins();

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    
    AssertError<UserError>(
      delegate() { 
        CompileFiles(files, globs);
      },
      @"already defined symbol 'foo'"
    );
  }

  [IsTested()]
  public void TestImportGlobalExecutionOnlyOnce()
  {
    string bhl1 = @"
    import ""bhl2""  
    import ""bhl2""

    func test1() { }
    ";

    string bhl2 = @"
    func int dummy()
    {
      trace(""once"")
      return 1
    }

    int a = dummy()

    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);

    var intp = CompileFiles(files, globs);
    intp.LoadModule("bhl1");

    var str = GetString(trace_stream);
    AssertEqual("once", str);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestImportGlobalExecutionOnlyOnceNested()
  {
    string bhl1 = @"
    import ""bhl2""  

    func test1() { }
    ";

    string bhl2 = @"
    import ""bhl3""

    func int dummy()
    {
      trace(""once"")
      return 1
    }

    class Foo {
      int a
    }

    Foo foo = { a : dummy() }

    func test2() { }
    ";

    string bhl3 = @"
    func test3() { }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);
    TestNewFile("bhl3.bhl", bhl3, files);

    var intp = CompileFiles(files, globs);
    intp.LoadModule("bhl1");

    var str = GetString(trace_stream);
    AssertEqual("once", str);

    CommonChecks(intp);
  }

  static int Fib(int x)
  {
    if(x == 0)
      return 0;
    else 
    {
      if(x == 1) 
        return 1;
      else
        return Fib(x-1) + Fib(x-2);
    }
  }

  [IsTested()]
  public void TestFib()
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
      
    func int test(int x) 
    {
      return fib(x)
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    const int x = 15;

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(ExecNode(node));
      stopwatch.Stop();
      Console.WriteLine("bhl fib ticks: {0}", stopwatch.ElapsedTicks);
    }

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      node.SetArgs(DynVal.NewNum(x));
      ExtractNum(ExecNode(node));
      stopwatch.Stop();
      Console.WriteLine("bhl fib ticks2: {0}", stopwatch.ElapsedTicks);
    }
    CommonChecks(intp);

    {
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      Fib(x);
      stopwatch.Stop();
      Console.WriteLine("C# fib ticks: {0}", stopwatch.ElapsedTicks);
    }
  }

  ////////////////////////////////////////////////

  static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/tests";
  }

  static void TestCleanDir()
  {
    string dir = TestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }

  static void TestNewFile(string path, string text, List<string> files)
  {
    string full_path = TestDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
  }

  static void SharedInit()
  {
    DynVal.PoolClear();
    DynValList.PoolClear();
    DynValDict.PoolClear();
    FuncCallNode.PoolClear();
    FuncCtx.PoolClear();
  }

  static Interpreter CompileFiles(List<string> test_files, GlobalScope globs = null)
  {
    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;
    //NOTE: we want interpreter to work with original globs
    var globs_copy = globs.Clone();
    SharedInit();

    var conf = new BuildConf();
    conf.compile_fmt = CompileFormat.AST;
    conf.globs = globs;
    conf.files = test_files;
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_dir = TestDirPath();
    conf.cache_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = false;
    conf.debug = true;

    var bld = new Build();
    int res = bld.Exec(conf);
    if(res != 0)
      throw new UserError(File.ReadAllText(conf.err_file));

    var intp = Interpreter.instance;
    var bin = new MemoryStream(File.ReadAllBytes(conf.res_file));
    var mloader = new ModuleLoader(bin);

    intp.Init(globs_copy, mloader);

    return intp;
  }

  static void NodeDump(BehaviorTreeNode node)
  {
    Util.NodeDump(node);
  }

  public struct Result
  {
    public BHS status;
    public DynVal[] vals;

    public DynVal val 
    {
      get {
        return vals != null ? vals[0] : null;
      }
    }
  }

  static Result ExecNode(BehaviorTreeNode node, int ret_vals = 1, bool keep_running = true)
  {
    Result res = new Result();

    res.status = BHS.NONE;
    while(true)
    {
      res.status = node.run();
      if(res.status != BHS.RUNNING)
        break;

      if(!keep_running)
        break;
    }
    if(ret_vals > 0)
    {
      res.vals = new DynVal[ret_vals];
      for(int i=0;i<ret_vals;++i)
        res.vals[i] = Interpreter.instance.PopValue();
    }
    else
      res.vals = null;
    return res;
  }

  static Interpreter Interpret(string src, GlobalScope globs = null, bool show_ast = false)
  {
    globs = globs == null ? SymbolTable.CreateBuiltins() : globs;
    //NOTE: we want interpreter to work with original globs
    var globs_copy = globs.Clone();
    SharedInit();

    var intp = Interpreter.instance;
    intp.Init(globs_copy, null/*empty module loader*/);

    var mreg = new ModuleRegistry();
    //fake module for this specific case
    var mod = new bhl.Module("", "");
    var bin = new MemoryStream();
    Frontend.Source2Bin(mod, src.ToStream(), bin, globs, mreg);
    bin.Position = 0;

    var ast = Util.Bin2Meta<AST_Module>(bin);
    if(show_ast)
      Util.ASTDump(ast);

    intp.Interpret(ast);
  
    return intp;
  }

  void CommonChecks(Interpreter intp)
  {
    intp.glob_mem.Clear();

    //for extra debug
    //Console.WriteLine(DynVal.PoolDump());

    if(intp.stack.Count > 0)
    {
      Console.WriteLine("=== Dangling stack values ===");
      for(int i=0;i<intp.stack.Count;++i)
        Console.WriteLine("Stack value #" + i + " " + intp.stack[i]);
    }
    AssertEqual(intp.stack.Count, 0);
    AssertEqual(intp.node_ctx_stack.Count, 0);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    AssertEqual(DynValDict.PoolCount, DynValDict.PoolCountFree);
    AssertEqual(FuncCtx.PoolCount, FuncCtx.PoolCountFree);
  }

  static double ExtractNum(Result res)
  {
    return res.val.num;
  }

  static bool ExtractBool(Result res)
  {
    return res.val.bval;
  }

  static string ExtractStr(Result res)
  {
    return res.val.str;
  }

  static object ExtractObj(Result res)
  {
    return res.val.obj;
  }
}


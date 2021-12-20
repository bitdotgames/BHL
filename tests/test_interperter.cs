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

  void BindColorAlpha(GlobalScope globs, bool bind_parent = true)
  {
    if(bind_parent)
      BindColor(globs);

    {
      var cl = new ClassSymbolNative("ColorAlpha", globs.Type("Color"),
        delegate(ref DynVal v) 
        { 
          v.obj = new ColorAlpha();
        }
      );

      globs.Define(cl);

      cl.Define(new FieldSymbol("a", globs.Type("float"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var c = (ColorAlpha)ctx.obj;
          v.SetNum(c.a);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var c = (ColorAlpha)ctx.obj;
          c.a = (float)v.num; 
          ctx.obj = c;
        }
      ));

      {
        var m = new FuncSymbolSimpleNative("mult_summ_alpha", globs.Type("float"),
          delegate()
          {
            var interp = Interpreter.instance;

            var c = (ColorAlpha)interp.PopValue().obj;

            interp.PushValue(DynVal.NewNum((c.r * c.a) + (c.g * c.a)));

            return BHS.SUCCESS;
          }
        );

        cl.Define(m);
      }
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

  void BindFoo(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("Foo",
        delegate(ref DynVal v) 
        { 
          v.obj = new Foo();
        }
      );
      globs.Define(cl);
      globs.Define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

      cl.Define(new FieldSymbol("hey", globs.Type("int"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.SetNum(f.hey);
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.hey = (int)v.num; 
          ctx.obj = f;
        }
      ));
      cl.Define(new FieldSymbol("colors", globs.Type("Color[]"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.colors;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.colors = (List<Color>)v.obj; 
          ctx.obj = f;
        }
      ));
      cl.Define(new FieldSymbol("sub_color", globs.Type("Color"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (Foo)ctx.obj;
          v.obj = f.sub_color;
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (Foo)ctx.obj;
          f.sub_color = (Color)v.obj; 
          ctx.obj = f;
        }
      ));
    }
  }

  public class DynValContainer
  {
    public DynVal dv;
  }

  void BindDynValContainer(GlobalScope globs, DynValContainer c)
  {
    {
      var cl = new ClassSymbolNative("DynValContainer",
        delegate(ref DynVal v) 
        { 
          v.obj = new DynValContainer();
        }
      );
      globs.Define(cl);

      cl.Define(new FieldSymbol("dv", globs.Type("any"),
        delegate(DynVal ctx, ref DynVal v)
        {
          var f = (DynValContainer)ctx.obj;
          if(f.dv != null)
          {
            v.TryDel();
            v = f.dv;
          }
          else
          {
            v.SetNil();
          }
        },
        delegate(ref DynVal ctx, DynVal v)
        {
          var f = (DynValContainer)ctx.obj;
          f.dv = DynVal.Assign(f.dv, v);
          ctx.obj = f;
        }
      ));
    }

    {
      var fn = new FuncSymbolSimpleNative("get_dv_container", globs.Type("DynValContainer"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewObj(c)); return BHS.SUCCESS; } );

      globs.Define(fn);
    }
  }

  void BindFooLambda(GlobalScope globs)
  {
    {
      var cl = new ClassSymbolNative("FooLambda",
        delegate(ref DynVal v) 
        { 
          v.obj = new FooLambda();
        }
      );
      globs.Define(cl);
      globs.Define(new ArrayTypeSymbolT<Foo>(globs, new TypeRef(cl), delegate() { return new List<Foo>(); } ));

      cl.Define(new FieldSymbol("script", globs.Type("void^()"),
          delegate(DynVal ctx, ref DynVal v) {
            var f = (FooLambda)ctx.obj;
            v.obj = f.script.Count == 0 ? null : ((BaseLambda)(f.script[0])).fct.obj;
          },
          delegate(ref DynVal ctx, DynVal v) {
            var f = (FooLambda)ctx.obj;
            var fctx = (FuncCtx)v.obj;
            if(f.script.Count == 0) f.script.Add(new BaseLambda()); ((BaseLambda)(f.script[0])).fct.obj = fctx;
            ctx.obj = f;
          }
     ));
    }
  }

  [IsTested()]
  public void TestPassingDynValToBindClass()
  {
    string bhl = @"

    class Foo
    {
      int b
    }

    func int test() 
    {
      Foo f1 = {b: 14} 
      Foo f2 = {b: 10} 

      DynValContainer c = get_dv_container()
      if(c.dv != null) {
        fail()
      }
      c.dv = f1
      c.dv = f2

      Foo tmp = (Foo)get_dv_container().dv
      return tmp.b
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 10);

    c.dv.Release();

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValToBindClassAnyNull()
  {
    string bhl = @"

    func bool test() 
    {
      DynValContainer c = get_dv_container()
      any foo = c.dv
      return foo == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValToBindClassAssignNull()
  {
    string bhl = @"

    func bool test() 
    {
      DynValContainer c = get_dv_container()
      c.dv = null
      return c.dv == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var c = new DynValContainer();

    BindDynValContainer(globs, c);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);

    c.dv.Release();

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDynValListOwnership()
  {
    var lst = DynValList.New();

    {
      var dv = DynVal.New();
      lst.Add(dv);
      AssertEqual(dv.refs, 1);

      lst.Clear();
      AssertEqual(dv.refs, -1);
    }

    {
      var dv = DynVal.New();
      lst.Add(dv);
      AssertEqual(dv.refs, 1);

      lst.RemoveAt(0);
      AssertEqual(dv.refs, -1);

      lst.Clear();
      AssertEqual(dv.refs, -1);
    }

    {
      var dv0 = DynVal.New();
      var dv1 = DynVal.New();
      lst.Add(dv0);
      lst.Add(dv1);
      AssertEqual(dv0.refs, 1);
      AssertEqual(dv1.refs, 1);

      lst.RemoveAt(1);
      AssertEqual(dv0.refs, 1);
      AssertEqual(dv1.refs, -1);

      lst.Clear();
      AssertEqual(dv0.refs, -1);
      AssertEqual(dv1.refs, -1);
    }

    DynValList.Del(lst);

    AssertEqual(DynValList.PoolCount, DynValList.PoolCountFree);
    AssertEqual(DynVal.PoolCount, DynVal.PoolCountFree);
  }

  [IsTested()]
  public void TestNativeClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      Color[] cs = new Color[]
      Color c0 = new Color
      cs.Add(c0)
      cs.RemoveAt(0)
      Color c1 = new Color
      c1.r = 10
      Color c2 = new Color
      c2.g = 20
      cs.Add(c1)
      cs.Add(c2)
      Color c3 = new Color
      cs.Add(c3)
      cs[2].r = 30
      Color c4 = new Color
      cs.Add(c4)
      cs.RemoveAt(3)
      return (string)cs.Count + (string)cs[0].r + (string)cs[1].g + (string)cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExecNode(node).val;

    AssertEqual(res.str, "3102030");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNativeClassTmpArray()
  {
    string bhl = @"

    func Color[] mkarray()
    {
      Color[] cs = new Color[]
      Color c0 = new Color
      c0.g = 1
      cs.Add(c0)
      Color c1 = new Color
      c1.r = 10
      cs.Add(c1)
      return cs
    }
      
    func float test() 
    {
      return mkarray()[1].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  public class BaseScript
  {}

  public class TestPtr 
  {
    public object obj;
  }

  public class BaseLambda : BaseScript
  {
    public TestPtr fct = new TestPtr();
  }

  public class Foo
  {
    public int hey;
    public List<Color> colors = new List<Color>();
    public Color sub_color = new Color();

    public void reset()
    {
      hey = 0;
      colors.Clear();
      sub_color = new Color();
    }
  }

  public class FooLambda
  {
    public List<BaseScript> script = new List<BaseScript>();

    public void reset()
    {
      script.Clear();
    }
  }

  [IsTested()]
  public void TestNativeSubClassArray()
  {
    string bhl = @"
      
    func string test(float k) 
    {
      Foo f = new Foo
      f.hey = 10
      Color c1 = new Color
      c1.r = 20
      Color c2 = new Color
      c2.g = 30
      f.colors.Add(c1)
      f.colors.Add(c2)
      return (string)f.colors.Count + (string)f.hey + (string)f.colors[0].r + (string)f.colors[1].g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    node.SetArgs(DynVal.NewNum(2));
    var res = ExecNode(node).val;

    AssertEqual(res.str, "2102030");
    CommonChecks(intp);
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

  void BindAnswer42(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("answer42", globs.Type("int"),
          delegate() { Interpreter.instance.PushValue(DynVal.NewNum(42)); return BHS.SUCCESS; } );
      globs.Define(fn);
    }
  }

  //simple console outputting version
  void BindLog(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolSimpleNative("log", globs.Type("void"),
          delegate() { Console.WriteLine(Interpreter.instance.PopValue().str); return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("str", globs.Type("string")));

      globs.Define(fn);
    }
  }

  public class NodeWithLog : BehaviorTreeTerminalNode
  {
    Dictionary<int, BHS> ctl;
    Stream sm;
    int id;

    public NodeWithLog(Stream sm, Dictionary<int, BHS> ctl)
    {
      this.ctl = ctl;
      this.sm = sm;
    }

    public override void init()
    {
      var interp = Interpreter.instance;
      id = (int)interp.PopValue().num;

      var sw = new StreamWriter(sm);
      sw.Write("INIT(" + id + ");");
      sw.Flush();
    }

    public override void deinit()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEINIT(" + id + ");");
      sw.Flush();
    }

    public override BHS execute()
    {
      var sw = new StreamWriter(sm);
      sw.Write("EXEC(" + id + ");");
      sw.Flush();
      return ctl[id];
    }

    public override void defer()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER(" + id + ");");
      sw.Flush();
    }
  }

  void BindNodeWithLog(GlobalScope globs, MemoryStream s, Dictionary<int, BHS> ctl)
  {
    {
      var fn = new FuncSymbolNative("NodeWithLog", globs.Type("void"),
          delegate() { return new NodeWithLog(s, ctl); } );

      fn.Define(new FuncArgSymbol("id", globs.Type("int")));
      globs.Define(fn);
    }
  }

  public class NodeWithDefer : BehaviorTreeTerminalNode
  {
    Stream sm;

    public NodeWithDefer(Stream sm)
    {
      this.sm = sm;
    }

    public override BHS execute()
    {
      return BHS.SUCCESS;
    }

    public override void defer()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER!!!");
      sw.Flush();
    }
  }

  public class NodeWithDeferRetInt : BehaviorTreeTerminalNode
  {
    Stream sm;
    int n;

    public NodeWithDeferRetInt(Stream sm)
    {
      this.sm = sm;
    }

    public override void init()
    {
      n = (int)Interpreter.instance.PopValue().num;
    }

    public override BHS execute()
    {
      Interpreter.instance.PushValue(DynVal.NewNum(n));
      return BHS.SUCCESS;
    }

    public override void defer()
    {
      var sw = new StreamWriter(sm);
      sw.Write("DEFER " + n + "!!!");
      sw.Flush();
    }
  }

  void BindNodeWithDefer(GlobalScope globs, MemoryStream s)
  {
    {
      var fn = new FuncSymbolNative("NodeWithDefer", globs.Type("void"),
          delegate() { return new NodeWithDefer(s); } );

      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolNative("NodeWithDeferRetInt", globs.Type("int"),
          delegate() { return new NodeWithDeferRetInt(s); } );

      fn.Define(new FuncArgSymbol("n", globs.Type("int")));
      globs.Define(fn);
    }
  }

  void BindWaitTicks(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("WaitTicks", globs.Type("void"),
          delegate() { return new WaitTicksNode(); } );
      fn.Define(new FuncArgSymbol("ticks", globs.Type("int")));
      fn.Define(new FuncArgSymbol("is_success", globs.Type("bool")));

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

  void BindStartScript(GlobalScope globs)
  {
    {
      var fn = new FuncSymbolNative("StartScript", globs.Type("void"),
          delegate() { return new StartScriptNode(); } );
      fn.Define(new FuncArgSymbol("script", globs.Type("void^()")));

      globs.Define(fn);
    }
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
  public void TestStartLambdaInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      while(true) {
        StartScriptInMgr(
          script: func() { 
            trace(""HERE;"") 
          },
          num : 1,
          now : false
        )
        yield()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(0, cs.Count); 
    }

    //NodeDump(node);

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(0, cs.Count); 
    }

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(1, FuncCtx.NodesCreated);
    node.stop();
    CommonChecks(intp);
  }
  
  [IsTested()]
  public void TestStartLambdaRunninInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      while(true) {
        StartScriptInMgr(
          script: func() { 
            trace(""HERE;"") 
            suspend()
          },
          num : 1,
          now : false
        )
        yield()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScriptInMgr(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(1, cs.Count); 
    }

    //NodeDump(node);

    {
      var status = node.run();
      AssertEqual(status, BHS.RUNNING);

      ScriptMgr.instance.run();

      var str = GetString(trace_stream);
      AssertEqual("HERE;HERE;", str);

      var cs = ScriptMgr.instance.getChildren();
      AssertEqual(2, cs.Count); 
      AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    }

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(2, FuncCtx.NodesCreated);
    node.stop();
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: func() { 
          suspend()
        },
        num : 3,
        now : false
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindStartScriptInMgr(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    ScriptMgr.instance.run();

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    AssertTrue(cs[1].GetHashCode() != cs[2].GetHashCode());
    AssertTrue(cs[0].GetHashCode() != cs[2].GetHashCode());

    //NodeDump(node);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    AssertEqual(3, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      void^() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: fn,
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    AssertEqual(2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartBindFuncPtrManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: say_here,
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolSimpleNative("say_here", globs.Type("void"), 
          delegate()
          {
            AddString(trace_stream, "HERE;");
            return BHS.RUNNING;
          }
          );
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaVarManyTimesInScriptMgr()
  {
    string bhl = @"

    func void test() 
    {
      void^() fn = func() {
        trace(""HERE;"")
        suspend()
      }

      StartScriptInMgr(
        script: func() { 
          fn()
        },
        num : 2,
        now : true
      )
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindStartScriptInMgr(globs);
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(2, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("HERE;HERE;", str);

    ScriptMgr.instance.stop();
    AssertTrue(!ScriptMgr.instance.busy());

    AssertEqual(2+2, FuncCtx.NodesCreated);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestLambdaIsKeptInFuncCallScope()
  {
    string bhl = @"

    func void test() 
    {
      StartScriptInMgr(
        script: func() { 
          trace(""DO;"")
        },
        num : 2,
        now : true
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
    AssertTrue(!ScriptMgr.instance.busy());

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestStartLambdaManyTimesInScriptMgrWithUseVals()
  {
    string bhl = @"

    func void test() 
    {
      float a = 0
      StartScriptInMgr(
        script: func() { 
          a = a + 1
          trace((string) a + "";"") 
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

    var cs = ScriptMgr.instance.getChildren();
    AssertEqual(3, cs.Count); 
    AssertTrue(cs[0].GetHashCode() != cs[1].GetHashCode());
    AssertTrue(cs[1].GetHashCode() != cs[2].GetHashCode());
    AssertTrue(cs[0].GetHashCode() != cs[2].GetHashCode());

    //NodeDump(node);

    var str = GetString(trace_stream);
    AssertEqual("1;1;1;", str);

    ScriptMgr.instance.stop();

    AssertTrue(!ScriptMgr.instance.busy());
    CommonChecks(intp);
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
  
  [IsTested()]
  public void TestYieldWhileInParal()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      paral {
        yield while(i < 3)
        while(true) {
          i = i + 1
          yield()
        }
      }
      return i
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.RUNNING, status);

    status = node.run();
    AssertEqual(BHS.SUCCESS, status);

    var val = intp.PopValue();
    AssertEqual(3, val.num);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFuncCaching()
  {
    string bhl = @"
      
    func float foo(float k)
    {
      return k
    }

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(3));
      var res = ExtractNum(ExecNode(node));

      AssertEqual(res, 3);
      CommonChecks(intp);
    }

    AssertEqual(FuncCallNode.PoolCount, 1);

    {
      var node = intp.GetFuncCallNode("test");
      node.SetArgs(DynVal.NewNum(30));
      var res = ExtractNum(ExecNode(node));

      AssertEqual(res, 30);
      CommonChecks(intp);
    }

    AssertEqual(FuncCallNode.PoolCount, 1);
  }

  [IsTested()]
  public void TestFuncCachingAndClearingPool()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func test() 
    {
      foo()
    }
    ";

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    {
      var node = intp.GetFuncCallNode("test");
      var status = node.run();
      AssertEqual(BHS.RUNNING, status);

      FuncCallNode.PoolClear();

      status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }

    AssertEqual(FuncCallNode.PoolCount, 0);
  }

  [IsTested()]
  public void TestFuncCachingAndClearingPoolWithInterleaving()
  {
    string bhl = @"
      
    func foo()
    {
      yield()
    }

    func bar()
    {
      yield()
    }

    func test1() 
    {
      foo()
    }

    func test2() 
    {
      bar()
    }
    ";

    var intp = Interpret(bhl);

    AssertEqual(FuncCallNode.PoolCount, 0);

    var node1 = intp.GetFuncCallNode("test1");
    var status = node1.run();
    AssertEqual(BHS.RUNNING, status);

    FuncCallNode.PoolClear();

    var node2 = intp.GetFuncCallNode("test2");
    status = node2.run();
    AssertEqual(BHS.RUNNING, status);
    AssertEqual(FuncCallNode.PoolCount, 1);

    //NOTE: at this point the node will try to free the cached pool item,
    //      however its version of pool item is outdated
    status = node1.run();
    AssertEqual(BHS.SUCCESS, status);

    status = node2.run();
    AssertEqual(BHS.SUCCESS, status);

    AssertEqual(FuncCallNode.PoolCount, 1);
  }

  [IsTested()]
  public void TestDetachedUserlandFuncsAreNotDoubleDeferred()
  {
    string bhl = @"
      
    func void foo(int n, bool keep_running)
    { 
      if(keep_running) {
        suspend()
      }
      NodeWithDeferRetInt(n)
    }

    func void test() 
    {
      paral_all {
        seq {
          foo(10, false)  //1st time, cached
          yield()
          yield()         
          //defer is triggered for parent seq, but MUST not be triggered for cached foo(),
          //at this moment cached foo() is already running in the block below, if we call
          //defer on it, it will invalidate its state and on the next run there will be
          //exception (it will try to pop the passed arguments again)
        }
        seq {
          yield()         //wait one tick
          paral {
            foo(20, true) //userland func is taken from cache, keeps ticking
            seq {         //let's interrupt paral after two ticks
              yield()
              yield()
            }
          }
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);
    BindNodeWithDefer(globs, trace_stream);

    var intp = Interpret(bhl, globs);

    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("DEFER 10!!!", str);

    CommonChecks(intp);
  }

  [IsTested()]
  public void TestBindClassCallMember()
  {
    string bhl = @"

    func float foo(float a)
    {
      return mkcolor(a).r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColor(globs);

    var intp = Interpret(bhl, globs);

    var foo = intp.GetFuncCallNode("foo");
    foo.SetArgs(DynVal.NewNum(10));
    var res = ExtractNum(ExecNode(foo));
    AssertEqual(res, 10);
    CommonChecks(intp);

    foo.SetArgs(DynVal.NewNum(20));
    res = ExtractNum(ExecNode(foo));
    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfTrue()
  {
    string bhl = @"

    func test() 
    {
      if(true) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestDanglingBrackets()
  {
    string bhl = @"

    func test() 
    {
      ()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "mismatched input '(' expecting '}'"
    );
  }

  [IsTested()]
  public void TestDanglingBrackets2()
  {
    string bhl = @"

    func foo() 
    {
    }

    func test() 
    {
      foo() ()
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "no func to call"
    );
  }

  [IsTested()]
  public void TestIfTrueComplexCondition()
  {
    string bhl = @"

    func test() 
    {
      if(true && true) {
        trace(""OK"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestIfFalse()
  {
    string bhl = @"

    func test() 
    {
      if(false) {
        trace(""NEVER"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullPassedAsNullObj()
  {
    string bhl = @"
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStringClass(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewObj(null));
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullPassedAsNewNil()
  {
    string bhl = @"
      
    func void test(StringClass c) 
    {
      if(c != null) {
        trace(""NEVER;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStringClass(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNil());
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  //TODO: do we really need this behavior supported?
  //[IsTested()]
  public void TestNullPassedAsCustomNull()
  {
    string bhl = @"
      
    func void test(CustomNull c) 
    {
      if(c != null) {
        trace(""NOTNULL;"")
      }
      if(c == null) {
        trace(""NULL;"")
      }

      yield()

      if(c != null) {
        trace(""NOTNULL2;"")
      }
      if(c == null) {
        trace(""NULL2;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindCustomNull(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var cn = new CustomNull();
    node.SetArgs(DynVal.NewObj(cn));

    cn.is_null = false;
    node.run();

    cn.is_null = true;
    node.run();

    var str = GetString(trace_stream);
    AssertEqual("NOTNULL;NULL2;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestOrShortCircuit()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      if(c == null || c.r == 0) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuit()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = null
      if(c != null && c.r == 0) {
        trace(""NEVER;"")
      } else {
        trace(""OK;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNot()
  {
    string bhl = @"
      
    func void test() 
    {
      bool ready = false
      bool activated = true
      if(!ready && activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestAndShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func void test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(!foo.ready && foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestOrShortCircuitWithBoolNotForClassMembers()
  {
    string bhl = @"

    class Foo 
    {
      bool ready
      bool activated
    }
      
    func void test() 
    {
      Foo foo = {}
      foo.ready = false
      foo.activated = true
      if(foo.ready || foo.activated) {
        trace(""OK;"")
      } else {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("OK;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestSetNullObjFromUserBinding()
  {
    string bhl = @"
      
    func void test() 
    {
      Color c = mkcolor_null()
      if(c == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullIncompatible()
  {
    string bhl = @"
      
    func bool test() 
    {
      return 0 == null
    }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestNullArray()
  {
    string bhl = @"
      
    func void test() 
    {
      Color[] cs = null
      Color[] cs2 = new Color[]
      if(cs == null) {
        trace(""NULL;"")
      }
      if(cs2 == null) {
        trace(""NULL2;"")
      }
      if(cs2 != null) {
        trace(""NOT NULL;"")
      }
      cs = cs2
      if(cs2 == cs) {
        trace(""EQ;"")
      }
      if(cs2 == null) {
        trace(""NEVER;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;EQ;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullArrayByDefault()
  {
    string bhl = @"
      
    func void test() 
    {
      Color[] cs
      if(cs == null) {
        trace(""NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNullFuncPtr()
  {
    string bhl = @"
      
    func void test() 
    {
      void^() fn = null
      void^() fn2 = func () { }
      if(fn == null) {
        trace(""NULL;"")
      }
      if(fn != null) {
        trace(""NEVER;"")
      }
      if(fn2 == null) {
        trace(""NEVER2;"")
      }
      if(fn2 != null) {
        trace(""NOT NULL;"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("NULL;NOT NULL;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileEmptyArrLoop()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = []
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileArrLoop()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = [1,2,3]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        trace((string)tmp)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestNestedWhileArrLoopWithVarDecls()
  {
    string bhl = @"

    func test() 
    {
      int[] arr = [1,2]
      int i = 0
      while(i < arr.Count) {
        int tmp = arr[i]
        int j = 0
        while(j < arr.Count) {
          int tmp2 = arr[j]
          trace((string)tmp + "","" + (string)tmp2 + "";"")
          j = j + 1
        }
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,1;1,2;2,1;2,2;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileManyTimesAtOneRun()
  {
    string bhl = @"

    func int test() 
    {
      int i = 0
      while(i < 1000) {
        i = i + 1
      }
      return i
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    {
      var status = node.run();
      AssertEqual(BHS.SUCCESS, status);
    }
    var res = intp.PopValue();

    AssertEqual(1000, res.num);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileCounterOnTheTop()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        i = i + 1
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestWhileFalse()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(false) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestFor()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForMultiExpression()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0, int j = 1; i < 3; i = i + 1, j = j + 2) {
        trace((string)(i*j) + "";"")
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("0;3;10;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForReverse()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 2; i >= 0; i = i - 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("210", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForUseExternalVar()
  {
    string bhl = @"

    func test() 
    {
      int i
      for(i = 1; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("12", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForNested()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        for(int j = 0; j < 2; j = j + 1) {
          trace((string)i + "","" + (string)j + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("0,0;0,1;1,0;1,1;2,0;2,1;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForSeveral()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0; i < 3; i = i + 1) {
        trace((string)i)
      }

      for(i = 0; i < 30; i = i + 10) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("01201020", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        for(int i = 0; i < 3; i = i + 1) {
          trace((string)i)
          yield()
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.SUCCESS, node.run());

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForBadPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(i ; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      // "mismatched input ';' expecting '='"
      "no viable alternative at input 'i ;"
    );
  }

  [IsTested()]
  public void TestForEmptyPreSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3; i = i + 1) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForEmptyPostSection()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      for(; i < 3;) {
        trace((string)i)
        i = i + 1
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("012", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForBadPostSection()
  {
    string bhl = @"

    func test() 
    {
      for(int i = 0 ; i < 3; i) {
        trace((string)i)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      // "mismatched input ')' expecting '='"
      "no viable alternative at input 'i)'"
    );
  }

  [IsTested()]
  public void TestForCondIsRequired()
  {
    string bhl = @"

    func test() 
    {
      for(;;) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "no viable alternative at input ';'"
    );
  }

  [IsTested()]
  public void TestForNonBoolCond()
  {
    string bhl = @"

    func int foo() 
    {
      return 14
    }

    func test() 
    {
      for(; foo() ;) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "foo():<int> have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeach()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1, 2, 3]
      foreach(is as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachForCustomArrayBinding()
  {
    string bhl = @"

    func test() 
    {
      Color[] cs = [{r:1}, {r:2}, {r:3}]
      foreach(cs as Color c) {
        trace((string)c.r)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachUseExternalIteratorVar()
  {
    string bhl = @"

    func test() 
    {
      int it
      int[] is = [1, 2, 3]
      foreach(is as it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachWithInPlaceArr()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachWithReturnedArr()
  {
    string bhl = @"

    func int[] foo()
    {
      return [1,2,3]
    }

    func test() 
    {
      foreach(foo() as int it) {
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachInParal()
  {
    string bhl = @"

    func test() 
    {
      paral {
        foreach([1,2,3] as int it) {
          trace((string)it)
          yield()
        }
        suspend()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.RUNNING, node.run());
    AssertEqual(BHS.SUCCESS, node.run());

    var str = GetString(trace_stream);
    AssertEqual("123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachBreak()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        if(it == 3) {
          break
        }
        trace((string)it)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("12", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachSeveral()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1,2,3]
      foreach(is as int it) {
        trace((string)it)
      }

      foreach(is as int it2) {
        trace((string)it2)
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("123123", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachNested()
  {
    string bhl = @"

    func test() 
    {
      int[] is = [1,2,3]
      foreach(is as int it) {
        foreach(is as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,1;1,2;1,3;2,1;2,2;2,3;3,1;3,2;3,3;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachNested2()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as int it) {
        foreach([20,30] as int it2) {
          trace((string)it + "","" + (string)it2 + "";"")
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1,20;1,30;2,20;2,30;3,20;3,30;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestForeachIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      foreach([1,2,3] as string it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachArrBadType()
  {
    string bhl = @"

    func float foo()
    {
      return 14
    }

    func test() 
    {
      foreach(foo() as float it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachExternalIteratorVarBadType()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach([1,2,3] as it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "have incompatible types"
    );
  }

  [IsTested()]
  public void TestForeachRedeclareError()
  {
    string bhl = @"

    func test() 
    {
      string it
      foreach([1,2,3] as int it) {
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "already defined symbol 'it'"
    );
  }

  [IsTested()]
  public void TestBadBreak()
  {
    string bhl = @"

    func test() 
    {
      break
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "not within loop construct"
    );
  }

  [IsTested()]
  public void TestBadBreakInDefer()
  {
    string bhl = @"

    func test() 
    {
      while(true) {
        defer {
          break
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "not within loop construct"
    );
  }

  [IsTested()]
  public void TestWhileFailure()
  {
    string bhl = @"

    func test() 
    {
      int i = 0
      while(i < 3) {
        trace((string)i)
        i = i + 1
        if(i == 2) {
          fail()
        }
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    {
      var status = node.run();
      AssertEqual(BHS.FAILURE, status);
    }

    var str = GetString(trace_stream);
    AssertEqual("01", str);
    CommonChecks(intp);
  }

  public class MakeFooNode : BehaviorTreeTerminalNode
  {
    public override void init()
    {
      var interp = Interpreter.instance;
      var foo = interp.PopValue().ValueClone();
      interp.PushValue(foo);
    }
  }

  [IsTested()]
  public void TestJsonEmptyCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonPartialCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {g: 10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonFullCtor()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1, g: 10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 11);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonCtorNotExpectedMember()
  {
    string bhl = @"
    func void test()
    {
      Color c = {b: 10}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"no such attribute 'b' in class 'Color"
    );
  }

  [IsTested()]
  public void TestJsonCtorBadType()
  {
    string bhl = @"
    func void test()
    {
      Color c = {r: ""what""}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"float, @(4,20) ""what"":<string> have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonExplicitEmptyClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitNoSuchClass()
  {
    string bhl = @"
      
    func void test() 
    {
      any c = new Foo {}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"type 'Foo' not found"
    );
  }

  [IsTested()]
  public void TestJsonExplicitClass()
  {
    string bhl = @"
      
    func float test() 
    {
      ColorAlpha c = new ColorAlpha {a: 1, g: 10, r: 100}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonMasterStructWithClass()
  {
    string bhl = @"
      
    func string test() 
    {
      MasterStruct n = {
        child : {str : ""hey""},
        child2 : {str : ""hey2""}
      }
      return n.child2.str
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindMasterStruct(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractStr(ExecNode(node));

    AssertEqual(res, "hey2");
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonMasterStructWithStruct()
  {
    string bhl = @"
      
    func int test() 
    {
      MasterStruct n = {
        child_struct : {n: 1, n2:10},
        child_struct2 : {n: 2, n2:20}
      }
      return n.child_struct2.n
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindMasterStruct(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color c = new ColorAlpha {a: 1, g: 10, r: 100}
      ColorAlpha ca = (ColorAlpha)c
      return ca.r + ca.g + ca.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha c = new Color {g: 10, r: 100}
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonReturnObject()
  {
    string bhl = @"

    func Color make()
    {
      return {r: 42}
    }
      
    func float test() 
    {
      Color c = make()
      return c.r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 42);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonEmptyArrCtor()
  {
    string bhl = @"
    func int test()
    {
      int[] a = []
      return a.Count 
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrCtor()
  {
    string bhl = @"
    func int test()
    {
      int[] a = [1,2,3]
      return a[0] + a[1] + a[2]
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 6);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrComplexCtor()
  {
    string bhl = @"
    func float test()
    {
      Color[] cs = [{r:10, g:100}, {g:1000, r:1}]
      return cs[0].r + cs[0].g + cs[1].r + cs[1].g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 1111);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArg()
  {
    string bhl = @"
    func float foo(Color c = {})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultEmptyArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {}, float a = 10)
    {
      return c.r
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultArgWithOtherDefaultArgs()
  {
    string bhl = @"
    func float foo(Color c = {r:20}, float a = 10)
    {
      return c.r + a
    }

    func float test()
    {
      return foo(a : 20)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 40);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonDefaultArgIncompatibleType()
  {
    string bhl = @"
    func float foo(ColorAlpha c = new Color{r:20})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonArgTypeMismatch()
  {
    string bhl = @"
    func float foo(float a = 10)
    {
      return a
    }

    func float test()
    {
      return foo(a : {})
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"can't be specified with {..}"
    );
  }

  [IsTested()]
  public void TestJsonDefaultArg()
  {
    string bhl = @"
    func float foo(Color c = {r:10})
    {
      return c.r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrDefaultArg()
  {
    string bhl = @"
    func float foo(Color[] c = [{r:10}])
    {
      return c[0].r
    }

    func float test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrEmptyDefaultArg()
  {
    string bhl = @"
    func int foo(Color[] c = [])
    {
      return c.Count
    }

    func int test()
    {
      return foo()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));

    AssertEqual(num, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonObjReAssign()
  {
    string bhl = @"
    func float test()
    {
      Color c = {r: 1}
      c = {g:10}
      return c.r + c.g
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrReAssign()
  {
    string bhl = @"
    func float test()
    {
      float[] b = [1]
      b = [2,3]
      return b[0] + b[1]
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var num = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(num, 5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicit()
  {
    string bhl = @"
      
    func float test() 
    {
      Color[] cs = [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayReturn()
  {
    string bhl = @"

    func Color[] make()
    {
      return [new ColorAlpha {a:4}, {g:10}, new Color {r:100}]
    }
      
    func float test() 
    {
      Color[] cs = make()
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicitAsArg()
  {
    string bhl = @"

    func float foo(Color[] cs)
    {
      ColorAlpha ca = (ColorAlpha)cs[0]
      return ca.a + cs[1].g + cs[2].r
    }
      
    func float test() 
    {
      return foo([new ColorAlpha {a:4}, {g:10}, new Color {r:100}])
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 114);
    CommonChecks(intp);
  }


  [IsTested()]
  public void TestJsonArrayExplicitSubClass()
  {
    string bhl = @"
      
    func float test() 
    {
      Color[] cs = [{r:1,g:2}, new ColorAlpha {g: 10, r: 100, a:2}]
      ColorAlpha c = (ColorAlpha)cs[1]
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 112);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestJsonArrayExplicitSubClassNotCompatible()
  {
    string bhl = @"
      
    func void test() 
    {
      ColorAlpha[] c = [{r:1,g:2,a:100}, new Color {g: 10, r: 100}]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    
    BindColorAlpha(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      @"have incompatible types"
    );
  }

  [IsTested()]
  public void TestJsonFuncArg()
  {
    string bhl = @"
    func void test(float b) 
    {
      Foo f = MakeFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]})
      trace((string)f.hey + (string)f.colors.Count + (string)f.colors[0].r + (string)f.colors[1].g + (string)f.colors[2].g)
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); } );
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(42));
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("14232342", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParal()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, ret_int(val: 2, ticks: 1))
          suspend()
        }
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      int^(int,int) p = ret_int
      paral {
        seq {
          foo(1, p(2, 1))
          suspend()
        }
        foo(10, p(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithMemberPtrCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    class Bar { 
      int^(int,int) ptr
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      Bar b = {}
      b.ptr = ret_int
      paral {
        seq {
          foo(1, b.ptr(2, 1))
          suspend()
        }
        foo(10, b.ptr(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalWithLambdaCall()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, 
              func int (int val, int ticks) {
                while(ticks > 0) {
                  yield()
                  ticks = ticks - 1
                }
                return val
              }(2, 1))
          suspend()
        }
        foo(10, 
            func int (int val, int ticks) {
              while(ticks > 0) {
                yield()
                ticks = ticks - 1
              }
              return val
            }(20, 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
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
  public void TestInterleaveValuesStackInParalWithMethods()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func void test() 
    {
      paral {
        seq {
          foo(1, (new Foo).self().ret_int(val: 2, ticks: 1))
          suspend()
        }
        foo(10, (new Foo).self().ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var cl = new ClassSymbolNative("Foo",
        delegate(ref DynVal v) 
        { 
          //fake object
          v.obj = null;
        }
      );

      globs.Define(cl);

      {
        var m = new FuncSymbolSimpleNative("self", globs.Type("Foo"),
          delegate()
          {
            var interp = Interpreter.instance;
            var obj = interp.PopValue().obj;
            interp.PushValue(DynVal.NewObj(obj));
            return BHS.SUCCESS;
          }
        );
        cl.Define(m);
      }

      {
        var m = new FuncSymbolNative("ret_int", globs.Type("int"),
          delegate() { return new Foo_ret_int(); }
        );
        m.Define(new FuncArgSymbol("val", globs.Type("int")));
        m.Define(new FuncArgSymbol("ticks", globs.Type("int")));
        cl.Define(m);
      }

    }

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveValuesStackInParalAll()
  {
    string bhl = @"
    func foo(int a, int b)
    {
      trace((string)a + "" "" + (string)b + "";"")
    }

    func int ret_int(int val, int ticks)
    {
      while(ticks > 0)
      {
        yield()
        ticks = ticks - 1
      }
      return val
    }

    func void test() 
    {
      paral_all {
        foo(1, ret_int(val: 2, ticks: 1))
        foo(10, ret_int(val: 20, ticks: 2))
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var trace_stream = new MemoryStream();
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);
    AssertEqual("1 2;10 20;", str);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsStack()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func hey(int a, int b)
    {
    }

    func void test() 
    {
      hey(1, foo(10))
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFail()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      hey(""bar"", foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYield()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 100
    }

    func hey(string b, int a)
    {
    }

    func test() 
    {
      hey(""bar"", 1 + foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForYieldInWhile()
  {
    string bhl = @"

    func int foo()
    {
      yield()
      return 1
    }

    func doer(ref int c)
    {
      while(c < 2) {
        c = c + foo()
      }
    }

    func test() 
    {
      int c = 0
      doer(ref c)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NOTE: making several runs
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();

    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.SUCCESS);

    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.RUNNING);
    AssertEqual(node.run(), BHS.SUCCESS);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStack()
  {
    string bhl = @"

    func hey(int n, int m)
    {
    }

    func int bar()
    {
      return 1
    }

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(bar(), foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBind()
  {
    string bhl = @"

    func int foo()
    {
      fail()
      return 100
    }

    func test() 
    {
      hey(""bar"", foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolSimpleNative("hey", globs.Type("void"),
          delegate() { return BHS.SUCCESS; } );
      fn.Define(new FuncArgSymbol("s", globs.Type("string")));
      fn.Define(new FuncArgSymbol("i", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackUserBindInBinOp()
  {
    string bhl = @"
    func test() 
    {
      bool res = foo(false) != bar(4)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    {
      var fn = new FuncSymbolSimpleNative("foo", globs.Type("int"),
          delegate() { 
            Interpreter.instance.PopValue();
            Interpreter.instance.PushValue(DynVal.NewNum(42));
            return BHS.SUCCESS; 
          } );
      fn.Define(new FuncArgSymbol("b", globs.Type("bool")));
      globs.Define(fn);
    }

    {
      var fn = new FuncSymbolSimpleNative("bar", globs.Type("int"),
          delegate() { 
            Interpreter.instance.PopValue();
            return BHS.FAILURE; 
          } );
      fn.Define(new FuncArgSymbol("n", globs.Type("int")));
      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(BHS.FAILURE, node.run());
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncThisArgOnStackForMethod()
  {
    string bhl = @"

    func float foo()
    {
      fail()
      return 10
    }

    func test() 
    {
      Color c = new Color
      c.mult_summ(foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtr()
  {
    string bhl = @"
    func int foo(int v) 
    {
      fail()
      return v
    }

    func bar()
    {
      int^(int) p = foo
      Foo f = MakeFoo({hey:1, colors:[{r:p(42)}]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  public class NodeTakingFunc : BehaviorTreeTerminalNode
  {
    MemoryStream stream;
    FuncCtx fct;

    public NodeTakingFunc(MemoryStream stream)
    {
      this.stream = stream;
    }

    public override void init()
    {
      var interp = Interpreter.instance;
      fct = (FuncCtx)interp.PopValue().obj;
      fct = fct.AutoClone();
      fct.Retain();
    }

    public override void deinit()
    {
      fct.Release();
      fct = null;
    }

    public override BHS execute()
    {
      var interp = Interpreter.instance;

      var node = fct.GetCallNode();
      var status = node.run();
      if(status == BHS.SUCCESS)
      {
        var lst = interp.PopValue().obj as DynValList;

        var sw = new StreamWriter(stream);
        for(int i=0;i<lst.Count;++i)
          sw.Write(lst[i].num + ";");
        sw.Flush();

        lst.TryDel();
      }
      return BHS.SUCCESS;
    }
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForFuncPtrPassedAsArg()
  {
    string bhl = @"

    func int[] make_ints(int n, int k)
    {
      int[] res = []
      return res
    }

    func int may_fail()
    {
      fail()
      return 10
    }

    func void test() 
    {
      NodeTakingFunc(fn: func int[] () { return make_ints(2, may_fail()) } )
      trace(""HERE"")
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("NodeTakingFunc", globs.Type("Foo"),
        delegate() { 
          return new NodeTakingFunc(trace_stream);
        }
      );
      fn.Define(new FuncArgSymbol("fn", globs.Type("int[]^()")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    AssertEqual(node.run(), BHS.SUCCESS);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("HERE", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f = MakeFoo({hey:1, colors:[{r:
          func int (int v) { 
            fail()
            return v
          }(42) 
        }]})
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, false)
      return 42
    }

    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:foo(2)}]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:foo(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncPtrArgsStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, false)
      return 42
    }

    func bar()
    {
      int^(int) p = foo
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:p(2)}]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:p(3)}]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanArgsStackInParalForLambda()
  {
    string bhl = @"
    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:10, colors:[{r:
              func int (int ticks) 
              { 
                WaitTicks(ticks, false)
                fail()
                return ticks
              }(2) 
              }]})
        }
        seq {
          f = MakeFoo({hey:20, colors:[{r:
              func int (int ticks) 
              { 
                WaitTicks(ticks, false)
                fail()
                return ticks
              }(3) 
              }]})
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.FAILURE);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestInterleaveFuncStackInParal()
  {
    string bhl = @"
    func int foo(int ticks) 
    {
      WaitTicks(ticks, true)
      return ticks
    }

    func bar()
    {
      Foo f
      paral_all {
        seq {
          f = MakeFoo({hey:foo(3), colors:[]})
          trace((string)f.hey)
        }
        seq {
          f = MakeFoo({hey:foo(2), colors:[]})
          trace((string)f.hey)
        }
      }
      trace((string)f.hey)
    }

    func void test() 
    {
      bar()
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindWaitTicks(globs);
    BindTrace(globs, trace_stream);
    BindLog(globs);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); });
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    var status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.RUNNING);
    status = node.run();
    AssertEqual(status, BHS.SUCCESS);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("233", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncStackInExpressionForYield()
  {
    string bhl = @"

    func int sub_sub_call()
    {
      yield()
      return 2
    }

    func int sub_call()
    {
      return 1 + 10 + 12 + sub_sub_call()
    }

    func test() 
    {
      int cost = 1 + sub_call()
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    AssertEqual(node.run(), BHS.RUNNING);
    node.stop();
    //NodeDump(node);

    CommonChecks(intp);
  }


  [IsTested()]
  public void TestJsonFuncArgChainCall()
  {
    string bhl = @"
    func void test(float b) 
    {
      trace((string)MakeFoo({hey:142, colors:[{r:2}, {g:3}, {g:b}]}).colors.Count)
    }
    ";

    var trace_stream = new MemoryStream();
    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);
    BindFoo(globs);
    BindTrace(globs, trace_stream);

    {
      var fn = new FuncSymbolNative("MakeFoo", globs.Type("Foo"),
          delegate() { return new MakeFooNode(); } );
      fn.Define(new FuncArgSymbol("conf", globs.Type("Foo")));

      globs.Define(fn);
    }

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    node.SetArgs(DynVal.NewNum(42));
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("3", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestEmptyUserClass()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      return f != null
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassBindConflict()
  {
    string bhl = @"

    class Foo { }
      
    func bool test() 
    {
      Foo f = {}
      return f != null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindFoo(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "already defined symbol 'Foo'"
    );
  }

  [IsTested()]
  public void TestSeveralEmptyUserClasses()
  {
    string bhl = @"

    class Foo { }
    class Bar { }
      
    func bool test() 
    {
      Foo f = {}
      Bar b = {}
      return f != null && b != null
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
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
      
    func void test() 
    {
      Foo f = {}
      trace((string)f.b + "";"" + (string)f.c + "";"" + f.s + "";"")
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("0;0;;", str);
    CommonChecks(intp);
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
  public void TestUserClassContainThisMemberNotAllowed()
  {
    string bhl = @"
      class Foo {
        int this
      }
    ";

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
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

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      "the keyword \"this\" is reserved"
    );
  }

  [IsTested()]
  public void TestUserClassDefaultInitInt()
  {
    string bhl = @"

    class Foo { 
      int c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == 0
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 0);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));
    //NodeDump(node);

    AssertEqual(res, 10);
    CommonChecks(intp);
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

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      @"mismatched input '(' expecting '}'"
    );
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
  public void TestUserClassDefaultInitFloat()
  {
    string bhl = @"

    class Foo { 
      float c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == 0
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStr()
  {
    string bhl = @"

    class Foo { 
      string c
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.c == """"
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
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

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
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
      return f.b == Bar::NONE
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.a == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithArr()
  {
    string bhl = @"

    class Foo { 
      int[] a
    }
      
    func int test() 
    {
      Foo f = {a : [10, 20]}
      return f.a[1]
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 20);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitFuncPtr()
  {
    string bhl = @"

    class Foo { 
      void^(int) ptr
    }
      
    func bool test() 
    {
      Foo f = {}
      return f.ptr == null
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractBool(ExecNode(node));

    AssertTrue(res);
    CommonChecks(intp);
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
      int^(int) ptr
    }
      
    func int test() 
    {
      Foo f = {}
      f.ptr = foo
      return f.ptr(2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(3, res);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestCleanFuncArgsOnStackClassPtr()
  {
    string bhl = @"

    class Foo
    {
      void^(int,int) ptr
    }

    func int foo()
    {
      fail()
      return 10
    }

    func void bar(int a, int b)
    {
    }

    func test() 
    {
      Foo f = {}
      f.ptr = bar
      f.ptr(10, foo())
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
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
      int^(int)[] ptrs
    }
      
    func int test() 
    {
      Foo f = {ptrs: []}
      f.ptrs.Add(foo)
      f.ptrs.Add(foo2)
      return f.ptrs[1](2)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(12, res);
    CommonChecks(intp);
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
      int^(int,int)[] ptrs
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

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);

    ExecNode(node, 0);
    //NodeDump(node);
    AssertEqual(intp.stack.Count, 0);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassWithSubClass()
  {
    string bhl = @"

    class Bar {
      int[] a
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

    var globs = SymbolTable.CreateBuiltins();

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 21);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassDefaultInitStringConcat()
  {
    string bhl = @"

    class Foo { 
      string s1
      string s2
    }
      
    func void test() 
    {
      Foo f = {}
      string s = f.s1 + f.s2
      trace(s)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassMember()
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 102);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    bool res = ExtractBool(ExecNode(node));

    //NodeDump(node);
    AssertTrue(res);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 10);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserSeveralClassMembers()
  {
    string bhl = @"

    class Foo { 
      float b
      int c
    }
      
    func float test() 
    {
      Foo f = { c : 2, b : 101.5 }
      f.b = f.b + f.c
      return f.b
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 103.5);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserSeveralClassMembers2()
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 113.5);
    CommonChecks(intp);
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
      Foo[] fs = [{b:1, c:2}]
      fs.Add({b:10, c:20})

      return fs[0].b + fs[0].c + fs[1].b + fs[1].c
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 33);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 101);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 101);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b[1]
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 102);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestUserClassInlineCastArr2()
  {
    string bhl = @"

    class Foo { 
      int[] b
    }
      
    func int test() 
    {
      Foo f = { b : [101, 102] }
      any a = f
      return ((Foo)a).b.Count
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var res = ExtractNum(ExecNode(node));

    AssertEqual(res, 2);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestChildUserClass()
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
      return f.x + f.y
    }
    ";

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 3);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 1);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 3);
    CommonChecks(intp);
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

    var intp = Interpret(bhl);
    var node = intp.GetFuncCallNode("test");
    var res = ExtractNum(ExecNode(node));

    //NodeDump(node);
    AssertEqual(res, 2);
    CommonChecks(intp);
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

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl);
      },
      @"already defined symbol 'x'"
    );
  }

  [IsTested()]
  public void TestChildUserClassOfBindClass()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }
      
    func float test() 
    {
      ColorA c = { r : 1, g : 2, a : 3}
      return c.r + c.g + c.a
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var n = ExtractNum(ExecNode(node));

    AssertEqual(6, n);
  }

  [IsTested()]
  public void TestExtendCSharpClassSubChild()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float a
    }

    class ColorF : ColorA 
    { 
      float f
    }
      
    func float test() 
    {
      ColorF c = { r : 1, g : 2, f : 3, a: 4}
      return c.r + c.g + c.a + c.f
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    //NodeDump(node);
    var n = ExtractNum(ExecNode(node));

    AssertEqual(10, n);
  }

  [IsTested()]
  public void TestExtendCSharpClassMemberAlreadyExists()
  {
    string bhl = @"

    class ColorA : Color 
    { 
      float g
    }
      
    func float test() 
    {
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    BindColor(globs);

    AssertError<UserError>(
      delegate() { 
        Interpret(bhl, globs);
      },
      "already defined symbol 'g'"
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

  [IsTested()]
  public void TestGetCallstackInfoFromFuncAsArg()
  {
    string bhl2 = @"
    func float bar(float b)
    {
      return b
    }

    func float wow(float b)
    {
      record_callstack()
      return b
    }

    func float foo(float k)
    {
      return bar(
          wow(k)
      )
    }

    ";

    string bhl1 = @"
    import ""bhl2""

    func float test(float k) 
    {
      return foo(k)
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var info = new List<Interpreter.CallStackInfo>();
    {
      var fn = new FuncSymbolSimpleNative("record_callstack", globs.Type("void"),
        delegate() { 
          Interpreter.instance.GetCallStackInfo(info); 
          return BHS.SUCCESS; 
        });
      globs.Define(fn);
    }

    TestCleanDir();
    var files = new List<string>();
    TestNewFile("bhl1.bhl", bhl1, files);
    TestNewFile("bhl2.bhl", bhl2, files);

    var intp = CompileFiles(files, globs);
    
    var node = intp.GetFuncCallNode("bhl1", "test");
    node.SetArgs(DynVal.NewNum(3));
    var res = ExtractNum(ExecNode(node));

    AssertEqual(3, res);
    AssertEqual(2, info.Count);
    AssertEqual("foo", info[0].func_name);
    AssertEqual("bhl2", info[0].module_name);
    AssertEqual(16, info[0].line_num);
    AssertEqual("test", info[1].func_name);
    AssertEqual("bhl1", info[1].module_name);
    AssertEqual(6, info[1].line_num);
  }

  [IsTested()]
  public void TestRefCountSimple()
  {
    string bhl = @"
    func void test() 
    {
      RefC r = new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturnResult()
  {
    string bhl = @"
    func RefC test() 
    {
      return new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignSame()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = r1
      r2 = r1
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignSelf()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      r1 = r1
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;INC2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountAssignOverwrite()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
      r1 = r2
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;INC2;DEC1;INC2;DEC0;REL0;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountSeveral()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC
      RefC r2 = new RefC
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;DEC0;REL0;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInLambda()
  {
    string bhl = @"
    func void test() 
    {
      RefC r1 = new RefC

      trace(""REFS"" + (string)r1.refs + "";"")

      void^() fn = func() {
        trace(""REFS"" + (string)r1.refs + "";"")
      }
      
      fn()
      trace(""REFS"" + (string)r1.refs + "";"")
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    //NodeDump(node);
    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;REFS1;INC2;INC3;INC4;DEC3;REL3;REFS3;DEC2;REL2;INC3;DEC2;REL2;REFS2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountInArray()
  {
    string bhl = @"
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountSeveralInArrayAccess()
  {
    string bhl = @"
    func void test() 
    {
      RefC[] rs = new RefC[]
      rs.Add(new RefC)
      rs.Add(new RefC)
      float refs = rs[1].refs
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;REL0;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturn()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func void test() 
    {
      RefC c1 = make()
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountPass()
  {
    string bhl = @"

    func void foo(RefC c)
    { 
      trace(""HERE;"")
    }

    func void test() 
    {
      foo(new RefC)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);
    BindTrace(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;HERE;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestRefCountReturnPass()
  {
    string bhl = @"

    func RefC make()
    {
      RefC c = new RefC
      return c
    }

    func void foo(RefC c)
    {
      RefC c2 = c
    }

    func void test() 
    {
      RefC c1 = make()
      foo(c1)
    }
    ";

    var trace_stream = new MemoryStream();

    var globs = SymbolTable.CreateBuiltins();

    BindRefC(globs, trace_stream);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");
    ExecNode(node, 0);

    var str = GetString(trace_stream);

    AssertEqual("INC1;DEC0;INC1;INC2;DEC1;REL1;DEC0;INC1;INC2;DEC1;INC2;INC3;DEC2;INC3;DEC2;REL2;DEC1;REL1;DEC0;REL0;", str);
    CommonChecks(intp);
  }

  [IsTested()]
  public void TestHashedName()
  {
    {
      var hn = new HashedName(0xBEAFDEADDEADBEAF);
      AssertEqual(0xDEADBEAF, hn.n1);
      AssertEqual(0xBEAFDEAD, hn.n2);
      AssertEqual(0xBEAFDEADDEADBEAF, hn.n);
      AssertEqual("", hn.s);
    }

    {
      var hn = new HashedName("Foo");
      AssertEqual(Hash.CRC28("Foo"), hn.n1);
      AssertEqual(0, hn.n2);
      AssertEqual(Hash.CRC28("Foo"), hn.n);
      AssertEqual("Foo", hn.s);
    }

    {
      var hn = new HashedName(0xBEAFDEADDEADBEAF, "Foo");
      AssertEqual(0xDEADBEAF, hn.n1);
      AssertEqual(0xBEAFDEAD, hn.n2);
      AssertEqual(0xBEAFDEADDEADBEAF, hn.n);
      AssertEqual("Foo", hn.s);
    }

    {
      var hn = new HashedName("Foo", 0xDEADBEAF);
      AssertEqual(Hash.CRC28("Foo"), hn.n1);
      AssertEqual(0xDEADBEAF, hn.n2);
      AssertEqual((ulong)0xDEADBEAF << 32 | (ulong)(Hash.CRC28("Foo")), hn.n);
      AssertEqual("Foo", hn.s);
    }
  }

  [IsTested()]
  public void TestStack()
  {
    //Push/PopFast
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop());
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop());
      AssertEqual(st.Count, 0);
    }
    
    //Push/Pop(repl)
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      AssertEqual(10, st.Pop(0));
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      AssertEqual(1, st.Pop(0));
      AssertEqual(st.Count, 0);
    }

    //Push/Dec
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(10);

      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(10, st[1]);

      st.Dec();
      AssertEqual(st.Count, 1);
      AssertEqual(1, st[0]);

      st.Dec();
      AssertEqual(st.Count, 0);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(1);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(0);
      AssertEqual(st.Count, 2);
      AssertEqual(2, st[0]);
      AssertEqual(3, st[1]);
    }

    //RemoveAt
    {
      var st = new FixedStack<int>(16); 
      st.Push(1);
      st.Push(2);
      st.Push(3);

      st.RemoveAt(2);
      AssertEqual(st.Count, 2);
      AssertEqual(1, st[0]);
      AssertEqual(2, st[1]);
    }
  }
  
  [IsTested()]
  public void TestDynVal()
  {
    // ToAny()
    {
      string str = "str";
      double num = 33.0;
      bool bl = false;
      object obj = new List<int>(33);

      var dv = DynVal.NewStr(str);
      Assert(dv.ToAny() == (object)str);
      
      dv = DynVal.NewNum(num);
      Assert((double)dv.ToAny() == num);

      dv = DynVal.NewBool(bl);
      Assert((bool)dv.ToAny() == bl);

      dv = DynVal.NewObj(obj);
      Assert(dv.ToAny() == obj);
    }
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
  public void TestWeird()
  {
    string bhl = @"

    func A(int b = 1)
    {
      trace(""A"" + (string)b)
      suspend()
    }

    func test() 
    {
      while(true) {
        int i = 0
        paral {
          A()
          seq {
            while(i < 1) {
              yield()
              i = i + 1
            }
          }
        }
        yield()
      }
    }
    ";

    var globs = SymbolTable.CreateBuiltins();
    var trace_stream = new MemoryStream();

    BindTrace(globs, trace_stream);
    BindStartScript(globs);

    var intp = Interpret(bhl, globs);
    var node = intp.GetFuncCallNode("test");

    node.run();
    node.run();
    node.run();
    node.stop();

    var str = GetString(trace_stream);

    AssertEqual("A1A1", str);
    CommonChecks(intp);
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


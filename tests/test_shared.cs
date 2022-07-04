using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Options;
using bhl;

public class IsTestedAttribute : Attribute
{
  public override string ToString()
  {
    return "Is Tested";
  }
}

public static class BHL_TestExt 
{
  public static string GetFullMessage(this Exception ex)
  {
    return ex.InnerException == null 
      ? ex.Message 
      : ex.Message + " --> " + ex.InnerException.GetFullMessage();
  }
}

public class BHL_TestRunner
{
  public static void Main(string[] args)
  {
    bool verbose = false;
    var p = new OptionSet() {
      { "verbose", "be verbose",
        v => verbose = v != null },
     };

    var names = p.Parse(args);

    Run(names, new TestNodes(), verbose);
    Run(names, new TestVM(), verbose);
    Run(names, new TestClasses(), verbose);
    Run(names, new TestInterfaces(), verbose);
    Run(names, new TestTypeCasts(), verbose);
    Run(names, new TestNamespace(), verbose);
    Run(names, new TestMaps(), verbose);
    //TODO:
    //Run(names, new TestLSP(), verbose);
  }

  static void Run(IList<string> names, BHL_TestBase test, bool verbose)
  {
    try
    {
      _Run(names, test, verbose);
    }
    catch(Exception e)
    {
      Console.WriteLine(e.ToString());
      Console.WriteLine("=========================");
      Console.WriteLine(e.GetFullMessage());
      System.Environment.Exit(1);
    }
  }

  static void _Run(IList<string> names, BHL_TestBase test, bool verbose)
  {
    int c = 0;
    foreach(var method in test.GetType().GetMethods())
    {
      if(IsMemberTested(method))
      {
        if(IsAllowedToRun(names, test, method))
        {
          if(verbose)
            Console.WriteLine(">>>> Testing " + test.GetType().Name + "." + method.Name + " <<<<");

          ++c;
          method.Invoke(test, new object[] {});
        }
      }
    }

    if(c > 0)
      Console.WriteLine("Done running "  + c + " tests");
  }

  static bool IsAllowedToRun(IList<string> names, BHL_TestBase test, MemberInfo member)
  {
    if(names?.Count == 0)
      return true;

    for(int i=0;i<names.Count;++i)
    {
      var parts = names[i].Split('.');

      string test_filter = parts.Length >= 1 ? parts[0] : null;
      string method_filter = parts.Length > 1 ? parts[1] : null;

      bool exact = true;
      if(test_filter != null && test_filter.EndsWith("~"))
      {
        exact = false;
        test_filter = test_filter.Substring(0, test_filter.Length-1);
      }

      if(method_filter != null && method_filter.EndsWith("~"))
      {
        exact = false;
        method_filter = method_filter.Substring(0, method_filter.Length-1);
      }

      if(test_filter == null || (test_filter != null && (exact ? test.GetType().Name == test_filter : test.GetType().Name.IndexOf(test_filter) != -1)))
      {
        if(method_filter == null || (method_filter != null && (exact ? member.Name == method_filter : member.Name.IndexOf(method_filter) != -1)))
          return true;
      }
    }

    return false;
  }

  static bool IsMemberTested(MemberInfo member)
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is IsTestedAttribute)
        return true;
    }
    return false;
  }
}

public class BHL_TestBase
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

  public ClassSymbolNative BindColor(Types ts)
  {
    var cl = new ClassSymbolNative("Color", null,
      delegate(VM.Frame frm, ref Val v, IType type) 
      { 
        v.SetObj(new Color(), type);
      }
    );

    ts.ns.Define(cl);
    cl.Define(new FieldSymbol("r", ts.T("float"),
      delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
      {
        var c = (Color)ctx.obj;
        v.SetFlt(c.r);
      },
      delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
      {
        var c = (Color)ctx.obj;
        c.r = (float)v.num; 
        ctx.SetObj(c, ctx.type);
      }
    ));
    cl.Define(new FieldSymbol("g", ts.T("float"),
      delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
      {
        var c = (Color)ctx.obj;
        v.SetFlt(c.g);
      },
      delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
      {
        var c = (Color)ctx.obj;
        c.g = (float)v.num; 
        ctx.SetObj(c, ctx.type);
      }
    ));

    {
      var m = new FuncSymbolNative("Add", ts.T("Color"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var k = (float)frm.stack.PopRelease().num;
          var c = (Color)frm.stack.PopRelease().obj;

          var newc = new Color();
          newc.r = c.r + k;
          newc.g = c.g + k;

          var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
          frm.stack.Push(v);

          return null;
        },
        new FuncArgSymbol("k", ts.T("float"))
      );

      cl.Define(m);
    }
    
    {
      var m = new FuncSymbolNative("mult_summ", ts.T("float"),
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
        {
          var k = frm.stack.PopRelease().num;
          var c = (Color)frm.stack.PopRelease().obj;
          frm.stack.Push(Val.NewFlt(frm.vm, (c.r * k) + (c.g * k)));
          return null;
        },
        new FuncArgSymbol("k", ts.T("float"))
      );

      cl.Define(m);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor", ts.T("Color"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            var r = frm.stack.PopRelease().num;
            var c = new Color();
            c.r = (float)r;
            var v = Val.NewObj(frm.vm, c, ts.T("Color").Get());
            frm.stack.Push(v);
            return null;
          },
        new FuncArgSymbol("r", ts.T("float"))
      );

      ts.ns.Define(fn);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor_null", ts.T("Color"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            frm.stack.Push(frm.vm.Null);
            return null;
          }
      );

      ts.ns.Define(fn);
    }

    ts.ns.Define(new ArrayTypeSymbolT<Color>("ArrayT_Color", ts.T("Color"), delegate() { return new List<Color>(); } ));

    return cl;
  }

  public void BindColorAlpha(Types ts)
  {
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("ColorAlpha", (ClassSymbol)ts.T("Color").Get(),
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new ColorAlpha(), type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("a", ts.T("float"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (ColorAlpha)ctx.obj;
          v.num = c.a;
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (ColorAlpha)ctx.obj;
          c.a = (float)v.num; 
          ctx.SetObj(c, ctx.type);
        }
      ));

      {
        var m = new FuncSymbolNative("mult_summ_alpha", ts.T("float"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status)
          {
            var c = (ColorAlpha)frm.stack.PopRelease().obj;

            frm.stack.Push(Val.NewFlt(frm.vm, (c.r * c.a) + (c.g * c.a)));

            return null;
          }
        );

        cl.Define(m);
      }
    }
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

  public void BindFoo(Types ts)
  {
    {
      var cl = new ClassSymbolNative("Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) 
        { 
          v.SetObj(new Foo(), type);
        }
      );
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol("hey", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          v.SetNum(f.hey);
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          f.hey = (int)v.num; 
          ctx.SetObj(f, ctx.type);
        }
      ));
      cl.Define(new FieldSymbol("colors", ts.T("ArrayT_Color"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          v.SetObj(f.colors, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          f.colors = (List<Color>)v.obj;
        }
      ));
      cl.Define(new FieldSymbol("sub_color", ts.T("Color"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          v.SetObj(f.sub_color, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var f = (Foo)ctx.obj;
          f.sub_color = (Color)v.obj; 
          ctx.SetObj(f, ctx.type);
        }
      ));
    }

    {
      var fn = new FuncSymbolNative("PassthruFoo", ts.T("Foo"),
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            frm.stack.Push(frm.stack.Pop());
            return null;
          },
          new FuncArgSymbol("foo", ts.T("Foo"))
      );

      ts.ns.Define(fn);
    }
  }

  public class Bar
  {
    public int Int;
    public float Flt;
    public string Str;
  }

  public ClassSymbolNative BindBar(Types ts)
  {
    var cl = new ClassSymbolNative("Bar", null,
      delegate(VM.Frame frm, ref Val v, IType type) 
      { 
        v.SetObj(new Bar(), type);
      }
    );

    ts.ns.Define(cl);
    cl.Define(new FieldSymbol("Int", Types.Int,
      delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        v.SetNum(c.Int);
      },
      delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        c.Int = (int)v.num; 
        ctx.SetObj(c, ctx.type);
      }
    ));
    cl.Define(new FieldSymbol("Flt", ts.T("float"),
      delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        v.SetFlt(c.Flt);
      },
      delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        c.Flt = (float)v.num; 
        ctx.SetObj(c, ctx.type);
      }
    ));
    cl.Define(new FieldSymbol("Str", ts.T("string"),
      delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        v.SetStr(c.Str);
      },
      delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
      {
        var c = (Bar)ctx.obj;
        c.Str = (string)v.obj; 
        ctx.SetObj(c, ctx.type);
      }
    ));

    return cl;
  }


  public FuncSymbolNative BindTrace(Types ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative("trace", Types.Void,
        delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
          string str = frm.stack.PopRelease().str;
          log.Append(str);
          return null;
        }, 
        new FuncArgSymbol("str", ts.T("string"))
    );
    ts.ns.Define(fn);
    return fn;
  }

  //simple console outputting version
  public void BindLog(Types ts)
  {
    {
      var fn = new FuncSymbolNative("log", Types.Void,
          delegate(VM.Frame frm, FuncArgsInfo args_info, ref BHS status) { 
            string str = frm.stack.PopRelease().str;
            Console.WriteLine(str); 
            return null;
          },
          new FuncArgSymbol("str", ts.T("string"))
      );
      ts.ns.Define(fn);
    }
  }

  public VM MakeVM(string bhl, Types ts = null, bool show_ast = false, bool show_bytes = false)
  {
    return MakeVM(Compile(bhl, ts, show_ast: show_ast, show_bytes: show_bytes), ts);
  }

  public static VM MakeVM(CompiledModule orig_cm, Types ts = null)
  {
    if(ts == null)
      ts = new Types();
    else
      //NOTE: we don't want to affect the original ts
      ts = ts.Clone();

    //let's serialize/unserialize the compiled module so that
    //it's going to go thru the full compilation cycle
    var ms = new MemoryStream();
    CompiledModule.ToStream(orig_cm, ms);

    var cm = CompiledModule.FromStream(ts, new MemoryStream(ms.GetBuffer()));

    var vm = new VM(ts);
    vm.RegisterModule(cm);
    return vm;
  }

  public VM.Fiber Execute(VM vm, string fn_name, params Val[] args)
  {
    return Execute(vm, fn_name, 0, args);
  }

  public VM.Fiber Execute(VM vm, string fn_name, FuncArgsInfo args_info, params Val[] args)
  {
    return Execute(vm, fn_name, args_info.bits, args);
  }

  public VM.Fiber Execute(VM vm, string fn_name, uint cargs_bits, params Val[] args)
  {
    var fb = vm.Start(fn_name, cargs_bits, args);
    const int LIMIT = 20;
    int c = 0;
    for(;c<LIMIT;++c)
    {
      if(!vm.Tick())
        return fb;
    }
    throw new Exception("Too many iterations: " + c);
  }

  public static int PredictOpcodeSize(ModuleCompiler.Definition op, byte[] bytes, int start_pos)
  {
    if(op.operand_width == null)
      return 0;
    int pos = start_pos;
    foreach(int ow in op.operand_width)
      Bytecode.Decode(bytes, ow, ref pos);
    return pos - start_pos;
  }

  public class TestImporter : IModuleLoader
  {
    public Dictionary<string, CompiledModule> mods = new Dictionary<string, CompiledModule>();

    public CompiledModule Load(string name, ISymbolResolver resolver, System.Action<string> on_import)
    {
      return mods[name];
    }
  }

  public static void CleanTestDir()
  {
    string dir = TestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true/*recursive*/);
  }

  public static void NewTestFile(string path, string text, ref List<string> files)
  {
    string full_path = TestDirPath() + "/" + path;
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
  }

  public static int ConstIdx(CompiledModule cm, string str)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.STR && cn.str == str)
        return i;
    }
    throw new Exception("Constant not found: " + str);
  }

  public static int ConstIdx(CompiledModule cm, int num)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.INT && cn.num == num)
        return i;
    }
    throw new Exception("Constant not found: " + num);
  }

  public static int ConstIdx(CompiledModule cm, double num)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.FLT && cn.num == num)
        return i;
    }
    throw new Exception("Constant not found: " + num);
  }

  public static int ConstIdx(CompiledModule cm, bool v)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.BOOL && cn.num == (v ? 1 : 0))
        return i;
    }
    throw new Exception("Constant not found: " + v);
  }

  public static int ConstIdx(CompiledModule cm, TypeProxy v)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.TPROXY && cn.tproxy.Equals(v))
        return i;
    }
    throw new Exception("Constant not found: " + v);
  }

  public static int ConstNullIdx(CompiledModule cm)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.NIL)
        return i;
    }
    throw new Exception("Constant null not found");
  }

  public void CommonChecks(VM vm, bool check_frames = true, bool check_fibers = true, bool check_instructions = true)
  {
    //cleaning globals
    vm.UnloadModules();

    //for extra debug
    if(vm.vals_pool.Allocs != vm.vals_pool.Free)
      Console.WriteLine(vm.vals_pool.Dump());

    AssertEqual(vm.vlsts_pool.Allocs, vm.vlsts_pool.Free);
    AssertEqual(vm.vals_pool.Allocs, vm.vals_pool.Free);
    AssertEqual(vm.ptrs_pool.Allocs, vm.ptrs_pool.Free);
    if(check_frames)
      AssertEqual(vm.frames_pool.Allocs, vm.frames_pool.Free);
    if(check_fibers)
      AssertEqual(vm.fibers_pool.Allocs, vm.fibers_pool.Free);
    if(check_instructions)
      AssertEqual(vm.coro_pool.Allocs, vm.coro_pool.Free);
  }

  public static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.GetDirectoryName(self_bin) + "/tmp/tests";
  }

  public static void Assert(bool condition, string msg = null)
  {
    if(!condition)
      throw new Exception("Assertion failed " + (msg != null ? msg : ""));
  }

  public static void AssertEqual<T>(T a, T b) where T : class
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }
  
  public static void AssertEqual(float a, float b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(double a, double b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(uint a, uint b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(ulong a, ulong b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(BHS a, BHS b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(string a, string b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertEqual(int a, int b)
  {
    if(!(a == b))
      throw new Exception("Assertion failed: " + a + " != " + b);
  }

  public static void AssertTrue(bool cond, string msg = "")
  {
    if(!cond)
      throw new Exception("Assertion failed" + (msg.Length > 0 ? (": " + msg) : ""));
  }

  public static void AssertFalse(bool cond, string msg = "")
  {
    if(cond)
      throw new Exception("Assertion failed" + (msg.Length > 0 ? (": " + msg) : ""));
  }

  public void AssertError<T>(Action action, string msg) where T : Exception
  {
    Exception err = null;
    try
    {
      action();
    }
    catch(T e)
    {
      err = e;
    }

    AssertTrue(err != null, "Error didn't occur"); 
    var idx = err.ToString().IndexOf(msg);
    AssertTrue(idx != -1, "Error message is: " + err);
  }

  public Stream CompileFiles(List<string> files, Types ts = null)
  {
    if(ts == null)
      ts = new Types();
    else
      //NOTE: we don't want to affect the original ts
      ts = ts.Clone();

    var conf = new CompileConf();
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.ts = ts;
    conf.files = files;
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_dir = TestDirPath();
    conf.tmp_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = false;

    var cmp = new CompilationExecutor();
    var err = cmp.Exec(conf);
    if(err != null)
      throw new Exception(ErrorUtils.ToJson(err));

    return new MemoryStream(File.ReadAllBytes(conf.res_file));
  }

  public CompiledModule Compile(string bhl, Types ts = null, bool show_ast = false, bool show_bytes = false)
  {
    if(ts == null)
      ts = new Types();
    else
      //NOTE: we don't want to affect the original ts
      ts = ts.Clone();

    var mdl = new bhl.Module(ts, "", "");

    var front_res = ANTLR_Parser.ProcessStream(mdl, bhl.ToStream(), ts);

    if(show_ast)
      AST_Dumper.Dump(front_res.ast);
    var c  = new ModuleCompiler(front_res);
    var cm = c.Compile();
    if(show_bytes)
      Dump(c);
    return cm;
  }

  public static string ByteArrayToString(byte[] ba)
  {
    var hex = new StringBuilder(ba.Length * 2);
    foreach(byte b in ba)
      hex.AppendFormat("{0:x2}", b);
    return hex.ToString();
  }

  public static void AssertEqual(byte[] a, byte[] b)
  {
    bool equal = true;
    string cmp = "";
    for(int i=0;i<(a.Length > b.Length ? a.Length : b.Length);i++)
    {
      string astr = "";
      if(i < a.Length)
        astr = string.Format("{0:x2}", a[i]);

      string bstr = "";
      if(i < b.Length)
        bstr = string.Format("{0:x2}", b[i]);

      cmp += string.Format("{0:x2}", i) + " " + astr + " | " + bstr;

      if(astr != bstr)
      {
        equal = false;
        cmp += " !!!";
      }

      cmp += "\n";
    }

    if(!equal)
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
  }

  public static void Print(ModuleCompiler c)
  {
    var bs = c.Compile().bytecode;
    ModuleCompiler.Definition op = null;
    int op_size = 0;

    for(int i=0;i<bs.Length;i++)
    {
      string str = string.Format("0x{0:x2}", bs[i]);
      if(op != null)
      {
        --op_size;
        if(op_size == 0)
          op = null; 
      }
      else
      {
        op = ModuleCompiler.LookupOpcode((Opcodes)bs[i]);
        op_size = PredictOpcodeSize(op, bs, i);
        str += "(" + op.name.ToString() + ")";
        if(op_size == 0)
          op = null;
      }
      Console.WriteLine(string.Format("{0:x2}", i) + " " + str);
    }
    Console.WriteLine("============");
  }

  public static void AssertEqual(ModuleCompiler ca, ModuleCompiler cb)
  {
    AssertEqual(ca.Compile(), cb.Compile());
  }

  public static void AssertEqual(CompiledModule ca, ModuleCompiler cb)
  {
    AssertEqual(ca, cb.Compile());
  }

  public static void AssertEqual(CompiledModule ca, CompiledModule cb)
  {
    string cmp;

    if(!CompareCode(ca.initcode, cb.initcode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: init bytes not equal");
    }

    if(!CompareCode(ca.bytecode, cb.bytecode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
  }

  static void Dump(ModuleCompiler c)
  {
    Dump(c.Compile());
  }

  static void Dump(CompiledModule c)
  {
    if(c.initcode?.Length > 0)
    {
      Console.WriteLine("=== INIT ===");
      Dump(c.initcode);
    }
    Console.WriteLine("=== CODE ===");
    Dump(c.bytecode);
  }

  static void Dump(byte[] bs)
  {
    string res = "";

    ModuleCompiler.Definition op = null;
    int op_size = 0;

    for(int i=0;i<bs?.Length;i++)
    {
      res += string.Format("{1:00} 0x{0:x2} {0}", bs[i], i);
      if(op != null)
      {
        --op_size;
        if(op_size == 0)
          op = null; 
      }
      else
      {
        op = ModuleCompiler.LookupOpcode((Opcodes)bs[i]);
        op_size = PredictOpcodeSize(op, bs, i);
        res += "(" + op.name.ToString() + ")";
        if(op_size == 0)
          op = null;
      }
      res += "\n";
    }

    Console.WriteLine(res);
  }

  static bool CompareCode(byte[] a, byte[] b, out string cmp)
  {
    ModuleCompiler.Definition aop = null;
    int aop_size = 0;
    ModuleCompiler.Definition bop = null;
    int bop_size = 0;

    bool equal = true;
    cmp = "";
    var lens = new List<int>();
    int max_len = 0;
    for(int i=0;i<(a?.Length > b?.Length ? a?.Length : b?.Length);i++)
    {
      string astr = "";
      if(i < a?.Length)
      {
        astr = string.Format("0x{0:x2} {0}", a[i]);
        if(aop != null)
        {
          --aop_size;
          if(aop_size == 0)
            aop = null; 
        }
        else
        {
          aop = ModuleCompiler.LookupOpcode((Opcodes)a[i]);
          aop_size = PredictOpcodeSize(aop, a, i);
          astr += "(" + aop.name.ToString() + ")";
          if(aop_size == 0)
            aop = null;
        }
      }

      string bstr = "";
      if(i < b?.Length)
      {
        bstr = string.Format("0x{0:x2} {0}", b[i]);
        if(bop != null)
        {
          --bop_size;
          if(bop_size == 0)
            bop = null; 
        }
        else
        {
          bop = ModuleCompiler.LookupOpcode((Opcodes)b[i]);
          bop_size = PredictOpcodeSize(bop, b, i);
          bstr += "(" + bop.name.ToString() + ")";
          if(bop_size == 0)
            bop = null;
        }
      }

      lens.Add(astr.Length);
      if(astr.Length > max_len)
        max_len = astr.Length;
      cmp += string.Format("{0,2}", i) + " " + astr + "{fill" + lens.Count + "} | " + bstr;

      if(a?.Length <= i || b?.Length <= i || a[i] != b[i])
      {
        equal = false;
        cmp += " <============== actual vs expected";
      }

      cmp += "\n";
    }

    for(int i=1;i<=lens.Count;++i)
    {
      cmp = cmp.Replace("{fill" + i + "}", new String(' ', max_len - lens[i-1]));
    }

    return equal;
  }

  public static void AssertEqual(List<Const> cas, List<Const> cbs)
  {
    AssertEqual(cas.Count, cbs.Count);
    for(int i=0;i<cas.Count;++i)
    {
      AssertEqual((int)cas[i].type, (int)cbs[i].type);
      AssertEqual(cas[i].num, cbs[i].num);
      AssertEqual(cas[i].str, cbs[i].str);
    }
  }
}

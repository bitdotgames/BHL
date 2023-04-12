using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Options;
using bhl;
using Antlr4.Runtime.Tree;

public class IsTestedAttribute : Attribute
{
  public override string ToString()
  {
    return "Is Tested";
  }
}

public class BHL_TestRunner
{
  static public bool verbose { get; private set; }

  public static void Main(string[] args)
  {
    verbose = false;
    var p = new OptionSet() {
      { "verbose", "be verbose",
        v => verbose = v != null },
     };

    var names = p.Parse(args);

    int counter = 0;

    counter += Run(names, new TestNodes());
    counter += Run(names, new TestVM());
    counter += Run(names, new TestAny());
    counter += Run(names, new TestEnum());
    counter += Run(names, new TestYield());
    counter += Run(names, new TestImport());
    counter += Run(names, new TestVariadic());
    counter += Run(names, new TestClasses());
    counter += Run(names, new TestInterfaces());
    counter += Run(names, new TestTypeCasts());
    counter += Run(names, new TestNamespace());
    counter += Run(names, new TestImplicitVar());
    counter += Run(names, new TestMaps());
    counter += Run(names, new TestStd());
    counter += Run(names, new TestErrors());
    counter += Run(names, new TestMarshall());
    counter += Run(names, new TestLSP());
    counter += Run(names, new TestPerf());

    Console.WriteLine("Total tests: " + counter);
  }

  static int Run(IList<string> names, BHL_TestBase test)
  {
    try
    {
      return _Run(names, test);
    }
    catch(Exception e)
    {
      //TODO: ICompileError can't be handled here, it's hidden by reflection exception
      Console.Error.WriteLine(e.ToString());
      Console.Error.WriteLine("=========================");
      Console.Error.WriteLine(e.GetFullMessage());
      System.Environment.Exit(1);
      return 0;
    }
  }

  internal class MethodToTest
  {
    internal MethodInfo method;
    internal int sub_test_idx_filter; 
  }

  static int _Run(IList<string> names, BHL_TestBase test)
  {
    var tested_methods = new List<MethodToTest>();

    foreach(var method in test.GetType().GetMethods())
    {
      MethodToTest to_test;
      if(HasTestedAttribute(method) && CheckForTesting(names, test, method, out to_test))
        tested_methods.Add(to_test);
    }

    int counter = 0;

    if(tested_methods.Count > 0)
    {
      Console.WriteLine(">>>> Testing " + test.GetType().Name + " (" + tested_methods.Count + ")");

      foreach(var to_test in tested_methods)
      {
        if(verbose)
          Console.WriteLine(">>>>> " + test.GetType().Name + "." + to_test.method.Name);

        test.sub_test_idx_filter = to_test.sub_test_idx_filter;
        test.sub_test_idx = -1;
        to_test.method.Invoke(test, new object[] {});
        ++counter;
      }
    }
    return counter;
  }

  static bool CheckForTesting(
    IList<string> names, 
    BHL_TestBase test, 
    MethodInfo member, 
    out MethodToTest to_test
  )
  {
    to_test = new MethodToTest();
    to_test.sub_test_idx_filter = -1;
    to_test.method = member;

    if(names?.Count == 0)
      return true;

    for(int i=0;i<names.Count;++i)
    {
      var name = names[i];

      var parts = name.Split('.');

      string test_filter = parts.Length >= 1 ? parts[0] : null;
      string method_filter = parts.Length > 1 ? parts[1] : null;
      string sub_filter = parts.Length > 2 ? parts[2] : null; 

      bool exact = true;
      if(!string.IsNullOrEmpty(test_filter) && test_filter.EndsWith("~"))
      {
        exact = false;
        test_filter = test_filter.Substring(0, test_filter.Length-1);
      }

      if(!string.IsNullOrEmpty(method_filter) && method_filter.EndsWith("~"))
      {
        exact = false;
        method_filter = method_filter.Substring(0, method_filter.Length-1);
      }

      if(string.IsNullOrEmpty(test_filter) || 
        (!string.IsNullOrEmpty(test_filter) && 
         (exact ? test.GetType().Name == test_filter : test.GetType().Name.IndexOf(test_filter) != -1))
        )
      {
        if(string.IsNullOrEmpty(method_filter) || 
          (!string.IsNullOrEmpty(method_filter) && 
          (exact ? member.Name == method_filter : member.Name.IndexOf(method_filter) != -1))
          )
        {
          if(sub_filter != null)
            to_test.sub_test_idx_filter = int.Parse(sub_filter);
          return true;
        }
      }
    }

    return false;
  }

  static bool HasTestedAttribute(MemberInfo member)
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
  internal int sub_test_idx;
  //TODO: make it a set?
  internal int sub_test_idx_filter;

  public void BindMin(Types ts)
  {
    var fn = new FuncSymbolNative("min", ts.T("float"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
          var b = (float)stack.PopRelease().num;
          var a = (float)stack.PopRelease().num;
          stack.Push(Val.NewFlt(frm.vm, a > b ? b : a)); 
          return null;
        },
        new FuncArgSymbol("a", ts.T("float")),
        new FuncArgSymbol("b", ts.T("float"))
    );
    ts.ns.Define(fn);
  }

  public void BindFail(Types ts)
  {
    var fn = new FuncSymbolNative("fail", ts.T("void"),
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) 
      { 
        status = BHS.FAILURE;
        return null;
      } 
    );
    ts.ns.Define(fn);
  }

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

  public ClassSymbolNative BindColor(Types ts, bool call_setup = true)
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
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var k = (float)stack.PopRelease().num;
          var c = (Color)stack.PopRelease().obj;

          var newc = new Color();
          newc.r = c.r + k;
          newc.g = c.g + k;

          var v = Val.NewObj(frm.vm, newc, ts.T("Color").Get());
          stack.Push(v);

          return null;
        },
        new FuncArgSymbol("k", ts.T("float"))
      );

      cl.Define(m);
    }
    
    {
      var m = new FuncSymbolNative("mult_summ", ts.T("float"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          var k = stack.PopRelease().num;
          var c = (Color)stack.PopRelease().obj;
          stack.Push(Val.NewFlt(frm.vm, (c.r * k) + (c.g * k)));
          return null;
        },
        new FuncArgSymbol("k", ts.T("float"))
      );

      cl.Define(m);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor", ts.T("Color"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            var r = stack.PopRelease().num;
            var c = new Color();
            c.r = (float)r;
            var v = Val.NewObj(frm.vm, c, ts.T("Color").Get());
            stack.Push(v);
            return null;
          },
        new FuncArgSymbol("r", ts.T("float"))
      );

      ts.ns.Define(fn);
    }
    
    {
      var fn = new FuncSymbolNative("mkcolor_null", ts.T("Color"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            stack.Push(frm.vm.Null);
            return null;
          }
      );

      ts.ns.Define(fn);
    }

    ts.ns.Define(new ArrayTypeSymbolT<Color>("ArrayT_Color", ts.T("Color"), delegate() { return new List<Color>(); } ));

    if(call_setup)
      cl.Setup();
    return cl;
  }

  public void BindColorAlpha(Types ts)
  {
    BindColor(ts);

    {
      var cl = new ClassSymbolNative("ColorAlpha", ts.T("Color"),
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
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
          {
            var c = (ColorAlpha)stack.PopRelease().obj;

            stack.Push(Val.NewFlt(frm.vm, (c.r * c.a) + (c.g * c.a)));

            return null;
          }
        );

        cl.Define(m);
        cl.Setup();
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
      cl.Setup();
    }

    {
      var fn = new FuncSymbolNative("PassthruFoo", ts.T("Foo"),
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            stack.Push(stack.Pop());
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

    cl.Setup();
    return cl;
  }

  public FuncSymbolNative BindTrace(Types ts, StringBuilder log)
  {
    var fn = new FuncSymbolNative("trace", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
          string str = stack.PopRelease().str;
          //for extra debug
          //Console.WriteLine(str);
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
          delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status) { 
            string str = stack.PopRelease().str;
            Console.WriteLine(str); 
            return null;
          },
          new FuncArgSymbol("str", ts.T("string"))
      );
      ts.ns.Define(fn);
    }
  }

  public void BindEnum(Types ts)
  {
    var en = new EnumSymbol("EnumState");
    ts.ns.Define(en);

    en.Define(new EnumItemSymbol("SPAWNED",  10));
    en.Define(new EnumItemSymbol("SPAWNED2", 20));
  }


  public VM MakeVM(string bhl, Func<Types> ts_fn = null, bool show_ast = false, bool show_bytes = false, bool show_parse_tree = false)
  {
    return MakeVM(
        Compile(
          bhl, 
          ts_fn, 
          show_ast: show_ast, 
          show_bytes: show_bytes, 
          show_parse_tree: show_parse_tree
        ), 
      ts_fn);
  }

  public static VM MakeVM(CompiledModule orig_cm, Func<Types> ts_fn = null)
  {
    Types ts = ts_fn == null ? new Types() : ts_fn();

    //let's serialize/unserialize the compiled module so that
    //it's going to go through the full compilation cycle
    var ms = new MemoryStream();
    CompiledModule.ToStream(orig_cm, ms);

    var cm = CompiledModule.FromStream(ts, new MemoryStream(ms.GetBuffer()));

    var vm = new VM(ts);
    vm.LoadModule(cm);
    return vm;
  }

  public VM MakeVM(Dictionary<string, string> file2src, Func<Types> ts_fn = null, bool clean_dir = true, bool use_cache = false)
  {
    if(clean_dir)
      CleanTestDir();

    var files = new List<string>();
    foreach(var kv in file2src)
      NewTestFile(kv.Key, kv.Value, ref files);

    Types ts = ts_fn == null ? new Types() : ts_fn();

    var loader = new ModuleLoader(ts, CompileFiles(files, ts_fn, use_cache));
    var vm = new VM(ts, loader);
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

    public CompiledModule Load(string name, INamedResolver resolver, System.Action<string, string> on_import)
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

  public static int NewTestFile(string path, string text, ref List<string> files, bool unique = false)
  {
    string full_path = Path.Combine(TestDirPath(), path);
    if(unique)
      files.Remove(full_path);
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
    return files.Count-1;
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

  public static int ConstIdx(CompiledModule cm, Proxy<INamed> v)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.INAMED && cn.inamed.Equals(v))
        return i;
    }
    throw new Exception("Constant not found: " + v);
  }

  public static int ConstIdx(CompiledModule cm, Proxy<IType> v)
  {
    for(int i=0;i<cm.constants.Count;++i)
    {
      var cn = cm.constants[i];
      if(cn.type == ConstType.ITYPE && cn.itype.Equals(v))
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

  public void CommonChecks(VM vm, bool check_frames = true, bool check_fibers = true, bool check_coros = true)
  {
    //forced cleanup of module globals
    vm.UnloadModules();

    if(check_frames)
      AssertEqual(0, vm.frames_pool.BusyCount);
    //for extra debug
    if(vm.vals_pool.BusyCount != 0)
      Console.WriteLine(vm.vals_pool.Dump());

    AssertEqual(0, vm.vals_pool.BusyCount);
    AssertEqual(0, vm.vlsts_pool.BusyCount);
    AssertEqual(0, vm.fptrs_pool.BusyCount);
    if(check_fibers)
      AssertEqual(0, vm.fibers_pool.BusyCount);
    if(check_coros)
      AssertEqual(vm.coro_pool.NewCount, vm.coro_pool.DelCount);
  }

  public void SubTest(System.Action fn)
  {
    SubTest("", fn);
  }

  public void SubTest(string name, System.Action fn)
  {
    ++sub_test_idx;
    if(sub_test_idx_filter == -1 || sub_test_idx_filter == sub_test_idx)
    {
      if(BHL_TestRunner.verbose)
        Console.WriteLine(">>>>>> Sub Test(" + sub_test_idx + ") : " + name);
      fn();
    }
  }

  public static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.Combine(Path.GetDirectoryName(self_bin), "tmp", "tests");
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
      throw new Exception("Assertion failed:\n" + a + "\n====\n" + b);
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

  public void AssertError<T>(Action action, string msg, PlaceAssert place_assert = null) where T : Exception
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

    AssertError(err, msg, place_assert);
  }

  public void AssertError(Exception err, string msg, PlaceAssert place_assert = null)
  {
    if(err == null)
      AssertTrue(false, "No error happened, expected: " + msg);

    //TODO: in case of multi errors we consider only the first one,
    //      probably it should be more flexible
    if(err is CompileErrorsException mex)
    {
      err = (Exception)mex.errors[0];
    }

    var idx = err.ToString().IndexOf(msg);
    AssertTrue(idx != -1, "Error message is: " + err);

    if(place_assert != null)
    {
      if(place_assert.err_type != null && err.GetType() != place_assert.err_type)
        AssertTrue(false, "Error types don't match, expected " + place_assert.err_type + ", got " + err.GetType()); 

      if(err is ICompileError cerr)
      {
        string place_err = ErrorUtils.ShowErrorPlace(place_assert.source, cerr.range);
        if(place_err.Trim('\r','\n') != place_assert.expect.Trim('\r','\n'))
          Console.WriteLine(err.StackTrace);
        AssertEqual(place_err.Trim('\r','\n'), place_assert.expect.Trim('\r','\n'));
      }
      else
        AssertTrue(false, "No ICompileError occured, got " + err?.GetType().Name); 
    }
  }

  public class PlaceAssert
  {
    public string source;
    public string expect;
    public System.Type err_type;

    public PlaceAssert(string source, string expect)
    {
      this.source = source;
      this.expect = expect;
    }

    public PlaceAssert(System.Type err_type, string source, string expect)
    {
      this.err_type = err_type;
      this.source = source;
      this.expect = expect;
    }
  }

  static public CompileConf MakeCompileConf(List<string> files, Func<Types> ts_fn = null, bool use_cache = false, int max_threads = 1)
  {
    Types ts = ts_fn == null ? new Types() : ts_fn();

    var conf = new CompileConf();
    conf.max_threads = max_threads;
    conf.module_fmt = ModuleBinaryFormat.FMT_BIN;
    conf.ts = ts;
    conf.files = Util.MakeUris(files);
    conf.res_file = TestDirPath() + "/result.bin";
    conf.inc_path.Add(TestDirPath());
    conf.tmp_dir = TestDirPath() + "/cache";
    conf.err_file = TestDirPath() + "/error.log";
    conf.use_cache = use_cache;
    conf.verbose = BHL_TestRunner.verbose;

    return conf;
  }

  //NOTE: returns stream of bhl compiled data
  static public Stream CompileFiles(CompilationExecutor exec, CompileConf conf)
  {
    var errors = exec.Exec(conf);
    if(errors.Count > 0)
    {
      if(conf.verbose)
      {
        foreach(var err in errors)
        {
          Console.Error.WriteLine(err.ToString());
          if(!string.IsNullOrEmpty(err.stack_trace))
            Console.Error.WriteLine(err.stack_trace);
          Console.Error.WriteLine("==========");
        }
      }
      throw new CompileErrorsException(errors);
    }

    var ms = new MemoryStream(File.ReadAllBytes(conf.res_file));
    return ms;
  }

  static public Stream CompileFiles(List<string> files, Func<Types> ts_fn = null, bool use_cache = false, int max_threads = 1)
  {
    return CompileFiles(MakeCompileConf(files, ts_fn, use_cache, max_threads));
  }

  static public Stream CompileFiles(CompileConf conf)
  {
    return CompileFiles(new CompilationExecutor(), conf);
  }

  public CompiledModule Compile(string bhl, Func<Types> ts_fn = null, bool show_ast = false, bool show_bytes = false, bool show_parse_tree = false)
  {
    Types ts = ts_fn == null ? new Types() : ts_fn();

    var proc = Parse(bhl, ts, show_ast: show_ast, show_parse_tree: show_parse_tree, throw_errors: true);

    var c  = new ModuleCompiler(proc.result);
    var cm = c.Compile();
    if(show_bytes)
      Dump(c);
    return cm;
  }

  public ANTLR_Processor Parse(
    string bhl, 
    Types ts, 
    bool show_ast = false, 
    bool show_parse_tree = false, 
    bool throw_errors = false
  ) 
  {
    var mdl = new bhl.Module(ts, "");
    var errors = new CompileErrors();
    var proc = ANTLR_Processor.MakeProcessor(
      mdl, 
      new FileImports(), 
      bhl.ToStream(), 
      ts, 
      errors,
      ErrorHandlers.MakeStandard("", errors)
    );

    if(show_parse_tree)
      Console.WriteLine(proc.parsed);

    ANTLR_Processor.ProcessAll(
      new Dictionary<Uri, ANTLR_Processor>() {{new Uri("file://"), proc}}, 
      null,
      new IncludePath()
    );

    if(show_ast)
      AST_Dumper.Dump(proc.result.ast);

    if(throw_errors && proc.result.errors.Count > 0)
      throw new CompileErrorsException(proc.result.errors);

    return proc;
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

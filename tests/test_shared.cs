using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using bhl;
using Xunit;

public class BHL_TestBase
{
  public const string TestModuleName = "";

  protected static void BindMin(Types ts)
  {
    var fn = new FuncSymbolNative(new Origin(), "min", ts.T("float"),
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
      {
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

  public struct IntStruct
  {
    public int n;

    public static void Decode(Val v, ref IntStruct dst)
    {
      dst.n = (int)v._num;
    }

    public static void Encode(Val v, IntStruct src, IType type)
    {
      v.type = type;
      v._num = src.n;
    }
  }

  public static void BindIntStruct(Types ts)
  {
    {
      var cl = new ClassSymbolNative(new Origin(), "IntStruct",
        delegate(VM.Frame frm, ref Val v, IType type)
        {
          var s = new IntStruct();
          IntStruct.Encode(v, s, type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "n", Types.Int,
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          v.num = s.n;
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var s = new IntStruct();
          IntStruct.Decode(ctx, ref s);
          s.n = (int)v.num;
          IntStruct.Encode(ctx, s, ctx.type);
        }
      ));
      cl.Setup();
    }
  }

  public class StringClass
  {
    public string str;
  }

  public void BindStringClass(Types ts)
  {
    {
      var cl = new ClassSymbolNative(new Origin(), "StringClass",
        delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(new StringClass(), type); }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "str", ts.T("string"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (StringClass)ctx.obj;
          v.str = c.str;
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (StringClass)ctx.obj;
          c.str = v.str;
          ctx.SetObj(c, ctx.type);
        }
      ));
      cl.Setup();
    }
  }

  public void BindFail(Types ts)
  {
    var fn = new FuncSymbolNative(new Origin(), "fail", ts.T("void"),
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
      return "[r=" + r + ",g=" + g + "]";
    }
  }

  public class ColorAlpha : Color
  {
    public float a;

    public override string ToString()
    {
      return "[r=" + r + ",g=" + g + ",a=" + a + "]";
    }
  }

  public class ColorNested
  {
    public Color c = new Color();
  }

  public ClassSymbolNative BindColor(Types ts, bool call_setup = true)
  {
    var cl = new ClassSymbolNative(new Origin(), "Color", null,
      delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(new Color(), type); },
      typeof(Color)
    );

    ts.ns.Define(cl);
    cl.Define(new FieldSymbol(new Origin(), "r", ts.T("float"),
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
    cl.Define(new FieldSymbol(new Origin(), "g", ts.T("float"),
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
      var m = new FuncSymbolNative(new Origin(), "Add", ts.T("Color"),
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
      var m = new FuncSymbolNative(new Origin(), "mult_summ", ts.T("float"),
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
      var fn = new FuncSymbolNative(new Origin(), "mkcolor", ts.T("Color"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
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
      var fn = new FuncSymbolNative(new Origin(), "mkcolor_null", ts.T("Color"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          stack.Push(frm.vm.Null);
          return null;
        }
      );

      ts.ns.Define(fn);
    }

    var arrT_Color = new NativeListTypeSymbol<Color>(
      new Origin(),
      "ArrayT_Color",
      (v) => (Color)v.obj,
      (_vm, itype, n) => Val.NewObj(_vm, n, cl),
      ts.T("Color")
    );
    ts.ns.Define(arrT_Color);
    arrT_Color.Setup();

    if(call_setup)
      cl.Setup();
    return cl;
  }

  public void BindColorAlpha(Types ts)
  {
    BindColor(ts);

    {
      var cl = new ClassSymbolNative(new Origin(), "ColorAlpha", ts.T("Color"),
        delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(new ColorAlpha(), type); },
        typeof(ColorAlpha)
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "a", ts.T("float"),
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
        var m = new FuncSymbolNative(new Origin(), "mult_summ_alpha", ts.T("float"),
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

  public struct MasterStruct
  {
    public StringClass child;
    public StringClass child2;
    public IntStruct child_struct;
    public IntStruct child_struct2;
  }

  public void BindMasterStruct(Types ts)
  {
    BindStringClass(ts);
    BindIntStruct(ts);

    {
      var cl = new ClassSymbolNative(new Origin(), "MasterStruct",
        delegate(VM.Frame frm, ref Val v, IType type)
        {
          var o = new MasterStruct();
          o.child = new StringClass();
          o.child2 = new StringClass();
          v.SetObj(o, type);
        }
      );

      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "child", ts.T("StringClass"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          c.child = (StringClass)v._obj;
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol(new Origin(), "child2", ts.T("StringClass"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          v.SetObj(c.child2, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          c.child2 = (StringClass)v.obj;
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol(new Origin(), "child_struct", ts.T("IntStruct"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct = s;
          ctx.SetObj(c, ctx.type);
        }
      ));

      cl.Define(new FieldSymbol(new Origin(), "child_struct2", ts.T("IntStruct"),
        delegate(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct.Encode(v, c.child_struct2, fld.type.Get());
        },
        delegate(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
        {
          var c = (MasterStruct)ctx.obj;
          IntStruct s = new IntStruct();
          IntStruct.Decode(v, ref s);
          c.child_struct2 = s;
          ctx.SetObj(c, ctx.type);
        }
      ));
      cl.Setup();
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
      var cl = new ClassSymbolNative(new Origin(), "Foo", null,
        delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(new Foo(), type); }
      );
      ts.ns.Define(cl);

      cl.Define(new FieldSymbol(new Origin(), "hey", Types.Int,
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
      cl.Define(new FieldSymbol(new Origin(), "colors", ts.T("ArrayT_Color"),
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
      cl.Define(new FieldSymbol(new Origin(), "sub_color", ts.T("Color"),
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
      var fn = new FuncSymbolNative(new Origin(), "PassthruFoo", ts.T("Foo"),
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
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
    var cl = new ClassSymbolNative(new Origin(), "Bar", null,
      delegate(VM.Frame frm, ref Val v, IType type) { v.SetObj(new Bar(), type); }
    );

    ts.ns.Define(cl);
    cl.Define(new FieldSymbol(new Origin(), "Int", Types.Int,
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
    cl.Define(new FieldSymbol(new Origin(), "Flt", ts.T("float"),
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
    cl.Define(new FieldSymbol(new Origin(), "Str", ts.T("string"),
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
    var fn = new FuncSymbolNative(new Origin(), "trace", Types.Void,
      delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
      {
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
      var fn = new FuncSymbolNative(new Origin(), "log", Types.Void,
        delegate(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
        {
          string str = stack.PopRelease().str;
          Console.WriteLine(str);
          return null;
        },
        new FuncArgSymbol("str", ts.T("string"))
      );
      ts.ns.Define(fn);
    }
  }

  public VM MakeVM(
    string bhl,
    Action<Types> ts_fn = null,
    bool show_ast = false,
    bool show_bytes = false,
    bool show_parse_tree = false,
    HashSet<string> defines = null
  )
  {
    return MakeVM(
      Compile(
        bhl,
        ts_fn,
        show_ast: show_ast,
        show_bytes: show_bytes,
        show_parse_tree: show_parse_tree,
        defines: defines
      ),
      ts_fn);
  }

  public static VM MakeVM(bhl.Module orig_cm, Action<Types> ts_fn = null)
  {
    Types ts = new Types();
    ts_fn?.Invoke(ts);

    //let's serialize/unserialize the compiled module so that
    //it's going to go through the full compilation cycle
    var ms = new MemoryStream();
    CompiledModule.ToStream(orig_cm, ms);

    var cm = CompiledModule.FromStream(ts, new MemoryStream(ms.GetBuffer()));

    var vm = new VM(ts);
    vm.LoadModule(cm);
    return vm;
  }

  public static List<string> MakeFiles(Dictionary<string, string> file2src, bool clean_dir = true)
  {
    if(clean_dir)
      CleanTestDir();

    var files = new List<string>();
    foreach(var kv in file2src)
      NewTestFile(kv.Key, kv.Value, ref files);
    return files;
  }

  public static async Task<VM> MakeVM(CompileConf conf, Action<Types> ts_fn = null, CompilationExecutor exec = null)
  {
    Types ts = new Types();
    ts_fn?.Invoke(ts);

    var stream = await CompileFiles(exec ?? new CompilationExecutor(), conf);

    var loader = new ModuleLoader(ts, stream);
    var vm = new VM(ts, loader);
    return vm;
  }

  public static async Task<VM> MakeVM(List<string> files, Action<Types> ts_fn = null, bool use_cache = false,
    CompilationExecutor executor = null)
  {
    Types ts = new Types();
    ts_fn?.Invoke(ts);

    var loader = new ModuleLoader(ts, await CompileFiles(files, ts_fn, use_cache: use_cache, executor: executor));
    var vm = new VM(ts, loader);
    return vm;
  }

  public static Task<VM> MakeVM(Dictionary<string, string> file2src, Action<Types> ts_fn = null, bool clean_dir = true,
    bool use_cache = false)
  {
    return MakeVM(MakeFiles(file2src, clean_dir), ts_fn, use_cache);
  }

  public VM.Fiber Execute(VM vm, string fn_name, params Val[] args)
  {
    return Execute(vm, fn_name, args?.Length ?? 0, args);
  }

  public VM.Fiber Execute(VM vm, string fn_name, FuncArgsInfo args_info, params Val[] args)
  {
    var fb = vm.Start(fn_name, args_info, new StackList<Val>(args));
    const int LIMIT = 20;
    int c = 0;
    for(; c < LIMIT; ++c)
    {
      if(!vm.Tick())
        return fb;
    }
    throw new Exception("Too many iterations: " + c);
  }

  public class TestImporter : IModuleLoader
  {
    public Dictionary<string, bhl.Module> mods = new Dictionary<string, bhl.Module>();

    public bhl.Module Load(string name, INamedResolver resolver)
    {
      return mods[name];
    }
  }

  public static void CleanTestDir()
  {
    string dir = TestDirPath();
    if(Directory.Exists(dir))
      Directory.Delete(dir, true /*recursive*/);
  }

  public static int NewTestFile(string path, string text, ref List<string> files, bool unique = false)
  {
    string full_path = Path.Combine(TestDirPath(), path);
    if(unique)
      files.Remove(full_path);
    Directory.CreateDirectory(Path.GetDirectoryName(full_path));
    File.WriteAllText(full_path, text);
    files.Add(full_path);
    return files.Count - 1;
  }

  public static int ConstIdx(bhl.Module module, string str)
  {
    for(int i = 0; i < module.compiled.constants.Length; ++i)
    {
      var cn = module.compiled.constants[i];
      if(cn.type == ConstType.STR && cn.str == str)
        return i;
    }

    throw new Exception("Constant not found: " + str);
  }

  public static int ConstIdx(bhl.Module module, int num)
  {
    for(int i = 0; i < module.compiled.constants.Length; ++i)
    {
      var cn = module.compiled.constants[i];
      if(cn.type == ConstType.INT && cn.num == num)
        return i;
    }

    throw new Exception("Constant not found: " + num);
  }

  public static int ConstIdx(bhl.Module module, double num)
  {
    for(int i = 0; i < module.compiled.constants.Length; ++i)
    {
      var cn = module.compiled.constants[i];
      if(cn.type == ConstType.FLT && cn.num == num)
        return i;
    }

    throw new Exception("Constant not found: " + num);
  }

  public static int ConstIdx(bhl.Module module, bool v)
  {
    for(int i = 0; i < module.compiled.constants.Length; ++i)
    {
      var cn = module.compiled.constants[i];
      if(cn.type == ConstType.BOOL && cn.num == (v ? 1 : 0))
        return i;
    }

    throw new Exception("Constant not found: " + v);
  }

  public static int TypeIdx(bhl.Module module, ProxyType v)
  {
    return module.compiled.type_refs.GetIndex(v);
  }

  public static int ConstNullIdx(bhl.Module module)
  {
    for(int i = 0; i < module.compiled.constants.Length; ++i)
    {
      var cn = module.compiled.constants[i];
      if(cn.type == ConstType.NIL)
        return i;
    }

    throw new Exception("Constant null not found");
  }

  public static void CommonChecks(VM vm, bool check_frames = true, bool check_fibers = true, bool check_coros = true)
  {
    //forced cleanup of module globals
    vm.UnloadModules();

    if(check_frames)
      Assert.Equal(0, vm.frames_pool.BusyCount);
    //for extra debug
    if(vm.vals_pool.BusyCount != 0)
      Console.WriteLine(vm.vals_pool.Dump());

    Assert.Equal(0, vm.vals_pool.BusyCount);
    Assert.Equal(0, vm.vlsts_pool.BusyCount);
    Assert.Equal(0, vm.fptrs_pool.BusyCount);
    if(check_fibers)
      Assert.Equal(0, vm.fibers_pool.BusyCount);
    if(check_coros)
      Assert.Equal(vm.coro_pool.NewCount, vm.coro_pool.DelCount);
  }

  public static string TestDirPath()
  {
    string self_bin = System.Reflection.Assembly.GetExecutingAssembly().Location;
    return Path.Combine(Path.GetDirectoryName(self_bin), "tmp", "tests");
  }

  public static bool IsUnix()
  {
    int p = (int)Environment.OSVersion.Platform;
    return (p == 4) || (p == 6) || (p == 128);
  }

  public static void AssertContains(string haystack, string needle)
  {
    if(haystack.IndexOf(needle) == -1)
      throw new Exception("String:\n" + haystack + "\n== doesn't contain ==\n" + needle);
  }

  public static void AssertEqual(string a, string b)
  {
    if(a != null && b != null && !a.Equals(b, StringComparison.Ordinal))
    {
      Console.WriteLine("A:\n" + a);
      Console.WriteLine("B:\n" + b);
    }

    Assert.Equal(a, b);
  }

  public void AssertError(Action action, string msg, PlaceAssert place_assert = null)
  {
    AssertError<Exception>(action, msg, place_assert);
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

  public async Task AssertErrorAsync<T>(Func<Task> action, string msg, PlaceAssert place_assert = null) where T : Exception
  {
    Exception err = null;
    try
    {
      await action();
    }
    catch(T e)
    {
      err = e;
    }

    AssertError(err, msg, place_assert);
  }

  public void AssertError(Exception err, string msg, PlaceAssert place_assert = null)
  {
    //let's normalize line endings
    msg = msg.Replace("\r", "");

    if(err == null)
      Assert.Fail("No error happened, expected: " + msg);

    //TODO: in case of multi errors we consider only the first one,
    //      probably it should be more flexible
    if(err is CompileErrorsException mex)
    {
      err = (Exception)mex.errors[0];
    }

    //let's normalize line endings
    var err_str = err.ToString().Replace("\r", "");
    var idx = err_str.IndexOf(msg);
    if(idx == -1)
    {
      Console.WriteLine("Actual:\n" + err_str);
      Console.WriteLine("Expected:\n" + msg);
    }

    Assert.Contains(msg, err_str);

    if(place_assert != null)
    {
      if(place_assert.err_type != null && err.GetType() != place_assert.err_type)
        Assert.Fail("Error types don't match, expected " + place_assert.err_type + ", got " + err.GetType());

      if(err is ICompileError cerr)
      {
        string place_err = ErrorUtils.ShowErrorPlace(place_assert.source, cerr.range);
        if(place_err.Trim('\r', '\n') != place_assert.expect.Trim('\r', '\n'))
          Console.WriteLine(err.StackTrace);
        Assert.Equal(place_err.Trim('\r', '\n'), place_assert.expect.Trim('\r', '\n'));
      }
      else
        Assert.Fail("No ICompileError occured, got " + err?.GetType().Name);
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

  public static CompileConf MakeCompileConf(
    List<string> files,
    Action<Types> ts_fn = null,
    bool use_cache = false,
    int max_threads = 1,
    List<string> src_dirs = null,
    List<string> inc_paths = null
  )
  {
    Types ts = new Types();
    ts_fn?.Invoke(ts);

    var proj = new ProjectConf();
    proj.max_threads = max_threads;
    if(src_dirs != null)
    {
      foreach(var src_dir in src_dirs)
        proj.src_dirs.Add(src_dir);
    }
    else
      proj.src_dirs.Add(TestDirPath());

    if(inc_paths != null)
    {
      foreach(var path in inc_paths)
        proj.inc_path.Add(path);
    }

    proj.module_fmt = ModuleBinaryFormat.FMT_BIN;
    proj.result_file = TestDirPath() + "/result.bin";
    proj.tmp_dir = TestDirPath() + "/cache";
    proj.error_file = TestDirPath() + "/error.log";
    proj.use_cache = use_cache;
    proj.verbosity = 0;
    proj.Setup();

    var conf = new CompileConf();
    conf.ts = ts;
    conf.logger = new Logger(proj.verbosity, new ConsoleLogger());
    conf.proj = proj;
    conf.files = BuildUtils.NormalizeFilePaths(files);

    return conf;
  }

  //NOTE: returns stream of bhl compiled data
  public static async Task<Stream> CompileFiles(CompilationExecutor exec, CompileConf conf)
  {
    var errors = await exec.Exec(conf);
    if(errors.Count > 0)
    {
      if(conf.proj.verbosity > 0)
      {
        foreach(var err in errors)
        {
          await Console.Error.WriteLineAsync(err.ToString());
          if(!string.IsNullOrEmpty(err.stack_trace))
            await Console.Error.WriteLineAsync(err.stack_trace);
          await Console.Error.WriteLineAsync("==========");
        }
      }

      throw new CompileErrorsException(errors);
    }

    var ms = new MemoryStream(File.ReadAllBytes(conf.proj.result_file));
    return ms;
  }

  public static Task<Stream> CompileFiles(List<string> files, Action<Types> ts_fn = null, bool use_cache = false,
    int max_threads = 1, CompilationExecutor executor = null)
  {
    return CompileFiles(MakeCompileConf(files, ts_fn, use_cache: use_cache, max_threads: max_threads),
      executor: executor);
  }

  public static Task<Stream> CompileFiles(
    Dictionary<string, string> file2src, Action<Types> ts_fn = null,
    bool use_cache = false, int max_threads = 1,
    bool clean_dir = true, CompilationExecutor executor = null)
  {
    return CompileFiles(MakeFiles(file2src, clean_dir), ts_fn, use_cache: use_cache, max_threads: max_threads,
      executor: executor);
  }

  public static Task<Stream> CompileFiles(CompileConf conf, CompilationExecutor executor = null)
  {
    return CompileFiles(executor ?? new CompilationExecutor(), conf);
  }

  public bhl.Module Compile(
    string bhl,
    Action<Types> ts_fn = null,
    bool show_ast = false,
    bool show_bytes = false,
    bool show_parse_tree = false,
    HashSet<string> defines = null
  )
  {
    var ts = new Types();
    ts_fn?.Invoke(ts);

    var proc = Parse(
      bhl,
      ts,
      show_ast: show_ast,
      show_parse_tree: show_parse_tree,
      throw_errors: true,
      defines: defines
    );

    var c = new ModuleCompiler(proc.result);
    var result = c.Compile();
    if(show_bytes)
      Dump(c);
    return result;
  }

  public ANTLR_Processor Parse(
    string bhl,
    Types ts,
    bool show_ast = false,
    bool show_parse_tree = false,
    bool throw_errors = false,
    HashSet<string> defines = null
  )
  {
    var mdl = new bhl.Module(ts, TestModuleName);
    var proc = ANTLR_Processor.ParseAndMakeProcessor(
      mdl,
      new FileImports(),
      bhl.ToStream(),
      ts,
      CompileErrorsHub.MakeEmpty(),
      defines,
      out var preproc_parsed
    );

    if(show_parse_tree)
    {
      if(preproc_parsed != null)
      {
        Console.WriteLine("<PREPROC>");
        Console.WriteLine(preproc_parsed);
        Console.WriteLine("</PREPROC>");
      }

      Console.WriteLine(proc.parsed);
    }

    var proc_bundle = new ProjectCompilationStateBundle(ts);
    proc_bundle.file2proc = new Dictionary<string, ANTLR_Processor>() { { TestModuleName, proc } };
    proc_bundle.file2cached = null;
    ANTLR_Processor.ProcessAll(proc_bundle);

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
    for(int i = 0; i < (a.Length > b.Length ? a.Length : b.Length); i++)
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
    var bs = c.Compile().compiled.bytecode;
    ModuleCompiler.Definition op = null;
    int op_size = 0;

    for(int i = 0; i < bs.Length; i++)
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
        op_size = ModuleCompiler.PredictOpcodeSize(op, bs, i);
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

  public static void AssertEqual(bhl.Module ca, ModuleCompiler cb)
  {
    AssertEqual(ca, cb.Compile());
  }

  public static void AssertEqual(bhl.Module ca, bhl.Module cb)
  {
    string cmp;

    if(!CompareCode(ca.compiled.initcode, cb.compiled.initcode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: init bytes not equal");
    }

    if(!CompareCode(ca.compiled.bytecode, cb.compiled.bytecode, out cmp))
    {
      Console.WriteLine(cmp);
      throw new Exception("Assertion failed: bytes not equal");
    }
  }

  static void Dump(ModuleCompiler c)
  {
    Dump(c.Compile());
  }

  static void Dump(Module module)
  {
    if(module.compiled?.initcode?.Length > 0)
    {
      Console.WriteLine("=== INIT ===");
      ModuleCompiler.Dump(module.compiled.initcode);
    }

    Console.WriteLine("=== CODE ===");
    ModuleCompiler.Dump(module.compiled.bytecode);
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
    for(int i = 0; i < (a?.Length > b?.Length ? a?.Length : b?.Length); i++)
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
          aop_size = ModuleCompiler.PredictOpcodeSize(aop, a, i);
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
          bop_size = ModuleCompiler.PredictOpcodeSize(bop, b, i);
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

    for(int i = 1; i <= lens.Count; ++i)
    {
      cmp = cmp.Replace("{fill" + i + "}", new String(' ', max_len - lens[i - 1]));
    }

    return equal;
  }

  public static void AssertEqual(List<Const> cas, List<Const> cbs)
  {
    Assert.Equal(cas.Count, cbs.Count);
    for(int i = 0; i < cas.Count; ++i)
    {
      Assert.Equal((int)cas[i].type, (int)cbs[i].type);
      Assert.Equal(cas[i].num, cbs[i].num);
      AssertEqual(cas[i].str, cbs[i].str);
    }
  }
}

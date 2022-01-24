using System;
using System.Reflection;
using System.Collections.Generic;
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
  public static GlobalScope Clone(this GlobalScope globs)
  {
    var globs_copy = new GlobalScope();
    var ms = globs.GetMembers();
    for(int i=0;i<ms.Count;++i)
      globs_copy.Define(ms[i]);
    return globs_copy;
  }

  public static void Decode(this DynVal dv, ref List<string> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add(tmp.str);
    }
  }

  public static void Encode(this DynVal dv, List<string> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewStr(dst[i]));
    dv.SetObj(lst);
  }

  public static void Decode(this DynVal dv, ref List<uint> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((uint)tmp.num);
    }
  }

  public static void Encode(this DynVal dv, List<uint> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewNum(dst[i]));
    dv.SetObj(lst);
  }

  public static void Decode(this DynVal dv, ref List<int> dst)
  {
    dst.Clear();
    var src = (DynValList)dv.obj;
    for(int i=0;i<src.Count;++i)
    {
      var tmp = src[i];
      dst.Add((int)tmp.num);
    }
  }

  public static void Encode(this DynVal dv, List<int> dst)
  {
    var lst = DynValList.New();
    for(int i=0;i<dst.Count;++i)
      lst.Add(DynVal.NewNum(dst[i]));
    dv.SetObj(lst);
  }

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
      { "verbose", "don't use cache",
        v => verbose = v != null },
     };

    var names = p.Parse(args);

    Run(names, new BHL_TestNodes(), verbose);
    Run(names, new BHL_TestVM(), verbose);
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
        Util.SetupASTFactory();
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
  public static void Assert(bool condition, string msg = null)
  {
    if(!condition)
      throw new Exception("Assertion failed " + (msg != null ? msg : ""));
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
    var idx = err.Message.IndexOf(msg);
    AssertTrue(idx != -1, "Error message is: " + err.Message);
  }
}

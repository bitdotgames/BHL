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
  public static Types Clone(this Types ts)
  {
    var ts_copy = new Types();
    var ms = ts.globs.GetMembers();
    //let's skip already defined built-ins
    for(int i=ts_copy.globs.GetMembers().Count;i<ms.Count;++i)
      ts_copy.globs.Define(ms[i]);
    return ts_copy;
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
    Run(names, new TestLSP(), verbose);
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
}

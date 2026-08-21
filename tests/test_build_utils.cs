using System;
using System.IO;
using System.Linq;
using bhl;
using Xunit;

public class TestBuildUtils
{
  static string MakeTempDir()
  {
    var dir = Path.Combine(Path.GetTempPath(), "bhl_glob_test_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  [Fact]
  public void TestGlobLiteralPathIsReturnedUnchanged()
  {
    var res = BuildUtils.Glob("some/literal/path.bhl");
    Assert.Equal(new[] { "some/literal/path.bhl" }, res);
  }

  [Fact]
  public void TestGlobTrailingWildcard()
  {
    var root = MakeTempDir();
    try
    {
      File.WriteAllText(Path.Combine(root, "a.bhl"), "");
      File.WriteAllText(Path.Combine(root, "b.bhl"), "");
      File.WriteAllText(Path.Combine(root, "c.txt"), "");

      var res = BuildUtils.Glob(root.Replace('\\', '/') + "/*.bhl");

      Assert.Equal(2, res.Count);
      Assert.Contains(res, f => f.EndsWith("a.bhl"));
      Assert.Contains(res, f => f.EndsWith("b.bhl"));
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  //NOTE: mirrors a UPM package resolved into Library/PackageCache/<name>@<hash>/...
  [Fact]
  public void TestGlobMiddleSegmentWildcard()
  {
    var root = MakeTempDir();
    try
    {
      var pkg_dir = Path.Combine(root, "com.bitgames.unitybhl@a1b2c3d4", "Runtime", "Bindings");
      Directory.CreateDirectory(pkg_dir);
      File.WriteAllText(Path.Combine(pkg_dir, "UnityBindings.bhl"), "");

      var unrelated_dir = Path.Combine(root, "com.other.package@zzzz", "Runtime", "Bindings");
      Directory.CreateDirectory(unrelated_dir);
      File.WriteAllText(Path.Combine(unrelated_dir, "Other.bhl"), "");

      var pattern = root.Replace('\\', '/') + "/com.bitgames.unitybhl@*/Runtime/Bindings/*.bhl";
      var res = BuildUtils.Glob(pattern);

      Assert.Single(res);
      Assert.EndsWith("UnityBindings.bhl", res[0]);
      Assert.Contains("com.bitgames.unitybhl@a1b2c3d4", res[0]);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [Fact]
  public void TestGlobNoMatchingDirectoryReturnsEmpty()
  {
    var root = MakeTempDir();
    try
    {
      var pattern = root.Replace('\\', '/') + "/com.bitgames.unitybhl@*/Runtime/Bindings/*.bhl";
      var res = BuildUtils.Glob(pattern);

      Assert.Empty(res);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }
}

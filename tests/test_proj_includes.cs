using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Xunit;

// Coverage for ProjectConf.includes - folding a "library" bhl.proj's src_dirs/bindings/
// inc_dirs into the including project (see ProjectConf.ExpandIncludes).
public class TestProjIncludes
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_proj_includes_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  static string WriteFile(string path, string content)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, content);
    return path;
  }

  static string WriteScriptedBinding(string path, string decl_name, string version)
  {
    return WriteFile(path, $@"
import ""std/bind""
func string,string BindingInfo() {{
  return ""{decl_name}"", ""{version}""
}}
func RegisterBindings(std.bind.Types types) {{
}}
");
  }

  static ProjectConf MakeProj(string dir)
  {
    var proj = new ProjectConf();
    proj.proj_file = Path.Combine(dir, "bhl.proj");
    proj.tmp_dir = Path.Combine(dir, "tmp");
    Directory.CreateDirectory(proj.tmp_dir);
    proj.use_cache = false;
    return proj;
  }

  [Fact]
  public void IncludeExpandsSrcDirsAndBindings()
  {
    var dir = MakeTempDir();
    try
    {
      var lib_dir = Path.Combine(dir, "pkg");
      Directory.CreateDirectory(Path.Combine(lib_dir, "scripts"));
      WriteScriptedBinding(Path.Combine(lib_dir, "bindings.bhl"), "unity", "1.0.0");
      WriteFile(Path.Combine(lib_dir, "bhl.proj"),
        @"{ ""src_dirs"": [""./scripts""], ""bindings"": [{ ""name"": ""unity"", ""sources"": [""./bindings.bhl""] }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(lib_dir, "bhl.proj"));
      proj.Setup();

      Assert.Single(proj.src_dirs);
      Assert.Equal(BuildUtils.NormalizeFilePath(Path.Combine(lib_dir, "scripts")), proj.src_dirs[0]);

      Assert.Single(proj.bindings);
      Assert.Equal("unity", proj.bindings[0].name);
      Assert.Equal(BuildUtils.NormalizeFilePath(Path.Combine(lib_dir, "bindings.bhl")), proj.bindings[0].sources[0]);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeResolvesPathsRelativeToIncludedFileNotProjFile()
  {
    var dir = MakeTempDir();
    try
    {
      //NOTE: the library's bhl.proj lives in a DIFFERENT dir than the consumer's bhl.proj,
      //      to prove its own relative paths resolve against itself, not proj_file
      var lib_dir = Path.Combine(dir, "pkg");
      Directory.CreateDirectory(Path.Combine(lib_dir, "sub"));
      WriteFile(Path.Combine(lib_dir, "bhl.proj"), @"{ ""src_dirs"": [""./sub""] }");

      var proj = MakeProj(Path.Combine(dir, "consumer"));
      proj.includes.Add(Path.Combine(lib_dir, "bhl.proj"));
      proj.Setup();

      Assert.Equal(BuildUtils.NormalizeFilePath(Path.Combine(lib_dir, "sub")), proj.src_dirs[0]);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void RelativeIncludePathResolvesAgainstProjFile()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""rel"" }] }");

      var proj = MakeProj(dir);
      proj.includes.Add("./pkg/bhl.proj");
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.Equal("rel", proj.bindings[0].name);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithWildcardResolvesSingleMatch()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg@1.2.3", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""wild"" }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg@*", "bhl.proj"));
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.Equal("wild", proj.bindings[0].name);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithNoMatchesThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "nope@*", "bhl.proj"));

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithMultipleMatchesThrows()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg@1.0.0", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""dup"" }] }");
      WriteFile(Path.Combine(dir, "pkg@2.0.0", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""dup"" }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg@*", "bhl.proj"));

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithNonEmptyDefinesThrows()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg", "bhl.proj"), @"{ ""defines"": [""FOO""] }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg", "bhl.proj"));

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithLegacyBindingsFieldsThrows()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg", "bhl.proj"), @"{ ""bindings_dll"": ""./bindings.dll"" }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg", "bhl.proj"));

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeWithPostprocFieldsThrows()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg", "bhl.proj"), @"{ ""postproc_dll"": ""./postproc.dll"" }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg", "bhl.proj"));

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludedScalarFieldsAreIgnored()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "pkg", "bhl.proj"),
        @"{ ""result_file"": ""./should_be_ignored.bytes"", ""tmp_dir"": ""./should_be_ignored"", ""verbosity"": 99 }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(dir, "pkg", "bhl.proj"));
      var expected_tmp_dir = proj.tmp_dir;
      proj.Setup();

      Assert.Empty(proj.result_file);
      Assert.Equal(BuildUtils.NormalizeFilePath(expected_tmp_dir), proj.tmp_dir);
      Assert.Equal(1, proj.verbosity);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void NestedIncludeCycleThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var a_path = Path.Combine(dir, "a", "bhl.proj");
      var b_path = Path.Combine(dir, "b", "bhl.proj");
      WriteFile(a_path, $@"{{ ""includes"": [{JsonQuote(b_path)}] }}");
      WriteFile(b_path, $@"{{ ""includes"": [{JsonQuote(a_path)}] }}");

      var proj = MakeProj(dir);
      proj.includes.Add(a_path);

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void NestedIncludeExpandsTransitively()
  {
    var dir = MakeTempDir();
    try
    {
      var inner_path = Path.Combine(dir, "inner", "bhl.proj");
      var outer_path = Path.Combine(dir, "outer", "bhl.proj");
      WriteFile(inner_path, @"{ ""bindings"": [{ ""name"": ""inner_binding"" }] }");
      WriteFile(outer_path, $@"{{ ""includes"": [{JsonQuote(inner_path)}] }}");

      var proj = MakeProj(dir);
      proj.includes.Add(outer_path);
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.Equal("inner_binding", proj.bindings[0].name);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void DiamondSharedIncludeIsFoldedInOnlyOnce()
  {
    var dir = MakeTempDir();
    try
    {
      //NOTE: 'root' includes both 'a' and 'b', which each include the same shared 'common' -
      //      common's binding must end up in root exactly once, not duplicated per path
      var common_path = Path.Combine(dir, "common", "bhl.proj");
      var a_path = Path.Combine(dir, "a", "bhl.proj");
      var b_path = Path.Combine(dir, "b", "bhl.proj");
      WriteFile(common_path, @"{ ""bindings"": [{ ""name"": ""common_binding"" }] }");
      WriteFile(a_path, $@"{{ ""includes"": [{JsonQuote(common_path)}], ""bindings"": [{{ ""name"": ""a_binding"" }}] }}");
      WriteFile(b_path, $@"{{ ""includes"": [{JsonQuote(common_path)}], ""bindings"": [{{ ""name"": ""b_binding"" }}] }}");

      var proj = MakeProj(dir);
      proj.includes.Add(a_path);
      proj.includes.Add(b_path);
      proj.Setup();

      Assert.Equal(3, proj.bindings.Count);
      Assert.Single(proj.bindings, x => x.name == "common_binding");
      Assert.Single(proj.bindings, x => x.name == "a_binding");
      Assert.Single(proj.bindings, x => x.name == "b_binding");
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void SameNameFromTwoDifferentIncludesThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var a_path = Path.Combine(dir, "a", "bhl.proj");
      var b_path = Path.Combine(dir, "b", "bhl.proj");
      WriteFile(a_path, @"{ ""bindings"": [{ ""name"": ""dup"" }] }");
      WriteFile(b_path, @"{ ""bindings"": [{ ""name"": ""dup"" }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(a_path);
      proj.includes.Add(b_path);

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void SameNameBetweenRootAndIncludeThrows()
  {
    var dir = MakeTempDir();
    try
    {
      var lib_path = Path.Combine(dir, "pkg", "bhl.proj");
      WriteFile(lib_path, @"{ ""bindings"": [{ ""name"": ""dup"" }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(lib_path);
      proj.bindings.Add(new BindingsEntryConf { name = "dup" });

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludedBindingLoadsCorrectlyEndToEnd()
  {
    var dir = MakeTempDir();
    try
    {
      var lib_dir = Path.Combine(dir, "pkg");
      WriteScriptedBinding(Path.Combine(lib_dir, "reg.bhl"), "included_e2e", "1.0.0");
      WriteFile(Path.Combine(lib_dir, "bhl.proj"),
        @"{ ""bindings"": [{ ""name"": ""included_e2e"", ""sources"": [""./reg.bhl""] }] }");

      var proj = MakeProj(dir);
      proj.includes.Add(Path.Combine(lib_dir, "bhl.proj"));
      proj.Setup();

      var bindings = (UserBindingsWithInfo)proj.LoadBindings();

      Assert.Single(bindings.info);
      Assert.Equal("included_e2e", bindings.info[0].name);
      Assert.Equal("1.0.0", bindings.info[0].version);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  static string JsonQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

using System;
using System.Collections.Generic;
using System.IO;
using bhl;
using Xunit;

// Coverage for BindingsEntryConf.include - expanding an entry into the contents of
// another JSON file, possibly matched via wildcard (see ProjectConf.ExpandBindingsIncludes).
public class TestBindingsInclude
{
  static string MakeTempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "bhl_bindings_include_" + Guid.NewGuid().ToString("N"));
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
func RegisterBindings(std.bind.Types types) {{
  types.RegisterVersion(""{decl_name}"", ""{version}"")
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
  public void IncludeExpandsSingleObjectEntry()
  {
    var dir = MakeTempDir();
    try
    {
      var included_bhl = WriteScriptedBinding(Path.Combine(dir, "pkg", "Bindings", "unity.bhl"), "unity", "1.0.0");
      WriteFile(Path.Combine(dir, "pkg", "bindings.json"),
        @"{ ""name"": ""unity"", ""sources"": [""./Bindings/unity.bhl""] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "pkg", "bindings.json") });
      proj.Setup();

      Assert.Single(proj.bindings);
      Assert.Equal("unity", proj.bindings[0].name);
      Assert.Equal(included_bhl.Replace('\\', '/'), proj.bindings[0].sources[0]);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeResolvesSourcesRelativeToIncludedFileNotProjFile()
  {
    var dir = MakeTempDir();
    try
    {
      //NOTE: bindings.json lives in a DIFFERENT dir than bhl.proj, to prove sources
      //      resolve against the included file, not proj_file
      var included_bhl = WriteScriptedBinding(Path.Combine(dir, "pkg", "sub", "x.bhl"), "x", "1.0.0");
      WriteFile(Path.Combine(dir, "pkg", "bindings.json"),
        @"{ ""name"": ""x"", ""sources"": [""./sub/x.bhl""] }");

      var proj = MakeProj(Path.Combine(dir, "consumer"));
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "pkg", "bindings.json") });
      proj.Setup();

      Assert.Equal(included_bhl.Replace('\\', '/'), proj.bindings[0].sources[0]);
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
      WriteFile(Path.Combine(dir, "pkg", "bindings.json"), @"{ ""name"": ""rel"", ""sources"": [] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = "./pkg/bindings.json" });
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
  public void IncludeExpandsArrayOfEntries()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "bindings.json"),
        @"[ { ""name"": ""a"", ""sources"": [] }, { ""name"": ""b"", ""sources"": [] } ]");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "bindings.json") });
      proj.Setup();

      Assert.Equal(2, proj.bindings.Count);
      Assert.Equal("a", proj.bindings[0].name);
      Assert.Equal("b", proj.bindings[1].name);
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
      WriteFile(Path.Combine(dir, "pkg@1.2.3", "bindings.json"),
        @"{ ""name"": ""wild"", ""sources"": [] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "pkg@*", "bindings.json") });
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
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "nope@*", "bindings.json") });

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
      WriteFile(Path.Combine(dir, "pkg@1.0.0", "bindings.json"), @"{ ""name"": ""dup"", ""sources"": [] }");
      WriteFile(Path.Combine(dir, "pkg@2.0.0", "bindings.json"), @"{ ""name"": ""dup"", ""sources"": [] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "pkg@*", "bindings.json") });

      Assert.Throws<Exception>(() => proj.Setup());
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void IncludeMixedWithOtherFieldsThrows()
  {
    var dir = MakeTempDir();
    try
    {
      WriteFile(Path.Combine(dir, "bindings.json"), @"{ ""name"": ""x"", ""sources"": [] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf
      {
        include = Path.Combine(dir, "bindings.json"),
        name = "should_not_be_here"
      });

      Assert.Throws<Exception>(() => proj.Setup());
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
      var a_path = Path.Combine(dir, "a.json");
      var b_path = Path.Combine(dir, "b.json");
      WriteFile(a_path, $@"{{ ""include"": {JsonQuote(b_path)} }}");
      WriteFile(b_path, $@"{{ ""include"": {JsonQuote(a_path)} }}");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = a_path });

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
      var inner_path = Path.Combine(dir, "inner.json");
      var outer_path = Path.Combine(dir, "outer.json");
      WriteFile(inner_path, @"{ ""name"": ""inner_binding"", ""sources"": [] }");
      WriteFile(outer_path, $@"{{ ""include"": {JsonQuote(inner_path)} }}");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = outer_path });
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
  public void IncludedEntryLoadsCorrectlyEndToEnd()
  {
    var dir = MakeTempDir();
    try
    {
      var included_bhl = WriteScriptedBinding(Path.Combine(dir, "pkg", "reg.bhl"), "included_e2e", "1.0.0");
      WriteFile(Path.Combine(dir, "pkg", "bindings.json"),
        @"{ ""name"": ""included_e2e"", ""sources"": [""./reg.bhl""] }");

      var proj = MakeProj(dir);
      proj.bindings.Add(new BindingsEntryConf { include = Path.Combine(dir, "pkg", "bindings.json") });
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using bhl;
using Xunit;

public class TestProjConf
{
  static string MakeProjFile(string dir)
  {
    string path = Path.Combine(dir, "bhl.proj");
    File.WriteAllText(path, "{}");
    return path;
  }

  [Fact]
  public void TmpDirDefaultsToStablePerProjectTempDir()
  {
    var dir = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_test_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
      var proj_file = MakeProjFile(dir);

      var proj_a = ProjectConf.ReadFromFile(proj_file);
      var proj_b = ProjectConf.ReadFromFile(proj_file);

      Assert.False(string.IsNullOrEmpty(proj_a.tmp_dir));
      Assert.Equal(proj_a.tmp_dir, proj_b.tmp_dir);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void TmpDirDiffersAcrossDifferentProjects()
  {
    var dir_a = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_test_a_" + Guid.NewGuid().ToString("N"));
    var dir_b = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_test_b_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir_a);
    Directory.CreateDirectory(dir_b);
    try
    {
      var proj_a = ProjectConf.ReadFromFile(MakeProjFile(dir_a));
      var proj_b = ProjectConf.ReadFromFile(MakeProjFile(dir_b));

      Assert.NotEqual(proj_a.tmp_dir, proj_b.tmp_dir);
    }
    finally
    {
      Directory.Delete(dir_a, true);
      Directory.Delete(dir_b, true);
    }
  }

  [Fact]
  public void ExplicitTmpDirIsNotOverridden()
  {
    var proj = new ProjectConf();
    proj.tmp_dir = "some/explicit/dir";
    proj.Setup();

    Assert.EndsWith("some/explicit/dir", proj.tmp_dir);
  }

  [Fact]
  public void ErrorFileDefaultsUnderTmpDir()
  {
    var proj = new ProjectConf();
    proj.Setup();

    Assert.StartsWith(proj.tmp_dir, proj.error_file);
    Assert.EndsWith("bhl.error", proj.error_file);
  }

  [Fact]
  public void ExplicitErrorFileIsNotOverridden()
  {
    var proj = new ProjectConf();
    proj.error_file = "some/explicit/errors.log";
    proj.Setup();

    Assert.EndsWith("some/explicit/errors.log", proj.error_file);
  }

  [Fact]
  public void MaxThreadsDefaultsToProcessorCount()
  {
    var proj = new ProjectConf();
    proj.Setup();

    Assert.Equal(Environment.ProcessorCount, proj.max_threads);
  }

  [Fact]
  public void ExplicitMaxThreadsIsNotOverridden()
  {
    var proj = new ProjectConf();
    proj.max_threads = 3;
    proj.Setup();

    Assert.Equal(3, proj.max_threads);
  }

  [Fact]
  public void SrcDirsExpandsWildcardToExistingDirs()
  {
    var root = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_test_srcdirs_" + Guid.NewGuid().ToString("N"));
    var pkg_dir = Path.Combine(root, "pkg@1.2.3", "Runtime", "Scripts");
    Directory.CreateDirectory(pkg_dir);
    try
    {
      var proj = new ProjectConf();
      proj.src_dirs.Add(Path.Combine(root, "pkg@*", "Runtime", "Scripts"));
      proj.Setup();

      Assert.Single(proj.src_dirs);
      Assert.Equal(BuildUtils.NormalizeFilePath(pkg_dir), proj.src_dirs[0]);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  //NOTE: assets interleaved with sources (e.g. tens of thousands of level files) otherwise get
  //      rescanned on every single build just to find the .bhl ones among them
  [Fact]
  public void SrcDirsExclamationPrefixPrunesTheWalk()
  {
    var root = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_excl_" + Guid.NewGuid().ToString("N"));
    var kept = Path.Combine(root, "kept");
    var skipped = Path.Combine(root, "skipped");
    var skipped_deep = Path.Combine(skipped, "deep");
    Directory.CreateDirectory(kept);
    Directory.CreateDirectory(skipped_deep);
    try
    {
      File.WriteAllText(Path.Combine(root, "top.bhl"), "func int A() { return 1 }");
      File.WriteAllText(Path.Combine(kept, "kept.bhl"), "func int B() { return 2 }");
      File.WriteAllText(Path.Combine(skipped, "skipped.bhl"), "func int C() { return 3 }");
      File.WriteAllText(Path.Combine(skipped_deep, "deep.bhl"), "func int D() { return 4 }");

      var proj = new ProjectConf();
      proj.src_dirs.Add(root);
      proj.src_dirs.Add("!" + skipped);
      proj.Setup();

      //NOTE: an exclusion is not a source dir itself
      Assert.Single(proj.src_dirs);
      Assert.Empty(proj.warnings);

      var files = new List<string>();
      proj.CollectSourceFiles(files);

      var names = files.Select(Path.GetFileName).OrderBy(x => x).ToList();
      Assert.Equal(new List<string>() { "kept.bhl", "top.bhl" }, names);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [Fact]
  public void SrcDirsExclusionMatchingNothingWarns()
  {
    var root = Path.Combine(Path.GetTempPath(), "bhl_proj_conf_excl_warn_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      var proj = new ProjectConf();
      proj.src_dirs.Add(root);
      proj.src_dirs.Add("!" + Path.Combine(root, "typo"));
      proj.Setup();

      Assert.Single(proj.src_dirs);
      Assert.Single(proj.warnings);
      Assert.Contains("matches nothing", proj.warnings[0]);
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  [Fact]
  public void SrcDirsWildcardWithNoMatchesResultsInEmptySrcDirs()
  {
    var proj = new ProjectConf();
    proj.src_dirs.Add(Path.Combine(Path.GetTempPath(),
      "bhl_proj_conf_test_nomatch_" + Guid.NewGuid().ToString("N"), "pkg@*", "Runtime"));
    proj.Setup();

    Assert.Empty(proj.src_dirs);
  }
}

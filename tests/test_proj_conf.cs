using System;
using System.IO;
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
}

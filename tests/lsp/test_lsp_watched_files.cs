using System;
using System.IO;
using bhl;
using bhl.lsp.handlers;
using Xunit;

// Coverage for DidChangeWatchedFilesHandler.TryGetReloadTarget - decides which bhl.proj
// changes are relevant to the tracked root (which can now include several via 'includes'),
// and always reloads from the root's own proj_file, never whichever file actually changed.
public class TestLspWatchedFiles : IDisposable
{
  string dir;

  public TestLspWatchedFiles()
  {
    dir = Path.Combine(Path.GetTempPath(), "bhlsp_watched_files_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
  }

  public void Dispose()
  {
    try { Directory.Delete(dir, true); } catch { }
  }

  string WriteFile(string path, string content)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, content);
    return path;
  }

  [Fact]
  public void ChangeToRootItselfIsRelevant()
  {
    var root_path = WriteFile(Path.Combine(dir, "bhl.proj"), "{}");
    var proj = ProjectConf.ReadFromFile(root_path);

    bool ok = DidChangeWatchedFilesHandler.TryGetReloadTarget(proj, root_path, out var reload_from);

    Assert.True(ok);
    Assert.Equal(BuildUtils.NormalizeFilePath(root_path), BuildUtils.NormalizeFilePath(reload_from));
  }

  [Fact]
  public void ChangeToIncludedLibraryIsRelevantAndReloadsFromRoot()
  {
    var lib_path = WriteFile(Path.Combine(dir, "pkg", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""lib"" }] }");
    var root_path = WriteFile(Path.Combine(dir, "root", "bhl.proj"),
      $@"{{ ""includes"": [{JsonQuote(lib_path)}] }}");
    var proj = ProjectConf.ReadFromFile(root_path);

    bool ok = DidChangeWatchedFilesHandler.TryGetReloadTarget(proj, lib_path, out var reload_from);

    Assert.True(ok);
    //NOTE: reloading from the changed (included) file's own dir would silently swap the
    //      tracked root project out for the library's own standalone config - must not happen
    Assert.Equal(BuildUtils.NormalizeFilePath(root_path), BuildUtils.NormalizeFilePath(reload_from));
  }

  [Fact]
  public void ChangeToUnrelatedProjectIsIgnored()
  {
    var root_path = WriteFile(Path.Combine(dir, "root", "bhl.proj"), "{}");
    var unrelated_path = WriteFile(Path.Combine(dir, "unrelated", "bhl.proj"), "{}");
    var proj = ProjectConf.ReadFromFile(root_path);

    bool ok = DidChangeWatchedFilesHandler.TryGetReloadTarget(proj, unrelated_path, out var reload_from);

    Assert.False(ok);
    Assert.Null(reload_from);
  }

  [Fact]
  public void ChangeToTransitivelyIncludedLibraryIsRelevant()
  {
    var inner_path = WriteFile(Path.Combine(dir, "inner", "bhl.proj"), @"{ ""bindings"": [{ ""name"": ""inner"" }] }");
    var outer_path = WriteFile(Path.Combine(dir, "outer", "bhl.proj"),
      $@"{{ ""includes"": [{JsonQuote(inner_path)}] }}");
    var root_path = WriteFile(Path.Combine(dir, "root", "bhl.proj"),
      $@"{{ ""includes"": [{JsonQuote(outer_path)}] }}");
    var proj = ProjectConf.ReadFromFile(root_path);

    bool ok = DidChangeWatchedFilesHandler.TryGetReloadTarget(proj, inner_path, out var reload_from);

    Assert.True(ok);
    Assert.Equal(BuildUtils.NormalizeFilePath(root_path), BuildUtils.NormalizeFilePath(reload_from));
  }

  static string JsonQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

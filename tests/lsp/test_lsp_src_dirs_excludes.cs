using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

// The LSP has its own src_dirs walk, so a '!' prefixed exclusion honoured by the compiler can
// still be ignored here - and then the editor and the build disagree about what's in the
// project. See docs: a '!' entry prunes the walk rather than naming a source dir.
public class TestLSPSrcDirsExcludes : TestLSPShared
{
  [Fact]
  public async Task ExcludedDirIsNotIndexed()
  {
    CleanTestFiles();

    string root = GetTestDirPath();
    Directory.CreateDirectory(Path.Combine(root, "kept"));
    Directory.CreateDirectory(Path.Combine(root, "skipped", "deep"));

    File.WriteAllText(Path.Combine(root, "top.bhl"), "func int A() { return 1 }");
    File.WriteAllText(Path.Combine(root, "kept", "kept.bhl"), "func int B() { return 2 }");
    File.WriteAllText(Path.Combine(root, "skipped", "skipped.bhl"), "func int C() { return 3 }");
    File.WriteAllText(Path.Combine(root, "skipped", "deep", "deep.bhl"), "func int D() { return 4 }");

    var conf = new ProjectConf();
    conf.src_dirs.Add(root);
    conf.src_dirs.Add("!" + Path.Combine(root, "skipped"));
    conf.Setup();

    var ws = new Workspace();
    ws.Init(new Types(), conf);
    await ws.IndexFilesAsync();

    var indexed = ws.Path2Proc.Keys.Select(Path.GetFileName).OrderBy(x => x).ToList();

    Assert.Equal(new List<string>() { "kept.bhl", "top.bhl" }, indexed);
  }
}

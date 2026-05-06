using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

// Tests for the 2-tier incremental recompilation optimisation:
//   Tier 1 — changed file + its direct importers → all semantic phases re-run
//   Tier 2 — everything else            → previous result kept, processing skipped
public class TestLSPIncremental : TestLSPShared, System.IDisposable
{
  TestLSPHost srv;
  Workspace ws;

  public TestLSPIncremental()
  {
    CleanTestFiles();
    ws = new Workspace();
    srv = NewTestServer(workspace: ws);
  }

  public void Dispose() => srv.Dispose();

  // After SendInit, Path2Proc is populated but Path2Doc is empty.
  // UpdateDocument requires a document in Path2Doc, so we seed it directly
  // without triggering a re-parse.
  void PreloadIntoPath2Doc(DocumentUri uri, string text)
  {
    var path = uri.PathNormalized();
    var doc = new BHLDocument(uri);
    if(ws.Path2Proc.TryGetValue(path, out var proc))
      doc.Update(text, proc);
    ws.Path2Doc[path] = doc;
  }

  ANTLR_Processor.Result GetResult(DocumentUri uri)
  {
    ws.Path2Proc.TryGetValue(uri.PathNormalized(), out var proc);
    return proc?.result;
  }

  void EditDocument(DocumentUri uri, string newText)
  {
    string path = uri.PathNormalized();
    File.WriteAllText(path, newText, Encoding.UTF8);
    ws.UpdateDocument(uri, newText);
  }

  // -----------------------------------------------------------------------

  [Fact]
  public async Task unrelated_file_result_is_preserved_after_change()
  {
    // A changes; C does NOT import A → C's result object must not be replaced.
    string srcA = "func void foo() {}";
    string srcB = "import \"a\"\nfunc void bar() { foo() }";
    string srcC = "func void baz() {}";

    var uriA = MakeTestDocument("a.bhl", srcA);
    var uriB = MakeTestDocument("b.bhl", srcB);
    var uriC = MakeTestDocument("c.bhl", srcC);

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);
    PreloadIntoPath2Doc(uriB, srcB);
    PreloadIntoPath2Doc(uriC, srcC);

    var resultC_before = GetResult(uriC);
    Assert.NotNull(resultC_before);

    EditDocument(uriA, "func void foo() {} func void foo2() {}");

    // Same object reference → C was not reprocessed.
    Assert.Same(resultC_before, GetResult(uriC));
  }

  [Fact]
  public async Task direct_importer_is_reprocessed_after_change()
  {
    // B imports A; editing A must produce a fresh result for B.
    string srcA = "func void foo() {}";
    string srcB = "import \"a\"\nfunc void bar() { foo() }";

    var uriA = MakeTestDocument("a.bhl", srcA);
    var uriB = MakeTestDocument("b.bhl", srcB);

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);
    PreloadIntoPath2Doc(uriB, srcB);

    var resultB_before = GetResult(uriB);
    Assert.NotNull(resultB_before);

    EditDocument(uriA, "func void foo() {} func void foo2() {}");

    // Different object reference → B was reprocessed.
    Assert.NotSame(resultB_before, GetResult(uriB));
  }

  [Fact]
  public async Task changed_file_itself_is_always_reprocessed()
  {
    string srcA = "func void foo() {}";
    var uriA = MakeTestDocument("a.bhl", srcA);
    MakeTestDocument("b.bhl", "import \"a\"\nfunc void bar() {}");

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);

    var resultA_before = GetResult(uriA);
    Assert.NotNull(resultA_before);

    EditDocument(uriA, "func void foo() {} func void extra() {}");

    Assert.NotSame(resultA_before, GetResult(uriA));
  }

  [Fact]
  public async Task errors_in_importer_are_detected_after_change()
  {
    // A exports foo(); B calls foo(). We rename foo() to bar() in A.
    // B should now report an error (unknown symbol foo).
    string srcA = "func void foo() {}";
    string srcB = "import \"a\"\nfunc void test() { foo() }";

    var uriA = MakeTestDocument("a.bhl", srcA);
    var uriB = MakeTestDocument("b.bhl", srcB);

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);
    PreloadIntoPath2Doc(uriB, srcB);

    Assert.Empty(GetResult(uriB).errors);

    EditDocument(uriA, "func void bar() {}");

    Assert.NotEmpty(GetResult(uriB).errors);
  }

  [Fact]
  public async Task unrelated_file_errors_survive_change()
  {
    // C has a pre-existing error; editing unrelated A must not disturb it.
    string srcA = "func void foo() {}";
    string srcC = "func void bad() { unknown_call() }";

    var uriA = MakeTestDocument("a.bhl", srcA);
    var uriC = MakeTestDocument("c.bhl", srcC);

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);
    PreloadIntoPath2Doc(uriC, srcC);

    Assert.NotEmpty(GetResult(uriC).errors);

    EditDocument(uriA, "func void foo() {} func void extra() {}");

    Assert.NotEmpty(GetResult(uriC).errors);
  }

  [Fact]
  public async Task multiple_importers_all_reprocessed()
  {
    // B and D both import A; C does not.
    // Editing A must reprocess B and D, and skip C.
    string srcA = "func void foo() {}";
    string srcB = "import \"a\"\nfunc void b() { foo() }";
    string srcC = "func void c() {}";
    string srcD = "import \"a\"\nfunc void d() { foo() }";

    var uriA = MakeTestDocument("a.bhl", srcA);
    var uriB = MakeTestDocument("b.bhl", srcB);
    var uriC = MakeTestDocument("c.bhl", srcC);
    var uriD = MakeTestDocument("d.bhl", srcD);

    await SendInit(srv);
    PreloadIntoPath2Doc(uriA, srcA);
    PreloadIntoPath2Doc(uriB, srcB);
    PreloadIntoPath2Doc(uriC, srcC);
    PreloadIntoPath2Doc(uriD, srcD);

    var resultC_before = GetResult(uriC);
    var resultB_before = GetResult(uriB);
    var resultD_before = GetResult(uriD);

    EditDocument(uriA, "func void foo() {} func void extra() {}");

    Assert.Same(resultC_before,    GetResult(uriC));
    Assert.NotSame(resultB_before, GetResult(uriB));
    Assert.NotSame(resultD_before, GetResult(uriD));
  }
}

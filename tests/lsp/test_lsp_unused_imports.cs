using System.Collections.Generic;
using System.Threading.Tasks;
using bhl;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPUnusedImports : TestLSPShared, System.IDisposable
{
  TestLSPHost srv;

  public TestLSPUnusedImports()
  {
    CleanTestFiles();
    srv = NewTestServer();
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  static int DeprecatedModifier => (int)ANTLR_Processor.SemanticModifier.Deprecated;

  static List<(int line, int col, int len, int type, int mods)> DecodeTokens(SemanticTokens result)
  {
    var tokens = new List<(int, int, int, int, int)>();
    var data = result.Data;
    int line = 0, col = 0;
    for(int i = 0; i + 4 < data.Length; i += 5)
    {
      int dl = (int)data[i];
      int dc = (int)data[i + 1];
      int len = (int)data[i + 2];
      int type = (int)data[i + 3];
      int mods = (int)data[i + 4];
      line += dl;
      col = dl != 0 ? dc : col + dc;
      tokens.Add((line, col, len, type, mods));
    }
    return tokens;
  }

  [Fact]
  public async Task TestUnusedImportMarkedDeprecated()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
import ""bhl1""
func void bar() {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await SendInit(srv);

    var result = await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri2 }
    );

    var tokens = DecodeTokens(result);
    // Line 1: import(kw,deprecated) "bhl1"(str,deprecated)
    Assert.NotEmpty(tokens);
    var import_kw = tokens.Find(t => t.line == 1 && t.col == 0);
    var import_str = tokens.Find(t => t.line == 1 && t.col == 7);
    Assert.True((import_kw.mods & DeprecatedModifier) != 0, "import keyword should be deprecated");
    Assert.True((import_str.mods & DeprecatedModifier) != 0, "import string should be deprecated");
  }

  [Fact]
  public async Task TestUsedImportNotDeprecated()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
import ""bhl1""
func void bar() { foo() }
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await SendInit(srv);

    var result = await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri2 }
    );

    var tokens = DecodeTokens(result);
    var import_kw = tokens.Find(t => t.line == 1 && t.col == 0);
    Assert.True((import_kw.mods & DeprecatedModifier) == 0, "import keyword should not be deprecated");
  }

  [Fact]
  public async Task TestPartiallyUsedImports()
  {
    string bhl1 = @"
func void foo() {}
";
    string bhl2 = @"
func void bar() {}
";
    string bhl3 = @"
import ""bhl1""
import ""bhl2""
func void test() { foo() }
";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl2.bhl", bhl2);
    var uri3 = MakeTestDocument("bhl3.bhl", bhl3);

    await SendInit(srv);

    var result = await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri3 }
    );

    var tokens = DecodeTokens(result);
    // Line 1: import "bhl1" — used, not deprecated
    var kw1 = tokens.Find(t => t.line == 1 && t.col == 0);
    Assert.True((kw1.mods & DeprecatedModifier) == 0, "bhl1 import should not be deprecated");
    // Line 2: import "bhl2" — unused, deprecated
    var kw2 = tokens.Find(t => t.line == 2 && t.col == 0);
    Assert.True((kw2.mods & DeprecatedModifier) != 0, "bhl2 import should be deprecated");
  }

  [Fact]
  public async Task TestUsedViaType()
  {
    string bhl1 = @"
class Vec3 { float x float y float z }
";
    string bhl2 = @"
import ""bhl1""
func void move(Vec3 pos) {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await SendInit(srv);

    var result = await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri2 }
    );

    var tokens = DecodeTokens(result);
    var import_kw = tokens.Find(t => t.line == 1 && t.col == 0);
    Assert.True((import_kw.mods & DeprecatedModifier) == 0, "import should not be deprecated when type is used");
  }
}

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

public class TestLSPSemanticTokens : TestLSPShared, IDisposable
{
  TestLSPHost srv;

  public TestLSPSemanticTokens()
  {
    srv = NewTestServer();

    CleanTestFiles();
  }

  public void Dispose()
  {
    srv.Dispose();
  }

  [Fact]
  public async Task TestSemanticTokensBasic()
  {
    string bhl = @"
    class BHL_M3Globals
    {
      []string match_chips_sfx
    }
    ";

    var uri1 = MakeTestDocument("bhl1.bhl", bhl);

    await SendInit(srv);

    var result =
      await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
        "textDocument/semanticTokens/full",
        new ()
        {
          TextDocument = uri1
        }
      );

    // Groups of 5: [deltaLine, deltaStartChar, length, tokenType, tokenModifiers]
    // class(kw) BHL_M3Globals(class) string(type) match_chips_sfx(var,definition) — sorted by position
    Assert.Equal(new int[] {1,4,5,6,0, 0,6,13,0,0, 2,8,6,5,0, 0,7,15,2,2}, result.Data);
  }
}

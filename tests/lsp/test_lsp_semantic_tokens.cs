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
  public async Task TestSemanticTokensImports()
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
func void test() {}
";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl2.bhl", bhl2);
    var uri3 = MakeTestDocument("bhl3.bhl", bhl3);

    await SendInit(srv);

    var result =
      await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
        "textDocument/semanticTokens/full",
        new ()
        {
          TextDocument = uri3
        }
      );

    // Print tokens for inspection:
    // import(kw) "bhl1"(str) import(kw) "bhl2"(str) func(kw) void(type) test(func,def)
    Console.WriteLine("Tokens: " + string.Join(",", result.Data));
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

  [Fact]
  public async Task TestSemanticTokensNamespaceAndClassQualifiers()
  {
    string bhl = @"
    namespace ns
    {
      class Foo
      {
        static func void Bar() { }
      }

      func void NsFunc() { }
    }

    func void test()
    {
      ns.NsFunc()
      ns.Foo.Bar()
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

    // Token type 10 is 'namespace' (see SemanticTokens.token_types), 0 is 'class'.
    // Decode groups of 5: [deltaLine, deltaStartChar, length, tokenType, tokenModifiers]
    var data = result.Data;
    var found_ns = false;
    var found_class = false;
    for(int i = 0; i + 4 < data.Length; i += 5)
    {
      int len = data[i + 2];
      int type = data[i + 3];
      if(len == 2 && type == 10) // "ns"
        found_ns = true;
      if(len == 3 && type == 0) // "Foo"
        found_class = true;
    }

    Assert.True(found_ns, "expected a 'namespace' token for the 'ns' qualifier");
    Assert.True(found_class, "expected a 'class' token for the 'Foo' qualifier");
  }

  [Fact]
  public async Task TestSemanticTokensEnumDeclAndQualifier()
  {
    string bhl = @"
    enum Color
    {
      Red = 1
      Green = 2
    }

    func void test()
    {
      Color c = Color.Red
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

    // Token type 11 is 'enum' (see SemanticTokens.token_types).
    // Decode groups of 5: [deltaLine, deltaStartChar, length, tokenType, tokenModifiers]
    var data = result.Data;
    var enum_token_count = 0;
    for(int i = 0; i + 4 < data.Length; i += 5)
    {
      int len = data[i + 2];
      int type = data[i + 3];
      if(len == 5 && type == 11) // "Color"
        enum_token_count++;
    }

    // Expect both the declaration ('enum Color') and the qualifier use ('Color.Red') to be tagged.
    Assert.Equal(2, enum_token_count);
  }
}

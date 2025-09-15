using System;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using bhl.lsp;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

public class TestLSPInitShutdownExit : TestLSPShared
{
  [Fact]
  public async Task _1()
  {
    using var srv = NewTestServer(new Workspace());
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    {
      var initParams = new InitializeParams
      {
        Capabilities = new ClientCapabilities(),
        ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
      };

      var result = await srv.SendRequestAsync<InitializeParams, InitializeResult>(
        "initialize",
        initParams,
        cts.Token
      );

      Assert.NotNull(result.Capabilities);
      Assert.Equal(TextDocumentSyncKind.Full, result.Capabilities.TextDocumentSync.Kind);
    }

    {
      var shutdownParams = new ShutdownParams() { };
      var result = await srv.SendRequestAsync<ShutdownParams>(
        "shutdown",
        shutdownParams,
        cts.Token
      );
      Assert.Equal(JTokenType.Null, result.Result.Type);
    }
  }
//
//  [Fact]
//  public async Task _3()
//  {
//    AssertEqual(
//      await srv.Handle(new Request(1, "shutdown").ToJson()),
//      NullResultJson(1)
//    );
//  }
//
//  [Fact]
//  public async Task _4()
//  {
//    Assert.False(srv.going_to_exit);
//    AssertEqual(
//      await srv.Handle(new Request(1, "exit").ToJson()),
//      string.Empty
//    );
//    Assert.True(srv.going_to_exit);
//  }
}

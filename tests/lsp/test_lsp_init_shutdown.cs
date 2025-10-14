using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using bhl.lsp;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Serilog;

public class TestLSPInitShutdownExit : TestLSPShared
{
  [Fact]
  public async Task InitShutdownExit()
  {
    using var srv_host = NewTestServer();

    {
      var result = await srv_host.SendRequestAsync<InitializeParams, InitializeResult>(
        "initialize",
        new ()
        {
          Capabilities = new (),
          ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
        }
      );

      Assert.NotNull(result.Capabilities);
      Assert.Equal(TextDocumentSyncKind.Full, result.Capabilities.TextDocumentSync.Kind);
    }

    {
      var result = await srv_host.SendRequestAsync<ShutdownParams>(
        "shutdown",
        new ()
      );
      Assert.Equal(JTokenType.Null, result.Result.Type);

      await (await srv_host.ServerTask).WasShutDown.WaitAsync(TimeSpan.FromSeconds(5));
    }

    {
      _ = srv_host.SendRequestAsync<ExitParams>(
        "exit",
        new ()
      );

      await (await srv_host.ServerTask).WaitForExit.WaitAsync(TimeSpan.FromSeconds(5));
    }
  }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using bhl.lsp.proto;

namespace bhl.lsp {

public class DiagnosticService : IService
{
  private Server srv;

  private CancellationTokenSource cts;

  private Dictionary<string, CompileErrors> uri2errs = new Dictionary<string, CompileErrors>();

  public DiagnosticService(Server srv)
  {
    this.srv = srv;

    this.cts = new CancellationTokenSource();

    srv.workspace.OnDiagnostics += PublishDiagnostics;
  }

  public void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc)
  {
    if (cc.textDocument?.publishDiagnostics != null)
    {
      sc.diagnosticProvider = new DiagnosticOptions
      {
        interFileDependencies = true,
        workspaceDiagnostics = true
      };
    }
  }

  async Task PublishDiagnostics(Dictionary<string, CompileErrors> new_uri2errs)
  {
    foreach (var kv in new_uri2errs)
    {
      if (!uri2errs.TryGetValue(kv.Key, out var errs) || errs.Count != kv.Value.Count)
      {
        var dparams = new PublishDiagnosticParams()
        {
          uri = new proto.Uri(kv.Key),
          diagnostics = new List<Diagnostic>()
        };

        foreach (var err in kv.Value)
        {
          var dg = new Diagnostic();
          dg.severity = DiagnosticSeverity.Error;
          dg.range = err.range;
          //NOTE: let's force the error to be 'one-line'
          if (dg.range.start.line != dg.range.end.line)
          {
            dg.range.end.line = dg.range.start.line + 1;
            dg.range.end.character = 0;
          }

          dg.message = err.text;

          if (dparams.diagnostics.Count > 0)
          {
            var prev_dg = dparams.diagnostics[dparams.diagnostics.Count - 1];
            //NOTE: let's skip diagnostics which starts on the same line
            //      just like the previous one
            if (prev_dg.range.start.line == dg.range.start.line)
              continue;
          }

          dparams.diagnostics.Add(dg);
        }

        var notification = new Notification()
        {
          method = "textDocument/publishDiagnostics",
          @params = dparams
        };

        await srv.Publish(notification, cts.Token);
      }
    }

    uri2errs = new_uri2errs;
  }
}

}

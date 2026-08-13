using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using bhl.lsp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Xunit;

public class TestLSPCodeAction : TestLSPShared, System.IDisposable
{
  TestLSPHost srv;
  Workspace ws;

  public TestLSPCodeAction()
  {
    CleanTestFiles();
    ws = new Workspace();
    srv = NewTestServer(workspace: ws);
  }

  public void Dispose() => srv.Dispose();

  async Task TriggerWorkspaceSetup(DocumentUri uri)
  {
    await SendInit(srv);
    await srv.SendRequestAsync<SemanticTokensParams, SemanticTokens>(
      "textDocument/semanticTokens/full",
      new() { TextDocument = uri }
    );
  }

  // Mimics what a real client does: scope Context.Diagnostics to whatever the server
  // most recently published for this file, and scope Only to the quick-fix lightbulb -
  // this is what isolates the per-diagnostic "Add missing imports" fix from the separate,
  // document-wide "Organize imports" source action tested further down.
  async Task<CommandOrCodeActionContainer> RequestCodeActions(DocumentUri uri)
  {
    ws.GetDiagnosticsToPublish().TryGetValue(uri.PathNormalized(), out var diags);

    return await srv.SendRequestAsync<CodeActionParams, CommandOrCodeActionContainer>(
      "textDocument/codeAction",
      new CodeActionParams
      {
        TextDocument = uri,
        Range = new Range(new Position(0, 0), new Position(0, 0)),
        Context = new CodeActionContext
        {
          Diagnostics = diags != null ? new Container<Diagnostic>(diags) : new Container<Diagnostic>(),
          Only = new Container<CodeActionKind>(CodeActionKind.QuickFix),
        },
      }
    );
  }

  async Task<CommandOrCodeActionContainer> RequestOrganizeImports(DocumentUri uri)
  {
    return await srv.SendRequestAsync<CodeActionParams, CommandOrCodeActionContainer>(
      "textDocument/codeAction",
      new CodeActionParams
      {
        TextDocument = uri,
        Range = new Range(new Position(0, 0), new Position(0, 0)),
        Context = new CodeActionContext
        {
          Diagnostics = new Container<Diagnostic>(),
          Only = new Container<CodeActionKind>(CodeActionKind.SourceOrganizeImports),
        },
      }
    );
  }

  [Fact]
  public async Task OffersAddMissingImportsAction()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "func void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var actions = await RequestCodeActions(uri2);

    Assert.NotNull(actions);
    Assert.Single(actions);

    var action = actions.First().CodeAction;
    Assert.NotNull(action);
    Assert.Equal("Add missing imports", action.Title);
    Assert.Equal(CodeActionKind.QuickFix, action.Kind);

    var edits = FlattenEdits(action.Edit);
    Assert.Single(edits);
    Assert.Equal("import \"bhl1\"\n", edits[0].newText);
  }

  [Fact]
  public async Task NoActionWhenNoErrors()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var actions = await RequestCodeActions(uri2);

    Assert.NotNull(actions);
    Assert.Empty(actions);
  }

  [Fact]
  public async Task NoActionWhenDiagnosticsNotPassed()
  {
    // Simulates a client asking for code actions at a location with no diagnostics attached
    // (e.g. just placing the cursor somewhere unrelated) - the fix must not be offered
    // unconditionally just because some import is missing somewhere in the file.
    string bhl1 = "func void foo() {}";
    string bhl2 = "func void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var actions = await srv.SendRequestAsync<CodeActionParams, CommandOrCodeActionContainer>(
      "textDocument/codeAction",
      new CodeActionParams
      {
        TextDocument = uri2,
        Range = new Range(new Position(0, 0), new Position(0, 0)),
        Context = new CodeActionContext
        {
          Diagnostics = new Container<Diagnostic>(),
          Only = new Container<CodeActionKind>(CodeActionKind.QuickFix),
        },
      }
    );

    Assert.NotNull(actions);
    Assert.Empty(actions);
  }

  [Fact]
  public async Task OrganizeImportsAddsMissingAndRemovesUnused()
  {
    // bhl1 gets imported but never used; bhl3 gets used but never imported.
    string bhl1 = "func void foo() {}";
    string bhl3 = "func void baz() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { baz() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    MakeTestDocument("bhl3.bhl", bhl3);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var actions = await RequestOrganizeImports(uri2);

    Assert.NotNull(actions);
    Assert.Single(actions);

    var action = actions.First().CodeAction;
    Assert.NotNull(action);
    Assert.Equal("Organize imports", action.Title);
    Assert.Equal(CodeActionKind.SourceOrganizeImports, action.Kind);

    var edits = FlattenEdits(action.Edit);
    Assert.Equal(2, edits.Count);
    Assert.Contains(edits, e => e.newText == "import \"bhl3\"\n");
    Assert.Contains(edits, e => e.newText == "");
  }

  [Fact]
  public async Task NoOrganizeImportsActionWhenNothingToDo()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    var actions = await RequestOrganizeImports(uri2);

    Assert.NotNull(actions);
    Assert.Empty(actions);
  }

  [Fact]
  public async Task UnrestrictedRequestOffersBothQuickFixAndOrganizeImports()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "func void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    ws.GetDiagnosticsToPublish().TryGetValue(uri2.PathNormalized(), out var diags);

    var actions = await srv.SendRequestAsync<CodeActionParams, CommandOrCodeActionContainer>(
      "textDocument/codeAction",
      new CodeActionParams
      {
        TextDocument = uri2,
        Range = new Range(new Position(0, 0), new Position(0, 0)),
        Context = new CodeActionContext
        {
          Diagnostics = diags != null ? new Container<Diagnostic>(diags) : new Container<Diagnostic>(),
        },
      }
    );

    Assert.NotNull(actions);
    var titles = actions.Select(a => a.CodeAction?.Title).ToList();
    Assert.Contains("Add missing imports", titles);
    Assert.Contains("Organize imports", titles);
    Assert.Equal(2, actions.Count());
  }

  [Fact]
  public async Task DoesNotAutoApplyOnSave()
  {
    string bhl1 = "func void foo() {}";
    string bhl2 = "import \"bhl1\"\nfunc void bar() { foo() }";

    MakeTestDocument("bhl1.bhl", bhl1);
    var uri2 = MakeTestDocument("bhl2.bhl", bhl2);

    await TriggerWorkspaceSetup(uri2);

    // Comment out the import (mirrors editing then saving in a real editor) and confirm
    // that saving no longer triggers a server-initiated workspace/applyEdit re-adding it.
    string commented = "//import \"bhl1\"\nfunc void bar() { foo() }";
    ws.UpdateDocument(uri2, commented);

    await SendDidSave(srv, uri2);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
    bool gotApplyEdit = false;
    try
    {
      await foreach(var msg in srv.RecvMsgsAsync(cts.Token))
      {
        if(msg.Method == "workspace/applyEdit")
        {
          gotApplyEdit = true;
          break;
        }
      }
    }
    catch(OperationCanceledException)
    {
      // expected: nothing arrived within the window
    }

    Assert.False(gotApplyEdit);
  }
}

#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace bhl.lsp.handlers;

public class SemanticTokensHandler : SemanticTokensHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public SemanticTokensHandler(ILogger<SemanticTokensHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
    SemanticTokensCapability capability, ClientCapabilities clientCapabilities
  )
  {
    return MakeRegistrationOptions();
  }

  static SemanticTokensRegistrationOptions MakeRegistrationOptions()
  {
    //NOTE: synchronized with ANTLR_Processor.token_types
    IEnumerable<SemanticTokenType> tokenTypes = new []
    {
      SemanticTokenType.Class,
      SemanticTokenType.Function,
      SemanticTokenType.Variable,
      SemanticTokenType.Number,
      SemanticTokenType.String,
      SemanticTokenType.Type,
      SemanticTokenType.Keyword,
      SemanticTokenType.Property,
      SemanticTokenType.Operator,
      SemanticTokenType.Parameter
    };

    IEnumerable<SemanticTokenModifier> tokenModifiers = new []
    {
      SemanticTokenModifier.Declaration,
      SemanticTokenModifier.Definition,
      SemanticTokenModifier.Readonly,
      SemanticTokenModifier.Static
    };

    return new ()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
      Legend = new SemanticTokensLegend
      {
        TokenTypes = new (tokenTypes),
        TokenModifiers = new (tokenModifiers)
      },
      Full = new SemanticTokensCapabilityRequestFull
      {
        Delta = true
      },
      Range = true
    };
  }

  public override async Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken ct)
  {
    return await base.Handle(request, ct);
  }

  public override async Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken ct)
  {
    return await base.Handle(request, ct);
  }

  public override async Task<SemanticTokensFullOrDelta?> Handle(SemanticTokensDeltaParams request, CancellationToken ct)
  {
    return await base.Handle(request, ct);
  }

  protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken ct)
  {
    await _workspace.SetupProjectIfEmptyAsync(identifier.TextDocument.Uri.Path, ct);

    var document = _workspace.GetOrLoadDocument(identifier.TextDocument.Uri);

    foreach (var token in document.Processed.semantic_tokens)
    {
      int line = token.line - 1;
      int column = token.column;
      builder.Push(line, column, token.len, (int)token.type_idx, (int)token.mods);
    }
  }

  protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
  {
    //TODO: investigate why RegistrationOptions are null
    //return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    return Task.FromResult(new SemanticTokensDocument(MakeRegistrationOptions().Legend));
  }
}

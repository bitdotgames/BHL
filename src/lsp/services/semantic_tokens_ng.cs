using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ILogger = Serilog.ILogger;

namespace bhl.lsp {
public class SemanticTokensHandler : SemanticTokensHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public SemanticTokensHandler(ILogger logger, Workspace workspace)
  {
      _logger = logger;
      _workspace = workspace;
  }

  public override async Task<SemanticTokens?> Handle(
      SemanticTokensParams request, CancellationToken cancellationToken
  )
  {
      var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
      return result;
  }

  public override async Task<SemanticTokens?> Handle(
      SemanticTokensRangeParams request, CancellationToken cancellationToken
  )
  {
      var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
      return result;
  }

  public override async Task<SemanticTokensFullOrDelta?> Handle(
      SemanticTokensDeltaParams request,
      CancellationToken cancellationToken
  )
  {
      var result = await base.Handle(request, cancellationToken).ConfigureAwait(false);
      return result;
  }

  protected override async Task Tokenize(
      SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
      CancellationToken cancellationToken
  )
  {
      var document = _workspace.GetOrLoadDocument(new proto.Uri(identifier.TextDocument.Uri.Path));
  //    using var typesEnumerator = RotateEnum(SemanticTokenType.Defaults).GetEnumerator();
  //    using var modifiersEnumerator = RotateEnum(SemanticTokenModifier.Defaults).GetEnumerator();
  //    // you would normally get this from a common source that is managed by current open editor, current active editor, etc.
  //    var content = await File.ReadAllTextAsync(DocumentUri.GetFileSystemPath(identifier), cancellationToken).ConfigureAwait(false);
      await Task.Yield();
      
      ANTLR_Processor.SemanticTokenNode prev = null;
      foreach (var token in document.proc.semantic_tokens)
      {
          int diff_line = token.line - (prev?.line??1);
          int diff_column = diff_line != 0 ? token.column : token.column - prev?.column??0;
          builder.Push(diff_line, diff_column, token.len, (int)token.type_idx, (int)token.mods);
          prev = token;
      }

      //    foreach (var (line, text) in content.Split('\n').Select((text, line) => ( line, text )))
  //    {
  //        var parts = text.TrimEnd().Split(';', ' ', '.', '"', '(', ')');
  //        var index = 0;
  //        foreach (var part in parts)
  //        {
  //            typesEnumerator.MoveNext();
  //            modifiersEnumerator.MoveNext();
  //            if (string.IsNullOrWhiteSpace(part)) continue;
  //            index = text.IndexOf(part, index, StringComparison.Ordinal);
  //            builder.Push(line, index, part.Length, typesEnumerator.Current, modifiersEnumerator.Current);
  //        }
  //    }
  }

  protected override Task<SemanticTokensDocument>
      GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
  { 
      return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
  }

  protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
      SemanticTokensCapability capability, ClientCapabilities clientCapabilities
  )
  {
      return new SemanticTokensRegistrationOptions
      {
          DocumentSelector = TextDocumentSelector.ForLanguage("bhl"),
          Legend = new SemanticTokensLegend
          {
              TokenModifiers = capability.TokenModifiers,
              TokenTypes = capability.TokenTypes
          },
          Full = new SemanticTokensCapabilityRequestFull
          {
              Delta = true
          },
          Range = true
      };
  }
}

}
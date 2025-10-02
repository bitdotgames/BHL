using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;


namespace bhl.lsp.handlers;

public class TextDocumentReferencesHandler : ReferencesHandlerBase
{
  private readonly ILogger _logger;
  private readonly Workspace _workspace;

  public TextDocumentReferencesHandler(ILogger<TextDocumentReferencesHandler> logger, Workspace workspace)
  {
    _logger = logger;
    _workspace = workspace;
  }

  //TODO: does it provide necessary capabilities?
  protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability,
    ClientCapabilities clientCapabilities)
  {
    return new()
    {
      DocumentSelector = TextDocumentSelector.ForLanguage("bhl")
    };
  }

  public override async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
  {
    await _workspace.SetupIfEmpty(request.TextDocument.Uri.Path);

    var document = _workspace.GetOrLoadDocument(request.TextDocument.Uri);

    var refs = new List<Location>();

    if(document != null)
    {
      var symb = document.FindSymbol(request.Position.FromLsp2Antlr());
      if(symb != null)
      {
        //1. adding all found references
        foreach(var kv in _workspace.Path2Proc)
        {
          foreach(var anKv in kv.Value.annotated_nodes)
          {
            if(anKv.Value.lsp_symbol == symb)
            {
              var loc = new Location
              {
                Uri = DocumentUri.Parse(kv.Key),
                Range = anKv.Value.range.FromAntlr2Lsp().ToRange()
              };
              refs.Add(loc);
            }
          }
        }

        //2. sorting by file name
        refs.Sort((a, b) =>
          {
            if(a.Uri.Path == b.Uri.Path)
              return a.Range.Start.Line.CompareTo(b.Range.Start.Line);
            else
              return a.Uri.Path.CompareTo(b.Uri.Path);
          }
        );

        //3. adding definition for native symbol (if any)
        if(symb is FuncSymbolNative)
        {
          refs.Add(new Location
          {
            Uri = DocumentUri.Parse(symb.origin.source_file),
            Range = symb.origin.source_range.FromAntlr2Lsp().ToRange()
          });
        }
      }
    }

    return new LocationContainer(refs);
  }
}

using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace bhl.lsp;

public static class Extensions
{
  public static T FromJson<T>(this string json) where T : class
  {
    return JsonConvert.DeserializeObject<T>(json);
  }

  public static string ToJson(this object obj)
  {
    var jsettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
    return JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.None, jsettings);
  }

  public static Range ToRange(this SourceRange range)
  {
    return new Range(range.start.ToPosition(), range.end.ToPosition());
  }

  public static Position ToPosition(this SourcePos pos)
  {
    //NOTE: in LSP position is 0 based
    return new Position(pos.line - 1, pos.column);
  }

  public static SourcePos FromAntlr2Lsp(this SourcePos pos)
  {
    return new SourcePos(pos.line - 1, pos.column);
  }

  public static SourcePos FromLsp2Antlr(this SourcePos pos)
  {
    return new SourcePos(pos.line + 1, pos.column);
  }

  public static SourceRange FromAntlr2Lsp(this SourceRange range)
  {
    return new SourceRange(range.start.FromAntlr2Lsp(), range.end.FromAntlr2Lsp());
  }

  public static SourceRange FromLsp2Antlr(this SourceRange range)
  {
    return new SourceRange(range.start.FromLsp2Antlr(), range.end.FromLsp2Antlr());
  }

  public static SourcePos FromLsp2Antlr(this Position pos)
  {
    return new SourcePos(pos.Line + 1, pos.Character);
  }

  public static void SetupForRootPath(this ProjectConf proj, string rootPath)
  {
    proj.proj_file = "default.proj";
    proj.src_dirs.Add(rootPath);
    proj.inc_dirs.Add(rootPath);
    proj.Setup();
  }

  public static async Task SetupIfEmptyAsync(this Workspace workspace, string filePath)
  {
    if (string.IsNullOrEmpty(workspace.ProjConf.proj_file))
    {
      workspace.ProjConf.SetupForRootPath(Path.GetDirectoryName(filePath));
      await workspace.IndexFilesAsync();
    }
  }

  public static Dictionary<string, List<Diagnostic>> GetDiagnostics(this Dictionary<string, CompileErrors> errors)
  {
    var result = new Dictionary<string, List<Diagnostic>>();

    foreach(var kv in errors)
    {
      List<Diagnostic> diagnostics = new();
      foreach (var err in kv.Value)
      {
        var range = err.range.ToRange();

        //NOTE: let's force the error to be 'one-line'
        if (range.Start.Line != range.End.Line)
        {
          range.End.Line = range.Start.Line + 1;
          range.End.Character = 0;
        }

        var current = new Diagnostic()
        {
          Severity = DiagnosticSeverity.Error,
          Range = range,
          Message = err.text
        };

        if (diagnostics.Count > 0)
        {
          var prev = diagnostics[^1];
          //NOTE: let's skip diagnostics which starts on the same line
          //      just like the previous one
          if (prev.Range.Start.Line == current.Range.Start.Line)
            continue;
        }

        diagnostics.Add(current);
      }

      result.Add(kv.Key, diagnostics);
    }
    return result;
  }
}

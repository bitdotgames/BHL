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
    return new Position(pos.line + 1, pos.column);
  }

  public static void SetupForRootPath(this ProjectConf proj, string rootPath)
  {
    proj.proj_file = "default.proj";
    proj.src_dirs.Add(rootPath);
    proj.inc_dirs.Add(rootPath);
    proj.Setup();
  }

  public static async Task SetupIfEmpty(this Workspace workspace, string filePath)
  {
    if (string.IsNullOrEmpty(workspace.ProjConf.proj_file))
    {
      workspace.ProjConf.SetupForRootPath(Path.GetDirectoryName(filePath));
      await workspace.IndexFiles();
    }
  }
}

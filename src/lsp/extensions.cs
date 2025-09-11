using Newtonsoft.Json;

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
}
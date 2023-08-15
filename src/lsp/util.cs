using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Newtonsoft.Json;

namespace bhl.lsp {

public class CodeIndex
{
  int length;
  List<int> line2byte_offset = new List<int>();

  public void Update(string text)
  {
    length = text.Length;

    line2byte_offset.Clear();
    
    if(length > 0)
      line2byte_offset.Add(0);

    for(int i = 0; i < text.Length; i++)
    {
      if(text[i] == '\r')
      {
        if(i + 1 < text.Length && text[i + 1] == '\n')
          i++;
        line2byte_offset.Add(i + 1);
      }
      else if(text[i] == '\n')
        line2byte_offset.Add(i + 1);
    }
  }

  public int CalcByteIndex(int line, int column)
  {
    if(line2byte_offset.Count > 0 && line < line2byte_offset.Count)
      return line2byte_offset[line] + column;

    return -1;
  }
  
  public int CalcByteIndex(int line)
  {
    if(line2byte_offset.Count > 0 && line < line2byte_offset.Count)
      return line2byte_offset[line];

    return -1;
  }
  
  public SourcePos GetIndexPosition(int index)
  {
    if(index >= length)
      return new SourcePos(-1, -1); 

    // Binary search.
    int low = 0;
    int high = line2byte_offset.Count - 1;
    int i = 0;
    
    while(low <= high)
    {
      i = (low + high) / 2;
      var v = line2byte_offset[i];
      if(v < index) 
        low = i + 1;
      else if(v > index) 
        high = i - 1;
      else 
        break;
    }
    
    var min = low <= high ? i : high;
    return new SourcePos(min, index - line2byte_offset[min]);
  }
}

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

}

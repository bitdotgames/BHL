#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace bhl
{

[InitializeOnLoad]
static class BHLFrontDetector
{
  const string DEFINE = "BHL_FRONT";

  static BHLFrontDetector()
  {
    bool antlrPresent = Type.GetType("Antlr4.Runtime.Lexer, Antlr4.Runtime.Standard") != null;
    bool lz4Present   = Type.GetType("LZ4ps.LZ4Codec, LZ4") != null;
    bool shouldEnable = antlrPresent && lz4Present;

    foreach(BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
    {
      if(group == BuildTargetGroup.Unknown || IsObsolete(group))
        continue;

      try
      {
        var raw  = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var list = new List<string>(
          raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

        bool changed = false;

        if(shouldEnable && !list.Contains(DEFINE))
        {
          list.Add(DEFINE);
          changed = true;
        }
        else if(!shouldEnable && list.Contains(DEFINE))
        {
          list.Remove(DEFINE);
          changed = true;
        }

        if(changed)
          PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
      }
      catch(Exception)
      {
        // some BuildTargetGroups are not supported on this Unity installation
      }
    }
  }

  static bool IsObsolete(BuildTargetGroup group)
  {
    var field = typeof(BuildTargetGroup).GetField(group.ToString());
    return field != null && field.IsDefined(typeof(ObsoleteAttribute), false);
  }
}

}
#endif

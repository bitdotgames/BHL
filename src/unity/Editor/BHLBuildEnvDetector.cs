#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace bhl
{

[InitializeOnLoad]
static class BHLBuildEnvDetector
{
  static BHLBuildEnvDetector()
  {
    // Build a group → representative BuildTarget map so we can query PluginImporter
    // per-platform without a manual switch statement.
    var groupToTarget = new Dictionary<BuildTargetGroup, BuildTarget>();
    foreach(BuildTarget t in Enum.GetValues(typeof(BuildTarget)))
    {
      if(t == BuildTarget.NoTarget) continue;
      var g = BuildPipeline.GetBuildTargetGroup(t);
      if(g != BuildTargetGroup.Unknown && !groupToTarget.ContainsKey(g))
        groupToTarget[g] = t;
    }

    var antlrImporter = FindPluginImporter("Antlr4.Runtime.Standard.dll");
    var lz4Importer   = FindPluginImporter("LZ4.dll");

    foreach(BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
    {
      if(group == BuildTargetGroup.Unknown || IsObsolete(group))
        continue;

      try
      {
        bool antlrForGroup = false;
        bool lz4ForGroup   = false;

        if(groupToTarget.TryGetValue(group, out var target))
        {
          antlrForGroup = antlrImporter != null && IsCompatibleWithPlatform(antlrImporter, target);
          lz4ForGroup   = lz4Importer   != null && IsCompatibleWithPlatform(lz4Importer,   target);
        }

        var raw  = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var list = new List<string>(
          raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

        list.Remove("BHL_PARSER");
        list.Remove("BHL_LZ4");

        if(antlrForGroup) list.Add("BHL_PARSER");
        if(lz4ForGroup)   list.Add("BHL_LZ4");

        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
      }
      catch(Exception)
      {
        // some BuildTargetGroups are not supported on this Unity installation
      }
    }
  }

  // Returns true if the plugin will be compiled in for the given build target.
  // Handles the "Any Platform" + per-platform exclusion model.
  static bool IsCompatibleWithPlatform(PluginImporter importer, BuildTarget target)
  {
    if(importer.GetCompatibleWithAnyPlatform())
    {
      try { return !importer.GetExcludeFromAnyPlatform(target.ToString()); }
      catch { return true; }
    }
    return importer.GetCompatibleWithPlatform(target);
  }

  // Finds a plugin importer by DLL filename (case-insensitive).
  static PluginImporter FindPluginImporter(string dllFileName)
  {
    foreach(var imp in PluginImporter.GetAllImporters())
      if(Path.GetFileName(imp.assetPath).Equals(dllFileName, StringComparison.OrdinalIgnoreCase))
        return imp;
    return null;
  }

  static bool IsObsolete(BuildTargetGroup group)
  {
    var field = typeof(BuildTargetGroup).GetField(group.ToString());
    return field != null && field.IsDefined(typeof(ObsoleteAttribute), false);
  }
}

}
#endif

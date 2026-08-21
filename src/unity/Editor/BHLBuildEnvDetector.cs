#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace bhl
{

[InitializeOnLoad]
public static class BHLBuildEnvDetector
{
  static BHLBuildEnvDetector()
  {
    Rescan();
  }

  //NOTE: public so a project's own UI (e.g. a control panel) can force a rescan on
  //      demand, rather than waiting for the next unrelated domain reload to pick up
  //      a plugin that was just added/reimported
  public static void Rescan()
  {
    var antlrImporter = FindPluginImporter("Antlr4.Runtime.Standard.dll");
    var lz4Importer   = FindPluginImporter("LZ4.dll");

    //NOTE: both plugins live under Plugins/Editor/, which Unity excludes from every
    //      Player build regardless of per-platform checkboxes - so what actually matters
    //      is GetCompatibleWithEditor(), not Player-target compatibility. BHL_PARSER/
    //      BHL_LZ4 are consumed by Editor-only compiler code either way.
    bool antlrForEditor = antlrImporter != null && antlrImporter.GetCompatibleWithEditor();
    bool lz4ForEditor   = lz4Importer   != null && lz4Importer.GetCompatibleWithEditor();

    Debug.Log("[BHL] BuildEnvDetector: Antlr4.Runtime.Standard.dll " +
      (antlrImporter != null ? $"found at {antlrImporter.assetPath}, CompatibleWithEditor={antlrForEditor}" : "NOT FOUND"));
    Debug.Log("[BHL] BuildEnvDetector: LZ4.dll " +
      (lz4Importer != null ? $"found at {lz4Importer.assetPath}, CompatibleWithEditor={lz4ForEditor}" : "NOT FOUND"));

    var activeGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
    Debug.Log($"[BHL] BuildEnvDetector: active build target = {EditorUserBuildSettings.activeBuildTarget} (group={activeGroup})");

    foreach(BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
    {
      if(group == BuildTargetGroup.Unknown || IsObsolete(group))
        continue;

      try
      {
        var raw  = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var list = new List<string>(
          raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

        list.Remove("BHL_PARSER");
        list.Remove("BHL_LZ4");

        if(antlrForEditor) list.Add("BHL_PARSER");
        if(lz4ForEditor)   list.Add("BHL_LZ4");

        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));

        if(group == activeGroup)
          Debug.Log($"[BHL] BuildEnvDetector: ACTIVE group={group} BHL_PARSER={antlrForEditor} BHL_LZ4={lz4ForEditor} -> defines=[{string.Join(";", list)}]");
      }
      catch(Exception e)
      {
        Debug.Log($"[BHL] BuildEnvDetector: group={group} skipped ({e.Message})");
      }
    }
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

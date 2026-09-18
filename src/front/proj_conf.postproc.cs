#if (BHL_PARSER || UNITY_EDITOR)

using System;
using System.Collections.Generic;

namespace bhl
{

//NOTE: postproc needs the compiler frontend (IFrontPostProcessor/DllPostProcessor live
//      in postproc.cs, itself compiler/LSP-only) - see src/vm/proj_conf.cs for the
//      universally-available core of ProjectConf this partial extends
public partial class ProjectConf
{
  //NOTE: list of .cs sources which are built into posproc_dll
  public List<string> postproc_sources = new List<string>();

  //NOTE: this can be a directory path as well containing an actual dll
  //      (posproc.dll/postproc.dll)
  public string postproc_dll = "";

  //NOTE: same as BindingsEntryConf.manual_build, but for postproc_dll/postproc_sources
  public bool postproc_manual_build = false;

  //NOTE: postproc_dll is loaded via reflection by whatever host runs the compiler
  //      (e.g. Unity Editor's Mono) - net8.0 (this repo's own default TargetFramework)
  //      pins typerefs to a System.Runtime version such hosts can't resolve, hence a
  //      netstandard default here instead. 2.1 rather than 2.0, since Unity's own
  //      default API compatibility level is netstandard2.1 (since 2021.2) and it adds a
  //      few conveniences (e.g. Enumerable.ToHashSet) 2.0 lacks
  public string postproc_target_framework = "netstandard2.1";

  partial void SetupPostproc()
  {
    for(int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = NormalizePath(proj_file, postproc_sources[i]);
    postproc_dll = NormalizePath(proj_file, postproc_dll);
  }

  partial void CheckNoPostproc(string included_file)
  {
    if(postproc_sources.Count > 0 || !string.IsNullOrEmpty(postproc_dll) || postproc_manual_build)
      throw new Exception(
        $"Included bhl.proj '{included_file}' must not use 'postproc_sources'/'postproc_dll'/" +
        "'postproc_manual_build' - postproc only applies to the including project"
      );
  }

  public IFrontPostProcessor LoadPostprocessor()
  {
#if UNITY_EDITOR
    //NOTE: postproc_dll itself is never loaded here - AppDomainPostProcessor instead
    //      looks for an already-loaded assembly named after it (see PostprocBridge)
    if(string.IsNullOrEmpty(postproc_dll))
      return new EmptyPostProcessor();

    return new AppDomainPostProcessor(System.IO.Path.GetFileNameWithoutExtension(postproc_dll));
#else
    if(!string.IsNullOrEmpty(postproc_dll))
      return new DllPostProcessor(postproc_dll);

    return new EmptyPostProcessor();
#endif
  }
}
}

#endif

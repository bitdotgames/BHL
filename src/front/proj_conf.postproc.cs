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
    if(!string.IsNullOrEmpty(postproc_dll))
      return new DllPostProcessor(postproc_dll);

    return new EmptyPostProcessor();
  }
}
}

#endif

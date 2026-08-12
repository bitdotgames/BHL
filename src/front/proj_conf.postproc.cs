#if (BHL_PARSER || UNITY_EDITOR)

namespace bhl
{

//NOTE: postproc needs the compiler frontend (IFrontPostProcessor/DllPostProcessor live
//      in postproc.cs, itself compiler/LSP-only) - see src/vm/proj_conf.cs for the
//      universally-available core of ProjectConf this partial extends
public partial class ProjectConf
{
  //NOTE: list of .cs sources which are built into posproc_dll
  public System.Collections.Generic.List<string> postproc_sources = new System.Collections.Generic.List<string>();

  //NOTE: this can be a directory path as well containing an actual dll
  //      (posproc.dll/postproc.dll)
  public string postproc_dll = "";

  //NOTE: same as BindingsModuleConf.manual_build, but for postproc_dll/postproc_sources
  public bool postproc_manual_build = false;

  partial void SetupPostproc()
  {
    for(int i = 0; i < postproc_sources.Count; ++i)
      postproc_sources[i] = NormalizePath(proj_file, postproc_sources[i]);
    postproc_dll = NormalizePath(proj_file, postproc_dll);
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

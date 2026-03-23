using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace bhl;

public interface IFrontPostProcessor
{
  //NOTE: returns patched result
  ANTLR_Processor.Result Patch(ANTLR_Processor.Result result, string src_file);
  void Tally();
}

public class DllPostProcessor : IFrontPostProcessor
{
  string dll_path;
  IFrontPostProcessor user_postproc;

  public DllPostProcessor(string dll_path)
  {
    this.dll_path = dll_path;
    user_postproc = LoadUserPostproc();
  }

  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result result, string src_file)
  {
    return user_postproc.Patch(result, src_file);
  }

  public void Tally()
  {
    user_postproc.Tally();
  }

  IFrontPostProcessor LoadUserPostproc()
  {
    var postproc_assembly = LoadAssemblyFromDirOrFile(dll_path);
    var types = postproc_assembly.GetTypes();

    var postproc_classes = new List<Type>();
    foreach(var type in types)
    {
      if(typeof(IFrontPostProcessor).IsAssignableFrom(type))
        postproc_classes.Add(type);
    }

    if(postproc_classes.Count == 0)
      throw new Exception($"IFrontPostProcessor instance not found in '{dll_path}'");

    if(postproc_classes.Count == 1)
      return InstantiatePostProc(postproc_classes[0]);

    var postproc_instances = postproc_classes.Select(InstantiatePostProc).ToList();
    return new CombinedPostProcessor(postproc_instances);
  }

  //NOTE: .Net build target can be a directory which actually contains the target dll, e.g. postproc.dll/postproc.dll,
  //      this function takes this into consideration
  static System.Reflection.Assembly LoadAssemblyFromDirOrFile(string path)
  {
    return System.Reflection.Assembly.LoadFrom(
      Directory.Exists(path) ? path + "/" + Path.GetFileName(path) : path
    );
  }

  static IFrontPostProcessor InstantiatePostProc(System.Type type)
  {
    return Activator.CreateInstance(type) as IFrontPostProcessor;
  }
}

public class EmptyPostProcessor : IFrontPostProcessor
{
  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result result, string src_file)
  {
    return result;
  }

  public void Tally()
  {
  }
}

public class CombinedPostProcessor : IFrontPostProcessor
{
  //List instead of Enumerable to preserve the specified order
  readonly IList<IFrontPostProcessor> _postprocessors;

  public CombinedPostProcessor(IList<IFrontPostProcessor> postprocessors)
  {
    _postprocessors = postprocessors;
  }

  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result result, string src_file)
  {
    for(int i = 0; i < _postprocessors.Count; i++)
      result = _postprocessors[i].Patch(result, src_file);

    return result;
  }

  public void Tally()
  {
    for(int i = 0; i < _postprocessors.Count; i++)
      _postprocessors[i].Tally();
  }
}

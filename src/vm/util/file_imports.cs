using System.Collections.Generic;

namespace bhl {
  
public class FileImports : marshall.IMarshallable
{
  public List<string> import_paths = new List<string>();
  public List<string> file_paths = new List<string>();

  public FileImports()
  {}

  public FileImports(Dictionary<string, string> imps)
  {
    foreach(var kv in imps)
    {
      import_paths.Add(kv.Key);
      file_paths.Add(kv.Value);
    }
  }

  //TODO: handle duplicate file_paths?
  //NOTE: file_path can be null if imported module is native for example
  public void Add(string import_path, string file_path)
  {
    if(import_paths.Contains(import_path))
      return;
    import_paths.Add(import_path);
    file_paths.Add(file_path);
  }

  public string MapToFilePath(string import_path)
  {
    int idx = import_paths.IndexOf(import_path);
    if(idx == -1)
      return null;
    return file_paths[idx];
  }

  public void Reset() 
  {
    import_paths.Clear();
    file_paths.Clear();
  }
  
  public void IndexTypeRefs(TypeRefIndex refs)
  {}

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.Sync(ctx, import_paths);
    marshall.Marshall.Sync(ctx, file_paths);
  }
}

}

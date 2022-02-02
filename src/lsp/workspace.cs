using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using bhl;

namespace bhlsp
{
  public abstract class BHLSPTextDocument
  {
    public Uri uri { get; set; }
    public string text { get; set; }
    
    List<int> indices = new List<int>();
    
    public virtual void Sync(string text)
    {
      this.text = text;
      ComputeIndexes();
    }

    void ComputeIndexes()
    {
      indices.Clear();
      
      int cur_index = 0;
      int cur_line = 0;
      int cur_col = 0;
      
      indices.Add(cur_index);

      int length = text.Length;
      // Go through file and record index of start of each line.
      for(int i = 0; i < length; ++i)
      {
        if(cur_index >= length)
          break;

        char ch = text[cur_index];
        if(ch == '\r')
        {
          if(cur_index + 1 >= length)
            break;
          
          if(text[cur_index + 1] == '\n')
          {
            cur_line++;
            cur_col = 0;
            cur_index += 2;
            indices.Add(cur_index);
          }
          else
          {
            // Error in code.
            cur_line++;
            cur_col = 0;
            cur_index += 1;
            indices.Add(cur_index);
          }
        }
        else if(ch == '\n')
        {
          cur_line++;
          cur_col = 0;
          cur_index += 1;
          indices.Add(cur_index);
        }
        else
        {
          cur_col += 1;
          cur_index += 1;
        }
        
        if(cur_index >= length)
          break;
      }
    }

    public int GetIndex(int line, int column)
    {
      if(indices.Count > 0)
        return indices[line] + column;

      return 0;
    }
    
    public int GetIndex(int line)
    {
      if(indices.Count > 0)
        return indices[line];

      return 0;
    }
    
    public (int, int) GetLineColumn(int index)
    {
      // Binary search.
      int low = 0;
      int high = indices.Count - 1;
      int i = 0;
      
      while (low <= high)
      {
        i = (low + high) / 2;
        var v = indices[i];
        if (v < index) low = i + 1;
        else if (v > index) high = i - 1;
        else break;
      }
      
      var min = low <= high ? i : high;
      return (min, index - indices[min]);
    }
  }

  public class BHLTextDocument : BHLSPTextDocument
  {
    public Dictionary<string, bhlParser.FuncDeclContext> funcDecls = new Dictionary<string, bhlParser.FuncDeclContext>();
    public List<string> imports = new List<string>();
    
    public override void Sync(string text)
    {
      base.Sync(text);
      
      imports.Clear();
      funcDecls.Clear();
      
      foreach(var progblock in ToParser().program().progblock())
      {
        var imports = progblock.imports();
        if(imports != null)
        {
          foreach(var mimport in imports.mimport())
          {
            var import = mimport.NORMALSTRING().GetText();
            //removing quotes
            import = import.Substring(1, import.Length-2);
            this.imports.Add(import);
          }
        }

        var decls = progblock.decls();
        if(decls != null)
        {
          foreach(var decl in decls.decl())
          {
            var fndecl = decl.funcDecl();
            if(fndecl?.NAME() != null)
              funcDecls.Add(fndecl.NAME().GetText(), fndecl);
          }
        }
      }
    }
    
    public IEnumerable<IParseTree> DFS()
    {
      IParseTree root = ToParser().program();
      
      Stack<IParseTree> toVisit = new Stack<IParseTree>();
      Stack<IParseTree> visitedAncestors = new Stack<IParseTree>();
      toVisit.Push(root);
      while(toVisit.Count > 0)
      {
        IParseTree node = toVisit.Peek();
        if(node.ChildCount > 0)
        {
          if(visitedAncestors.Count == 0 || visitedAncestors.Peek() != node)
          {
            visitedAncestors.Push(node);

            if(node as TerminalNodeImpl == null)
            {
              ParserRuleContext internal_node = node as ParserRuleContext;
              int child_count = internal_node.children.Count;
              for(int i = child_count - 1; i >= 0; --i)
              {
                IParseTree o = internal_node.children[i];
                toVisit.Push(o);
              }
              
              continue;
            }
          }
          
          visitedAncestors.Pop();
        }
        
        yield return node;
        
        toVisit.Pop();
      }
    }
    
    public bhlParser ToParser()
    {
      var ais = new AntlrInputStream(text.ToStream());
      var lex = new bhlLexer(ais);
      var tokens = new CommonTokenStream(lex);
      var parser = new bhlParser(tokens);
      
      lex.RemoveErrorListeners();
      parser.RemoveErrorListeners();

      return parser;
    }
  }
  
  public class BHLSPWorkspace
  {
    private static BHLSPWorkspace self_;
    public static BHLSPWorkspace self
    {
      get
      {
        if(self_ == null)
          self_ = new BHLSPWorkspace();

        return self_;
      }
    }

    struct RootPath
    {
      public string path;
      public bool cleanup;
    }
    
    private List<RootPath> root = new List<RootPath>();
    private Dictionary<string, BHLSPTextDocument> documents = new Dictionary<string, BHLSPTextDocument>();

    public TextDocumentSyncKind syncKind { get; set; } = TextDocumentSyncKind.Full;

    public bool declarationLinkSupport;
    public bool definitionLinkSupport;
    public bool typeDefinitionLinkSupport;
    public bool implementationLinkSupport;
    
    public void Shutdown()
    {
      documents.Clear();
      for(int i = root.Count - 1; i >= 0; i--)
      {
        if(root[i].cleanup)
          root.RemoveAt(i);
      }
    }

    public void AddRoot(string pathFolder, bool cleanup, bool check = true)
    {
      if(check && !Directory.Exists(pathFolder))
        return;
      
      root.Add(new RootPath {path = pathFolder, cleanup = cleanup});
    }
    
    public async void TryAddDocuments(string pathFolder)
    {
      pathFolder = Util.NormalizeFilePath(pathFolder);
      if(Directory.Exists(pathFolder))
      {
        AddRoot(pathFolder, true, false);
        
        string[] files = new string[0];
        
#if BHLSP_DEBUG
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
        
        await Task.Run(() =>
        {
          files = Directory.GetFiles(pathFolder, "*.bhl", SearchOption.AllDirectories);
        });
        
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"SearchFiles ({files.Length}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
      
        sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
        Dictionary<string, string> map = new Dictionary<string, string>();
        
        var tasks = new List<Task>();
        for(int i = 0; i < files.Length; i++)
        {
          string path = files[i];
          tasks.Add(ReadAsyncFile(path, map));
        }
        
        await Task.WhenAll(tasks);
        
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"ReadFiles ({map.Count}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
        
        sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
        
        tasks.Clear();
        foreach (var path in map.Keys)
          tasks.Add(Task.Run(() => TryAddDocument(path, map[path])));
      
        await Task.WhenAll(tasks);
        
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"AddDocuments ({map.Count}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
#endif
      }
    }
    
    public void TryAddDocuments(WorkspaceFolder folder)
    {
      TryAddDocuments(folder.uri.LocalPath);
    }

    public void TryAddDocuments(Uri uriFolder)
    {
      TryAddDocuments(uriFolder.LocalPath);
    }
    
    object lockRead = new object();
    
    async Task ReadAsyncFile(string file, Dictionary<string, string> map)
    {
      byte[] buffer;
      using (FileStream sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
      {
        buffer = new byte[sourceStream.Length];
        await sourceStream.ReadAsync(buffer, 0, (int)sourceStream.Length);
      }

      string content = System.Text.Encoding.ASCII.GetString(buffer);
      
      lock(lockRead)
      {
        map.Add(file, content);
      }
    }
    
    public void TryAddDocument(string path, string text)
    {
      TryAddDocument(new Uri($"file://{path}"), text);
    }
    
    object lockAdd = new object();
    
    public void TryAddDocument(Uri uri, string text = null)
    {
      string path = uri.LocalPath;
      path = Util.NormalizeFilePath(path);
      
      if(!documents.ContainsKey(path))
      {
        BHLSPTextDocument document = CreateDocument(path);
        if(document != null)
        {
          document.uri = uri;
          document.Sync(string.IsNullOrEmpty(text) ? System.IO.File.ReadAllText(path) : text);

          lock(lockAdd)
          {
            if(!documents.ContainsKey(path))
              documents.Add(path, document);
          }
        }
      }
    }
    
    BHLSPTextDocument CreateDocument(string path)
    {
      if(File.Exists(path))
      {
        string ext = Path.GetExtension(path);
        switch(ext)
        {
          case ".bhl": return new BHLTextDocument();
        }
      }
      
      return null;
    }
    
    public BHLSPTextDocument FindDocument(Uri uri)
    {
      return FindDocument(uri.LocalPath);
    }
    
    public BHLSPTextDocument FindDocument(string path)
    {
      path = Util.NormalizeFilePath(path);
      
      if(documents.ContainsKey(path))
        return documents[path];

      return null;
    }

    public IEnumerable<BHLSPTextDocument> ForEachDocuments()
    {
      lock(lockAdd)
      {
        foreach(var document in documents.Values)
        {
          yield return document;
        }
      }
    }
    
    public IEnumerable<BHLTextDocument> forEachImports(BHLTextDocument root)
    {
      Queue<BHLTextDocument> toVisit = new Queue<BHLTextDocument>();
      
      toVisit.Enqueue(root);
      while(toVisit.Count > 0)
      {
        BHLTextDocument document = toVisit.Dequeue();
        
        string ext = Path.GetExtension(document.uri.AbsolutePath);
        foreach(var import in document.imports)
        {
          string path = ResolveImportPath(import, ext);
          if(!string.IsNullOrEmpty(path))
          {
            TryAddDocument(path, null);
            if(FindDocument(path) is BHLTextDocument doc)
              toVisit.Enqueue(doc);
          }
        }
        
        yield return document;
      }
    }

    string ResolveImportPath(string import, string ext)
    {
      var filePath = string.Empty;
      foreach(var root in this.root)
      {
        string path = string.Empty;
        if(!Path.IsPathRooted(import))
        {
          var dir = Path.GetDirectoryName(root.path);
          path = Path.GetFullPath(Path.Combine(dir, import) + ext);
          if(File.Exists(path))
          {
            filePath = path;
            break;
          }
        }
        else
        {
          path = Path.GetFullPath(root.path + "/" + import.Substring(import.LastIndexOf("/") + 1) + ext);
          if(File.Exists(path))
          {
            filePath = path;
            break;
          }
        }
      }
      
      return filePath;
    }
  }
}
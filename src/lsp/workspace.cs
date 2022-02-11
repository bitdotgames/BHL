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
    private class BHLTextDocumentVisitor : bhlBaseVisitor<object>
    {
      public readonly Dictionary<string, bhlParser.FuncDeclContext> funcDecls = new Dictionary<string, bhlParser.FuncDeclContext>();
      public readonly Dictionary<string, bhlParser.ClassDeclContext> classDecls = new Dictionary<string, bhlParser.ClassDeclContext>();
      public readonly Dictionary<string, bhlParser.VarDeclareAssignContext> varDeclars = new Dictionary<string, bhlParser.VarDeclareAssignContext>();
      public readonly List<string> imports = new List<string>();
      public readonly List<uint> dataSemanticTokens = new List<uint>();
      
      private int next;
      private BHLTextDocument document;

      public void VisitDocument(BHLTextDocument document)
      {
        this.document = document;
        next = 0;
        
        imports.Clear();
        funcDecls.Clear();
        classDecls.Clear();
        varDeclars.Clear();
        dataSemanticTokens.Clear();
        
        VisitProgram(document.ToParser().program());
      }
      
      public override object VisitProgram(bhlParser.ProgramContext ctx)
      {
        for(var i=0;i<ctx.progblock().Length;++i)
          Visit(ctx.progblock()[i]);
        
        return null;
      }

      public override object VisitClassDecl(bhlParser.ClassDeclContext ctx)
      {
        var classDeclName = ctx.NAME().GetText();
        if(!classDecls.ContainsKey(classDeclName))
          classDecls.Add(classDeclName, ctx);
        
        AddToken(ctx.NAME(), SemanticTokenTypes.@class);
        
        if(ctx.classEx() != null)
          AddToken(ctx.classEx().NAME(), SemanticTokenTypes.@class);
        
        return null;
      }
      
      public override object VisitFuncDecl(bhlParser.FuncDeclContext ctx)
      {
        var funcDeclName = ctx.NAME()?.GetText();
        if(!string.IsNullOrEmpty(funcDeclName) && !funcDecls.ContainsKey(funcDeclName))
        {
          funcDecls.Add(funcDeclName, ctx);
          AddToken(ctx.NAME(), SemanticTokenTypes.function);
        }
        
        return null;
      }

      public override object VisitVarDeclareAssign(bhlParser.VarDeclareAssignContext ctx)
      {
        string varDeclareAssignName = ctx.varDeclare()?.NAME()?.GetText();
        if(!string.IsNullOrEmpty(varDeclareAssignName) && !varDeclars.ContainsKey(varDeclareAssignName))
          varDeclars.Add(varDeclareAssignName, ctx);
        
        return null;
      }
      
      public override object VisitImports(bhlParser.ImportsContext ctx)
      {
        foreach(var mimport in ctx.mimport())
        {
          var import = mimport.NORMALSTRING().GetText();
          //removing quotes
          import = import.Substring(1, import.Length-2);
          imports.Add(import);
        }
        
        return null;
      }
      
      private void AddToken(ITerminalNode node, string tokenType)
      {
        if(string.IsNullOrEmpty(tokenType) || node == null)
          return;
      
        var t = Array.IndexOf(semanticTokenTypes, tokenType);
        if(t < 0)
          return;
      
        var nextStart = document.GetLineColumn(next);
        var lineColumnSymbol = document.GetLineColumn(node.Symbol.StartIndex);

        var diffLine = lineColumnSymbol.Item1 - nextStart.Item1;
        var diffColumn = diffLine != 0 ? lineColumnSymbol.Item2 : lineColumnSymbol.Item2 - nextStart.Item2;
        // line
        dataSemanticTokens.Add((uint)diffLine);
        // startChar
        dataSemanticTokens.Add((uint)diffColumn);
        // length
        dataSemanticTokens.Add((uint)(node.Symbol.StopIndex - node.Symbol.StartIndex + 1));
        // tokenType
        dataSemanticTokens.Add((uint)t);
        // tokenModifiers
        dataSemanticTokens.Add(0);

        next = node.Symbol.StartIndex;
      }
    }
    
    public static string[] semanticTokenTypes = 
    {
      SemanticTokenTypes.@class,
      SemanticTokenTypes.comment,
      SemanticTokenTypes.function
    };
    
    public static string[] semanticTokenModifiers = 
    {
      SemanticTokenModifiers.definition
    };

    private readonly BHLTextDocumentVisitor visitor = new BHLTextDocumentVisitor();

    public Dictionary<string, bhlParser.ClassDeclContext> ClassDecls => visitor.classDecls;
    public Dictionary<string, bhlParser.VarDeclareAssignContext> VarDeclars => visitor.varDeclars;
    public Dictionary<string, bhlParser.FuncDeclContext> FuncDecls => visitor.funcDecls;
    public List<string> Imports => visitor.imports;
    public List<uint> DataSemanticTokens => visitor.dataSemanticTokens;
    
    public override void Sync(string text)
    {
      base.Sync(text);
      visitor.VisitDocument(this);
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
    
    public async void TryAddDocuments()
    {
      foreach(var rootPath in root)
      {
        string[] files = new string[0];
        
#if BHLSP_DEBUG
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
        
        await Task.Run(() =>
        {
          files = Directory.GetFiles(rootPath.path, "*.bhl", SearchOption.AllDirectories);
        });
        
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"SearchFiles ({files.Length}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
        
        sw = new System.Diagnostics.Stopwatch();
        sw.Start();
#endif
        
        var tasks = new List<Task>();
        foreach(var path in files)
          tasks.Add(Task.Run(() => TryAddDocument(path)));
        await Task.WhenAll(tasks);
        
#if BHLSP_DEBUG
        sw.Stop();
        BHLSPLogger.WriteLine($"AddDocuments ({files.Length}) done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
#endif
      }
    }
    
    public void TryAddDocument(string path, string text = null)
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

          if(string.IsNullOrEmpty(text))
          {
            byte[] buffer = File.ReadAllBytes(path);
            text = System.Text.Encoding.Default.GetString(buffer);
          }
          
          lock(lockAdd)
          {
            if(!documents.ContainsKey(path))
            {
              document.Sync(text);
              documents.Add(path, document);
            }
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
    
    public IEnumerable<BHLTextDocument> forEachBhlImports(BHLTextDocument root)
    {
      Queue<BHLTextDocument> toVisit = new Queue<BHLTextDocument>();
      
      toVisit.Enqueue(root);
      while(toVisit.Count > 0)
      {
        BHLTextDocument document = toVisit.Dequeue();
        
        string ext = Path.GetExtension(document.uri.AbsolutePath);
        foreach(var import in document.Imports)
        {
          string path = ResolveImportPath(document.uri.LocalPath, import, ext);
          if(!string.IsNullOrEmpty(path))
          {
            TryAddDocument(path);
            if(FindDocument(path) is BHLTextDocument doc)
              toVisit.Enqueue(doc);
          }
        }
        
        yield return document;
      }
    }

    public string ResolveImportPath(string docpath, string import, string ext)
    {
      var filePath = string.Empty;

      foreach(var root in this.root)
      {
        string path = string.Empty;
        if(!Path.IsPathRooted(import))
        {
          var dir = Path.GetDirectoryName(docpath);
          path = Path.GetFullPath(Path.Combine(dir, import) + ext);
          if(File.Exists(path))
          {
            filePath = path;
            break;
          }
        }
        else
        {
          path = Path.GetFullPath(root.path + "/" + import + ext);
          if(File.Exists(path))
          {
            filePath = path;
            break;
          }
        }
      }
      
      if(!string.IsNullOrEmpty(filePath))
        filePath = Util.NormalizeFilePath(filePath);
      
      return filePath;
    }
  }
}
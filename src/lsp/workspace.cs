using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Antlr4.Runtime;
using bhl;

namespace bhlsp
{
  public abstract class BHLSPTextDocument
  {
    public Uri uri { get; set; }
    public string text { get;  protected set; }

    public virtual void Sync(string text)
    {
      this.text = text;
    }
  }
  
  public class BHLTextDocument : BHLSPTextDocument
  {
    public override void Sync(string text)
    {
      base.Sync(text);
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
    
    private Dictionary<string, BHLSPTextDocument> documents = new Dictionary<string, BHLSPTextDocument>();

    public TextDocumentSyncKind syncKind { get; set; } = TextDocumentSyncKind.Full;

    public bool declarationLinkSupport;
    public bool definitionLinkSupport;
    public bool typeDefinitionLinkSupport;
    public bool implementationLinkSupport;
    
    public void Shutdown()
    {
      documents.Clear();
    }
    
    public void TryAddDocuments(string pathFolder)
    {
      TryAddDocuments(new Uri(pathFolder));
    }
    
    public void TryAddDocuments(WorkspaceFolder folder)
    {
      TryAddDocuments(folder.uri);
    }
    
    public void TryAddDocuments(Uri uriFolder)
    {
      if(!Directory.Exists(uriFolder.LocalPath))
        return;
      
      Dictionary<string, string> contents = new Dictionary<string, string>();
      
#if BHLSP_DEBUG
      var sw = new System.Diagnostics.Stopwatch();
      sw.Start();
#endif
      
      var files = Directory.GetFiles(uriFolder.LocalPath, "*.bhl", SearchOption.AllDirectories);
      
#if BHLSP_DEBUG
      sw.Stop();
      BHLSPLogger.WriteLine($"SearchFiles done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
      
      sw = new System.Diagnostics.Stopwatch();
      sw.Start();
#endif
      
      var tasks = new List<Task>();
      for (int i = 0; i < files.Length; i++)
      {
        string path = files[i];
        if(!documents.ContainsKey(path))
          tasks.Add(ReadAsyncFile(files[i], contents));
      }
      
      Task.WhenAll(tasks).Wait();
      
#if BHLSP_DEBUG
      sw.Stop();
      BHLSPLogger.WriteLine($"ReadFiles done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
      
      sw = new System.Diagnostics.Stopwatch();
      sw.Start();
#endif
      
      tasks.Clear();
      foreach(var path in contents.Keys)
        tasks.Add(TryAddAsyncDocument(path, contents[path]));
      
      Task.WhenAll(tasks).Wait();
      
#if BHLSP_DEBUG
      sw.Stop();
      BHLSPLogger.WriteLine($"AddDocuments done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
#endif
    }
    
    async Task TryAddAsyncDocument(string path, string text)
    {
      var task = Task.Run(() => TryAddDocument(path, text));
      await task;
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
    
    object lockAddDocument = new object();
    
    public void TryAddDocument(Uri uri, string text = null)
    {
      string path = uri.LocalPath;

      if(!documents.ContainsKey(path))
      {
        BHLSPTextDocument document = CreateDocument(path);
        if(document != null)
        {
          document.Sync(string.IsNullOrEmpty(text) ? System.IO.File.ReadAllText(path) : text);
        
          lock(lockAddDocument)
          {
            if(!documents.ContainsKey(path))
              documents.Add(path, document);
          }
        }
      }
    }
    
    public BHLSPTextDocument FindDocument(Uri uri)
    {
      return FindDocument(uri.LocalPath);
    }
    
    public BHLSPTextDocument FindDocument(string path)
    {
      if(documents.ContainsKey(path))
        return documents[path];

      return null;
    }
    
    BHLSPTextDocument CreateDocument(string path)
    {
      string ext = Path.GetExtension(path);
      switch(ext)
      {
        case ".bhl": return new BHLTextDocument();
      }

      return null;
    }
  }
}
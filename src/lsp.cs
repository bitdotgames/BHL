using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhlsp
{
  public class BHLSPServer
  {
    byte[] buffer = new byte[4096];
    
    private const byte CR = (byte)13;
    private const byte LF = (byte)10;
    private readonly byte[] separator = { CR, LF };
    
    private Stream input;
    private Stream output;

    private BHLSPServerTarget target;
    
    private readonly object outputLock = new object();
    
    public BHLSPServer(Stream output, Stream input)
    {
      this.input = input;
      this.output = output;

      target = new BHLSPServerTarget(this);
    }
    
    public async Task Listen()
    {
      while (true)
      {
        try
        {
          var success = await ReadAndHandle();
          if (!success)
            break;
        }
        catch (Exception e)
        {
          BHLSPC.Logger.WriteLine(e);
        }
      }
    }
    
    async Task<bool> ReadAndHandle()
    {
      string json = await Read();
      
      var msg = JsonConvert.DeserializeObject<RequestMessage>(json); //TODO: Batch Mode
      
      if(msg == null)
        return false;

      object result = null;
      
      if(msg.IsMessage())
      {
        BHLSPC.Logger.WriteLine($"INCOMING {msg.method}");
        
        foreach(var method in target.GetType().GetMethods())
        {
          if(IsJsonRpcMethod(method, msg.method))
          {  
            result = method.Invoke(target, new object[] {msg.id, msg.@params});
          }
        }
      }

      if (result != null)
      {
        Write(JsonConvert.SerializeObject(result));
      }
      
      return true;
    }

    private bool IsJsonRpcMethod(MethodInfo method, string name)
    {
      foreach(var attribute in method.GetCustomAttributes(true))
      {
        if(attribute is JsonRpcMethodAttribute jsonRpcMethod && jsonRpcMethod.Method == name)
          return true;
      }
      
      return false;
    }

    private async Task<string> Read()
    {
      int length = await FillBufferAsync();
      int offset = 0;
      
      //skip header
      while (ReadToSeparator(offset, length, out int count))
        offset += count + separator.Length;
      
      return Encoding.UTF8.GetString(buffer, offset, length - offset);
    }

    private bool ReadToSeparator(int offset, int length, out int count)
    {
      count = 0;
      
      for (int i = offset; i < length; i++)
      {
        if(IsSeparatorMatched(i, length))
          return true;
        
        count++;
      }
      
      return false;
    }
    
    private bool IsSeparatorMatched(int offset, int len)
    {
      for (var i = 0; i < separator.Length; i++)
      {
        var b = offset + i < len ? buffer[offset + i] : buffer[offset];
        if (b != separator[i])
          return false;
      }
      
      return true;
    }
    
    async Task<int> FillBufferAsync()
    {
      try
      {
        int length = await input.ReadAsync(buffer, 0, buffer.Length);
        return length;
      }
      catch
      {
        return 0;
      }
    }
    
    private void Write(string json)
    {
      var utf8 = Encoding.UTF8.GetBytes(json);
      lock (outputLock)
      {
        using (var writer = new StreamWriter(output, Encoding.ASCII, 1024, true))
        {
          writer.Write($"Content-Length: {utf8.Length}\r\n");
          writer.Write("\r\n");
          writer.Flush();
        }
        
        output.Write(utf8, 0, utf8.Length);
        output.Flush();
      }
    }
  }
  
  [AttributeUsage(AttributeTargets.Method)]
  public class JsonRpcMethodAttribute : Attribute
  {
    private string method;

    public JsonRpcMethodAttribute(string method)
    {
      this.method = method;
    }

    public string Method => method;
  }
  
  internal class BHLSPServerTarget
  {
    private BHLSPServer server;
    
    public BHLSPServerTarget(BHLSPServer server)
    {
      this.server = server;
    }
    
    [JsonRpcMethod("initialize")]
    public object Initialize(SumType<int, string> id, JToken args)
    {
      BHLSPC.Logger.WriteLine("--> Initialize");
      BHLSPC.Logger.WriteLine(args.ToString());
      
      var init_params = args.ToObject<InitializeParams>();

      bool enableCompletion = false;
      
      ServerCapabilities capabilities = new ServerCapabilities
      {
        textDocumentSync = new TextDocumentSyncOptions
        {
          openClose = true,
          change = TextDocumentSyncKind.Incremental,
          save = new SaveOptions
          {
            includeText = true
          }
        },

        completionProvider =
          enableCompletion
            ? new CompletionOptions
            {
              resolveProvider = true,
              triggerCharacters = new[] {",", "."}
            }
            : null,

        hoverProvider = true,

        signatureHelpProvider = null,

        declarationProvider = null,

        definitionProvider = true,

        typeDefinitionProvider = false,

        implementationProvider = false,

        referencesProvider = true,

        documentHighlightProvider = true,

        documentSymbolProvider = true,

        codeLensProvider = null,

        documentLinkProvider = null,

        colorProvider = false,

        documentFormattingProvider = true,

        documentRangeFormattingProvider = false,

        renameProvider = true,

        foldingRangeProvider = false,

        executeCommandProvider = null,

        selectionRangeProvider = null,

        workspaceSymbolProvider = false,
        
        codeActionProvider = false,

        semanticTokensProvider = new SemanticTokensOptions
        {
          full = true,
          range = false,
          legend = new SemanticTokensLegend
          {
            tokenTypes = new[]
            {
              "class",
              "variable",
              "enum",
              "comment",
              "string",
              "keyword",
            },
            tokenModifiers = new[]
            {
              "declaration",
              "documentation",
            }
          }
        },
      };
      
      InitializeResult result = new InitializeResult
      {
        capabilities = capabilities
      };
      
      string json = JsonConvert.SerializeObject(result);
      BHLSPC.Logger.WriteLine("<-- " + json);
      
      return new ResponseMessage { id = id, result = result };
    }
    
    [JsonRpcMethod("initialized")]
    public void Initialized(SumType<int, string> id, JToken args)
    {
      BHLSPC.Logger.WriteLine("--> Initialized");
    }
    
    [JsonRpcMethod("shutdown")]
    public object Shutdown(SumType<int, string> id, JToken args)
    {
      BHLSPC.Logger.WriteLine("--> Shutdown");
      return new ResponseMessage { id = id };
    }
  }

  internal class MessageBase
  {
    public string jsonrpc { get; set; } = "2.0";

    public bool IsMessage()
    {
      return jsonrpc == "2.0";
    }
  }

  internal class RequestMessage : MessageBase
  {
    public SumType<int, string> id { get; set; }
    public string method { get; set; }
    public JToken @params { get; set; }
  }
  
  public enum ErrorCodes
  {
    ParseError = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603,
    ServerErrorStart = -32099,
    ServerErrorEnd = -32000,
    ServerNotInitialized = -32002,
    UnknownErrorCode = -32001,
    RequestCancelled = -32800,
  }
  
  internal class ResponseError
  {
    public ErrorCodes code { get; set; }
    public string message { get; set; }
    public object data { get; set; }
  }
  
  internal class ResponseMessage : MessageBase
  {
    public SumType<int, string> id { get; set; }
    public object result { get; set; }
    public ResponseError error { get; set; }
  }
  
#region -- PROTOCOL ---
  
  public class SumTypeConverter : JsonConverter
  {
    private readonly object converterLock = new object();
    
    public override bool CanConvert(Type objectType)
    {
      return true;
    }
    
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      if (writer == null)
        throw new ArgumentNullException(nameof(writer));
      
      object obj = ((ISumType)value).Value;
      if (obj == null)
      {
        writer.WriteNull();
        return;
      }
      
      JToken.FromObject(obj).WriteTo(writer, Array.Empty<JsonConverter>());
    }
    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      JToken jtoken = JToken.ReadFrom(reader);
      
      try
      {
        lock (converterLock)
        {
          foreach (Type con in objectType.GenericTypeArguments)
          {
            try
            {
              var p = jtoken.ToObject(jtoken.Type == JTokenType.String ? typeof(string) : con, serializer);
              Type[] ts = { p.GetType() };
              ConstructorInfo c = objectType.GetConstructor(ts);
              if (c != null)
              {
                object[] parameters = { p };
                
                var test = c.Invoke(parameters);
                return test;
              }
            }
            catch
            {
            }
          }
        }
      }
      finally
      {
      }
      
      throw new JsonSerializationException();
    }
  }
  
  public interface ISumType
  {
    object Value { get; }
  }
  
  [JsonConverter(typeof(SumTypeConverter))]
  public struct SumType<T1, T2> : IEquatable<SumType<T1, T2>>, ISumType
  {
    public SumType(T1 val)
    {
      this.Value = (object) val;
    }

    public SumType(T2 val)
    {
      this.Value = (object) val;
    }

    public object Value { get; }

    public override bool Equals(object obj)
    {
      return obj is SumType<T1, T2> other && this.Equals(other);
    }

    public bool Equals(SumType<T1, T2> other)
    {
      return EqualityComparer<object>.Default.Equals(this.Value, other.Value);
    }

    public override int GetHashCode()
    {
      return EqualityComparer<object>.Default.GetHashCode(this.Value) - 1937169414;
    }

    public TResult Match<TResult>(Func<T1, TResult> firstMatch, Func<T2, TResult> secondMatch,
      Func<TResult> defaultMatch = null)
    {
      if (firstMatch == null)
        throw new ArgumentNullException(nameof(firstMatch));
      if (secondMatch == null)
        throw new ArgumentNullException(nameof(secondMatch));
      if (this.Value is T1 obj1)
        return firstMatch(obj1);
      if (this.Value is T2 obj2)
        return secondMatch(obj2);
      return defaultMatch != null ? defaultMatch() : default(TResult);
    }

    public static bool operator ==(SumType<T1, T2> left, SumType<T1, T2> right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(SumType<T1, T2> left, SumType<T1, T2> right)
    {
      return !(left == right);
    }

    public static implicit operator SumType<T1, T2>(T1 val)
    {
      return new SumType<T1, T2>(val);
    }

    public static implicit operator SumType<T1, T2>(T2 val)
    {
      return new SumType<T1, T2>(val);
    }

    public static explicit operator T1(SumType<T1, T2> sum)
    {
      if (sum.Value is T1 obj1)
        return obj1;
      throw new InvalidCastException();
    }

    public static explicit operator T2(SumType<T1, T2> sum)
    {
      if (sum.Value is T2 obj2)
        return obj2;
      throw new InvalidCastException();
    }
  }
  
  [JsonConverter(typeof(SumTypeConverter))]
  public struct SumType<T1, T2, T3> : IEquatable<SumType<T1, T2, T3>>, ISumType
  {
    public SumType(T1 val)
    {
      this.Value = (object) val;
    }

    public SumType(T2 val)
    {
      this.Value = (object) val;
    }

    public SumType(T3 val)
    {
      this.Value = (object) val;
    }

    public object Value { get; }

    public override bool Equals(object obj)
    {
      return obj is SumType<T1, T2, T3> other && this.Equals(other);
    }

    public bool Equals(SumType<T1, T2, T3> other)
    {
      return EqualityComparer<object>.Default.Equals(this.Value, other.Value);
    }

    public override int GetHashCode()
    {
      return EqualityComparer<object>.Default.GetHashCode(this.Value) - 1937169414;
    }

    public TResult Match<TResult>(Func<T1, TResult> firstMatch, Func<T2, TResult> secondMatch,
      Func<T3, TResult> thirdMatch, Func<TResult> defaultMatch = null)
    {
      if (firstMatch == null)
        throw new ArgumentNullException(nameof(firstMatch));
      if (secondMatch == null)
        throw new ArgumentNullException(nameof(secondMatch));
      if (thirdMatch == null)
        throw new ArgumentNullException(nameof(thirdMatch));
      if (this.Value is T1 obj1)
        return firstMatch(obj1);
      if (this.Value is T2 obj2)
        return secondMatch(obj2);
      if (this.Value is T3 obj3)
        return thirdMatch(obj3);
      return defaultMatch != null ? defaultMatch() : default(TResult);
    }

    public static bool operator ==(SumType<T1, T2, T3> left, SumType<T1, T2, T3> right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(SumType<T1, T2, T3> left, SumType<T1, T2, T3> right)
    {
      return !(left == right);
    }

    public static implicit operator SumType<T1, T2, T3>(T1 val)
    {
      return new SumType<T1, T2, T3>(val);
    }

    public static implicit operator SumType<T1, T2, T3>(T2 val)
    {
      return new SumType<T1, T2, T3>(val);
    }

    public static implicit operator SumType<T1, T2, T3>(T3 val)
    {
      return new SumType<T1, T2, T3>(val);
    }

    public static implicit operator SumType<T1, T2, T3>(SumType<T1, T2> sum)
    {
      throw new NotImplementedException();
    }

    public static explicit operator SumType<T1, T2>(SumType<T1, T2, T3> sum)
    {
      if (sum.Value is T1 obj1)
        return (SumType<T1, T2>) obj1;
      if (sum.Value is T2 obj2)
        return (SumType<T1, T2>) obj2;
      throw new InvalidCastException();
    }

    public static explicit operator T1(SumType<T1, T2, T3> sum)
    {
      if (sum.Value is T1 obj)
        return obj;
      throw new InvalidCastException();
    }

    public static explicit operator T2(SumType<T1, T2, T3> sum)
    {
      if (sum.Value is T2 obj)
        return obj;
      throw new InvalidCastException();
    }

    public static explicit operator T3(SumType<T1, T2, T3> sum)
    {
      if (sum.Value is T3 obj)
        return obj;
      throw new InvalidCastException();
    }
  }
  
  public class WorkspaceFolder
  {
    public string uri { get; set; }
    public string name { get; set; }
  }
  
  public class SaveOptions
  {
    public bool? includeText { get; set; }
  }
  
  public class WorkDoneProgressOptions
  {
    public bool? workDoneProgress { get; set; } = false;
  }
  
  public class CompletionOptions : WorkDoneProgressOptions
  {
    public string[] triggerCharacters { get; set; }
    public string[] allCommitCharacters { get; set; }
    public bool? resolveProvider { get; set; }
  }
  
  public class HoverOptions : WorkDoneProgressOptions
  {
  }
  
  public class SignatureHelpOptions : WorkDoneProgressOptions
  {
    public string[] triggerCharacters { get; set; }
    public string[] retriggerCharacters { get; set; }
  }
  
  public class DeclarationOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentFilter
  {
    public string language { get; set; }
    public string scheme { get; set; }
    public string pattern { get; set; }
  }
  
  public class DeclarationRegistrationOptions : DeclarationOptions
  {
    public DocumentFilter[] documentSelector { get; set; }
    public string id { get; set; }
  }
  
  public class DefinitionOptions : WorkDoneProgressOptions
  {
    public DocumentFilter[] documentSelector { get; set; }
    public string id { get; set; }
  }
  
  public class TypeDefinitionOptions : WorkDoneProgressOptions
  {
  }
  
  public class TextDocumentRegistrationOptions
  {
    public DocumentFilter[] documentSelector { get; set; }
  }
  
  public class TypeDefinitionRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
    public string id { get; set; }
  }
  
  public enum TextDocumentSyncKind { None=0, Full=1, Incremental=2 }
  
  public class TextDocumentSyncOptions
  {
    public bool? openClose { get; set; }
    public TextDocumentSyncKind? change { get; set; }
    public bool? willSave { get; set; }
    public bool? willSaveWaitUntil { get; set; }
    public SumType<bool, SaveOptions> save { get; set; }
  }
  
  public class ImplementationOptions : WorkDoneProgressOptions
  {
  }
  
  public class ImplementationRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
    public string id { get; set; }
  }
  
  public class ReferenceOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentHighlightOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentSymbolOptions : WorkDoneProgressOptions
  {
    public string label { get; set; }
  }

  public enum CodeActionKind
  {
    QuickFix=0,
    Refactor=1,
    RefactorExtract=2,
    RefactorInline=3,
    RefactorRewrite=4,
    Source=5,
    SourceOrganizeImports=6,
    Empty=7
  }

  public class CodeActionOptions : WorkDoneProgressOptions
  {
    public CodeActionKind[] codeActionKinds { get; set; }
    public bool? resolveProvider { get; set; }
  }
  
  public class CodeLensOptions : WorkDoneProgressOptions
  {
    public bool? resolveProvider { get; set; }
  }
  
  public class DocumentLinkOptions : WorkDoneProgressOptions
  {
    public bool? resolveProvider { get; set; }
  }
  
  public class DocumentColorOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentColorRegistrationOptions : TextDocumentRegistrationOptions
  {
    public string id { get; set; }
    public bool? workDoneProgress { get; set; }
  }
  
  public class DocumentFormattingOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentRangeFormattingOptions : WorkDoneProgressOptions
  {
  }
  
  public class DocumentOnTypeFormattingOptions
  {
    public string firstTriggerCharacter { get; set; }
    public string[] moreTriggerCharacter { get; set; }
  }
  
  public class RenameOptions : WorkDoneProgressOptions
  {
    public bool? prepareProvider { get; set; }
  }
  
  public class FoldingRangeOptions : WorkDoneProgressOptions
  {
  }
  
  public class FoldingRangeRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
    public string id { get; set; }
  }
  
  public class ExecuteCommandOptions : WorkDoneProgressOptions
  {
    public string[] commands { get; set; }
  }
  
  public class SelectionRangeOptions : WorkDoneProgressOptions
  {
  }
  
  public class SelectionRangeRegistrationOptions : SelectionRangeOptions
  {
    public DocumentFilter[] documentSelector { get; set; }
    public string id { get; set; }
  }
  
  public class LinkedEditingRangeOptions : WorkDoneProgressOptions
  {
  }
  
  public class LinkedEditingRangeRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
    public string id { get; set; }
  }
  
  public class CallHierarchyOptions : WorkDoneProgressOptions
  {
  }
  
  public class CallHierarchyRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
    public string id { get; set; }
  }
  
  public class SemanticTokensLegend
  {
    public string[] tokenTypes { get; set; }
    public string[] tokenModifiers { get; set; }
  }
  
  public class SemanticTokensOptions : WorkDoneProgressOptions
  {
    public SemanticTokensLegend legend { get; set; }
    public SumType<bool, object> range { get; set; }
    public SumType<bool, object> full { get; set; }
  }
  
  public class SemanticTokensRegistrationOptions : TextDocumentRegistrationOptions
  {
    public SemanticTokensLegend legend { get; set; }
    public SumType<bool, object> range { get; set; }
    public SumType<bool, object> full { get; set; }
    public string id { get; set; }
    public bool? workDoneProgress { get; set; }
  }
  
  public class MonikerOptions : WorkDoneProgressOptions
  {
  }
  
  public class MonikerRegistrationOptions : TextDocumentRegistrationOptions
  {
    public bool? workDoneProgress { get; set; }
  }
  
  public class WorkspaceSymbolOptions : WorkDoneProgressOptions
  {
  }
  
  public class WorkDoneProgressParams
  {
    public SumType<string, int> workDoneToken { get; set; }
  }
  
  public class InitializeParams : WorkDoneProgressParams
  {
    public class InitializeParamsClientInfo
    {
      public string name { get; set; }
      public string version { get; set; }
    }

    public int? processId { get; set; }
    public InitializeParamsClientInfo clientInfo { get; set; }
    public string locale { get; set; } //(See https://en.wikipedia.org/wiki/IETF_language_tag)
    public string rootPath { get; set; }
    public Uri rootUri { get; set; }
    public object initializationOptions { get; set; }
    public string trace { get; set; }
    public WorkspaceFolder[] workspaceFolders { get; set; }
  }
  
  public enum FileOperationPatternKind { File = 0, Folder = 1 }
  
  public class FileOperationPatternOptions
  {
    public bool? ignoreCase { get; set; }
  }
  
  public class FileOperationPattern
  {
    public string glob { get; set; }
    public FileOperationPatternKind matches { get; set; }
    public FileOperationPatternOptions options { get; set; }
  }
  
  public class FileOperationFilter
  {
    public string scheme { get; set; }
    public FileOperationPattern pattern { get; set; }
  }
  
  public class FileOperationRegistrationOptions
  {
    public FileOperationFilter[] filters { get; set; }
  }
  
  public class ServerCapabilities
  {
    public class WorkspaceFoldersServerCapabilities
    {
      public bool? supported { get; set; }
      public SumType<string, bool> changeNotifications { get; set; }
    }
    
    public class ServerCapabilitiesWorkspaceFileOperations
    {
      public FileOperationRegistrationOptions didCreate { get; set; }
      public FileOperationRegistrationOptions willCreate { get; set; }
      public FileOperationRegistrationOptions didRename { get; set; }
      public FileOperationRegistrationOptions willRename { get; set; }
      public FileOperationRegistrationOptions didDelete { get; set; }
      public FileOperationRegistrationOptions willDelete { get; set; }
    }
    
    public class ServerCapabilitiesWorkspace
    {
      public WorkspaceFoldersServerCapabilities workspaceFolders { get; set; }
      public ServerCapabilitiesWorkspaceFileOperations fileOperations { get; set; }
    }
    
    public SumType<TextDocumentSyncOptions, TextDocumentSyncKind> textDocumentSync { get; set; }
    public CompletionOptions completionProvider { get; set; }
    public SumType<bool, HoverOptions> hoverProvider { get; set; }
    public SignatureHelpOptions signatureHelpProvider { get; set; }
    public SumType<bool, DeclarationOptions, DeclarationRegistrationOptions> declarationProvider { get; set; }
    public SumType<bool, DefinitionOptions> definitionProvider { get; set; }
    public SumType<bool, TypeDefinitionOptions, TypeDefinitionRegistrationOptions> typeDefinitionProvider { get; set; }
    public SumType<bool, ImplementationOptions, ImplementationRegistrationOptions> implementationProvider { get; set; }
    public SumType<bool, ReferenceOptions> referencesProvider { get; set; }
    public SumType<bool, DocumentHighlightOptions> documentHighlightProvider { get; set; }
    public SumType<bool, DocumentSymbolOptions> documentSymbolProvider { get; set; }
    public SumType<bool, CodeActionOptions> codeActionProvider { get; set; }
    public CodeLensOptions codeLensProvider { get; set; }
    public DocumentLinkOptions documentLinkProvider { get; set; }
    public SumType<bool, DocumentColorOptions, DocumentColorRegistrationOptions> colorProvider { get; set; }
    public SumType<bool, DocumentFormattingOptions> documentFormattingProvider { get; set; }
    public SumType<bool, DocumentRangeFormattingOptions> documentRangeFormattingProvider { get; set; }
    public DocumentOnTypeFormattingOptions documentOnTypeFormattingProvider { get; set; }
    public SumType<bool, RenameOptions> renameProvider { get; set; }
    public SumType<bool, FoldingRangeOptions, FoldingRangeRegistrationOptions> foldingRangeProvider { get; set; }
    public ExecuteCommandOptions executeCommandProvider { get; set; }
    public SumType<bool, SelectionRangeOptions, SelectionRangeRegistrationOptions> selectionRangeProvider { get; set; }
    public SumType<bool, LinkedEditingRangeOptions, LinkedEditingRangeRegistrationOptions> linkedEditingRangeProvider { get; set; }
    public SumType<bool, CallHierarchyOptions, CallHierarchyRegistrationOptions> callHierarchyProvider { get; set; }
    public SumType<SemanticTokensOptions, SemanticTokensRegistrationOptions> semanticTokensProvider { get; set; }
    public SumType<bool, MonikerOptions, MonikerRegistrationOptions> monikerProvider { get; set; }
    public SumType<bool, WorkspaceSymbolOptions> workspaceSymbolProvider { get; set; }
    public ServerCapabilitiesWorkspace workspace { get; set; }
    public object experimental { get; set; }
  }
  
  public class InitializeResult
  {
    public class InitializeResultsServerInfo
    {
      public string name { get; set; }
      public string version { get; set; }
    }
    
    public ServerCapabilities capabilities { get; set; }
    public InitializeResultsServerInfo serverInfo { get; set; }
  }
  
  public enum InitializeErrorCode { UnknownProtocolVersion = 1 }
  
  public class InitializeError
  {
    public bool retry { get; set; }
  }
#endregion
}
using System;
using System.Collections;
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
    private BHLSPConnection connection;
    
    public BHLSPServer(Stream output, Stream input)
    {
      var target = new BHLSPServerTarget();

      target.AttachRpcService(new BHLSPGeneralJsonRpcService());
      
      connection = new BHLSPConnection(output, input, target);
    }
    
    public async Task Listen()
    {
      while (true)
      {
        try
        {
          var success = await connection.ReadAndHandle();
          if (!success)
            break;
        }
        catch (Exception e)
        {
          BHLSPC.Logger.WriteLine(e);
        }
      }
    }
  }
  
  internal class BHLSPConnection
  {
    byte[] buffer = new byte[4096];
    
    private const byte CR = (byte)13;
    private const byte LF = (byte)10;
    
    private readonly byte[] separator = { CR, LF };
    
    private Stream input;
    private Stream output;
    
    private readonly object outputLock = new object();
    
    private BHLSPServerTarget target;
    
    public BHLSPConnection(Stream output, Stream input, BHLSPServerTarget target)
    {
      this.input = input;
      this.output = output;
      this.target = target;
    }
    
    public async Task<bool> ReadAndHandle()
    {
      string json = await Read();

      RequestMessage req = null;
      ResponseMessage resp = null;

      try
      {
        req = JsonConvert.DeserializeObject<RequestMessage>(json);
        if(req == null)
          return false;
      }
      catch
      {
        resp = new ResponseMessage
        {
          id = null, error = new ResponseError
          {
            code = (int)ErrorCodes.ParseError,
            message = ""
          }
        };
      }
      
      if(req != null && req.IsMessage())
        resp = target.HandleMessage(req);
      
      if(resp != null)
        Write(JsonConvert.SerializeObject(resp));
      
      return true;
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
  
  internal abstract class MessageBase
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
    RequestFailed = -32803 // @since 3.17.0
  }
  
  internal class ResponseError
  {
    public int code { get; set; }
    public string message { get; set; }
    public object data { get; set; }
  }
  
  internal class ResponseMessage : MessageBase
  {
    public SumType<int, string> id { get; set; }
    public object result { get; set; }
    public ResponseError error { get; set; }
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

  internal abstract class BHLSPJsonRpcService
  {
  }

  internal abstract class BHLSPGeneralJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("initialize")]
    public abstract RpcResult Initialize(InitializeParams args);

    [JsonRpcMethod("initialized")]
    public abstract RpcResult Initialized();

    [JsonRpcMethod("shutdown")]
    public abstract RpcResult Shutdown();

    [JsonRpcMethod("exit")]
    public abstract RpcResult Exit();
  }

  internal abstract class BHLSPTextDocumentJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("textDocument/didOpen")]
    public abstract RpcResult DidOpenTextDocument(DidOpenTextDocumentParams args);

    [JsonRpcMethod("textDocument/didChange")]
    public abstract RpcResult DidChangeTextDocument(DidChangeTextDocumentParams args);

    [JsonRpcMethod("textDocument/willSave")]
    public abstract RpcResult WillSaveTextDocument(WillSaveTextDocumentParams args);

    [JsonRpcMethod("textDocument/willSaveWaitUntil")]
    public abstract RpcResult WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams args);

    [JsonRpcMethod("textDocument/didSave")]
    public abstract RpcResult DidSaveTextDocument(DidSaveTextDocumentParams args);

    [JsonRpcMethod("textDocument/didClose")]
    public abstract RpcResult DidCloseTextDocument(DidCloseTextDocumentParams args);

    [JsonRpcMethod("textDocument/completion")]
    public abstract RpcResult Completion(CompletionParams args);

    [JsonRpcMethod("completionItem/resolve")]
    public abstract RpcResult ResolveCompletionItem(CompletionItem args);

    [JsonRpcMethod("textDocument/hover")]
    public abstract RpcResult Hover(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/signatureHelp")]
    public abstract RpcResult SignatureHelp(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/references")]
    public abstract RpcResult FindReferences(ReferenceParams args);

    [JsonRpcMethod("textDocument/documentHighlight")]
    public abstract RpcResult DocumentHighlight(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/documentSymbol")]
    public abstract RpcResult DocumentSymbols(DocumentSymbolParams args);

    [JsonRpcMethod("textDocument/documentColor")]
    public abstract RpcResult DocumentColor(DocumentColorParams args);

    [JsonRpcMethod("textDocument/colorPresentation")]
    public abstract RpcResult ColorPresentation(ColorPresentationParams args);

    [JsonRpcMethod("textDocument/formatting")]
    public abstract RpcResult DocumentFormatting(DocumentFormattingParams args);

    [JsonRpcMethod("textDocument/rangeFormatting")]
    public abstract RpcResult DocumentRangeFormatting(DocumentRangeFormattingParams args);

    [JsonRpcMethod("textDocument/onTypeFormatting")]
    public abstract RpcResult DocumentOnTypeFormatting(DocumentOnTypeFormattingParams args);

    [JsonRpcMethod("textDocument/definition")]
    public abstract RpcResult GotoDefinition(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/typeDefinition")]
    public abstract RpcResult GotoTypeDefinition(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/implementation")]
    public abstract RpcResult GotoImplementation(TextDocumentPositionParams args);

    [JsonRpcMethod("textDocument/codeAction")]
    public abstract RpcResult CodeAction(CodeActionParams args);

    [JsonRpcMethod("textDocument/codeLens")]
    public abstract RpcResult CodeLens(CodeLensParams args);

    [JsonRpcMethod("codeLens/resolve")]
    public abstract RpcResult ResolveCodeLens(CodeLens args);

    [JsonRpcMethod("textDocument/documentLink")]
    public abstract RpcResult DocumentLink(DocumentLinkParams args);

    [JsonRpcMethod("documentLink/resolve")]
    public abstract RpcResult ResolveDocumentLink(DocumentLink args);

    [JsonRpcMethod("textDocument/rename")]
    public abstract RpcResult Rename(RenameParams args);

    [JsonRpcMethod("textDocument/foldingRange")]
    public abstract RpcResult FoldingRange(FoldingRangeParams args);
  }

  internal abstract class BHLSPWorkspaceJsonRpcServiceTemplate : BHLSPJsonRpcService
  {
    [JsonRpcMethod("workspace/didChangeWorkspaceFolders")]
    public abstract RpcResult DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams args);

    [JsonRpcMethod("workspace/didChangeConfiguration")]
    public abstract RpcResult DidChangeConfiguration(DidChangeConfigurationParams args);

    [JsonRpcMethod("workspace/didChangeWatchedFiles")]
    public abstract RpcResult DidChangeWatchedFiles(DidChangeWatchedFilesParams args);

    [JsonRpcMethod("workspace/symbol")]
    public abstract RpcResult Symbol(WorkspaceSymbolParams args);

    [JsonRpcMethod("workspace/executeCommand")]
    public abstract RpcResult ExecuteCommand(ExecuteCommandParams args);
  }
  
  internal class BHLSPServerTarget
  {
    List<BHLSPJsonRpcService> services = new List<BHLSPJsonRpcService>();

    public BHLSPServerTarget AttachRpcService(BHLSPJsonRpcService service)
    {
      services.Add(service);
      return this;
    }
    
    public ResponseMessage HandleMessage(RequestMessage request)
    {
      BHLSPC.Logger.WriteLine($"--> {request.method}");

      ResponseMessage response = null;
      bool isNotification = request.id.Value == null;
      
      try
      {
        RpcResult result = CallRpcMethod(request.method, request.@params);
          
        if (!isNotification)
        {
          response = new ResponseMessage
          {
            id = request.id,
            result = result.result,
            error = result.error
          };
        }
      }
      catch (Exception e)
      {
        BHLSPC.Logger.WriteLine(e);
        if(!isNotification)
        {
          response = new ResponseMessage
          {
            id = request.id, error = new ResponseError
            {
              code = (int)ErrorCodes.InternalError,
              message = ""
            }
          };
        }
      }
      
      if(response != null)
        BHLSPC.Logger.WriteLine($"<-- {request.method}");
      
      return response;
    }

    private RpcResult CallRpcMethod(string name, JToken @params)
    {
      foreach (var service in services)
      {
        foreach (var method in service.GetType().GetMethods())
        {
          if (IsAllowedToInvoke(method, name))
          {
            object[] args = null;
            var pms = method.GetParameters();
            if(pms.Length > 0)
              args = new[] { @params.ToObject(pms[0].ParameterType) };
            
            return (RpcResult)method.Invoke(service, args);
          }
        }
      }
      
      return RpcResult.Error(new ResponseError
      {
        code = (int)ErrorCodes.MethodNotFound,
        message = ""
      });
    }

    private bool IsAllowedToInvoke(MethodInfo m, string name)
    {
      foreach(var attribute in m.GetCustomAttributes(true))
      {
        if(attribute is JsonRpcMethodAttribute jsonRpcMethod && jsonRpcMethod.Method == name)
          return true;
      }

      return false;
    }
  }
  
  internal class RpcResult
  {
    public object result;
    public ResponseError error;
    
    public static RpcResult Success(object result)
    {
      return new RpcResult(result, null);
    }

    public static RpcResult Error(ResponseError error)
    {
      return new RpcResult(null, error);
    }
    
    RpcResult(object result, ResponseError error)
    {
      this.result = result;
      this.error = error;
    }
  }

#region -- RPC SERVICES --

  internal class BHLSPGeneralJsonRpcService : BHLSPGeneralJsonRpcServiceTemplate
  {
    public override RpcResult Initialize(InitializeParams args)
    {
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
        
        hoverProvider = false,
        declarationProvider = false,
        definitionProvider = false,
        typeDefinitionProvider = false,
        implementationProvider = false,
        referencesProvider = false,
        documentHighlightProvider = false,
        documentSymbolProvider = false,
        
        colorProvider = true,
        
        documentFormattingProvider = false,
        documentRangeFormattingProvider = false,
        renameProvider = false,
        foldingRangeProvider = false,
        selectionRangeProvider = false,
        codeActionProvider = false,
        linkedEditingRangeProvider = false,
        callHierarchyProvider = false,
        monikerProvider = false,
        workspaceSymbolProvider = false,
        
        semanticTokensProvider = new SemanticTokensOptions
        {
          full = true,
          range = false,
          legend = new SemanticTokensLegend
          {
            tokenTypes = new[]
            {
              "class",
              "enum",
              "variable",
              "comment",
              "string",
              "function"
            },
            tokenModifiers = new[]
            {
              "declaration",
              "documentation",
            }
          }
        },
      };
      
      return RpcResult.Success(new InitializeResult
      {
        capabilities = capabilities,
        serverInfo = new InitializeResult.InitializeResultsServerInfo
        {
          name = "bhlsp",
          version = "0.0.1"
        }
      });
    }

    public override RpcResult Initialized()
    {
      return RpcResult.Success(null);
    }

    public override RpcResult Shutdown()
    {
      return RpcResult.Success(null);
    }

    public override RpcResult Exit()
    {
      return RpcResult.Success(null);
    }
  }

#endregion  
  
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
        /*writer.WriteStartObject();
          writer.WriteEnd();*/

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
      /**
		   * The server supports workspace folder.
		   *
		   * @since 3.6.0
		   */
      public WorkspaceFoldersServerCapabilities workspaceFolders { get; set; }
      
      /**
		   * The server is interested in file notifications/requests.
		   *
		   * @since 3.16.0
		   */
      public ServerCapabilitiesWorkspaceFileOperations fileOperations { get; set; }
    }
    
    /**
	   * Defines how text documents are synced. Is either a detailed structure
	   * defining each notification or for backwards compatibility the
	   * TextDocumentSyncKind number. If omitted it defaults to
	   * `TextDocumentSyncKind.None`.
	   */
    public SumType<TextDocumentSyncOptions, TextDocumentSyncKind> textDocumentSync { get; set; }
    
    /**
	   * The server provides completion support.
	   */
    public CompletionOptions completionProvider { get; set; }
    
    /**
	   * The server provides hover support.
	   */
    public SumType<bool, HoverOptions> hoverProvider { get; set; }
    
    /**
	   * The server provides signature help support.
	   */
    public SignatureHelpOptions signatureHelpProvider { get; set; }
    
    /**
	   * The server provides go to declaration support.
	   *
	   * @since 3.14.0
	   */
    public SumType<bool, DeclarationOptions, DeclarationRegistrationOptions> declarationProvider { get; set; }
    
    /**
	   * The server provides goto definition support.
	   */
    public SumType<bool, DefinitionOptions> definitionProvider { get; set; }
    
    /**
	   * The server provides goto type definition support.
	   *
	   * @since 3.6.0
	   */
    public SumType<bool, TypeDefinitionOptions, TypeDefinitionRegistrationOptions> typeDefinitionProvider { get; set; }
    
    /**
	   * The server provides goto implementation support.
	   *
	   * @since 3.6.0
	   */
    public SumType<bool, ImplementationOptions, ImplementationRegistrationOptions> implementationProvider { get; set; }
    
    /**
	   * The server provides find references support.
	   */
    public SumType<bool, ReferenceOptions> referencesProvider { get; set; }
    
    /**
	   * The server provides document highlight support.
	   */
    public SumType<bool, DocumentHighlightOptions> documentHighlightProvider { get; set; }
    
    /**
	   * The server provides document symbol support.
	   */
    public SumType<bool, DocumentSymbolOptions> documentSymbolProvider { get; set; }
    
    /**
	   * The server provides code actions. The `CodeActionOptions` return type is
	   * only valid if the client signals code action literal support via the
	   * property `textDocument.codeAction.codeActionLiteralSupport`.
	   */
    public SumType<bool, CodeActionOptions> codeActionProvider { get; set; }
    
    /**
	   * The server provides code lens.
	   */
    public CodeLensOptions codeLensProvider { get; set; }
    
    /**
	   * The server provides document link support.
	   */
    public DocumentLinkOptions documentLinkProvider { get; set; }
    
    /**
	   * The server provides color provider support.
	   *
	   * @since 3.6.0
	   */
    public SumType<bool, DocumentColorOptions, DocumentColorRegistrationOptions> colorProvider { get; set; }
    
    /**
	   * The server provides document formatting.
	   */
    public SumType<bool, DocumentFormattingOptions> documentFormattingProvider { get; set; }
    
    /**
	   * The server provides document range formatting.
	   */
    public SumType<bool, DocumentRangeFormattingOptions> documentRangeFormattingProvider { get; set; }
    
    /**
	   * The server provides document formatting on typing.
	   */
    public DocumentOnTypeFormattingOptions documentOnTypeFormattingProvider { get; set; }
    
    /**
	   * The server provides rename support. RenameOptions may only be
	   * specified if the client states that it supports
	   * `prepareSupport` in its initial `initialize` request.
	   */
    public SumType<bool, RenameOptions> renameProvider { get; set; }
    
    /**
	   * The server provides folding provider support.
	   *
	   * @since 3.10.0
	   */
    public SumType<bool, FoldingRangeOptions, FoldingRangeRegistrationOptions> foldingRangeProvider { get; set; }
    
    /**
	   * The server provides execute command support.
	   */
    public ExecuteCommandOptions executeCommandProvider { get; set; }
    
    /**
	   * The server provides selection range support.
	   *
	   * @since 3.15.0
	   */
    public SumType<bool, SelectionRangeOptions, SelectionRangeRegistrationOptions> selectionRangeProvider { get; set; }
    
    /**
	   * The server provides linked editing range support.
	   *
	   * @since 3.16.0
	   */
    public SumType<bool, LinkedEditingRangeOptions, LinkedEditingRangeRegistrationOptions> linkedEditingRangeProvider { get; set; }
    
    /**
	   * The server provides call hierarchy support.
	   *
	   * @since 3.16.0
	   */
    public SumType<bool, CallHierarchyOptions, CallHierarchyRegistrationOptions> callHierarchyProvider { get; set; }
    
    /**
	   * The server provides semantic tokens support.
	   *
	   * @since 3.16.0
	   */
    public SumType<SemanticTokensOptions, SemanticTokensRegistrationOptions> semanticTokensProvider { get; set; }
    
    /**
	   * Whether server provides moniker support.
	   *
	   * @since 3.16.0
	   */
    public SumType<bool, MonikerOptions, MonikerRegistrationOptions> monikerProvider { get; set; }
    
    /**
     * The server provides workspace symbol support.
     */
    public SumType<bool, WorkspaceSymbolOptions> workspaceSymbolProvider { get; set; }
    
    /**
	   * Workspace specific server capabilities
	   */
    public ServerCapabilitiesWorkspace workspace { get; set; }
    
    /**
	   * Experimental server capabilities.
	   */
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
    
    /**
	   * Information about the server.
	   *
	   * @since 3.15.0
	   */
    public InitializeResultsServerInfo serverInfo { get; set; }
  }

  public class InitializeError
  {
    public bool retry { get; set; }
  }
  
  public class TextDocumentItem
  {
    /**
     * The text document's URI.
     */
    public string uri { get; set; }

    /**
     * The text document's language identifier.
     */
    public string languageId { get; set; }

    /**
     * The version number of this document (it will increase after each
     * change, including undo/redo).
     */
    public int version { get; set; }

    /**
     * The content of the opened text document.
     */
    public string text { get; set; }
  }
  
  public class DidOpenTextDocumentParams
  {
    /**
     * The document that was opened.
     */
    public TextDocumentItem textDocument { get; set; }
  }
  
  public class TextDocumentIdentifier
  {
    /**
     * The text document's URI.
     */
    public string uri { get; set; }
  }
  
  public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
  {
    /**
     * The version number of this document.
	   *
	   * The version number of a document will increase after each change,
	   * including undo/redo. The number doesn't need to be consecutive.
     */
    public int version { get; set; }
  }
  
  public class Position
  {
    /**
     * Line position in a document (zero-based).
     */
    public uint line { get; set; }

    /**
     * Character offset on a line in a document (zero-based). Assuming that
	   * the line is represented as a string, the `character` value represents
	   * the gap between the `character` and `character + 1`.
	   *
	   * If the character value is greater than the line length it defaults back
	   * to the line length.
     */
    public uint character { get; set; }
  }
  
  public class Range
  {
    /**
     * The range's start position.
     */
    public Position start { get; set; }

    /**
     * The range's end position.
     */
    public Position end { get; set; }
  }
  
  public class TextDocumentContentChangeEvent
  {
    /**
     * The range of the document that changed.
     */
    public Range range { get; set; }

    /**
     * The optional length of the range that got replaced.
     *
     * @deprecated use range instead.
     */
    public uint? rangeLength { get; set; }

    /**
     * The new text for the provided range.
     * or
     * The new text of the whole document.
     */
    public string text { get; set; }
  }
  
  public class DidChangeTextDocumentParams
  {
    /**
     * The document that did change. The version number points
     * to the version after all provided content changes have
     * been applied.
     */
    public VersionedTextDocumentIdentifier textDocument { get; set; }

    /**
     * The actual content changes. The content changes describe single state
	   * changes to the document. So if there are two content changes c1 (at
	   * array index 0) and c2 (at array index 1) for a document in state S then
	   * c1 moves the document from S to S' and c2 from S' to S''. So c1 is
	   * computed on the state S and c2 is computed on the state S'.
	   *
       * To mirror the content of a document using change events use the following
	   * approach:
	   * - start with the same initial content
     * - apply the 'textDocument/didChange' notifications in the order you
	   *   receive them.
	   * - apply the `TextDocumentContentChangeEvent`s in a single notification
	   *   in the order you receive them.
     */
    public TextDocumentContentChangeEvent[] contentChanges { get; set; }
  }
  
  public enum TextDocumentSaveReason { Manual = 1, AfterDelay = 2, FocusOut = 3 }
  
  public class WillSaveTextDocumentParams
  {
    /**
     * The document that will be saved.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * The 'TextDocumentSaveReason'.
     */
    public TextDocumentSaveReason reason { get; set; }
  }
  
  public class DidSaveTextDocumentParams
  {
    /**
     * The document that was saved.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * Optional the content when saved. Depends on the includeText value
     * when the save notification was requested.
     */
    public string text { get; set; }
  }
  
  public class DidCloseTextDocumentParams
  {
    /**
     * The document that was closed.
     */
    public TextDocumentIdentifier textDocument { get; set; }
  }
  
  public class TextDocumentPositionParams
  {
    /**
     * The text document.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * The position inside the text document.
     */
    public Position position { get; set; }
  }
  
  public enum CompletionTriggerKind { Invoked = 1, TriggerCharacter = 2, TriggerForIncompleteCompletions = 3 }
  
  public class CompletionContext
  {
    /**
     * How the completion was triggered.
     */
    public CompletionTriggerKind triggerKind { get; set; }

    /**
     * The trigger character (a single character) that has trigger code
	   * complete. Is undefined if
	   * `triggerKind !== CompletionTriggerKind.TriggerCharacter`
	   */
    public string triggerCharacter { get; set; }
  }
  
  public class CompletionParams : TextDocumentPositionParams
  {
    /**
     * The completion context. This is only available if the client specifies
     * to send this using the client capability
	   * `completion.contextSupport === true`
     */
    public CompletionContext context { get; set; }

    /**
     * An optional token that a server can use to report work done progress.
     */
    public SumType<string, int> workDoneToken { get; set; }
        
    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public enum CompletionItemKind
  {
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25,
  }
  
  public enum CompletionItemTag { Deprecated = 1 }
  
  public class MarkupContent
  {
    /**
     * The type of the Markup
     * PlainText: 'plaintext' = 'plaintext';
     * Markdown: 'markdown' = 'markdown';
     * type MarkupKind = 'plaintext' | 'markdown';
     */
    public string kind { get; set; }

    /**
     * The content itself
     */
    public string value { get; set; }
  }
  
  public enum InsertTextFormat { Plaintext = 1, Snippet = 2 }
  
  public enum InsertTextMode { AsIs = 1, AdjustIndentation = 2 }
  
  public class TextEdit
  {
    /**
     * The range of the text document to be manipulated. To insert
     * text into a document create a range where start === end.
     */
    public Range range { get; set; }

    /**
     * The string to be inserted. For delete operations use an
     * empty string.
     */
    public string newText { get; set; }
  }
  
  public class InsertReplaceEdit
  {
    /**
	   * The string to be inserted.
	   */
    public string newText { get; set; }

    /**
	   * The range if the insert is requested
	   */
    public Range insert { get; set; }

    /**
	   * The range if the replace is requested.
	   */
    public Range replace { get; set; }
  }
  
  public class Command
  {
    /**
     * Title of the command, like `save`.
     */
    public string title { get; set; }

    /**
     * The identifier of the actual command handler.
     */
    public string command { get; set; }

    /**
     * Arguments that the command handler should be
     * invoked with.
     */
    public object[] arguments { get; set; }
  }
  
  public class CompletionItem
  {
    /**
	   * The label of this completion item.
	   *
	   * The label property is also by default the text that
	   * is inserted when selecting this completion.
	   *
	   * If label details are provided the label itself should
	   * be an unqualified name of the completion item.
	   */
    public string label { get; set; }

    /**
	   * The kind of this completion item. Based of the kind
	   * an icon is chosen by the editor. The standardized set
	   * of available values is defined in `CompletionItemKind`.
	   */
    public CompletionItemKind? kind { get; set; }

    /**
     * Tags for this completion item.
     *
     * @since 3.15.0
     */
    public CompletionItemTag[] tags { get; set; }

    /**
     * A human-readable string with additional information
     * about this item, like type or symbol information.
     */
    public string detail { get; set; }

    /**
     * A human-readable string that represents a doc-comment.
     */
    public SumType<string, MarkupContent> documentation { get; set; }

    /**
     * Indicates if this item is deprecated.
     *
     * @deprecated Use `tags` instead if supported.
     */
    public bool? deprecated { get; set; }

    /**
     * Select this item when showing.
     *
     * *Note* that only one completion item can be selected and that the
     * tool / client decides which item that is. The rule is that the *first*
     * item of those that match best is selected.
     */
    public bool? preselect { get; set; }

    /**
     * A string that should be used when comparing this item
     * with other items. When `falsy` the label is used.
     */
    public string sortText { get; set; }

    /**
     * A string that should be used when filtering a set of
     * completion items. When `falsy` the label is used.
     */
    public string filterText { get; set; }

    /**
     * A string that should be inserted into a document when selecting
     * this completion. When `falsy` the label is used.
     *
     * The `insertText` is subject to interpretation by the client side.
     * Some tools might not take the string literally. For example
     * VS Code when code complete is requested in this example
	   * `con<cursor position>` and a completion item with an `insertText` of
	   * `console` is provided it will only insert `sole`. Therefore it is
	   * recommended to use `textEdit` instead since it avoids additional client
	   * side interpretation.
     */
    public string insertText { get; set; }

    /**
	   * The format of the insert text. The format applies to both the
	   * `insertText` property and the `newText` property of a provided
	   * `textEdit`. If omitted defaults to `InsertTextFormat.PlainText`.
	   *
	   * Please note that the insertTextFormat doesn't apply to
	   * `additionalTextEdits`.
	   */
    public InsertTextFormat insertTextFormat { get; set; }

    /**
	   * How whitespace and indentation is handled during completion
	   * item insertion. If not provided the client's default value depends on
	   * the `textDocument.completion.insertTextMode` client capability.
	   *
	   * @since 3.16.0
	   * @since 3.17.0 - support for `textDocument.completion.insertTextMode`
	   */
    public InsertTextMode? insertTextMode { get; set; }

    /**
     * An edit which is applied to a document when selecting this completion.
	   * When an edit is provided the value of `insertText` is ignored.
	   *
	   * *Note:* The range of the edit must be a single line range and it must
	   * contain the position at which completion has been requested.
	   *
	   * Most editors support two different operations when accepting a completion
	   * item. One is to insert a completion text and the other is to replace an
	   * existing text with a completion text. Since this can usually not be
	   * predetermined by a server it can report both ranges. Clients need to
	   * signal support for `InsertReplaceEdits` via the
	   * `textDocument.completion.insertReplaceSupport` client capability
	   * property.
	   *
	   * *Note 1:* The text edit's range as well as both ranges from an insert
	   * replace edit must be a [single line] and they must contain the position
	   * at which completion has been requested.
	   * *Note 2:* If an `InsertReplaceEdit` is returned the edit's insert range
	   * must be a prefix of the edit's replace range, that means it must be
	   * contained and starting at the same position.
	   *
	   * @since 3.16.0 additional type `InsertReplaceEdit`
     */
    public SumType<TextEdit, InsertReplaceEdit>? textEdit { get; set; }

    /**
     * An optional array of additional text edits that are applied when
	   * selecting this completion. Edits must not overlap (including the same
	   * insert position) with the main edit nor with themselves.
	   *
	   * Additional text edits should be used to change text unrelated to the
	   * current cursor position (for example adding an import statement at the
	   * top of the file if the completion item will insert an unqualified type).
	   */
    public TextEdit[] additionalTextEdits { get; set; }

    /**
     * An optional set of characters that when pressed while this completion is
	   * active will accept it first and then type that character. *Note* that all
	   * commit characters should have `length=1` and that superfluous characters
	   * will be ignored.
     */
    public string[] commitCharacters { get; set; }

    /**
     * An optional command that is executed *after* inserting this completion.
	   * *Note* that additional modifications to the current document should be
	   * described with the additionalTextEdits-property.
     */
    public Command command { get; set; }

    /**
     * A data entry field that is preserved on a completion item between
     * a completion and a completion resolve request.
     */
    public object data { get; set; }
  }
  
  public class ReferenceContext
  {
    /**
     * Include the declaration of the current symbol.
     */
    public bool includeDeclaration { get; set; }
  }
  
  public class ReferenceParams : TextDocumentPositionParams
  {
    public ReferenceContext context { get; set; }

    /**
     * An optional token that a server can use to report work done progress.
     */
    public SumType<string, int> workDoneToken { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class DocumentSymbolParams : WorkDoneProgressParams
  {
    /**
     * The text document.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class DocumentColorParams : WorkDoneProgressParams
  {
    /**
	   * The text document.
	   */
    public TextDocumentIdentifier textDocument { get; set; }
    
    /**
	   * An optional token that a server can use to report partial results (e.g.
	   * streaming) to the client.
	   */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class Color
  {
    /**
		 * The red component of this color in the range [0-1].
		 */
    public float red { get; set; }

    /**
		 * The green component of this color in the range [0-1].
		 */
    public float green { get; set; }

    /**
		 * The blue component of this color in the range [0-1].
		 */
    public float blue { get; set; }

    /**
		 * The alpha component of this color in the range [0-1].
		 */
    public float alpha { get; set; }
  }
  
  public class ColorPresentationParams : WorkDoneProgressParams
  {
    /**
	   * The text document.
	   */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
	   * The color information to request presentations for.
	   */
    public Color color { get; set; }

    /**
	   * The range where the color would be inserted. Serves as a context.
	   */
    public Range range { get; set; }
    
    /**
	   * An optional token that a server can use to report partial results (e.g.
	   * streaming) to the client.
	   */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class FormattingOptions
  {
    /**
     * Size of a tab in spaces.
     */
    public uint tabSize { get; set; }

    /**
     * Prefer spaces over tabs.
     */
    public bool insertSpaces { get; set; }

    /**
     * Trim trailing whitespace on a line.
     *
     * @since 3.15.0
     */
    public bool? trimTrailingWhitespace { get; set; }

    /**
     * Insert a newline character at the end of the file if one does not exist.
     *
     * @since 3.15.0
     */
    public bool? insertFinalNewline { get; set; }

    /**
     * Trim all newlines after the final newline at the end of the file.
     *
     * @since 3.15.0
     */
    public bool? trimFinalNewlines { get; set; }

    /**
     * Signature for further properties.
     * [key: string]: boolean | integer | string;
     */
    //public object properties { get; set; } //TODO: ...
  }
  
  public class DocumentFormattingParams : WorkDoneProgressParams 
  {
    /**
     * The document to format.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * The format options.
     */
    public FormattingOptions options { get; set; }
  }
  
  public class DocumentRangeFormattingParams : WorkDoneProgressParams
  {
    /**
     * The document to format.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * The range to format
     */
    public Range range { get; set; }

    /**
     * The format options
     */
    public FormattingOptions options { get; set; }
  }
  
  public class DocumentOnTypeFormattingParams : TextDocumentPositionParams
  {
    /**
     * The character that has been typed.
     */
    public string ch { get; set; }

    /**
     * The format options.
     */
    public FormattingOptions options { get; set; }
  }
  
  public enum DiagnosticSeverity
  {
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
  }
  
  public class CodeDescription
  {
    /**
	   * An URI to open with more information about the diagnostic error.
	   */
    public string href { get; set; }
  }
  
  public enum DiagnosticTag
  {
    Unnecessary = 1,
    Deprecated = 2
  }
  
  public class Location
  {
    public string uri { get; set; }
    public Range range { get; set; }
  }
  
  public class DiagnosticRelatedInformation
  {
    /**
     * The location of this related diagnostic information.
     */
    public Location location { get; set; }

    /**
     * The message of this related diagnostic information.
     */
    public string message { get; set; }
  }
  
  public class Diagnostic
  {
    /**
     * The range at which the message applies.
     */
    public Range range { get; set; }

    /**
     * The diagnostic's severity. Can be omitted. If omitted it is up to the
     * client to interpret diagnostics as error, warning, info or hint.
     */
    public DiagnosticSeverity severity { get; set; }

    /**
     * The diagnostic's code, which might appear in the user interface.
     */
    public SumType<string, int> code { get; set; }

    /**
	   * An optional property to describe the error code.
	   *
	   * @since 3.16.0
	   */
    public CodeDescription codeDescription { get; set; }

    /**
     * A human-readable string describing the source of this
     * diagnostic, e.g. 'typescript' or 'super lint'.
     */
    public string source { get; set; }

    /**
     * The diagnostic's message.
     */
    public string message { get; set; }

    /**
     * Additional metadata about the diagnostic.
     *
     * @since 3.15.0
     */
    public DiagnosticTag[] tags { get; set; }

    /**
     * An array of related diagnostic information, e.g. when symbol-names within
     * a scope collide all definitions can be marked via this property.
     */
    public DiagnosticRelatedInformation[] relatedInformation { get; set; }

    /**
     * A data entry field that is preserved between a
     * `textDocument/publishDiagnostics` notification and
     * `textDocument/codeAction` request.
     *
     * @since 3.16.0
     */
    public object data { get; set; }
  }
  
  public class CodeActionContext
  {
    /**
     * An array of diagnostics known on the client side overlapping the range
	   * provided to the `textDocument/codeAction` request. They are provided so
	   * that the server knows which errors are currently presented to the user
	   * for the given range. There is no guarantee that these accurately reflect
	   * the error state of the resource. The primary parameter
	   * to compute code actions is the provided range.
     */
    public Diagnostic[] diagnostics { get; set; }

    /**
     * Requested kind of actions to return.
     *
     * Actions not of this kind are filtered out by the client before being
	   * shown. So servers can omit computing them.
	   */
    public CodeActionKind[] only { get; set; }
  }
  
  public class CodeActionParams : WorkDoneProgressParams
  {
    /**
     * The document in which the command was invoked.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * The range for which the command was invoked.
     */
    public Range range { get; set; }

    /**
     * Context carrying additional information.
     */
    public CodeActionContext context { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class CodeLensParams : WorkDoneProgressParams
  {
    /**
     * The document to request code lens for.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class CodeLens
  {
    /**
     * The range in which this code lens is valid. Should only span a single
	   * line.
     */
    public Range range { get; set; }

    /**
     * The command this code lens represents.
     */
    public Command command { get; set; }

    /**
     * A data entry field that is preserved on a code lens item between
     * a code lens and a code lens resolve request.
     */
    public object data { get; set; }
  }
  
  public class DocumentLinkParams : WorkDoneProgressParams
  {
    /**
     * The document to provide document links for.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class DocumentLink
  {
    /**
     * The range this link applies to.
     */
    public Range range { get; set; }

    /**
     * The uri this link points to. If missing a resolve request is sent later.
     */
    public string target { get; set; }

    /**
     * The tooltip text when you hover over this link.
     *
     * If a tooltip is provided, is will be displayed in a string that includes
	   * instructions on how to trigger the link, such as `{0} (ctrl + click)`.
	   * The specific instructions vary depending on OS, user settings, and
	   * localization.
	   *
     * @since 3.15.0
     */
    public string tooltip { get; set; }

    /**
     * A data entry field that is preserved on a document link between a
     * DocumentLinkRequest and a DocumentLinkResolveRequest.
     */
    public object data { get; set; }
  }
  
  public class RenameParams : TextDocumentPositionParams
  {
    /**
     * The new name of the symbol. If the given name is not valid the
     * request must return a [ResponseError](#ResponseError) with an
     * appropriate message set.
     */
    public string newName { get; set; }

    /**
     * An optional token that a server can use to report work done progress.
     */
    public SumType<string, int> workDoneToken { get; set; }
  }
  
  public class FoldingRangeParams : WorkDoneProgressParams
  {
    /**
     * The text document.
     */
    public TextDocumentIdentifier textDocument { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class WorkspaceFoldersChangeEvent
  {
    /**
     * The array of added workspace folders
     */
    public WorkspaceFolder[] added { get; set; }
    
    /**
     * The array of the removed workspace folders
     */
    public WorkspaceFolder[] removed { get; set; }
  }
  
  public class DidChangeWorkspaceFoldersParams
  {
    /**
     * The actual workspace folder change event.
     */
    public WorkspaceFoldersChangeEvent @event { get; set; }
  }
  
  public class DidChangeConfigurationParams
  {
    /**
     * The actual changed settings
     */
    public object settings { get; set; }
  }
  
  public enum FileChangeType
  {
    Created = 1,
    Changed = 2,
    Deleted = 3
  }
  
  public class FileEvent
  {
    /**
     * The file's URI.
     */
    public string uri { get; set; }

    /**
     * The change type.
     */
    public FileChangeType type { get; set; }
  }
  
  public class DidChangeWatchedFilesParams
  {
    /**
     * The actual file events.
     */
    public FileEvent[] changes { get; set; }
  }
  
  public class WorkspaceSymbolParams : WorkDoneProgressParams
  {
    /**
     * A query string to filter symbols by. Clients may send an empty
     * string here to request all symbols.
     */
    public string query { get; set; }

    /**
     * An optional token that a server can use to report partial results (e.g. streaming) to
     * the client.
     */
    public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class ExecuteCommandParams : WorkDoneProgressParams 
  {
    /**
     * The identifier of the actual command handler.
     */
    public string command { get; set; }

    /**
     * Arguments that the command should be invoked with.
     */
    public object[] arguments { get; set; }
  }
  
#endregion
}
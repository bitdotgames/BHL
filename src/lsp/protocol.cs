using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhl.lsp.proto {

public class SelectorTypeJsonConverter : JsonConverter
{
  public override bool CanConvert(Type objectType)
  {
    return objectType == typeof(ISelectorType);
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    WriteJson(writer, (ISelectorType)value, serializer);
  }

  void WriteJson(JsonWriter writer, ISelectorType value, JsonSerializer serializer)
  {
    var obj = value.Value;
    if(obj == null)
    {
      writer.WriteNull();
      return;
    }
    
    var jsonObject = JToken.FromObject(obj, serializer);
    jsonObject.WriteTo(writer);
  }
  
  public override bool CanRead => false;

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
  {
    throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
  }
}

public interface ISelectorType
{
  object Value { get; }
}

[JsonConverter(typeof(SelectorTypeJsonConverter))]
public struct EitherType<T1, T2> : IEquatable<EitherType<T1, T2>>, ISelectorType
{
  public object Value { get; }

  public EitherType(T1 val)
  {
    Value = val;
  }

  public EitherType(T2 val)
  {
    Value = val;
  }

  public override bool Equals(object obj)
  {
    return obj is EitherType<T1, T2> other && Equals(other);
  }

  public bool Equals(EitherType<T1, T2> other)
  {
    return EqualityComparer<object>.Default.Equals(Value, other.Value);
  }

  public override int GetHashCode()
  {
    return EqualityComparer<object>.Default.GetHashCode(Value) - 1937169414;
  }

  public static bool operator ==(EitherType<T1, T2> left, EitherType<T1, T2> right)
  {
    return left.Equals(right);
  }

  public static bool operator !=(EitherType<T1, T2> left, EitherType<T1, T2> right)
  {
    return !(left == right);
  }

  public static implicit operator EitherType<T1, T2>(T1 val)
  {
    return new EitherType<T1, T2>(val);
  }

  public static implicit operator EitherType<T1, T2>(T2 val)
  {
    return new EitherType<T1, T2>(val);
  }

  public static explicit operator T1(EitherType<T1, T2> sum)
  {
    if(sum.Value is T1 obj1)
      return obj1;

    throw new InvalidCastException();
  }

  public static explicit operator T2(EitherType<T1, T2> sum)
  {
    if(sum.Value is T2 obj2)
      return obj2;

    throw new InvalidCastException();
  }
}

[JsonConverter(typeof(SelectorTypeJsonConverter))]
public struct EitherType<T1, T2, T3> : IEquatable<EitherType<T1, T2, T3>>, ISelectorType
{
  public object Value { get; }

  public EitherType(T1 val)
  {
    Value = val;
  }

  public EitherType(T2 val)
  {
    Value = val;
  }

  public EitherType(T3 val)
  {
    Value = val;
  }

  public override bool Equals(object obj)
  {
    return obj is EitherType<T1, T2, T3> other && Equals(other);
  }

  public bool Equals(EitherType<T1, T2, T3> other)
  {
    return EqualityComparer<object>.Default.Equals(Value, other.Value);
  }

  public override int GetHashCode()
  {
    return EqualityComparer<object>.Default.GetHashCode(Value) - 1937169414;
  }

  public static bool operator ==(EitherType<T1, T2, T3> left, EitherType<T1, T2, T3> right)
  {
    return left.Equals(right);
  }

  public static bool operator !=(EitherType<T1, T2, T3> left, EitherType<T1, T2, T3> right)
  {
    return !(left == right);
  }

  public static implicit operator EitherType<T1, T2, T3>(T1 val)
  {
    return new EitherType<T1, T2, T3>(val);
  }

  public static implicit operator EitherType<T1, T2, T3>(T2 val)
  {
    return new EitherType<T1, T2, T3>(val);
  }

  public static implicit operator EitherType<T1, T2, T3>(T3 val)
  {
    return new EitherType<T1, T2, T3>(val);
  }

  public static explicit operator T1(EitherType<T1, T2, T3> sum)
  {
    if(sum.Value is T1 obj)
      return obj;

    throw new InvalidCastException();
  }

  public static explicit operator T2(EitherType<T1, T2, T3> sum)
  {
    if(sum.Value is T2 obj)
      return obj;

    throw new InvalidCastException();
  }

  public static explicit operator T3(EitherType<T1, T2, T3> sum)
  {
    if(sum.Value is T3 obj)
      return obj;

    throw new InvalidCastException();
  }
}

public class UriJsonConverter : JsonConverter
{
  public override bool CanConvert(Type objectType)
  {
    return true;
  }

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
  {
    string path = (string)reader.Value;
    var uri = Uri.Decode(path);
    return uri;
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    writer.WriteValue(value.ToString());
  }
}

[JsonConverter(typeof(UriJsonConverter))]
public class Uri
{
  public string path;

  public Uri()
  {}

  public Uri(string path)
  {
    this.path = Util.NormalizeFilePath(path);
  }

  public static Uri Decode(string path)
  {
    //let's skip the 'file://' part and extra '/' for Windows 
    string tmp = path.Substring(7); 
    if(System.IO.Path.DirectorySeparatorChar == '\\' && 
        tmp.Length > 3 && tmp[0] == '/' && tmp[2] == ':')
      tmp = tmp.Substring(1);

    return new Uri(tmp);
  }

  public string Encode()
  {
    string tmp = path;
    //let's remove the starting '/' for Windows if there's one
    if(System.IO.Path.DirectorySeparatorChar == '\\' && 
        tmp.Length > 3 && tmp[0] == '/' && tmp[2] == ':')
      tmp = tmp.Substring(1);

    return "file://" + tmp;
  }

  public override string ToString()
  {
    return Encode();
  }
}

public class WorkspaceFolder
{
  /**
   * The associated URI for this workspace folder.
   */
  public Uri uri;

  /**
   * The name of the workspace folder. Used to refer to this
   * workspace folder in the user interface.
   */
  public string name;
}

public class SaveOptions
{
  /**
   * The client is supposed to include the content on save.
   */
  public bool? includeText;
}

public class WorkDoneProgressOptions
{
  public bool? workDoneProgress = false;
}

public class CompletionOptions : WorkDoneProgressOptions
{
  public string[] triggerCharacters;
  public string[] allCommitCharacters;
  public bool? resolveProvider;
}

public class HoverOptions : WorkDoneProgressOptions
{
}

public class SignatureHelpOptions : WorkDoneProgressOptions
{
  /**
   * The characters that trigger signature help
   * automatically.
   */
  public string[] triggerCharacters;

  /**
   * List of characters that re-trigger signature help.
   *
   * These trigger characters are only active when signature help is already
   * showing. All trigger characters are also counted as re-trigger
   * characters.
   *
   * @since 3.15.0
   */
  public string[] retriggerCharacters;
}

public class DeclarationOptions : WorkDoneProgressOptions
{
}

public class DocumentFilter
{
  public string language;
  public string scheme;
  public string pattern;
}

public class DeclarationRegistrationOptions : DeclarationOptions
{
  public DocumentFilter[] documentSelector;
  public string id;
}

public class DefinitionOptions : WorkDoneProgressOptions
{
}

public class TypeDefinitionOptions : WorkDoneProgressOptions
{
}

public class TextDocumentRegistrationOptions
{
  public DocumentFilter[] documentSelector;
}

public class TypeDefinitionRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public class DefinitionRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public enum TextDocumentSyncKind
{
  /**
   * Documents should not be synced at all.
   */
  None = 0,

  /**
   * Documents are synced by always sending the full content
   * of the document.
   */
  Full = 1,

  /**
   * Documents are synced by sending the full content on open.
   * After that only incremental updates to the document are
   * send.
   */
  Incremental = 2
}

public class TextDocumentSyncOptions
{
  /**
   * Open and close notifications are sent to the server. If omitted open
   * close notification should not be sent.
   */
  public bool? openClose;

  /**
   * Change notifications are sent to the server. See
   * TextDocumentSyncKind.None, TextDocumentSyncKind.Full and
   * TextDocumentSyncKind.Incremental. If omitted it defaults to
   * TextDocumentSyncKind.None.
   */
  public TextDocumentSyncKind? change;

  /**
   * If present will save notifications are sent to the server. If omitted
   * the notification should not be sent.
   */
  public bool? willSave;

  /**
   * If present will save wait until requests are sent to the server. If
   * omitted the request should not be sent.
   */
  public bool? willSaveWaitUntil;

  /**
   * If present save notifications are sent to the server. If omitted the
   * notification should not be sent.
   */
  public EitherType<bool, SaveOptions> save;
}

public class ImplementationOptions : WorkDoneProgressOptions
{
}

public class ImplementationRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public class ReferenceOptions : WorkDoneProgressOptions
{
}

public class DocumentHighlightOptions : WorkDoneProgressOptions
{
}

public class DocumentSymbolOptions : WorkDoneProgressOptions
{
  /**
   * A human-readable string that is shown when multiple outlines trees
   * are shown for the same document.
   *
   * @since 3.16.0
   */
  public string label;
}

public enum CodeActionKind
{
  QuickFix = 0,
  Refactor = 1,
  RefactorExtract = 2,
  RefactorInline = 3,
  RefactorRewrite = 4,
  Source = 5,
  SourceOrganizeImports = 6,
  Empty = 7
}

public class CodeActionOptions : WorkDoneProgressOptions
{
  public CodeActionKind[] codeActionKinds;
  public bool? resolveProvider;
}

public class CodeLensOptions : WorkDoneProgressOptions
{
  public bool? resolveProvider;
}

public class DocumentLinkOptions : WorkDoneProgressOptions
{
  public bool? resolveProvider;
}

public class DocumentColorOptions : WorkDoneProgressOptions
{
}

public class DocumentColorRegistrationOptions : TextDocumentRegistrationOptions
{
  public string id;
  public bool? workDoneProgress;
}

public class DocumentFormattingOptions : WorkDoneProgressOptions
{
}

public class DocumentRangeFormattingOptions : WorkDoneProgressOptions
{
}

public class DocumentOnTypeFormattingOptions
{
  public string firstTriggerCharacter;
  public string[] moreTriggerCharacter;
}

public class RenameOptions : WorkDoneProgressOptions
{
  public bool? prepareProvider;
}

public class FoldingRangeOptions : WorkDoneProgressOptions
{
}

public class FoldingRangeRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public class ExecuteCommandOptions : WorkDoneProgressOptions
{
  public string[] commands;
}

public class SelectionRangeOptions : WorkDoneProgressOptions
{
}

public class SelectionRangeRegistrationOptions : SelectionRangeOptions
{
  public DocumentFilter[] documentSelector;
  public string id;
}

public class LinkedEditingRangeOptions : WorkDoneProgressOptions
{
}

public class LinkedEditingRangeRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public class CallHierarchyOptions : WorkDoneProgressOptions
{
}

public class CallHierarchyRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
  public string id;
}

public class SemanticTokensLegend
{
  public string[] tokenTypes;
  public string[] tokenModifiers;
}

public class SemanticTokensOptionRange
{
}

public class SemanticTokensOptionFull
{
  /**
   * The server supports deltas for full documents.
   */
  public bool? delta;
}

public class SemanticTokensOptions : WorkDoneProgressOptions
{
  public SemanticTokensLegend legend;
  public EitherType<bool, SemanticTokensOptionRange> range;
  public EitherType<bool, SemanticTokensOptionFull> full;
}

public class SemanticTokensRegistrationOptions : TextDocumentRegistrationOptions
{
  public SemanticTokensLegend legend;
  public EitherType<bool, object> range;
  public EitherType<bool, object> full;
  public string id;
  public bool? workDoneProgress;
}

public class MonikerOptions : WorkDoneProgressOptions
{
}

public class MonikerRegistrationOptions : TextDocumentRegistrationOptions
{
  public bool? workDoneProgress;
}

public class WorkspaceSymbolOptions : WorkDoneProgressOptions
{
}

public class WorkDoneProgressParams
{
  public EitherType<string, int> workDoneToken;
}

public class SignatureHelpClientCapabilities
{
  public class SignatureHelpClientCapabilitiesParameterInformation
  {
    /**
     * The client supports processing label offsets instead of a
     * simple label string.
     *
     * @since 3.14.0
     */
    public bool? labelOffsetSupport;
  }

  public class SignatureHelpClientCapabilitiesSignatureInformation
  {
    /**
     * Client supports the follow content formats for the documentation
     * property. The order describes the preferred format of the client.
     *
     * MarkupKind = 'plaintext' | 'markdown';
     */
    public string[] documentationFormat;

    /**
     * Client capabilities specific to parameter information.
     */
    public SignatureHelpClientCapabilitiesParameterInformation parameterInformation;

    /**
     * The client supports the `activeParameter` property on
     * `SignatureInformation` literal.
     *
     * @since 3.16.0
     */
    public bool? activeParameterSupport;
  }

  /**
   * Whether signature help supports dynamic registration.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports the following `SignatureInformation`
   * specific properties.
   */
  public SignatureHelpClientCapabilitiesSignatureInformation signatureInformation;

  /**
   * The client supports to send additional context information for a
   * `textDocument/signatureHelp` request. A client that opts into
   * contextSupport will also support the `retriggerCharacters` on
   * `SignatureHelpOptions`.
   *
   * @since 3.15.0
   */
  public bool? contextSupport;
}

public class TextDocumentSyncClientCapabilities
{
  /**
   * Whether text document synchronization supports dynamic registration.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports sending will save notifications.
   */
  public bool? willSave;

  /**
   * The client supports sending a will save request and
   * waits for a response providing text edits which will
   * be applied to the document before it is saved.
   */
  public bool? willSaveWaitUntil;

  /**
   * The client supports did save notifications.
   */
  public bool? didSave;
}

public class DeclarationClientCapabilities
{
  /**
   * Whether declaration supports dynamic registration. If this is set to
   * `true` the client supports the new `DeclarationRegistrationOptions`
   * return value for the corresponding server capability as well.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports additional metadata in the form of declaration links.
   */
  public bool? linkSupport;
}

public class DefinitionClientCapabilities
{
  /**
   * Whether definition supports dynamic registration.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports additional metadata in the form of definition links.
   *
   * @since 3.14.0
   */
  public bool? linkSupport;
}

public class TypeDefinitionClientCapabilities
{
  /**
   * Whether implementation supports dynamic registration. If this is set to
   * `true` the client supports the new `TypeDefinitionRegistrationOptions`
   * return value for the corresponding server capability as well.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports additional metadata in the form of definition links.
   *
   * @since 3.14.0
   */
  public bool? linkSupport;
}

public class ImplementationClientCapabilities
{
  /**
   * Whether implementation supports dynamic registration. If this is set to
   * `true` the client supports the new `ImplementationRegistrationOptions`
   * return value for the corresponding server capability as well.
   */
  public bool? dynamicRegistration;

  /**
   * The client supports additional metadata in the form of definition links.
   *
   * @since 3.14.0
   */
  public bool? linkSupport;
}

public class HoverClientCapabilities
{
  /**
   * Whether hover supports dynamic registration.
   */
  public bool? dynamicRegistration;

  /**
   * Client supports the follow content formats if the content
   * property refers to a `literal of type MarkupContent`.
   * The order describes the preferred format of the client.
   * 
   * MarkupKind = 'plaintext' | 'markdown';
   */
  public string[] contentFormat;
}

public class ReferenceClientCapabilities
{
  /**
   * Whether references supports dynamic registration.
   */
  public bool? dynamicRegistration;
}

public class SemanticTokensClientCapabilities
{
  public class SemanticTokensClientCapabilitiesRequests
  {
    /**
     * The client will send the `textDocument/semanticTokens/range` request
     * if the server provides a corresponding handler.
     */
    public EitherType<bool, object> range;

    /**
     * The client will send the `textDocument/semanticTokens/full` request
     * if the server provides a corresponding handler.
     */
    public EitherType<bool, object> full;
  }

  /**
   * Whether implementation supports dynamic registration. If this is set to
   * `true` the client supports the new `(TextDocumentRegistrationOptions &
   * StaticRegistrationOptions)` return value for the corresponding server
   * capability as well.
   */
  public bool? dynamicRegistration;

  /**
   * Which requests the client supports and might send to the server
   * depending on the server's capability. Please note that clients might not
   * show semantic tokens or degrade some of the user experience if a range
   * or full request is advertised by the client but not provided by the
   * server. If for example the client capability `requests.full` and
   * `request.range` are both set to true but the server only provides a
   * range provider the client might not render a minimap correctly or might
   * even decide to not show any semantic tokens at all.
   */
  public SemanticTokensClientCapabilitiesRequests requests;

  /**
   * The token types that the client supports.
   */
  public string[] tokenTypes;

  /**
   * The token modifiers that the client supports.
   */
  public string[] tokenModifiers;

  /**
   * The formats the clients supports.
   *
   * type TokenFormat = 'relative'
   */
  public string[] formats;

  /**
   * Whether the client supports tokens that can overlap each other.
   */
  public bool? overlappingTokenSupport;

  /**
   * Whether the client supports tokens that can span multiple lines.
   */
  public bool? multilineTokenSupport;

  /**
   * Whether the client allows the server to actively cancel a
   * semantic token request, e.g. supports returning
   * ErrorCodes.ServerCancelled. If a server does the client
   * needs to retrigger the request.
   *
   * @since 3.17.0
   */
  public bool? serverCancelSupport;

  /**
   * Whether the client uses semantic tokens to augment existing
   * syntax tokens. If set to `true` client side created syntax
   * tokens and semantic tokens are both used for colorization. If
   * set to `false` the client only uses the returned semantic tokens
   * for colorization.
   *
   * If the value is `undefined` then the client behavior is not
   * specified.
   *
   * @since 3.17.0
   */
  public bool? augmentsSyntaxTokens;
}

public class PublishDiagnosticsClientCapabilities 
{
	/**
	 * Whether the clients accepts diagnostics with related information.
	 */
	public bool relatedInformation;

  public class TagSupport 
  {
		/**
		 * The tags supported by the client.
		 */
		public DiagnosticTag[] valueSet;
  }

	/**
	 * Client supports the tag property to provide meta data about a diagnostic.
	 * Clients supporting tags have to handle unknown tags gracefully.
	 *
	 * @since 3.15.0
	 */
	public TagSupport tagSupport;

	/**
	 * Whether the client interprets the version property of the
	 * `textDocument/publishDiagnostics` notification's parameter.
	 *
	 * @since 3.15.0
	 */
	public bool versionSupport;

	/**
	 * Client supports a codeDescription property
	 *
	 * @since 3.16.0
	 */
	public bool codeDescriptionSupport;

	/**
	 * Whether code action supports the `data` property which is
	 * preserved between a `textDocument/publishDiagnostics` and
	 * `textDocument/codeAction` request.
	 *
	 * @since 3.16.0
	 */
	public bool dataSupport;
}

public class TextDocumentClientCapabilities
{
  public TextDocumentSyncClientCapabilities synchronization;

  /**
   * Capabilities specific to the `textDocument/hover` request.
   */
  public HoverClientCapabilities hover;

  /**
   * Capabilities specific to the `textDocument/signatureHelp` request.
   */
  public SignatureHelpClientCapabilities signatureHelp;

  /**
   * Capabilities specific to the `textDocument/declaration` request.
   *
   * @since 3.14.0
   */
  public DeclarationClientCapabilities declaration;

  /**
   * Capabilities specific to the `textDocument/definition` request.
   */
  public DefinitionClientCapabilities definition;

  /**
   * Capabilities specific to the `textDocument/typeDefinition` request.
   *
   * @since 3.6.0
   */
  public TypeDefinitionClientCapabilities typeDefinition;

  /**
   * Capabilities specific to the `textDocument/implementation` request.
   *
   * @since 3.6.0
   */
  public ImplementationClientCapabilities implementation;

  /**
   * Capabilities specific to the `textDocument/references` request.
   */
  public ReferenceClientCapabilities references;

  /**
   * Capabilities specific to the various semantic token requests.
   *
   * @since 3.16.0
   */
  public SemanticTokensClientCapabilities semanticTokens;

	/**
	 * Capabilities specific to the `textDocument/publishDiagnostics`
	 * notification.
	 */
	public PublishDiagnosticsClientCapabilities publishDiagnostics;
}

public class ClientCapabilities
{
  /**
   * Text document specific client capabilities.
   */
  public TextDocumentClientCapabilities textDocument;
}

public class InitializeParams : WorkDoneProgressParams
{
  public class InitializeParamsClientInfo
  {
    public string name;
    public string version;
  }

  /**
   * The process Id of the parent process that started the server. Is null if
   * the process has not been started by another process. If the parent
   * process is not alive then the server should exit (see exit notification)
   * its process.
   */
  public int? processId;

  /**
   * Information about the client
   *
   * @since 3.15.0
   */
  public InitializeParamsClientInfo clientInfo;

  /**
   * The locale the client is currently showing the user interface
   * in. This must not necessarily be the locale of the operating
   * system.
   *
   * Uses IETF language tags as the value's syntax
   * (See https://en.wikipedia.org/wiki/IETF_language_tag)
   *
   * @since 3.16.0
   */
  public string locale;

  /**
   * The rootPath of the workspace. Is null
   * if no folder is open.
   *
   * @deprecated in favour of `rootUri`.
   */
  public string rootPath;

  /**
   * The rootUri of the workspace. Is null if no
   * folder is open. If both `rootPath` and `rootUri` are set
   * `rootUri` wins.
   *
   * @deprecated in favour of `workspaceFolders`
   */
  public Uri rootUri;

  /**
   * User provided initialization options.
   */
  public object initializationOptions;

  /**
   * The capabilities provided by the client (editor or tool)
   */
  public ClientCapabilities capabilities;

  /**
   * The initial trace setting. If omitted trace is disabled ('off').
   */
  public string trace;

  /**
   * The workspace folders configured in the client when the server starts.
   * This property is only available if the client supports workspace folders.
   * It can be `null` if the client supports workspace folders but none are
   * configured.
   *
   * @since 3.6.0
   */
  public WorkspaceFolder[] workspaceFolders;
}

public enum FileOperationPatternKind
{
  File = 0,
  Folder = 1
}

public class FileOperationPatternOptions
{
  public bool? ignoreCase;
}

public class FileOperationPattern
{
  public string glob;
  public FileOperationPatternKind matches;
  public FileOperationPatternOptions options;
}

public class FileOperationFilter
{
  public string scheme;
  public FileOperationPattern pattern;
}

public class FileOperationRegistrationOptions
{
  public FileOperationFilter[] filters;
}

public class ServerCapabilities
{
  public class WorkspaceFoldersServerCapabilities
  {
    public bool? supported;
    public EitherType<string, bool> changeNotifications;
  }

  public class ServerCapabilitiesWorkspaceFileOperations
  {
    public FileOperationRegistrationOptions didCreate;
    public FileOperationRegistrationOptions willCreate;
    public FileOperationRegistrationOptions didRename;
    public FileOperationRegistrationOptions willRename;
    public FileOperationRegistrationOptions didDelete;
    public FileOperationRegistrationOptions willDelete;
  }

  public class ServerCapabilitiesWorkspace
  {
    /**
     * The server supports workspace folder.
     *
     * @since 3.6.0
     */
    public WorkspaceFoldersServerCapabilities workspaceFolders;

    /**
     * The server is interested in file notifications/requests.
     *
     * @since 3.16.0
     */
    public ServerCapabilitiesWorkspaceFileOperations fileOperations;
  }

  /**
   * Defines how text documents are synced. Is either a detailed structure
   * defining each notification or for backwards compatibility the
   * TextDocumentSyncKind number. If omitted it defaults to
   * `TextDocumentSyncKind.None`.
   */
  public EitherType<TextDocumentSyncOptions, TextDocumentSyncKind> textDocumentSync;

  /**
   * The server provides completion support.
   */
  public CompletionOptions completionProvider;

  /**
   * The server provides hover support.
   */
  public EitherType<bool, HoverOptions> hoverProvider;

  /**
   * The server provides signature help support.
   */
  public SignatureHelpOptions signatureHelpProvider;

  /**
   * The server provides go to declaration support.
   *
   * @since 3.14.0
   */
  public EitherType<bool, DeclarationOptions, DeclarationRegistrationOptions> declarationProvider;

  /**
   * The server provides goto definition support.
   */
  public EitherType<bool, DefinitionOptions, DefinitionRegistrationOptions> definitionProvider;

  /**
   * The server provides goto type definition support.
   *
   * @since 3.6.0
   */
  public EitherType<bool, TypeDefinitionOptions, TypeDefinitionRegistrationOptions> typeDefinitionProvider;

  /**
   * The server provides goto implementation support.
   *
   * @since 3.6.0
   */
  public EitherType<bool, ImplementationOptions, ImplementationRegistrationOptions> implementationProvider;

  /**
   * The server provides find references support.
   */
  public EitherType<bool, ReferenceOptions> referencesProvider;

  /**
   * The server provides document highlight support.
   */
  public EitherType<bool, DocumentHighlightOptions> documentHighlightProvider;

  /**
   * The server provides document symbol support.
   */
  public EitherType<bool, DocumentSymbolOptions> documentSymbolProvider;

  /**
   * The server provides code actions. The `CodeActionOptions` return type is
   * only valid if the client signals code action literal support via the
   * property `textDocument.codeAction.codeActionLiteralSupport`.
   */
  public EitherType<bool, CodeActionOptions> codeActionProvider;

  /**
   * The server provides code lens.
   */
  public CodeLensOptions codeLensProvider;

  /**
   * The server provides document link support.
   */
  public DocumentLinkOptions documentLinkProvider;

  /**
   * The server provides color provider support.
   *
   * @since 3.6.0
   */
  public EitherType<bool, DocumentColorOptions, DocumentColorRegistrationOptions> colorProvider;

  /**
   * The server provides document formatting.
   */
  public EitherType<bool, DocumentFormattingOptions> documentFormattingProvider;

  /**
   * The server provides document range formatting.
   */
  public EitherType<bool, DocumentRangeFormattingOptions> documentRangeFormattingProvider;

  /**
   * The server provides document formatting on typing.
   */
  public DocumentOnTypeFormattingOptions documentOnTypeFormattingProvider;

  /**
   * The server provides rename support. RenameOptions may only be
   * specified if the client states that it supports
   * `prepareSupport` in its initial `initialize` request.
   */
  public EitherType<bool, RenameOptions> renameProvider;

  /**
   * The server provides folding provider support.
   *
   * @since 3.10.0
   */
  public EitherType<bool, FoldingRangeOptions, FoldingRangeRegistrationOptions> foldingRangeProvider;

  /**
   * The server provides execute command support.
   */
  public ExecuteCommandOptions executeCommandProvider;

  /**
   * The server provides selection range support.
   *
   * @since 3.15.0
   */
  public EitherType<bool, SelectionRangeOptions, SelectionRangeRegistrationOptions> selectionRangeProvider;

  /**
   * The server provides linked editing range support.
   *
   * @since 3.16.0
   */
  public EitherType<bool, LinkedEditingRangeOptions, LinkedEditingRangeRegistrationOptions> linkedEditingRangeProvider;

  /**
   * The server provides call hierarchy support.
   *
   * @since 3.16.0
   */
  public EitherType<bool, CallHierarchyOptions, CallHierarchyRegistrationOptions> callHierarchyProvider;

  /**
   * The server provides semantic tokens support.
   *
   * @since 3.16.0
   */
  public EitherType<SemanticTokensOptions, SemanticTokensRegistrationOptions> semanticTokensProvider;

  /**
   * Whether server provides moniker support.
   *
   * @since 3.16.0
   */
  public EitherType<bool, MonikerOptions, MonikerRegistrationOptions> monikerProvider;

  /**
   * The server provides workspace symbol support.
   */
  public EitherType<bool, WorkspaceSymbolOptions> workspaceSymbolProvider;

  /**
   * Workspace specific server capabilities
   */
  public ServerCapabilitiesWorkspace workspace;

  //NOTE: we are going to support DiagnosticOptions only
  //public EitherType<DiagnosticOptions, DiagnosticRegistrationOptions> diagnosticProvider;
  public DiagnosticOptions diagnosticProvider;

  /**
   * Experimental server capabilities.
   */
  public object experimental;
}

public class InitializeResult
{
  public class InitializeResultsServerInfo
  {
    public string name;
    public string version;
  }

  public ServerCapabilities capabilities;

  /**
   * Information about the server.
   *
   * @since 3.15.0
   */
  public InitializeResultsServerInfo serverInfo;
}

public class InitializeError
{
  public bool retry;
}

public class TextDocumentItem
{
  /**
   * The text document's URI.
   */
  public Uri uri;

  /**
   * The text document's language identifier.
   */
  public string languageId;

  /**
   * The version number of this document (it will increase after each
   * change, including undo/redo).
   */
  public int version;

  /**
   * The content of the opened text document.
   */
  public string text;
}

public class DidOpenTextDocumentParams
{
  /**
   * The document that was opened.
   */
  public TextDocumentItem textDocument;
}

public class TextDocumentIdentifier
{
  /**
   * The text document's URI.
   */
  public Uri uri;
}

public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
{
  /**
   * * The version number of this document.
   * *
   * * The version number of a document will increase after each change,
   * * including undo/redo. The number doesn't need to be consecutive.
   */
  public int version;
}

public class Position
{
  /**
   * Line position in a document (zero-based).
   */
  public uint line;

  /**
   * * Character offset on a line in a document (zero-based). Assuming that
   * * the line is represented as a string, the `character` value represents
   * * the gap between the `character` and `character + 1`.
   * *
   * * If the character value is greater than the line length it defaults back
   * * to the line length.
   */
  public uint character;

  public static implicit operator Position(SourcePos pos)
  {
    //NOTE: in LSP line is 0 based
    return new Position() { 
      line = (uint)(pos.line-1), 
      character = (uint)pos.column 
    }; 
  }
}

public class Range
{
  /**
   * The range's start position.
   */
  public Position start;

  /**
   * The range's end position.
   */
  public Position end;

  public static implicit operator Range(SourceRange range)
  {
    return new Range() { start = range.start, end = range.end }; 
  }
}

public class TextDocumentContentChangeEvent
{
  /**
   * The range of the document that changed.
   */
  public Range range;

  /**
   * The optional length of the range that got replaced.
   *
   * @deprecated use range instead.
   */
  public uint? rangeLength;

  /**
   * The new text for the provided range.
   * or
   * The new text of the whole document.
   */
  public string text;
}

public class DidChangeTextDocumentParams
{
  /**
   * The document that did change. The version number points
   * to the version after all provided content changes have
   * been applied.
   */
  public VersionedTextDocumentIdentifier textDocument;

  /**
   * * The actual content changes. The content changes describe single state
   * * changes to the document. So if there are two content changes c1 (at
   * * array index 0) and c2 (at array index 1) for a document in state S then
   * * c1 moves the document from S to S' and c2 from S' to S''. So c1 is
   * * computed on the state S and c2 is computed on the state S'.
   * *
   * * To mirror the content of a document using change events use the following
   * * approach:
   * * - start with the same initial content
   * * - apply the 'textDocument/didChange' notifications in the order you
   * *   receive them.
   * * - apply the `TextDocumentContentChangeEvent`s in a single notification
   * *   in the order you receive them.
   */
  public TextDocumentContentChangeEvent[] contentChanges;
}

public enum TextDocumentSaveReason
{
  Manual = 1,
  AfterDelay = 2,
  FocusOut = 3
}

public class WillSaveTextDocumentParams
{
  /**
   * The document that will be saved.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The 'TextDocumentSaveReason'.
   */
  public TextDocumentSaveReason reason;
}

public class DidSaveTextDocumentParams
{
  /**
   * The document that was saved.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * Optional the content when saved. Depends on the includeText value
   * when the save notification was requested.
   */
  public string text;
}

public class DidCloseTextDocumentParams
{
  /**
   * The document that was closed.
   */
  public TextDocumentIdentifier textDocument;
}

public class TextDocumentPositionParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The position inside the text document.
   */
  public Position position;
}

public enum CompletionTriggerKind
{
  Invoked = 1,
  TriggerCharacter = 2,
  TriggerForIncompleteCompletions = 3
}

public class CompletionContext
{
  /**
   * How the completion was triggered.
   */
  public CompletionTriggerKind triggerKind;

  /**
   * * The trigger character (a single character) that has trigger code
   * * complete. Is undefined if
   * * `triggerKind !== CompletionTriggerKind.TriggerCharacter`
   */
  public string triggerCharacter;
}

public class CompletionParams : TextDocumentPositionParams
{
  /**
   * * The completion context. This is only available if the client specifies
   * * to send this using the client capability
   * * `completion.contextSupport === true`
   */
  public CompletionContext context;

  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
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
  TypeParameter = 25
}

public enum CompletionItemTag
{
  Deprecated = 1
}

public class MarkupContent
{
  /**
   * The type of the Markup
   * PlainText: 'plaintext' = 'plaintext';
   * Markdown: 'markdown' = 'markdown';
   * type MarkupKind = 'plaintext' | 'markdown';
   */
  public string kind;

  /**
   * The content itself
   */
  public string value;
}

public enum InsertTextFormat
{
  Plaintext = 1,
  Snippet = 2
}

public enum InsertTextMode
{
  AsIs = 1,
  AdjustIndentation = 2
}

public class TextEdit
{
  /**
   * The range of the text document to be manipulated. To insert
   * text into a document create a range where start === end.
   */
  public Range range;

  /**
   * The string to be inserted. For delete operations use an
   * empty string.
   */
  public string newText;
}

public class InsertReplaceEdit
{
  /**
   * The string to be inserted.
   */
  public string newText;

  /**
   * The range if the insert is requested
   */
  public Range insert;

  /**
   * The range if the replace is requested.
   */
  public Range replace;
}

public class Command
{
  /**
   * Title of the command, like `save`.
   */
  public string title;

  /**
   * The identifier of the actual command handler.
   */
  public string command;

  /**
   * Arguments that the command handler should be
   * invoked with.
   */
  public object[] arguments;
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
  public string label;

  /**
   * The kind of this completion item. Based of the kind
   * an icon is chosen by the editor. The standardized set
   * of available values is defined in `CompletionItemKind`.
   */
  public CompletionItemKind? kind;

  /**
   * Tags for this completion item.
   *
   * @since 3.15.0
   */
  public CompletionItemTag[] tags;

  /**
   * A human-readable string with additional information
   * about this item, like type or symbol information.
   */
  public string detail;

  /**
   * A human-readable string that represents a doc-comment.
   */
  public EitherType<string, MarkupContent> documentation;

  /**
   * Indicates if this item is deprecated.
   *
   * @deprecated Use `tags` instead if supported.
   */
  public bool? deprecated;

  /**
   * Select this item when showing.
   *
   * *Note* that only one completion item can be selected and that the
   * tool / client decides which item that is. The rule is that the *first*
   * item of those that match best is selected.
   */
  public bool? preselect;

  /**
   * A string that should be used when comparing this item
   * with other items. When `falsy` the label is used.
   */
  public string sortText;

  /**
   * A string that should be used when filtering a set of
   * completion items. When `falsy` the label is used.
   */
  public string filterText;

  /**
   * * A string that should be inserted into a document when selecting
   * * this completion. When `falsy` the label is used.
   * *
   * * The `insertText` is subject to interpretation by the client side.
   * * Some tools might not take the string literally. For example
   * * VS Code when code complete is requested in this example
   * * `con
   * <cursor position>
   *   ` and a completion item with an `insertText` of
   *   * `console` is provided it will only insert `sole`. Therefore it is
   *   * recommended to use `textEdit` instead since it avoids additional client
   *   * side interpretation.
   */
  public string insertText;

  /**
   * The format of the insert text. The format applies to both the
   * `insertText` property and the `newText` property of a provided
   * `textEdit`. If omitted defaults to `InsertTextFormat.PlainText`.
   *
   * Please note that the insertTextFormat doesn't apply to
   * `additionalTextEdits`.
   */
  public InsertTextFormat insertTextFormat;

  /**
   * How whitespace and indentation is handled during completion
   * item insertion. If not provided the client's default value depends on
   * the `textDocument.completion.insertTextMode` client capability.
   *
   * @since 3.16.0
   * @since 3.17.0 - support for `textDocument.completion.insertTextMode`
   */
  public InsertTextMode? insertTextMode;

  /**
   * * An edit which is applied to a document when selecting this completion.
   * * When an edit is provided the value of `insertText` is ignored.
   * *
   * * *Note:* The range of the edit must be a single line range and it must
   * * contain the position at which completion has been requested.
   * *
   * * Most editors support two different operations when accepting a completion
   * * item. One is to insert a completion text and the other is to replace an
   * * existing text with a completion text. Since this can usually not be
   * * predetermined by a server it can report both ranges. Clients need to
   * * signal support for `InsertReplaceEdits` via the
   * * `textDocument.completion.insertReplaceSupport` client capability
   * * property.
   * *
   * * *Note 1:* The text edit's range as well as both ranges from an insert
   * * replace edit must be a [single line] and they must contain the position
   * * at which completion has been requested.
   * * *Note 2:* If an `InsertReplaceEdit` is returned the edit's insert range
   * * must be a prefix of the edit's replace range, that means it must be
   * * contained and starting at the same position.
   * *
   * * @since 3.16.0 additional type `InsertReplaceEdit`
   */
  public EitherType<TextEdit, InsertReplaceEdit>? textEdit;

  /**
   * * An optional array of additional text edits that are applied when
   * * selecting this completion. Edits must not overlap (including the same
   * * insert position) with the main edit nor with themselves.
   * *
   * * Additional text edits should be used to change text unrelated to the
   * * current cursor position (for example adding an import statement at the
   * * top of the file if the completion item will insert an unqualified type).
   */
  public TextEdit[] additionalTextEdits;

  /**
   * * An optional set of characters that when pressed while this completion is
   * * active will accept it first and then type that character. *Note* that all
   * * commit characters should have `length=1` and that superfluous characters
   * * will be ignored.
   */
  public string[] commitCharacters;

  /**
   * * An optional command that is executed *after* inserting this completion.
   * * *Note* that additional modifications to the current document should be
   * * described with the additionalTextEdits-property.
   */
  public Command command;

  /**
   * A data entry field that is preserved on a completion item between
   * a completion and a completion resolve request.
   */
  public object data;
}

public class ReferenceContext
{
  /**
   * Include the declaration of the current symbol.
   */
  public bool includeDeclaration;
}

public class ReferenceParams : TextDocumentPositionParams
{
  public ReferenceContext context;

  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class DocumentSymbolParams : WorkDoneProgressParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class DocumentColorParams : WorkDoneProgressParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class Color
{
  /**
   * The red component of this color in the range [0-1].
   */
  public float red;

  /**
   * The green component of this color in the range [0-1].
   */
  public float green;

  /**
   * The blue component of this color in the range [0-1].
   */
  public float blue;

  /**
   * The alpha component of this color in the range [0-1].
   */
  public float alpha;
}

public class ColorPresentationParams : WorkDoneProgressParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The color information to request presentations for.
   */
  public Color color;

  /**
   * The range where the color would be inserted. Serves as a context.
   */
  public Range range;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class FormattingOptions
{
  /**
   * Size of a tab in spaces.
   */
  public uint tabSize;

  /**
   * Prefer spaces over tabs.
   */
  public bool insertSpaces;

  /**
   * Trim trailing whitespace on a line.
   *
   * @since 3.15.0
   */
  public bool? trimTrailingWhitespace;

  /**
   * Insert a newline character at the end of the file if one does not exist.
   *
   * @since 3.15.0
   */
  public bool? insertFinalNewline;

  /**
   * Trim all newlines after the final newline at the end of the file.
   *
   * @since 3.15.0
   */
  public bool? trimFinalNewlines;

  /**
  * Signature for further properties.
  * [key: string]: boolean | integer | string;
  */
  //public object properties; //TODO: ...
}

public class DocumentFormattingParams : WorkDoneProgressParams
{
  /**
   * The document to format.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The format options.
   */
  public FormattingOptions options;
}

public class DocumentRangeFormattingParams : WorkDoneProgressParams
{
  /**
   * The document to format.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The range to format
   */
  public Range range;

  /**
   * The format options
   */
  public FormattingOptions options;
}

public class DocumentOnTypeFormattingParams : TextDocumentPositionParams
{
  /**
   * The character that has been typed.
   */
  public string ch;

  /**
   * The format options.
   */
  public FormattingOptions options;
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
  public string href;
}

public enum DiagnosticTag
{
  Unnecessary = 1,
  Deprecated = 2
}

public class Location
{
  public Uri uri;
  public Range range;
}

public class DiagnosticRelatedInformation
{
  /**
   * The location of this related diagnostic information.
   */
  public Location location;

  /**
   * The message of this related diagnostic information.
   */
  public string message;
}

public class Diagnostic
{
  /**
   * The range at which the message applies.
   */
  public Range range;

  /**
   * The diagnostic's severity. Can be omitted. If omitted it is up to the
   * client to interpret diagnostics as error, warning, info or hint.
   */
  public DiagnosticSeverity severity;

  /**
   * The diagnostic's code, which might appear in the user interface.
   */
  public EitherType<string, int> code;

  /**
   * An optional property to describe the error code.
   *
   * @since 3.16.0
   */
  public CodeDescription codeDescription;

  /**
   * A human-readable string describing the source of this
   * diagnostic, e.g. 'typescript' or 'super lint'.
   */
  public string source;

  /**
   * The diagnostic's message.
   */
  public string message;

  /**
   * Additional metadata about the diagnostic.
   *
   * @since 3.15.0
   */
  public DiagnosticTag[] tags;

  /**
   * An array of related diagnostic information, e.g. when symbol-names within
   * a scope collide all definitions can be marked via this property.
   */
  public DiagnosticRelatedInformation[] relatedInformation;

  /**
   * A data entry field that is preserved between a
   * `textDocument/publishDiagnostics` notification and
   * `textDocument/codeAction` request.
   *
   * @since 3.16.0
   */
  public object data;
}

public class CodeActionContext
{
  /**
   * * An array of diagnostics known on the client side overlapping the range
   * * provided to the `textDocument/codeAction` request. They are provided so
   * * that the server knows which errors are currently presented to the user
   * * for the given range. There is no guarantee that these accurately reflect
   * * the error state of the resource. The primary parameter
   * * to compute code actions is the provided range.
   */
  public Diagnostic[] diagnostics;

  /**
   * * Requested kind of actions to return.
   * *
   * * Actions not of this kind are filtered out by the client before being
   * * shown. So servers can omit computing them.
   */
  public CodeActionKind[] only;
}

public class CodeActionParams : WorkDoneProgressParams
{
  /**
   * The document in which the command was invoked.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * The range for which the command was invoked.
   */
  public Range range;

  /**
   * Context carrying additional information.
   */
  public CodeActionContext context;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class CodeLensParams : WorkDoneProgressParams
{
  /**
   * The document to request code lens for.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class CodeLens
{
  /**
   * The range in which this code lens is valid. Should only span a single line.
   */
  public Range range;

  /**
   * The command this code lens represents.
   */
  public Command command;

  /**
   * A data entry field that is preserved on a code lens item between
   * a code lens and a code lens resolve request.
   */
  public object data;
}

public class DocumentLinkParams : WorkDoneProgressParams
{
  /**
   * The document to provide document links for.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class DocumentLink
{
  /**
   * The range this link applies to.
   */
  public Range range;

  /**
   * The uri this link points to. If missing a resolve request is sent later.
   */
  public string target;

  /**
   * * The tooltip text when you hover over this link.
   * *
   * * If a tooltip is provided, is will be displayed in a string that includes
   * * instructions on how to trigger the link, such as `{0} (ctrl + click)`.
   * * The specific instructions vary depending on OS, user settings, and
   * * localization.
   * *
   * * @since 3.15.0
   */
  public string tooltip;

  /**
   * A data entry field that is preserved on a document link between a
   * DocumentLinkRequest and a DocumentLinkResolveRequest.
   */
  public object data;
}

public class RenameParams : TextDocumentPositionParams
{
  /**
   * The new name of the symbol. If the given name is not valid the
   * request must return a [ResponseError](#ResponseError) with an
   * appropriate message set.
   */
  public string newName;

  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;
}

public class FoldingRangeParams : WorkDoneProgressParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class WorkspaceFoldersChangeEvent
{
  /**
   * The array of added workspace folders
   */
  public WorkspaceFolder[] added;

  /**
   * The array of the removed workspace folders
   */
  public WorkspaceFolder[] removed;
}

public class DidChangeWorkspaceFoldersParams
{
  /**
   * The actual workspace folder change event.
   */
  public WorkspaceFoldersChangeEvent @event;
}

public class DidChangeConfigurationParams
{
  /**
   * The actual changed settings
   */
  public object settings;
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
  public string uri;

  /**
   * The change type.
   */
  public FileChangeType type;
}

public class DidChangeWatchedFilesParams
{
  /**
   * The actual file events.
   */
  public FileEvent[] changes;
}

public class WorkspaceSymbolParams : WorkDoneProgressParams
{
  /**
   * A query string to filter symbols by. Clients may send an empty
   * string here to request all symbols.
   */
  public string query;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to
   * the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class ExecuteCommandParams : WorkDoneProgressParams
{
  /**
   * The identifier of the actual command handler.
   */
  public string command;

  /**
   * Arguments that the command should be invoked with.
   */
  public object[] arguments;
}

public enum SignatureHelpTriggerKind
{
  Invoked = 1,
  TriggerCharacter = 2,
  ContentChange = 3
}

public class ParameterInformation
{
  /**
   * The label of this parameter information.
   *
   * Either a string or an inclusive start and exclusive end offsets within
   * its containing signature label. (see SignatureInformation.label). The
   * offsets are based on a UTF-16 string representation as `Position` and
   * `Range` does.
   *
   * *Note*: a label of type string should be a substring of its containing
   * signature label. Its intended use case is to highlight the parameter
   * label part in the `SignatureInformation.label`.
   */
  public EitherType<string, List<uint>> label;

  /**
   * The human-readable doc-comment of this parameter. Will be shown
   * in the UI but can be omitted.
   */
  public EitherType<string, MarkupContent> documentation;
}

public class SignatureInformation
{
  /**
   * The label of this signature. Will be shown in
   * the UI.
   */
  public string label;

  /**
   * The human-readable doc-comment of this signature. Will be shown
   * in the UI but can be omitted.
   */
  public EitherType<string, MarkupContent> documentation;

  /**
   * The parameters of this signature.
   */
  public ParameterInformation[] parameters;

  /**
   * The index of the active parameter.
   *
   * If provided, this is used in place of `SignatureHelp.activeParameter`.
   *
   * @since 3.16.0
   */
  public uint? activeParameter;
}

public class SignatureHelp
{
  /**
   * One or more signatures. If no signatures are available the signature help
   * request should return `null`.
   */
  public SignatureInformation[] signatures;

  /**
   * The active signature. If omitted or the value lies outside the
   * range of `signatures` the value defaults to zero or is ignore if
   * the `SignatureHelp` as no signatures.
   *
   * Whenever possible implementors should make an active decision about
   * the active signature and shouldn't rely on a default value.
   *
   * In future version of the protocol this property might become
   * mandatory to better express this.
   */
  public uint? activeSignature;

  /**
   * The active parameter of the active signature. If omitted or the value
   * lies outside the range of `signatures[activeSignature].parameters`
   * defaults to 0 if the active signature has parameters. If
   * the active signature has no parameters it is ignored.
   * In future version of the protocol this property might become
   * mandatory to better express the active parameter if the
   * active signature does have any.
   */
  public uint? activeParameter;
}

public class SignatureHelpContext
{
  /**
   * Action that caused signature help to be triggered.
   */
  public SignatureHelpTriggerKind triggerKind;

  /**
   * Character that caused signature help to be triggered.
   *
   * This is undefined when triggerKind !==
   * SignatureHelpTriggerKind.TriggerCharacter
   */
  public string triggerCharacter;

  /**
   * `true` if signature help was already showing when it was triggered.
   *
   * Retriggers occur when the signature help is already active and can be
   * caused by actions such as typing a trigger character, a cursor move, or
   * document content changes.
   */
  public bool isRetrigger;

  /**
   * The currently active `SignatureHelp`.
   *
   * The `activeSignatureHelp` has its `SignatureHelp.activeSignature` field
   * updated based on the user navigating through available signatures.
   */
  public SignatureHelp activeSignatureHelp;
}

public class SignatureHelpParams : TextDocumentPositionParams
{
  /**
   * The signature help context. This is only available if the client
   * specifies to send this using the client capability
   * `textDocument.signatureHelp.contextSupport === true`
   *
   * @since 3.15.0
   */
  public SignatureHelpContext context;

  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;
}

public class DeclarationParams : TextDocumentPositionParams
{
  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class DefinitionParams : TextDocumentPositionParams
{
  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class TypeDefinitionParams : TextDocumentPositionParams
{
  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g. streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class ImplementationParams : TextDocumentPositionParams
{
  /**
   * An optional token that a server can use to report work done progress.
   */
  public EitherType<string, int> workDoneToken;

  /**
   * An optional token that a server can use to report partial results (e.g.
   * streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class Hover
{
  /**
   * The hover's content
   */
  public MarkupContent contents;

  /**
   * An optional range is a range inside a text document
   * that is used to visualize a hover, e.g. by changing the background color.
   */
  public Range range;
}

public class SemanticTokensParams : WorkDoneProgressParams
{
  /**
   * The text document.
   */
  public TextDocumentIdentifier textDocument;

  /**
   * An optional token that a server can use to report partial results (e.g.
   * streaming) to the client.
   */
  public EitherType<int, string> partialResultToken;
}

public class DiagnosticOptions : WorkDoneProgressParams
{
  /**
	 * An optional identifier under which the diagnostics are
	 * managed by the client.
	 */
	public string identifier;

	/**
	 * Whether the language has inter file dependencies meaning that
	 * editing code in one file can result in a different diagnostic
	 * set in another file. Inter file dependencies are common for
	 * most programming languages and typically uncommon for linters.
	 */
	public bool interFileDependencies;

	/**
	 * The server provides support for workspace diagnostics as well.
	 */
	public bool workspaceDiagnostics;
}

public class PublishDiagnosticParams
{
  /**
	 * The URI for which diagnostic information is reported.
	 */
	public Uri uri;

	/**
	 * Optional the version number of the document the diagnostics are published
	 * for.
	 *
	 * @since 3.15.0
	 */
	public int? version;

	/**
	 * An array of diagnostic information items.
	 */
	public List<Diagnostic> diagnostics;
}

public class SemanticTokens
{
  /**
   * An optional result id. If provided and clients support delta updating
   * the client will include the result id in the next semantic token request.
   * A server can then instead of computing all semantic tokens again simply
   * send a delta.
   */
  public string resultId;

  /**
  * The actual tokens.
   */
  public uint[] data;
}

public class SemanticTokenTypes
{
  public static string @namespace = "namespace";

  /**
   * Represents a generic type. Acts as a fallback for types which
   * can't be mapped to a specific type like class or enum.
   */
  public static string type = "type";

  public static string @class = "class";
  public static string @enum = "enum";
  public static string @interface = "interface";
  public static string @struct = "struct";
  public static string typeParameter = "typeParameter";
  public static string parameter = "parameter";
  public static string variable = "variable";
  public static string property = "property";
  public static string enumMember = "enumMember";
  public static string @event = "event";
  public static string function = "function";
  public static string method = "method";
  public static string macro = "macro";
  public static string keyword = "keyword";
  public static string modifier = "modifier";
  public static string comment = "comment";
  public static string @string = "string";
  public static string number = "number";
  public static string regexp = "regexp";
  public static string @operator = "operator";
}

public class SemanticTokenModifiers
{
  public static string declaration = "declaration";
  public static string definition = "definition";
  public static string @readonly = "readonly";
  public static string @static = "static";
  public static string deprecated = "deprecated";
  public static string @abstract = "abstract";
  public static string async = "async";
  public static string modification = "modification";
  public static string documentation = "documentation";
  public static string defaultLibrary = "defaultLibrary";
}


public class CancelParams
{
  /**
   * The request id to cancel.
   */
  public EitherType<Int32, Int64, string> id;
}

}

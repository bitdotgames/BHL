using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bhlsp
{
  public class SumTypeJsonConverter: JsonConverter
  {
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
    
    public override bool CanRead => false;
    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
    }
  }
  
  public interface ISumType
  {
    object Value { get; }
  }
  
  [JsonConverter(typeof(SumTypeJsonConverter))]
  public struct SumType<T1, T2> : IEquatable<SumType<T1, T2>>, ISumType
  {
    public SumType(T1 val)
    {
      this.Value = (object)val;
    }

    public SumType(T2 val)
    {
      this.Value = (object)val;
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
  
  [JsonConverter(typeof(SumTypeJsonConverter))]
  public struct SumType<T1, T2, T3> : IEquatable<SumType<T1, T2, T3>>, ISumType
  {
    public SumType(T1 val)
    {
      this.Value = (object)val;
    }

    public SumType(T2 val)
    {
      this.Value = (object)val;
    }

    public SumType(T3 val)
    {
      this.Value = (object)val;
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
	  /**
		 * The client is supposed to include the content on save.
		 */
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
    /**
	   * The characters that trigger signature help
	   * automatically.
	   */
    public string[] triggerCharacters { get; set; }
    
    /**
	   * List of characters that re-trigger signature help.
	   *
	   * These trigger characters are only active when signature help is already
	   * showing. All trigger characters are also counted as re-trigger
	   * characters.
	   *
	   * @since 3.15.0
	   */
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
    public bool? openClose { get; set; }
    
	  /**
		 * Change notifications are sent to the server. See
		 * TextDocumentSyncKind.None, TextDocumentSyncKind.Full and
		 * TextDocumentSyncKind.Incremental. If omitted it defaults to
		 * TextDocumentSyncKind.None.
		 */
	  public TextDocumentSyncKind? change { get; set; }
    
	  /**
		 * If present will save notifications are sent to the server. If omitted
		 * the notification should not be sent.
		 */
	  public bool? willSave { get; set; }
	  
	  /**
		 * If present will save wait until requests are sent to the server. If
		 * omitted the request should not be sent.
		 */
    public bool? willSaveWaitUntil { get; set; }
    
	  /**
		 * If present save notifications are sent to the server. If omitted the
		 * notification should not be sent.
		 */
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
		  public bool? labelOffsetSupport { get; set; }
	  }
	  
	  public class SignatureHelpClientCapabilitiesSignatureInformation
	  {
		  /**
		   * Client supports the follow content formats for the documentation
		   * property. The order describes the preferred format of the client.
		   *
		   * MarkupKind = 'plaintext' | 'markdown';
		   */
		  public string[] documentationFormat { get; set; }

		  /**
		   * Client capabilities specific to parameter information.
		   */
		  public SignatureHelpClientCapabilitiesParameterInformation parameterInformation { get; set; }

		  /**
		   * The client supports the `activeParameter` property on
		   * `SignatureInformation` literal.
		   *
		   * @since 3.16.0
		   */
		  public bool? activeParameterSupport { get; set; }
	  }
	  
	  /**
		 * Whether signature help supports dynamic registration.
		 */
	  public bool? dynamicRegistration { get; set; }

	  /**
		 * The client supports the following `SignatureInformation`
		 * specific properties.
		 */
	  public SignatureHelpClientCapabilitiesSignatureInformation signatureInformation { get; set; }

	  /**
	   * The client supports to send additional context information for a
	   * `textDocument/signatureHelp` request. A client that opts into
	   * contextSupport will also support the `retriggerCharacters` on
	   * `SignatureHelpOptions`.
	   *
	   * @since 3.15.0
	   */
	  public bool? contextSupport { get; set; }
	}
  
  public class TextDocumentSyncClientCapabilities
  {
	  /**
		 * Whether text document synchronization supports dynamic registration.
		 */
	  public bool? dynamicRegistration { get; set; }

	  /**
		 * The client supports sending will save notifications.
		 */
	  public bool? willSave { get; set; }

	  /**
		 * The client supports sending a will save request and
		 * waits for a response providing text edits which will
		 * be applied to the document before it is saved.
		 */
	  public bool? willSaveWaitUntil { get; set; }

	  /**
		 * The client supports did save notifications.
		 */
	  public bool? didSave { get; set; }
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
  
  public class TextDocumentClientCapabilities
  {
	  public TextDocumentSyncClientCapabilities synchronization { get; set; }
	  
	  /**
		 * Capabilities specific to the `textDocument/signatureHelp` request.
		 */
	  public SignatureHelpClientCapabilities signatureHelp { get; set; }
	  
	  /**
		 * Capabilities specific to the `textDocument/declaration` request.
		 *
		 * @since 3.14.0
		 */
	  public DeclarationClientCapabilities declaration { get; set; }
	  
	  /**
		 * Capabilities specific to the `textDocument/definition` request.
		 */
	  public DefinitionClientCapabilities definition { get; set; }
	  
	  /**
		 * Capabilities specific to the `textDocument/typeDefinition` request.
		 *
		 * @since 3.6.0
		 */
	  public TypeDefinitionClientCapabilities typeDefinition { get; set; }
	  
	  /**
		 * Capabilities specific to the `textDocument/implementation` request.
		 *
		 * @since 3.6.0
		 */
	  public ImplementationClientCapabilities implementation { get; set; }
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
      public string name { get; set; }
      public string version { get; set; }
    }
    
    /**
		 * The process Id of the parent process that started the server. Is null if
		 * the process has not been started by another process. If the parent
		 * process is not alive then the server should exit (see exit notification)
		 * its process.
		 */
    public int? processId { get; set; }
    
    /**
		 * Information about the client
		 *
		 * @since 3.15.0
		 */
    public InitializeParamsClientInfo clientInfo { get; set; }
    
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
    public string locale { get; set; }
    
    /**
		 * The rootPath of the workspace. Is null
		 * if no folder is open.
		 *
		 * @deprecated in favour of `rootUri`.
		 */
    public string rootPath { get; set; }
    
    /**
		 * The rootUri of the workspace. Is null if no
		 * folder is open. If both `rootPath` and `rootUri` are set
		 * `rootUri` wins.
		 *
		 * @deprecated in favour of `workspaceFolders`
		 */
    public Uri rootUri { get; set; }
    
    /**
		 * User provided initialization options.
		 */
    public object initializationOptions { get; set; }
    
    /**
		 * The capabilities provided by the client (editor or tool)
		 */
    public ClientCapabilities capabilities { get; set; }
    
    /**
		 * The initial trace setting. If omitted trace is disabled ('off').
		 */
    public string trace { get; set; }
    
    /**
		 * The workspace folders configured in the client when the server starts.
		 * This property is only available if the client supports workspace folders.
		 * It can be `null` if the client supports workspace folders but none are
		 * configured.
		 *
		 * @since 3.6.0
		 */
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
    public Uri uri { get; set; }
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
	  public SumType<string, List<uint>> label { get; set; }

	  /**
		 * The human-readable doc-comment of this parameter. Will be shown
		 * in the UI but can be omitted.
		 */
	  public SumType<string, MarkupContent> documentation { get; set; }
  }
  
  public class SignatureInformation
  {
	  /**
		 * The label of this signature. Will be shown in
		 * the UI.
		 */
	  public string label { get; set; }

	  /**
		 * The human-readable doc-comment of this signature. Will be shown
		 * in the UI but can be omitted.
		 */
	  public SumType<string, MarkupContent> documentation { get; set; }

	  /**
		 * The parameters of this signature.
		 */
	  public ParameterInformation[] parameters { get; set; }

	  /**
		 * The index of the active parameter.
		 *
		 * If provided, this is used in place of `SignatureHelp.activeParameter`.
		 *
		 * @since 3.16.0
		 */
	  public uint? activeParameter { get; set; }
  }
  
  public class SignatureHelp
  {
    /**
		 * One or more signatures. If no signatures are available the signature help
		 * request should return `null`.
		 */
    public SignatureInformation[] signatures { get; set; }

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
    public uint? activeSignature { get; set; }

    /**
		 * The active parameter of the active signature. If omitted or the value
		 * lies outside the range of `signatures[activeSignature].parameters`
		 * defaults to 0 if the active signature has parameters. If
		 * the active signature has no parameters it is ignored.
		 * In future version of the protocol this property might become
		 * mandatory to better express the active parameter if the
		 * active signature does have any.
		 */
    public uint? activeParameter { get; set; }
  }
  
  public class SignatureHelpContext
  {
    /**
	   * Action that caused signature help to be triggered.
	   */
    public SignatureHelpTriggerKind triggerKind { get; set; }

    /**
	   * Character that caused signature help to be triggered.
	   *
	   * This is undefined when triggerKind !==
	   * SignatureHelpTriggerKind.TriggerCharacter
	   */
    public string triggerCharacter { get; set; }

    /**
	   * `true` if signature help was already showing when it was triggered.
	   *
	   * Retriggers occur when the signature help is already active and can be
	   * caused by actions such as typing a trigger character, a cursor move, or
	   * document content changes.
	   */
    public bool isRetrigger { get; set; }

    /**
	   * The currently active `SignatureHelp`.
	   *
	   * The `activeSignatureHelp` has its `SignatureHelp.activeSignature` field
	   * updated based on the user navigating through available signatures.
	   */
    public SignatureHelp activeSignatureHelp { get; set; }
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
    public SignatureHelpContext context { get; set; }
    
    /**
     * An optional token that a server can use to report work done progress.
     */
    public SumType<string, int> workDoneToken { get; set; }
  }
  
  public class DeclarationParams : TextDocumentPositionParams
  {
	  /**
     * An optional token that a server can use to report work done progress.
     */
	  public SumType<string, int> workDoneToken { get; set; }
	  
	  /**
		 * An optional token that a server can use to report partial results (e.g.
		 * streaming) to the client.
		 */
	  public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class DefinitionParams : TextDocumentPositionParams
  {
	  /**
     * An optional token that a server can use to report work done progress.
     */
	  public SumType<string, int> workDoneToken { get; set; }
	  
	  /**
		 * An optional token that a server can use to report partial results (e.g.
		 * streaming) to the client.
		 */
	  public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class TypeDefinitionParams : TextDocumentPositionParams
  {
	  /**
     * An optional token that a server can use to report work done progress.
     */
	  public SumType<string, int> workDoneToken { get; set; }
	  
	  /**
		 * An optional token that a server can use to report partial results (e.g.
		 * streaming) to the client.
		 */
	  public SumType<int, string> partialResultToken { get; set; }
  }
  
  public class ImplementationParams : TextDocumentPositionParams
  {
	  /**
     * An optional token that a server can use to report work done progress.
     */
	  public SumType<string, int> workDoneToken { get; set; }
	  
	  /**
		 * An optional token that a server can use to report partial results (e.g.
		 * streaming) to the client.
		 */
	  public SumType<int, string> partialResultToken { get; set; }
  }
}
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.13.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from bhlPreprocParser.g by ANTLR 4.13.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419


using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;

/// <summary>
/// This class provides an empty implementation of <see cref="IbhlPreprocParserListener"/>,
/// which can be extended to create a listener which only needs to handle a subset
/// of the available methods.
/// </summary>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.1")]
[System.Diagnostics.DebuggerNonUserCode]
[System.CLSCompliant(false)]
public partial class bhlPreprocParserBaseListener : IbhlPreprocParserListener {
	/// <summary>
	/// Enter a parse tree produced by <see cref="bhlPreprocParser.program"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterProgram([NotNull] bhlPreprocParser.ProgramContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="bhlPreprocParser.program"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitProgram([NotNull] bhlPreprocParser.ProgramContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="bhlPreprocParser.text"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterText([NotNull] bhlPreprocParser.TextContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="bhlPreprocParser.text"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitText([NotNull] bhlPreprocParser.TextContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="bhlPreprocParser.code"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterCode([NotNull] bhlPreprocParser.CodeContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="bhlPreprocParser.code"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitCode([NotNull] bhlPreprocParser.CodeContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>preprocConditional</c>
	/// labeled alternative in <see cref="bhlPreprocParser.directive"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPreprocConditional([NotNull] bhlPreprocParser.PreprocConditionalContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>preprocConditional</c>
	/// labeled alternative in <see cref="bhlPreprocParser.directive"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPreprocConditional([NotNull] bhlPreprocParser.PreprocConditionalContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>preprocConditionalSymbol</c>
	/// labeled alternative in <see cref="bhlPreprocParser.preprocessor_expression"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPreprocConditionalSymbol([NotNull] bhlPreprocParser.PreprocConditionalSymbolContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>preprocConditionalSymbol</c>
	/// labeled alternative in <see cref="bhlPreprocParser.preprocessor_expression"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPreprocConditionalSymbol([NotNull] bhlPreprocParser.PreprocConditionalSymbolContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>preprocNot</c>
	/// labeled alternative in <see cref="bhlPreprocParser.preprocessor_expression"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterPreprocNot([NotNull] bhlPreprocParser.PreprocNotContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>preprocNot</c>
	/// labeled alternative in <see cref="bhlPreprocParser.preprocessor_expression"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitPreprocNot([NotNull] bhlPreprocParser.PreprocNotContext context) { }

	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void EnterEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void ExitEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitTerminal([NotNull] ITerminalNode node) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitErrorNode([NotNull] IErrorNode node) { }
}

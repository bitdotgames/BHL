//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.7.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from bhlPreprocLexer.g by ANTLR 4.7.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

using System;
using System.IO;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using DFA = Antlr4.Runtime.Dfa.DFA;

[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.7.1")]
[System.CLSCompliant(false)]
public partial class bhlPreprocLexer : Lexer {
	protected static DFA[] decisionToDFA;
	protected static PredictionContextCache sharedContextCache = new PredictionContextCache();
	public const int
		SHARP=1, CODE=2, IF=3, ELIF=4, ELSE=5, ENDIF=6, BANG=7, DIRECTIVE_WHITESPACES=8, 
		DIRECTIVE_STRING=9, CONDITIONAL_SYMBOL=10, NEW_LINE=11, DIRECTIVE_COMMENT=12, 
		SLASH=13;
	public const int
		COMMENTS_CHANNEL=2;
	public const int
		DIRECTIVE_MODE=1;
	public static string[] channelNames = {
		"DEFAULT_TOKEN_CHANNEL", "HIDDEN", "COMMENTS_CHANNEL"
	};

	public static string[] modeNames = {
		"DEFAULT_MODE", "DIRECTIVE_MODE"
	};

	public static readonly string[] ruleNames = {
		"SHARP", "COMMENT", "LINE_COMMENT", "SLASH", "CHARACTER_LITERAL", "QUOTE_STRING", 
		"STRING", "CODE", "IF", "ELIF", "ELSE", "ENDIF", "BANG", "DIRECTIVE_WHITESPACES", 
		"DIRECTIVE_STRING", "CONDITIONAL_SYMBOL", "NEW_LINE", "DIRECTIVE_COMMENT", 
		"EscapeSequence", "OctalEscape", "UnicodeEscape", "HexDigit", "StringFragment", 
		"LETTER"
	};


	public bhlPreprocLexer(ICharStream input)
	: this(input, Console.Out, Console.Error) { }

	public bhlPreprocLexer(ICharStream input, TextWriter output, TextWriter errorOutput)
	: base(input, output, errorOutput)
	{
		Interpreter = new LexerATNSimulator(this, _ATN, decisionToDFA, sharedContextCache);
	}

	private static readonly string[] _LiteralNames = {
		null, "'#'", null, "'if'", "'elif'", "'else'", "'endif'", "'!'", null, 
		null, null, null, null, "'/'"
	};
	private static readonly string[] _SymbolicNames = {
		null, "SHARP", "CODE", "IF", "ELIF", "ELSE", "ENDIF", "BANG", "DIRECTIVE_WHITESPACES", 
		"DIRECTIVE_STRING", "CONDITIONAL_SYMBOL", "NEW_LINE", "DIRECTIVE_COMMENT", 
		"SLASH"
	};
	public static readonly IVocabulary DefaultVocabulary = new Vocabulary(_LiteralNames, _SymbolicNames);

	[NotNull]
	public override IVocabulary Vocabulary
	{
		get
		{
			return DefaultVocabulary;
		}
	}

	public override string GrammarFileName { get { return "bhlPreprocLexer.g"; } }

	public override string[] RuleNames { get { return ruleNames; } }

	public override string[] ChannelNames { get { return channelNames; } }

	public override string[] ModeNames { get { return modeNames; } }

	public override string SerializedAtn { get { return new string(_serializedATN); } }

	static bhlPreprocLexer() {
		decisionToDFA = new DFA[_ATN.NumberOfDecisions];
		for (int i = 0; i < _ATN.NumberOfDecisions; i++) {
			decisionToDFA[i] = new DFA(_ATN.GetDecisionState(i), i);
		}
	}
	private static char[] _serializedATN = {
		'\x3', '\x608B', '\xA72A', '\x8133', '\xB9ED', '\x417C', '\x3BE7', '\x7786', 
		'\x5964', '\x2', '\xF', '\xDA', '\b', '\x1', '\b', '\x1', '\x4', '\x2', 
		'\t', '\x2', '\x4', '\x3', '\t', '\x3', '\x4', '\x4', '\t', '\x4', '\x4', 
		'\x5', '\t', '\x5', '\x4', '\x6', '\t', '\x6', '\x4', '\a', '\t', '\a', 
		'\x4', '\b', '\t', '\b', '\x4', '\t', '\t', '\t', '\x4', '\n', '\t', '\n', 
		'\x4', '\v', '\t', '\v', '\x4', '\f', '\t', '\f', '\x4', '\r', '\t', '\r', 
		'\x4', '\xE', '\t', '\xE', '\x4', '\xF', '\t', '\xF', '\x4', '\x10', '\t', 
		'\x10', '\x4', '\x11', '\t', '\x11', '\x4', '\x12', '\t', '\x12', '\x4', 
		'\x13', '\t', '\x13', '\x4', '\x14', '\t', '\x14', '\x4', '\x15', '\t', 
		'\x15', '\x4', '\x16', '\t', '\x16', '\x4', '\x17', '\t', '\x17', '\x4', 
		'\x18', '\t', '\x18', '\x4', '\x19', '\t', '\x19', '\x3', '\x2', '\x3', 
		'\x2', '\x3', '\x2', '\x3', '\x2', '\x3', '\x3', '\x3', '\x3', '\x3', 
		'\x3', '\x3', '\x3', '\a', '\x3', '=', '\n', '\x3', '\f', '\x3', '\xE', 
		'\x3', '@', '\v', '\x3', '\x3', '\x3', '\x3', '\x3', '\x3', '\x3', '\x3', 
		'\x3', '\x3', '\x3', '\x3', '\x4', '\x3', '\x4', '\x3', '\x4', '\x3', 
		'\x4', '\a', '\x4', 'K', '\n', '\x4', '\f', '\x4', '\xE', '\x4', 'N', 
		'\v', '\x4', '\x3', '\x4', '\x3', '\x4', '\x3', '\x5', '\x3', '\x5', '\x3', 
		'\x5', '\x3', '\x5', '\x3', '\x6', '\x3', '\x6', '\x3', '\x6', '\x5', 
		'\x6', 'Y', '\n', '\x6', '\x3', '\x6', '\x3', '\x6', '\x3', '\x6', '\x3', 
		'\x6', '\x3', '\a', '\x3', '\a', '\x3', '\a', '\a', '\a', '\x62', '\n', 
		'\a', '\f', '\a', '\xE', '\a', '\x65', '\v', '\a', '\x3', '\a', '\x3', 
		'\a', '\x3', '\a', '\x3', '\a', '\x3', '\b', '\x3', '\b', '\x3', '\b', 
		'\x3', '\b', '\x3', '\t', '\x6', '\t', 'p', '\n', '\t', '\r', '\t', '\xE', 
		'\t', 'q', '\x3', '\n', '\x3', '\n', '\x3', '\n', '\x3', '\v', '\x3', 
		'\v', '\x3', '\v', '\x3', '\v', '\x3', '\v', '\x3', '\f', '\x3', '\f', 
		'\x3', '\f', '\x3', '\f', '\x3', '\f', '\x3', '\r', '\x3', '\r', '\x3', 
		'\r', '\x3', '\r', '\x3', '\r', '\x3', '\r', '\x3', '\xE', '\x3', '\xE', 
		'\x3', '\xF', '\x6', '\xF', '\x8A', '\n', '\xF', '\r', '\xF', '\xE', '\xF', 
		'\x8B', '\x3', '\xF', '\x3', '\xF', '\x3', '\x10', '\x3', '\x10', '\x3', 
		'\x11', '\x3', '\x11', '\x3', '\x11', '\a', '\x11', '\x95', '\n', '\x11', 
		'\f', '\x11', '\xE', '\x11', '\x98', '\v', '\x11', '\x3', '\x12', '\x5', 
		'\x12', '\x9B', '\n', '\x12', '\x3', '\x12', '\x3', '\x12', '\x3', '\x12', 
		'\x3', '\x12', '\x3', '\x13', '\x3', '\x13', '\x3', '\x13', '\x3', '\x13', 
		'\a', '\x13', '\xA5', '\n', '\x13', '\f', '\x13', '\xE', '\x13', '\xA8', 
		'\v', '\x13', '\x3', '\x13', '\x3', '\x13', '\x3', '\x13', '\x3', '\x13', 
		'\x3', '\x13', '\x3', '\x14', '\x3', '\x14', '\x3', '\x14', '\x3', '\x14', 
		'\x5', '\x14', '\xB3', '\n', '\x14', '\x3', '\x15', '\x3', '\x15', '\x3', 
		'\x15', '\x3', '\x15', '\x3', '\x15', '\x3', '\x15', '\x3', '\x15', '\x3', 
		'\x15', '\x3', '\x15', '\x5', '\x15', '\xBE', '\n', '\x15', '\x3', '\x16', 
		'\x3', '\x16', '\x3', '\x16', '\x3', '\x16', '\x3', '\x16', '\x3', '\x16', 
		'\x3', '\x16', '\x3', '\x17', '\x3', '\x17', '\x3', '\x18', '\x3', '\x18', 
		'\x3', '\x18', '\x3', '\x18', '\a', '\x18', '\xCD', '\n', '\x18', '\f', 
		'\x18', '\xE', '\x18', '\xD0', '\v', '\x18', '\x3', '\x18', '\x3', '\x18', 
		'\x3', '\x19', '\x3', '\x19', '\x3', '\x19', '\x3', '\x19', '\x3', '\x19', 
		'\x5', '\x19', '\xD9', '\n', '\x19', '\x4', '>', '\xA6', '\x2', '\x1A', 
		'\x4', '\x3', '\x6', '\x2', '\b', '\x2', '\n', '\xF', '\f', '\x2', '\xE', 
		'\x2', '\x10', '\x2', '\x12', '\x4', '\x14', '\x5', '\x16', '\x6', '\x18', 
		'\a', '\x1A', '\b', '\x1C', '\t', '\x1E', '\n', ' ', '\v', '\"', '\f', 
		'$', '\r', '&', '\xE', '(', '\x2', '*', '\x2', ',', '\x2', '.', '\x2', 
		'\x30', '\x2', '\x32', '\x2', '\x4', '\x2', '\x3', '\x11', '\x4', '\x2', 
		'\f', '\f', '\xF', '\xF', '\x4', '\x2', ')', ')', '^', '^', '\x5', '\x2', 
		'$', '%', ')', ')', '\x31', '\x31', '\x4', '\x2', '\v', '\v', '\"', '\"', 
		'\x3', '\x2', '\x32', ';', '\n', '\x2', '$', '$', ')', ')', '^', '^', 
		'\x64', '\x64', 'h', 'h', 'p', 'p', 't', 't', 'v', 'v', '\x3', '\x2', 
		'\x32', '\x35', '\x3', '\x2', '\x32', '\x39', '\x5', '\x2', '\x32', ';', 
		'\x43', 'H', '\x63', 'h', '\x4', '\x2', '$', '$', '^', '^', '\x6', '\x2', 
		'&', '&', '\x43', '\\', '\x61', '\x61', '\x63', '|', '\x4', '\x2', '\x2', 
		'\x101', '\xD802', '\xDC01', '\x3', '\x2', '\xD802', '\xDC01', '\x3', 
		'\x2', '\xDC02', '\xE001', '\x3', '\x2', '\xEB', '\xEB', '\x2', '\xE6', 
		'\x2', '\x4', '\x3', '\x2', '\x2', '\x2', '\x2', '\x6', '\x3', '\x2', 
		'\x2', '\x2', '\x2', '\b', '\x3', '\x2', '\x2', '\x2', '\x2', '\n', '\x3', 
		'\x2', '\x2', '\x2', '\x2', '\f', '\x3', '\x2', '\x2', '\x2', '\x2', '\xE', 
		'\x3', '\x2', '\x2', '\x2', '\x2', '\x10', '\x3', '\x2', '\x2', '\x2', 
		'\x2', '\x12', '\x3', '\x2', '\x2', '\x2', '\x3', '\x14', '\x3', '\x2', 
		'\x2', '\x2', '\x3', '\x16', '\x3', '\x2', '\x2', '\x2', '\x3', '\x18', 
		'\x3', '\x2', '\x2', '\x2', '\x3', '\x1A', '\x3', '\x2', '\x2', '\x2', 
		'\x3', '\x1C', '\x3', '\x2', '\x2', '\x2', '\x3', '\x1E', '\x3', '\x2', 
		'\x2', '\x2', '\x3', ' ', '\x3', '\x2', '\x2', '\x2', '\x3', '\"', '\x3', 
		'\x2', '\x2', '\x2', '\x3', '$', '\x3', '\x2', '\x2', '\x2', '\x3', '&', 
		'\x3', '\x2', '\x2', '\x2', '\x4', '\x34', '\x3', '\x2', '\x2', '\x2', 
		'\x6', '\x38', '\x3', '\x2', '\x2', '\x2', '\b', '\x46', '\x3', '\x2', 
		'\x2', '\x2', '\n', 'Q', '\x3', '\x2', '\x2', '\x2', '\f', 'U', '\x3', 
		'\x2', '\x2', '\x2', '\xE', '^', '\x3', '\x2', '\x2', '\x2', '\x10', 'j', 
		'\x3', '\x2', '\x2', '\x2', '\x12', 'o', '\x3', '\x2', '\x2', '\x2', '\x14', 
		's', '\x3', '\x2', '\x2', '\x2', '\x16', 'v', '\x3', '\x2', '\x2', '\x2', 
		'\x18', '{', '\x3', '\x2', '\x2', '\x2', '\x1A', '\x80', '\x3', '\x2', 
		'\x2', '\x2', '\x1C', '\x86', '\x3', '\x2', '\x2', '\x2', '\x1E', '\x89', 
		'\x3', '\x2', '\x2', '\x2', ' ', '\x8F', '\x3', '\x2', '\x2', '\x2', '\"', 
		'\x91', '\x3', '\x2', '\x2', '\x2', '$', '\x9A', '\x3', '\x2', '\x2', 
		'\x2', '&', '\xA0', '\x3', '\x2', '\x2', '\x2', '(', '\xB2', '\x3', '\x2', 
		'\x2', '\x2', '*', '\xBD', '\x3', '\x2', '\x2', '\x2', ',', '\xBF', '\x3', 
		'\x2', '\x2', '\x2', '.', '\xC6', '\x3', '\x2', '\x2', '\x2', '\x30', 
		'\xC8', '\x3', '\x2', '\x2', '\x2', '\x32', '\xD8', '\x3', '\x2', '\x2', 
		'\x2', '\x34', '\x35', '\a', '%', '\x2', '\x2', '\x35', '\x36', '\x3', 
		'\x2', '\x2', '\x2', '\x36', '\x37', '\b', '\x2', '\x2', '\x2', '\x37', 
		'\x5', '\x3', '\x2', '\x2', '\x2', '\x38', '\x39', '\a', '\x31', '\x2', 
		'\x2', '\x39', ':', '\a', ',', '\x2', '\x2', ':', '>', '\x3', '\x2', '\x2', 
		'\x2', ';', '=', '\v', '\x2', '\x2', '\x2', '<', ';', '\x3', '\x2', '\x2', 
		'\x2', '=', '@', '\x3', '\x2', '\x2', '\x2', '>', '?', '\x3', '\x2', '\x2', 
		'\x2', '>', '<', '\x3', '\x2', '\x2', '\x2', '?', '\x41', '\x3', '\x2', 
		'\x2', '\x2', '@', '>', '\x3', '\x2', '\x2', '\x2', '\x41', '\x42', '\a', 
		',', '\x2', '\x2', '\x42', '\x43', '\a', '\x31', '\x2', '\x2', '\x43', 
		'\x44', '\x3', '\x2', '\x2', '\x2', '\x44', '\x45', '\b', '\x3', '\x3', 
		'\x2', '\x45', '\a', '\x3', '\x2', '\x2', '\x2', '\x46', 'G', '\a', '\x31', 
		'\x2', '\x2', 'G', 'H', '\a', '\x31', '\x2', '\x2', 'H', 'L', '\x3', '\x2', 
		'\x2', '\x2', 'I', 'K', '\n', '\x2', '\x2', '\x2', 'J', 'I', '\x3', '\x2', 
		'\x2', '\x2', 'K', 'N', '\x3', '\x2', '\x2', '\x2', 'L', 'J', '\x3', '\x2', 
		'\x2', '\x2', 'L', 'M', '\x3', '\x2', '\x2', '\x2', 'M', 'O', '\x3', '\x2', 
		'\x2', '\x2', 'N', 'L', '\x3', '\x2', '\x2', '\x2', 'O', 'P', '\b', '\x4', 
		'\x3', '\x2', 'P', '\t', '\x3', '\x2', '\x2', '\x2', 'Q', 'R', '\a', '\x31', 
		'\x2', '\x2', 'R', 'S', '\x3', '\x2', '\x2', '\x2', 'S', 'T', '\b', '\x5', 
		'\x3', '\x2', 'T', '\v', '\x3', '\x2', '\x2', '\x2', 'U', 'X', '\a', ')', 
		'\x2', '\x2', 'V', 'Y', '\x5', '(', '\x14', '\x2', 'W', 'Y', '\n', '\x3', 
		'\x2', '\x2', 'X', 'V', '\x3', '\x2', '\x2', '\x2', 'X', 'W', '\x3', '\x2', 
		'\x2', '\x2', 'Y', 'Z', '\x3', '\x2', '\x2', '\x2', 'Z', '[', '\a', ')', 
		'\x2', '\x2', '[', '\\', '\x3', '\x2', '\x2', '\x2', '\\', ']', '\b', 
		'\x6', '\x3', '\x2', ']', '\r', '\x3', '\x2', '\x2', '\x2', '^', '\x63', 
		'\a', ')', '\x2', '\x2', '_', '\x62', '\x5', '(', '\x14', '\x2', '`', 
		'\x62', '\n', '\x3', '\x2', '\x2', '\x61', '_', '\x3', '\x2', '\x2', '\x2', 
		'\x61', '`', '\x3', '\x2', '\x2', '\x2', '\x62', '\x65', '\x3', '\x2', 
		'\x2', '\x2', '\x63', '\x61', '\x3', '\x2', '\x2', '\x2', '\x63', '\x64', 
		'\x3', '\x2', '\x2', '\x2', '\x64', '\x66', '\x3', '\x2', '\x2', '\x2', 
		'\x65', '\x63', '\x3', '\x2', '\x2', '\x2', '\x66', 'g', '\a', ')', '\x2', 
		'\x2', 'g', 'h', '\x3', '\x2', '\x2', '\x2', 'h', 'i', '\b', '\a', '\x3', 
		'\x2', 'i', '\xF', '\x3', '\x2', '\x2', '\x2', 'j', 'k', '\x5', '\x30', 
		'\x18', '\x2', 'k', 'l', '\x3', '\x2', '\x2', '\x2', 'l', 'm', '\b', '\b', 
		'\x3', '\x2', 'm', '\x11', '\x3', '\x2', '\x2', '\x2', 'n', 'p', '\n', 
		'\x4', '\x2', '\x2', 'o', 'n', '\x3', '\x2', '\x2', '\x2', 'p', 'q', '\x3', 
		'\x2', '\x2', '\x2', 'q', 'o', '\x3', '\x2', '\x2', '\x2', 'q', 'r', '\x3', 
		'\x2', '\x2', '\x2', 'r', '\x13', '\x3', '\x2', '\x2', '\x2', 's', 't', 
		'\a', 'k', '\x2', '\x2', 't', 'u', '\a', 'h', '\x2', '\x2', 'u', '\x15', 
		'\x3', '\x2', '\x2', '\x2', 'v', 'w', '\a', 'g', '\x2', '\x2', 'w', 'x', 
		'\a', 'n', '\x2', '\x2', 'x', 'y', '\a', 'k', '\x2', '\x2', 'y', 'z', 
		'\a', 'h', '\x2', '\x2', 'z', '\x17', '\x3', '\x2', '\x2', '\x2', '{', 
		'|', '\a', 'g', '\x2', '\x2', '|', '}', '\a', 'n', '\x2', '\x2', '}', 
		'~', '\a', 'u', '\x2', '\x2', '~', '\x7F', '\a', 'g', '\x2', '\x2', '\x7F', 
		'\x19', '\x3', '\x2', '\x2', '\x2', '\x80', '\x81', '\a', 'g', '\x2', 
		'\x2', '\x81', '\x82', '\a', 'p', '\x2', '\x2', '\x82', '\x83', '\a', 
		'\x66', '\x2', '\x2', '\x83', '\x84', '\a', 'k', '\x2', '\x2', '\x84', 
		'\x85', '\a', 'h', '\x2', '\x2', '\x85', '\x1B', '\x3', '\x2', '\x2', 
		'\x2', '\x86', '\x87', '\a', '#', '\x2', '\x2', '\x87', '\x1D', '\x3', 
		'\x2', '\x2', '\x2', '\x88', '\x8A', '\t', '\x5', '\x2', '\x2', '\x89', 
		'\x88', '\x3', '\x2', '\x2', '\x2', '\x8A', '\x8B', '\x3', '\x2', '\x2', 
		'\x2', '\x8B', '\x89', '\x3', '\x2', '\x2', '\x2', '\x8B', '\x8C', '\x3', 
		'\x2', '\x2', '\x2', '\x8C', '\x8D', '\x3', '\x2', '\x2', '\x2', '\x8D', 
		'\x8E', '\b', '\xF', '\x4', '\x2', '\x8E', '\x1F', '\x3', '\x2', '\x2', 
		'\x2', '\x8F', '\x90', '\x5', '\x30', '\x18', '\x2', '\x90', '!', '\x3', 
		'\x2', '\x2', '\x2', '\x91', '\x96', '\x5', '\x32', '\x19', '\x2', '\x92', 
		'\x95', '\x5', '\x32', '\x19', '\x2', '\x93', '\x95', '\t', '\x6', '\x2', 
		'\x2', '\x94', '\x92', '\x3', '\x2', '\x2', '\x2', '\x94', '\x93', '\x3', 
		'\x2', '\x2', '\x2', '\x95', '\x98', '\x3', '\x2', '\x2', '\x2', '\x96', 
		'\x94', '\x3', '\x2', '\x2', '\x2', '\x96', '\x97', '\x3', '\x2', '\x2', 
		'\x2', '\x97', '#', '\x3', '\x2', '\x2', '\x2', '\x98', '\x96', '\x3', 
		'\x2', '\x2', '\x2', '\x99', '\x9B', '\a', '\xF', '\x2', '\x2', '\x9A', 
		'\x99', '\x3', '\x2', '\x2', '\x2', '\x9A', '\x9B', '\x3', '\x2', '\x2', 
		'\x2', '\x9B', '\x9C', '\x3', '\x2', '\x2', '\x2', '\x9C', '\x9D', '\a', 
		'\f', '\x2', '\x2', '\x9D', '\x9E', '\x3', '\x2', '\x2', '\x2', '\x9E', 
		'\x9F', '\b', '\x12', '\x5', '\x2', '\x9F', '%', '\x3', '\x2', '\x2', 
		'\x2', '\xA0', '\xA1', '\a', '\x31', '\x2', '\x2', '\xA1', '\xA2', '\a', 
		',', '\x2', '\x2', '\xA2', '\xA6', '\x3', '\x2', '\x2', '\x2', '\xA3', 
		'\xA5', '\v', '\x2', '\x2', '\x2', '\xA4', '\xA3', '\x3', '\x2', '\x2', 
		'\x2', '\xA5', '\xA8', '\x3', '\x2', '\x2', '\x2', '\xA6', '\xA7', '\x3', 
		'\x2', '\x2', '\x2', '\xA6', '\xA4', '\x3', '\x2', '\x2', '\x2', '\xA7', 
		'\xA9', '\x3', '\x2', '\x2', '\x2', '\xA8', '\xA6', '\x3', '\x2', '\x2', 
		'\x2', '\xA9', '\xAA', '\a', ',', '\x2', '\x2', '\xAA', '\xAB', '\a', 
		'\x31', '\x2', '\x2', '\xAB', '\xAC', '\x3', '\x2', '\x2', '\x2', '\xAC', 
		'\xAD', '\b', '\x13', '\x6', '\x2', '\xAD', '\'', '\x3', '\x2', '\x2', 
		'\x2', '\xAE', '\xAF', '\a', '^', '\x2', '\x2', '\xAF', '\xB3', '\t', 
		'\a', '\x2', '\x2', '\xB0', '\xB3', '\x5', '*', '\x15', '\x2', '\xB1', 
		'\xB3', '\x5', ',', '\x16', '\x2', '\xB2', '\xAE', '\x3', '\x2', '\x2', 
		'\x2', '\xB2', '\xB0', '\x3', '\x2', '\x2', '\x2', '\xB2', '\xB1', '\x3', 
		'\x2', '\x2', '\x2', '\xB3', ')', '\x3', '\x2', '\x2', '\x2', '\xB4', 
		'\xB5', '\a', '^', '\x2', '\x2', '\xB5', '\xB6', '\t', '\b', '\x2', '\x2', 
		'\xB6', '\xB7', '\t', '\t', '\x2', '\x2', '\xB7', '\xBE', '\t', '\t', 
		'\x2', '\x2', '\xB8', '\xB9', '\a', '^', '\x2', '\x2', '\xB9', '\xBA', 
		'\t', '\t', '\x2', '\x2', '\xBA', '\xBE', '\t', '\t', '\x2', '\x2', '\xBB', 
		'\xBC', '\a', '^', '\x2', '\x2', '\xBC', '\xBE', '\t', '\t', '\x2', '\x2', 
		'\xBD', '\xB4', '\x3', '\x2', '\x2', '\x2', '\xBD', '\xB8', '\x3', '\x2', 
		'\x2', '\x2', '\xBD', '\xBB', '\x3', '\x2', '\x2', '\x2', '\xBE', '+', 
		'\x3', '\x2', '\x2', '\x2', '\xBF', '\xC0', '\a', '^', '\x2', '\x2', '\xC0', 
		'\xC1', '\a', 'w', '\x2', '\x2', '\xC1', '\xC2', '\x5', '.', '\x17', '\x2', 
		'\xC2', '\xC3', '\x5', '.', '\x17', '\x2', '\xC3', '\xC4', '\x5', '.', 
		'\x17', '\x2', '\xC4', '\xC5', '\x5', '.', '\x17', '\x2', '\xC5', '-', 
		'\x3', '\x2', '\x2', '\x2', '\xC6', '\xC7', '\t', '\n', '\x2', '\x2', 
		'\xC7', '/', '\x3', '\x2', '\x2', '\x2', '\xC8', '\xCE', '\a', '$', '\x2', 
		'\x2', '\xC9', '\xCD', '\n', '\v', '\x2', '\x2', '\xCA', '\xCB', '\a', 
		'^', '\x2', '\x2', '\xCB', '\xCD', '\v', '\x2', '\x2', '\x2', '\xCC', 
		'\xC9', '\x3', '\x2', '\x2', '\x2', '\xCC', '\xCA', '\x3', '\x2', '\x2', 
		'\x2', '\xCD', '\xD0', '\x3', '\x2', '\x2', '\x2', '\xCE', '\xCC', '\x3', 
		'\x2', '\x2', '\x2', '\xCE', '\xCF', '\x3', '\x2', '\x2', '\x2', '\xCF', 
		'\xD1', '\x3', '\x2', '\x2', '\x2', '\xD0', '\xCE', '\x3', '\x2', '\x2', 
		'\x2', '\xD1', '\xD2', '\a', '$', '\x2', '\x2', '\xD2', '\x31', '\x3', 
		'\x2', '\x2', '\x2', '\xD3', '\xD9', '\t', '\f', '\x2', '\x2', '\xD4', 
		'\xD9', '\n', '\r', '\x2', '\x2', '\xD5', '\xD6', '\t', '\xE', '\x2', 
		'\x2', '\xD6', '\xD9', '\t', '\xF', '\x2', '\x2', '\xD7', '\xD9', '\t', 
		'\x10', '\x2', '\x2', '\xD8', '\xD3', '\x3', '\x2', '\x2', '\x2', '\xD8', 
		'\xD4', '\x3', '\x2', '\x2', '\x2', '\xD8', '\xD5', '\x3', '\x2', '\x2', 
		'\x2', '\xD8', '\xD7', '\x3', '\x2', '\x2', '\x2', '\xD9', '\x33', '\x3', 
		'\x2', '\x2', '\x2', '\x14', '\x2', '\x3', '>', 'L', 'X', '\x61', '\x63', 
		'q', '\x8B', '\x94', '\x96', '\x9A', '\xA6', '\xB2', '\xBD', '\xCC', '\xCE', 
		'\xD8', '\a', '\x4', '\x3', '\x2', '\t', '\x4', '\x2', '\x2', '\x3', '\x2', 
		'\x4', '\x2', '\x2', '\x2', '\x4', '\x2',
	};

	public static readonly ATN _ATN =
		new ATNDeserializer().Deserialize(_serializedATN);


}

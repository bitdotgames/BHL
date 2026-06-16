#if BHL_FRONT

using Antlr4.Runtime;
using System.IO;
using static bhlParser;



/// <summary>
/// All lexer methods that used in grammar (IsStrictMode)
/// should start with Upper Case Char similar to Lexer rules.
/// </summary>
public abstract class bhlLexerBase : Lexer
{
  private IToken _lastToken = null;

  public bhlLexerBase(ICharStream input)
    : base(input)
  {
  }

  public bhlLexerBase(ICharStream input, TextWriter output, TextWriter errorOutput) : this(input)
  {
  }

  public bool IsStartOfFile()
  {
    return _lastToken == null;
  }

  /// <summary>
  /// Return the next token from the character stream and records this last
  /// token in case it resides on the default channel. This recorded token
  /// is used to determine when the lexer could possibly match a regex
  /// literal.
  ///
  /// </summary>
  /// <returns>
  /// The next token from the character stream.
  /// </returns>
  public override IToken NextToken()
  {
    // Get the next token.
    IToken next = base.NextToken();

    if(next.Channel == DefaultTokenChannel)
    {
      // Keep track of the last token on the default channel.
      _lastToken = next;
    }

    return next;
  }

  public override void Reset()
  {
    _lastToken = null;
    base.Reset();
  }
}

public abstract class bhlParserBase : Parser
{
  public bhlParserBase(ITokenStream input)
    : base(input)
  {
  }

  public bhlParserBase(ITokenStream input, TextWriter output, TextWriter errorOutput) : this(input)
  {
  }

  /// <summary>
  /// Whether the previous token value equals to str
  /// </summary>
  protected bool prev(string str)
  {
    return this.TokenStream.LT(-1).Text.Equals(str);
  }

  // Whether the next token value equals to @param str
  protected bool next(string str)
  {
    return CurrentToken.Text.Equals(str);
  }

  // Cheap token-type check short-circuits before the hidden-channel scan.
  protected bool eosCondition() => CurrentToken.Type == CLOSE_BRACE || lineTerminator();

  /// <summary>
  /// Returns true if the hidden channel between the previous visible token
  /// and the current one contains a line terminator (NL token or a block
  /// comment that spans multiple lines).
  ///
  /// Look-back depth is two positions because BHL's lexer always emits NL
  /// as a distinct token that immediately precedes any line-starting WS,
  /// so the pattern is either [-1=NL], [-1=WS, -2=NL], or [-1=WS/-1=DC,
  /// -2=DC-with-newline].
  /// </summary>
  protected bool lineTerminator()
  {
    int idx = CurrentToken.TokenIndex - 1;
    IToken token = this.TokenStream.Get(idx);

    if(token.Channel != Lexer.Hidden)
      return false;

    if(token.Type == NL)
      return true;

    // Skip one WS token and look at what precedes it.
    if(token.Type == WS)
      token = this.TokenStream.Get(idx - 1);

    return token.Type == NL ||
      (token.Type == DELIMITED_COMMENT &&
       (token.Text.Contains('\n') || token.Text.Contains('\r')));
  }

  protected bool notLineTerminator()
  {
    return !this.lineTerminator();
  }

  protected bool whiteSpace()
  {
    IToken token = this.TokenStream.Get(CurrentToken.TokenIndex - 1);
    return token.Channel == Lexer.Hidden && token.Type == WS;
  }

  protected bool notWhiteSpace()
  {
    return !this.whiteSpace();
  }
}

#endif

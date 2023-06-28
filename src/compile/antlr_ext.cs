using Antlr4.Runtime;
using System.Collections.Generic;
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
  {}

  public bhlLexerBase(ICharStream input, TextWriter output, TextWriter errorOutput) : this(input)
  {}

  public bool IsStartOfFile(){
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
  {}

  public bhlParserBase(ITokenStream input, TextWriter output, TextWriter errorOutput) : this(input)
  {}

  /// <summary>
  /// Short form for prev(String str)
  /// </summary>
  protected bool p(string str)
  {
    return prev(str);
  }

  /// <summary>
  /// Whether the previous token value equals to str
  /// </summary>
  protected bool prev(string str)
  {
    return ((ITokenStream)this.InputStream).LT(-1).Text.Equals(str);
  }

  // Short form for next(String str)
  protected bool n(string str)
  {
    return next(str);
  }

  // Whether the next token value equals to @param str
  protected bool next(string str)
  {
    return ((ITokenStream)this.InputStream).LT(1).Text.Equals(str);
  }

  protected bool notLineTerminator()
  {
    return !here(NL);
  }

  protected bool closeBrace()
  {
    return ((ITokenStream)this.InputStream).LT(1).Type == CLOSE_BRACE;
  }

  /// <summary>Returns true if on the current index of the parser's
  /// token stream a token of the given type exists on the
  /// Hidden channel.
  /// </summary>
  /// <param name="type">
  /// The type of the token on the Hidden channel to check.
  /// </param>
  protected bool here(int type)
  {
    // Get the token ahead of the current index.
    int possibleIndexEosToken = CurrentToken.TokenIndex - 1;
    IToken ahead = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);

    // Check if the token resides on the Hidden channel and if it's of the
    // provided type.
    return ahead.Channel == Lexer.Hidden && ahead.Type == type;
  }

  /// <summary>
  /// Returns true if on the current index of the parser's
  /// token stream a token exists on the Hidden channel which
  /// either is a line terminator, or is a multi line comment that
  /// contains a line terminator.
  /// </summary>
  protected bool lineTerminatorAhead()
  {
    // Get the token ahead of the current index.
    int possibleIndexEosToken = CurrentToken.TokenIndex - 1;
    IToken ahead = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);

    if(ahead.Channel != Lexer.Hidden)
    {
      // We're only interested in tokens on the Hidden channel.
      return false;
    }

    if(ahead.Type == NL)
    {
      // There is definitely a line terminator ahead.
      return true;
    }

    if(ahead.Type == WS)
    {
      // Get the token ahead of the current whitespaces.
      possibleIndexEosToken = CurrentToken.TokenIndex - 2;
      ahead = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);
    }

    // Get the token's text and type.
    string text = ahead.Text;
    int type = ahead.Type;

    // Check if the token is, or contains a line terminator.
    return (type == DELIMITED_COMMENT && (text.Contains("\r") || text.Contains("\n"))) ||
      (type == NL);
  }
}

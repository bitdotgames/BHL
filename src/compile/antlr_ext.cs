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
    return ((ITokenStream)this.InputStream).LT(-1).Text.Equals(str);
  }

  // Whether the next token value equals to @param str
  protected bool next(string str)
  {
    return ((ITokenStream)this.InputStream).LT(1).Text.Equals(str);
  }

  protected bool closeBrace()
  {
    return ((ITokenStream)this.InputStream).LT(1).Type == CLOSE_BRACE;
  }

  protected bool closeBracket()
  {
    return ((ITokenStream)this.InputStream).LT(1).Type == CLOSE_BRACKET;
  }

  /// <summary>
  /// Returns true if on the current index of the parser's
  /// token stream a token exists on the Hidden channel which
  /// either is a line terminator, or is a multi line comment that
  /// contains a line terminator.
  /// </summary>
  protected bool lineTerminator()
  {
    //NOTE: CurrentToken contains prefetched new token, we need
    //      to 'look back' and get from input stream the previous
    //      token from the hidden channel

    // Get the token ahead of the current index.
    int possibleIndexEosToken = CurrentToken.TokenIndex - 1;
    IToken token = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);

    if(token.Channel != Lexer.Hidden)
    {
      // We're only interested in tokens on the Hidden channel.
      return false;
    }


    if(token.Type == NL)
    {
      // There is definitely a line terminator ahead.
      return true;
    }

    if(token.Type == WS)
    {
      // Get the token ahead of the current whitespaces.
      possibleIndexEosToken = CurrentToken.TokenIndex - 2;
      token = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);
    }

    // Get the token's text and type.
    string text = token.Text;
    int type = token.Type;

    // Check if the token is, or contains a line terminator.
    return
      type == NL ||
      (type == DELIMITED_COMMENT && (text.Contains("\r") || text.Contains("\n")));
  }

  protected bool notLineTerminator()
  {
    return !this.lineTerminator();
  }

  protected bool whiteSpace()
  {
    //NOTE: CurrentToken contains prefetched new token, we need
    //      to 'look back' and get from input stream the previous
    //      token from the hidden channel

    // Get the token ahead of the current index.
    int possibleIndexEosToken = CurrentToken.TokenIndex - 1;
    IToken token = ((ITokenStream)this.InputStream).Get(possibleIndexEosToken);

    if(token.Channel != Lexer.Hidden)
    {
      // We're only interested in tokens on the Hidden channel.
      return false;
    }

    return token.Type == WS;
  }

  protected bool notWhiteSpace()
  {
    return !this.whiteSpace();
  }
}

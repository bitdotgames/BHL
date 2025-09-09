#if BHL_FRONT
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
#endif

namespace bhl
{

public struct SourcePos
{
  public int line;
  public int column;

  public SourcePos(int line, int column)
  {
    this.line = line;
    this.column = column;
  }

  public override string ToString()
  {
    return line + ":" + column;
  }
}

public struct SourceRange
{
  public SourcePos start;
  public SourcePos end;

  public SourceRange(int line, int column)
  {
    this.start = new SourcePos(line, column);
    this.end = new SourcePos(line, column);
  }

  public SourceRange(SourcePos start, SourcePos end)
  {
    this.start = start;
    this.end = end;
  }

  public SourceRange(SourcePos start)
  {
    this.start = start;
    this.end = start;
  }

#if BHL_FRONT
  public SourceRange(Antlr4.Runtime.Misc.Interval interval, ITokenStream tokens)
  {
    var a = tokens.Get(interval.a);
    var b = tokens.Get(interval.b);
    this.start = new SourcePos(a.Line, a.Column);
    this.end = new SourcePos(b.Line, b.Column + (b.StopIndex - b.StartIndex));
  }
#endif

  public override string ToString()
  {
    return start + ".." + end;
  }
}

}
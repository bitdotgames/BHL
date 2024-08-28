
namespace bhl {
  
public class Origin
{
#if BHL_FRONT
  public AnnotatedParseTree parsed;
#endif
  public string native_file_path;
  public int native_line;

  public string source_file { 
    get {
#if BHL_FRONT
      if(parsed != null)
        return parsed.file;
#endif
      return native_file_path;
    }
  }

  public int source_line {
    get {
      return source_range.start.line; 
    }
  }

  public SourceRange source_range {
    get {
#if BHL_FRONT
      if(parsed != null)
        return parsed.range;
#endif
      return new SourceRange(new SourcePos(native_line, 1));
    }
  }

#if BHL_FRONT
  public Origin(AnnotatedParseTree ptree)
  {
    this.parsed = ptree;
  }

  public static implicit operator Origin(AnnotatedParseTree ptree)
  {
    return new Origin(ptree);
  }
#endif

  public Origin(
    [System.Runtime.CompilerServices.CallerFilePath] string file_path = "",
    [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
  )
  {
    this.native_file_path = file_path;
    this.native_line = line;
  }
}

}

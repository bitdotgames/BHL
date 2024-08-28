namespace bhl {
  
public static class SymbolExtensions
{
  static public bool IsStatic(this Symbol symb)
  {
    return
      (symb is FuncSymbol fs && fs.attribs.HasFlag(FuncAttrib.Static)) ||
      (symb is FieldSymbol fds && fds.attribs.HasFlag(FieldAttrib.Static))
      ;
  }
}
  
}

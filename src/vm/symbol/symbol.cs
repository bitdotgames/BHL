using System;
using System.Collections.Generic;

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

public abstract class Symbol : INamed, ITypeRefIndexable, marshall.IMarshallableGeneric 
{
  public string name;

  // All symbols know what scope contains them
  public IScope scope;

  // Information about origin where symbol is defined: file and line, parse tree
  public Origin origin;

  public Symbol(Origin origin, string name) 
  { 
    this.origin = origin;
    this.name = name; 
  }

  public string GetName()
  {
    return name;
  }

  public override string ToString() 
  {
    return name;
  }

  public abstract uint ClassId();

  public abstract void IndexTypeRefs(TypeRefIndex refs);
  public abstract void Sync(marshall.SyncContext ctx);
}

public interface ITyped
{
  IType GetIType();
}

public interface INativeType
{
  System.Type GetNativeType();
  object GetNativeObject(Val v);
}

public class TypeSet<T> : marshall.IMarshallable where T : class, IType
{
  //TODO: since TypeProxy implements custom Equals we could use HashSet here
  internal List<ProxyType> list = new List<ProxyType>();

  public int Count
  {
    get {
      return list.Count;
    }
  }

  public T this[int index]
  {
    get {
      var tp = list[index];
      var s = (T)tp.Get();
      if(s == null)
        throw new Exception("Type not found: " + tp);
      return s;
    }
  }

  public TypeSet()
  {}

  public bool Add(T t)
  {
    return Add(new ProxyType(t));
  }

  public bool Add(ProxyType tp)
  {
    if(list.IndexOf(tp) != -1)
      return false;
    list.Add(tp);
    return true;
  }

  public void Clear()
  {
    list.Clear();
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncTypeRefs(ctx, list);
  }
}

}

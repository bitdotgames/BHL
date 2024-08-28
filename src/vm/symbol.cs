using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

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

public class VariableSymbol : Symbol, ITyped, IScopeIndexed
{
  public const uint CLASS_ID = 8;

  public ProxyType type;

  int _scope_idx = -1;
  public int scope_idx {
    get {
      return _scope_idx;
    }
    set {
      _scope_idx = value;
    }
  }

#if BHL_FRONT
  //referenced upvalue, keep in mind it can be a 'local' variable from the   
  //outer lambda wrapping the current one
  internal VariableSymbol _upvalue;
#endif

  public VariableSymbol(Origin origin, string name, ProxyType type) 
    : base(origin, name) 
  {
    this.type = type;
  }

  //marshall factory version
  public VariableSymbol()
    : base(null, "")
  {}

#if BHL_FRONT
  public IType GuessType() 
  {
    return origin?.parsed == null ? type.Get() : origin.parsed.eval_type;  
  }
#endif
  
  public IType GetIType()
  {
    return type.Get();
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(type);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.SyncTypeRef(ctx, ref type);
    marshall.Marshall.Sync(ctx, ref _scope_idx);
  }

  public override string ToString()
  {
    return type + " " + name;
  }
}

public class GlobalVariableSymbol : VariableSymbol
{
  new public const uint CLASS_ID = 22;

  public bool is_local;

  public GlobalVariableSymbol(Origin origin, string name, ProxyType type) 
    : base(origin, name, type) 
  {}
  
  //marshall factory version
  public GlobalVariableSymbol()
    : base(null, "", new ProxyType())
  {}

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref is_local);
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

[System.Flags]
public enum FieldAttrib : byte
{
  None        = 0,
  Static      = 1,
}

public class FieldSymbol : VariableSymbol
{
  public delegate void FieldGetter(VM.Frame frm, Val v, ref Val res, FieldSymbol fld);
  public delegate void FieldSetter(VM.Frame frm, ref Val v, Val nv, FieldSymbol fld);
  public delegate void FieldRef(VM.Frame frm, Val v, out Val res, FieldSymbol fld);

  public FieldGetter getter;
  public FieldSetter setter;
  public FieldRef getref;

  protected byte _attribs = 0;
  public FieldAttrib attribs {
    get {
      return (FieldAttrib)_attribs;
    }
    set {
      _attribs = (byte)value;
    }
  }

  public FieldSymbol(Origin origin, string name, ProxyType type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : this(origin, name, 0, type, getter, setter, getref)
  {
  }

  public FieldSymbol(Origin origin, string name, FieldAttrib attribs, ProxyType type, FieldGetter getter = null, FieldSetter setter = null, FieldRef getref = null) 
    : base(origin, name, type)
  {
    this.attribs = attribs;

    this.getter = getter;
    this.setter = setter;
    this.getref = getref;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    base.Sync(ctx);

    marshall.Marshall.Sync(ctx, ref _attribs);
  }
}

public class FieldSymbolScript : FieldSymbol
{
  new public const uint CLASS_ID = 9;

  public FieldSymbolScript(Origin origin, string name, ProxyType type) 
    : base(origin, name, type, null, null, null)
  {
    this.getter = Getter;
    this.setter = Setter;
    this.getref = Getref;
  }
  //marshall factory version
  public FieldSymbolScript()
    : this(null, "", new ProxyType())
  {}

  void Getter(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    v.ValueCopyFrom(m[scope_idx]);
  }

  void Setter(VM.Frame frm, ref Val ctx, Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    var curr = m[scope_idx];
    for(int i=0;i<curr._refs;++i)
    {
      v.RefMod(RefOp.USR_INC);
      curr.RefMod(RefOp.USR_DEC);
    }
    curr.ValueCopyFrom(v);
  }

  void Getref(VM.Frame frm, Val ctx, out Val v, FieldSymbol fld)
  {
    var m = (IList<Val>)ctx._obj;
    v = m[scope_idx];
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }
}

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

public interface INativeType
{
  System.Type GetNativeType();
  object GetNativeObject(Val v);
}

public abstract class EnumSymbol : Symbol, IScope, IType, IEnumerable<Symbol>
{
  internal SymbolsStorage members;

  public EnumSymbol(Origin origin, string name)
     : base(origin, name)
  {
    this.members = new SymbolsStorage(this);
  }

  public IScope GetFallbackScope() { return scope; }

  public Symbol Resolve(string name) 
  {
    return members.Find(name);
  }

  public void Define(Symbol sym)
  {
    if(!(sym is EnumItemSymbol))
      throw new Exception("Invalid item");
    members.Add(sym);
  }

  public IEnumerator<Symbol> GetEnumerator() { return members.GetEnumerator(); }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public EnumItemSymbol FindValue(string name)
  {
    return Resolve(name) as EnumItemSymbol;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
  }
  
  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

public class EnumSymbolScript : EnumSymbol
{
  public const uint CLASS_ID = 12;

  public EnumSymbolScript(Origin origin, string name)
    : base(origin, name)
  {}

  //marshall factory version
  public EnumSymbolScript()
    : base(null, null)
  {}

  //0 - OK, 1 - duplicate key, 2 - duplicate value
  public int TryAddItem(Origin origin, string name, int val)
  {
    for(int i=0;i<members.Count;++i)
    {
      var m = (EnumItemSymbol)members[i];
      if(m.val == val)
        return 2;
      else if(m.name == name)
        return 1;
    }

    var item = new EnumItemSymbol(origin, name, val);
    //TODO: should be set by SymbolsDictionary
    item.scope = this;
    members.Add(item);
    return 0;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref members);
    if(ctx.is_read)
    {
      for(int i=0;i<members.Count;++i)
      {
        var item = (EnumItemSymbol)members[i];
        item.scope = this;
      }
    }
  }
}

public class EnumSymbolNative : EnumSymbol, INativeType
{
  System.Type native_type;

  public EnumSymbolNative(Origin origin, string name, System.Type native_type)
    : base(origin, name)
  {
    this.native_type = native_type;
  }

  public System.Type GetNativeType()
  {
    return native_type;
  }

  public object GetNativeObject(Val v)
  {
    //TODO: is it valid?
    return v?._num;
  }
}

public class EnumItemSymbol : Symbol, IType 
{
  public const uint CLASS_ID = 15;

  public EnumSymbol owner {
    get {
      return scope as EnumSymbol;
    }
  }
  public int val;

  public EnumItemSymbol(Origin origin, string name, int val = 0) 
    : base(origin, name) 
  {
    this.val = val;
  }

  //marshall factory version
  public EnumItemSymbol() 
    : base(null, null)
  {}

  public override uint ClassId()
  {
    return CLASS_ID;
  }
  
  public override void IndexTypeRefs(TypeRefIndex refs)
  {}

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref name);
    marshall.Marshall.Sync(ctx, ref val);
  }
}

public class SymbolsStorage : marshall.IMarshallable, IEnumerable<Symbol>
{
  internal IScope scope;
  internal List<Symbol> list = new List<Symbol>();
  //NOTE: used for lookup by name
  Dictionary<string, int> name2idx = new Dictionary<string, int>();

  public int Count
  {
    get {
      return list.Count;
    }
  }

  public Symbol this[int index]
  {
    get {
      return list[index];
    }
  }

  public SymbolsStorage(IScope scope)
  {
    if(scope == null)
      throw new Exception("Scope is null");
    this.scope = scope;
  }

  public bool Contains(string name)
  {
    return Find(name) != null;
  }

  public Symbol Find(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return null;
    return list[idx];
  }

  public void Add(Symbol s)
  {
    if(Find(s.name) != null)
      throw new SymbolError(s, "already defined symbol '" + s.name + "'"); 

    //only assingning scope if it's not assigned yet
    if(s.scope == null)
      s.scope = scope;

    if(s is IScopeIndexed si && si.scope_idx == -1)
      si.scope_idx = list.Count;

    list.Add(s);
    name2idx.Add(s.name, list.Count-1);
  }

  public void RemoveAt(int index)
  {
    var s = list[index];
    if(s.scope == scope)
      s.scope = null;
    list.RemoveAt(index);
    name2idx.Remove(s.name);
  }

  public Symbol TryAt(int index)
  {
    if(index < 0 || index >= list.Count)
      return null;
    return list[index];
  }

  public int IndexOf(Symbol s)
  {
    //TODO: use lookup by name instead?
    return list.IndexOf(s);
  }

  public int IndexOf(string name)
  {
    int idx;
    if(!name2idx.TryGetValue(name, out idx))
      return -1;
    return idx;
  }

  public bool Replace(Symbol what, Symbol subst)
  {
    int idx = IndexOf(what);
    if(idx == -1)
      return false;
    
    list[idx] = subst;

    name2idx.Remove(what.name);
    name2idx[subst.name] = idx;

    return true;
  }

  public void Clear()
  {
    foreach(var s in list)
    {
      if(s.scope == scope)
        s.scope = null;
    }
    list.Clear();
    name2idx.Clear();
  }

  public void Sync(marshall.SyncContext ctx) 
  {
    marshall.Marshall.SyncGeneric(ctx, list);

    if(ctx.is_read)
    {
      for(int idx=0;idx<list.Count;++idx)
      {
        var tmp = list[idx];
        tmp.scope = scope;
        name2idx.Add(tmp.name, idx);
      }
    }
  }

  public void UnionWith(SymbolsStorage o)
  {
    for(int i=0;i<o.Count;++i)
      Add(o[i]);
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    return list.GetEnumerator();
  }
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

public class TypeRefIndex
{
  internal List<ProxyType> all = new List<ProxyType>();

  public int Count {
    get {
      return all.Count;
    }
  }

  bool TryAdd(ProxyType v)
  {
    if(FindIndex(v) == -1)
    {
      all.Add(v);
      return true;
    }
    return false;
  }

  public void Index(ProxyType v)
  {
    if(!TryAdd(v))
      return;
    
    if(v.Get() is ITypeRefIndexable itr)
      itr.IndexTypeRefs(this);
  }

  public void Index(IType v)
  {
    Index(new ProxyType(v));
  }

  public void Index(Symbol s)
  {
    if(s is IType itype)
      Index(itype);
    else if(s is ITypeRefIndexable itr)
      itr.IndexTypeRefs(this);
  }
  
  public void Index(IList<ProxyType> vs)
  {
    foreach(var v in vs)
      Index(v);
  }
  
  public void Index(IList<Symbol> vs)
  {
    foreach(var v in vs)
      Index(v);
  }

  public int FindIndex(ProxyType v)
  {
    return all.IndexOf(v);
  }

  public int GetIndex(ProxyType v)
  {
    int idx = FindIndex(v);
    if(idx == -1)
      throw new Exception("Not found index for type '" + v + "', total entries " + all.Count);
    return idx;
  }

  public ProxyType Get(int idx)
  {
    return all[idx];
  }

  public void SetAt(int idx, ProxyType v)
  {
    //let's fill the gap if any
    for(int i=all.Count-1; i <= idx; ++i)
      all.Add(new ProxyType());
        
    all[idx] = v;
  }

  public bool IsValid(int idx)
  {
    return idx >= 0 && idx < all.Count && !all[idx].IsNull();
  }
}
public class SymbolFactory : marshall.IFactory
{
  public Types types;
  public INamedResolver resolver;

  public SymbolFactory(Types types, INamedResolver resolver) 
  {
    this.types = types;
    this.resolver = resolver; 
  }

  public marshall.IMarshallableGeneric CreateById(uint id) 
  {
    switch(id)
    {
      case IntSymbol.CLASS_ID:
        return Types.Int;
      case FloatSymbol.CLASS_ID:
        return Types.Float;
      case StringSymbol.CLASS_ID:
        return Types.String;
      case BoolSymbol.CLASS_ID:
        return Types.Bool;
      case AnySymbol.CLASS_ID:
        return Types.Any;
      case VoidSymbol.CLASS_ID:
        return Types.Void;
      case VariableSymbol.CLASS_ID:
        return new VariableSymbol(); 
      case GlobalVariableSymbol.CLASS_ID:
        return new GlobalVariableSymbol(); 
      case FuncArgSymbol.CLASS_ID:
        return new FuncArgSymbol(); 
      case FieldSymbolScript.CLASS_ID:
        return new FieldSymbolScript(); 
      case GenericArrayTypeSymbol.CLASS_ID:
        return new GenericArrayTypeSymbol(); 
      case GenericNativeArrayTypeSymbol.CLASS_ID:
        return new GenericArrayTypeSymbol(); 
      case GenericMapTypeSymbol.CLASS_ID:
        return new GenericMapTypeSymbol(); 
      case ClassSymbolScript.CLASS_ID:
        return new ClassSymbolScript();
      case InterfaceSymbolScript.CLASS_ID:
        return new InterfaceSymbolScript();
      case EnumSymbolScript.CLASS_ID:
        return new EnumSymbolScript();
      case EnumItemSymbol.CLASS_ID:
        return new EnumItemSymbol();
      case FuncSymbolScript.CLASS_ID:
        return new FuncSymbolScript();
      case FuncSignature.CLASS_ID:
        return new FuncSignature();
      case RefType.CLASS_ID:
        return new RefType();
      case TupleType.CLASS_ID:
        return new TupleType();
      case Namespace.CLASS_ID:
        return new Namespace();
      default:
        return null;
    }
  }
}

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

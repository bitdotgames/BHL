using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace bhl
{

public class Namespace : Symbol, IScope,
  marshall.IMarshallable, IEnumerable<Symbol>, INamedResolver
{
  public const uint CLASS_ID = 20;

  public ModuleDeclared module;

  internal List<Namespace> links = new List<Namespace>();

  //NOTE: paramater which reflects 'how deeply' the namespace was linked from other namespaces,
  //      it's reset to 0 once we add any real members to namespace. 'Deeply linked' namespaces
  //      are ignored during Resolve(..) which means the namespace was locally added from another
  //      imported module which in its turn also imported it.
  internal int indirectness = 0;

  // True when this namespace entry was created as an import-link shadow rather than being
  // directly defined in the owning module. Such entries should be skipped when collecting
  // module-local symbols (e.g. for IDE completion across all modules).
  public bool IsLinkedShadow => indirectness > 0;

  public SymbolsStorage members;

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public Namespace(ModuleDeclared module, string name)
    : base(null, name)
  {
    this.module = module;
    this.members = new SymbolsStorage(this);
  }

  public Namespace(ModuleDeclared module)
    : this(module, "")
  {
  }

  //marshall version
  public Namespace()
    : this(null, "")
  {
  }

  public override string ToString()
  {
    if(indirectness == 0)
      return name;
    else
      return name + " -> " + indirectness;
  }

  public INamed ResolveNamedByPath(NamePath path)
  {
    return this.ResolveSymbolByPath(path);
  }

  public void ForAllLocalSymbols(System.Action<Symbol> cb)
  {
    for(int m = 0; m < members.Count; ++m)
    {
      var member = members[m];
      var ns = member as Namespace;

      //NOTE: taking into consideration only 'direct' namespaces
      if(ns == null || ns.indirectness == 0)
        cb(member);

      if(ns != null)
      {
        if(ns.indirectness == 0)
          ns.ForAllLocalSymbols(cb);
      }
      else if(member is IScope s)
        s.ForAllSymbols(cb);
    }
  }

  public Namespace Nest(string name)
  {
    var sym = Resolve(name);
    if(sym != null && !(sym is Namespace))
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'");

    if(sym == null)
    {
      sym = new Namespace(module, name);
      Define(sym);
    }

    return sym as Namespace;
  }

  public void Link(Namespace other)
  {
    var conflict = TryLink(other);
    if(!conflict.Ok)
      throw new SymbolError(conflict.local, "already defined symbol '" + conflict.local.name + "'");
  }

  public struct LinkConflict
  {
    public Symbol local;
    public Symbol other;

    public bool Ok
    {
      get { return local == null && other == null; }
    }

    public LinkConflict(Symbol local, Symbol other)
    {
      this.local = local;
      this.other = other;
    }
  }

  public LinkConflict TryLink(Namespace other)
  {
    if(IsLinked(other))
      return default(LinkConflict);

    for(int i = 0; i < other.members.Count; ++i)
    {
      var other_symb = other.members[i];

      var this_symb = _Resolve(other_symb.name);

      if(other_symb is Namespace other_ns)
      {
        if(this_symb is Namespace this_ns)
        {
          var conflict = this_ns.TryLink(other_ns);
          if(!conflict.Ok)
            return conflict;
          if(other_ns.indirectness < this_ns.indirectness)
            this_ns.indirectness = other_ns.indirectness + 1;
        }
        else if(this_symb != null)
          return new LinkConflict(this_symb, other_symb);
        else
        {
          //NOTE: let's create a local version of non-existing namespace
          var ns = new Namespace(module, other_ns.name);
          //NOTE: increasing indirectness, taking into account 'other ns'
          //      indirectness as well
          //      (indirectnes will be reset once any real members added)
          ns.indirectness += (other_ns.indirectness + 1);
          ns.TryLink(other_ns);
          members.Add(ns);
        }
      }
      else if(this_symb != null)
      {
        //NOTE: let's ignore other local module symbols
        if(!other_symb.IsModuleLocal())
          return new LinkConflict(this_symb, other_symb);
      }
    }

    links.Add(other);

    return default(LinkConflict);
  }

  bool IsLinked(Namespace other)
  {
    return other == this || links.IndexOf(other) != -1;
  }

  public Namespace UnlinkAll()
  {
    var clean = new Namespace(module, name);

    for(int i = 0; i < members.Count; ++i)
    {
      var s = members[i];
      var ns = s as Namespace;
      if(ns == null || ns.indirectness == 0)
        clean.members.Add(s);
    }

    return clean;
  }

  // One declaration taking part in the cross module uniqueness check. 'symbol' is null when the
  // declaration was replayed from a build cache instead of a materialized module, in which case
  // only the path, its kind and the owning module's name are known.
  public readonly struct Decl
  {
    public readonly string path;
    public readonly bool is_namespace;
    public readonly Symbol symbol;
    public readonly string module_name;

    public Decl(string path, bool is_namespace, Symbol symbol, string module_name)
    {
      this.path = path;
      this.is_namespace = is_namespace;
      this.symbol = symbol;
      this.module_name = module_name;
    }
  }

  // The declarations of a module which must be unique across the whole project. Module local
  // symbols shadow one another across modules and an indirect namespace comes from a linked
  // import rather than being declared here, so neither takes part.
  public static IEnumerable<Decl> EnumDecls(ModuleDeclared module)
  {
    return EnumDecls(module.ns, "", module.name);
  }

  static IEnumerable<Decl> EnumDecls(Namespace ns, string prefix, string module_name)
  {
    for(int i = 0; i < ns.members.Count; ++i)
    {
      var symb = ns.members[i];

      if(symb is Namespace sub_ns)
      {
        if(sub_ns.indirectness != 0)
          continue;

        string path = prefix + sub_ns.name;
        yield return new Decl(path, true, sub_ns, module_name);

        foreach(var nested in EnumDecls(sub_ns, path + ".", module_name))
          yield return nested;
      }
      else
      {
        if(symb.IsModuleLocal())
          continue;

        yield return new Decl(prefix + symb.name, false, symb, module_name);
      }
    }
  }

  // Each module is parsed against its own private Namespace, so a global redeclared across
  // two different modules is only caught by looking at them together like this. Returns only
  // the first conflict found (module-local symbols are expected to shadow one another across
  // modules and are excluded).
  public static SymbolError CheckUniqueSymbols(IEnumerable<ModuleDeclared> modules)
  {
    return CheckUniqueSymbols(modules.Select(EnumDecls));
  }

  public static SymbolError CheckUniqueSymbols(IEnumerable<IEnumerable<Decl>> modules)
  {
    //NOTE: a flat path -> declaration map rather than merging every module into one namespace.
    //      That accumulated one link per module, and _Resolve scans links, so the merge
    //      cost grew with the number of modules already merged - quadratic overall, paid
    //      on every build including an incremental one that touched a single file
    var seen = new Dictionary<string, Decl>();

    foreach(var decls in modules)
    {
      foreach(var decl in decls)
      {
        if(seen.TryGetValue(decl.path, out var prev))
        {
          //NOTE: namespaces of the same path merge, anything else under it collides
          if(!decl.is_namespace || !prev.is_namespace)
            return Conflict(prev, decl);
        }
        else
          seen[decl.path] = decl;
      }
    }

    return null;
  }

  static SymbolError Conflict(Decl local, Decl other)
  {
    string ModuleName(Decl d) => d.symbol != null
      ? ((d.symbol.scope as Namespace)?.module?.name ?? "?")
      : (d.module_name ?? "?");

    string TypePath(Decl d) => d.symbol != null ? d.symbol.GetFullTypePath().ToString() : d.path;

    //NOTE: a declaration replayed from the build cache carries no origin
    string OriginSuffix(Decl d) => d.symbol?.origin != null
      ? $" ({d.symbol.origin.source_file}:{d.symbol.origin.source_range.start.line})"
      : "";

    //NOTE: whichever side was materialized gives the error a source position
    return new SymbolError(local.symbol ?? other.symbol,
      "symbol '" + TypePath(other) + "' declared in module '" + ModuleName(other) +
      "'" + OriginSuffix(other) + " conflicts with an existing declaration in module '" +
      ModuleName(local) + "'" + OriginSuffix(local));
  }


  public IScope GetFallbackScope()
  {
    return scope;
  }

  public IEnumerator<Symbol> GetEnumerator()
  {
    //TODO: this is to remove any duplicated symbols
    //      (like symbols in linked namespaces)
    var seen_names = new HashSet<string>();

    foreach(var s in members)
    {
      var ns = s as Namespace;
      if(ns == null || ns.indirectness <= 1)
      {
        seen_names.Add(s.name);
        yield return s;
      }
    }

    foreach(var lnk in links)
    {
      foreach(var s in lnk.members)
      {
        if(seen_names.Contains(s.name))
          continue;

        var ns = s as Namespace;
        if(ns == null || ns.indirectness == 0)
          yield return s;
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public virtual Symbol Resolve(string name)
  {
    var s = members.Find(name);
    if(s != null)
    {
      //NOTE: allowing only namespaces which have indirectness <= 1
      //      (with any real members or directly imported ones)
      var ns = s as Namespace;
      if(ns == null || ns.indirectness <= 1)
        return s;
    }

    foreach(var lnk in links)
    {
      s = lnk.members.Find(name);
      if(s != null)
      {
        var ns = s as Namespace;
        if(ns == null || ns.indirectness == 0)
          return s;
      }
    }

    return null;
  }

  Symbol _Resolve(string name)
  {
    var s = members.Find(name);
    if(s != null)
      return s;

    foreach(var lnk in links)
    {
      s = lnk.members.Find(name);
      if(s != null)
        return s;
    }

    return null;
  }

  public virtual void Define(Symbol sym)
  {
    if(Resolve(sym.name) != null)
      throw new SymbolError(sym, "already defined symbol '" + sym.name + "'");

    if(sym is FuncSymbolNative fsn)
      module?.nfunc_index.Index(fsn);
    else if(sym is FuncSymbolScript fss)
      module?.func_index.Index(fss);
    else if(sym is VariableSymbol vs)
      module?.gvar_index.Index(vs);

    //NOTE: let's reset 'indirectness', it's now considered a 'real' namespace
    //      since it now contains real symbols
    indirectness = 0;

    members.Add(sym);
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(members.list);
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    //NOTE: persisting only essential data since other pieces
    //      will be restored (e.g module setup, during imports, etc)
    marshall.Marshall.Sync(ctx, ref name);
    var _members = members;
    //NOTE: there's no need to persist members of indirect namespace
    if(!ctx.is_read && indirectness > 0)
      _members = new SymbolsStorage(this);
    marshall.Marshall.Sync(ctx, ref _members);
    marshall.Marshall.Sync(ctx, ref indirectness);
  }
}

}
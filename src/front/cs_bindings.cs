#if (BHL_PARSER || UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace bhl
{

//NOTE: bindings read from C# registration sources without compiling them, e.g. by the LSP
//      on every change of the sources. The sources are seen as if compiled with BHL_FRONT
//      defined and must be in a strict straight-line shape (see docs/csharp-bindings.md): static
//      methods calling the bhl symbols API, no control flow. Anything else is an error
public class CsSourceBindings : IUserBindings
{
  public static readonly string[] Defines = new string[] { "BHL_FRONT" };

  readonly List<string> files;

  public CsSourceBindings(List<string> files)
  {
    this.files = files;
  }

  public void Register(Types ts)
  {
    CsBindingsProgram.Read(files, Defines).Run(ts);
  }

  //NOTE: (name, version) of the [BhlBinding] attributes, without running the registration
  public IEnumerable<(string name, string version)> GetDeclaredBindings()
  {
    return CsBindingsProgram.Read(files, Defines).declared;
  }
}

public class CsBindingsException : Exception
{
  public CsBindingsException(string file, int line, string msg)
    : base($"{file}:{line}: {msg}")
  {}

  public CsBindingsException(string msg)
    : base(msg)
  {}
}

public class CsBindingsProgram
{
  public List<(string name, string version)> declared = new List<(string name, string version)>();

  //NOTE: by full name; partial classes in several files are one class
  internal Dictionary<string, CsClass> classes = new Dictionary<string, CsClass>();

  public static CsBindingsProgram Read(IList<string> files, IEnumerable<string> defines)
  {
    var prog = new CsBindingsProgram();
    var defs = new HashSet<string>(defines);
    foreach(var file in files)
    {
      var src = CsPreprocessor.Process(file, File.ReadAllText(file), defs);
      new CsParser(file, CsLexer.Tokenize(file, src), prog).ParseFile();
    }
    return prog;
  }

  //NOTE: runs the entry the way the compiled code would: 'Register' of the class implementing
  //      IUserBindings (it may combine several generated modules), else the only 'Register'
  public void Run(Types ts)
  {
    var entry = FindEntry(classes.Values.Where(c => c.is_user_bindings), "class implementing IUserBindings") ??
                FindEntry(classes.Values, "class");
    if(entry == null)
      throw new CsBindingsException("C# bindings: no 'Register' method found in the sources");

    foreach(var d in declared)
      ts.RegisterBindingsVersion(d.name, d.version);

    new CsInterpreter(this).Call(entry, new List<object>() { ts }, entry.line);
  }

  static CsMethod FindEntry(IEnumerable<CsClass> candidates, string what)
  {
    var found = candidates.Where(c => c.methods.ContainsKey("Register")).Select(c => c.methods["Register"]).ToList();
    if(found.Count > 1)
      throw new CsBindingsException(
        $"C# bindings: 'Register' is declared in more than one {what} ({string.Join(", ", found.Select(m => m.cls.full_name))}) - " +
        "the entry is 'Register' of a single class implementing IUserBindings"
      );
    return found.Count == 1 ? found[0] : null;
  }
}

///////////////////////////////////////////////////

static class CsPreprocessor
{
  //NOTE: line based, inactive lines become empty so that lines stay the same
  public static string Process(string file, string src, HashSet<string> defines)
  {
    var lines = src.Replace("\r\n", "\n").Split('\n');
    var sb = new StringBuilder();
    //NOTE: per #if: was the enclosing region active, is any branch taken already
    var stack = new List<(bool outer_active, bool taken)>();
    bool active = true;

    for(int i = 0; i < lines.Length; ++i)
    {
      string t = lines[i].TrimStart();
      if(!t.StartsWith("#"))
      {
        sb.Append(active ? lines[i] : "").Append('\n');
        continue;
      }

      string d = t.Substring(1).Trim();
      string kw = new string(d.TakeWhile(char.IsLetter).ToArray());
      string rest = d.Substring(kw.Length).Trim();
      int line = i + 1;

      if(kw == "if")
      {
        bool cond = Eval(file, line, rest, defines);
        stack.Add((active, cond));
        active = active && cond;
      }
      else if(kw == "elif" || kw == "else")
      {
        if(stack.Count == 0)
          throw new CsBindingsException(file, line, $"#{kw} without #if");
        var top = stack[stack.Count - 1];
        bool cond = kw == "else" || Eval(file, line, rest, defines);
        active = top.outer_active && !top.taken && cond;
        stack[stack.Count - 1] = (top.outer_active, top.taken || cond);
      }
      else if(kw == "endif")
      {
        if(stack.Count == 0)
          throw new CsBindingsException(file, line, "#endif without #if");
        active = stack[stack.Count - 1].outer_active;
        stack.RemoveAt(stack.Count - 1);
      }
      else if(kw == "define" || kw == "undef")
      {
        if(active)
        {
          if(kw == "define")
            defines.Add(rest);
          else
            defines.Remove(rest);
        }
      }
      else if(kw != "pragma" && kw != "region" && kw != "endregion" && kw != "nullable")
        throw new CsBindingsException(file, line, $"unsupported directive '#{kw}'");

      sb.Append('\n');
    }

    if(stack.Count > 0)
      throw new CsBindingsException(file, lines.Length, "unterminated #if");

    return sb.ToString();
  }

  //NOTE: symbols, true/false, !, &&, ||, parentheses
  static bool Eval(string file, int line, string expr, HashSet<string> defines)
  {
    int pos = 0;
    bool res = Or();
    Skip();
    if(pos < expr.Length)
      throw new CsBindingsException(file, line, $"invalid condition '{expr}'");
    return res;

    void Skip()
    {
      while(pos < expr.Length && char.IsWhiteSpace(expr[pos]))
        ++pos;
    }

    bool Or()
    {
      bool v = And();
      Skip();
      while(pos + 1 < expr.Length && expr[pos] == '|' && expr[pos + 1] == '|')
      {
        pos += 2;
        v = And() | v;
        Skip();
      }
      return v;
    }

    bool And()
    {
      bool v = Unary();
      Skip();
      while(pos + 1 < expr.Length && expr[pos] == '&' && expr[pos + 1] == '&')
      {
        pos += 2;
        v = Unary() & v;
        Skip();
      }
      return v;
    }

    bool Unary()
    {
      Skip();
      if(pos < expr.Length && expr[pos] == '!')
      {
        ++pos;
        return !Unary();
      }
      if(pos < expr.Length && expr[pos] == '(')
      {
        ++pos;
        bool v = Or();
        Skip();
        if(pos >= expr.Length || expr[pos] != ')')
          throw new CsBindingsException(file, line, $"invalid condition '{expr}'");
        ++pos;
        return v;
      }
      int start = pos;
      while(pos < expr.Length && (char.IsLetterOrDigit(expr[pos]) || expr[pos] == '_'))
        ++pos;
      if(start == pos)
        throw new CsBindingsException(file, line, $"invalid condition '{expr}'");
      string sym = expr.Substring(start, pos - start);
      if(sym == "true")
        return true;
      if(sym == "false")
        return false;
      return defines.Contains(sym);
    }
  }
}

///////////////////////////////////////////////////

enum CsTok
{
  Ident,
  Int,
  Float,
  String,
  Char,
  Punct,
  EOF
}

class CsToken
{
  public CsTok kind;
  public string text;
  public object value;
  public int line;

  public override string ToString()
  {
    return kind == CsTok.EOF ? "end of file" : $"'{text}'";
  }
}

static class CsLexer
{
  static readonly string[] Puncts2 = new string[] { "=>", "==", "!=", "&&", "||", "::", "<=", ">=" };

  public static List<CsToken> Tokenize(string file, string src)
  {
    var toks = new List<CsToken>();
    int i = 0;
    int line = 1;

    while(i < src.Length)
    {
      char c = src[i];

      if(c == '\n')
      {
        ++line;
        ++i;
        continue;
      }
      if(char.IsWhiteSpace(c))
      {
        ++i;
        continue;
      }
      if(c == '/' && i + 1 < src.Length && src[i + 1] == '/')
      {
        while(i < src.Length && src[i] != '\n')
          ++i;
        continue;
      }
      if(c == '/' && i + 1 < src.Length && src[i + 1] == '*')
      {
        i += 2;
        while(i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/'))
        {
          if(src[i] == '\n')
            ++line;
          ++i;
        }
        i += 2;
        continue;
      }

      int tok_line = line;

      if(char.IsLetter(c) || c == '_' || (c == '@' && i + 1 < src.Length && char.IsLetter(src[i + 1])))
      {
        int start = c == '@' ? ++i : i;
        while(i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_'))
          ++i;
        toks.Add(new CsToken() { kind = CsTok.Ident, text = src.Substring(start, i - start), line = tok_line });
        continue;
      }

      if(char.IsDigit(c))
      {
        int start = i;
        bool is_float = false;
        if(c == '0' && i + 1 < src.Length && (src[i + 1] == 'x' || src[i + 1] == 'X'))
        {
          i += 2;
          while(i < src.Length && Uri.IsHexDigit(src[i]))
            ++i;
          string hex = src.Substring(start + 2, i - start - 2);
          while(i < src.Length && char.IsLetter(src[i]))
            ++i;
          toks.Add(new CsToken() { kind = CsTok.Int, text = src.Substring(start, i - start), value = Convert.ToInt64(hex, 16), line = tok_line });
          continue;
        }
        while(i < src.Length && (char.IsDigit(src[i]) || src[i] == '.' || src[i] == 'e' || src[i] == 'E'))
        {
          if(src[i] == '.' || src[i] == 'e' || src[i] == 'E')
            is_float = true;
          ++i;
        }
        string num = src.Substring(start, i - start);
        string suffix = "";
        while(i < src.Length && char.IsLetter(src[i]))
          suffix += src[i++];
        suffix = suffix.ToLower();
        if(suffix == "f" || suffix == "d" || suffix == "m")
          is_float = true;
        var tok = new CsToken() { text = num + suffix, line = tok_line };
        if(is_float)
        {
          tok.kind = CsTok.Float;
          tok.value = suffix == "f"
            ? (object)float.Parse(num, System.Globalization.CultureInfo.InvariantCulture)
            : double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
          tok.kind = CsTok.Int;
          long v = long.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
          tok.value = suffix.Contains("u") || suffix.Contains("l") || v > int.MaxValue || v < int.MinValue ? (object)v : (int)v;
        }
        toks.Add(tok);
        continue;
      }

      if(c == '"' || (c == '@' && i + 1 < src.Length && src[i + 1] == '"'))
      {
        bool verbatim = c == '@';
        i += verbatim ? 2 : 1;
        var sb = new StringBuilder();
        while(true)
        {
          if(i >= src.Length)
            throw new CsBindingsException(file, tok_line, "unterminated string");
          char sc = src[i];
          if(verbatim)
          {
            if(sc == '"' && i + 1 < src.Length && src[i + 1] == '"')
            {
              sb.Append('"');
              i += 2;
              continue;
            }
            if(sc == '"')
            {
              ++i;
              break;
            }
            if(sc == '\n')
              ++line;
            sb.Append(sc);
            ++i;
            continue;
          }
          if(sc == '\n')
            throw new CsBindingsException(file, tok_line, "newline in string");
          if(sc == '"')
          {
            ++i;
            break;
          }
          if(sc == '\\')
          {
            sb.Append(Unescape(file, tok_line, src, ref i));
            continue;
          }
          sb.Append(sc);
          ++i;
        }
        toks.Add(new CsToken() { kind = CsTok.String, text = sb.ToString(), value = sb.ToString(), line = tok_line });
        continue;
      }

      if(c == '$')
        throw new CsBindingsException(file, tok_line, "interpolated strings are not supported");

      if(c == '\'')
      {
        ++i;
        char ch = src[i] == '\\' ? Unescape(file, tok_line, src, ref i) : src[i++];
        if(i >= src.Length || src[i] != '\'')
          throw new CsBindingsException(file, tok_line, "invalid char literal");
        ++i;
        toks.Add(new CsToken() { kind = CsTok.Char, text = ch.ToString(), value = ch, line = tok_line });
        continue;
      }

      string p2 = i + 1 < src.Length ? src.Substring(i, 2) : null;
      if(p2 != null && Array.IndexOf(Puncts2, p2) != -1)
      {
        toks.Add(new CsToken() { kind = CsTok.Punct, text = p2, line = tok_line });
        i += 2;
        continue;
      }
      toks.Add(new CsToken() { kind = CsTok.Punct, text = c.ToString(), line = tok_line });
      ++i;
    }

    toks.Add(new CsToken() { kind = CsTok.EOF, text = "", line = line });
    return toks;
  }

  static char Unescape(string file, int line, string src, ref int i)
  {
    char e = src[i + 1];
    i += 2;
    switch(e)
    {
      case 'n': return '\n';
      case 't': return '\t';
      case 'r': return '\r';
      case '0': return '\0';
      case '\\': return '\\';
      case '"': return '"';
      case '\'': return '\'';
      default:
        throw new CsBindingsException(file, line, $"unsupported escape '\\{e}'");
    }
  }
}

///////////////////////////////////////////////////

abstract class CsExpr
{
  public int line;
}

class CsLit : CsExpr
{
  public object value;
}

class CsName : CsExpr
{
  public string name;
}

class CsMember : CsExpr
{
  public CsExpr target;
  public string name;
}

class CsArg
{
  public string name;
  public CsExpr expr;
}

class CsCall : CsExpr
{
  public CsExpr target;
  public List<CsArg> args;
}

class CsNew : CsExpr
{
  public string type;
  public List<string> generic_args = new List<string>();
  public List<CsArg> args;
  //NOTE: a collection initializer, only for lists
  public List<CsExpr> items;
}

//NOTE: '|' of enum flags or integers
class CsBitOr : CsExpr
{
  public CsExpr left;
  public CsExpr right;
}

class CsNewArray : CsExpr
{
  public string elem_type;
  public List<CsExpr> items;
}

class CsCast : CsExpr
{
  public string type;
  public CsExpr expr;
}

abstract class CsStmt
{
  public int line;
}

class CsBlock : CsStmt
{
  public List<CsStmt> stmts = new List<CsStmt>();
}

class CsVarDecl : CsStmt
{
  public string name;
  public CsExpr expr;
}

class CsExprStmt : CsStmt
{
  public CsCall call;
}

//NOTE: the only conditional: 'if(<scope>.Resolve(<name>) == null) <stmt>' - defines a symbol
//      shared by several bindings modules only once
class CsDefineIfAbsent : CsStmt
{
  public CsExpr scope;
  public CsExpr name;
  public CsStmt body;
}

class CsClass
{
  public string full_name;
  public bool is_user_bindings;
  public Dictionary<string, CsMethod> methods = new Dictionary<string, CsMethod>();
}

class CsMethod
{
  public CsClass cls;
  public string file;
  public int line;
  public string name;
  public List<string> args = new List<string>();
  public CsBlock body;
}

///////////////////////////////////////////////////

class CsParser
{
  static readonly HashSet<string> Modifiers = new HashSet<string>()
  {
    "public", "private", "protected", "internal", "static", "partial", "sealed",
    "abstract", "readonly", "unsafe", "extern", "new", "override", "virtual"
  };

  static readonly HashSet<string> PrimitiveTypes = new HashSet<string>()
  {
    "object", "string", "bool", "byte", "sbyte", "short", "ushort", "int", "uint",
    "long", "ulong", "float", "double", "char", "void"
  };

  readonly string file;
  readonly List<CsToken> toks;
  readonly CsBindingsProgram prog;
  int pos;
  readonly List<string> namespaces = new List<string>();
  CsClass cls;

  public CsParser(string file, List<CsToken> toks, CsBindingsProgram prog)
  {
    this.file = file;
    this.toks = toks;
    this.prog = prog;
  }

  CsToken Peek(int offset = 0)
  {
    return toks[Math.Min(pos + offset, toks.Count - 1)];
  }

  CsToken Next()
  {
    return toks[Math.Min(pos++, toks.Count - 1)];
  }

  bool Is(string text, int offset = 0)
  {
    var t = Peek(offset);
    return (t.kind == CsTok.Punct || t.kind == CsTok.Ident) && t.text == text;
  }

  bool Accept(string text)
  {
    if(!Is(text))
      return false;
    ++pos;
    return true;
  }

  CsToken Expect(string text)
  {
    if(!Is(text))
      throw Error(Peek(), $"expected '{text}', got {Peek()}");
    return Next();
  }

  string ExpectIdent()
  {
    var t = Next();
    if(t.kind != CsTok.Ident)
      throw Error(t, $"expected an identifier, got {t}");
    return t.text;
  }

  CsBindingsException Error(CsToken t, string msg)
  {
    return new CsBindingsException(file, t.line, msg);
  }

  public void ParseFile()
  {
    int ns_depth = 0;
    var attrs = new List<(string name, List<object> args, int line)>();

    while(Peek().kind != CsTok.EOF)
    {
      if(Accept("using"))
      {
        while(!Accept(";"))
          Next();
      }
      else if(Accept("namespace"))
      {
        namespaces.Add(ParseQualifiedName());
        if(Accept("{"))
          ++ns_depth;
        else
          Expect(";");
      }
      else if(Is("}") && ns_depth > 0)
      {
        Next();
        --ns_depth;
        namespaces.RemoveAt(namespaces.Count - 1);
      }
      else if(Is("["))
        attrs.AddRange(ParseAttributes());
      else
      {
        ParseClass(attrs);
        attrs.Clear();
      }
    }
  }

  string ParseQualifiedName()
  {
    string name = ExpectIdent();
    while(Accept("."))
      name += "." + ExpectIdent();
    return name;
  }

  List<(string name, List<object> args, int line)> ParseAttributes()
  {
    var res = new List<(string name, List<object> args, int line)>();
    Expect("[");
    while(true)
    {
      int line = Peek().line;
      string name = ParseQualifiedName();
      var args = new List<object>();
      if(Accept("("))
      {
        while(!Accept(")"))
        {
          var t = Next();
          if(t.kind == CsTok.EOF)
            throw Error(t, "unterminated attribute");
          if(t.kind == CsTok.String)
            args.Add(t.value);
        }
      }
      res.Add((name, args, line));
      if(Accept("]"))
        break;
      Expect(",");
    }
    return res;
  }

  void ParseClass(List<(string name, List<object> args, int line)> attrs)
  {
    while(Modifiers.Contains(Peek().text))
      Next();
    var kw = Next();
    if(kw.text != "class" && kw.text != "struct")
      throw Error(kw, $"expected a class declaration, got {kw}");
    string class_name = ExpectIdent();

    var outer = cls;
    string full_name = (outer != null ? outer.full_name : string.Join(".", namespaces)).Trim('.');
    full_name = (full_name.Length > 0 ? full_name + "." : "") + class_name;
    if(!prog.classes.TryGetValue(full_name, out cls))
    {
      cls = new CsClass() { full_name = full_name };
      prog.classes[full_name] = cls;
    }

    foreach(var a in attrs)
    {
      string short_name = a.name.Split('.').Last();
      if(short_name != "BhlBinding" && short_name != "BhlBindingAttribute")
        continue;
      if(a.args.Count != 2)
        throw new CsBindingsException(file, a.line, "[BhlBinding] expects (name, version) string literals");
      prog.declared.Add(((string)a.args[0], (string)a.args[1]));
    }

    //NOTE: of the base classes and interfaces only IUserBindings matters (see Run)
    while(!Is("{"))
    {
      var t = Next();
      if(t.kind == CsTok.EOF)
        throw Error(t, "unterminated class declaration");
      if(t.text == "IUserBindings")
        cls.is_user_bindings = true;
    }
    Expect("{");

    while(true)
    {
      if(Accept("}"))
      {
        cls = outer;
        break;
      }
      var member_attrs = new List<(string name, List<object> args, int line)>();
      while(Is("["))
        member_attrs.AddRange(ParseAttributes());

      int save = pos;
      while(Modifiers.Contains(Peek().text))
        Next();
      if(Is("class") || Is("struct"))
      {
        pos = save;
        ParseClass(member_attrs);
        continue;
      }
      if(Accept("const"))
      {
        SkipToSemicolon();
        continue;
      }

      var start = Peek();
      ParseTypeName();
      string name = ExpectIdent();

      if(Is("("))
        ParseMethod(name, start);
      else if(Accept(";"))
      {}
      else if(Is("="))
        SkipToSemicolon();
      else
        throw Error(Peek(), $"unsupported class member '{name}'");
    }
  }

  void SkipToSemicolon()
  {
    int depth = 0;
    while(true)
    {
      var t = Next();
      if(t.kind == CsTok.EOF)
        throw Error(t, "expected ';'");
      if(t.text == "(" || t.text == "{" || t.text == "[")
        ++depth;
      else if(t.text == ")" || t.text == "}" || t.text == "]")
        --depth;
      else if(t.text == ";" && depth == 0)
        return;
    }
  }

  string ParseTypeName()
  {
    string name = ParseQualifiedName();
    if(Is("<"))
    {
      Next();
      var gargs = new List<string>() { ParseTypeName() };
      while(Accept(","))
        gargs.Add(ParseTypeName());
      Expect(">");
      name += "<" + string.Join(",", gargs) + ">";
    }
    while(Is("[") && Is("]", 1))
    {
      pos += 2;
      name += "[]";
    }
    if(Accept("?"))
      name += "?";
    return name;
  }

  void ParseMethod(string name, CsToken start)
  {
    var m = new CsMethod() { cls = cls, file = file, line = start.line, name = name };
    Expect("(");
    if(!Is(")"))
    {
      do
      {
        while(Is("ref") || Is("out") || Is("in") || Is("this") || Is("params"))
          Next();
        ParseTypeName();
        m.args.Add(ExpectIdent());
        if(Is("="))
          throw Error(Peek(), "default parameter values are not supported");
      }
      while(Accept(","));
    }
    Expect(")");
    if(!Is("{"))
      throw Error(Peek(), $"method '{name}' must have a body");
    m.body = ParseBlock();

    if(cls.methods.ContainsKey(name))
      throw Error(start, $"method '{cls.full_name}.{name}' is declared more than once (overloads are not supported)");
    cls.methods[name] = m;
  }

  CsBlock ParseBlock()
  {
    var block = new CsBlock() { line = Expect("{").line };
    while(!Accept("}"))
    {
      if(Peek().kind == CsTok.EOF)
        throw Error(Peek(), "unterminated block");
      var st = ParseStmt();
      if(st != null)
        block.stmts.Add(st);
    }
    return block;
  }

  //NOTE: returns null for declarations which have no effect (local functions)
  CsStmt ParseStmt()
  {
    var t = Peek();

    if(Is("{"))
      return ParseBlock();

    if(Is("if"))
      return ParseDefineIfAbsent();

    if(Is("return") || Is("for") || Is("foreach") || Is("while") || Is("do") || Is("switch") ||
       Is("try") || Is("throw") || Is("goto") || Is("break") || Is("continue") || Is("else"))
      throw Error(t, $"'{t.text}' is not supported: bindings must be straight-line code");

    if(Accept("const") || Is("var"))
    {
      if(!Accept("var"))
        ParseTypeName();
      return ParseVarDeclRest(t);
    }

    //NOTE: a local function (its body is native code) or a typed local variable
    if(Is("static"))
    {
      Next();
      ParseTypeName();
      ExpectIdent();
      SkipLocalFunction();
      return null;
    }

    int save = pos;
    if(TryParseTypeAndIdent())
    {
      if(Is("("))
      {
        SkipLocalFunction();
        return null;
      }
      pos = save;
      ParseTypeName();
      return ParseVarDeclRest(t);
    }
    pos = save;

    var expr = ParseExpr();
    if(Is("="))
      throw Error(Peek(), "assignments are not supported: bindings must be straight-line calls");
    Expect(";");
    if(!(expr is CsCall call))
      throw Error(t, "only calls are allowed as statements");
    return new CsExprStmt() { line = t.line, call = call };
  }

  bool TryParseTypeAndIdent()
  {
    if(Peek().kind != CsTok.Ident)
      return false;
    try
    {
      ParseTypeName();
    }
    catch(CsBindingsException)
    {
      return false;
    }
    if(Peek().kind != CsTok.Ident)
      return false;
    Next();
    return Is("(") || Is("=") || Is(";");
  }

  CsStmt ParseVarDeclRest(CsToken start)
  {
    string name = ExpectIdent();
    if(!Accept("="))
      throw Error(Peek(), $"local '{name}' must be initialized");
    var expr = ParseExpr();
    Expect(";");
    return new CsVarDecl() { line = start.line, name = name, expr = expr };
  }

  void SkipLocalFunction()
  {
    Expect("(");
    int depth = 1;
    while(depth > 0)
    {
      var t = Next();
      if(t.kind == CsTok.EOF)
        throw Error(t, "unterminated local function");
      if(t.text == "(")
        ++depth;
      else if(t.text == ")")
        --depth;
    }
    if(Accept("=>"))
    {
      SkipToSemicolon();
      return;
    }
    Expect("{");
    depth = 1;
    while(depth > 0)
    {
      var t = Next();
      if(t.kind == CsTok.EOF)
        throw Error(t, "unterminated local function");
      if(t.text == "{")
        ++depth;
      else if(t.text == "}")
        --depth;
    }
  }

  CsStmt ParseDefineIfAbsent()
  {
    var t = Expect("if");
    Expect("(");
    var cond = ParseExpr();
    if(!(cond is CsCall call) || !(call.target is CsMember m) || m.name != "Resolve" || call.args.Count != 1 ||
       !Accept("==") || !Accept("null"))
      throw Error(t, "only 'if(<scope>.Resolve(<name>) == null)' is supported: bindings must be straight-line code");
    Expect(")");
    var body = ParseStmt();
    if(Is("else"))
      throw Error(Peek(), "'else' is not supported");
    return new CsDefineIfAbsent() { line = t.line, scope = m.target, name = call.args[0].expr, body = body };
  }

  CsExpr ParseExpr()
  {
    var e = ParseOperand();
    while(Is("|"))
    {
      var t = Next();
      e = new CsBitOr() { line = t.line, left = e, right = ParseOperand() };
    }
    return e;
  }

  CsExpr ParseOperand()
  {
    var t = Peek();
    CsExpr e;

    if(t.kind == CsTok.Int || t.kind == CsTok.Float || t.kind == CsTok.String || t.kind == CsTok.Char)
    {
      Next();
      e = new CsLit() { line = t.line, value = t.value };
    }
    else if(Is("-") && (Peek(1).kind == CsTok.Int || Peek(1).kind == CsTok.Float))
    {
      Next();
      var n = Next();
      object v = n.value;
      if(v is int i)
        v = -i;
      else if(v is long l)
        v = -l;
      else if(v is float f)
        v = -f;
      else
        v = -(double)v;
      e = new CsLit() { line = t.line, value = v };
    }
    else if(Accept("true") || Accept("false"))
      e = new CsLit() { line = t.line, value = t.text == "true" };
    else if(Accept("null"))
      e = new CsLit() { line = t.line, value = null };
    else if(Accept("new"))
      e = ParseNew(t);
    else if(Is("("))
      e = ParseParenOrCast(t);
    else if(Is("typeof") || Is("default") || Is("nameof") || Is("this") || Is("base"))
      throw Error(t, $"'{t.text}' is not supported");
    else if(t.kind == CsTok.Ident)
    {
      Next();
      e = new CsName() { line = t.line, name = t.text };
    }
    else
      throw Error(t, $"unexpected {t}");

    while(true)
    {
      if(Is("=>"))
        throw Error(Peek(), "lambdas are not supported");
      if(Accept("."))
        e = new CsMember() { line = Peek(-1).line, target = e, name = ExpectIdent() };
      else if(Is("("))
        e = new CsCall() { line = Peek().line, target = e, args = ParseArgs() };
      else
        break;
    }
    return e;
  }

  CsExpr ParseNew(CsToken t)
  {
    string type = ParseQualifiedName();
    var gargs = new List<string>();
    if(Accept("<"))
    {
      gargs.Add(ParseTypeName());
      while(Accept(","))
        gargs.Add(ParseTypeName());
      Expect(">");
    }
    if(Is("[") && Is("]", 1))
    {
      pos += 2;
      Expect("{");
      var items = new List<CsExpr>();
      while(!Accept("}"))
      {
        items.Add(ParseExpr());
        if(!Is("}"))
          Expect(",");
      }
      return new CsNewArray() { line = t.line, elem_type = type, items = items };
    }
    var args = ParseArgs();
    List<CsExpr> init = null;
    if(Is("{"))
    {
      if(type != "List" && type != "System.Collections.Generic.List")
        throw Error(Peek(), "object initializers are not supported");
      Next();
      init = new List<CsExpr>();
      while(!Accept("}"))
      {
        init.Add(ParseExpr());
        if(!Is("}"))
          Expect(",");
      }
    }
    return new CsNew() { line = t.line, type = type, generic_args = gargs, args = args, items = init };
  }

  CsExpr ParseParenOrCast(CsToken t)
  {
    //NOTE: '(' ... ')' '=>' is a lambda
    int depth = 0;
    for(int i = 0; ; ++i)
    {
      var p = Peek(i);
      if(p.kind == CsTok.EOF)
        break;
      if(p.text == "(")
        ++depth;
      else if(p.text == ")" && --depth == 0)
      {
        if(Is("=>", i + 1))
          throw Error(t, "lambdas are not supported");
        break;
      }
    }

    //NOTE: '(' QualifiedName ')' followed by the start of an operand is a cast
    int close = 1;
    while(Peek(close).kind == CsTok.Ident && Is(".", close + 1))
      close += 2;
    if(Peek(close).kind == CsTok.Ident && Is(")", close + 1))
    {
      var after = Peek(close + 2);
      bool operand = after.kind == CsTok.Int || after.kind == CsTok.Float || after.kind == CsTok.String ||
                     after.kind == CsTok.Char || after.kind == CsTok.Ident || after.text == "(" ||
                     (after.text == "-" && (PrimitiveTypes.Contains(Peek(close).text)));
      if(operand)
      {
        Next();
        string type = ParseQualifiedName();
        Expect(")");
        return new CsCast() { line = t.line, type = type, expr = ParseOperand() };
      }
    }

    Expect("(");
    var e = ParseExpr();
    Expect(")");
    return e;
  }

  List<CsArg> ParseArgs()
  {
    var args = new List<CsArg>();
    Expect("(");
    if(Accept(")"))
      return args;
    do
    {
      string name = null;
      if(Peek().kind == CsTok.Ident && Is(":", 1))
      {
        name = Next().text;
        Next();
      }
      if(Is("ref") || Is("out") || Is("in"))
        throw Error(Peek(), $"'{Peek().text}' arguments are not supported");
      args.Add(new CsArg() { name = name, expr = ParseExpr() });
    }
    while(Accept(","));
    Expect(")");
    return args;
  }
}

///////////////////////////////////////////////////

//NOTE: an identifier which is neither a local nor a known type: a method group of native
//      code (e.g. a field getter). Allowed only where a delegate is expected
class CsNativeRef
{
  public string name;

  public override string ToString()
  {
    return name;
  }
}

class CsTypeRef
{
  public System.Type type;
}

class CsInterpreter
{
  const int MaxCallDepth = 64;

  static readonly Dictionary<string, System.Type> Primitives = new Dictionary<string, System.Type>()
  {
    { "object", typeof(object) }, { "string", typeof(string) }, { "bool", typeof(bool) },
    { "byte", typeof(byte) }, { "sbyte", typeof(sbyte) }, { "short", typeof(short) },
    { "ushort", typeof(ushort) }, { "int", typeof(int) }, { "uint", typeof(uint) },
    { "long", typeof(long) }, { "ulong", typeof(ulong) }, { "float", typeof(float) },
    { "double", typeof(double) }, { "char", typeof(char) },
    { "System.Type", typeof(System.Type) }, { "System.Object", typeof(object) }
  };

  static Dictionary<string, System.Type> _bhl_types;
  static List<MethodInfo> _extensions;

  readonly CsBindingsProgram prog;
  string file;
  CsClass cls;
  int depth;

  public CsInterpreter(CsBindingsProgram prog)
  {
    this.prog = prog;
  }

  //NOTE: public types of the bhl namespace by short and qualified names, nested ones as Outer.Inner
  static Dictionary<string, System.Type> BhlTypes()
  {
    if(_bhl_types != null)
      return _bhl_types;
    var res = new Dictionary<string, System.Type>();
    foreach(var t in typeof(Types).Assembly.GetExportedTypes())
    {
      if(t.Namespace != "bhl")
        continue;
      string name = t.IsGenericTypeDefinition ? t.Name.Substring(0, t.Name.IndexOf('`')) + "`" + t.GetGenericArguments().Length : t.Name;
      for(var outer = t.DeclaringType; outer != null; outer = outer.DeclaringType)
        name = outer.Name + "." + name;
      res[name] = t;
      res["bhl." + name] = t;
    }
    _bhl_types = res;
    return res;
  }

  static List<MethodInfo> Extensions()
  {
    if(_extensions != null)
      return _extensions;
    _extensions = typeof(Types).Assembly.GetExportedTypes()
      .Where(t => t.IsAbstract && t.IsSealed)
      .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
      .Where(m => m.IsDefined(typeof(ExtensionAttribute), false))
      .ToList();
    return _extensions;
  }

  static System.Type ResolveType(string name, List<string> generic_args = null)
  {
    if(Primitives.TryGetValue(name, out var p))
      return p;
    if(generic_args != null && generic_args.Count > 0)
    {
      System.Type gt;
      if(name == "List" || name == "System.Collections.Generic.List")
        gt = typeof(List<>);
      else if(!BhlTypes().TryGetValue(name + "`" + generic_args.Count, out gt))
        return null;
      var targs = generic_args.Select(a => ResolveType(a)).ToArray();
      if(targs.Any(a => a == null))
        return null;
      return gt.MakeGenericType(targs);
    }
    return BhlTypes().TryGetValue(name, out var t) ? t : null;
  }

  CsBindingsException Error(int line, string msg)
  {
    return new CsBindingsException(file, line, msg);
  }

  public void Call(CsMethod m, List<object> args, int line)
  {
    if(m.args.Count != args.Count)
      throw Error(line, $"'{m.name}' expects {m.args.Count} arguments, got {args.Count}");
    if(++depth > MaxCallDepth)
      throw Error(line, "calls are nested too deep");

    var caller_file = file;
    var caller_cls = cls;
    file = m.file;
    cls = m.cls;
    var scope = new Dictionary<string, object>();
    for(int i = 0; i < args.Count; ++i)
      scope[m.args[i]] = args[i];
    Exec(m.body, scope);
    file = caller_file;
    cls = caller_cls;
    --depth;
  }

  void Exec(CsStmt st, Dictionary<string, object> scope)
  {
    switch(st)
    {
      case CsBlock b:
        //NOTE: a block's locals are not seen after it
        var inner = new Dictionary<string, object>(scope);
        foreach(var s in b.stmts)
          Exec(s, inner);
        break;
      case CsVarDecl d:
        if(scope.ContainsKey(d.name))
          throw Error(d.line, $"'{d.name}' is already declared");
        scope[d.name] = Eval(d.expr, scope);
        break;
      case CsExprStmt e:
        Eval(e.call, scope);
        break;
      case CsDefineIfAbsent c:
        var target = Eval(c.scope, scope);
        var name = Eval(c.name, scope);
        if(InvokeMethod(target, "Resolve", new List<(string, object)>() { (null, name) }, c.line) == null)
          Exec(c.body, scope);
        break;
      default:
        throw Error(st.line, "unsupported statement");
    }
  }

  object Eval(CsExpr e, Dictionary<string, object> scope)
  {
    switch(e)
    {
      case CsLit l:
        return l.value;

      case CsName n:
        if(scope.TryGetValue(n.name, out var v))
          return v;
        var t = ResolveType(n.name);
        if(t != null)
          return new CsTypeRef() { type = t };
        return new CsNativeRef() { name = n.name };

      case CsMember m:
        return EvalMember(m, scope);

      case CsCall c:
        return EvalCall(c, scope);

      case CsNew nw:
      {
        var type = ResolveType(nw.type, nw.generic_args);
        if(type == null)
          throw Error(nw.line, $"unknown type '{nw.type}'");
        var args = EvalArgs(nw.args, scope);
        object obj;
        if(args.Count == 0 && type.IsValueType)
          obj = Activator.CreateInstance(type);
        else
          obj = InvokeBest(type.Name, type.GetConstructors().Cast<MethodBase>(), null, args, nw.line);
        if(nw.items != null)
        {
          var add = type.GetMethod("Add");
          var item_type = add.GetParameters()[0].ParameterType;
          for(int i = 0; i < nw.items.Count; ++i)
          {
            if(!TryConvert(Eval(nw.items[i], scope), item_type, out var item, out _))
              throw Error(nw.line, $"list item {i} is not a {item_type.Name}");
            add.Invoke(obj, new object[] { item });
          }
        }
        //NOTE: points to the registration code instead of where the reader is
        if(obj is Origin origin)
        {
          origin.native_file_path = file;
          origin.native_line = nw.line;
        }
        return obj;
      }

      case CsNewArray na:
      {
        var elem = ResolveType(na.elem_type);
        if(elem == null)
          throw Error(na.line, $"unknown type '{na.elem_type}'");
        var arr = Array.CreateInstance(elem, na.items.Count);
        for(int i = 0; i < na.items.Count; ++i)
        {
          if(!TryConvert(Eval(na.items[i], scope), elem, out var item, out _))
            throw Error(na.line, $"array item {i} is not a {elem.Name}");
          arr.SetValue(item, i);
        }
        return arr;
      }

      case CsBitOr bo:
      {
        var l = Eval(bo.left, scope);
        var r = Eval(bo.right, scope);
        if(l != null && r != null && l.GetType() == r.GetType() && l.GetType().IsEnum)
          return Enum.ToObject(l.GetType(), Convert.ToInt64(l) | Convert.ToInt64(r));
        if(l is int li && r is int ri)
          return li | ri;
        if(l != null && r != null && IsNumeric(l.GetType()) && IsNumeric(r.GetType()) &&
           !(l is float) && !(l is double) && !(r is float) && !(r is double))
          return Convert.ToInt64(l) | Convert.ToInt64(r);
        throw Error(bo.line, $"'|' of {Describe(l)} and {Describe(r)}");
      }

      case CsCast cs:
      {
        var val = Eval(cs.expr, scope);
        var type = ResolveType(cs.type);
        if(type == null)
          throw Error(cs.line, $"unknown type '{cs.type}'");
        //NOTE: a reference cast only checks the instance
        if(!type.IsValueType)
        {
          if(val != null && !type.IsInstanceOfType(val))
            throw Error(cs.line, $"'{Describe(val)}' is not a {cs.type}");
          return val;
        }
        try
        {
          return Convert.ChangeType(val, type, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch(Exception)
        {
          throw Error(cs.line, $"can't cast '{Describe(val)}' to {cs.type}");
        }
      }

      default:
        throw Error(e.line, "unsupported expression");
    }
  }

  object EvalMember(CsMember m, Dictionary<string, object> scope)
  {
    //NOTE: qualified names like System.Type or bhl.FuncAttrib
    if(m.target is CsName tn && !scope.ContainsKey(tn.name) && ResolveType(tn.name) == null)
    {
      var qt = ResolveType(tn.name + "." + m.name);
      if(qt != null)
        return new CsTypeRef() { type = qt };
    }

    var target = Eval(m.target, scope);
    if(target is CsNativeRef nr)
      throw Error(m.line, $"unknown name '{nr.name}'");

    if(target is CsTypeRef tr)
    {
      var nested = tr.type.GetNestedType(m.name);
      if(nested != null)
        return new CsTypeRef() { type = nested };
      if(tr.type.IsEnum)
        return Enum.Parse(tr.type, m.name);
      var sf = tr.type.GetField(m.name, BindingFlags.Public | BindingFlags.Static);
      if(sf != null)
        return sf.GetValue(null);
      var sp = tr.type.GetProperty(m.name, BindingFlags.Public | BindingFlags.Static);
      if(sp != null)
        return sp.GetValue(null);
      throw Error(m.line, $"'{tr.type.Name}' has no static member '{m.name}'");
    }

    if(target == null)
      throw Error(m.line, $"'{m.name}' of null");
    var f = target.GetType().GetField(m.name, BindingFlags.Public | BindingFlags.Instance);
    if(f != null)
      return f.GetValue(target);
    var p = target.GetType().GetProperty(m.name, BindingFlags.Public | BindingFlags.Instance);
    if(p != null && p.GetIndexParameters().Length == 0)
      return p.GetValue(target);
    throw Error(m.line, $"'{target.GetType().Name}' has no member '{m.name}'");
  }

  object EvalCall(CsCall c, Dictionary<string, object> scope)
  {
    var args = EvalArgs(c.args, scope);

    if(c.target is CsName fn && !scope.ContainsKey(fn.name))
    {
      if(!cls.methods.TryGetValue(fn.name, out var method))
        throw Error(c.line, $"unknown method '{fn.name}'");
      return CallSource(method, args, c.line);
    }

    if(!(c.target is CsMember m))
      throw Error(c.line, "unsupported call");

    //NOTE: a method of another class of the sources, by its full name or a trailing part of it
    string qualified = Dotted(m.target);
    if(qualified != null && !scope.ContainsKey(qualified.Split('.')[0]))
    {
      var others = prog.classes.Values
        .Where(k => k.full_name == qualified || k.full_name.EndsWith("." + qualified)).ToList();
      if(others.Count > 1)
        throw Error(c.line, $"'{qualified}' is ambiguous: {string.Join(", ", others.Select(k => k.full_name))}");
      if(others.Count == 1)
      {
        if(!others[0].methods.TryGetValue(m.name, out var method))
          throw Error(c.line, $"unknown method '{qualified}.{m.name}'");
        return CallSource(method, args, c.line);
      }
    }

    var target = Eval(m.target, scope);
    if(target is CsNativeRef nr)
      throw Error(c.line, $"unknown name '{nr.name}'");

    return InvokeMethod(target, m.name, args, c.line);
  }

  object CallSource(CsMethod method, List<(string name, object value)> args, int line)
  {
    if(args.Any(a => a.name != null))
      throw Error(line, $"named arguments are not supported for '{method.name}'");
    Call(method, args.Select(a => a.value).ToList(), line);
    return null;
  }

  static string Dotted(CsExpr e)
  {
    if(e is CsName n)
      return n.name;
    if(e is CsMember m)
    {
      var t = Dotted(m.target);
      return t == null ? null : t + "." + m.name;
    }
    return null;
  }

  List<(string name, object value)> EvalArgs(List<CsArg> args, Dictionary<string, object> scope)
  {
    return args.Select(a => (a.name, Eval(a.expr, scope))).ToList();
  }

  object InvokeMethod(object target, string name, List<(string name, object value)> args, int line)
  {
    if(target is CsTypeRef tr)
    {
      var statics = tr.type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(mi => mi.Name == name);
      return InvokeBest(tr.type.Name + "." + name, statics.Cast<MethodBase>(), null, args, line);
    }

    if(target == null)
      throw Error(line, $"'{name}' of null");

    var instance = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(mi => mi.Name == name).ToList();
    if(instance.Count > 0)
      return InvokeBest(target.GetType().Name + "." + name, instance.Cast<MethodBase>(), target, args, line);

    //NOTE: extension methods get the target as their first argument
    var ext = Extensions().Where(mi => mi.Name == name && mi.GetParameters()[0].ParameterType.IsInstanceOfType(target)).ToList();
    if(ext.Count == 0)
      throw Error(line, $"'{target.GetType().Name}' has no method '{name}'");
    var ext_args = new List<(string name, object value)>() { (null, target) };
    ext_args.AddRange(args);
    return InvokeBest(name, ext.Cast<MethodBase>(), null, ext_args, line);
  }

  object InvokeBest(string what, IEnumerable<MethodBase> candidates, object target, List<(string name, object value)> args, int line)
  {
    MethodBase best = null;
    object[] best_args = null;
    int best_score = -1;
    bool ambiguous = false;

    foreach(var cand in candidates)
    {
      if(!TryBind(cand, args, out var call_args, out int score))
        continue;
      if(score > best_score)
      {
        best = cand;
        best_args = call_args;
        best_score = score;
        ambiguous = false;
      }
      else if(score == best_score)
        ambiguous = true;
    }

    if(best == null)
      throw Error(line, $"no '{what}' matches arguments ({string.Join(", ", args.Select(a => (a.name != null ? a.name + ": " : "") + Describe(a.value)))})");
    if(ambiguous)
      throw Error(line, $"ambiguous call of '{what}'");

    try
    {
      if(best is ConstructorInfo ci)
        return ci.Invoke(best_args);
      return best.Invoke(target, best_args);
    }
    catch(TargetInvocationException ex)
    {
      throw Error(line, $"'{what}' failed: {ex.InnerException?.Message}");
    }
  }

  static string Describe(object v)
  {
    if(v == null)
      return "null";
    if(v is CsNativeRef nr)
      return "native " + nr.name;
    if(v is string s)
      return "\"" + s + "\"";
    return v.GetType().Name;
  }

  static bool TryBind(MethodBase m, List<(string name, object value)> args, out object[] call_args, out int score)
  {
    var ps = m.GetParameters();
    call_args = new object[ps.Length];
    score = 0;
    var bound = new bool[ps.Length];

    int positional = args.TakeWhile(a => a.name == null).Count();
    if(args.Skip(positional).Any(a => a.name == null))
      return false;

    bool has_params = ps.Length > 0 && ps[ps.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
    int fixed_count = has_params ? ps.Length - 1 : ps.Length;

    if(positional > fixed_count && !has_params)
      return false;

    for(int i = 0; i < Math.Min(positional, fixed_count); ++i)
    {
      if(!TryConvert(args[i].value, ps[i].ParameterType, out call_args[i], out int s))
        return false;
      score += s;
      bound[i] = true;
    }

    if(has_params && positional > fixed_count)
    {
      var arr_type = ps[ps.Length - 1].ParameterType;
      var elem_type = arr_type.GetElementType();
      int extra = positional - fixed_count;
      //NOTE: the normal form: an array passed as is
      if(extra == 1 && TryConvert(args[fixed_count].value, arr_type, out var as_arr, out int sa))
      {
        call_args[ps.Length - 1] = as_arr;
        score += sa;
      }
      else
      {
        var arr = Array.CreateInstance(elem_type, extra);
        for(int i = 0; i < extra; ++i)
        {
          if(!TryConvert(args[fixed_count + i].value, elem_type, out var item, out int s))
            return false;
          arr.SetValue(item, i);
          score += s;
        }
        call_args[ps.Length - 1] = arr;
      }
      bound[ps.Length - 1] = true;
    }

    foreach(var a in args.Skip(positional))
    {
      int idx = Array.FindIndex(ps, p => p.Name == a.name);
      if(idx == -1 || bound[idx])
        return false;
      if(!TryConvert(a.value, ps[idx].ParameterType, out call_args[idx], out int s))
        return false;
      score += s;
      bound[idx] = true;
    }

    for(int i = 0; i < ps.Length; ++i)
    {
      if(bound[i])
        continue;
      if(i == ps.Length - 1 && has_params)
        call_args[i] = Array.CreateInstance(ps[i].ParameterType.GetElementType(), 0);
      else if(ps[i].HasDefaultValue)
        call_args[i] = ps[i].DefaultValue;
      else
        return false;
    }
    return true;
  }

  static bool TryConvert(object arg, System.Type ptype, out object res, out int score)
  {
    res = null;
    score = 0;

    bool is_delegate = typeof(Delegate).IsAssignableFrom(ptype);

    if(arg is CsNativeRef)
    {
      if(!is_delegate)
        return false;
      res = Placeholder(ptype);
      score = 2;
      return true;
    }

    if(arg == null)
    {
      if(ptype.IsValueType && Nullable.GetUnderlyingType(ptype) == null)
        return false;
      score = 2;
      return true;
    }

    var atype = arg.GetType();
    if(ptype == atype)
    {
      res = arg;
      score = 3;
      return true;
    }
    if(ptype.IsAssignableFrom(atype))
    {
      res = arg;
      score = 2;
      return true;
    }

    var op = FindImplicit(atype, ptype);
    if(op != null)
    {
      res = op.Invoke(null, new object[] { arg });
      score = 1;
      return true;
    }

    if(IsNumeric(atype) && IsNumeric(ptype) && !ptype.IsEnum)
    {
      res = Convert.ChangeType(arg, ptype, System.Globalization.CultureInfo.InvariantCulture);
      score = 1;
      return true;
    }

    return false;
  }

  static MethodInfo FindImplicit(System.Type from, System.Type to)
  {
    var cands = to.GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Concat(from.GetMethods(BindingFlags.Public | BindingFlags.Static));
    foreach(var mi in cands)
    {
      if(mi.Name != "op_Implicit" || mi.ReturnType != to)
        continue;
      if(mi.GetParameters()[0].ParameterType.IsAssignableFrom(from))
        return mi;
    }
    return null;
  }

  static bool IsNumeric(System.Type t)
  {
    switch(System.Type.GetTypeCode(t))
    {
      case TypeCode.Byte:
      case TypeCode.SByte:
      case TypeCode.Int16:
      case TypeCode.UInt16:
      case TypeCode.Int32:
      case TypeCode.UInt32:
      case TypeCode.Int64:
      case TypeCode.UInt64:
      case TypeCode.Single:
      case TypeCode.Double:
        return true;
      default:
        return false;
    }
  }

  //NOTE: native code is not there: a delegate the front end checks for presence gets its
  //      placeholder, any other one is just null
  static object Placeholder(System.Type t)
  {
    if(t == typeof(FieldSymbol.FieldGetter))
      return FieldSymbol.PendingGetter;
    if(t == typeof(FieldSymbol.FieldSetter))
      return FieldSymbol.PendingSetter;
    if(t == typeof(VM.ClassCreator))
      return ClassSymbolNative.PendingCreator;
    return null;
  }
}

}

#endif

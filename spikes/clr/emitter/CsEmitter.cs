using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using bhl;

namespace bhl.spike.clr
{

// A first slice of a CLR backend: walks the same typed AST ModuleCompiler walks and writes C#
// instead of bytecode. Scalar functions, control flow and calls only - enough to run real
// scripts from the suite and compare against the VM, not enough to be a backend.
//
// Expressions are built on a stack because the AST is shaped for a stack machine: a node's
// children push their C# text, the node pops and combines.
public class CsEmitter : AST_Visitor
{
  public class NotSupported : Exception
  {
    public NotSupported(string what) : base("CLR spike doesn't do " + what) {}
  }

  StringBuilder _out = new StringBuilder();
  Stack<string> _exprs = new Stack<string>();
  int _indent;
  bool _in_coro;
  bool _in_method;
  bool _in_subclass;
  int _tmp;
  //NOTE: 'continue' lands before a for's increment, not at the condition the way C#'s does,
  //      so each loop gets a label and a real continue is a goto to it
  Stack<string> _cont_labels = new Stack<string>();

  const string RT = "bhl.spike.clr.BhlRt";

  public static string Emit(AST_Module ast, string class_name = "Gen", Namespace ns = null)
  {
    var e = new CsEmitter();
    e.Line("using System;");
    e.Line("using System.Collections.Generic;");
    e.Line("public static class " + class_name);
    e.Line("{");
    ++e._indent;
    //NOTE: the one native the spike knows, so trace based scripts can be compared
    e.Line("public static Action<string> __trace;");

    //NOTE: an interface is only a symbol, it has no AST node of its own
    if(ns != null)
      e.EmitInterfaces(ns);
    e.Visit(ast);
    --e._indent;
    e.Line("}");
    return e._out.ToString();
  }

  void Line(string s)
  {
    _out.Append(new string(' ', _indent * 2)).Append(s).Append('\n');
  }

  string Pop() { return _exprs.Pop(); }

  static string TypeOf(IType t)
  {
    if(t == null || t == Types.Void) return "void";
    if(t == Types.Int) return "long";
    if(t == Types.Float) return "double";
    if(t == Types.Bool) return "bool";
    if(t == Types.String) return "string";
    if(t is ArrayTypeSymbol arr) return "List<" + TypeOf(arr.item_type.Get()) + ">";
    if(t is ClassSymbolScript cs) return cs.name;
    if(t is InterfaceSymbolScript isy) return isy.name;
    if(t is MapTypeSymbol map)
      return "Dictionary<" + TypeOf(map.key_type.Get()) + ", " + TypeOf(map.val_type.Get()) + ">";
    if(t is FuncSignature sig)
    {
      var ps = sig.arg_types.Select(a => TypeOf(a.Get())).ToList();
      bool is_void = sig.return_type.Get() == Types.Void;
      if(is_void)
        return ps.Count == 0 ? "Action" : "Action<" + string.Join(", ", ps) + ">";
      ps.Add(TypeOf(sig.return_type.Get()));
      return "Func<" + string.Join(", ", ps) + ">";
    }
    throw new NotSupported("type '" + t + "'");
  }

  static string Name(Symbol s) { return "v_" + s.name; }

  //////////////////////////////// structure

  public override void DoVisit(AST_Module ast) { VisitChildren(ast); }
  public override void DoVisit(AST_Interim ast) { VisitChildren(ast); }
  public override void DoVisit(AST_Seq ast) { EmitBlock(ast); }
  public override void DoVisit(AST_FuncBody ast) { EmitBlock(ast); }

  //NOTE: 'defer' runs when the scope leaves, by any path, which is try/finally - so the rest
  //      of the block has to be emitted inside the try, and the emitter only learns a defer
  //      is there when it reaches it
  void EmitBlock(AST_Tree ast)
  {
    for(int i = 0; i < ast.children.Count; ++i)
    {
      if(!(ast.children[i] is AST_Defer def))
      {
        Visit(ast.children[i]);
        continue;
      }

      Line("try");
      Line("{");
      ++_indent;
      for(int j = i + 1; j < ast.children.Count; ++j)
        Visit(ast.children[j]);
      --_indent;
      Line("}");
      Line("finally");
      Line("{");
      ++_indent;
      VisitChildren(def);
      --_indent;
      Line("}");
      return;
    }
  }
  public override void DoVisit(AST_Import ast) { throw new NotSupported("imports"); }

  public override void DoVisit(AST_FuncDecl ast)
  {
    var fs = ast.symbol;
    bool was_coro = _in_coro;
    _in_coro = fs.attribs.HasFlag(FuncAttrib.Coro);

    var args = new List<string>();
    for(int i = 0; i < fs.GetTotalArgsNum(); ++i)
    {
      var a = fs.TryGetArg(i);
      args.Add(TypeOf(a.type.Get()) + " " + Name(a));
    }

    string mods = "public static ";
    if(_in_method)
    {
      if(fs.attribs.HasFlag(FuncAttrib.Virtual))
        mods = "public virtual ";
      else if(fs.attribs.HasFlag(FuncAttrib.Override))
        mods = "public override ";
      else
        mods = "public ";
    }
    if(_in_coro)
    {
      //NOTE: the value leaves through __ret, an IEnumerator can't return one
      args.Add(RT + ".Ret __ret");
      Line(mods + "IEnumerator<" + RT + ".Tick> " + fs.name +
           "(" + string.Join(", ", args) + ")");
    }
    else
      Line(mods + TypeOf(fs.GetReturnType()) + " " + fs.name +
           "(" + string.Join(", ", args) + ")");

    Line("{");
    ++_indent;
    //NOTE: child 0 is the params interim, child 1 the body
    Visit(ast.children[1]);
    --_indent;
    Line("}");

    _in_coro = was_coro;
  }

  public override void DoVisit(AST_If ast)
  {
    for(int b = 0; b < ast.BranchCount(); ++b)
    {
      Visit(ast.BranchCond(b));
      Line((b == 0 ? "if(" : "else if(") + Pop() + ")");
      Line("{");
      ++_indent;
      Visit(ast.BranchBody(b));
      --_indent;
      Line("}");
    }

    var els = ast.ElseBody();
    if(els != null)
    {
      Line("else");
      Line("{");
      ++_indent;
      Visit(els);
      --_indent;
      Line("}");
    }
  }

  static bool HasContinueMarker(IAST body)
  {
    //NOTE: a for's marker sits among the body's own statements, nested loops carry their own
    if(!(body is AST_Tree t))
      return false;
    foreach(var c in t.children)
      if(c is AST_Continue k && k.jump_marker)
        return true;
    return false;
  }

  public override void DoVisit(AST_While ast)
  {
    string label = "__cont" + (_tmp++);
    bool marked = HasContinueMarker(ast.children[1]);

    //NOTE: the condition is re-evaluated each pass, so it's emitted inside the loop
    Line("while(true)");
    Line("{");
    ++_indent;
    Visit(ast.children[0]);
    Line("if(!(" + Pop() + ")) break;");

    _cont_labels.Push(label);
    Visit(ast.children[1]);
    //NOTE: a plain while has no marker, so its continue lands at the end of the body
    if(!marked)
      Line(label + ": ;");
    _cont_labels.Pop();

    --_indent;
    Line("}");
  }

  public override void DoVisit(AST_DoWhile ast)
  {
    Line("do");
    Line("{");
    ++_indent;
    Visit(ast.children[0]);
    --_indent;
    Line("}");
    Visit(ast.children[1]);
    Line("while(" + Pop() + ");");
  }

  public override void DoVisit(AST_Break ast) { Line("break;"); }
  public override void DoVisit(AST_Continue ast)
  {
    if(ast.jump_marker)
      Line(_cont_labels.Peek() + ": ;");
    else
      Line("goto " + _cont_labels.Peek() + ";");
  }

  public override void DoVisit(AST_Return ast)
  {
    VisitChildren(ast);

    if(_in_coro)
    {
      if(_exprs.Count > 0)
        Line("__ret.val = " + Pop() + ";");
      Line("yield break;");
    }
    else
      Line(_exprs.Count > 0 ? "return " + Pop() + ";" : "return;");
  }

  public override void DoVisit(AST_Yield ast)
  {
    Line("yield return default(" + RT + ".Tick);");
  }

  //////////////////////////////// variables

  public override void DoVisit(AST_VarDecl ast)
  {
    var vs = ast.symb;
    Line(TypeOf(vs.type.Get()) + " " + Name(vs) + " = default(" + TypeOf(vs.type.Get()) + ");");
  }

  public override void DoVisit(AST_VarRead ast)
  {
    if(ast.symbol is FieldSymbol fld)
    {
      //NOTE: the instance was pushed by a preceding sibling
      _exprs.Push(Pop() + "." + MemberName(fld));
      return;
    }
    _exprs.Push(ast.symbol.name == "this" ? "this" : Name(ast.symbol));
  }

  //NOTE: an array's Count is a field on the native type, and C#'s List spells it the same
  static string MemberName(FieldSymbol f)
  {
    return f.scope is ArrayTypeSymbol ? f.name : "v_" + f.name;
  }

  public override void DoVisit(AST_VarWrite ast)
  {
    if(ast.symbol is FieldSymbol fld)
    {
      //NOTE: value pushed first, then the instance
      var inst = Pop();
      Line(inst + "." + MemberName(fld) + " = " + Pop() + ";");
      return;
    }

    var vs = (VariableSymbol)ast.symbol;
    Line((ast.is_decl ? TypeOf(vs.type.Get()) + " " : "") + Name(vs) + " = " + Pop() + ";");
  }

  public override void DoVisit(AST_Inc ast) { Line(Name(ast.symbol) + "++;"); }
  public override void DoVisit(AST_Dec ast) { Line(Name(ast.symbol) + "--;"); }

  void EmitInterfaces(Namespace ns)
  {
    foreach(var s in ns)
    {
      if(!(s is InterfaceSymbolScript itf))
        continue;

      Line("public interface " + itf.name);
      Line("{");
      ++_indent;
      foreach(var m in itf)
      {
        if(!(m is FuncSymbol f))
          continue;
        var ps = new List<string>();
        for(int i = 0; i < f.GetTotalArgsNum(); ++i)
        {
          var a = f.TryGetArg(i);
          ps.Add(TypeOf(a.type.Get()) + " " + Name(a));
        }
        Line(TypeOf(f.GetReturnType()) + " " + f.name + "(" + string.Join(", ", ps) + ");");
      }
      --_indent;
      Line("}");
    }
  }

  //////////////////////////////// lambdas and func pointers

  //NOTE: emitted as a local before the expression that uses it, so its body can be written as
  //      statements. C# closures capture by reference, which is what the VM does too
  public override void DoVisit(AST_LambdaDecl ast)
  {
    var fs = ast.symbol;
    string name = "__lmb" + (_tmp++);

    var ps = new List<string>();
    for(int i = 0; i < fs.GetTotalArgsNum(); ++i)
    {
      var a = fs.TryGetArg(i);
      ps.Add(TypeOf(a.type.Get()) + " " + Name(a));
    }

    Line(TypeOf(fs.signature) + " " + name + " = (" + string.Join(", ", ps) + ") =>");
    Line("{");
    ++_indent;

    bool was_coro = _in_coro;
    _in_coro = false;
    Visit(ast.children[1]);
    _in_coro = was_coro;

    --_indent;
    Line("};");

    _exprs.Push(name);
  }

  public override void DoVisit(AST_GetFuncAddr ast)
  {
    var fs = (FuncSymbol)ast.symbol;
    //NOTE: a method group needs the target type spelled out to become a delegate here
    _exprs.Push("((" + TypeOf(fs.signature) + ")" + fs.name + ")");
  }

  public override void DoVisit(AST_CallFuncPtrVar ast)
  {
    EmitPtrCall(ast, Name(ast.symbol), ((VariableSymbol)ast.symbol).type.Get() as FuncSignature);
  }

  public override void DoVisit(AST_CallFuncPtr ast)
  {
    //NOTE: the pointer was pushed by a preceding sibling, under the args
    var sig = (ast.symbol as ITyped)?.GetIType() as FuncSignature;
    if(sig == null)
      throw new NotSupported("calling a func pointer of unknown signature");

    VisitChildren(ast);

    int n = sig.arg_types.Count;
    var args = new string[n];
    for(int i = n - 1; i >= 0; --i)
      args[i] = Pop();

    string call = Pop() + "(" + string.Join(", ", args) + ")";
    if(sig.return_type.Get() == Types.Void)
      Line(call + ";");
    else
      _exprs.Push(call);
  }

  void EmitPtrCall(AST_Tree ast, string target, FuncSignature sig)
  {
    if(sig == null)
      throw new NotSupported("calling something that isn't a func pointer");

    VisitChildren(ast);

    int n = sig.arg_types.Count;
    var args = new string[n];
    for(int i = n - 1; i >= 0; --i)
      args[i] = Pop();

    string call = target + "(" + string.Join(", ", args) + ")";
    if(sig.return_type.Get() == Types.Void)
      Line(call + ";");
    else
      _exprs.Push(call);
  }

  //////////////////////////////// classes and arrays

  public override void DoVisit(AST_ClassDecl ast)
  {
    var cl = ast.symbol;

    var bases = new List<string>();
    if(cl.super_class != null)
      bases.Add(cl.super_class.name);
    for(int i = 0; i < cl.implements.Count; ++i)
      bases.Add(cl.implements[i].name);

    bool was_sub = _in_subclass;
    _in_subclass = cl.super_class != null;

    Line("public class " + cl.name + (bases.Count > 0 ? " : " + string.Join(", ", bases) : ""));
    Line("{");
    ++_indent;

    foreach(var m in cl)
    {
      //NOTE: an inherited field is already declared on the base
      if(m is VariableSymbol f && !(m is FuncSymbol) && m.scope == cl)
        Line("public " + TypeOf(f.type.Get()) + " " + Name(f) + ";");
    }

    bool was_method = _in_method;
    _in_method = true;
    VisitChildren(ast);
    _in_method = was_method;
    _in_subclass = was_sub;

    --_indent;
    Line("}");
  }

  public override void DoVisit(AST_New ast)
  {
    _exprs.Push("new " + TypeOf(ast.type) + "()");
  }

  public override void DoVisit(AST_Index ast)
  {
    //NOTE: pushed by preceding siblings: the value too, when it's a write
    var idx = Pop();
    var cont = Pop();

    //NOTE: a BHL int is a long, and C#'s List indexer wants an int - a map key is whatever
    //      its key type is and must be left alone
    string at = cont + "[" + (ast.is_map ? idx : "(int)" + idx) + "]";

    if(ast.is_write)
      Line(at + " = " + Pop() + ";");
    else
      _exprs.Push(at);
  }

  public override void DoVisit(AST_CallMethod ast)
  {
    VisitChildren(ast);

    var fs = (FuncSymbol)ast.symbol;
    int n = fs.GetTotalArgsNum();
    var args = new string[n];
    for(int i = n - 1; i >= 0; --i)
      args[i] = Pop();

    //NOTE: the instance was pushed before the args
    var inst = Pop();
    var call = inst + "." + fs.name + "(" + string.Join(", ", args) + ")";

    if(fs.GetReturnType() == Types.Void)
      Line(call + ";");
    else
      _exprs.Push(call);
  }

  public override void DoVisit(AST_JsonArr ast)
  {
    var items = new List<string>();
    for(int i = 0; i < ast.children.Count; ++i)
    {
      //NOTE: an add marker consumes the value the preceding child pushed
      if(ast.children[i] is AST_JsonArrAddItem)
        items.Add(Pop());
      else
        Visit(ast.children[i]);
    }
    if(ast.children.Count > 0)
      items.Add(Pop());

    _exprs.Push("new " + TypeOf(ast.type) + "() { " + string.Join(", ", items) + " }");
  }

  public override void DoVisit(AST_JsonArrAddItem ast) {}

  //////////////////////////////// expressions

  public override void DoVisit(AST_Literal ast)
  {
    switch(ast.type)
    {
      case ConstType.INT:  _exprs.Push(((long)ast.nval).ToString()); break;
      case ConstType.FLT:  _exprs.Push(ast.nval.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d"); break;
      case ConstType.BOOL: _exprs.Push(ast.nval == 1 ? "true" : "false"); break;
      case ConstType.STR:  _exprs.Push("\"" + ast.sval.Replace("\"", "\\\"") + "\""); break;
      case ConstType.NIL:  _exprs.Push("null"); break;
      default: throw new NotSupported("literal " + ast.type);
    }
  }

  static string OpOf(EnumBinaryOp op)
  {
    switch(op)
    {
      case EnumBinaryOp.ADD: return "+";
      case EnumBinaryOp.SUB: return "-";
      case EnumBinaryOp.MUL: return "*";
      case EnumBinaryOp.DIV: return "/";
      case EnumBinaryOp.MOD: return "%";
      case EnumBinaryOp.GT:  return ">";
      case EnumBinaryOp.GTE: return ">=";
      case EnumBinaryOp.LT:  return "<";
      case EnumBinaryOp.LTE: return "<=";
      case EnumBinaryOp.EQ:
      case EnumBinaryOp.EQ_SCLR:
      case EnumBinaryOp.EQ_STR: return "==";
      case EnumBinaryOp.AND: return "&&";
      case EnumBinaryOp.OR:  return "||";
      case EnumBinaryOp.BIT_AND: return "&";
      case EnumBinaryOp.BIT_OR:  return "|";
      case EnumBinaryOp.CONCAT:  return "+";
      default: throw new NotSupported("operator " + op);
    }
  }

  public override void DoVisit(AST_BinaryOpExp ast)
  {
    VisitChildren(ast);
    var rhs = Pop();
    var lhs = Pop();
    _exprs.Push("(" + lhs + " " + OpOf(ast.type) + " " + rhs + ")");
  }

  public override void DoVisit(AST_UnaryOpExp ast)
  {
    VisitChildren(ast);
    var v = Pop();
    _exprs.Push(ast.type == EnumUnaryOp.NOT ? "(!" + v + ")" : "(-" + v + ")");
  }

  public override void DoVisit(AST_TypeCast ast)
  {
    VisitChildren(ast);
    var v = Pop();
    var t = TypeOf(ast.type);
    //NOTE: BHL's (string) converts, C#'s cast from a number doesn't compile
    _exprs.Push(t == "string"
      ? "System.Convert.ToString(" + v + ", System.Globalization.CultureInfo.InvariantCulture)"
      : "((" + t + ")" + v + ")");
  }

  public override void DoVisit(AST_CallFunc ast)
  {
    if(ast.symbol is FuncSymbolNative nfs)
    {
      if(nfs.name != "trace")
        throw new NotSupported("native '" + nfs.name + "'");
      VisitChildren(ast);
      Line("__trace(" + Pop() + ");");
      return;
    }

    if(!(ast.symbol is FuncSymbolScript fs))
      throw new NotSupported("native calls");

    VisitChildren(ast);

    int n = fs.GetTotalArgsNum();
    var args = new string[n];
    for(int i = n - 1; i >= 0; --i)
      args[i] = Pop();

    if(fs.attribs.HasFlag(FuncAttrib.Coro))
    {
      if(!_in_coro)
        throw new NotSupported("calling a coro from a plain func");

      string r = "__r" + (_tmp++);
      Line("var " + r + " = new " + RT + ".Ret();");
      args = args.Concat(new string[] { r }).ToArray();

      string co = "__co" + (_tmp++);
      Line("var " + co + " = " + RT + ".Drive(" + fs.name + "(" + string.Join(", ", args) + "));");
      Line("while(" + co + ".MoveNext()) yield return default(" + RT + ".Tick);");

      if(fs.GetReturnType() != Types.Void)
        _exprs.Push(r + ".val");
      return;
    }

    var call = fs.name + "(" + string.Join(", ", args) + ")";
    if(fs.GetReturnType() == Types.Void)
      Line(call + ";");
    else
      _exprs.Push(call);
  }

  public override void DoVisit(AST_PopValue ast)
  {
    if(_exprs.Count > 0)
      Pop();
  }

  //////////////////////////////// everything else is out of scope for the slice



  public override void DoVisit(AST_Paral ast)    { EmitParal(ast, "Paral"); }
  public override void DoVisit(AST_ParalAll ast) { EmitParal(ast, "ParalAll"); }

  //NOTE: a branch is a local iterator function, which C# allows - that keeps each branch's
  //      own suspension points and its own finally blocks where they belong
  void EmitParal(AST_Tree ast, string driver)
  {
    if(!_in_coro)
      throw new NotSupported("paral outside a coro func");

    var names = new List<string>();
    for(int i = 0; i < ast.children.Count; ++i)
    {
      string n = "__br" + (_tmp++);
      names.Add(n);

      Line("IEnumerator<" + RT + ".Tick> " + n + "()");
      Line("{");
      ++_indent;
      Visit(ast.children[i]);
      //NOTE: a branch with no suspension point still has to be an iterator
      Line("yield break;");
      --_indent;
      Line("}");
    }

    string co = "__co" + (_tmp++);
    Line("var " + co + " = " + RT + "." + driver + "(" +
         string.Join(", ", names.Select(n => n + "()")) + ");");
    Line("while(" + co + ".MoveNext()) yield return default(" + RT + ".Tick);");
    Line(co + ".Dispose();");
  }
  //NOTE: handled by EmitBlock, which needs to see it in its block's context
  public override void DoVisit(AST_Defer ast)        { throw new NotSupported("defer outside a block"); }


  public override void DoVisit(AST_TypeAs ast)       { throw new NotSupported("as"); }
  public override void DoVisit(AST_TypeIs ast)       { throw new NotSupported("is"); }
  public override void DoVisit(AST_Typeof ast)       { throw new NotSupported("typeof"); }




  public override void DoVisit(AST_JsonObj ast)      { throw new NotSupported("json init"); }


  public override void DoVisit(AST_JsonMap ast)
  {
    //NOTE: pairs push key then value, a trailing add consumes each
    var pairs = new List<string>();
    foreach(var c in ast.children)
    {
      Visit(c);
      var v = Pop();
      var k = Pop();
      pairs.Add("{ " + k + ", " + v + " }");
    }
    _exprs.Push("new " + TypeOf(ast.type) + "() { " + string.Join(", ", pairs) + " }");
  }
  public override void DoVisit(AST_JsonMapAddItem ast) {}
  public override void DoVisit(AST_JsonPair ast) { VisitChildren(ast); }
}

}

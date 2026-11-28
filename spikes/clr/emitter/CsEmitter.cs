using System;
using System.Collections.Generic;
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

  public static string Emit(AST_Module ast, string class_name = "Gen")
  {
    var e = new CsEmitter();
    e.Line("using System;");
    e.Line("public static class " + class_name);
    e.Line("{");
    ++e._indent;
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
    throw new NotSupported("type '" + t + "'");
  }

  static string Name(Symbol s) { return "v_" + s.name; }

  //////////////////////////////// structure

  public override void DoVisit(AST_Module ast) { VisitChildren(ast); }
  public override void DoVisit(AST_Interim ast) { VisitChildren(ast); }
  public override void DoVisit(AST_Seq ast) { VisitChildren(ast); }
  public override void DoVisit(AST_FuncBody ast) { VisitChildren(ast); }
  public override void DoVisit(AST_Import ast) { throw new NotSupported("imports"); }

  public override void DoVisit(AST_FuncDecl ast)
  {
    var fs = ast.symbol;
    if(fs.attribs.HasFlag(FuncAttrib.Coro))
      throw new NotSupported("coroutines");

    var args = new List<string>();
    for(int i = 0; i < fs.GetTotalArgsNum(); ++i)
    {
      var a = fs.TryGetArg(i);
      args.Add(TypeOf(a.type.Get()) + " " + Name(a));
    }

    Line("public static " + TypeOf(fs.GetReturnType()) + " " + fs.name +
         "(" + string.Join(", ", args) + ")");
    Line("{");
    ++_indent;
    //NOTE: child 0 is the params interim, child 1 the body
    Visit(ast.children[1]);
    --_indent;
    Line("}");
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

  public override void DoVisit(AST_While ast)
  {
    //NOTE: the condition is re-evaluated each pass, so it's emitted inside the loop
    Line("while(true)");
    Line("{");
    ++_indent;
    Visit(ast.children[0]);
    Line("if(!(" + Pop() + ")) break;");
    Visit(ast.children[1]);
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
  public override void DoVisit(AST_Continue ast) { Line("continue;"); }

  public override void DoVisit(AST_Return ast)
  {
    VisitChildren(ast);
    Line(_exprs.Count > 0 ? "return " + Pop() + ";" : "return;");
  }

  //////////////////////////////// variables

  public override void DoVisit(AST_VarDecl ast)
  {
    var vs = ast.symb;
    Line(TypeOf(vs.type.Get()) + " " + Name(vs) + " = default(" + TypeOf(vs.type.Get()) + ");");
  }

  public override void DoVisit(AST_VarRead ast) { _exprs.Push(Name(ast.symbol)); }

  public override void DoVisit(AST_VarWrite ast)
  {
    var vs = (VariableSymbol)ast.symbol;
    //NOTE: the value was pushed by a preceding sibling, as in the bytecode
    Line((ast.is_decl ? TypeOf(vs.type.Get()) + " " : "") + Name(vs) + " = " + Pop() + ";");
  }

  public override void DoVisit(AST_Inc ast) { Line(Name(ast.symbol) + "++;"); }
  public override void DoVisit(AST_Dec ast) { Line(Name(ast.symbol) + "--;"); }

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
    _exprs.Push("((" + TypeOf(ast.type) + ")" + Pop() + ")");
  }

  public override void DoVisit(AST_CallFunc ast)
  {
    if(!(ast.symbol is FuncSymbolScript fs))
      throw new NotSupported("native calls");

    VisitChildren(ast);

    int n = fs.GetTotalArgsNum();
    var args = new string[n];
    for(int i = n - 1; i >= 0; --i)
      args[i] = Pop();

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

  public override void DoVisit(AST_LambdaDecl ast)   { throw new NotSupported("lambdas"); }
  public override void DoVisit(AST_ClassDecl ast)    { throw new NotSupported("classes"); }
  public override void DoVisit(AST_Paral ast)        { throw new NotSupported("paral"); }
  public override void DoVisit(AST_ParalAll ast)     { throw new NotSupported("paral_all"); }
  public override void DoVisit(AST_Defer ast)        { throw new NotSupported("defer"); }
  public override void DoVisit(AST_Yield ast)        { throw new NotSupported("yield"); }
  public override void DoVisit(AST_New ast)          { throw new NotSupported("new"); }
  public override void DoVisit(AST_Index ast)        { throw new NotSupported("indexing"); }
  public override void DoVisit(AST_TypeAs ast)       { throw new NotSupported("as"); }
  public override void DoVisit(AST_TypeIs ast)       { throw new NotSupported("is"); }
  public override void DoVisit(AST_Typeof ast)       { throw new NotSupported("typeof"); }
  public override void DoVisit(AST_CallMethod ast)   { throw new NotSupported("methods"); }
  public override void DoVisit(AST_GetFuncAddr ast)  { throw new NotSupported("func pointers"); }
  public override void DoVisit(AST_CallFuncPtr ast)  { throw new NotSupported("func pointers"); }
  public override void DoVisit(AST_CallFuncPtrVar ast) { throw new NotSupported("func pointers"); }
  public override void DoVisit(AST_JsonObj ast)      { throw new NotSupported("json init"); }
  public override void DoVisit(AST_JsonArr ast)      { throw new NotSupported("json init"); }
  public override void DoVisit(AST_JsonArrAddItem ast) { throw new NotSupported("json init"); }
  public override void DoVisit(AST_JsonMap ast)      { throw new NotSupported("json init"); }
  public override void DoVisit(AST_JsonMapAddItem ast) { throw new NotSupported("json init"); }
  public override void DoVisit(AST_JsonPair ast)     { throw new NotSupported("json init"); }
}

}

using System;
using System.Collections.Generic;

namespace bhl {

using marshall;

public abstract class AST_Visitor
{
  public abstract void DoVisit(AST_Interim node);
  public abstract void DoVisit(AST_Import node);
  public abstract void DoVisit(AST_Module node);
  public abstract void DoVisit(AST_VarDecl node);
  public abstract void DoVisit(AST_FuncDecl node);
  public abstract void DoVisit(AST_LambdaDecl node);
  public abstract void DoVisit(AST_ClassDecl node);
  public abstract void DoVisit(AST_EnumDecl node);
  public abstract void DoVisit(AST_Block node);
  public abstract void DoVisit(AST_TypeCast node);
  public abstract void DoVisit(AST_Call node);
  public abstract void DoVisit(AST_Return node);
  public abstract void DoVisit(AST_Break node);
  public abstract void DoVisit(AST_Continue node);
  public abstract void DoVisit(AST_PopValue node);
  public abstract void DoVisit(AST_Literal node);
  public abstract void DoVisit(AST_BinaryOpExp node);
  public abstract void DoVisit(AST_UnaryOpExp node);
  public abstract void DoVisit(AST_New node);
  public abstract void DoVisit(AST_Inc node);
  public abstract void DoVisit(AST_Dec node);
  public abstract void DoVisit(AST_JsonObj node);
  public abstract void DoVisit(AST_JsonArr node);
  public abstract void DoVisit(AST_JsonArrAddItem node);
  public abstract void DoVisit(AST_JsonPair node);

  public void Visit(IMarshallable node)
  {
    if(node == null)
      throw new Exception("NULL node");

    if(node is AST_Interim)
      DoVisit(node as AST_Interim);
    else if(node is AST_Block)
      DoVisit(node as AST_Block);
    else if(node is AST_Literal)
      DoVisit(node as AST_Literal);
    else if(node is AST_Call)
      DoVisit(node as AST_Call);
    else if(node is AST_VarDecl)
      DoVisit(node as AST_VarDecl);
    else if(node is AST_LambdaDecl)
      DoVisit(node as AST_LambdaDecl);
    //NOTE: base class must be handled after AST_LambdaDecl
    else if(node is AST_FuncDecl)
      DoVisit(node as AST_FuncDecl);
    else if(node is AST_ClassDecl)
      DoVisit(node as AST_ClassDecl);
    else if(node is AST_EnumDecl)
      DoVisit(node as AST_EnumDecl);
    else if(node is AST_TypeCast)
      DoVisit(node as AST_TypeCast);
    else if(node is AST_Return)
      DoVisit(node as AST_Return);
    else if(node is AST_Break)
      DoVisit(node as AST_Break);
    else if(node is AST_Continue)
      DoVisit(node as AST_Continue);
    else if(node is AST_PopValue)
      DoVisit(node as AST_PopValue);
    else if(node is AST_BinaryOpExp)
      DoVisit(node as AST_BinaryOpExp);
    else if(node is AST_UnaryOpExp)
      DoVisit(node as AST_UnaryOpExp);
    else if(node is AST_New)
      DoVisit(node as AST_New);
    else if(node is AST_Inc)
      DoVisit(node as AST_Inc);
    else if(node is AST_Dec)
      DoVisit(node as AST_Dec);
    else if(node is AST_JsonObj)
      DoVisit(node as AST_JsonObj);
    else if(node is AST_JsonArr)
      DoVisit(node as AST_JsonArr);
    else if(node is AST_JsonArrAddItem)
      DoVisit(node as AST_JsonArrAddItem);
    else if(node is AST_JsonPair)
      DoVisit(node as AST_JsonPair);
    else if(node is AST_Import)
      DoVisit(node as AST_Import);
    else if(node is AST_Module)
      DoVisit(node as AST_Module);
    else 
      throw new Exception("Not known type: " + node.GetType().Name);
  }

  public void VisitChildren(AST_Nested node)
  {
    if(node == null)
      return;
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
  }
}

public interface IPostProcessor
{
  //returns path to the result file
  string Patch(LazyAST lazy_ast, string src_file, string result_file);
  void Tally();
}

public class EmptyPostProcessor : IPostProcessor 
{
  public string Patch(LazyAST lazy_ast, string src_file, string result_file) { return result_file; }
  public void Tally() {}
}

public interface IASTResolver
{
  AST_Module Get();
}

public class LazyAST
{
  IASTResolver resolver;
  AST_Module resolved;

  public LazyAST(IASTResolver resolver)
  {
    this.resolver = resolver;
  }

  public LazyAST(AST_Module resolved)
  {
    this.resolved = resolved;
  }

  public AST_Module Get()
  {
    if(resolved == null)
      resolved = resolver.Get();
    return resolved;
  }
}

public class AST_Nested : IMarshallable
{
  public List<IMarshallable> children = new List<IMarshallable>();

  public virtual uint CLASS_ID() 
  {
    return 59352479; 
  }

  public virtual void Sync(SyncContext ctx) 
  {
    Marshall.SyncGeneric(ctx, children);
  }

  public virtual int GetFieldsNum() 
  {
    return 1; 
  }
}

public class AST_Interim : AST_Nested 
{
  public override uint CLASS_ID() 
  {
    return 240440595; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum(); 
  }
}

public class AST_Import  : IMarshallable
{
  public List<uint> module_ids = new List<uint>();
  public List<string> module_names = new List<string>();

  public uint CLASS_ID() 
  {
    return 117209009; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, module_ids);
    Marshall.Sync(ctx, module_names);
  }

  public int GetFieldsNum() 
  {
    return 2; 
  }
}

public class AST_Module : AST_Nested 
{
  public uint id;
  public string name = "";

  public override uint CLASS_ID() 
  {
    return 127311748; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    Marshall.Sync(ctx, ref id);
    Marshall.Sync(ctx, ref name);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 2; 
  }
}

public enum EnumUnaryOp 
{
  NEG = 1,
  NOT = 2,
}

public class AST_UnaryOpExp : AST_Nested 
{
  public EnumUnaryOp type = new EnumUnaryOp();

  public override uint CLASS_ID() 
  {
    return 224392343; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    int __tmp_type = (int)type;
    Marshall.Sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumUnaryOp)__tmp_type;
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public enum EnumBinaryOp 
{
  AND = 1,
  OR = 2,
  ADD = 3,
  SUB = 4,
  MUL = 5,
  DIV = 6,
  MOD = 7,
  GT = 8,
  LT = 9,
  GTE = 10,
  LTE = 11,
  EQ = 12,
  NQ = 13,
  BIT_OR = 14,
  BIT_AND = 15,
}

public class AST_BinaryOpExp  : AST_Nested 
{
  public EnumBinaryOp type = new EnumBinaryOp();

  public override uint CLASS_ID() 
  {
    return 78094287; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    int __tmp_type = (int)type;
    Marshall.Sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumBinaryOp)__tmp_type;
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_Inc : IMarshallable
{
  public uint symb_idx;

  public uint CLASS_ID() 
  {
    return 192507281; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref symb_idx);
  }

  public int GetFieldsNum() 
  {
    return 1; 
  }
}

public class AST_Dec : IMarshallable
{
  public uint symb_idx;

  public uint CLASS_ID() 
  {
    return 5580553; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref symb_idx);
  }

  public int GetFieldsNum() 
  {
    return 1; 
  }
}

public class AST_New : AST_Nested 
{
  public string type = "";

  public override uint CLASS_ID() 
  {
    return 119043746; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    Marshall.Sync(ctx, ref type);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_FuncDecl : AST_Nested 
{
  public string type = "";
  public string name = "";
  public uint module_id;
  public uint local_vars_num;
  public byte required_args_num;
  public byte default_args_num;
  public int ip_addr = -1;

  public override uint CLASS_ID() 
  {
    return 19638951; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref type);
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref module_id);
    Marshall.Sync(ctx, ref local_vars_num);
    Marshall.Sync(ctx, ref required_args_num);
    Marshall.Sync(ctx, ref default_args_num);
    Marshall.Sync(ctx, ref ip_addr);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 7; 
  }
}

public class AST_ClassDecl : AST_Nested 
{
  public string name = "";
  public string parent = "";

  public override uint CLASS_ID() 
  {
    return 168955538; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref parent);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 2; 
  }
}

public class AST_EnumItem : IMarshallable
{
  public string name;
  public int value;

  public uint CLASS_ID() 
  {
    return 42971075; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref value);
  }

  public int GetFieldsNum() 
  {
    return 2; 
  }
}

public class AST_EnumDecl : AST_Nested 
{
  public string name = "";

  public override uint CLASS_ID() 
  {
    return 207366473; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref name);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_UpVal : IMarshallable
{
  public string name = "";
  public uint symb_idx;
  public uint upsymb_idx;

  public uint CLASS_ID() 
  {
    return 121447213; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref symb_idx);
    Marshall.Sync(ctx, ref upsymb_idx);
  }

  public int GetFieldsNum() 
  {
    return 3; 
  }
}

public class AST_LambdaDecl : AST_FuncDecl 
{
  public List<AST_UpVal> upvals = new List<AST_UpVal>();

  public override uint CLASS_ID() 
  {
    return 44443142; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    Marshall.Sync(ctx, upvals);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_TypeCast : AST_Nested 
{
  public string type = "";

  public override uint CLASS_ID() 
  {
    return 234453676; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref type);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public enum EnumCall 
{
  VAR             = 1,
  VARW            = 2,
  MVAR            = 10,
  MVARW           = 11,
  MVARREF         = 12,
  FUNC            = 3,
  MFUNC           = 30,
  ARR_IDX         = 4,
  ARR_IDXW        = 40,
  GET_ADDR        = 5,
  FUNC_VAR        = 6,
  FUNC_MVAR        = 7,
  LMBD            = 8,
  GVAR            = 50,
  GVARW           = 51,
}

public class AST_Call  : AST_Nested 
{
  public EnumCall type = new EnumCall();
  public string name = "";
  public uint module_id;
  public uint cargs_bits;
  public int line_num;
  public int symb_idx;
  public string scope_type = "";

  public override uint CLASS_ID() 
  {
    return 42771415; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    int __tmp_type = (int)type;
    Marshall.Sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumCall)__tmp_type;

    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref module_id);
    Marshall.Sync(ctx, ref cargs_bits);
    Marshall.Sync(ctx, ref line_num);
    Marshall.Sync(ctx, ref symb_idx);
    Marshall.Sync(ctx, ref scope_type);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 7; 
  }
}

public class AST_Return  : AST_Nested 
{
  public int num;

  public override uint CLASS_ID() 
  {
    return 204244643; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);
    Marshall.Sync(ctx, ref num);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_Break : IMarshallable
{
  public uint CLASS_ID() 
  {
    return 93587594; 
  }

  public void Sync(SyncContext ctx) 
  {
  }

  public int GetFieldsNum() 
  {
    return 0; 
  }
}

public class AST_Continue : IMarshallable
{
  public bool jump_marker;

  public uint CLASS_ID() 
  {
    return 83587594; 
  }

  public void Sync(SyncContext ctx) 
  {
    Marshall.Sync(ctx, ref jump_marker);
  }

  public int GetFieldsNum() 
  {
    return 1; 
  }
}

public enum EnumLiteral 
{
  NUM = 1,
  BOOL = 2,
  STR = 3,
  NIL = 4,
}

public class AST_Literal : IMarshallable
{
  public EnumLiteral type = new EnumLiteral();
  public double nval;
  public string sval = "";

  public uint CLASS_ID() 
  {
    return 246902930; 
  }

  public void Sync(SyncContext ctx) 
  {
    int __tmp_type = (int)type;
    Marshall.Sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumLiteral)__tmp_type;

    Marshall.Sync(ctx, ref nval);
    Marshall.Sync(ctx, ref sval);
  }

  public int GetFieldsNum() 
  {
    return 3; 
  }
}

public class AST_VarDecl : AST_Nested 
{
  public string name = "";
  public string type = "";
  public uint symb_idx;
  public bool is_func_arg;
  public bool is_ref;

  public override uint CLASS_ID() 
  {
    return 232512499; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref symb_idx);
    Marshall.Sync(ctx, ref is_func_arg);
    Marshall.Sync(ctx, ref type);
    Marshall.Sync(ctx, ref is_ref);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 5; 
  }
}

public enum EnumBlock 
{
  FUNC = 0,
  SEQ = 1,
  DEFER = 2,
  PARAL = 3,
  PARAL_ALL = 4,
  IF = 7,
  WHILE = 8,
  FOR = 9,
}

public class AST_Block : AST_Nested 
{
  public EnumBlock type = new EnumBlock();

  public override uint CLASS_ID() 
  {
    return 183750514; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    int __tmp_type = (int)type;
    Marshall.Sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumBlock)__tmp_type;
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 1; 
  }
}

public class AST_JsonObj : AST_Nested 
{
  public string type = "";
  public int line_num;

  public override uint CLASS_ID() 
  {
    return 31901170; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref type);
    Marshall.Sync(ctx, ref line_num);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 2; 
  }
}

public class AST_JsonArr : AST_Nested 
{
  public string type;
  public int line_num;

  public override uint CLASS_ID() 
  {
    return 47604479; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref type);
    Marshall.Sync(ctx, ref line_num);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 2; 
  }
}

public class AST_JsonArrAddItem : IMarshallable
{
  public uint CLASS_ID() 
  {
    return 58382586; 
  }

  public void Sync(SyncContext ctx) 
  {
  }

  public int GetFieldsNum() 
  {
    return 0; 
  }
}

public class AST_JsonPair : AST_Nested 
{
  public string name = "";
  public uint symb_idx;
  public string scope_type = "";

  public override uint CLASS_ID() 
  {
    return 235544635; 
  }

  public override void Sync(SyncContext ctx) 
  {
    base.Sync(ctx);

    Marshall.Sync(ctx, ref name);
    Marshall.Sync(ctx, ref symb_idx);
    Marshall.Sync(ctx, ref scope_type);
  }

  public override int GetFieldsNum() 
  {
    return base.GetFieldsNum() + 3; 
  }
}

public class AST_PopValue : IMarshallable
{
  public uint CLASS_ID() 
  {
    return 87387238; 
  }

  public void Sync(SyncContext ctx) 
  {
  }

  public int GetFieldsNum() 
  {
    return 0; 
  }
}

public static class AST_Factory
{
  static public IMarshallable Create(uint id) 
  {
    switch(id)
    {
      case 59352479: { return new AST_Nested(); };
      case 240440595: { return new AST_Interim(); };
      case 117209009: { return new AST_Import(); };
      case 127311748: { return new AST_Module(); };
      case 224392343: { return new AST_UnaryOpExp(); };
      case 78094287: { return new AST_BinaryOpExp(); };
      case 192507281: { return new AST_Inc(); };
      case 5580553: { return new AST_Dec(); };
      case 119043746: { return new AST_New(); };
      case 19638951: { return new AST_FuncDecl(); };
      case 168955538: { return new AST_ClassDecl(); };
      case 42971075: { return new AST_EnumItem(); };
      case 207366473: { return new AST_EnumDecl(); };
      case 121447213: { return new AST_UpVal(); };
      case 44443142: { return new AST_LambdaDecl(); };
      case 234453676: { return new AST_TypeCast(); };
      case 42771415: { return new AST_Call(); };
      case 204244643: { return new AST_Return(); };
      case 93587594: { return new AST_Break(); };
      case 83587594: { return new AST_Continue(); };
      case 246902930: { return new AST_Literal(); };
      case 232512499: { return new AST_VarDecl(); };
      case 183750514: { return new AST_Block(); };
      case 31901170: { return new AST_JsonObj(); };
      case 47604479: { return new AST_JsonArr(); };
      case 58382586: { return new AST_JsonArrAddItem(); };
      case 235544635: { return new AST_JsonPair(); };
      case 87387238: { return new AST_PopValue(); };
      default: 
        return null;
    }
  }
}

} // namespace bhl

using System;
using System.Collections.Generic;

namespace bhl {

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

  public void Visit(AST_Base node)
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

  public void VisitChildren(AST node)
  {
    if(node == null)
      return;
    var children = node.children;
    for(int i=0;i<children.Count;++i)
      Visit(children[i]);
  }
}

public class AST_Base : BaseMetaStruct 
{
  static public  uint STATIC_CLASS_ID = 246837896;

  public override uint CLASS_ID() 
  {
    return 246837896; 
  }

  public AST_Base()
  {
    reset();
  }

  public override void reset() 
  {
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
  }

  public override int getFieldsCount() 
  {
    return 0; 
  }
}

public class AST  : AST_Base 
{
  public List<AST_Base> children = new List<AST_Base>();

  static public  new  uint STATIC_CLASS_ID = 59352479;

  public override uint CLASS_ID() 
  {
    return 59352479; 
  }

  public AST()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    if(children == null) children = new List<AST_Base>(); children.Clear();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.syncVirtual(ctx, children);
  }

  public override int getFieldsCount() 
  {
    return 1; 
  }
}

public class AST_Interim  : AST 
{
  static public  new  uint STATIC_CLASS_ID = 240440595;

  public override uint CLASS_ID() 
  {
    return 240440595; 
  }

  public AST_Interim()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount(); 
  }
}

public class AST_Import  : AST_Base 
{
  public List<uint> module_ids = new List<uint>();
  public List<string> module_names = new List<string>();

  static public  new  uint STATIC_CLASS_ID = 117209009;

  public override uint CLASS_ID() 
  {
    return 117209009; 
  }

  public AST_Import()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    if(module_ids == null) module_ids = new List<uint>(); module_ids.Clear();
    if(module_names == null) module_names = new List<string>(); module_names.Clear();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, module_ids);
    MetaHelper.sync(ctx, module_names);
  }

  public override int getFieldsCount() 
  {
    return 2; 
  }
}

public class AST_Module  : AST 
{
  public uint id;
  public string name = "";

  static public  new  uint STATIC_CLASS_ID = 127311748;

  public override uint CLASS_ID() 
  {
    return 127311748; 
  }

  public AST_Module()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    id = 0;
    name = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, ref id);
    MetaHelper.sync(ctx, ref name);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 2; 
  }
}

public enum EnumUnaryOp 
{
  NEG = 1,
  NOT = 2,
}

public class AST_UnaryOpExp  : AST 
{
  public EnumUnaryOp type = new EnumUnaryOp();

  static public  new  uint STATIC_CLASS_ID = 224392343;

  public override uint CLASS_ID() 
  {
    return 224392343; 
  }

  public AST_UnaryOpExp()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = new EnumUnaryOp(); 
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    int __tmp_type = (int)type;
    MetaHelper.sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumUnaryOp)__tmp_type;
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
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

public class AST_BinaryOpExp  : AST 
{
  public EnumBinaryOp type = new EnumBinaryOp();

  static public  new  uint STATIC_CLASS_ID = 78094287;

  public override uint CLASS_ID() 
  {
    return 78094287; 
  }

  public AST_BinaryOpExp()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = new EnumBinaryOp(); 
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    int __tmp_type = (int)type;
    MetaHelper.sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumBinaryOp)__tmp_type;
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_Inc  : AST_Base 
{
  public uint symb_idx;

  static public  new  uint STATIC_CLASS_ID = 192507281;

  public override uint CLASS_ID() 
  {
    return 192507281; 
  }

  public AST_Inc()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
    symb_idx = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, ref symb_idx);
  }

  public override int getFieldsCount() 
  {
    return 1; 
  }
}

public class AST_Dec  : AST_Base 
{
  public uint symb_idx;

  static public  new  uint STATIC_CLASS_ID = 5580553;

  public override uint CLASS_ID() 
  {
    return 5580553; 
  }

  public AST_Dec()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
    symb_idx = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, ref symb_idx);
  }

  public override int getFieldsCount() 
  {
    return 1; 
  }
}

public class AST_New  : AST 
{
  public string type = "";

  static public  new  uint STATIC_CLASS_ID = 119043746;

  public override uint CLASS_ID() 
  {
    return 119043746; 
  }

  public AST_New()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
    type = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, ref type);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_FuncDecl  : AST 
{
  public string type = "";
  public string name = "";
  public uint module_id;
  public uint local_vars_num;
  public byte required_args_num;
  public byte default_args_num;
  public int ip_addr;

  static public  new  uint STATIC_CLASS_ID = 19638951;

  public override uint CLASS_ID() 
  {
    return 19638951; 
  }

  public AST_FuncDecl()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = "";
    module_id = 0;
    name = "";
    local_vars_num = 0;
    required_args_num = 0;
    default_args_num = 0;
    ip_addr = -1;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref type);
    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref module_id);
    MetaHelper.sync(ctx, ref local_vars_num);
    MetaHelper.sync(ctx, ref required_args_num);
    MetaHelper.sync(ctx, ref default_args_num);
    MetaHelper.sync(ctx, ref ip_addr);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 7; 
  }
}

public class AST_ClassDecl  : AST 
{
  public string name = "";
  public string parent = "";

  static public  new  uint STATIC_CLASS_ID = 168955538;

  public override uint CLASS_ID() 
  {
    return 168955538; 
  }

  public AST_ClassDecl()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    name = "";
    parent = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref parent);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 2; 
  }
}

public class AST_EnumItem  : AST_Base 
{
  public string name;
  public int value;

  static public  new  uint STATIC_CLASS_ID = 42971075;

  public override uint CLASS_ID() 
  {
    return 42971075; 
  }


  public AST_EnumItem()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
    name = "";
    value = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref value);
  }

  public override int getFieldsCount() 
  {
    return 2; 
  }
}

public class AST_EnumDecl  : AST 
{
  public string name = "";

  static public  new  uint STATIC_CLASS_ID = 207366473;

  public override uint CLASS_ID() 
  {
    return 207366473; 
  }

  public AST_EnumDecl()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    name = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_UpVal  :  BaseMetaStruct 
{
  public string name = "";
  public uint symb_idx;
  public uint upsymb_idx;

  static public  uint STATIC_CLASS_ID = 121447213;

  public override uint CLASS_ID() 
  {
    return 121447213; 
  }

  public AST_UpVal()
  {
    reset();
  }

  public override void reset() 
  {
    name = "";
    symb_idx = 0;
    upsymb_idx = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref symb_idx);
    MetaHelper.sync(ctx, ref upsymb_idx);
  }

  public override int getFieldsCount() 
  {
    return 3; 
  }
}

public class AST_LambdaDecl  : AST_FuncDecl 
{
  public List<AST_UpVal> upvals = new List<AST_UpVal>();

  static public  new  uint STATIC_CLASS_ID = 44443142;

  public override uint CLASS_ID() 
  {
    return 44443142; 
  }

  public AST_LambdaDecl()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    if(upvals == null) upvals = new List<AST_UpVal>(); 
    upvals.Clear();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, upvals);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_TypeCast  : AST 
{
  public string type = "";

  static public  new  uint STATIC_CLASS_ID = 234453676;

  public override uint CLASS_ID() 
  {
    return 234453676; 
  }

  public AST_TypeCast()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref type);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
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

public class AST_Call  : AST 
{
  public EnumCall type = new EnumCall();
  public string name = "";
  public uint module_id;
  public uint cargs_bits;
  public uint line_num;
  public uint symb_idx;
  public string scope_type = "";

  static public  new  uint STATIC_CLASS_ID = 42771415;

  public override uint CLASS_ID() 
  {
    return 42771415; 
  }

  public AST_Call()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = new EnumCall(); 
    name = "";
    module_id = 0;
    cargs_bits = 0;
    line_num = 0;
    symb_idx = 0;
    scope_type = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    int __tmp_type = (int)type;
    MetaHelper.sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumCall)__tmp_type;

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref module_id);
    MetaHelper.sync(ctx, ref cargs_bits);
    MetaHelper.sync(ctx, ref line_num);
    MetaHelper.sync(ctx, ref symb_idx);
    MetaHelper.sync(ctx, ref scope_type);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 7; 
  }
}

public class AST_Return  : AST 
{
  static public  new  uint STATIC_CLASS_ID = 204244643;

  public int num;

  public override uint CLASS_ID() 
  {
    return 204244643; 
  }

  public AST_Return()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
    MetaHelper.sync(ctx, ref num);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_Break  : AST_Base 
{
  static public  new  uint STATIC_CLASS_ID = 93587594;

  public override uint CLASS_ID() 
  {
    return 93587594; 
  }

  public AST_Break()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
  }

  public override int getFieldsCount() 
  {
    return 0; 
  }
}

public class AST_Continue  : AST_Base 
{
  static public  new  uint STATIC_CLASS_ID = 83587594;
  public bool jump_marker;

  public override uint CLASS_ID() 
  {
    return 83587594; 
  }

  public AST_Continue()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref jump_marker);
  }

  public override int getFieldsCount() 
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

public class AST_Literal  : AST_Base 
{
  public EnumLiteral type = new EnumLiteral();
  public double nval;
  public string sval = "";

  static public  new  uint STATIC_CLASS_ID = 246902930;

  public override uint CLASS_ID() 
  {
    return 246902930; 
  }

  public AST_Literal()
  {
    reset();
  }


  public override void reset() 
  {
    base.reset();

    type = new EnumLiteral(); 
    nval = 0;
    sval = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    int __tmp_type = (int)type;
    MetaHelper.sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumLiteral)__tmp_type;


    MetaHelper.sync(ctx, ref nval);
    MetaHelper.sync(ctx, ref sval);
  }

  public override int getFieldsCount() 
  {
    return 3; 
  }
}

public class AST_VarDecl  : AST 
{
  public string name = "";
  public string type = "";
  public uint symb_idx;
  public bool is_func_arg;
  public bool is_ref;

  static public  new  uint STATIC_CLASS_ID = 232512499;

  public override uint CLASS_ID() 
  {
    return 232512499; 
  }

  public AST_VarDecl()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    name = "";
    type = "";
    symb_idx = 0;
    is_func_arg = false;
    is_ref = false;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref symb_idx);
    MetaHelper.sync(ctx, ref is_func_arg);
    MetaHelper.sync(ctx, ref type);
    MetaHelper.sync(ctx, ref is_ref);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 5; 
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

public class AST_Block  : AST 
{
  public EnumBlock type = new EnumBlock();

  static public  new  uint STATIC_CLASS_ID = 183750514;

  public override uint CLASS_ID() 
  {
    return 183750514; 
  }

  public AST_Block()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
    type = new EnumBlock(); 
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    int __tmp_type = (int)type;
    MetaHelper.sync(ctx, ref __tmp_type);
    if(ctx.is_read) type = (EnumBlock)__tmp_type;
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 1; 
  }
}

public class AST_JsonObj  : AST 
{
  public string type = "";
  public int line_num;

  static public  new  uint STATIC_CLASS_ID = 31901170;

  public override uint CLASS_ID() 
  {
    return 31901170; 
  }

  public AST_JsonObj()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = "";
    line_num = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref type);
    MetaHelper.sync(ctx, ref line_num);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 2; 
  }
}

public class AST_JsonArr  : AST 
{
  public string type;
  public int line_num;

  static public  new  uint STATIC_CLASS_ID = 47604479;

  public override uint CLASS_ID() 
  {
    return 47604479; 
  }

  public AST_JsonArr()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    type = "";
    line_num = 0;
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref type);
    MetaHelper.sync(ctx, ref line_num);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 2; 
  }
}

public class AST_JsonArrAddItem  : AST_Base 
{
  static public  new  uint STATIC_CLASS_ID = 58382586;

  public override uint CLASS_ID() 
  {
    return 58382586; 
  }

  public AST_JsonArrAddItem()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
  }

  public override int getFieldsCount() 
  {
    return 0; 
  }
}

public class AST_JsonPair  : AST 
{
  public string name = "";
  public uint symb_idx;
  public string scope_type = "";

  static public  new  uint STATIC_CLASS_ID = 235544635;

  public override uint CLASS_ID() 
  {
    return 235544635; 
  }

  public AST_JsonPair()
  {
    reset();
  }

  public override void reset() 
  {
    base.reset();

    name = "";
    symb_idx = 0;
    scope_type = "";
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);

    MetaHelper.sync(ctx, ref name);
    MetaHelper.sync(ctx, ref symb_idx);
    MetaHelper.sync(ctx, ref scope_type);
  }

  public override int getFieldsCount() 
  {
    return base.getFieldsCount() + 3; 
  }
}

public class AST_PopValue  : AST_Base 
{
  static public  new  uint STATIC_CLASS_ID = 87387238;

  public override uint CLASS_ID() 
  {
    return 87387238; 
  }

  public AST_PopValue()
  {
    reset();
  }


  public override void reset() 
  {
    base.reset();
  }

  public override void syncFields(MetaSyncContext ctx) 
  {
    base.syncFields(ctx);
  }

  public override int getFieldsCount() 
  {
    return 0; 
  }
}

public static class AST_Factory
{
  static public IMetaStruct createById(uint crc) 
  {
    switch(crc)
    {
      case 246837896: { return new AST_Base(); };
      case 59352479: { return new AST(); };
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

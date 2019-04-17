using System.Collections.Generic;

namespace bhl {

//TODO: migrate to manually written AST
//public class AST_Base : BaseMetaStruct 
//{
//  static public  uint STATIC_CLASS_ID = 246837896;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 246837896; 
//  }
//
//  public AST_Base()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Base)other);
//  }
//
//  public void copyFrom(AST_Base other)
//  {
//    reset();
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Base copy = new AST_Base();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 0; 
//  }
//}
//
//public class AST  : AST_Base 
//{
//  public List<AST_Base> children = new List<AST_Base>();
//
//  static public  new  uint STATIC_CLASS_ID = 59352479;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 59352479; 
//  }
//
//  public AST()
//  {
//    reset();
//  }
//
//  public override void reset() 
//  {
//    base.reset();
//
//    if(children == null) children = new List<AST_Base>(); children.Clear();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST)other);
//  }
//
//  public void copyFrom(AST other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    for(int i=0;other.children != null && i<other.children.Count;++i) 
//    { 
//      var tmp = (AST_Base)other.children[i].clone();
//      children.Add(tmp);
//    }
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST copy = new AST();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.syncVirtual(ctx, children);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 1; 
//  }
//}
//
//public class AST_Interim  : AST 
//{
//  static public  new  uint STATIC_CLASS_ID = 240440595;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 240440595; 
//  }
//
//  public AST_Interim()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Interim)other);
//  }
//
//  public void copyFrom(AST_Interim other)
//  {
//    reset();
//
//    base.copyFrom(other);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Interim copy = new AST_Interim();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 1; 
//  }
//}
//
//public class AST_Import  : AST_Base 
//{
//  public List<uint> modules = new List<uint>();
//
//  static public  new  uint STATIC_CLASS_ID = 117209009;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 117209009; 
//  }
//
//  public AST_Import()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    if(modules == null) modules = new List<uint>(); modules.Clear();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Import)other);
//  }
//
//  public void copyFrom(AST_Import other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    for(int i=0;other.modules != null && i<other.modules.Count;++i) 
//      modules.Add(other.modules[i]);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Import copy = new AST_Import();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, modules);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 1; 
//  }
//}
//
//public class AST_Module  : AST 
//{
//  public uint nname;
//  public string name = "";
//
//  static public  new  uint STATIC_CLASS_ID = 127311748;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 127311748; 
//  }
//
//  public AST_Module()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    nname = 0;
//    name = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Module)other);
//  }
//
//  public void copyFrom(AST_Module other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    nname = other.nname;
//    name = other.name;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Module copy = new AST_Module();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 3; 
//  }
//}
//
//public enum EnumUnaryOp 
//{
//  NEG = 1,
//  NOT = 2,
//}
//
//public class AST_UnaryOpExp  : AST 
//{
//  public EnumUnaryOp type = new EnumUnaryOp();
//
//  static public  new  uint STATIC_CLASS_ID = 224392343;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 224392343; 
//  }
//
//  public AST_UnaryOpExp()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    type = new EnumUnaryOp(); 
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_UnaryOpExp)other);
//  }
//
//  public void copyFrom(AST_UnaryOpExp other)
//  {
//    reset();
//    base.copyFrom(other);
//    type = other.type;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_UnaryOpExp copy = new AST_UnaryOpExp();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    int __tmp_type = (int)type;
//    MetaHelper.sync(ctx, ref __tmp_type);
//    if(ctx.is_read) type = (EnumUnaryOp)__tmp_type;
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public enum EnumBinaryOp 
//{
//  AND = 1,
//  OR = 2,
//  ADD = 3,
//  SUB = 4,
//  MUL = 5,
//  DIV = 6,
//  MOD = 7,
//  GT = 8,
//  LT = 9,
//  GTE = 10,
//  LTE = 11,
//  EQ = 12,
//  NQ = 13,
//  BIT_OR = 14,
//  BIT_AND = 15,
//}
//
//public class AST_BinaryOpExp  : AST 
//{
//  public EnumBinaryOp type = new EnumBinaryOp();
//
//  static public  new  uint STATIC_CLASS_ID = 78094287;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 78094287; 
//  }
//
//  public AST_BinaryOpExp()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    type = new EnumBinaryOp(); 
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_BinaryOpExp)other);
//  }
//
//  public void copyFrom(AST_BinaryOpExp other)
//  {
//    reset();
//    base.copyFrom(other);
//    type = other.type;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_BinaryOpExp copy = new AST_BinaryOpExp();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    int __tmp_type = (int)type;
//    MetaHelper.sync(ctx, ref __tmp_type);
//    if(ctx.is_read) type = (EnumBinaryOp)__tmp_type;
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_Inc  : AST_Base 
//{
//  public uint nname;
//
//  static public  new  uint STATIC_CLASS_ID = 192507281;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 192507281; 
//  }
//
//  public AST_Inc()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//    nname = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Inc)other);
//  }
//
//  public void copyFrom(AST_Inc other)
//  {
//    reset();
//    base.copyFrom(other);
//    nname = other.nname;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Inc copy = new AST_Inc();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref nname);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 1; 
//  }
//}
//
//public class AST_New  : AST 
//{
//  public uint ntype;
//  public string type = "";
//
//  static public  new  uint STATIC_CLASS_ID = 119043746;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 119043746; 
//  }
//
//  public AST_New()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//    ntype = 0;
//    type = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_New)other);
//  }
//
//  public void copyFrom(AST_New other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    ntype = other.ntype;
//    type = other.type;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_New copy = new AST_New();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref ntype);
//    MetaHelper.sync(ctx, ref type);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 3; 
//  }
//}
//
//public class AST_FuncDecl  : AST 
//{
//  public uint ntype;
//  public string type = "";
//  public uint nname1;
//  public uint nname2;
//  public string name = "";
//
//  static public  new  uint STATIC_CLASS_ID = 19638951;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 19638951; 
//  }
//
//  public AST_FuncDecl()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    ntype = 0;
//    type = "";
//    nname1 = 0;
//    nname2 = 0;
//    name = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_FuncDecl)other);
//  }
//
//  public void copyFrom(AST_FuncDecl other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    ntype = other.ntype;
//    type = other.type;
//    nname1 = other.nname1;
//    nname2 = other.nname2;
//    name = other.name;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_FuncDecl copy = new AST_FuncDecl();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref ntype);
//    MetaHelper.sync(ctx, ref type);
//    MetaHelper.sync(ctx, ref nname1);
//    MetaHelper.sync(ctx, ref nname2);
//    MetaHelper.sync(ctx, ref name);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 6; 
//  }
//}
//
//public class AST_ClassDecl  : AST 
//{
//  public uint nname;
//  public string name = "";
//  public uint nparent;
//  public string parent = "";
//
//  static public  new  uint STATIC_CLASS_ID = 168955538;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 168955538; 
//  }
//
//  public AST_ClassDecl()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    nname = 0;
//    name = "";
//    nparent = 0;
//    parent = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_ClassDecl)other);
//  }
//
//  public void copyFrom(AST_ClassDecl other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    nname = other.nname;
//    name = other.name;
//    nparent = other.nparent;
//    parent = other.parent;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_ClassDecl copy = new AST_ClassDecl();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//    MetaHelper.sync(ctx, ref nparent);
//    MetaHelper.sync(ctx, ref parent);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 5; 
//  }
//}
//
//public class AST_EnumItem  : AST_Base 
//{
//  public uint nname;
//  public int value;
//
//  static public  new  uint STATIC_CLASS_ID = 42971075;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 42971075; 
//  }
//
//
//  public AST_EnumItem()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//    nname = 0;
//    value = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_EnumItem)other);
//  }
//
//  public void copyFrom(AST_EnumItem other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    nname = other.nname;
//    value = other.value;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_EnumItem copy = new AST_EnumItem();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref value);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_EnumDecl  : AST 
//{
//  public uint nname;
//  public string name = "";
//
//  static public  new  uint STATIC_CLASS_ID = 207366473;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 207366473; 
//  }
//
//  public AST_EnumDecl()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    nname = 0;
//    name = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_EnumDecl)other);
//  }
//
//  public void copyFrom(AST_EnumDecl other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    nname = other.nname;
//    name = other.name;
//
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_EnumDecl copy = new AST_EnumDecl();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 3; 
//  }
//}
//
//public class AST_UseParam  :  BaseMetaStruct 
//{
//  public uint nname;
//  public string name = "";
//
//  static public  uint STATIC_CLASS_ID = 121447213;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 121447213; 
//  }
//
//  public AST_UseParam()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    nname = 0;
//    name = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_UseParam)other);
//  }
//
//  public void copyFrom(AST_UseParam other)
//  {
//    reset();
//    nname = other.nname;
//    name = other.name;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_UseParam copy = new AST_UseParam();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_LambdaDecl  : AST_FuncDecl 
//{
//  public List<AST_UseParam> useparams = new List<AST_UseParam>();
//
//  static public  new  uint STATIC_CLASS_ID = 44443142;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 44443142; 
//  }
//
//  public AST_LambdaDecl()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    if(useparams == null) useparams = new List<AST_UseParam>(); 
//    useparams.Clear();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_LambdaDecl)other);
//  }
//
//  public void copyFrom(AST_LambdaDecl other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    for(int i=0;other.useparams != null && i<other.useparams.Count;++i) 
//    { 
//      var tmp = new AST_UseParam(); tmp.copyFrom(other.useparams[i]);
//      useparams.Add(tmp);
//    }
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_LambdaDecl copy = new AST_LambdaDecl();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, useparams);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 7; 
//  }
//}
//
//public class AST_TypeCast  : AST 
//{
//  public uint ntype;
//  public string type = "";
//
//  static public  new  uint STATIC_CLASS_ID = 234453676;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 234453676; 
//  }
//
//  public AST_TypeCast()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    ntype = 0;
//    type = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_TypeCast)other);
//  }
//
//  public void copyFrom(AST_TypeCast other)
//  {
//    reset();
//    base.copyFrom(other);
//    ntype = other.ntype;
//    type = other.type;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_TypeCast copy = new AST_TypeCast();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//    MetaHelper.sync(ctx, ref ntype);
//    MetaHelper.sync(ctx, ref type);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 3; 
//  }
//}
//
//public enum EnumCall 
//{
//  VAR = 1,
//  VARW = 2,
//  MVAR = 10,
//  MVARW = 11,
//  MVARREF = 12,
//  FUNC = 3,
//  MFUNC = 30,
//  ARR_IDX = 4,
//  ARR_IDXW = 40,
//  FUNC2VAR = 5,
//  FUNC_PTR = 6,
//  FUNC_PTR_POP = 7,
//}
//
//public class AST_Call  : AST 
//{
//  public EnumCall type = new EnumCall();
//  public uint nname1;
//  public uint nname2;
//  public string name = "";
//  public uint cargs_bits;
//  public uint scope_ntype;
//  public uint line_num;
//
//  static public  new  uint STATIC_CLASS_ID = 42771415;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 42771415; 
//  }
//
//  public AST_Call()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    type = new EnumCall(); 
//    nname1 = 0;
//    nname2 = 0;
//    name = "";
//    cargs_bits = 0;
//    scope_ntype = 0;
//    line_num = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Call)other);
//  }
//
//  public void copyFrom(AST_Call other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    type = other.type;
//    nname1 = other.nname1;
//    nname2 = other.nname2;
//    name = other.name;
//    cargs_bits = other.cargs_bits;
//    scope_ntype = other.scope_ntype;
//    line_num = other.line_num;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Call copy = new AST_Call();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    int __tmp_type = (int)type;
//    MetaHelper.sync(ctx, ref __tmp_type);
//    if(ctx.is_read) type = (EnumCall)__tmp_type;
//
//    MetaHelper.sync(ctx, ref nname1);
//    MetaHelper.sync(ctx, ref nname2);
//    MetaHelper.sync(ctx, ref name);
//    MetaHelper.sync(ctx, ref cargs_bits);
//    MetaHelper.sync(ctx, ref scope_ntype);
//    MetaHelper.sync(ctx, ref line_num);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 8; 
//  }
//}
//
//public class AST_Return  : AST 
//{
//  static public  new  uint STATIC_CLASS_ID = 204244643;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 204244643; 
//  }
//
//  public AST_Return()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Return)other);
//  }
//
//  public void copyFrom(AST_Return other)
//  {
//    reset();
//
//    base.copyFrom(other);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Return copy = new AST_Return();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 1; 
//  }
//}
//
//public class AST_Break  : AST_Base 
//{
//  static public  new  uint STATIC_CLASS_ID = 93587594;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 93587594; 
//  }
//
//  public AST_Break()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Break)other);
//  }
//
//  public void copyFrom(AST_Break other)
//  {
//    reset();
//
//    base.copyFrom(other);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Break copy = new AST_Break();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 0; 
//  }
//}
//
//public enum EnumLiteral 
//{
//  NUM = 1,
//  BOOL = 2,
//  STR = 3,
//  NIL = 4,
//}
//
//public class AST_Literal  : AST_Base 
//{
//  public EnumLiteral type = new EnumLiteral();
//  public double nval;
//  public string sval = "";
//
//  static public  new  uint STATIC_CLASS_ID = 246902930;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 246902930; 
//  }
//
//  public AST_Literal()
//  {
//    reset();
//  }
//
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    type = new EnumLiteral(); 
//    nval = 0;
//    sval = "";
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Literal)other);
//  }
//
//  public void copyFrom(AST_Literal other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    type = other.type;
//    nval = other.nval;
//    sval = other.sval;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Literal copy = new AST_Literal();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    int __tmp_type = (int)type;
//    MetaHelper.sync(ctx, ref __tmp_type);
//    if(ctx.is_read) type = (EnumLiteral)__tmp_type;
//
//
//    MetaHelper.sync(ctx, ref nval);
//    MetaHelper.sync(ctx, ref sval);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 3; 
//  }
//}
//
//public class AST_VarDecl  : AST 
//{
//  public uint nname;
//  public string name = "";
//  public uint ntype;
//
//  static public  new  uint STATIC_CLASS_ID = 232512499;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 232512499; 
//  }
//
//  public AST_VarDecl()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    nname = 0;
//    name = "";
//    ntype = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_VarDecl)other);
//  }
//
//  public void copyFrom(AST_VarDecl other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    nname = other.nname;
//    name = other.name;
//    ntype = other.ntype;
//
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_VarDecl copy = new AST_VarDecl();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//    MetaHelper.sync(ctx, ref ntype);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 4; 
//  }
//}
//
//public enum EnumBlock 
//{
//  FUNC = 0,
//  SEQ = 1,
//  DEFER = 2,
//  PARAL = 3,
//  PARAL_ALL = 4,
//  PRIO = 5,
//  FOREVER = 6,
//  IF = 7,
//  WHILE = 8,
//  FOR = 9,
//  GROUP = 10,
//  UNTIL_FAILURE = 11,
//  UNTIL_SUCCESS = 12,
//  NOT = 13,
//  SEQ_ = 14,
//  EVAL = 15,
//}
//
//public class AST_Block  : AST 
//{
//  public EnumBlock type = new EnumBlock();
//
//  static public  new  uint STATIC_CLASS_ID = 183750514;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 183750514; 
//  }
//
//  public AST_Block()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//    type = new EnumBlock(); 
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_Block)other);
//  }
//
//  public void copyFrom(AST_Block other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    type = other.type;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_Block copy = new AST_Block();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    int __tmp_type = (int)type;
//    MetaHelper.sync(ctx, ref __tmp_type);
//    if(ctx.is_read) type = (EnumBlock)__tmp_type;
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_JsonObj  : AST 
//{
//  public uint ntype;
//
//  static public  new  uint STATIC_CLASS_ID = 31901170;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 31901170; 
//  }
//
//  public AST_JsonObj()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    ntype = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_JsonObj)other);
//  }
//
//  public void copyFrom(AST_JsonObj other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    ntype = other.ntype;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_JsonObj copy = new AST_JsonObj();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref ntype);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_JsonArr  : AST 
//{
//  public uint ntype;
//
//  static public  new  uint STATIC_CLASS_ID = 47604479;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 47604479; 
//  }
//
//  public AST_JsonArr()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    ntype = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_JsonArr)other);
//  }
//
//  public void copyFrom(AST_JsonArr other)
//  {
//    reset();
//
//    base.copyFrom(other);
//    ntype = other.ntype;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_JsonArr copy = new AST_JsonArr();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref ntype);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 2; 
//  }
//}
//
//public class AST_JsonArrAddItem  : AST_Base 
//{
//  static public  new  uint STATIC_CLASS_ID = 58382586;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 58382586; 
//  }
//
//  public AST_JsonArrAddItem()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_JsonArrAddItem)other);
//  }
//
//  public void copyFrom(AST_JsonArrAddItem other)
//  {
//    reset();
//
//    base.copyFrom(other);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_JsonArrAddItem copy = new AST_JsonArrAddItem();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 0; 
//  }
//}
//
//public class AST_JsonPair  : AST 
//{
//  public uint nname;
//  public string name = "";
//  public uint scope_ntype;
//
//  static public  new  uint STATIC_CLASS_ID = 235544635;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 235544635; 
//  }
//
//  public AST_JsonPair()
//  {
//    reset();
//  }
//
//  public  override  void reset() 
//  {
//    base.reset();
//
//    nname = 0;
//    name = "";
//    scope_ntype = 0;
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_JsonPair)other);
//  }
//
//  public void copyFrom(AST_JsonPair other)
//  {
//    reset();
//
//    base.copyFrom(other);
//
//    nname = other.nname;
//    name = other.name;
//    scope_ntype = other.scope_ntype;
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_JsonPair copy = new AST_JsonPair();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//
//    MetaHelper.sync(ctx, ref nname);
//    MetaHelper.sync(ctx, ref name);
//    MetaHelper.sync(ctx, ref scope_ntype);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 4; 
//  }
//}
//
//public class AST_PopValue  : AST_Base 
//{
//  static public  new  uint STATIC_CLASS_ID = 87387238;
//
//  public  override  uint CLASS_ID() 
//  {
//    return 87387238; 
//  }
//
//  public AST_PopValue()
//  {
//    reset();
//  }
//
//
//  public  override  void reset() 
//  {
//    base.reset();
//  }
//
//  public  override  void copy(IMetaStruct other)
//  {
//    copyFrom((AST_PopValue)other);
//  }
//
//  public void copyFrom(AST_PopValue other)
//  {
//    reset();
//
//    base.copyFrom(other);
//  }
//
//  public  override  IMetaStruct clone()
//  {
//    AST_PopValue copy = new AST_PopValue();
//    copy.copy(this);
//    return copy;
//  }
//
//  public  override  void syncFields(MetaSyncContext ctx) 
//  {
//    base.syncFields(ctx);
//  }
//
//  public  override  int getFieldsCount() 
//  {
//    return 0; 
//  }
//}
//
//public class AutogenBundle 
//{
//  static public IMetaStruct createById(uint crc) 
//  {
//    switch(crc)
//    {
//      case 246837896: { return new AST_Base(); };
//      case 59352479: { return new AST(); };
//      case 240440595: { return new AST_Interim(); };
//      case 117209009: { return new AST_Import(); };
//      case 127311748: { return new AST_Module(); };
//      case 224392343: { return new AST_UnaryOpExp(); };
//      case 78094287: { return new AST_BinaryOpExp(); };
//      case 192507281: { return new AST_Inc(); };
//      case 119043746: { return new AST_New(); };
//      case 19638951: { return new AST_FuncDecl(); };
//      case 168955538: { return new AST_ClassDecl(); };
//      case 42971075: { return new AST_EnumItem(); };
//      case 207366473: { return new AST_EnumDecl(); };
//      case 121447213: { return new AST_UseParam(); };
//      case 44443142: { return new AST_LambdaDecl(); };
//      case 234453676: { return new AST_TypeCast(); };
//      case 42771415: { return new AST_Call(); };
//      case 204244643: { return new AST_Return(); };
//      case 93587594: { return new AST_Break(); };
//      case 246902930: { return new AST_Literal(); };
//      case 232512499: { return new AST_VarDecl(); };
//      case 183750514: { return new AST_Block(); };
//      case 31901170: { return new AST_JsonObj(); };
//      case 47604479: { return new AST_JsonArr(); };
//      case 58382586: { return new AST_JsonArrAddItem(); };
//      case 235544635: { return new AST_JsonPair(); };
//      case 87387238: { return new AST_PopValue(); };
//      default: 
//        return null;
//    }
//  }
//
//  static public System.Type id2type(uint crc) 
//  {
//    switch(crc) 
//    {
//      case 246837896: { return typeof(AST_Base); };
//      case 59352479: { return typeof(AST); };
//      case 240440595: { return typeof(AST_Interim); };
//      case 117209009: { return typeof(AST_Import); };
//      case 127311748: { return typeof(AST_Module); };
//      case 224392343: { return typeof(AST_UnaryOpExp); };
//      case 78094287: { return typeof(AST_BinaryOpExp); };
//      case 192507281: { return typeof(AST_Inc); };
//      case 119043746: { return typeof(AST_New); };
//      case 19638951: { return typeof(AST_FuncDecl); };
//      case 168955538: { return typeof(AST_ClassDecl); };
//      case 42971075: { return typeof(AST_EnumItem); };
//      case 207366473: { return typeof(AST_EnumDecl); };
//      case 121447213: { return typeof(AST_UseParam); };
//      case 44443142: { return typeof(AST_LambdaDecl); };
//      case 234453676: { return typeof(AST_TypeCast); };
//      case 42771415: { return typeof(AST_Call); };
//      case 204244643: { return typeof(AST_Return); };
//      case 93587594: { return typeof(AST_Break); };
//      case 246902930: { return typeof(AST_Literal); };
//      case 232512499: { return typeof(AST_VarDecl); };
//      case 183750514: { return typeof(AST_Block); };
//      case 31901170: { return typeof(AST_JsonObj); };
//      case 47604479: { return typeof(AST_JsonArr); };
//      case 58382586: { return typeof(AST_JsonArrAddItem); };
//      case 235544635: { return typeof(AST_JsonPair); };
//      case 87387238: { return typeof(AST_PopValue); };
//      default: 
//        return null;
//    }
//  }
//}

} // namespace bhl

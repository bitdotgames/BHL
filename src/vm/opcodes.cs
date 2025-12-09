namespace bhl
{

public enum Opcodes
{
  Nop                   = 0,
  Constant              = 1,
  Add                   = 2,
  Sub                   = 3,
  Div                   = 4,
  Mul                   = 5,

  SetVar                = 6,
  GetVar                = 7,
  GetVarScalar          = 8,
  SetVarScalar          = 9,
  DeclVar               = 10,
  SetGVar               = 11,
  GetGVar               = 12,

  Frame                 = 13,
  Return                = 14,
  Jump                  = 16,
  JumpZ                 = 17,
  JumpPeekZ             = 18,
  JumpPeekNZ            = 19,
  Pop                   = 20,

  CallLocal             = 21,
  CallGlobNative        = 22,
  Call                  = 23,
  CallNative            = 24,
  CallMethod            = 25,
  CallMethodNative      = 26,
  CallMethodIface       = 27,
  CallMethodIfaceNative = 28,
  CallMethodVirt        = 29,
  CallFuncPtr           = 30,
  //for a case when func pointer comes before args
  //TODO: if CallFuncPtr operated on stack offsets this one would not be necessary
  CallFuncPtrInv        = 31,

  GetFuncLocalPtr       = 32,
  GetFuncPtr            = 33,
  GetFuncNativePtr      = 34,
  GetFuncIpPtr          = 35,

  GetAttr               = 36,
  SetAttr               = 37,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  SetAttrInplace        = 38,

  UnaryNot              = 39,
  UnaryNeg              = 40,
  And                   = 41,
  Or                    = 42,
  Mod                   = 43,
  BitOr                 = 44,
  BitAnd                = 45,
  EqualLite             = 46,
  Equal                 = 47,
  UnaryBitNot           = 48,
  LT                    = 49,
  LTE                   = 50,
  GT                    = 51,
  GTE                   = 52,

  DefArg                = 53,

  TypeCast              = 54,
  TypeAs                = 55,
  TypeIs                = 56,
  Typeof                = 57,

  Scope                 = 58,
  Defer                 = 59,
  Paral                 = 60,
  ParalAll              = 61,

  New                   = 62,
  SetUpval              = 63,
  Inc                   = 64,
  Dec                   = 65,
  ArrIdx                = 66,
  ArrIdxW               = 67,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  ArrAddInplace         = 68,

  BitShr                = 69,
  BitShl                = 70,

  MapIdx                = 71,
  MapIdxW               = 72,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  MapAddInplace         = 73,

  MakeRef               = 74,
  GetRef                = 75,
  SetRef                = 76,


  MAX                   = 77
}

}

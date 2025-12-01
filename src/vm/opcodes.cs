namespace bhl
{

public enum Opcodes
{
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

  Concat                = 20,

  Pop                   = 22,

  CallLocal             = 23,
  CallGlobNative        = 24,
  Call                  = 25,
  CallNative            = 26,
  CallMethod            = 27,
  CallMethodNative      = 28,
  CallMethodIface       = 29,
  CallMethodIfaceNative = 30,
  CallMethodVirt        = 31,
  CallFuncPtr           = 32,
  //for a case when func pointer comes before args
  //TODO: if CallFuncPtr operated on stack offsets this one would not be necessary
  CallFuncPtrInv        = 33,
  CallVarMethodNative   = 34,

  GetFuncLocalPtr       = 37,
  GetFuncPtr            = 38,
  GetFuncNativePtr      = 39,
  GetFuncIpPtr          = 40,

  GetAttr               = 41,
  SetAttr               = 42,
  GetVarAttr            = 43,
  SetVarAttr            = 44,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  SetAttrPeek           = 45,
  GetRefAttr            = 46,
  SetRefAttr            = 47,

  Equal                 = 49,
  UnaryNot              = 50,
  UnaryNeg              = 51,
  And                   = 52,
  Or                    = 53,
  Mod                   = 54,
  BitOr                 = 55,
  BitAnd                = 56,
  EqualScalar           = 57,
  EqualString           = 58,
  UnaryBitNot           = 59,
  LT                    = 60,
  LTE                   = 61,
  GT                    = 62,
  GTE                   = 63,

  DefArg                = 64,

  TypeCast              = 65,
  TypeAs                = 66,
  TypeIs                = 67,
  Typeof                = 68,

  Scope                 = 70,
  Defer                 = 71,
  Paral                 = 72,
  ParalAll              = 73,

  New                   = 77,
  SetUpval              = 79,
  Inc                   = 81,
  Dec                   = 82,
  ArrIdx                = 83,
  ArrIdxW               = 84,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  ArrAddInplace         = 85,

  BitShr                = 86,
  BitShl                = 87,

  MapIdx                = 91,
  MapIdxW               = 92,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  MapAddInplace         = 93,

  MakeRef               = 94,
  GetRef                = 95,
  SetRef                = 96,

  Nop                   = 99,

  MAX                   = 100
}

}

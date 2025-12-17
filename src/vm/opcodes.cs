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

  Nop                   = 13,

  Frame                 = 14,
  Return                = 15,
  Jump                  = 16,
  JumpZ                 = 17,
  JumpPeekZ             = 18,
  JumpPeekNZ            = 19,

  Concat                = 20,

  Pop                   = 21,

  CallLocal             = 22,
  CallGlobNative        = 23,
  Call                  = 24,
  CallNative            = 25,
  CallMethod            = 26,
  CallMethodNative      = 27,
  CallMethodIface       = 28,
  CallMethodIfaceNative = 29,
  CallMethodVirt        = 30,
  CallFuncPtr           = 31,
  //for a case when func pointer comes before args
  //TODO: if CallFuncPtr operated on stack offsets this one would not be necessary
  CallFuncPtrInv        = 32,
  CallVarMethodNative   = 33,
  CallGVarMethodNative  = 34,

  GetFuncLocalPtr       = 35,
  GetFuncPtr            = 36,
  GetFuncNativePtr      = 37,
  GetFuncIpPtr          = 38,

  GetAttr               = 39,
  SetAttr               = 40,
  GetVarAttr            = 41,
  SetVarAttr            = 42,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  SetAttrPeek           = 43,
  GetRefAttr            = 44,
  SetRefAttr            = 45,
  GetGVarAttr           = 46,
  SetGVarAttr           = 47,

  UnaryNot              = 48,
  UnaryNeg              = 49,
  And                   = 50,
  Or                    = 51,
  Mod                   = 52,
  BitOr                 = 53,
  BitAnd                = 54,
  EqualScalar           = 55,
  EqualString           = 56,
  UnaryBitNot           = 57,
  LT                    = 58,
  LTE                   = 59,
  GT                    = 60,
  GTE                   = 61,
  Equal                 = 62,

  TypeCast              = 63,
  TypeAs                = 64,
  TypeIs                = 65,
  Typeof                = 66,

  DefArg                = 67,

  Scope                 = 68,
  Defer                 = 69,
  Paral                 = 70,
  ParalAll              = 71,

  New                   = 72,
  SetUpval              = 73,
  Inc                   = 74,
  Dec                   = 75,
  ArrIdx                = 76,
  ArrIdxW               = 77,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  ArrAddInplace         = 78,

  BitShr                = 79,
  BitShl                = 80,

  MapIdx                = 81,
  MapIdxW               = 82,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  MapAddInplace         = 83,

  MakeRef               = 84,
  GetRef                = 85,
  SetRef                = 86,


  MAX                   = 87
}

}

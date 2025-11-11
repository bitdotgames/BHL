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
  DeclVar               = 8,
  SetGVar               = 10,
  GetGVar               = 11,
  Frame                 = 12, //TODO: do we really need it?
  Return                = 14,
  Jump                  = 16,
  JumpZ                 = 17,
  JumpPeekZ             = 18,
  JumpPeekNZ            = 19,
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
  GetFuncLocalPtr       = 39,
  GetFuncPtr            = 40,
  GetFuncNativePtr      = 41,
  //TODO: not really needed?
  LastArgToTop          = 44,
  GetAttr               = 45,
  SetAttr               = 47,
  //TODO: used for json alike array initialization,
  //      can be replaced with more low-level opcodes?
  SetAttrInplace        = 48,
  UnaryNot              = 50,
  UnaryNeg              = 51,
  And                   = 52,
  Or                    = 53,
  Mod                   = 54,
  BitOr                 = 55,
  BitAnd                = 56,
  Equal                 = 57,
  NotEqual              = 58,
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

  Scope                   = 70,
  Defer                 = 71,
  Paral                 = 72,
  ParalAll              = 73,

  New                   = 77,
  Lambda                = 78,
  SetUpval          = 79, //TODO: split it into capture 'ref' and 'val'
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

  DeclRef               = 94,
  GetRef                = 95,
  SetRef                = 96,

  Nop                   = 99,
  MAX                   = 100
}

}

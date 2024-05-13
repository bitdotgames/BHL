
namespace bhl {

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
  ArgVar                = 9,
  SetGVar               = 10,
  GetGVar               = 11,
  InitFrame             = 12,
  ExitFrame             = 13,
  Return                = 14,
  ReturnVal             = 15,
  Jump                  = 16,
  JumpZ                 = 17,
  JumpPeekZ             = 18,
  JumpPeekNZ            = 19,
  Pop                   = 22,
  CallLocal             = 23,
  CallNative            = 24,
  CallFunc              = 25,
  CallMethod            = 26,
  CallMethodNative      = 27,
  CallMethodIface       = 28,
  CallMethodIfaceNative = 29,
  CallMethodVirt        = 30,
  CallFuncPtr           = 31,
  GetFuncLocalPtr       = 38,
  GetFuncPtr            = 39,
  GetFuncNativePtr      = 40,
  GetFuncPtrFromVar     = 41,
  LastArgToTop          = 43,
  GetAttr               = 44,
  RefAttr               = 45,
  SetAttr               = 46,
  SetAttrInplace        = 47,
  ArgRef                = 48,
  UnaryNot              = 49,
  UnaryNeg              = 50,
  And                   = 51,
  Or                    = 52,
  Mod                   = 53,
  BitOr                 = 54,
  BitAnd                = 55,
  Equal                 = 56,
  NotEqual              = 57,
  LT                    = 59,
  LTE                   = 60,
  GT                    = 61,
  GTE                   = 62,
  DefArg                = 63, 
  TypeCast              = 64,
  TypeAs                = 65,
  TypeIs                = 66,
  Typeof                = 67,
  Block                 = 75,
  New                   = 76,
  Lambda                = 77,
  UseUpval              = 78,
  Inc                   = 80,
  Dec                   = 81,
  ArrIdx                = 82,
  ArrIdxW               = 83,
  ArrAddInplace         = 84,  //TODO: used for json alike array initialization,   
                               //      can be replaced with more low-level opcodes?
  MapIdx                = 90,
  MapIdxW               = 91,
  MapAddInplace         = 92,  //TODO: used for json alike array initialization,   
}

} //namespace bhl

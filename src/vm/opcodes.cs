
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
  PopReturnVals         = 21,
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
  GetFuncPtrFromVar     = 42,
  LastArgToTop          = 44,
  GetAttr               = 45,
  RefAttr               = 46,
  SetAttr               = 47,
  SetAttrInplace        = 48,
  ArgRef                = 49,
  UnaryNot              = 50,
  UnaryNeg              = 51,
  And                   = 52,
  Or                    = 53,
  Mod                   = 54,
  BitOr                 = 55,
  BitAnd                = 56,
  Equal                 = 57,
  NotEqual              = 58,
  LT                    = 60,
  LTE                   = 61,
  GT                    = 62,
  GTE                   = 63,
  DefArg                = 64, 
  TypeCast              = 65,
  TypeAs                = 66,
  TypeIs                = 67,
  Typeof                = 68,
  Block                 = 76,
  New                   = 77,
  Lambda                = 78,
  UseUpval              = 79,
  Inc                   = 81,
  Dec                   = 82,
  ArrIdx                = 83,
  ArrIdxW               = 84,
  ArrAddInplace         = 85,  //TODO: used for json alike array initialization,   
                               //      can be replaced with more low-level opcodes?
  MapIdx                = 91,
  MapIdxW               = 92,
  MapAddInplace         = 93,  //TODO: used for json alike array initialization,   
}

}

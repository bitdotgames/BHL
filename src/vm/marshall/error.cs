using System;

namespace bhl.marshall
{

public enum ErrorCode
{
  SUCCESS                = 0,
  NO_RESERVED_SPACE      = 1,
  DANGLING_DATA          = 2,
  IO_READ                = 3,
  TYPE_MISMATCH          = 4,
  INVALID_POS            = 5,
  BAD_CLASS_ID           = 6,
}

public class Error : Exception
{
  public ErrorCode code { get; }

  public Error(ErrorCode code, string msg = "")
    : base($"({code}) {msg}")
  {
    this.code = code;
  }
}

}
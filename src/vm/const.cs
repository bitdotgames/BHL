using System;
using System.Runtime.CompilerServices;

namespace bhl
{

public enum ConstType
{
  INT        = 1,
  FLT        = 2,
  BOOL       = 3,
  STR        = 4,
  NIL        = 5,
}

public class Const : IEquatable<Const>
{
  static public readonly Const Nil = new Const(ConstType.NIL, 0, "");

  public ConstType type;
  public double num;
  public string str;

  public Const(ConstType type, double num, string str)
  {
    this.type = type;
    this.num = num;
    this.str = str;
  }

  public Const(int num)
  {
    type = ConstType.INT;
    this.num = num;
    str = "";
  }

  public Const(double num)
  {
    type = ConstType.FLT;
    this.num = num;
    str = "";
  }

  public Const(string str)
  {
    type = ConstType.STR;
    this.str = str;
    num = 0;
  }

  public Const(bool v)
  {
    type = ConstType.BOOL;
    num = v ? 1 : 0;
    this.str = "";
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void FillVal(ref Val v)
  {
    v = type switch
    {
#if USE_VAL_MINI
      ConstType.INT => new Val { type = Types.Int, num = num },
      ConstType.FLT => new Val { type = Types.Float, num = num },
      ConstType.STR => new Val { type = Types.String, obj = str },
      ConstType.BOOL => new Val { type = Types.Bool, num = num },
      ConstType.NIL => new Val { type = Types.Null },
#else
      ConstType.INT => new Val { type_id = Types.Int.ClassId(), _int0 = (int)num },
      ConstType.FLT => new Val { type_id = Types.Float.ClassId(), _double0 = num },
      // ConstType.STR => new Val { type_id = Types.String.ClassId(), obj = str },
      ConstType.BOOL => new Val { type_id = Types.Bool.ClassId(), _int0 = (num == 1) ? 1 : 0 },
      ConstType.NIL => new Val { type_id = Types.Null.ClassId() },
#endif
      _ => throw new Exception("Bad type")
    };
  }

  public bool Equals(Const o)
  {
    if(o == null)
      return false;

    return type == o.type &&
           num == o.num &&
           str == o.str
      ;
  }
}

}

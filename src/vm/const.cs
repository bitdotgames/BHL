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
    if(type == ConstType.INT || type == ConstType.FLT)
    {
      v.type = Types.Int;
      v._num = num;
    }
    else if(type == ConstType.FLT)
    {
      v.type = Types.Float;
      v._num = num;
    }
    else if(type == ConstType.STR)
    {
      v.type = Types.String;
      v._obj = str;
    }
    else if(type == ConstType.BOOL)
    {
      v.type = Types.Bool;
      v._num = num;
    }
    else if(type == ConstType.NIL)
    {
      v.type = Types.Null;
      v._obj = null;
      v._refc = null;
    }
    else
      throw new Exception("Bad type");
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

using System;

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

public struct Const : IEquatable<Const>
{
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

  public bool Equals(Const o)
  {
    return type == o.type &&
           num == o.num &&
           str == o.str
      ;
  }
}

}

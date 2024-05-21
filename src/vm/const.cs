using System;
using System.Collections.Generic;

namespace bhl {

public enum ConstType 
{
  INT        = 1,
  FLT        = 2,
  BOOL       = 3,
  STR        = 4,
  NIL        = 5,
  ITYPE      = 6,
}

public class Const : IEquatable<Const>
{
  static public readonly Const Nil = new Const(ConstType.NIL, 0, "");

  public ConstType type;
  public double num;
  public string str;
  public Proxy<IType> itype;

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

  public Const(Proxy<IType> itype)
  {
    type = ConstType.ITYPE;
    this.itype = itype;
  }

  public Val ToVal(VM vm)
  {
    if(type == ConstType.INT)
      return Val.NewInt(vm, num);
    else if(type == ConstType.FLT)
      return Val.NewFlt(vm, num);
    else if(type == ConstType.BOOL)
      return Val.NewBool(vm, num == 1);
    else if(type == ConstType.STR)
      return Val.NewStr(vm, str);
    else if(type == ConstType.NIL)
      return vm.Null;
    else if(type == ConstType.ITYPE)
      return Val.NewObj(vm, itype, Types.Any/*TODO: ???*/);
    else
      throw new Exception("Bad type");
  }

  public bool Equals(Const o)
  {
    if(o == null)
      return false;

    return type == o.type && 
           num == o.num && 
           str == o.str &&
           itype.Equals(o.itype)
           ;
  }
}

} //namespace bhl

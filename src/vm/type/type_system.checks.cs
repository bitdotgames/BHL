using System;
using System.Collections.Generic;

namespace bhl
{

#if (BHL_FRONT || BHL_PARSER || UNITY_EDITOR)
public partial class Types : INamedResolver, IProxyTypeCache
{
  static Dictionary<Tuple<IType, IType>, IType> bin_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), String },
    { new Tuple<IType, IType>(Int, Int),       Int },
    { new Tuple<IType, IType>(Int, Float),     Float },
    { new Tuple<IType, IType>(Float, Float),   Float },
    { new Tuple<IType, IType>(Float, Int),     Float },
  };

  static Dictionary<Tuple<IType, IType>, IType> rtl_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), Bool },
    { new Tuple<IType, IType>(Int, Int),       Bool },
    { new Tuple<IType, IType>(Int, Float),     Bool },
    { new Tuple<IType, IType>(Float, Float),   Bool },
    { new Tuple<IType, IType>(Float, Int),     Bool },
  };

  static Dictionary<Tuple<IType, IType>, IType> eq_op_res_type = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(String, String), Bool },
    { new Tuple<IType, IType>(Int, Int),       Bool },
    { new Tuple<IType, IType>(Int, Float),     Bool },
    { new Tuple<IType, IType>(Float, Float),   Bool },
    { new Tuple<IType, IType>(Float, Int),     Bool },
    { new Tuple<IType, IType>(Any, Any),       Bool },
    { new Tuple<IType, IType>(Null, Any),      Bool },
    { new Tuple<IType, IType>(Any, Null),      Bool },
  };

  // Indicate whether a type supports a promotion to a wider type.
  static HashSet<Tuple<IType, IType>> is_subset_of = new HashSet<Tuple<IType, IType>>()
  {
    { new Tuple<IType, IType>(Int, Float) },
  };

  static public IType MatchTypes(Dictionary<Tuple<IType, IType>, IType> table, AnnotatedParseTree lhs,
    AnnotatedParseTree rhs, CompileErrors errors)
  {
    IType result;
    if(!table.TryGetValue(new Tuple<IType, IType>(lhs.eval_type, rhs.eval_type), out result))
      errors.Add(new ParseError(rhs,
        "incompatible types: '" + lhs.eval_type.GetFullTypePath() + "' and '" + rhs.eval_type.GetFullTypePath() + "'"));
    return result;
  }

  static bool CanAssignTo(IType lhs, IType rhs)
  {
    if(rhs == null || lhs == null)
      return false;

    return rhs == lhs ||
           lhs == Any ||
           (lhs == Types.Int && rhs is EnumSymbol) ||
           (is_subset_of.Contains(new Tuple<IType, IType>(rhs, lhs))) ||
           (lhs is IInstantiable && rhs == Null) ||
           (lhs is FuncSignature && rhs == Null) ||
           Is(rhs, lhs)
      ;
  }

  public bool CheckAssign(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs.eval_type, rhs.eval_type))
    {
      errors.Add(new ParseError(rhs,
        "incompatible types: '" + lhs.eval_type.GetFullTypePath() + "' and '" + rhs.eval_type.GetFullTypePath() + "'"));
      return false;
    }

    return true;
  }

  //NOTE: SemanticError(..) is attached to rhs, not lhs.
  //      Usually this is the case when expression (rhs) is passed
  //      to a func arg (lhs) and it this case it makes sense
  //      to report about the site where it's actually passed (rhs),
  //      not where it's defined (lhs)
  public bool CheckAssign(IType lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs, rhs.eval_type))
    {
      errors.Add(MakeIncompatibleTypesError(rhs, lhs.GetFullTypePath().ToString(),
        rhs.eval_type.GetFullTypePath().ToString()));
      return false;
    }

    return true;
  }

  public bool CheckAssign(AnnotatedParseTree lhs, IType rhs, CompileErrors errors)
  {
    if(!CanAssignTo(lhs.eval_type, rhs))
    {
      errors.Add(MakeIncompatibleTypesError(lhs, lhs.eval_type.GetFullTypePath().ToString(),
        rhs.GetFullTypePath().ToString()));
      return false;
    }

    return true;
  }

  static ParseError MakeIncompatibleTypesError(AnnotatedParseTree pt, string lhs_path, string rhs_path)
  {
    return new ParseError(pt, "incompatible types: '" + lhs_path + "' and '" + rhs_path + "'");
  }

  static public bool CheckCastIsPossible(AnnotatedParseTree dest, AnnotatedParseTree from, CompileErrors errors)
  {
    var dest_type = dest.eval_type;
    var from_type = from.eval_type;

    if(!CheckCastIsPossible(dest_type, from_type))
    {
      errors.Add(new ParseError(dest,
        "incompatible types for casting: '" + dest_type.GetFullTypePath() + "' and '" + from_type.GetFullTypePath() +
        "'"));
      return false;
    }

    return true;
  }

  public IType CheckBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, string op, CompileErrors errors)
  {
    if(!IsBinOpCompatible(lhs.eval_type))
    {
      errors.Add(new ParseError(lhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));
      return null;
    }

    if(!IsBinOpCompatible(rhs.eval_type))
    {
      errors.Add(new ParseError(rhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));
      return null;
    }

    return MatchTypes(bin_op_res_type, lhs, rhs, errors);
  }

  public IType CheckBinOpOverload(IScope scope, AnnotatedParseTree lhs, AnnotatedParseTree rhs, FuncSymbol op_func,
    CompileErrors errors)
  {
    var op_func_arg_type = op_func.signature.arg_types[1];
    CheckAssign(op_func_arg_type.Get(), rhs, errors);
    return op_func.GetReturnType();
  }

  public IType CheckRelationalBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, string op, CompileErrors errors)
  {
    if(!IsNumeric(lhs.eval_type))
    {
      errors.Add(new ParseError(lhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));
      return Bool;
    }

    if(!IsNumeric(rhs.eval_type))
    {
      errors.Add(new ParseError(rhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));
      return Bool;
    }

    MatchTypes(rtl_op_res_type, lhs, rhs, errors);

    return Bool;
  }

  public IType CheckEqBinOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, CompileErrors errors)
  {
    if(lhs.eval_type == rhs.eval_type)
      return Bool;

    if(lhs.eval_type is ClassSymbol && rhs.eval_type is ClassSymbol)
      return Bool;

    //TODO: add INullableType?
    if(((lhs.eval_type is ClassSymbol || lhs.eval_type is InterfaceSymbol || lhs.eval_type is FuncSignature) &&
        rhs.eval_type == Null) ||
       ((rhs.eval_type is ClassSymbol || rhs.eval_type is InterfaceSymbol || rhs.eval_type is FuncSignature) &&
        lhs.eval_type == Null))
      return Bool;

    if((lhs.eval_type == Types.Int && rhs.eval_type is EnumSymbol) ||
       (lhs.eval_type is EnumSymbol && rhs.eval_type == Types.Int))
      return Bool;

    MatchTypes(eq_op_res_type, lhs, rhs, errors);

    return Bool;
  }

  public IType CheckUnaryMinus(AnnotatedParseTree a, CompileErrors errors)
  {
    if(!(a.eval_type == Int || a.eval_type == Float))
      errors.Add(new ParseError(a,
        $"operator '-' cannot be applied to operand of type '{a.eval_type?.GetName() ?? "?"}'"));

    return a.eval_type;
  }

  public IType CheckBitNot(AnnotatedParseTree a, CompileErrors errors)
  {
    if(a.eval_type != Int)
      errors.Add(new ParseError(a,
        $"operator '~' cannot be applied to operand of type '{a.eval_type?.GetName() ?? "?"}'"));

    return Int;
  }

  public IType CheckBitOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, string op, CompileErrors errors)
  {
    if(lhs.eval_type != Int || rhs.eval_type != Int)
      errors.Add(new ParseError(lhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));

    return Int;
  }

  public IType CheckLogicalOp(AnnotatedParseTree lhs, AnnotatedParseTree rhs, string op, CompileErrors errors)
  {
    if(lhs.eval_type != Bool)
      errors.Add(new ParseError(lhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));

    if(rhs.eval_type != Bool)
      errors.Add(new ParseError(rhs,
        $"operator '{op}' cannot be applied to operands of type '{lhs.eval_type?.GetName() ?? "?"}' and '{rhs.eval_type?.GetName() ?? "?"}'"));

    return Bool;
  }

  public IType CheckLogicalNot(AnnotatedParseTree a, CompileErrors errors)
  {
    if(a.eval_type != Bool)
      errors.Add(new ParseError(a,
        $"operator '!' cannot be applied to operand of type '{a.eval_type?.GetName() ?? "?"}'"));

    return a.eval_type;
  }
}
#endif

}

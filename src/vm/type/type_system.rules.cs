using System;
using System.Collections.Generic;

namespace bhl
{

public partial class Types : INamedResolver, IProxyTypeCache
{
  static Dictionary<Tuple<IType, IType>, IType> cast_from_to = new Dictionary<Tuple<IType, IType>, IType>()
  {
    { new Tuple<IType, IType>(Bool,   String),    String },
    { new Tuple<IType, IType>(Bool,   Int),       Int    },
    { new Tuple<IType, IType>(Bool,   Float),     Float  },
    { new Tuple<IType, IType>(Bool,   Any),       Any    },
    { new Tuple<IType, IType>(String, String),    String },
    { new Tuple<IType, IType>(String, Any),       Any    },
    { new Tuple<IType, IType>(Int,    Bool),      Bool   },
    { new Tuple<IType, IType>(Int,    String),    String },
    { new Tuple<IType, IType>(Int,    Int),       Int    },
    { new Tuple<IType, IType>(Int,    Float),     Float  },
    { new Tuple<IType, IType>(Int,    Any),       Any    },
    { new Tuple<IType, IType>(Float,  Bool),      Bool   },
    { new Tuple<IType, IType>(Float,  String),    String },
    { new Tuple<IType, IType>(Float,  Int),       Int    },
    { new Tuple<IType, IType>(Float,  Float),     Float  },
    { new Tuple<IType, IType>(Float,  Any),       Any    },
    { new Tuple<IType, IType>(Any,    Bool),      Bool   },
    { new Tuple<IType, IType>(Any,    String),    String },
    { new Tuple<IType, IType>(Any,    Int),       Int    },
    { new Tuple<IType, IType>(Any,    Float),     Float  },
    { new Tuple<IType, IType>(Any,    Any),       Any    },
  };

  static public bool IsCompoundType(string name)
  {
    for(int i = 0; i < name.Length; ++i)
    {
      char c = name[i];
      if(!(Char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        return true;
    }

    return false;
  }

  static public bool Is(IType type, IType dest_type)
  {
    if(type == null || dest_type == null)
      return false;

    //quick shortcut
    if(dest_type == Any)
      return true;

    //NOTE: using Equals() since we might deal with ephemeral types
    if(type.Equals(dest_type))
      return true;

    //NOTE: special case for array type checked against '[]any'
    //NOTE: using Equals() since Array is an ephemeral type
    if(type is ArrayTypeSymbol && dest_type.Equals(Array))
      return true;

    //NOTE: special case for array type checked against '[any]any'
    //NOTE: using Equals() since Map is an ephemeral type
    if(type is MapTypeSymbol && dest_type.Equals(Map))
      return true;

    if(type is IInstantiable ti && dest_type is IInstantiable di)
    {
      var tset = ti.GetAllRelatedTypesSet();
      var dset = di.GetAllRelatedTypesSet();

      return tset.IsSupersetOf(dset);
    }

    return false;
  }

  static public bool Is(Val val, IType dest_type)
  {
    //NOTE: special handling of native types - we need to go through C# type system.
    //      Make sense only if there's C# object is present - this way we can use C# reflection
    //      by getting the type of the object
    if(dest_type is INativeType dest_intype)
    {
      var val_type = val.type;

      if(val_type is INativeType val_intype)
      {
        var dest_ntype = dest_intype.GetNativeType();
        var nobj = val_intype.GetNativeObject(val);
        return dest_ntype?.IsAssignableFrom(
          nobj?.GetType() ?? val_intype.GetNativeType()
        ) ?? false;
      }
      //special case for 'any' type being cast to native type
      else if(val_type == Types.Any)
      {
        var dest_ntype = dest_intype.GetNativeType();
        if(val.type == Types.Null && dest_ntype != null)
          return true;
        var nobj = val.obj;
        return dest_ntype?.IsAssignableFrom(nobj?.GetType()) ?? false;
      }
      else
        return false;
    }
    else
      return Is(val.type, dest_type);
  }

  static public bool CheckCastIsPossible(IType dest_type, IType from_type)
  {
    //optimization shortcut
    if(dest_type == from_type)
      return true;

    //enum special cases
    if((from_type == Float || from_type == Int) && dest_type is EnumSymbol)
      return true;
    if((dest_type == Float || dest_type == Int) && from_type is EnumSymbol)
      return true;
    if(dest_type == String && from_type is EnumSymbol)
      return true;

    cast_from_to.TryGetValue(new Tuple<IType, IType>(from_type, dest_type), out var cast_type);
    if(cast_type == dest_type)
      return true;

    //going the 'heavy path', checking if types are convertible one to another
    if(Is(from_type, dest_type) || Is(dest_type, from_type))
      return true;

    return false;
  }

  static public bool IsBinOpCompatible(IType type)
  {
    return (IsScalar(type) || IsString(type)) && type is not EnumSymbol;
  }

  static public bool IsScalar(IType type)
  {
    return type == Int ||
           type == Float ||
           type == Bool ||
           type is EnumSymbol;
           ;
  }
  static public bool IsNumeric(IType type)
  {
    return type == Int ||
           type == Float;
  }

  static public bool IsString(IType type)
  {
    return type == String;
  }
}

}

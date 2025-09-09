namespace bhl
{

public class SymbolFactory : marshall.IFactory
{
  public Types types;
  public INamedResolver resolver;

  public SymbolFactory(Types types, INamedResolver resolver)
  {
    this.types = types;
    this.resolver = resolver;
  }

  public marshall.IMarshallableGeneric CreateById(uint id)
  {
    switch(id)
    {
      case IntSymbol.CLASS_ID:
        return Types.Int;
      case FloatSymbol.CLASS_ID:
        return Types.Float;
      case StringSymbol.CLASS_ID:
        return Types.String;
      case BoolSymbol.CLASS_ID:
        return Types.Bool;
      case AnySymbol.CLASS_ID:
        return Types.Any;
      case VoidSymbol.CLASS_ID:
        return Types.Void;
      case VariableSymbol.CLASS_ID:
        return new VariableSymbol();
      case GlobalVariableSymbol.CLASS_ID:
        return new GlobalVariableSymbol();
      case FuncArgSymbol.CLASS_ID:
        return new FuncArgSymbol();
      case FieldSymbolScript.CLASS_ID:
        return new FieldSymbolScript();
      case GenericArrayTypeSymbol.CLASS_ID:
        return new GenericArrayTypeSymbol();
      case NativeListTypeSymbol.CLASS_ID:
        return new GenericArrayTypeSymbol();
      case GenericMapTypeSymbol.CLASS_ID:
        return new GenericMapTypeSymbol();
      case ClassSymbolScript.CLASS_ID:
        return new ClassSymbolScript();
      case InterfaceSymbolScript.CLASS_ID:
        return new InterfaceSymbolScript();
      case EnumSymbolScript.CLASS_ID:
        return new EnumSymbolScript();
      case EnumItemSymbol.CLASS_ID:
        return new EnumItemSymbol();
      case FuncSymbolScript.CLASS_ID:
        return new FuncSymbolScript();
      case FuncSignature.CLASS_ID:
        return new FuncSignature();
      case RefType.CLASS_ID:
        return new RefType();
      case TupleType.CLASS_ID:
        return new TupleType();
      case Namespace.CLASS_ID:
        return new Namespace();
      default:
        return null;
    }
  }
}

}
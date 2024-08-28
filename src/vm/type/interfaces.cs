using System.Collections.Generic;
using bhl.marshall;

namespace bhl {
  
// Denotes any named entity which can be located by this name
public interface INamed
{
  string GetName();
}

public interface IType : INamed
{}

public interface ITyped
{
  IType GetIType();
}

public interface INativeType
{
  System.Type GetNativeType();
  object GetNativeObject(Val v);
}

public interface ITypeRefIndexable
{
  void IndexTypeRefs(TypeRefIndex refs);
}

// Denotes types which are created 'on the fly'
public interface IEphemeralType : IType, ITypeRefIndexable, IMarshallableGeneric
{}

public interface IInstantiable : IType, IScope 
{
  HashSet<IInstantiable> GetAllRelatedTypesSet();
}
}

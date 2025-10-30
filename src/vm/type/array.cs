using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace bhl
{

public abstract class ArrayTypeSymbol : ClassSymbol
{
  public ProxyType item_type;

  //marshall factory version
  public ArrayTypeSymbol()
    : base(null, null)
  {
  }

  public ArrayTypeSymbol(Origin origin, string name, ProxyType item_type)
    : base(origin, name)
  {
    this.item_type = item_type;
  }

  protected virtual void DefineMembers()
  {
    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative(new Origin(), "Add", Types.Void, BindAdd,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "RemoveAt", Types.Void, BindRemoveAt,
        new FuncArgSymbol("idx", Types.Int)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Clear", Types.Void, BindClear);
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Insert", Types.Void, BindInsert,
        new FuncArgSymbol("idx", Types.Int),
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Count", Types.Int, BindCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "IndexOf", Types.Int, BindIndexOf,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }
  }

  public override void Setup()
  {
    if(item_type.IsEmpty())
      throw new Exception("Invalid item type");

    this.creator = BindCreateArr;

    DefineMembers();

    base.Setup();
  }

  public ArrayTypeSymbol(Origin origin, string name)
    : base(origin, name)
  {
  }

  public ArrayTypeSymbol(Origin origin, ProxyType item_type)
    : this(origin, "[]" + item_type, item_type)
  {
  }

  void BindCreateArr(VM vm, ref Val v, IType type)
  {
    ArrCreate(vm, ref v);
  }

  void BindCount(VM vm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetNum(ArrCount(ctx));
  }

  Coroutine BindAdd(VM vm, VM.ExecState exec, FuncArgsInfo args_info)
  {
    var stack = exec.stack;

    stack.Pop(out var val);
    stack.Pop(out var arr);

    ArrAdd(arr, val);

    val._refc?.Release();
    arr._refc?.Release();
    return null;
  }

  Coroutine BindRemoveAt(VM vm, VM.ExecState exec, FuncArgsInfo args_info)
  {
    var stack = exec.stack;

    stack.Pop(out var idx);
    stack.Pop(out var arr);

    ArrRemoveAt(arr, idx);

    arr._refc?.Release();
    return null;
  }

  Coroutine BindIndexOf(VM vm, VM.ExecState exec, FuncArgsInfo args_info)
  {
    var stack = exec.stack;

    stack.Pop(out var val);
    stack.Pop(out var arr);

    int idx = ArrIndexOf(arr, val);

    val._refc?.Release();
    arr._refc?.Release();
    stack.Push(idx);
    return null;
  }

  Coroutine BindClear(VM vm, VM.ExecState exec, FuncArgsInfo args_info)
  {
    exec.stack.Pop(out var arr);

    ArrClear(arr);

    arr._refc?.Release();
    return null;
  }

  Coroutine BindInsert(VM vm, VM.ExecState exec, FuncArgsInfo args_info)
  {
    var stack = exec.stack;

    stack.Pop(out var val);
    stack.Pop(out var idx);
    stack.Pop(out var arr);

    ArrInsert(arr, idx, val);

    arr._refc?.Release();
    val._refc?.Release();
    return null;
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
    refs.Index(item_type);
  }

  public abstract void ArrCreate(VM vm, ref Val arr);
  public abstract int ArrCount(Val arr);
  public abstract void ArrAdd(Val arr, Val val);
  public abstract Val ArrGetAt(Val arr, int idx);
  public abstract void ArrSetAt(Val arr, int idx, Val val);
  public abstract void ArrRemoveAt(Val arr, int idx);
  public abstract int ArrIndexOf(Val arr, Val val);
  public abstract void ArrClear(Val arr);
  public abstract void ArrInsert(Val arr, int idx, Val val);
}

//NOTE: operates on IList<Val> (e.g ValList)
public class GenericArrayTypeSymbol :
  ArrayTypeSymbol, IEquatable<GenericArrayTypeSymbol>, IEphemeralType
{
  public const uint CLASS_ID = 10;

  public GenericArrayTypeSymbol(Origin origin, ProxyType item_type)
    : base(origin, item_type)
  {
  }

  //marshall factory version
  public GenericArrayTypeSymbol()
    : base()
  {
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static ValList AsValList(Val arr)
  {
    var lst = arr._obj as ValList;
    if(arr._obj != null && lst == null)
      throw new Exception("Not a ValList: " + arr.obj.GetType().Name);
    return lst;
  }

  public override void ArrCreate(VM vm, ref Val arr)
  {
    arr.SetObj(ValList.New(vm), this);
  }

  public override int ArrCount(Val arr)
  {
    return AsValList(arr).Count;
  }

  public override void ArrAdd(Val arr, Val val)
  {
    var lst = AsValList(arr);
    lst.Add(val.CloneValue());
  }

  public override int ArrIndexOf(Val arr, Val val)
  {
    var lst = AsValList(arr);

    int idx = -1;
    for(int i = 0; i < lst.Count; ++i)
    {
      if(lst[i].IsValueEqual(ref val))
      {
        idx = i;
        break;
      }
    }

    return idx;
  }

  public override Val ArrGetAt(Val arr, int idx)
  {
    var lst = AsValList(arr);
    var res = lst[idx];
    res.Retain();
    return res;
  }

  public override void ArrSetAt(Val arr, int idx, Val val)
  {
    var lst = AsValList(arr);
    lst.SetValueCopyAt(idx, val);
  }

  public override void ArrRemoveAt(Val arr, int idx)
  {
    var lst = AsValList(arr);
    lst.RemoveAt(idx);
  }

  public override void ArrClear(Val arr)
  {
    var lst = AsValList(arr);
    lst.Clear();
  }

  public override void ArrInsert(Val arr, int idx, Val val)
  {
    var lst = AsValList(arr);
    lst.Insert(idx, val.CloneValue());
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.SyncTypeRef(ctx, ref item_type);

    if(ctx.is_read)
    {
      name = "[]" + item_type;

      //NOTE: once we have all members unmarshalled we should actually Setup() the instance
      Setup();
    }
  }

  public override bool Equals(object o)
  {
    if(!(o is GenericArrayTypeSymbol))
      return false;
    return this.Equals((GenericArrayTypeSymbol)o);
  }

  public bool Equals(GenericArrayTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return item_type.Equals(o.item_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

//NOTE: operates on IList (not compatible with ValList)
public abstract class NativeListTypeSymbol :
  ArrayTypeSymbol, IEquatable<NativeListTypeSymbol>
{
  public const uint CLASS_ID = 24;

  public NativeListTypeSymbol(
    Origin origin, string name, ProxyType item_type)
    : base(origin, name, item_type)
  {
  }

  //NOTE: making it sort of 'inherit' from the base GenericArrayTypeSymbol
  protected override HashSet<IInstantiable> CollectAllRelatedTypesSet()
  {
    var related_types = new HashSet<IInstantiable>();
    related_types.Add(this);
    var arr_type = new GenericArrayTypeSymbol(new Origin(), item_type);
    arr_type.Setup();
    related_types.Add(arr_type);
    return related_types;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static IList AsIList(Val arr)
  {
    var lst = arr._obj as IList;
    if(arr._obj != null && lst == null)
      throw new Exception("Not an IList: " + arr._obj.GetType().Name);
    return lst;
  }

  public abstract IList CreateList(VM vm);

  public override void ArrCreate(VM vm, ref Val arr)
  {
    arr.SetObj(CreateList(vm), this);
  }

  public override int ArrCount(Val arr)
  {
    return AsIList(arr).Count;
  }

  public override void ArrRemoveAt(Val arr, int idx)
  {
    AsIList(arr).RemoveAt(idx);
  }

  public override void ArrClear(Val arr)
  {
    AsIList(arr).Clear();
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void IndexTypeRefs(TypeRefIndex refs)
  {
  }

  public override void Sync(marshall.SyncContext ctx)
  {
  }

  public override bool Equals(object o)
  {
    if(!(o is NativeListTypeSymbol))
      return false;
    return this.Equals((NativeListTypeSymbol)o);
  }

  public bool Equals(NativeListTypeSymbol o)
  {
    if(ReferenceEquals(o, null))
      return false;
    if(ReferenceEquals(this, o))
      return true;
    return item_type.Equals(o.item_type);
  }

  public override int GetHashCode()
  {
    return name.GetHashCode();
  }
}

//TODO: should it implement INativeType?
//NOTE: operates on IList<T>
public class NativeListTypeSymbol<T> : NativeListTypeSymbol
{
  Func<Val, T> val2native;
  public Func<Val, T> Val2Native => val2native;

  Func<ProxyType, T, Val> native2val;
  public Func<ProxyType, T, Val> Native2Val => native2val;

  public NativeListTypeSymbol(
    Origin origin, string name,
    Func<Val, T> val2native,
    Func<ProxyType, T, Val> native2val,
    ProxyType item_type
  )
    : base(origin, name, item_type)
  {
    this.val2native = val2native;
    this.native2val = native2val;
  }

  protected override void DefineMembers()
  {
    base.DefineMembers();

    {
      var fn = new FuncSymbolNative(new Origin(), "At", item_type,
        (VM vm, VM.ExecState exec, FuncArgsInfo args_info) =>
        {
          int idx = exec.stack.Pop();
          var arr = exec.stack.Pop();

          var res = ArrGetAt(arr, idx);

          exec.stack.Push(res);

          arr.Release();
          return null;
        },
        new FuncArgSymbol("idx", Types.Int)
      );
      Define(fn);
    }
  }

  public override IList CreateList(VM vm)
  {
    return new List<T>();
  }

  public override void ArrAdd(Val arr, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst.Add(val2native(val));
  }

  public override void ArrInsert(Val arr, int idx, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst.Insert(idx, val2native(val));
  }

  public override Val ArrGetAt(Val arr, int idx)
  {
    var lst = (IList<T>)arr._obj;
    return native2val(item_type, lst[idx]);
  }

  public override void ArrSetAt(Val arr, int idx, Val val)
  {
    var lst = (IList<T>)arr._obj;
    lst[idx] = val2native(val);
  }

  public override int ArrIndexOf(Val arr, Val val)
  {
    var lst = (IList<T>)arr._obj;
    return lst.IndexOf(val2native(val));
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }
}

}

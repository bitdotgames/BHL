using System;
using System.Collections.Generic;

namespace bhl {

public abstract class ArrayTypeSymbol : ClassSymbol
{
  internal FuncSymbolNative FuncArrIdx;
  internal FuncSymbolNative FuncArrIdxW;

  public Proxy<IType> item_type;

  //marshall factory version
  public ArrayTypeSymbol()     
    : base(null, null)
  {}

  public ArrayTypeSymbol(Origin origin, string name, Proxy<IType> item_type)     
    : base(origin, name)
  {
    this.item_type = item_type;
  }

  public override void Setup()
  {
    if(item_type.IsEmpty())
      throw new Exception("Invalid item type");

    this.creator = CreateArr;

    //NOTE: must be first member of the class
    {
      var fn = new FuncSymbolNative(new Origin(), "Add", Types.Void, Add,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "RemoveAt", Types.Void, RemoveAt,
        new FuncArgSymbol("idx", Types.Int)
      );
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Clear", Types.Void, Clear);
      this.Define(fn);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "Insert", Types.Void, Insert,
        new FuncArgSymbol("idx", Types.Int),
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      var vs = new FieldSymbol(new Origin(), "Count", Types.Int, GetCount, null);
      this.Define(vs);
    }

    {
      var fn = new FuncSymbolNative(new Origin(), "IndexOf", Types.Int, IndexOf,
        new FuncArgSymbol("o", item_type)
      );
      this.Define(fn);
    }

    {
      //hidden system method not available directly
      FuncArrIdx = new FuncSymbolNative(new Origin(), "$ArrIdx", item_type, ArrIdx);
    }

    {
      //hidden system method not available directly
      FuncArrIdxW = new FuncSymbolNative(new Origin(), "$ArrIdxW", Types.Void, ArrIdxW);
    }

    base.Setup();
  }

  public ArrayTypeSymbol(Origin origin, string name)     
    : base(origin, name)
  {}

  public ArrayTypeSymbol(Origin origin, Proxy<IType> item_type) 
    : this(origin, "[]" + item_type.path, item_type)
  {}

  public abstract void CreateArr(VM.Frame frame, ref Val v, IType type);
  public abstract void GetCount(VM.Frame frame, Val ctx, ref Val v, FieldSymbol fld);
  public abstract Coroutine Add(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine IndexOf(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
  public abstract Coroutine Insert(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status);
}

public class GenericArrayTypeSymbol : ArrayTypeSymbol, IEquatable<GenericArrayTypeSymbol>, IEphemeralType
{
  public const uint CLASS_ID = 10;
    
  public GenericArrayTypeSymbol(Origin origin, Proxy<IType> item_type)
    : base(origin, item_type)
  {}
    
  //marshall factory version
  public GenericArrayTypeSymbol()
    : base()
  {}
    
  static IList<Val> AsList(Val arr)
  {
    var lst = arr.obj as IList<Val>;
    if(lst == null)
      throw new Exception("Not a ValList: " + (arr.obj != null ? arr.obj.GetType().Name : ""+arr));
    return lst;
  }

  public override void CreateArr(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(ValList.New(frm.vm), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    var lst = AsList(ctx);
    v.SetNum(lst.Count);
  }
  
  public override Coroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.Add(val);
    val.Release();
    arr.Release();
    return null;
  }

  public override Coroutine IndexOf(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = AsList(arr);

    int idx = -1;
    for(int i=0;i<lst.Count;++i)
    {
      if(lst[i].IsValueEqual(val))
      {
        idx = i;
        break;
      }
    }

    val.Release();
    arr.Release();
    stack.Push(Val.NewInt(frm.vm, idx));
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdx conventions
  public override Coroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = AsList(arr);
    var res = lst[idx]; 
    stack.PushRetain(res);
    arr.Release();
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdxW conventions
  public override Coroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var val = stack.Pop();
    var lst = AsList(arr);
    lst[idx] = val;
    val.Release();
    arr.Release();
    return null;
  }

  public override Coroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override Coroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.Clear();
    arr.Release();
    return null;
  }
  
  public override Coroutine Insert(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var idx = stack.Pop();
    var arr = stack.Pop();
    var lst = AsList(arr);
    lst.Insert((int)idx.num, val);
    idx.Release();
    val.Release();
    arr.Release();
    return null;
  }

  public override uint ClassId()
  {
    return CLASS_ID;
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    marshall.Marshall.Sync(ctx, ref item_type);

    if(ctx.is_read)
    {
      name = "[]" + item_type.path;

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

public class ArrayTypeSymbolT<T> : ArrayTypeSymbol where T : new()
{
  public delegate IList<T> CreatorCb();
  public static CreatorCb Creator;

  public ArrayTypeSymbolT(Origin origin, string name, Proxy<IType> item_type, CreatorCb creator) 
    : base(origin, name, item_type)
  {
    Creator = creator;
  }

  public ArrayTypeSymbolT(Origin origin, Proxy<IType> item_type, CreatorCb creator) 
    : base(origin, "[]" + item_type.path, item_type)
  {}

  public override void CreateArr(VM.Frame frm, ref Val v, IType type)
  {
    v.SetObj(Creator(), type);
  }

  public override void GetCount(VM.Frame frm, Val ctx, ref Val v, FieldSymbol fld)
  {
    v.SetNum(((IList<T>)ctx.obj).Count);
  }
  
  public override Coroutine Add(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Add((T)val.obj);
    val.Release();
    arr.Release();
    return null;
  }

  public override Coroutine IndexOf(VM.Frame frm, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    int idx = lst.IndexOf((T)val.obj);
    val.Release();
    arr.Release();
    stack.Push(Val.NewInt(frm.vm, idx));
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdx conventions
  public override Coroutine ArrIdx(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    var res = Val.NewObj(frame.vm, lst[idx], item_type.Get());
    stack.Push(res);
    arr.Release();
    return null;
  }

  //NOTE: follows special Opcodes.ArrIdxW conventions
  public override Coroutine ArrIdxW(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var val = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst[idx] = (T)val.obj;
    val.Release();
    arr.Release();
    return null;
  }

  public override Coroutine RemoveAt(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.RemoveAt(idx); 
    arr.Release();
    return null;
  }

  public override Coroutine Clear(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    int idx = (int)stack.PopRelease().num;
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Clear();
    arr.Release();
    return null;
  }
  
  public override Coroutine Insert(VM.Frame frame, ValStack stack, FuncArgsInfo args_info, ref BHS status)
  {
    var val = stack.Pop();
    var idx = stack.Pop();
    var arr = stack.Pop();
    var lst = (IList<T>)arr.obj;
    lst.Insert((int)idx.num, (T)val.obj); 
    arr.Release();
    idx.Release();
    val.Release();
    return null;
  }

  public override uint ClassId()
  {
    throw new NotImplementedException();
  }

  public override void Sync(marshall.SyncContext ctx)
  {
    throw new NotImplementedException();
  }
}

} //namespace bhl

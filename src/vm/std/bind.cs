using System;
using System.Collections.Generic;

#pragma warning disable CS8981

namespace bhl
{
public static partial class std
{
  public static class bind
  {
    static public Module MakeModule(Types ts)
    {
      var m = new Module(ts, "std/bind");

      var bind = m.ns.Nest("std").Nest("bind");

      var symbol_type = new ClassSymbolNative(new Origin(), "Symbol", null, null, typeof(bhl.Namespace));
      bind.Define(symbol_type);

      var ns_type = new ClassSymbolNative(new Origin(), "Namespace", null, null, typeof(bhl.Namespace));
      bind.Define(ns_type);
      {
        {
          var fn = new FuncSymbolNative(new Origin(), "Define", Types.Void,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var symbol = (Symbol)exec.stack.Pop().obj;
              exec.stack.Pop(); //for self

              ((Namespace)self.obj).Define(symbol);

              return null;
            },
            new FuncArgSymbol("symbol", symbol_type)
          );
          ns_type.Define(fn);
        }
      }
      ns_type.Setup();

      var proxy_type = new ClassSymbolNative(new Origin(), "ProxyType", null, null, typeof(bhl.ProxyType));
      bind.Define(proxy_type);
      proxy_type.Setup();

      {
        var cl = new ClassSymbolNative(
          new Origin(),
          "Types",
          null,
          (VM.ExecState exec, ref Val val, IType itype) =>
          {
            val.SetObj( new bhl.Types(), itype);
          },
          typeof(bhl.Types)
          );
        bind.Define(cl);

        cl.Define(new FieldSymbol(new Origin(), "ns", ns_type,
          (VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) =>
          {
            var self = (bhl.Types)ctx.obj;
            v.SetObj(self.ns, fld.GetIType());
          },
          null
          )
        );

        {
          var fn = new FuncSymbolNative(new Origin(), "T", proxy_type,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              string type = exec.stack.Pop();
              exec.stack.Pop(); //for self

              var t = new ProxyType((Namespace)self.obj, type);
              exec.stack.Push(Val.NewObj(t, proxy_type));
              return null;
            },
            new FuncArgSymbol("type", Types.String)
          );
          cl.Define(fn);
        }

        cl.Setup();
      }

      var fsn_type = new ClassSymbolNative(new Origin(), "FuncSymbolNative", symbol_type, null, null, typeof(bhl.FuncSymbolNative));
      bind.Define(fsn_type);
      fsn_type.Setup();

      var fsn_arg_type = new ClassSymbolNative(new Origin(), "FuncArgSymbol", null, null, typeof(bhl.FuncArgSymbol));
      bind.Define(fsn_arg_type);
      fsn_arg_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewFuncSymbolNative", fsn_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var args = (ValList)exec.stack.Pop().obj;
            List<FuncArgSymbol> func_args = new();
            foreach(var arg in args)
              func_args.Add((FuncArgSymbol)arg.obj);

            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var fsn = new FuncSymbolNative(
              new Origin(), //pass it from above?
              name,
              type_ref,
              null,
              func_args.ToArray()
              );
            exec.stack.Push(Val.NewObj(fsn, fsn_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type),
          new FuncArgSymbol("args", ts.TArr(fsn_arg_type))
        );
        bind.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "NewFuncArgSymbol", fsn_arg_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var farg = new FuncArgSymbol(name, type_ref);
            exec.stack.Push(Val.NewObj(farg, fsn_arg_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type)
        );
        bind.Define(fn);
      }

      return m;
    }
  }
}

}

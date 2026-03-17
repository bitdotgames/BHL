using System;
using System.Collections.Generic;

#pragma warning disable CS8981

namespace bhl
{
public static partial class std
{
  public static class bind
  {
    public static ClassSymbolNative TypesSymbol;

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
              var ns = (Namespace)self.obj;
              var symbol = (Symbol)exec.stack.Pop().obj;
              exec.stack.Pop(); //for self

              ns.Define(symbol);

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
              var types = (Types)self.obj;
              string type = exec.stack.Pop();
              exec.stack.Pop(); //for self

              var proxy = types.T(type);
              exec.stack.Push(Val.NewObj(proxy, proxy_type));
              return null;
            },
            new FuncArgSymbol("type", Types.String)
          );
          cl.Define(fn);
        }

        cl.Setup();
        //TODO: this looks a bit dirty but 'kinda ok' for now,
        //      maybe it makes sense to bind these symbols as static ones
        //      and just add them to the newly registered modules?
        TypesSymbol = cl;
      }

      var fsn_type = new ClassSymbolNative(new Origin(), "FuncSymbolNative", symbol_type, null, null, typeof(bhl.FuncSymbolNative));
      bind.Define(fsn_type);
      fsn_type.Setup();

      var fsn_arg_type = new ClassSymbolNative(new Origin(), "FuncArgSymbol", null, null, typeof(bhl.FuncArgSymbol));
      bind.Define(fsn_arg_type);
      fsn_arg_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewFuncSymbolNative", fsn_type, 2,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            FuncAttrib attribs = FuncAttrib.None;

            bool is_static = args_info.IsDefaultArgUsed(1) ? false : exec.stack.Pop();
            if(is_static)
              attribs |= FuncAttrib.Static;

            bool is_coro = args_info.IsDefaultArgUsed(0) ? false : exec.stack.Pop();
            if(is_coro)
              attribs |= FuncAttrib.Coro;

            var args = (ValList)exec.stack.Pop().obj;
            List<FuncArgSymbol> func_args = new();
            foreach(var arg in args)
              func_args.Add((FuncArgSymbol)arg.obj);
            args.Release();

            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var fsn = new FuncSymbolNative(
              new Origin(), //pass it from above?
              name,
              attribs,
              type_ref,
              0,
              null,
              func_args.ToArray()
              );
            exec.stack.Push(Val.NewObj(fsn, fsn_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type),
          new FuncArgSymbol("args", ts.TArr(fsn_arg_type)),
          new FuncArgSymbol("is_coro", Types.Bool),
          new FuncArgSymbol("is_static", Types.Bool)
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

      var fld_type = new ClassSymbolNative(new Origin(), "FieldSymbol",symbol_type, null, null, typeof(bhl.FieldSymbol));
      bind.Define(fld_type);
      fld_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewFieldSymbol", fld_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            bool has_setter = exec.stack.Pop();
            bool has_getter = exec.stack.Pop();
            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var fld = new FieldSymbol(
              new Origin(), //pass it from above?
              name,
              type_ref,
              has_getter ? delegate(VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) {} : null,
              has_setter ? delegate(VM.ExecState exec, ref Val ctx, Val v, FieldSymbol fld) {} : null
            );
            exec.stack.Push(Val.NewObj(fld, fld_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type),
          new FuncArgSymbol("has_getter", Types.Bool),
          new FuncArgSymbol("has_setter", Types.Bool)
        );
        bind.Define(fn);
      }

      var cln_type = new ClassSymbolNative(new Origin(), "ClassSymbolNative", symbol_type, null, null, typeof(bhl.ClassSymbolNative));
      bind.Define(cln_type);

      {
        var fn = new FuncSymbolNative(new Origin(), "Define", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var cl = (ClassSymbolNative)self.obj;
            var symbol = (Symbol)exec.stack.Pop().obj;
            exec.stack.Pop(); //for self

            cl.Define(symbol);

            return null;
          },
          new FuncArgSymbol("symbol", symbol_type)
        );
        cln_type.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "Setup", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var cl = (ClassSymbolNative)self.obj;
            exec.stack.Pop(); //for self

            cl.Setup();

            return null;
          }
        );
        cln_type.Define(fn);
      }

      cln_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewClassSymbolNative", cln_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            bool has_ctor = exec.stack.Pop();
            var parent_type_ref_obj = exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var cl = new ClassSymbolNative(
              new Origin(), //pass it from above?
              name,
              parent_type_ref_obj == null ? new ProxyType() : (ProxyType)parent_type_ref_obj,
              has_ctor ? delegate(VM.ExecState exec, ref Val v, IType type) {} : null
              );
            exec.stack.Push(Val.NewObj(cl, cln_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("parent_type", proxy_type),
          new FuncArgSymbol("has_ctor", Types.Bool)
        );
        bind.Define(fn);
      }

      var enum_type = new ClassSymbolNative(new Origin(), "EnumSymbolNative", symbol_type, null, null, typeof(bhl.EnumSymbolNative));
      bind.Define(enum_type);

      {
        var fn = new FuncSymbolNative(new Origin(), "DefineItem", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var enm = (EnumSymbolNative)self.obj;
            int value = exec.stack.Pop();
            string name = exec.stack.Pop();
            exec.stack.Pop(); //for self

            enm.Define(new EnumItemSymbol(new Origin(), name, value));

            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("value", Types.Int)
        );
        enum_type.Define(fn);
      }

      enum_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewEnumSymbolNative", enum_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string name = exec.stack.Pop();

            var enm = new EnumSymbolNative(
              new Origin(), //pass it from above?
              name,
              null
            );
            exec.stack.Push(Val.NewObj(enm, enum_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String)
        );
        bind.Define(fn);
      }

      return m;
    }
  }
}

}

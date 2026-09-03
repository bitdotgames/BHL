using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable CS8981

namespace bhl
{
public static partial class std
{
  public static class bind
  {
    public static ClassSymbolNative TypesSymbol;

    //NOTE: DSL-internal bookkeeping for NewClassSymbolScript/DefineVirtualMethod, kept out of ClassSymbolScript/ModuleDeclared, weak so it never outlives its owner
    static readonly ConditionalWeakTable<ClassSymbolScript, ModuleDeclared> _class_owner_module = new();
    static readonly ConditionalWeakTable<ModuleDeclared, List<byte>> _module_stub_bytecode = new();

    //NOTE: hand-assembles a stub body matching EmitFuncDecl's output for an empty function (Frame + Return); v1 is void-only, no constant pool needed
    static void DefineStubBody(FuncSymbolScript fs, ModuleDeclared owner, int args_num)
    {
      if(fs.signature.return_type.Get() != Types.Void)
        throw new System.Exception("DefineVirtualMethod: only void-returning stubs are supported for now");

      var bytecode = _module_stub_bytecode.GetValue(owner, _ => new List<byte>());

      //NOTE: a bindings module never goes through ModuleDeclared.Setup's SetupFuncSymbol (only explicitly-loaded modules do), so _module is assigned by hand
      fs._module = owner;
      fs._ip_addr = bytecode.Count;
      bytecode.Add((byte)Opcodes.Frame);
      bytecode.Add((byte)args_num); //locals_vars_num - args are the stub's only locals
      bytecode.Add(0); //return_vars_num - void only
      bytecode.Add((byte)Opcodes.Return);

      owner.InitWithCompiled(new CompiledModule(
        -1, new List<string>(), 0,
        System.Array.Empty<Const>(), new TypeRefIndex(),
        System.Array.Empty<byte>(), bytecode.ToArray(),
        new Ip2SrcLine()
      ));
    }

    //NOTE: plain C# entry points for native bindings (e.g. Unity's UnityBindings.cs) - the
    //      script-callable NewClassSymbolScript/DefineVirtualMethod below are thin wrappers over these,
    //      same relationship as ClassSymbolNative/FuncSymbolNative have to their own DSL functions
    public static ClassSymbolScript NewClassSymbolScript(ModuleDeclared module, string name, ClassSymbol super_class = null, IList<InterfaceSymbol> implements = null)
    {
      var cl = new ClassSymbolScript(new Origin(), name);
      cl.SetSuperClassAndInterfaces(super_class, implements);
      _class_owner_module.Add(cl, module);
      return cl;
    }

    //NOTE: always virtual and bodyless - a non-virtual stub could never be overridden or do anything, so there's no point offering that
    public static FuncSymbolScript DefineVirtualMethod(ClassSymbolScript cl, string name, ProxyType ret_type, FuncArgSymbol[] args)
    {
      if(!_class_owner_module.TryGetValue(cl, out var owner))
        throw new System.Exception("DefineVirtualMethod: class '" + name + "' has no owning module - was it created via NewClassSymbolScript?");

      var fs = new FuncSymbolScript(new Origin(), name, FuncAttrib.Virtual, ret_type, args);
      //NOTE: the ctor's signature.attribs mask covers Coro/VariadicArgs only - Virtual needs this
      fs.attribs = FuncAttrib.Virtual;
      DefineStubBody(fs, owner, args.Length);
      cl.Define(fs);
      return fs;
    }

    static public ModuleDeclared MakeModule(Types ts)
    {
      var m = new ModuleDeclared("std/bind");

      var bind = m.ns.Nest("std").Nest("bind");

      var symbol_type = new ClassSymbolNative(new Origin(), "Symbol", typeof(bhl.Namespace));
      bind.Define(symbol_type);

      var ns_type = new ClassSymbolNative(new Origin(), "Namespace", typeof(bhl.Namespace));
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

        {
          var fn = new FuncSymbolNative(new Origin(), "IsDefined", Types.Bool,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var ns = (Namespace)self.obj;
              string name = exec.stack.Pop();
              exec.stack.Pop(); //for self

              exec.stack.Push(Val.NewBool(ns.Resolve(name) != null));

              return null;
            },
            new FuncArgSymbol("name", Types.String)
          );
          ns_type.Define(fn);
        }

        {
          var fn = new FuncSymbolNative(new Origin(), "Nest", ns_type,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var ns = (Namespace)self.obj;
              string name = exec.stack.Pop();
              exec.stack.Pop(); //for self

              exec.stack.Push(Val.NewObj(ns.Nest(name), ns_type));

              return null;
            },
            new FuncArgSymbol("name", Types.String)
          );
          ns_type.Define(fn);
        }
      }
      ns_type.Setup();

      var proxy_type = new ClassSymbolNative(new Origin(), "ProxyType", typeof(bhl.ProxyType));
      bind.Define(proxy_type);
      proxy_type.Setup();

      var module_type = new ClassSymbolNative(new Origin(), "ModuleDeclared", typeof(bhl.ModuleDeclared));
      bind.Define(module_type);
      module_type.Define(new FieldSymbol(new Origin(), "ns", ns_type,
        (VM.ExecState exec, Val ctx, ref Val v, FieldSymbol fld) =>
        {
          var self = (bhl.ModuleDeclared)ctx.obj;
          v.SetObj(self.ns, fld.GetIType());
        },
        null
        )
      );
      module_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewModuleDeclared", module_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            string name = exec.stack.Pop();
            exec.stack.Push(Val.NewObj(new bhl.ModuleDeclared(name), module_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String)
        );
        bind.Define(fn);
      }

      {
        var cl = new ClassSymbolNative(
          new Origin(),
          "Types",
          typeof(bhl.Types),
          (VM.ExecState exec, ref Val val, IType itype) =>
          {
            val.SetObj( new bhl.Types(), itype);
          }
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

        {
          var fn = new FuncSymbolNative(new Origin(), "TArr", proxy_type,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var types = (Types)self.obj;
              var type = (ProxyType)exec.stack.Pop().obj;
              exec.stack.Pop(); //for self

              var proxy = types.TArr(type);
              exec.stack.Push(Val.NewObj(proxy, proxy_type));
              return null;
            },
            new FuncArgSymbol("type", proxy_type)
          );
          cl.Define(fn);
        }

        {
          var fn = new FuncSymbolNative(new Origin(), "TTuple", FuncAttrib.VariadicArgs, proxy_type, 0,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var types = (Types)self.obj;
              var vargs = (ValList)exec.stack.Pop().obj;
              var vargs_list = new List<ScopeExtensions.TypeArg>(vargs.Count);
              foreach(var varg in vargs)
                vargs_list.Add(new ScopeExtensions.TypeArg((ProxyType)varg.obj));
              vargs.Release();
              exec.stack.Pop(); //for self

              var proxy = types.TTuple(vargs_list.ToArray());
              exec.stack.Push(Val.NewObj(proxy, proxy_type));
              return null;
            },
            new FuncArgSymbol("types", ts.TArr(proxy_type))
          );
          cl.Define(fn);
        }

        {
          var fn = new FuncSymbolNative(new Origin(), "TFunc", FuncAttrib.VariadicArgs, proxy_type, 0,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var types = (Types)self.obj;
              var vargs = (ValList)exec.stack.Pop().obj;
              var vargs_list = new List<ScopeExtensions.TypeArg>(vargs.Count);
              foreach(var varg in vargs)
                vargs_list.Add(new ScopeExtensions.TypeArg((ProxyType)varg.obj));
              vargs.Release();
              var return_type = (ProxyType)exec.stack.Pop().obj;
              bool is_coro = exec.stack.Pop();
              exec.stack.Pop(); //for self

              var proxy = types.TFunc(is_coro, return_type, vargs_list.ToArray());
              exec.stack.Push(Val.NewObj(proxy, proxy_type));
              return null;
            },
            new FuncArgSymbol("is_coro", Types.Bool),
            new FuncArgSymbol("return_type", proxy_type),
            new FuncArgSymbol("args", ts.TArr(proxy_type))
          );
          cl.Define(fn);
        }

        {
          var fn = new FuncSymbolNative(new Origin(), "SetupType", Types.Void,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var types = (Types)self.obj;
              string name = exec.stack.Pop();
              exec.stack.Pop(); //for self

              var tmp = types.T(name).Get();
              if(tmp == null)
                throw new System.Exception("Type '" + name + "' not resolved");

              if(tmp is ClassSymbolNative csn)
                csn.Setup();
              else if(tmp is InterfaceSymbolNative isn)
                isn.Setup();

              return null;
            },
            new FuncArgSymbol("name", Types.String)
          );
          cl.Define(fn);
        }

        {
          var fn = new FuncSymbolNative(new Origin(), "RegisterModule", Types.Void,
            (VM.ExecState exec, FuncArgsInfo args_info) =>
            {
              ref var self = ref exec.GetSelfRef();
              var types = (Types)self.obj;
              var module = (bhl.ModuleDeclared)exec.stack.Pop().obj;
              exec.stack.Pop(); //for self

              types.RegisterModule(module);

              return null;
            },
            new FuncArgSymbol("module", module_type)
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

      var fsn_arg_type = new ClassSymbolNative(new Origin(), "FuncArgSymbol", typeof(bhl.FuncArgSymbol));
      bind.Define(fsn_arg_type);
      fsn_arg_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewFuncSymbolNative", fsn_type, 3,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {

            int default_args_num = args_info.IsDefaultArgUsed(2) ? 0 : exec.stack.Pop();

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
              default_args_num,
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
          new FuncArgSymbol("is_static", Types.Bool),
          new FuncArgSymbol("default_args_num", Types.Int)
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

      var cl_type = new ClassSymbolNative(new Origin(), "ClassSymbolNative", symbol_type, null, null, typeof(bhl.ClassSymbolNative));
      bind.Define(cl_type);

      {
        var fn = new FuncSymbolNative(new Origin(), "Define", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var cl = (ClassSymbol)self.obj;
            var symbol = (Symbol)exec.stack.Pop().obj;
            exec.stack.Pop(); //for self

            cl.Define(symbol);

            return null;
          },
          new FuncArgSymbol("symbol", symbol_type)
        );
        cl_type.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "Setup", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var cl = (ClassSymbol)self.obj;
            exec.stack.Pop(); //for self

            cl.Setup();

            return null;
          }
        );
        cl_type.Define(fn);
      }

      cl_type.Setup();

      var ifs_type = new ClassSymbolNative(new Origin(), "InterfaceSymbolNative", symbol_type, null, null, typeof(bhl.InterfaceSymbolNative));
      bind.Define(ifs_type);
      ifs_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewClassSymbolNative", cl_type, 1,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ValList implements = args_info.IsDefaultArgUsed(0) ? null : (ValList)exec.stack.Pop().obj;
            IList<ProxyType> proxy_implements = null;
            if(implements != null)
            {
              proxy_implements = new ProxyType[implements.Count];
              for (int i = 0; i < implements.Count; i++)
                proxy_implements[i] = (ProxyType)implements[i].obj;
            }
            implements?.Release();

            bool has_ctor = exec.stack.Pop();
            var parent_type_ref_obj = exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var cl = new ClassSymbolNative(
              new Origin(), //pass it from above?
              name,
              parent_type_ref_obj == null ? new ProxyType() : (ProxyType)parent_type_ref_obj,
              proxy_implements,
              has_ctor ? delegate(VM.ExecState exec, ref Val v, IType type) {} : null
            );
            exec.stack.Push(Val.NewObj(cl, cl_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("parent_type", proxy_type),
          new FuncArgSymbol("has_ctor", Types.Bool),
          new FuncArgSymbol("implements", ts.TArr(proxy_type))
        );
        bind.Define(fn);
      }

      {
        var fn = new FuncSymbolNative(new Origin(), "NewInterfaceSymbolNative", ifs_type, 1,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ValList inherits = args_info.IsDefaultArgUsed(0) ? null : (ValList)exec.stack.Pop().obj;
            IList<ProxyType> proxy_inherits = null;
            if(inherits != null)
            {
              proxy_inherits = new ProxyType[inherits.Count];
              for (int i = 0; i < inherits.Count; i++)
                proxy_inherits[i] = (ProxyType)inherits[i].obj;
            }
            inherits?.Release();

            var func_args = (ValList)exec.stack.Pop().obj;
            var funcs_args_list = new List<FuncSymbol>(func_args.Count);
            foreach(var func_arg in func_args)
              funcs_args_list.Add((FuncSymbol)func_arg.obj);
            func_args.Release();

            string name = exec.stack.Pop();

            var ifs = new InterfaceSymbolNative(
              new Origin(), //pass it from above?
              name,
              proxy_inherits,
              funcs_args_list.ToArray()
            );
            exec.stack.Push(Val.NewObj(ifs, ifs_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("funcs", ts.TArr(fsn_type)),
          new FuncArgSymbol("inherits", ts.TArr(proxy_type))
        );
        bind.Define(fn);
      }

      var ifss_type = new ClassSymbolNative(new Origin(), "InterfaceSymbolScript", symbol_type, null, null, typeof(bhl.InterfaceSymbolScript));
      bind.Define(ifss_type);

      {
        var fn = new FuncSymbolNative(new Origin(), "DefineMethod", Types.Void, 1,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var ifs = (InterfaceSymbolScript)self.obj;

            bool is_coro = args_info.IsDefaultArgUsed(0) ? false : exec.stack.Pop();

            var args = (ValList)exec.stack.Pop().obj;
            var func_args = new FuncArgSymbol[args.Count];
            for(int i = 0; i < args.Count; ++i)
              func_args[i] = (FuncArgSymbol)args[i].obj;
            args.Release();
            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();
            exec.stack.Pop(); //for self

            ifs.DefineMethod(name, type_ref, is_coro, func_args);

            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type),
          new FuncArgSymbol("args", ts.TArr(fsn_arg_type)),
          new FuncArgSymbol("is_coro", Types.Bool)
        );
        ifss_type.Define(fn);
      }

      ifss_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewInterfaceSymbolScript", ifss_type, 1,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ValList inherits = args_info.IsDefaultArgUsed(0) ? null : (ValList)exec.stack.Pop().obj;
            List<InterfaceSymbol> inherits_list = null;
            if(inherits != null)
            {
              inherits_list = new List<InterfaceSymbol>(inherits.Count);
              foreach(var inh in inherits)
                inherits_list.Add((InterfaceSymbol)((ProxyType)inh.obj).Get());
            }
            inherits?.Release();

            string name = exec.stack.Pop();

            var ifs = new InterfaceSymbolScript(new Origin(), name);
            if(inherits_list != null)
              ifs.SetInherits(inherits_list);

            exec.stack.Push(Val.NewObj(ifs, ifss_type));
            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("inherits", ts.TArr(proxy_type))
        );
        bind.Define(fn);
      }

      //NOTE: unlike ClassSymbolNative, real .bhl classes can ': Base' this (see the native-class-extension check in antlr_proc.pass.cs); methods are signature-only stubs (_ip_addr == -1), same as InterfaceSymbolScript.DefineMethod
      var clss_type = new ClassSymbolNative(new Origin(), "ClassSymbolScript", cl_type, null, null, typeof(bhl.ClassSymbolScript));
      bind.Define(clss_type);

      {
        var fn = new FuncSymbolNative(new Origin(), "DefineVirtualMethod", Types.Void,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ref var self = ref exec.GetSelfRef();
            var cl = (bhl.ClassSymbolScript)self.obj;

            var args = (ValList)exec.stack.Pop().obj;
            var func_args = new FuncArgSymbol[args.Count];
            for(int i = 0; i < args.Count; ++i)
              func_args[i] = (FuncArgSymbol)args[i].obj;
            args.Release();
            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();
            exec.stack.Pop(); //for self

            DefineVirtualMethod(cl, name, type_ref, func_args);

            return null;
          },
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("type", proxy_type),
          new FuncArgSymbol("args", ts.TArr(fsn_arg_type))
        );
        clss_type.Define(fn);
      }

      clss_type.Setup();

      {
        var fn = new FuncSymbolNative(new Origin(), "NewClassSymbolScript", clss_type, 2,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            ValList implements = args_info.IsDefaultArgUsed(1) ? null : (ValList)exec.stack.Pop().obj;
            List<InterfaceSymbol> implements_list = null;
            if(implements != null)
            {
              implements_list = new List<InterfaceSymbol>(implements.Count);
              foreach(var impl in implements)
                implements_list.Add((InterfaceSymbol)((ProxyType)impl.obj).Get());
            }
            implements?.Release();

            var parent_type_ref_obj = args_info.IsDefaultArgUsed(0) ? null : exec.stack.Pop().obj;
            string name = exec.stack.Pop();
            var module = (bhl.ModuleDeclared)exec.stack.Pop().obj;

            var super_class = parent_type_ref_obj == null ? null : (ClassSymbol)((ProxyType)parent_type_ref_obj).Get();
            var cl = NewClassSymbolScript(module, name, super_class, implements_list);

            exec.stack.Push(Val.NewObj(cl, clss_type));
            return null;
          },
          new FuncArgSymbol("module", module_type),
          new FuncArgSymbol("name", Types.String),
          new FuncArgSymbol("parent_type", proxy_type),
          new FuncArgSymbol("implements", ts.TArr(proxy_type))
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

      {
        var fn = new FuncSymbolNative(new Origin(), "NewNativeListTypeSymbol", cl_type,
          (VM.ExecState exec, FuncArgsInfo args_info) =>
          {
            var type_ref = (ProxyType)exec.stack.Pop().obj;
            string name = exec.stack.Pop();

            var cl = new NativeListTypeSymbol<object>(
              new Origin(),
              name,
              (v) => null,
              (itype, n) => null,
              type_ref
            );

            exec.stack.Push(Val.NewObj(cl, cl_type));
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

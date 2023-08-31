using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using bhl;
using bhl.marshall;

public class TestMarshall : BHL_TestBase
{
  [IsTested()]
  public void TestSerializeModuleSymbols()
  {
    var s = new MemoryStream();
    {
      var ts = new Types();
      var m = new Module(ts);

      var ns = m.ns;
      ns.Link(ts.ns);

      ns.Define(new VariableSymbol(new Origin(), "foo", Types.Int));

      ns.Define(new VariableSymbol(new Origin(), "bar", Types.String));

      ns.Define(new VariableSymbol(new Origin(), "wow", ns.TArr(Types.Bool)));

      ns.Define(new VariableSymbol(new Origin(), "hey", ns.TMap(Types.String, Types.Int)));

      var Test = new FuncSymbolScript(new Origin(), new FuncSignature(ns.T(Types.Int,Types.Float), ns.TRef(Types.Int), Types.String), "Test", 1, 155);
      Test.Define(new FuncArgSymbol("a", Types.Int, is_ref: true));
      Test.Define(new FuncArgSymbol("s", Types.String));
      ns.Define(Test);

      var Make = new FuncSymbolScript(new Origin(), new FuncSignature(FuncSignatureAttrib.Coro, ns.TArr(Types.String), ns.T("Bar")), "Make", 3, 15);
      Make.Define(new FuncArgSymbol("bar", ns.T("Bar")));
      ns.Define(Make);

      var Foo = new ClassSymbolScript(new Origin(), "Foo");
      Foo.Define(new FieldSymbolScript(new Origin(), "Int", Types.Int));
      Foo.Define(new FuncSymbolScript(new Origin(), new FuncSignature(Types.Void), "Hey", 0, 3));
      ns.Define(Foo);
      var Bar = new ClassSymbolScript(new Origin(), "Bar");
      Bar.SetSuperAndInterfaces(Foo);
      Bar.Define(new FieldSymbolScript(new Origin(), "Float", Types.Float));
      Bar.Define(new FuncSymbolScript(new Origin(), new FuncSignature(ns.T(Types.Bool,Types.Bool), Types.Int), "What", 1, 1));
      ns.Define(Bar);

      var Enum = new EnumSymbolScript(new Origin(), "Enum");
      Enum.TryAddItem(null, "Type1", 1);
      Enum.TryAddItem(null, "Type2", 2);
      ns.Define(Enum);

      Marshall.Obj2Stream(ns, s);
    }

    {
      var ts = new Types();

      var ns = new Namespace();
      ns.Link(ts.ns);

      var factory = new SymbolFactory(ts, resolver: ns);

      s.Position = 0;
      Marshall.Stream2Obj(s, ns, factory);

      //NOTE: right after un-marshalling some symbols are not setup yet
      ns.SetupSymbols();

      AssertEqual(9 + ts.ns.members.Count, ns.GetSymbolsIterator().Count);
      AssertEqual(9, ns.members.Count);

      var foo = (VariableSymbol)ns.Resolve("foo");
      AssertEqual(foo.name, "foo");
      AssertEqual(foo.type.Get(), Types.Int);
      AssertEqual(foo.scope, ns);
      AssertEqual(foo.scope_idx, 0);

      var bar = (VariableSymbol)ns.Resolve("bar");
      AssertEqual(bar.name, "bar");
      AssertEqual(bar.type.Get(), Types.String);
      AssertEqual(bar.scope, ns);
      AssertEqual(bar.scope_idx, 1);

      var wow = (VariableSymbol)ns.Resolve("wow");
      AssertEqual(wow.name, "wow");
      AssertEqual(wow.type.Get().GetName(), ns.TArr(Types.Bool).Get().GetName());
      AssertEqual(((GenericArrayTypeSymbol)wow.type.Get()).item_type.Get(), Types.Bool);
      AssertEqual(wow.scope, ns);
      AssertEqual(wow.scope_idx, 2);

      var hey = (VariableSymbol)ns.Resolve("hey");
      AssertEqual(hey.name, "hey");
      AssertEqual(hey.type.Get().GetName(), ns.TMap(Types.String, Types.Int).Get().GetName());
      AssertEqual(((GenericMapTypeSymbol)hey.type.Get()).key_type.Get(), Types.String);
      AssertEqual(hey.scope, ns);
      AssertEqual(hey.scope_idx, 3);

      var Test = (FuncSymbolScript)ns.Resolve("Test");
      AssertEqual(Test.name, "Test");
      AssertFalse(Test.attribs.HasFlag(FuncAttrib.Coro));
      AssertEqual(Test.scope, ns);
      AssertEqual(ns.TFunc(ns.T(Types.Int, Types.Float), ns.TRef(Types.Int), Types.String).path, Test.signature.GetName());
      AssertEqual(1, Test.default_args_num);
      AssertEqual(2, Test.local_vars_num);
      AssertEqual(155, Test.ip_addr);
      AssertEqual(4, Test.scope_idx);
      AssertEqual(2, Test.GetTotalArgsNum());
      AssertEqual("a", Test.GetArg(0).name);
      AssertTrue(Test.GetArg(0).is_ref);
      AssertEqual(Types.Int, Test.GetArg(0).type.Get());
      AssertEqual("s", Test.GetArg(1).name);
      AssertFalse(Test.GetArg(1).is_ref);
      AssertEqual(Types.String, Test.GetArg(1).type.Get());

      var Make = (FuncSymbolScript)ns.Resolve("Make");
      AssertEqual(Make.name, "Make");
      AssertTrue(Make.attribs.HasFlag(FuncAttrib.Coro));
      AssertEqual(Make.scope, ns);
      AssertEqual(1, Make.signature.arg_types.Count);
      AssertEqual(1, Make.GetTotalArgsNum());
      AssertEqual("bar", Make.GetArg(0).name);
      AssertFalse(Make.GetArg(0).is_ref);
      AssertEqual(ns.T("Bar").Get(), Make.GetArg(0).type.Get());
      AssertEqual(ns.TArr(Types.String).Get().GetName(), Make.GetReturnType().GetName());
      AssertEqual(ns.T("Bar").Get(), Make.signature.arg_types[0].Get());
      AssertEqual(3, Make.default_args_num);
      AssertEqual(1, Make.local_vars_num);
      AssertEqual(15, Make.ip_addr);
      AssertEqual(5, Make.scope_idx);

      var Foo = (ClassSymbolScript)ns.Resolve("Foo");
      AssertEqual(Foo.scope, ns);
      AssertTrue(Foo.super_class == null);
      AssertEqual(Foo.name, "Foo");
      AssertEqual(Foo.GetSymbolsIterator().Count, 2);
      var Foo_Int = Foo.Resolve("Int") as FieldSymbolScript;
      AssertEqual(Foo_Int.scope, Foo);
      AssertEqual(Foo_Int.name, "Int");
      AssertEqual(Foo_Int.type.Get(), Types.Int);
      AssertEqual(Foo_Int.scope_idx, 0);
      var Foo_Hey = Foo.Resolve("Hey") as FuncSymbolScript;
      AssertEqual(Foo_Hey.scope, Foo);
      AssertEqual(Foo_Hey.name, "Hey");
      AssertEqual(Foo_Hey.GetReturnType(), Types.Void);
      AssertEqual(0, Foo_Hey.default_args_num);
      AssertEqual(0, Foo_Hey.local_vars_num);
      AssertEqual(3, Foo_Hey.ip_addr);
      AssertEqual(1, Foo_Hey.scope_idx);

      var Bar = (ClassSymbolScript)ns.Resolve("Bar");
      AssertEqual(Bar.scope, ns);
      AssertEqual(Bar.super_class, Foo);
      AssertEqual(Bar.name, "Bar");
      AssertEqual(Bar.GetSymbolsIterator().Count, 2/*from parent*/+2);
      var Bar_Float = Bar.Resolve("Float") as FieldSymbolScript;
      AssertEqual(Bar_Float.scope, Bar);
      AssertEqual(Bar_Float.name, "Float");
      AssertEqual(Bar_Float.type.Get(), Types.Float);
      AssertEqual(Bar_Float.scope_idx, 2);
      var Bar_What = Bar.Resolve("What") as FuncSymbolScript;
      AssertEqual(Bar_What.name, "What");
      AssertEqual(Bar_What.GetReturnType().GetName(), ns.T(Types.Bool, Types.Bool).Get().GetName());
      AssertEqual(Bar_What.signature.arg_types[0].Get(), Types.Int);
      AssertEqual(1, Bar_What.default_args_num);
      AssertEqual(0, Bar_What.local_vars_num);
      AssertEqual(1, Bar_What.ip_addr);

      var Enum = (EnumSymbolScript)ns.Resolve("Enum");
      AssertEqual(Enum.scope, ns);
      AssertEqual(Enum.name, "Enum");
      AssertEqual(Enum.members.Count, 2);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type1")).owner, Enum);
      AssertEqual(Enum.Resolve("Type1").scope, Enum);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type1")).val, 1);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type2")).owner, Enum);
      AssertEqual(Enum.Resolve("Type2").scope, Enum);
      AssertEqual(((EnumItemSymbol)Enum.Resolve("Type2")).val, 2);
    }
  }
}

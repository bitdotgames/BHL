using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using bhl;
using bhl.marshall;
using Xunit;

public class TestMarshall : BHL_TestBase
{
  [Fact]
  public void TestSerializeModuleSymbols()
  {
    var s = new MemoryStream();
    {
      var ts = new Types();
      var m = new Module(ts, "dummy");

      var ns = m.ns;
      ns.Link(ts.ns);

      ns.Define(new VariableSymbol(new Origin(), "foo", Types.Int));

      ns.Define(new VariableSymbol(new Origin(), "bar", Types.String));

      ns.Define(new VariableSymbol(new Origin(), "wow", ns.TArr(Types.Bool)));

      ns.Define(new VariableSymbol(new Origin(), "hey", ns.TMap(Types.String, Types.Int)));

      var Test = new FuncSymbolScript(new Origin(), new FuncSignature(ns.T(Types.Int,Types.Float), Types.String, ns.TRef(Types.Int)), "Test", 1, 155);
      Test.Define(new FuncArgSymbol("s", Types.String));
      Test.Define(new FuncArgSymbol("a", Types.Int, is_ref: true));
      ns.Define(Test);

      var Make = new FuncSymbolScript(new Origin(), new FuncSignature(FuncSignatureAttrib.Coro, ns.TArr(Types.String), ns.T("Bar")), "Make", 3, 15);
      Make.Define(new FuncArgSymbol("bar", ns.T("Bar")));
      ns.Define(Make);

      var Foo = new ClassSymbolScript(new Origin(), "Foo");
      ns.Define(Foo);
      Foo.Define(new FieldSymbolScript(new Origin(), "Int", Types.Int));
      Foo.Define(new FuncSymbolScript(new Origin(), new FuncSignature(Types.Void), "Hey", 0, 3));
      
      var Bar = new ClassSymbolScript(new Origin(), "Bar");
      Bar.SetSuperClassAndInterfaces(Foo);
      ns.Define(Bar);
      Bar.Define(new FieldSymbolScript(new Origin(), "Float", Types.Float));
      Bar.Define(new FuncSymbolScript(new Origin(), new FuncSignature(ns.T(Types.Bool,Types.Bool), Types.Int), "What", 1, 1));

      var Enum = new EnumSymbolScript(new Origin(), "Enum");
      Enum.TryAddItem(null, "Type1", 1);
      Enum.TryAddItem(null, "Type2", 2);
      ns.Define(Enum);

      CompiledModule.ToStream(m, s, leave_open: true);
    }

    {
      var ts = new Types();

      s.Position = 0;
      var m = CompiledModule.FromStream(ts, s);
      
      //NOTE: right after un-marshalling module must be setup explicitly
      m.Setup(name => null);

      var ns = m.ns;

      Assert.Equal(9 + ts.ns.members.Count, ns.Count());
      Assert.Equal(9, ns.members.Count);

      var foo = (VariableSymbol)ns.Resolve("foo");
      AssertEqual(foo.name, "foo");
      Assert.Equal(foo.type.Get(), Types.Int);
      Assert.Equal(foo.scope, ns);
      Assert.Equal(0, foo.scope_idx);

      var bar = (VariableSymbol)ns.Resolve("bar");
      AssertEqual(bar.name, "bar");
      Assert.Equal(bar.type.Get(), Types.String);
      Assert.Equal(bar.scope, ns);
      Assert.Equal(1, bar.scope_idx);

      var wow = (VariableSymbol)ns.Resolve("wow");
      AssertEqual(wow.name, "wow");
      AssertEqual(wow.type.Get().GetName(), ns.TArr(Types.Bool).Get().GetName());
      Assert.Equal(((GenericArrayTypeSymbol)wow.type.Get()).item_type.Get(), Types.Bool);
      Assert.Equal(wow.scope, ns);
      Assert.Equal(2, wow.scope_idx);

      var hey = (VariableSymbol)ns.Resolve("hey");
      AssertEqual(hey.name, "hey");
      AssertEqual(hey.type.Get().GetName(), ns.TMap(Types.String, Types.Int).Get().GetName());
      Assert.Equal(((GenericMapTypeSymbol)hey.type.Get()).key_type.Get(), Types.String);
      Assert.Equal(hey.scope, ns);
      Assert.Equal(3, hey.scope_idx);

      var Test = (FuncSymbolScript)ns.Resolve("Test");
      AssertEqual(Test.name, "Test");
      Assert.False(Test.attribs.HasFlag(FuncAttrib.Coro));
      Assert.Equal(Test.scope, ns);
      AssertEqual(ns.TFunc(ns.T(Types.Int, Types.Float), Types.String, ns.TRef(Types.Int)).ToString(), Test.signature.ToString());
      Assert.Equal(1, Test.default_args_num);
      Assert.Equal(2, Test.local_vars_num);
      Assert.Equal(155, Test.ip_addr);
      Assert.Equal(4, Test.scope_idx);
      Assert.Equal(2, Test.GetTotalArgsNum());
      AssertEqual("s", Test.GetArg(0).name);
      Assert.False(Test.GetArg(0).is_ref);
      Assert.Equal(Types.String, Test.GetArg(0).type.Get());
      AssertEqual("a", Test.GetArg(1).name);
      Assert.True(Test.GetArg(1).is_ref);
      Assert.Equal(Types.Int, Test.GetArg(1).type.Get());

      var Make = (FuncSymbolScript)ns.Resolve("Make");
      AssertEqual(Make.name, "Make");
      Assert.True(Make.attribs.HasFlag(FuncAttrib.Coro));
      Assert.Equal(Make.scope, ns);
      Assert.Single(Make.signature.arg_types);
      Assert.Equal(1, Make.GetTotalArgsNum());
      AssertEqual("bar", Make.GetArg(0).name);
      Assert.False(Make.GetArg(0).is_ref);
      Assert.Equal(ns.T("Bar").Get(), Make.GetArg(0).type.Get());
      AssertEqual(ns.TArr(Types.String).Get().GetName(), Make.GetReturnType().GetName());
      Assert.Equal(ns.T("Bar").Get(), Make.signature.arg_types[0].Get());
      Assert.Equal(3, Make.default_args_num);
      Assert.Equal(1, Make.local_vars_num);
      Assert.Equal(15, Make.ip_addr);
      Assert.Equal(5, Make.scope_idx);

      var Foo = (ClassSymbolScript)ns.Resolve("Foo");
      Assert.Equal(Foo.scope, ns);
      Assert.True(Foo.super_class == null);
      AssertEqual(Foo.name, "Foo");
      Assert.Equal(2, Foo.Count());
      var Foo_Int = Foo.Resolve("Int") as FieldSymbolScript;
      Assert.Equal(Foo_Int.scope, Foo);
      AssertEqual(Foo_Int.name, "Int");
      Assert.Equal(Foo_Int.type.Get(), Types.Int);
      Assert.Equal(0, Foo_Int.scope_idx);
      var Foo_Hey = Foo.Resolve("Hey") as FuncSymbolScript;
      Assert.Equal(Foo_Hey.scope, Foo);
      AssertEqual(Foo_Hey.name, "Hey");
      Assert.Equal(Foo_Hey.GetReturnType(), Types.Void);
      Assert.Equal(0, Foo_Hey.default_args_num);
      Assert.Equal(0, Foo_Hey.local_vars_num);
      Assert.Equal(3, Foo_Hey.ip_addr);
      Assert.Equal(1, Foo_Hey.scope_idx);

      var Bar = (ClassSymbolScript)ns.Resolve("Bar");
      Assert.Equal(Bar.scope, ns);
      Assert.Equal(Bar.super_class, Foo);
      AssertEqual(Bar.name, "Bar");
      Assert.Equal(2/*from parent*/+2, Bar.Count());
      var Bar_Float = Bar.Resolve("Float") as FieldSymbolScript;
      Assert.Equal(Bar_Float.scope, Bar);
      AssertEqual(Bar_Float.name, "Float");
      Assert.Equal(Bar_Float.type.Get(), Types.Float);
      Assert.Equal(2, Bar_Float.scope_idx);
      var Bar_What = Bar.Resolve("What") as FuncSymbolScript;
      AssertEqual(Bar_What.name, "What");
      AssertEqual(Bar_What.GetReturnType().GetName(), ns.T(Types.Bool, Types.Bool).Get().GetName());
      Assert.Equal(Bar_What.signature.arg_types[0].Get(), Types.Int);
      Assert.Equal(1, Bar_What.default_args_num);
      Assert.Equal(0, Bar_What.local_vars_num);
      Assert.Equal(1, Bar_What.ip_addr);

      var Enum = (EnumSymbolScript)ns.Resolve("Enum");
      Assert.Equal(Enum.scope, ns);
      AssertEqual(Enum.name, "Enum");
      Assert.Equal(2, Enum.Count());
      Assert.Equal(((EnumItemSymbol)Enum.Resolve("Type1")).owner, Enum);
      Assert.Equal(Enum.Resolve("Type1").scope, Enum);
      Assert.Equal(1, ((EnumItemSymbol)Enum.Resolve("Type1")).val);
      Assert.Equal(((EnumItemSymbol)Enum.Resolve("Type2")).owner, Enum);
      Assert.Equal(Enum.Resolve("Type2").scope, Enum);
      Assert.Equal(2, ((EnumItemSymbol)Enum.Resolve("Type2")).val);
    }
  }
  
  [Fact]
  public void TestSerializeModuleSymbolsTypeEdgeCase()
  {
    var s = new MemoryStream();
    {
      var ts = new Types();
      var m = new Module(ts, "dummy");

      var ns = m.ns;
      ns.Link(ts.ns);

      var Test = new FuncSymbolScript(new Origin(), new FuncSignature(ns.T(Types.Int,Types.Float), Types.String, ns.TRef(Types.Int)), "Test", 1, 155);
      Test.Define(new FuncArgSymbol("s", Types.String));
      Test.Define(new FuncArgSymbol("a", Types.Int, is_ref: true));
      ns.Define(Test);

      CompiledModule.ToStream(m, s, leave_open: true);
    }

    {
      var ts = new Types();

      s.Position = 0;
      var m = CompiledModule.FromStream(ts, s);
      
      //NOTE: right after un-marshalling module must be setup explicitly
      m.Setup(name => null);

      var ns = m.ns;

      Assert.Equal(1 + ts.ns.members.Count, ns.Count());
      Assert.Equal(1, ns.members.Count);

      var Test = (FuncSymbolScript)ns.Resolve("Test");
      AssertEqual(Test.name, "Test");
      Assert.False(Test.attribs.HasFlag(FuncAttrib.Coro));
      Assert.Equal(Test.scope, ns);
      AssertEqual(ns.TFunc(ns.T(Types.Int, Types.Float), Types.String, ns.TRef(Types.Int)).ToString(), Test.signature.ToString());
      Assert.Equal(1, Test.default_args_num);
      Assert.Equal(2, Test.local_vars_num);
      Assert.Equal(155, Test.ip_addr);
      Assert.Equal(0, Test.scope_idx);
      Assert.Equal(2, Test.GetTotalArgsNum());
      AssertEqual("s", Test.GetArg(0).name);
      Assert.False(Test.GetArg(0).is_ref);
      Assert.Equal(Types.String, Test.GetArg(0).type.Get());
      AssertEqual("a", Test.GetArg(1).name);
      Assert.True(Test.GetArg(1).is_ref);
      Assert.Equal(Types.Int, Test.GetArg(1).type.Get());
    }
  }
}

using System;
using System.IO;
using bhl;

public class BHL_Example
{
  public static void Main(string[] args)
  {
    var bhl_symbols = SymbolTable.CreateBuiltins();

    var bytes = File.ReadAllBytes("tmp/bhl.bytes");
    InterpreterRegistry.Load(new MemoryStream(bytes));

    var intp = Interpreter.instance;
    intp.Init(bhl_symbols, InterpreterRegistry.LoadModule);

    var node = intp.GetFuncNode("mage", "MAGE");
    node.run(null);
  }
}

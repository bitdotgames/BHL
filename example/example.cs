using System;
using System.Threading;
using System.IO;
using bhl;

public class Example
{
  public static void Main(string[] args)
  {
    var symbols = SymbolTable.CreateBuiltins();
    var bnd = new MyBindings();
    bnd.Register(symbols);

    var bytes = new MemoryStream(File.ReadAllBytes("tmp/bhl.bytes"));
    var ml = new ModuleLoader(bytes);

    var intp = Interpreter.instance;

    intp.Init(symbols, ml);

    var node = intp.GetFuncNode("unit", "Unit");

    //NOTE: emulating update game loop
    Time.dt = 0.016f;
    while(true)
    {
      node.run();
      Thread.Sleep(16);
    }
  }
}

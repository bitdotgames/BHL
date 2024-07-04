using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime;
using Mono.Options;

namespace bhl
{
    
public class BenchCmd : ICmd
{
  public void Run(string[] args)
  {
    if (args.Length == 0)
      throw new Exception("No arguments");
    
    var file = args[0];
    var src = new MemoryStream(File.ReadAllBytes(file));

    for (int i = 0; i < 5; ++i)
    {
      Console.WriteLine($"=== BHL bench {file} attempt: {i+1} ===");
      var module = new Module(null, "dummy", file);
      
      var sw = Stopwatch.StartNew();
      var preproc = new ANTLR_Preprocessor(
        module, 
        new CompileErrors(),
        null,
        src, 
        new HashSet<string>() {"SERVER"}
      );
      
      var preproced = preproc.Process();
      Console.WriteLine($"BHL preproc file done ({Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2)} sec)");
      
      var lex = new bhlLexer(new AntlrInputStream(preproced));
      var tokens = new CommonTokenStream(lex);
    
      var parser = new bhlParser(tokens);

      sw = Stopwatch.StartNew();
      parser.program();
      Console.WriteLine($"BHL parse file done ({Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2)} sec)");
    }
  }
}

}

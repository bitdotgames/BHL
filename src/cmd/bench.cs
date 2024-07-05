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
  public static void Usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl bench [-n=<iterations count>] [--defines=FOO,BAR]file1 file2 ..fileN");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }
  
  public void Run(string[] args)
  {
    if (args.Length == 0)
      throw new Exception("No arguments");

    int iterations = 1;
    var defines = new HashSet<string>();
    
    var opts = new OptionSet() {
      { "defines=", "comma delimetered defines",
        v =>
        {
          foreach (var d in v.Split(","))
            defines.Add(d);
        } },
      { "n=", "number of bench iterations",
        v =>
        {
          iterations = int.Parse(v);
        } },
     };
    
    var files = new List<string>();
    try
    {
      files = opts.Parse(args);
    }
    catch(OptionException e)
    {
      Usage(e.Message);
    }

    foreach (var file in files)
      BenchFile(file, iterations, defines);
  }

  static void BenchFile(string file, int iterations, HashSet<string> defines)
  {
    Console.WriteLine($"=== BHL bench {file} ===");
     
     for (int i = 0; i < iterations; ++i)
     {
       Console.WriteLine($"== Iteration: {i + 1} ==");
       var module = new Module(null, "dummy", file);

       Stopwatch sw = null;
       
       var src = new MemoryStream(File.ReadAllBytes(file));
       
       sw = Stopwatch.StartNew();
       var preproc_bench_lexer = new bhlPreprocLexer(new ANTLR_Preprocessor.CustomInputStream(src));
       while (preproc_bench_lexer.NextToken().Type != bhlPreprocLexer.Eof) {}
       Console.WriteLine($"Preprocessor Lexer ({sw.ElapsedMilliseconds} ms)");

       src.Position = 0;
    
       sw = Stopwatch.StartNew();
       var preproc = new ANTLR_Preprocessor(
         module,
         new CompileErrors(),
         null,
         src,
         defines
       );
       var preprocd = preproc.Process();
       Console.WriteLine($"Preprocessor ({sw.ElapsedMilliseconds} ms)");

       sw = Stopwatch.StartNew();
       var bench_lexer = new bhlLexer(new AntlrInputStream(preprocd));
       while (bench_lexer.NextToken().Type != bhlLexer.Eof) {}
       Console.WriteLine($"Parser Lexer ({sw.ElapsedMilliseconds} ms)");
       
       preprocd.Position = 0;

       var lex = new bhlLexer(new AntlrInputStream(preprocd));
       var tokens = new CommonTokenStream(lex);
       var parser = new bhlParser(tokens);

       sw = Stopwatch.StartNew();
       parser.program();
       Console.WriteLine($"Parser ({sw.ElapsedMilliseconds} ms)");
     }
  }
}

}

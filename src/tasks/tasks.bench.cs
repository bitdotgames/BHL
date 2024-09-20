using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Mono.Options;

namespace bhl {
    
public static partial class Tasks
{
  static void bench_usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl bench [--profile] [--fast] [-n=<iterations count>] [--defines=FOO,BAR]file1 file2 ..fileN");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }
  
  [Task]
  public static void bench(Taskman tm, string[] args)
  {
    if (args.Length == 0)
      throw new Exception("No arguments");

    int iterations = 1;
    var defines = new HashSet<string>();
    bool profile = false;
    bool fast = false;
    
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
      { "p|profile", "profile parser",
        v => profile = v != null },
      { "fast", "fast parsing strategy",
        v => fast = v != null },
     };
    
    var files = new List<string>();
    try
    {
      files = opts.Parse(args);
    }
    catch(OptionException e)
    {
      bench_usage(e.Message);
    }

    foreach (var file in files)
      BenchFile(file, iterations, defines, profile, fast);
  }

  static void BenchFile(
    string file, 
    int iterations, 
    HashSet<string> defines, 
    bool profile,
    bool fast
    )
  {
    Console.WriteLine($"=== BHL bench {file} ===");
     
     for(int i = 0; i < iterations; ++i)
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
         CompileErrorsHub.MakeEmpty(), 
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

       if(fast)
       {
         parser.Interpreter.PredictionMode = PredictionMode.SLL;
         parser.RemoveErrorListeners();
         parser.ErrorHandler = new BailErrorStrategy();
       }

       if(profile)
         parser.Profile = true;

       sw = Stopwatch.StartNew();
       parser.program();
       Console.WriteLine($"Parser ({sw.ElapsedMilliseconds} ms)");
       
       if(profile)
         DumpParserProfile(parser);
     }
  }

  static void DumpParserProfile(Parser parser)
  {
    Console.WriteLine("== Parser profiler dump");
    foreach(var info in parser.ParseInfo.getDecisionInfo())
    {
      var ds = parser.Atn.GetDecisionState(info.decision);
      var rule = parser.RuleNames[ds.ruleIndex];
      if(info.timeInPrediction > 0)
      {
        Console.WriteLine("RULE " + rule +
                          " timeInPrediction: " + info.timeInPrediction +
                          " invocations: " + info.invocations +
                          " SLL_TL: " + info.SLL_TotalLook +
                          " SLL_ML: " + info.SLL_MaxLook +
                          " ambigs: " + info.ambiguities.Count +
                          " errs: " + info.errors.Count
                          );
      }
    }
  }
}

}

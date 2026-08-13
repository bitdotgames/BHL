using System;
using bhl;

//NOTE: demonstrates IFrontPostProcessor - it patches the typed AST produced by the
//      frontend right before bytecode is emitted for it. Here every call to a specific
//      function is replaced with a constant literal, e.g. useful for stubbing out a
//      feature flag or baking in a build-time constant without touching the .bhl source
public class ReplaceCallWithConstPostProcessor : IFrontPostProcessor
{
  string func_name;
  double const_value;
  int replaced_count;

  public ReplaceCallWithConstPostProcessor(string func_name, double const_value)
  {
    this.func_name = func_name;
    this.const_value = const_value;
  }

  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result result, string src_file)
  {
    ReplaceCalls(result.ast);
    return result;
  }

  void ReplaceCalls(AST_Tree tree)
  {
    if(tree == null)
      return;

    for(int i = 0; i < tree.children.Count; ++i)
    {
      if(tree.children[i] is AST_Call call && call.symbol?.name == func_name)
      {
        var lit = new AST_Literal(ConstType.INT) { nval = const_value };
        tree.children[i] = lit;
        ++replaced_count;
      }
      else if(tree.children[i] is AST_Tree child)
        ReplaceCalls(child);
    }
  }

  public void Tally()
  {
    Console.WriteLine($"[postproc] replaced {replaced_count} call(s) to '{func_name}' with constant literal {const_value}");
  }
}

#if (BHL_FRONT || BHL_PARSER)

using System;
using System.Collections.Generic;

namespace bhl
{

public class ReplSession
{
  readonly VM _vm;
  int _module_idx;
  // Statements accumulated across REPL lines; prepended as preamble so that
  // variables declared earlier (e.g. `var foo = new Foo{}`) remain in scope.
  readonly List<string> _statements = new List<string>();

  static readonly HashSet<string> _decl_keywords =
    new HashSet<string> { "func", "class", "struct", "enum", "interface" };

  public ReplSession(VM vm)
  {
    _vm = vm;
  }

  // Evaluates one REPL line.
  // Declarations (func/class/struct/enum/interface) are compiled and kept loaded.
  // Statements (var, assignments, etc.) execute and are accumulated as context for
  // subsequent lines. Expressions return their value(s).
  public Val[] Eval(string input)
  {
    if(IsDeclaration(input))
    {
      _vm.LoadSource(input, $"__repl_{_module_idx++}__");
      return Array.Empty<Val>();
    }

    // Try as expression with accumulated statements as preamble (variables stay in scope).
    // Fall back to void statement if compilation rejects it.
    try
    {
      return _vm.EvalExpression(input, _statements);
    }
    catch(Exception e) when(e.Message.StartsWith("Eval error:"))
    {
      _vm.EvalStatement(input, _statements);
      _statements.Add(input);
      return Array.Empty<Val>();
    }
  }

  static bool IsDeclaration(string input)
  {
    int i = 0;
    while(i < input.Length && input[i] == ' ') i++;
    int start = i;
    while(i < input.Length && (char.IsLetter(input[i]) || input[i] == '_')) i++;
    if(i == start) return false;
    return _decl_keywords.Contains(input.Substring(start, i - start));
  }
}

}

#endif

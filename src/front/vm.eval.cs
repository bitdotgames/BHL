#if (BHL_FRONT || BHL_PARSER || UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using bhl.dap;

namespace bhl
{

static class EvalSessionBridge
{
#if UNITY_5_3_OR_NEWER
  [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
  static void Register()
#else
  [System.Runtime.CompilerServices.ModuleInitializer]
  internal static void Register()
#endif
  {
    DebugSession.EvalProvider = (vm, exec, frame_idx, expr) => vm.EvalExpression(exec, frame_idx, expr);
  }
}

public partial class VM
{
  public Val[] EvalExpression(string expr, IReadOnlyList<string> preamble = null) =>
    EvalCore(new List<(string, string, Val)>(), expr, preamble: preamble);

  public Val[] EvalStatement(string stmt, IReadOnlyList<string> preamble = null) =>
    EvalCore(new List<(string, string, Val)>(), stmt, forced_ret_type: "void", preamble: preamble);

  public Val[] EvalExpression(ExecState exec, int frame_idx, string expr)
  {
    if(frame_idx < 0 || frame_idx >= exec.frames_count)
      throw new Exception($"Frame index {frame_idx} out of range");

    ref var frame = ref exec.frames[frame_idx];
    var table = frame.module?.decl.compiled.local_var_table;

    var locals = new List<(string name, string type_str, Val val)>();
    for(int i = 0; i < frame.locals_vars_num; ++i)
    {
      var name = table?.TryGet(frame.start_ip, i);
      if(name == null) continue;

      var val = frame.locals.vals[frame.locals_offset + i];
      var type_str = val.type != null
        ? val.type.GetFullTypePath().ToString()
        : "any";

      locals.Add((name, type_str, val));
    }

    return EvalCore(locals, expr);
  }

  Val[] EvalCore(List<(string name, string type_str, Val val)> locals, string expr, string forced_ret_type = null, IReadOnlyList<string> preamble = null)
  {
    var ret_type_str = forced_ret_type ?? TryGetKnownReturnTypeStr(expr) ?? "any";
    var src = BuildEvalSource(locals, expr, ret_type_str, preamble);
    var decl = CompileSource(src, "__eval__");

    var module = new Module(decl);
    LoadModule(module);

    // Disable the debugger while executing the eval function so that breakpoints
    // don't re-fire inside __eval__ (IPs are matched globally without module context)
    var saved_debugger = debugger;
    debugger = null;

    try
    {
      var fs = decl.ns.ResolveSymbolByPath("__eval__") as FuncSymbolScript;
      if(fs == null)
        throw new Exception("Failed to resolve eval function");

      var args = new StackList<Val>();
      foreach(var (_, _, v) in locals)
      {
        // The eval frame will call ReleaseLocals() on these, so we must Retain
        // them here to avoid releasing refs still owned by the paused fiber's frame.
        v._refc?.Retain();
        args.Add(v);
      }

      var result_stack = Execute(fs, args);

      if(ret_type_str == "void")
        return System.Array.Empty<Val>();

      var result = new Val[result_stack.sp];
      for(int i = 0; i < result_stack.sp; ++i)
        result[i] = result_stack.vals[i];
      return result;
    }
    finally
    {
      debugger = saved_debugger;
      UnloadModule(decl.name);
    }
  }

  // Returns "void", the tuple type name (e.g. "int,string"), or null (= use "any").
  // Only fires for direct function calls whose name is a simple identifier.
  string TryGetKnownReturnTypeStr(string expr)
  {
    int i = 0;
    while(i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_'))
      i++;
    if(i == 0) return null;

    int j = i;
    while(j < expr.Length && expr[j] == ' ') j++;
    if(j >= expr.Length || expr[j] != '(') return null;

    var name = expr.Substring(0, i);
    var sym = ResolveNamedByPath(name) as FuncSymbol;
    if(sym == null) return null;

    var ret = sym.GetReturnType();
    if(ret == Types.Void) return "void";
    if(ret is TupleType)  return ret.GetName();
    return null;
  }

  static string BuildEvalSource(List<(string name, string type_str, Val val)> locals, string expr, string ret_type_str, IReadOnlyList<string> preamble = null)
  {
    var sb = new StringBuilder();
    sb.Append("func ");
    sb.Append(ret_type_str);
    sb.Append(" __eval__(");
    for(int i = 0; i < locals.Count; ++i)
    {
      if(i > 0) sb.Append(", ");
      sb.Append(locals[i].type_str);
      sb.Append(' ');
      sb.Append(locals[i].name);
    }
    sb.Append(") { ");
    if(preamble != null)
      foreach(var stmt in preamble)
      {
        sb.Append(stmt);
        sb.Append("; ");
      }
    if(ret_type_str != "void") sb.Append("return ");
    sb.Append(expr);
    sb.Append("; }");
    return sb.ToString();
  }

  public void LoadSource(string src, string module_name)
  {
    var decl = CompileSource(src, module_name);
    LoadModule(new Module(decl));
  }

  ModuleDeclared CompileSource(string src, string module_name)
  {
    var err_hub = CompileErrorsHub.MakeEmpty();
    var decl = new ModuleDeclared(module_name, "");

    using var src_stream = new MemoryStream(Encoding.UTF8.GetBytes(src));

    var processor = ANTLR_Processor.ParseAndMakeProcessor(
      decl,
      src_stream,
      types,
      err_hub
    );

    if(err_hub.errors.Count > 0)
      throw new Exception($"Eval error: {err_hub.errors[0].text}");

    // Make all currently-loaded module symbols visible to the eval compiler.
    // types.ns is already linked via InitScope; TryLink skips duplicates.
    foreach(var kv in modules)
      decl.ns.Link(kv.Key.ns);

    var bundle = new ProjectCompilationStateBundle(types);
    bundle.file2proc["__eval__"] = processor;
    bundle.file2cached = null;
    ANTLR_Processor.ProcessAll(bundle);

    if(err_hub.errors.Count > 0)
      throw new Exception($"Eval error: {err_hub.errors[0].text}");

    // Inject a synthetic import list so the compiler can emit correct module indices
    // for calls to functions defined in currently-loaded modules.
    var import_ast = new AST_Import();
    foreach(var kv in modules)
      if(types.IsImported(kv.Key))
        import_ast.module_names.Add(kv.Key.name);
    if(import_ast.module_names.Count > 0)
      processor.result.ast.children.Insert(0, import_ast);

    return new ModuleCompiler(processor.result).Compile();
  }
}
}

#endif

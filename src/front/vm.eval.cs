#if BHL_FRONT

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
  public Val EvalExpression(ExecState exec, int frame_idx, string expr)
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

    var src = BuildEvalSource(locals, expr);
    var decl = CompileEvalSource(src);

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
        args.Add(v);

      var result_stack = Execute(fs, args);

      return result_stack.sp > 0 ? result_stack.vals[result_stack.sp - 1] : default;
    }
    finally
    {
      debugger = saved_debugger;
      UnloadModule(decl.name);
    }
  }

  static string BuildEvalSource(List<(string name, string type_str, Val val)> locals, string expr)
  {
    var sb = new StringBuilder();
    sb.Append("func any __eval__(");
    for(int i = 0; i < locals.Count; ++i)
    {
      if(i > 0) sb.Append(", ");
      sb.Append(locals[i].type_str);
      sb.Append(' ');
      sb.Append(locals[i].name);
    }
    sb.Append(") { return ");
    sb.Append(expr);
    sb.Append("; }");
    return sb.ToString();
  }

  ModuleDeclared CompileEvalSource(string src)
  {
    var err_hub = CompileErrorsHub.MakeEmpty();
    var decl = new ModuleDeclared("__eval__", "");

    using var src_stream = new MemoryStream(Encoding.UTF8.GetBytes(src));



    var processor = ANTLR_Processor.ParseAndMakeProcessor(
      decl,
      null,
      src_stream,
      types,
      err_hub,
      null,
      out _
    );

    if(err_hub.errors.Count > 0)
      throw new Exception($"Eval error: {err_hub.errors[0].text}");

    processor.Phase_Outline();
    processor.Phase_ParseTypes1();
    processor.Phase_ParseTypes2();
    processor.Phase_ParseFuncBodies();
    processor.Phase_SetResult();
    // Phase_SetResult marks the module as ready; reset it so LoadModule.Setup() runs SetupFuncSymbol
    decl.is_ready = false;

    if(err_hub.errors.Count > 0)
      throw new Exception($"Eval error: {err_hub.errors[0].text}");

    var compiler = new ModuleCompiler(processor.result);
    compiler.Compile_VisitAST();
    compiler.Compile_PatchInstructions();
    var compiled_decl = compiler.Compile_Finish();
    compiled_decl.AssignId();

    return compiled_decl;
  }
}
}

#endif

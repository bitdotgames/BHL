using System.Collections.Generic;

namespace bhl
{

//NOTE: counts how often each run of adjacent opcodes actually executes, which is what picking
//      superinstructions needs - a static count over the sources says how often a run appears,
//      not how often it runs, and a loop body appears once and runs a million times.
//
//      The collector is always compiled so it can be tested, but it's only called from the
//      dispatch loop under BHL_OPCODE_PROFILE: a runtime check there is not free.
public static class OpcodeProfiler
{
  //NOTE: nothing can be fused across a control transfer, so one ends the current run. The
  //      terminator itself is still part of it - 'cmp, JumpZ' is exactly the shape we want
  static readonly bool[] _terminates = BuildTerminators();

  static readonly Dictionary<long, long> _runs = new Dictionary<long, long>();

  const int MAX_RUN = 4;
  static readonly int[] _window = new int[MAX_RUN];
  static int _len;

  static bool[] BuildTerminators()
  {
    var res = new bool[(int)Opcodes.MAX];
    foreach(var op in new Opcodes[] {
      Opcodes.Jump, Opcodes.JumpZ, Opcodes.JumpPeekZ, Opcodes.JumpPeekNZ,
      Opcodes.Return, Opcodes.DefArg,
      Opcodes.CallLocal, Opcodes.CallGlobNative, Opcodes.Call, Opcodes.CallNative,
      Opcodes.CallMethod, Opcodes.CallMethodNative, Opcodes.CallMethodIface,
      Opcodes.CallMethodIfaceNative, Opcodes.CallMethodVirt, Opcodes.CallFuncPtr,
      Opcodes.CallFuncPtrInv, Opcodes.CallVarMethodNative, Opcodes.CallGVarMethodNative,
      Opcodes.Scope, Opcodes.Defer, Opcodes.Paral, Opcodes.ParalAll,
      Opcodes.LocalConstCmpJump, Opcodes.ConstReturn
    })
      res[(int)op] = true;
    return res;
  }

  public static void Record(Opcodes op)
  {
    if(_len == MAX_RUN)
    {
      for(int i = 1; i < MAX_RUN; ++i)
        _window[i-1] = _window[i];
      _len = MAX_RUN - 1;
    }
    _window[_len++] = (int)op;

    //NOTE: length in the high word, opcodes in order from the low byte up
    for(int len = 2; len <= _len; ++len)
    {
      long key = (long)len << 32;
      for(int i = 0; i < len; ++i)
        key |= (long)(uint)_window[_len - len + i] << (8 * i);

      _runs.TryGetValue(key, out long n);
      _runs[key] = n + 1;
    }

    int idx = (int)op;
    if(idx < _terminates.Length && _terminates[idx])
      _len = 0;
  }

  //NOTE: execution can also move without dispatching an opcode - a coroutine resuming, a
  //      region being left - so start a fresh run rather than bridging the gap
  public static void Reset()
  {
    _len = 0;
  }

  public static void Clear()
  {
    _runs.Clear();
    _len = 0;
  }

  public static List<(string run, int len, long count)> Top(int n = 40)
  {
    var res = new List<(string, int, long)>();
    foreach(var kv in _runs)
    {
      int len = (int)(kv.Key >> 32);

      var names = new string[len];
      for(int i = 0; i < len; ++i)
        names[i] = ((Opcodes)((kv.Key >> (8 * i)) & 0xFF)).ToString();

      res.Add((string.Join(",", names), len, kv.Value));
    }

    res.Sort((a, b) => b.Item3.CompareTo(a.Item3));
    if(res.Count > n)
      res.RemoveRange(n, res.Count - n);
    return res;
  }

  public static string Report(int n = 40)
  {
    var sb = new System.Text.StringBuilder();
    sb.Append("executed opcode runs, most frequent first:\n");
    foreach(var e in Top(n))
      sb.Append($"{e.count,12}  [{e.len}] {e.run}\n");
    return sb.ToString();
  }
}

}

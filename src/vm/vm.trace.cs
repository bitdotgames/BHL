using System;
using System.Collections.Generic;

namespace bhl
{

public partial class VM
{
  public struct TraceItem
  {
    public string file;
    public string func;
    public int line;
    public int ip;
  }

  public class Error : Exception
  {
    public List<TraceItem> trace;

    public Error(List<TraceItem> trace, Exception e)
      : base(ToString(trace), e)
    {
      this.trace = trace;
    }

    public enum TraceFormat
    {
      Verbose = 1,
      Compact = 2
    }

    static public string ToString(List<TraceItem> trace, TraceFormat format = TraceFormat.Compact)
    {
      string s = "\n";
      foreach(var t in trace)
      {
        switch(format)
        {
          case TraceFormat.Compact:
            s += "at " + t.func + "(..) in " + t.file + ":" + t.line + "\n";
            break;
          case TraceFormat.Verbose:
          default:
            s += "at " + t.func + "(..) +" + t.ip + " in " + t.file + ":" + t.line + "\n";
            break;
        }
      }

      return s;
    }
  }

}

}

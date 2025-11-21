using Antlr4.Runtime.Tree;

namespace bhl;

public partial class ANTLR_Processor
{
  private class VarsOrDeclsProxy
  {
    bhlParser.VarDeclareContext[] vdecls;
    bhlParser.VarOrDeclareContext[] vodecls;
    bhlParser.VarDeclareOrChainExpContext[] vdeclsorexps;
    bhlParser.ChainExpContext[] exps;

    public int Count
    {
      get
      {
        if(vdecls != null)
          return vdecls.Length;
        else if(vodecls != null)
          return vodecls.Length;
        else if(vdeclsorexps != null)
          return vdeclsorexps.Length;
        else if(exps != null)
          return exps.Length;
        return -1;
      }
    }

    public VarsOrDeclsProxy(bhlParser.VarDeclareContext[] vdecls)
    {
      this.vdecls = vdecls;
    }

    public VarsOrDeclsProxy(bhlParser.VarOrDeclareContext[] vodecls)
    {
      this.vodecls = vodecls;
    }

    public VarsOrDeclsProxy(bhlParser.VarDeclareOrChainExpContext[] vdeclsorexps)
    {
      this.vdeclsorexps = vdeclsorexps;
    }

    public VarsOrDeclsProxy(bhlParser.ChainExpContext[] exps)
    {
      this.exps = exps;
    }

    public IParseTree At(int i)
    {
      if(vdecls != null)
        return (IParseTree)vdecls[i];
      else if(vodecls != null)
        return (IParseTree)vodecls[i];
      else if(vdeclsorexps != null)
        return (IParseTree)vdeclsorexps[i];
      else if(exps != null)
        return (IParseTree)exps[i];

      return null;
    }

    public bhlParser.TypeContext TypeAt(int i)
    {
      if(vdecls != null)
        return vdecls[i].type();
      else if(vodecls != null)
        return vodecls[i].varDeclare()?.type();
      else if(vdeclsorexps != null)
        return vdeclsorexps[i].varDeclare()?.type();

      return null;
    }

    public ITerminalNode LocalNameAt(int i)
    {
      if(vdecls != null)
        return vdecls[i].NAME();
      else if(vodecls != null)
      {
        if(vodecls[i].varDeclare() != null)
          return vodecls[i].varDeclare().NAME();
        else
          return vodecls[i].NAME();
      }
      else if(vdeclsorexps != null)
      {
        if(vdeclsorexps[i].varDeclare() != null)
          return vdeclsorexps[i].varDeclare().NAME();
        else if(vdeclsorexps[i].chainExp().name() != null &&
                vdeclsorexps[i].chainExp().chainExpItem().Length == 0)
          return vdeclsorexps[i].chainExp().name().NAME();
      }
      else if(exps != null)
      {
        if(exps[i].name() != null &&
           exps[i].chainExpItem().Length == 0)
          return exps[i].name().NAME();
      }

      return null;
    }

    public bhlParser.ChainExpContext VarAccessAt(int i)
    {
      if(vdeclsorexps != null && vdeclsorexps[i].chainExp() != null)
      {
        var chain = new ExpChain(null, vdeclsorexps[i].chainExp());
        if(chain.IsMemorySlotAccess)
          return vdeclsorexps[i].chainExp();
      }
      else if(exps != null)
      {
        var chain = new ExpChain(null, exps[i]);
        if(chain.IsMemorySlotAccess)
          return exps[i];
      }

      return null;
    }
  }

}

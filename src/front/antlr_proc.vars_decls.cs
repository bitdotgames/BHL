#if (BHL_FRONT || BHL_PARSER)

using Antlr4.Runtime.Tree;


namespace bhl
{

public partial class ANTLR_Processor
{
  private abstract class VarsOrDeclsProxy
  {
    public abstract int Count { get; }
    public abstract IParseTree At(int i);
    public abstract bhlParser.TypeContext TypeAt(int i);
    public abstract ITerminalNode LocalNameAt(int i);
    public abstract bhlParser.ChainExpContext VarAccessAt(int i);

    public static VarsOrDeclsProxy From(bhlParser.VarDeclareContext[] items) =>
      new VarDeclareProxy(items);
    public static VarsOrDeclsProxy From(bhlParser.VarOrDeclareContext[] items) =>
      new VarOrDeclareProxy(items);
    public static VarsOrDeclsProxy From(bhlParser.VarOrDeclareContext item) =>
      new VarOrDeclareProxy(new[] { item });
    public static VarsOrDeclsProxy From(bhlParser.VarDeclareOrChainExpContext[] items) =>
      new VarDeclareOrChainExpProxy(items);
    public static VarsOrDeclsProxy From(bhlParser.ChainExpContext item) =>
      new ChainExpProxy(new[] { item });
  }

  private sealed class VarDeclareProxy : VarsOrDeclsProxy
  {
    readonly bhlParser.VarDeclareContext[] items;
    public VarDeclareProxy(bhlParser.VarDeclareContext[] items) => this.items = items;

    public override int Count => items.Length;
    public override IParseTree At(int i) => items[i];
    public override bhlParser.TypeContext TypeAt(int i) => items[i].type();
    public override ITerminalNode LocalNameAt(int i) => items[i].NAME();
    public override bhlParser.ChainExpContext VarAccessAt(int i) => null;
  }

  private sealed class VarOrDeclareProxy : VarsOrDeclsProxy
  {
    readonly bhlParser.VarOrDeclareContext[] items;
    public VarOrDeclareProxy(bhlParser.VarOrDeclareContext[] items) => this.items = items;

    public override int Count => items.Length;
    public override IParseTree At(int i) => items[i];
    public override bhlParser.TypeContext TypeAt(int i) => items[i].varDeclare()?.type();
    public override ITerminalNode LocalNameAt(int i)
    {
      var vd = items[i].varDeclare();
      return vd != null ? vd.NAME() : items[i].NAME();
    }
    public override bhlParser.ChainExpContext VarAccessAt(int i) => null;
  }

  private sealed class VarDeclareOrChainExpProxy : VarsOrDeclsProxy
  {
    readonly bhlParser.VarDeclareOrChainExpContext[] items;
    public VarDeclareOrChainExpProxy(bhlParser.VarDeclareOrChainExpContext[] items) => this.items = items;

    public override int Count => items.Length;
    public override IParseTree At(int i) => items[i];
    public override bhlParser.TypeContext TypeAt(int i) => items[i].varDeclare()?.type();
    public override ITerminalNode LocalNameAt(int i)
    {
      var vd = items[i].varDeclare();
      if(vd != null)
        return vd.NAME();
      var chain = items[i].chainExp();
      if(chain.name() != null && chain.chainExpItem().Length == 0)
        return chain.name().NAME();
      return null;
    }
    public override bhlParser.ChainExpContext VarAccessAt(int i)
    {
      var chain_exp = items[i].chainExp();
      if(chain_exp != null && new ExpChain(null, chain_exp).IsMemorySlotAccess)
        return chain_exp;
      return null;
    }
  }

  private sealed class ChainExpProxy : VarsOrDeclsProxy
  {
    readonly bhlParser.ChainExpContext[] items;
    public ChainExpProxy(bhlParser.ChainExpContext[] items) => this.items = items;

    public override int Count => items.Length;
    public override IParseTree At(int i) => items[i];
    public override bhlParser.TypeContext TypeAt(int i) => null;
    public override ITerminalNode LocalNameAt(int i)
    {
      if(items[i].name() != null && items[i].chainExpItem().Length == 0)
        return items[i].name().NAME();
      return null;
    }
    public override bhlParser.ChainExpContext VarAccessAt(int i)
    {
      if(new ExpChain(null, items[i]).IsMemorySlotAccess)
        return items[i];
      return null;
    }
  }
}
}

#endif

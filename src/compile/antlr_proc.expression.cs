using System.Collections.Generic;
using Antlr4.Runtime;

namespace bhl;

public partial class ANTLR_Processor
{
  public struct ExpChain
  {
    public ParserRuleContext ctx;
    public bhlParser.NameContext name_ctx;
    public bhlParser.ExpContext paren_exp_ctx;
    public bhlParser.FuncLambdaContext lmb_ctx;
    public ExpChainItems items;

    public bool Incomplete { get ; private set; }

    public bool IsGlobalNs
    {
      get { return name_ctx?.GLOBAL() != null; }
    }

    public bool IsFuncCall
    {
      get
      {
        return items.Count > 0 &&
               items.At(items.Count - 1) is bhlParser.CallArgsContext;
      }
    }

    public bool IsSimpleVarAccess
    {
      get
      {
        return items.Count == 0 && name_ctx != null;
      }
    }

    public bool IsMemorySlotAccess
    {
      get
      {
        return
          (items.Count == 0 && name_ctx != null) ||
          (items.Count > 0 &&
           (items.At(items.Count - 1) is bhlParser.MemberAccessContext ||
            items.At(items.Count - 1) is bhlParser.ArrAccessContext));
      }
    }

    public ExpChain(ParserRuleContext ctx, bhlParser.ChainExpContext chain)
    {
      this.ctx = ctx;
      this.name_ctx = null;
      this.paren_exp_ctx = null;
      this.lmb_ctx = null;
      items = new ExpChainItems();

      Incomplete = false;

      Init(ctx, chain);
    }

    void Init(ParserRuleContext ctx, bhlParser.ChainExpContext chain)
    {
      this.ctx = ctx;

      if(chain.name() != null)
        name_ctx = chain.name();
      //paren chain
      else if(chain.exp() != null)
        paren_exp_ctx = chain.exp();
      else if(chain.funcLambda() != null)
        lmb_ctx = chain.funcLambda();
      items = new ExpChainItems(chain.chainExpItem());
    }
  }

  public struct ExpChainItems
  {
    bhlParser.ChainExpItemContext[] items_arr;
    List<ParserRuleContext> items_lst;

    public int Count
    {
      get
      {
        if(items_lst != null)
          return items_lst.Count;
        return items_arr == null ? 0 : items_arr.Length;
      }
    }

    public ExpChainItems(bhlParser.ChainExpItemContext[] items)
    {
      items_arr = items;
      items_lst = null;
    }

    public ParserRuleContext At(int i)
    {
      if(items_lst != null)
        return items_lst[i];

      return _Get(items_arr[i]);
    }

    void _Add(ParserRuleContext ctx)
    {
      //let's make a copy
      //TODO: a hybrid approach can be used instead
      if(items_lst == null)
      {
        items_lst = new List<ParserRuleContext>();
        if(items_arr != null)
        {
          foreach(var item in items_arr)
            items_lst.Add(_Get(item));
        }
      }

      items_lst.Add(ctx);
    }

    static ParserRuleContext _Get(bhlParser.ChainExpItemContext item)
    {
      if(item.callArgs() != null)
        return item.callArgs();
      else if(item.memberAccess() != null)
        return item.memberAccess();
      else
        return item.arrAccess();
    }

    public void Add(bhlParser.ChainExpItemContext item)
    {
      _Add(_Get(item));
    }

    public void Add(bhlParser.ChainExpItemContext[] items)
    {
      foreach(var item in items)
        Add(item);
    }

    public void Add(bhlParser.MemberAccessContext macc)
    {
      _Add(macc);
    }

    public void Add(bhlParser.CallArgsContext cargs)
    {
      _Add(cargs);
    }

    public void Add(bhlParser.ArrAccessContext acc)
    {
      _Add(acc);
    }
  }

}

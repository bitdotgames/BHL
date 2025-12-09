using System.Collections.Generic;

namespace bhl;

public interface IFrontPostProcessor
{
  //NOTE: returns patched result
  ANTLR_Processor.Result Patch(ANTLR_Processor.Result fres, string src_file);
  void Tally();
}

public class EmptyPostProcessor : IFrontPostProcessor
{
  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result fres, string src_file)
  {
    return fres;
  }

  public void Tally()
  {
  }
}

public class CombinedPostProcessor : IFrontPostProcessor
{
  //List instead of Enumerable to preserve the specified order
  readonly IList<IFrontPostProcessor> _postprocessors;

  public CombinedPostProcessor(IList<IFrontPostProcessor> postprocessors)
  {
    _postprocessors = postprocessors;
  }

  public ANTLR_Processor.Result Patch(ANTLR_Processor.Result fres, string src_file)
  {
    for(int i = 0; i < _postprocessors.Count; i++)
      fres = _postprocessors[i].Patch(fres, src_file);

    return fres;
  }

  public void Tally()
  {
    for(int i = 0; i < _postprocessors.Count; i++)
      _postprocessors[i].Tally();
  }
}

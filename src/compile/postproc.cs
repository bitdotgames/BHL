namespace bhl
{

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

}
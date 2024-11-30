using BenchmarkDotNet.Running;

public class BenchProgram
{
  public static void Main(string[] args)
  {
    BenchmarkSwitcher.FromAssembly(typeof(BenchProgram).Assembly).Run(args);
  }
}

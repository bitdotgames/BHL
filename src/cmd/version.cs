using System;

namespace bhl {

public class VersionCmd : ICmd
{
  public void Run(string[] args)
  {
    Console.WriteLine(Version.Name);
  }
}

}

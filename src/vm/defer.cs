using System;

namespace bhl;

public struct DeferBlock
{
  public int ip;
  public int max_ip;

  internal void Execute(VM.ExecState exec)
  {
    //1. let's remeber the original ip in order to restore it once
    //   the execution of this block is done (defer block can be
    //   located anywhere in the code)
    int ip_orig = exec.ip;
    exec.ip = this.ip;

    //2. let's create the execution region
    exec.PushRegion(exec.frames_count - 1, min_ip: ip, max_ip: max_ip);
    //3. and execute it
    exec.Execute(
      //NOTE: we re-use the existing exec.stack but limit the execution
      //      only up to the defer code block
      exec.regions_count - 1
    );
    if(exec.status != BHS.SUCCESS)
      throw new Exception("Defer execution invalid status: " + exec.status);

    exec.ip = ip_orig;
  }

  public override string ToString()
  {
    return "Defer block: " + ip + " " + max_ip;
  }
}
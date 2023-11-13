using System;
using System.Collections.Generic;

namespace bhl {

public interface ITask
{
  //NOTE: returns true if running
  bool Tick();
  void Stop();
}

public class TaskManager
{
  readonly List<ITask> _tasks = new List<ITask>();

  public bool IsBusy
  {
    get { return _tasks.Count > 0; }
  }

  //NOTE: if true it means it executes only one task per Tick,
  //      not in 'pseudo parallel' manner
  public bool SequentialExecution;

  public TaskManager(bool sequentialExecution = false)
  {
    this.SequentialExecution = sequentialExecution;
  }

  public void Tick()
  {
    try
    {
      for(int i = 0; i < _tasks.Count;)
      {
        var t = _tasks[i];
        bool is_done = !t.Tick();
        if(is_done)
        {
          RemoveAt(i);
          continue;
        }

        if(SequentialExecution)
          break;

        ++i;
      }
    }
    catch(Exception)
    {
      Stop();
      throw;
    }
  }

  void RemoveAt(int i)
  {
    //protecting against situation when
    //someone has called Stop() already
    if(_tasks.Count > 0)
      _tasks.RemoveAt(i);
  }

  public void Stop()
  {
    for(int i = _tasks.Count; i-- > 0;)
    {
      var t = _tasks[i];
      t.Stop();
    }

    _tasks.Clear();
  }

  public void Add(ITask t)
  {
    _tasks.Add(t);
  }
}

} //namespace bhl

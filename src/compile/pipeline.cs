using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace bhl {
public interface IPipelineStageBase
{
  public string Name { get; }
}
public interface IPipelineStage<TIn, TOut> :  IPipelineStageBase
{ 
  Task<TOut> Run(TIn input, CancellationToken token);
}

public class ParallelStage<TIn, TOut> : IPipelineStage<IEnumerable<TIn>, List<TOut>>
{
  private readonly Func<TIn, CancellationToken, Task<TOut>> _worker;
  private readonly IProgress<(int done, int total)> _progress;

  private readonly string _name;
  public string Name => _name;

  public ParallelStage(
      string name,
      Func<TIn, CancellationToken, Task<TOut>> worker,
      IProgress<(int, int)> progress = null)
  {
    _name = name;
    _worker = worker;
    _progress = progress;
  }

  public async Task<List<TOut>> Run(IEnumerable<TIn> inputs, CancellationToken token)
  {
    var input_list = inputs.ToList();
    int total = input_list.Count;
    int completed = 0;

    var tasks = input_list.Select(async item =>
    {
      var result = await _worker(item, token);
      _progress?.Report((Interlocked.Increment(ref completed), total));
      return result;
    });

    return (await Task.WhenAll(tasks)).ToList();
  }
}

public class TransformStage<TIn, TOut> : IPipelineStage<TIn, TOut>
{
  private readonly string _name;
  public string Name => _name;
  
  private readonly Func<TIn, TOut> _action;

  public TransformStage(string name, Func<TIn, TOut> action)
  {
    _name = name;
    _action = action;
  }

  public Task<TOut> Run(TIn input, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    return Task.FromResult(_action(input));
  }
}

public class TransformAsyncStage<TIn, TOut> : IPipelineStage<TIn, TOut>
{
  private readonly string _name;
  public string Name => _name;
  
  private readonly Func<TIn, CancellationToken, Task<TOut>> _action;

  public TransformAsyncStage(string name, Func<TIn, CancellationToken, Task<TOut>> action)
  {
    _name = name;
    _action = action;
  }

  public Task<TOut> Run(TIn input, CancellationToken token) => _action(input, token);
}

public class Pipeline<TIn, TOut>
{
  private readonly List<IPipelineStageBase> _stages = new();
  
  private readonly Logger _logger;

  public Pipeline(Logger logger)
  {
    _logger = logger;
  }
  
  public Pipeline<TIn, TOut> Parallel<TPIn, TPOut>(
    string name,
    Func<TPIn, CancellationToken, Task<TPOut>> worker
  )
  {
    _stages.Add(new ParallelStage<TPIn, TPOut>(name, worker));
    return this;
  }
  
  public Pipeline<TIn, TOut> Transform<TPIn, TPOut>(
    string name,
    Func<TPIn, TPOut> worker
    )
  {
    _stages.Add(new TransformStage<TPIn, TPOut>(name, worker));
    return this;
  }
  
  public async Task<TOut> RunAsync(TIn input, CancellationToken token)
  {
    object current = input;

    foreach (var stage in _stages)
    {
      var sw = Stopwatch.StartNew();
      dynamic _stage = stage;
      current = await _stage.Run((dynamic)current, token);
      sw.Stop();
      _logger.Log(1, $"{stage.Name} done({Math.Round(sw.ElapsedMilliseconds/1000.0f,2)} sec)");
    }

    return (TOut)current;
  }
}
}

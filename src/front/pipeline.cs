#if (BHL_FRONT || BHL_PARSER || UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace bhl
{

public interface IPipelineStageBase
{
  public string Title { get; }
}

public interface IPipelineStage<TIn, TOut> :  IPipelineStageBase
{
  Task<TOut> Run(TIn input, CancellationToken token);
}

public class ParallelStage<TIn, TOut> : IPipelineStage<IEnumerable<TIn>, List<TOut>>
{
  private readonly Func<TIn, CancellationToken, Task<TOut>> _worker;
  private readonly IProgress<(int done, int total)> _progress;

  private readonly string _title;
  private int _total_workers;
  public string Title => FormatTitle(_title);

  public ParallelStage(
    string title,
    Func<TIn, CancellationToken, Task<TOut>> worker,
    IProgress<(int, int)> progress = null)
  {
    _title = title;
    _worker = worker;
    _progress = progress;
  }

  string FormatTitle(string title)
  {
    return title.Replace("%workers%", _total_workers.ToString());
  }

  public async Task<List<TOut>> Run(IEnumerable<TIn> inputs, CancellationToken token)
  {
    var input_list = inputs.ToList();
    _total_workers = input_list.Count;
    int completed = 0;

    var tasks = input_list.Select(async item =>
    {
      var result = await _worker(item, token);
      _progress?.Report((Interlocked.Increment(ref completed), _total_workers));
      return result;
    });

    return (await Task.WhenAll(tasks)).ToList();
  }
}

public class TransformStage<TIn, TOut> : IPipelineStage<TIn, TOut>
{
  private readonly string _title;
  public string Title => _title;

  private readonly Func<TIn, TOut> _action;

  public TransformStage(string title, Func<TIn, TOut> action)
  {
    _title = title;
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
  private readonly string _title;
  public string Title => _title;

  private readonly Func<TIn, CancellationToken, Task<TOut>> _action;

  public TransformAsyncStage(string title, Func<TIn, CancellationToken, Task<TOut>> action)
  {
    _title = title;
    _action = action;
  }

  public Task<TOut> Run(TIn input, CancellationToken token) => _action(input, token);
}

public class Pipeline<TIn, TOut>
{
  private readonly List<(Func<string> title, Func<object, CancellationToken, Task<object>> run)> _stages = new();

  private readonly Logger _logger;

  public Pipeline(Logger logger)
  {
    _logger = logger;
  }

  public Pipeline<TIn, TOut> Parallel<TPIn, TPOut>(
    string title,
    Func<TPIn, CancellationToken, Task<TPOut>> worker
  )
  {
    var stage = new ParallelStage<TPIn, TPOut>(title, worker);
    _stages.Add((() => stage.Title, async (input, token) => (object)await stage.Run((IEnumerable<TPIn>)input, token)));
    return this;
  }

  public Pipeline<TIn, TOut> Transform<TPIn, TPOut>(
    string name,
    Func<TPIn, TPOut> worker
  )
  {
    var stage = new TransformStage<TPIn, TPOut>(name, worker);
    _stages.Add((() => stage.Title, (input, token) =>
    {
      token.ThrowIfCancellationRequested();
      return Task.FromResult<object>(worker((TPIn)input));
    }));
    return this;
  }

  public async Task<TOut> RunAsync(TIn input, CancellationToken token)
  {
    object current = input;

    foreach(var (getTitle, run) in _stages)
    {
      var sw = Stopwatch.StartNew();
      current = await run(current, token);
      sw.Stop();
      _logger.Log(1, $"{getTitle()} ({Math.Round(sw.ElapsedMilliseconds / 1000.0f, 2)} sec)");
    }

    return (TOut)current;
  }
}
}

#endif

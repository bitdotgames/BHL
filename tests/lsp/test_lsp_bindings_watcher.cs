using System;
using System.IO;
using System.Threading.Tasks;
using bhl;
using bhl.lsp;
using Xunit;

// Regression coverage for Workspace's bindings-DLL FileSystemWatcher (lsp/workspace.cs).
// A prior bug (EnableRaisingEvents set before handlers were subscribed) could silently end
// up not watching for anything meaningful on Linux's inotify-backed implementation, while
// still appearing to work on macOS/Windows. These tests exercise the real OS file watcher —
// CI runs this suite on ubuntu-latest and windows-latest, which is exactly where that class
// of bug would (and wouldn't) show up.
public class TestLSPBindingsWatcher : IDisposable
{
  string dir;
  string dll_path;
  Workspace workspace;

  public TestLSPBindingsWatcher()
  {
    dir = Path.Combine(Path.GetTempPath(), "bhlsp_bindings_watcher_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    dll_path = Path.Combine(dir, "bindings.dll");
    File.WriteAllText(dll_path, "v1");

    workspace = new Workspace();
  }

  public void Dispose()
  {
    workspace.Shutdown();
    try { Directory.Delete(dir, true); } catch { }
  }

  static Task<bool> WaitForChangeAsync(Workspace ws, TimeSpan timeout)
  {
    var tcs = new TaskCompletionSource<bool>();
    Action handler = null;
    handler = () =>
    {
      ws.BindingsDllChanged -= handler;
      tcs.TrySetResult(true);
    };
    ws.BindingsDllChanged += handler;

    var cts = new System.Threading.CancellationTokenSource(timeout);
    cts.Token.Register(() => tcs.TrySetResult(false));

    return tcs.Task;
  }

  // Some sandboxed/containerized shells run without access to the OS's file-change
  // notification service (e.g. no Mach IPC to fseventsd on macOS), so a plain
  // FileSystemWatcher never fires - through no fault of Workspace's watcher setup.
  // Probe for that with a throwaway file before trusting a negative result below,
  // so environments that genuinely can't deliver notifications skip instead of
  // falsely failing, while a real regression still fails wherever notifications work
  // (in particular the ubuntu-latest/windows-latest CI legs this suite targets).
  async Task<bool> EnvironmentSupportsFileWatchingAsync()
  {
    var probe_path = Path.Combine(dir, "__canary__");
    File.WriteAllText(probe_path, "0");

    using var watcher = new FileSystemWatcher(dir, Path.GetFileName(probe_path))
    {
      NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
    };
    var tcs = new TaskCompletionSource<bool>();
    FileSystemEventHandler handler = (_, _) => tcs.TrySetResult(true);
    watcher.Changed += handler;
    watcher.Created += handler;
    watcher.EnableRaisingEvents = true;

    File.WriteAllText(probe_path, "1");

    var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
    return winner == tcs.Task;
  }

  [Fact]
  public async Task fires_on_in_place_rewrite()
  {
    if(!await EnvironmentSupportsFileWatchingAsync())
      return;

    var conf = new ProjectConf { bindings = new() { ["test"] = new BindingsEntryConf { dll = dll_path } } };
    workspace.Init(new Types(), conf);

    var wait = WaitForChangeAsync(workspace, TimeSpan.FromSeconds(10));
    File.WriteAllText(dll_path, "v2 - rewritten in place");

    Assert.True(await wait, "expected BindingsDllChanged to fire after an in-place rewrite");
  }

  [Fact]
  public async Task fires_on_atomic_replace_via_rename()
  {
    if(!await EnvironmentSupportsFileWatchingAsync())
      return;

    var conf = new ProjectConf { bindings = new() { ["test"] = new BindingsEntryConf { dll = dll_path } } };
    workspace.Init(new Types(), conf);

    var wait = WaitForChangeAsync(workspace, TimeSpan.FromSeconds(10));

    // Mirrors how external build tools typically publish an output file: write to a temp
    // path in the same directory, then atomically move it over the destination.
    var tmp_path = dll_path + ".tmp";
    File.WriteAllText(tmp_path, "v2 - built externally");
    File.Move(tmp_path, dll_path, overwrite: true);

    Assert.True(await wait, "expected BindingsDllChanged to fire after an external rebuild (temp+rename)");
  }
}

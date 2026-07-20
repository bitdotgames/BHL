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

  [Fact]
  public async Task fires_on_in_place_rewrite()
  {
    var conf = new ProjectConf { bindings_dll = dll_path };
    workspace.Init(new Types(), conf);

    var wait = WaitForChangeAsync(workspace, TimeSpan.FromSeconds(10));
    File.WriteAllText(dll_path, "v2 - rewritten in place");

    Assert.True(await wait, "expected BindingsDllChanged to fire after an in-place rewrite");
  }

  [Fact]
  public async Task fires_on_atomic_replace_via_rename()
  {
    var conf = new ProjectConf { bindings_dll = dll_path };
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

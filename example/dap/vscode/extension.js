const vscode = require('vscode');
const net    = require('net');

const HOST     = 'localhost';
const PORT     = 7777;
const RETRY_MS = 1000;

let retryTimer = null;
let out = null;

function log(msg) {
  out.appendLine(`[${new Date().toLocaleTimeString()}] ${msg}`);
}

function activate(context) {
  out = vscode.window.createOutputChannel('BHL Debug');
  context.subscriptions.push(out);

  context.subscriptions.push(
    vscode.debug.registerDebugAdapterDescriptorFactory(
      'bhl',
      new BHLDebugAdapterDescriptorFactory()
    )
  );

  context.subscriptions.push(
    vscode.debug.onDidStartDebugSession(session => {
      if(session.configuration.type === 'bhl')
        log(`Session started: ${session.name}`);
    })
  );

  context.subscriptions.push(
    vscode.debug.onDidTerminateDebugSession(session => {
      if(session.configuration.type === 'bhl') {
        log(`Session ended: ${session.name}`);
        scheduleRetry();
      }
    })
  );

  context.subscriptions.push({ dispose: () => clearTimeout(retryTimer) });

  log(`Activated — probing ${HOST}:${PORT} every ${RETRY_MS}ms`);
  tryConnect();
}

function tryConnect() {
  if(vscode.debug.activeDebugSession?.configuration.type === 'bhl')
    return;

  const socket = new net.Socket();
  socket.setTimeout(1000);

  socket.once('connect', () => {
    socket.destroy();
    log(`Server found on ${HOST}:${PORT} — attaching`);
    vscode.debug.startDebugging(
      vscode.workspace.workspaceFolders?.[0],
      { type: 'bhl', request: 'attach', name: 'Attach to BHL (Unity)', host: HOST, port: PORT }
    );
  });

  socket.once('error',   err => { socket.destroy(); log(`Connect failed: ${err.message}`); scheduleRetry(); });
  socket.once('timeout', ()  => { socket.destroy(); log('Connect timed out'); scheduleRetry(); });

  socket.connect(PORT, HOST);
}

function scheduleRetry() {
  clearTimeout(retryTimer);
  retryTimer = setTimeout(tryConnect, RETRY_MS);
}

class BHLDebugAdapterDescriptorFactory {
  createDebugAdapterDescriptor(session) {
    const host = session.configuration.host || HOST;
    const port = session.configuration.port || PORT;
    log(`Creating debug adapter: ${host}:${port}`);
    return new vscode.DebugAdapterServer(port, host);
  }
}

module.exports = { activate };

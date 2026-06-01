const vscode = require('vscode');
const net    = require('net');

const HOST     = 'localhost';
const PORT     = 7777;
const RETRY_MS = 1000;

let retryTimer = null;
let out = null;
let connectAttempt = 0;

function log(msg) {
  out.appendLine(`[${new Date().toLocaleTimeString()}] ${msg}`);
}

function fmtBp(bp) {
  if(bp instanceof vscode.SourceBreakpoint) {
    const loc = bp.location;
    const file = loc.uri.fsPath.replace(/.*[/\\]/, '');
    const line = loc.range.start.line + 1;
    const cond = bp.condition ? ` cond=${bp.condition}` : '';
    return `${file}:${line}${cond} [${bp.enabled ? 'on' : 'off'}]`;
  }
  if(bp instanceof vscode.FunctionBreakpoint)
    return `fn:${bp.functionName} [${bp.enabled ? 'on' : 'off'}]`;
  return String(bp);
}

function activate(context) {
  out = vscode.window.createOutputChannel('BHL Debug');
  context.subscriptions.push(out);
  out.show(true);

  context.subscriptions.push(
    vscode.debug.registerDebugAdapterDescriptorFactory(
      'bhl',
      new BHLDebugAdapterDescriptorFactory()
    )
  );

  context.subscriptions.push(
    vscode.debug.onDidStartDebugSession(session => {
      if(session.configuration.type !== 'bhl') return;
      log(`Session started: ${session.name}`);
      const bps = vscode.debug.breakpoints.filter(bp => bp.enabled);
      log(`Breakpoints at session start (${bps.length}): ${bps.map(fmtBp).join(', ') || '(none)'}`);
    })
  );

  context.subscriptions.push(
    vscode.debug.onDidChangeBreakpoints(({ added, removed, changed }) => {
      for(const bp of added)   log(`Breakpoint added:   ${fmtBp(bp)}`);
      for(const bp of removed) log(`Breakpoint removed: ${fmtBp(bp)}`);
      for(const bp of changed) log(`Breakpoint changed: ${fmtBp(bp)}`);
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
  if(vscode.debug.activeDebugSession?.configuration.type === 'bhl') {
    log('Skipping probe — BHL debug session already active');
    return;
  }

  const attempt = ++connectAttempt;
  log(`Probe #${attempt} → ${HOST}:${PORT}`);

  const socket = new net.Socket();
  socket.setTimeout(1000);

  socket.once('connect', () => {
    socket.destroy();
    log(`#${attempt} Server found on ${HOST}:${PORT} — attaching`);
    vscode.debug.startDebugging(
      vscode.workspace.workspaceFolders?.[0],
      { type: 'bhl', request: 'attach', name: 'Attach to BHL (Unity)', host: HOST, port: PORT }
    ).then(
      started => log(`startDebugging result: ${started}`),
      err     => log(`startDebugging error: ${err?.message ?? err}`)
    );
  });

  socket.once('error',   err => { socket.destroy(); log(`#${attempt} Connect failed: ${err.message}`); scheduleRetry(); });
  socket.once('timeout', ()  => { socket.destroy(); log(`#${attempt} Connect timed out`); scheduleRetry(); });

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
    log(`Creating debug adapter proxy → ${host}:${port}`);
    return new vscode.DebugAdapterInlineImplementation(new DAPProxy(host, port, scheduleRetry));
  }
}

// Transparent proxy between VS Code and the BHL DAP server.
// Logs every message so we can see exact source paths.
class DAPProxy {
  constructor(host, port, onClose) {
    this._emitter = new vscode.EventEmitter();
    this.onDidSendMessage = this._emitter.event;
    this._buf = Buffer.alloc(0);

    this._socket = net.createConnection(port, host);
    this._socket.on('data',  chunk => { this._buf = Buffer.concat([this._buf, chunk]); this._drain(); });
    this._socket.on('error', err   => log(`DAP socket error: ${err.message}`));
    this._socket.on('close', ()    => { log('DAP socket closed — scheduling retry'); onClose(); });
  }

  // VS Code → server
  handleMessage(msg) {
    logDAP('→', msg);
    const body   = JSON.stringify(msg);
    const header = `Content-Length: ${Buffer.byteLength(body, 'utf8')}\r\n\r\n`;
    this._socket.write(header + body, 'utf8');
  }

  // server → VS Code
  _drain() {
    while(true) {
      const sep = this._buf.indexOf('\r\n\r\n');
      if(sep === -1) break;
      const header   = this._buf.toString('utf8', 0, sep);
      const match    = /Content-Length:\s*(\d+)/i.exec(header);
      if(!match) break;
      const bodyLen  = parseInt(match[1]);
      const start    = sep + 4;
      if(this._buf.length < start + bodyLen) break;
      const body     = this._buf.toString('utf8', start, start + bodyLen);
      this._buf      = this._buf.slice(start + bodyLen);
      try {
        const msg = JSON.parse(body);
        logDAP('←', msg);
        this._emitter.fire(msg);
      } catch(e) {
        log(`DAP parse error: ${e.message}`);
      }
    }
  }

  dispose() { this._socket.destroy(); }
}

function logDAP(dir, msg) {
  if(msg.type === 'request') {
    log(`${dir} req  ${msg.command} #${msg.seq}`);
    if(msg.command === 'setBreakpoints') {
      const src = msg.arguments?.source;
      log(`     source path: ${src?.path ?? src?.name ?? '(none)'}`);
      for(const bp of (msg.arguments?.breakpoints ?? []))
        log(`     line ${bp.line}${bp.condition ? ` cond=${bp.condition}` : ''}`);
    }

  } else if(msg.type === 'response') {
    log(`${dir} resp ${msg.command} #${msg.request_seq} success=${msg.success}`);
    if(msg.command === 'setBreakpoints') {
      for(const bp of (msg.body?.breakpoints ?? []))
        log(`     id=${bp.id} verified=${bp.verified} line=${bp.line} source=${bp.source?.path ?? '(same)'}`);
    }
    if(msg.command === 'stackTrace') {
      for(const f of (msg.body?.stackFrames ?? []))
        log(`     frame ${f.id} ${f.source?.path ?? f.source?.name ?? '(no source)'}:${f.line}`);
    }

  } else if(msg.type === 'event') {
    if(msg.event === 'stopped') {
      const b = msg.body ?? {};
      const ids = b.hitBreakpointIds?.join(',') ?? '(none)';
      log(`${dir} evt  stopped reason=${b.reason} thread=${b.threadId} hitIds=${ids}`);
    } else {
      log(`${dir} evt  ${msg.event}`);
    }
  }
}

function deactivate() {
  clearTimeout(retryTimer);
  log('Deactivated');
}

module.exports = { activate, deactivate };

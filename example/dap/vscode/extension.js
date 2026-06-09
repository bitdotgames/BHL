const vscode = require('vscode');
const net    = require('net');

const HOST = 'localhost';
const PORT = 7777;

let out = null;

function log(msg) {
  out.appendLine(`[${new Date().toLocaleTimeString()}] ${msg}`);
}

function fmtBp(bp) {
  if(bp instanceof vscode.SourceBreakpoint) {
    const loc  = bp.location;
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

  context.subscriptions.push(
    vscode.debug.registerDebugAdapterDescriptorFactory('bhl', new BHLDebugAdapterDescriptorFactory())
  );

  context.subscriptions.push(
    vscode.debug.onDidStartDebugSession(session => {
      if(session.configuration.type !== 'bhl') return;
      out.show(true);
      log(`Session started: ${session.name}`);
      const bps = vscode.debug.breakpoints.filter(bp => bp.enabled);
      log(`Breakpoints at session start (${bps.length}): ${bps.map(fmtBp).join(', ') || '(none)'}`);
    })
  );

  context.subscriptions.push(
    vscode.debug.onDidTerminateDebugSession(session => {
      if(session.configuration.type === 'bhl')
        log(`Session ended: ${session.name}`);
    })
  );

  context.subscriptions.push(
    vscode.debug.onDidChangeBreakpoints(({ added, removed, changed }) => {
      for(const bp of added)   log(`Breakpoint added:   ${fmtBp(bp)}`);
      for(const bp of removed) log(`Breakpoint removed: ${fmtBp(bp)}`);
      for(const bp of changed) log(`Breakpoint changed: ${fmtBp(bp)}`);
    })
  );
}

class BHLDebugAdapterDescriptorFactory {
  async createDebugAdapterDescriptor(session) {
    const host = session.configuration.host || HOST;
    const port = session.configuration.port || PORT;
    await waitForServer(host, port);
    log(`Server ready — starting DAP session on ${host}:${port}`);
    return new vscode.DebugAdapterInlineImplementation(new DAPProxy(host, port));
  }
}

const RETRY_MS = 1000;

// Probes host:port every RETRY_MS until a TCP connection succeeds,
// then resolves. VS Code waits on the Thenable returned by
// createDebugAdapterDescriptor, so the DAP session only starts once
// the server is actually ready — no message buffering needed.
function waitForServer(host, port) {
  return new Promise(resolve => {
    let logged = false;

    function attempt() {
      const socket = net.createConnection(port, host);
      socket.setTimeout(RETRY_MS);

      socket.once('connect', () => { socket.destroy(); resolve(); });
      socket.once('error',   () => { socket.destroy(); retry(); });
      socket.once('timeout', () => { socket.destroy(); retry(); });
    }

    function retry() {
      if(!logged) { logged = true; log(`Waiting for BHL server on ${host}:${port}...`); }
      setTimeout(attempt, RETRY_MS);
    }

    attempt();
  });
}

class DAPProxy {
  constructor(host, port) {
    this._emitter = new vscode.EventEmitter();
    this.onDidSendMessage = this._emitter.event;
    this._buf = Buffer.alloc(0);

    this._socket = net.createConnection(port, host);
    this._socket.on('data',  chunk => { this._buf = Buffer.concat([this._buf, chunk]); this._drain(); });
    this._socket.on('error', err   => log(`DAP socket error: ${err.message}`));
    this._socket.on('close', ()    => log('DAP socket closed'));
  }

  handleMessage(msg) {
    logDAP('→', msg);
    const body   = JSON.stringify(msg);
    const header = `Content-Length: ${Buffer.byteLength(body, 'utf8')}\r\n\r\n`;
    this._socket.write(header + body, 'utf8');
  }

  _drain() {
    while(true) {
      const sep = this._buf.indexOf('\r\n\r\n');
      if(sep === -1) break;
      const header  = this._buf.toString('utf8', 0, sep);
      const match   = /Content-Length:\s*(\d+)/i.exec(header);
      if(!match) break;
      const bodyLen = parseInt(match[1]);
      const start   = sep + 4;
      if(this._buf.length < start + bodyLen) break;
      const body    = this._buf.toString('utf8', start, start + bodyLen);
      this._buf     = this._buf.slice(start + bodyLen);
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
      const b   = msg.body ?? {};
      const ids = b.hitBreakpointIds?.join(',') ?? '(none)';
      log(`${dir} evt  stopped reason=${b.reason} thread=${b.threadId} hitIds=${ids}`);
    } else {
      log(`${dir} evt  ${msg.event}`);
    }
  }
}

function deactivate() {}

module.exports = { activate, deactivate };

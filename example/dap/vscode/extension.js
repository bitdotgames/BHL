const vscode = require('vscode');

function activate(context) {
  context.subscriptions.push(
    vscode.debug.registerDebugAdapterDescriptorFactory(
      'bhl',
      new BHLDebugAdapterDescriptorFactory()
    )
  );
}

// Tells VS Code to connect directly to the TCP server started by BHLDebugServer.
// VS Code's built-in DAP client handles all protocol framing and message exchange.
class BHLDebugAdapterDescriptorFactory {
  createDebugAdapterDescriptor(session) {
    const host = session.configuration.host || 'localhost';
    const port = session.configuration.port || 7777;
    return new vscode.DebugAdapterServer(port, host);
  }
}

module.exports = { activate };

'use strict';

import * as path from 'path';
import { workspace, commands, ExtensionContext } from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  const config = workspace.getConfiguration('bhl');
  const executablePath = config.get<string>('executablePath') || 'bhl';
  const logFile = config.get<string>('logFile') || '';
  const forceRebuild = config.get<boolean>('forceRebuild') || false;

  const args = ['lsp'];
  if (logFile) {
    args.push(`--log-file=${logFile}`);
  }

  const isWindows = process.platform === 'win32';
  const command = isWindows ? 'cmd.exe' : executablePath;
  const spawnArgs = isWindows ? ['/c', executablePath, ...args] : args;
  const spawnOptions = forceRebuild
    ? { env: { ...process.env, BHL_REBUILD: '1', BHL_SILENT: '1' } }
    : undefined;

  const serverOptions: ServerOptions = {
    command,
    args: spawnArgs,
    ...(spawnOptions ? { options: spawnOptions } : {}),
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [
      { scheme: 'file', language: 'bhl' }
    ],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/*.bhl')
    },
  };

  client = new LanguageClient('bhl', 'BHL Language Server', serverOptions, clientOptions);
  client.start();

  context.subscriptions.push(
    client/*,
    commands.registerCommand('bhl.reload', () => {
      client.sendRequest('workspace/executeCommand', { command: 'bhl.reload' });
    })*/
  );
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}

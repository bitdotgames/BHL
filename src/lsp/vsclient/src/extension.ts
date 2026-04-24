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
  const serverCommand = config.get<string>('executablePath') || 'bhl';
  const logFile = config.get<string>('logFile') || '';
  const forceRebuild = config.get<boolean>('forceRebuild') ?? true;

  const parts = serverCommand.trim().split(/\s+/);
  const command = parts[0];
  const args = [...parts.slice(1), 'lsp'];
  if (logFile) {
    args.push(`--log-file=${logFile}`);
  }

  const spawnOptions = forceRebuild
    ? { env: { ...process.env, BHL_REBUILD: '1', BHL_SILENT: '1' } }
    : undefined;

  const serverOptions: ServerOptions = {
    command,
    args,
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

'use strict';

import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
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

  const serverOptions: ServerOptions = {
    command: executablePath,
    args,
    options: forceRebuild ? { env: { ...process.env, BHL_REBUILD: '1', BHL_SILENT: '1' } } : {},
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

  context.subscriptions.push(client);
}

export function deactivate(): Thenable<void> | undefined {
  return client?.stop();
}

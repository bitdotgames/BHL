# Visual Studio Code

## Prerequisites

- [Node.js](https://nodejs.org/) and npm
- `vsce` packaging tool: `npm install -g @vscode/vsce`
- The `bhl` executable on your PATH (or configure its path in settings)

## Build and install the extension

```sh
cd src/lsp/vsclient
npm install
npm run package       # produces bhl-0.0.1.vsix
code --install-extension bhl-0.0.1.vsix
```

Reload VS Code. The extension activates automatically when you open a `.bhl` file.

## Settings

Open **Code > Settings > Extensions ** and search for **BHL**, or add to `settings.json`:

```json
{
  "bhl.executablePath": "/path/to/bhl",
  "bhl.logFile": "/tmp/bhlsp.log",
  "bhl.forceRebuild": false
}
```

| Setting | Default | Description |
|---|---|---|
| `bhl.executablePath` | `""` (uses `bhl` on PATH) | Path to the `bhl` executable |
| `bhl.logFile` | `""` (disabled) | Path for the LSP log file |
| `bhl.forceRebuild` | `false` | Set `BHL_REBUILD=1` to force full rebuild on startup (recommended if you want to apply LSP server fixes on each client restart) |

## Project setup

Open the folder that contains your `bhl.proj` file as the workspace root (**File > Open Folder**).
The LSP server detects this file and initializes properly.

---

# SublimeText

## Syntax file

First you have to setup a proper syntax which declares scope for .bhl files.

* Tools > Developer > New Syntax
* Add the following contents and name the file as **bhl.sublime-syntax**

```
%YAML 1.2
---
# http://www.sublimetext.com/docs/syntax.html

name: bhl
file_extensions:
  - bhl
scope: source.bhl

contexts:
  main: []
```

**Make sure** you can select bhl syntax in **View > Syntax** menu. You might need to restart an editor. Now you can proceed to LSP setup.

## LSP setup

* Press combination Win/Linux: ctrl+shift+p, Mac: cmd+shift+p
* Write: Install Package Control
* Select LSP package
* Restart Sublime
* Go To Settings > Package Settings > LSP > Settings

Add the following content to the file:

```
// Settings in here override those in "LSP/LSP.sublime-settings"
{
	"semantic_highlighting": true,

	"clients": {
		"bhlsp" : {
			"enabled": true,
			"command" : [
				"path/to/bhl/bhl",
				"lsp",
				"--log-file=/tmp/bhlsp.log"
			],
			"selector": "source.bhl"
		}
	}
}
```

## Project setup

You have to add the directory which contains **bhl.proj** file the to the Sublime project. So that it's
displayed as a separate entry in project's **FOLDERS** side bar.
Once the bhl LSP server starts via Sublime it will detect this directory and will be properly initialized.

# NeoVim

## Neovim 0.11+ (built-in LSP)

No plugins required. Add to your config:

```lua
vim.lsp.config("bhl", {
  cmd = { "/path/to/bhl", "lsp", "--log-file=/tmp/bhlsp.log" },
  filetypes = { "bhl" },
  root_markers = { "bhl.proj" },
})
vim.lsp.enable("bhl")

vim.filetype.add({ extension = { bhl = "bhl" } })
```

To force full rebuild on startup:

```lua
vim.env.BHL_REBUILD = 1
```

## Neovim (nvim-lspconfig)

Install `nvim-lspconfig`:

```
Plug 'neovim/nvim-lspconfig'
```

Configure LSP for bhl files:

```lua
local configs = require('lspconfig.configs')
if not configs.bhl then
  configs.bhl = {
    default_config = {
      cmd = { '/path/to/bhl', 'lsp', '--log-file=/tmp/bhlsp.log' },
      filetypes = { 'bhl' },
      root_dir = require('lspconfig.util').root_pattern('bhl.proj'),
      settings = {},
    },
  }
end
require('lspconfig').bhl.setup{}

vim.filetype.add({ extension = { bhl = "bhl" } })
```

## Optional: Telescope for LSP navigation

```lua
-- Plug 'nvim-lua/plenary.nvim'
-- Plug 'nvim-telescope/telescope.nvim'
vim.keymap.set('n', '<leader>r', '<cmd>Telescope lsp_references<CR>')
vim.keymap.set('n', '<leader>d', '<cmd>Telescope lsp_definitions<CR>')
```

## Project setup

Open Neovim from the directory containing your `bhl.proj` file, or ensure that directory is an ancestor of the files you edit. The LSP server uses `bhl.proj` as the root marker.


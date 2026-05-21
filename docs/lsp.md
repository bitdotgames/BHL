# Visual Studio Code

See **[https://github.com/pachanga/BHL-VSCode](https://github.com/pachanga/BHL-VSCode)** for installation and setup.

## Language features

### Completions

- Symbols from the current file and all imported modules are suggested.
- Symbols from modules not yet imported are also offered; selecting them automatically inserts the required `import` line at the top of the file.
- Typing inside an `import "..."` string triggers module-name completions, listing all known modules that are not already imported.

### Fix missing imports on save

When you save a file, the server scans for unresolved symbol errors and automatically inserts the missing `import` statements. If a symbol is found in exactly one known module, the import is added without prompting.

### Remove unused imports on save

*(available as a workspace command)* The server detects `import` statements whose symbols are never used and can remove them.

### Auto-reload on project changes

The server watches `bhl.proj` for changes. If you edit the project file (add source directories, change defines, update the bindings path) the server reloads the entire workspace automatically — no manual restart needed.

The server also watches the `bindings_dll` path declared in `bhl.proj`. If the bindings DLL is rebuilt on disk the server reloads automatically so the new C# types and functions are immediately visible.

### Diagnostics and compilation timing

Every keystroke triggers an incremental recompile. Only the changed file and its direct importers are reprocessed; unrelated files keep their previous results. The recompile time is logged to the LSP output channel (`window/logMessage`) so you can track performance:

```
BHL: recompiled in 12ms
```

On startup or after a manual reload (`bhl.reload` command):

```
BHL: 47 file(s) indexed in 340ms
```

### Available commands

| Command | Description |
|---|---|
| `bhl.reload` | Reload bindings and recompile the whole workspace |

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


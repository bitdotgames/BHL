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

Make sure you can select bhl syntax in View > Syntax menu. Now you can proceed to LSP setup.

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

* Install 'neovim/nvim-lspconfig' extension which simplifies configuration of LSP servers. For example 
using Plug:

```
Plug 'neovim/nvim-lspconfig'
```

* Install telescope extension and configure it for basic LSP actions:

```
Plug 'nvim-lua/plenary.nvim' 
Plug 'nvim-telescope/telescope.nvim' 
nnoremap <leader>r :Telescope lsp_references<CR>
nnoremap <leader>d :Telescope lsp_definitions<CR>
```

* Configure LSP for bhl files using Lua:

```
local configs = require('lspconfig.configs')
-- Check if it's already defined for when reloading this file.
if not configs.bhl then
  configs.bhl = {
    default_config = {
      cmd = {'/path/to//bhl/bhl', 'lsp', '--log-file=/tmp/bhlsp.log'},
      filetypes = {'bhl'},
      root_dir = lspconfig.util.root_pattern('bhl.proj'),
      settings = {},
    };
  }
end
lspconfig.bhl.setup{}
vim.cmd [[ au BufNewFile,BufRead /*.bhl setf bhl ]]
```


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

Now you can proceed to LSP setup.

## LSP setup

* Install Package Control
* Install LSP package
* Restart Sublime
* Go To Settings > Package Settings > LSP > Settings

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

For simplicity you have to make the directory which contains **bhl.proj** file the root directory of the project. 
This way once the LSP server starts via Sublime it will be properly initialized.


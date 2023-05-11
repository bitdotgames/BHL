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

You have to add the directory which contains **bhl.proj** file the to the Sublime project. So that it's
displayed as a separate entry in project's **FOLDERS** side bar.
Once the bhl LSP server starts via Sublime it will detect this directory and will be properly initialized.


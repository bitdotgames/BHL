# SublimeText

## LSP settings

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
				"--inc-path=/path/to/root/configs"
			],
			"selector": "source.bhl"
		}
	}
}
```

## syntax bhl

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

#extends: Packages/Go/Go.sublime-syntax
```

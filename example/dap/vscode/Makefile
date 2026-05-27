NAME    := $(shell node -e "process.stdout.write(require('./package.json').name)")
VERSION := $(shell node -e "process.stdout.write(require('./package.json').version)")
VSIX    := $(NAME)-$(VERSION).vsix

.PHONY: all install pack clean

all: install

pack: $(VSIX)

$(VSIX): package.json extension.js
	npx --yes @vscode/vsce package --allow-missing-repository

install: $(VSIX)
	code --install-extension $(VSIX) --force

clean:
	rm -f $(VSIX)

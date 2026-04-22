.PHONY: all build package install clean

all: package

build:
	npm run compile

package: build
	npm run package

install: package
	code --install-extension bhl-*.vsix

clean:
	rm -rf out/ bhl-*.vsix

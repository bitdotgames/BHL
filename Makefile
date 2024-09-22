.PHONY: build
build:
	dotnet build bhl.csproj

.PHONY: publish
publish:
	dotnet publish bhl.csproj

.PHONY: test
test:
	cd ./tests && dotnet test

.PHONY: examples
examples:
	cd ./example && ./run.sh

.PHONY: geng
geng:
	mkdir -p ./tmp
	cp ./grammar/bhlPreprocLexer.g ./tmp/
	cp ./grammar/bhlPreprocParser.g ./tmp/
	cp ./grammar/bhlLexer.g ./tmp/
	cp ./grammar/bhlParser.g ./tmp/
	cp ./util/g4sharp ./tmp/
	cd tmp && ./g4sharp *.g && cp bhl*.cs ../src/g/

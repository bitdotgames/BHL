.PHONY: build
build:
	dotnet build bhl.csproj

.PHONY: lsp
lsp:
	dotnet publish bhl.csproj

.PHONY: test
test:
	cd ./tests && dotnet test

.PHONY: bench
bench:
	cd ./bench && dotnet run -c Release --framework net8.0 -- --minIterationCount 9 --maxIterationCount 12 -f '*'

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

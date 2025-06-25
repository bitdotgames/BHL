NET=net8.0

.PHONY: build
build:
	dotnet build --framework $(NET) bhl.csproj

.PHONY: publish
publish:
	dotnet publish --framework $(NET) bhl.csproj

.PHONY: clean
clean:
	dotnet clean bhl.csproj
	rm -rf ./obj
	rm -rf ./bin
	rm -rf ./build
	rm -rf ./tmp
	rm -rf ./src/vm/obj
	rm -rf ./src/vm/bin
	rm -rf ./src/compile/obj
	rm -rf ./src/compile/bin
	rm -rf ./src/lsp/obj
	rm -rf ./src/lsp/bin

.PHONY: lsp
lsp: publish

.PHONY: test
test:
	cd ./tests && dotnet test --framework $(NET)

.PHONY: bench
bench:
	cd ./bench && dotnet run -c Release --framework $(NET) -- --minIterationCount 9 --maxIterationCount 12 -f '*'

.PHONY: examples
examples:
	cd ./example && ./run.sh

.PHONY: geng
geng:
	./util/g4sharp ./grammar/*.g && mv ./grammar/bhl*.cs ./src/g/ && rm ./grammar/*.interp && rm ./grammar/*.tokens

publish:
	dotnet publish bhl.csproj

test:
	cd ./tests && dotnet test

geng:
	mkdir -p ./tmp
	cp ./grammar/bhlPreprocLexer.g ./tmp/
	cp ./grammar/bhlPreprocParser.g ./tmp/
	cp ./grammar/bhlLexer.g ./tmp/
	cp ./grammar/bhlParser.g ./tmp/
	cp ./util/g4sharp ./tmp/
	cd tmp && ./g4sharp *.g && cp bhl*.cs ../src/g/

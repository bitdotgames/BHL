.PHONY: build
build:
	dotnet build bhl.csproj

.PHONY: publish
publish:
	dotnet publish bhl.csproj

# --- Unity package version ---------------------------------------------------
# Mirror the version from src/vm/version.cs (source of truth) into the Unity
# package.json as bare SemVer (leading 'v' stripped).
.PHONY: sync-unity-version
sync-unity-version:
	@ver=$$(sed -nE 's/.*Name[[:space:]]*=[[:space:]]*"v?([^"]+)".*/\1/p' src/vm/version.cs); \
	tmp=$$(mktemp); \
	jq --arg v "$$ver" '.version = $$v' src/package.json > $$tmp && mv $$tmp src/package.json; \
	echo "src/package.json version -> $$ver"

# --- self-contained, dotnet-free bhl binaries (one file per platform) --------
DIST_DIR ?= ./build/dist
STANDALONE_RIDS := linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64

# single compressed self-contained file; no dotnet needed on the host.
# R2R is intentionally OFF: for the long-lived LSP server startup is irrelevant,
# and dropping it yields a smaller binary and lets all RIDs cross-build from one host.
STANDALONE_FLAGS := -c Release --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:PublishReadyToRun=false \
  -p:InvariantGlobalization=true \
  -p:DebugType=none

# build one RID: `make publish-standalone-one RID=osx-arm64`
.PHONY: publish-standalone-one
publish-standalone-one:
	dotnet publish bhl.csproj $(STANDALONE_FLAGS) -r $(RID) -o "$(DIST_DIR)/$(RID)"

# build all shipped RIDs
.PHONY: publish-standalone
publish-standalone:
	@for rid in $(STANDALONE_RIDS); do \
	  echo "==> self-contained bhl for $$rid"; \
	  $(MAKE) --no-print-directory publish-standalone-one RID=$$rid || exit 1; \
	done
	@echo "Standalone bhl binaries in $(DIST_DIR)/<rid>/ — run 'bhl lsp' from any of them."

# just the current host's platform
.PHONY: publish-standalone-host
publish-standalone-host:
	dotnet publish bhl.csproj $(STANDALONE_FLAGS) -o "$(DIST_DIR)/host"

.PHONY: clean
clean:
	dotnet clean bhl.csproj
	rm -rf ./obj
	rm -rf ./bin
	rm -rf ./build
	rm -rf ./tmp
	rm -rf ./src/vm/obj
	rm -rf ./src/vm/bin
	rm -rf ./src/vm/build
	rm -rf ./src/compile/obj
	rm -rf ./src/compile/bin
	rm -rf ./src/compile/build
	rm -rf ./src/lsp/obj
	rm -rf ./src/lsp/bin
	rm -rf ./src/lsp/build
	rm -rf ./tests/build
	rm -rf ./tests/obj

.PHONY: lsp
lsp: publish

.PHONY: test
test:
	cd ./tests && dotnet test

.PHONY: bench
bench:
	cd ./bench && dotnet run -c Release -- --minIterationCount 9 --maxIterationCount 12 -f '*'

.PHONY: examples
examples:
	cd ./example && ./run.sh

.PHONY: geng
geng:
	./util/g4sharp ./grammar/*.g && mv ./grammar/bhl*.cs ./src/front/g/ && rm ./grammar/*.interp && rm ./grammar/*.tokens
	./util/bhl_front_guard src/front/g/

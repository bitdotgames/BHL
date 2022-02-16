#!/bin/bash

set -e

function mytrap {
  if [ -f tmp/bhl.err ];
  then
    echo "======================="
    echo "BHL ERROR:"
    cat tmp/bhl.err
    echo ""
  fi
}

trap mytrap ERR

#1. Running frontend over bhl sources
rm -rf tmp/bhl.bytes
rm -rf tmp/bhl.err
../bhl run --user-sources=bindings.cs -C --dir=. --result=tmp/bhl.bytes --cache_dir=tmp --error=tmp/bhl.err
#2. Building bhl backend dll
../bhl build_back_dll mcs 
#3. Building example: adding bhl backend dll, user bindings 
mcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
#4. Running example
MONO_PATH=$MONO_PATH:../ mono --debug example.exe

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

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

#1. Building bhl backend dll
(cd .. && ./bhl build_back_dll mcs) 
#2. Building example: adding bhl backend dll, user bindings 
mcs -r:../bhl_back.dll -out:example.exe $DIR/bindings.cs $DIR/example.cs

#3. Compiling bhl sources to byte code
rm -rf tmp/bhl.bytes
rm -rf tmp/bhl.err
(cd .. && ./bhl compile --user-sources=$DIR/bindings.cs -C --dir=$DIR --result=$DIR/tmp/bhl.bytes --tmp-dir=$DIR/tmp --error=$DIR/tmp/bhl.err)
#4. Running example
MONO_PATH=$MONO_PATH:../ mono --debug example.exe tmp/bhl.bytes


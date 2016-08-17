#!/bin/bash

#1. Running frontend over bhl sources
MONO_PATH=/Applications/Unity/Unity.app/Contents/Frameworks/Mono
php ../bhl -D USER_SOURCES=bindings.cs run --dir=. --result=tmp/bhl.bytes --cache_dir=tmp --error=tmp/bhl.err

if [ $? -ne 0 ] ;
then
  echo "======================="
  echo "BHL ERROR:"
  if [ -f tmp/bhl.err ];
  then
    cat tmp/bhl.err
    echo ""
  fi
  exit 1
fi

set -e
#2. Building bhl backend dll
php ../bhl build_back_dll $MONO_PATH/bin/gmcs  
#3. Building example: adding bhl backend dll, user bindings 
$MONO_PATH/bin/gmcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
#4. Running example
MONO_PATH=$MONO_PATH/lib/mono/2.0/:../ $MONO_PATH/bin/mono --debug example.exe

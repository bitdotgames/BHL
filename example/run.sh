#!/bin/bash

UNITY_MONO_PATH=/Applications/Unity/Unity.app/Contents/Frameworks/Mono
#Unity 5.4 it's here
#UNITY_MONO_PATH=/Applications/Unity/Unity.app/Contents/Mono

#1. Running frontend over bhl sources
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

if [ "$1" == "-unity" ] ;
then
  #2. Building bhl backend dll
  php ../bhl build_back_dll $UNITY_MONO_PATH/bin/gmcs  
  #3. Building example: adding bhl backend dll, user bindings 
  $UNITY_MONO_PATH/bin/gmcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
  #4. Running example
  MONO_PATH=$UNITY_MONO_PATH/lib/mono/2.0/:../ $UNITY_MONO_PATH/bin/mono --debug example.exe
else
 php ../bhl build_back_dll mcs 
 mcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
 mono --debug example.exe
fi

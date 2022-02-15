#!/bin/bash

#For Unity 5.4 it's here
UNITY_MONO_PATH=/Applications/Unity/Unity.app/Contents/Mono
if [ ! -d $UNITY_MONO_PATH ] ; then
  UNITY_MONO_PATH=/Applications/Unity/Unity.app/Contents/Frameworks/Mono
fi

#1. Running frontend over bhl sources
rm -rf tmp/bhl.bytes
rm -rf tmp/bhl.err
../bhl run --user-sources=bindings.cs -C --dir=. --result=tmp/bhl.bytes --cache_dir=tmp --error=tmp/bhl.err

if [ $? -ne 0 ] ;
then
  if [ -f tmp/bhl.err ];
  then
    echo "======================="
    echo "BHL ERROR:"
    cat tmp/bhl.err
    echo ""
  fi
  exit 1
fi

set -e

if [ "$1" == "-unity" ] ;
then
  #2. Building bhl backend dll
  ../bhl build_back_dll $UNITY_MONO_PATH/bin/gmcs  
  #3. Building example: adding bhl backend dll, user bindings 
  $UNITY_MONO_PATH/bin/gmcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
  #4. Running example
  MONO_PATH=$UNITY_MONO_PATH/lib/mono/2.0/:../ $UNITY_MONO_PATH/bin/mono --debug example.exe
else
  #2. Building bhl backend dll
  ../bhl build_back_dll mcs 
  #3. Building example: adding bhl backend dll, user bindings 
  mcs -r:../bhl_back.dll -out:example.exe bindings.cs example.cs
  MONO_PATH=$MONO_PATH:../ mono --debug example.exe
fi

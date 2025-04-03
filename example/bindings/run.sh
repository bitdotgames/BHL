#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

rm -rf $DIR/tmp/bhl.bytes 
rm -rf $DIR/bin
rm -rf $DIR/obj

#Compiling bhl sources to byte code
$DIR/../../bhl compile -p $DIR/bhl.proj --result=$DIR/tmp/bhl.bytes

#Running example
dotnet run --framework net8.0 --project $DIR/example.csproj -- $DIR/tmp/bhl.bytes

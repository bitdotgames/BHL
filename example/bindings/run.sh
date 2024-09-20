#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

rm -rf $DIR/tmp/bhl.bytes 

#Compiling bhl sources to byte code
$DIR/../../bhl compile -p $DIR/bhl.proj --result=$DIR/tmp/bhl.bytes

#Running example
dotnet run --project $DIR/example.csproj -- $DIR/tmp/bhl.bytes

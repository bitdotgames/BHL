#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

#Compiling bhl sources to byte code
rm $DIR/tmp/bhl.bytes 
$DIR/../../bhl compile -p $DIR/bhl.proj --result=$DIR/tmp/bhl.bytes

#Running example
dotnet run --project $DIR/example.csproj -- $DIR/tmp/bhl.bytes

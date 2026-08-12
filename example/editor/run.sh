#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

rm -rf $DIR/tmp
rm -rf $DIR/bin
rm -rf $DIR/obj

#NOTE: no separate 'bhl compile' step - example.cs compiles and runs in one process
dotnet run --project $DIR/example.csproj -- $DIR

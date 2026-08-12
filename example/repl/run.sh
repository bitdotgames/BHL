#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

rm -rf $DIR/tmp
rm -rf $DIR/bin
rm -rf $DIR/obj

#NOTE: no separate 'bhl compile' step - example.bhl (Main()) is compiled and run in
#      process, then an extra expression unknown at compile time is evaluated on the
#      spot against the same VM via VM.EvalExpression
dotnet run --project $DIR/example.csproj -- $DIR

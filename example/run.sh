#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

#cd $DIR/hello && ./run.sh
cd $DIR/bindings && ./run.sh

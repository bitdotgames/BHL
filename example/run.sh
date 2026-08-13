#!/bin/bash

set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

export BHL_REBUILD=1
export BHL_SILENT=0

cd $DIR/hello && ./run.sh
cd $DIR/unity && ./run.sh
cd $DIR/editor && ./run.sh
cd $DIR/postproc && ./run.sh
cd $DIR/repl && ./run.sh
cd $DIR/gameplay && ./run.sh

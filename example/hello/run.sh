#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

$DIR/../../bhl run $DIR/hello.bhl
$DIR/../../bhl run $DIR/hello_args.bhl Bob
$DIR/../../bhl run $DIR/hello_func.bhl --func=other Tom
$DIR/../../bhl run $DIR/hello_coro.bhl
$DIR/../../bhl repl -e 'std.io.WriteLine("Hello, World! (as repl)")'

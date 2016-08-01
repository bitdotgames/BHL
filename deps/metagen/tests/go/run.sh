#!/bin/sh

export GOPATH=`readlink -f ../../../../go/`

rm -rf autogen/*
php make.php
go run test.go

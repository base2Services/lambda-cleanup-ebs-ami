#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

WD=$PWD
cd $DIR/..
rm -rf bin
dotnet restore
dotnet publish
cd bin/Debug/netcoreapp2.1/publish && zip -r Base2.Lambdas.zip *
cd $WD
mv bin/Debug/netcoreapp2.1/publish/Base2.Lambdas.zip .

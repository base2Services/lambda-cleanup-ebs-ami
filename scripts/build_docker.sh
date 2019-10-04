#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

WD=$PWD
cd $DIR/..
rm -rf bin
docker run --rm -v $HOME/.nuget:/root/.nuget -v $DIR/..:/project -w /project microsoft/dotnet dotnet restore
docker run --rm -v $HOME/.nuget:/root/.nuget -v $DIR/..:/project -w /project microsoft/dotnet dotnet publish
cd bin/Debug/netcoreapp2.1/publish && zip -r Base2.Lambdas.zip *
cd $WD
mv bin/Debug/netcoreapp2.1/publish/Base2.Lambdas.zip .

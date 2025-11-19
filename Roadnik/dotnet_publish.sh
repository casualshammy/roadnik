#!/bin/bash

TARGETARCH=$1

if [ "$TARGETARCH" == "amd64" ]; then
    dotnet publish -r linux-x64 -o out Roadnik && chmod +x /app/out/roadnik-server
elif [ "$TARGETARCH" == "arm64" ]; then
    dotnet publish -r linux-arm64 -o out Roadnik && chmod +x /app/out/roadnik-server
fi
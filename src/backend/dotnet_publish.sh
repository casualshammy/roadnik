#!/bin/bash

TARGETARCH=$1

if [ "$TARGETARCH" == "amd64" ]; then
    dotnet publish -r linux-musl-x64 -o out src/backend && chmod +x /app/out/roadnik-server
elif [ "$TARGETARCH" == "arm64" ]; then
    dotnet publish -r linux-musl-arm64 -o out src/backend && chmod +x /app/out/roadnik-server
fi
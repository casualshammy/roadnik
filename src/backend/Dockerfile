FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-dotnet
ARG TARGETARCH
WORKDIR /app
RUN apt update && apt -y --allow-downgrades install clang zlib1g-dev
COPY ./ ./
RUN if [ "$TARGETARCH" = "amd64" ]; then \
        dotnet publish -r linux-x64 -o out src/backend && chmod +x /app/out/roadnik-server; \
    elif [ "$TARGETARCH" = "arm64" ]; then \
        dotnet publish -r linux-arm64 -o out src/backend && chmod +x /app/out/roadnik-server; \
    fi

FROM dhi.io/node:26-alpine3.23-dev AS build-node
WORKDIR /app
COPY ./src/frontend/app ./
RUN npm i
RUN npm run build
COPY ./src/frontend/landing ./dist

FROM dhi.io/dotnet:10 AS final
EXPOSE 8080
WORKDIR /app
COPY --from=build-dotnet /app/out ./bin
COPY --from=build-node /app/dist ./www

ENTRYPOINT ["/app/bin/roadnik-server"]
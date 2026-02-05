FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-dotnet
ARG TARGETARCH
WORKDIR /app
RUN apt-get update && apt-get -y install clang zlib1g-dev
COPY ./ ./
COPY ./Roadnik/dotnet_publish.sh ./
RUN chmod +x ./dotnet_publish.sh
RUN ./dotnet_publish.sh $TARGETARCH

FROM node:alpine AS build-node
WORKDIR /app
COPY ./www-vue ./
RUN npm i
RUN npm run build
COPY ./www-main-page ./dist

FROM mcr.microsoft.com/dotnet/runtime:10.0 as final
EXPOSE 8080
WORKDIR /app
COPY --from=build-dotnet /app/out ./bin
COPY --from=build-node /app/dist ./www

ENTRYPOINT ["/app/bin/roadnik-server"]
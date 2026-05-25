FROM dhi.io/dotnet:10-sdk-alpine3.23 AS build-dotnet
ARG TARGETARCH
WORKDIR /app
RUN apk add --no-cache clang musl-dev zlib-dev
COPY ./ ./
COPY ./src/backend/dotnet_publish.sh ./
RUN chmod +x ./dotnet_publish.sh
RUN ./dotnet_publish.sh $TARGETARCH

FROM node:alpine AS build-node
WORKDIR /app
COPY ./src/frontend/app ./
RUN npm i
RUN npm run build
COPY ./src/frontend/main-page ./dist

FROM dhi.io/dotnet:10-alpine3.23 AS final
EXPOSE 8080
WORKDIR /app
COPY --from=build-dotnet /app/out ./bin
COPY --from=build-node /app/dist ./www

ENTRYPOINT ["/app/bin/roadnik-server"]
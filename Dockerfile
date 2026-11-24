# The API host's container image. Build from the repository root:
#   docker build --tag cryptostuff-api .
# Run it with CoinGecko__ApiKey and Api__Key set and a volume at /data; see
# "Container image" in README.md. test/container-smoke.sh checks it.

# The SDK tag names global.json's pinned version exactly: the build runs with
# global.json in place, so an image whose SDK the pin rejects fails the build.
# Move this tag whenever global.json moves.
FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CryptoStuff.Api --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# A new named volume mounted here takes this directory's owner, so the
# non-root user below can create the database on a freshly created volume.
RUN mkdir /data && chown $APP_UID:$APP_UID /data
ENV ConnectionStrings__CryptoStuff="Data Source=/data/cryptostuff.db"
# The non-root `app` user the aspnet image ships.
USER $APP_UID
ENTRYPOINT ["dotnet", "CryptoStuff.Api.dll"]

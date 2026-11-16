FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Server/Domain/K7.Server.Domain.csproj           src/Server/Domain/
COPY src/Shared/K7.Shared/K7.Shared.csproj               src/Shared/K7.Shared/
COPY tools/K7.Import/K7.Import.csproj                    tools/K7.Import/
RUN dotnet restore "tools/K7.Import/K7.Import.csproj"

COPY src/Server/Domain/     src/Server/Domain/
COPY src/Shared/K7.Shared/  src/Shared/K7.Shared/
COPY tools/K7.Import/       tools/K7.Import/

ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=0.0.0
RUN dotnet publish "tools/K7.Import/K7.Import.csproj" \
    -c $BUILD_CONFIGURATION -o /publish --no-restore \
    -p:Version=${APP_VERSION}


FROM mcr.microsoft.com/dotnet/runtime:10.0-noble@sha256:80a51954ac70925e5783cb1ed35129a4243dcb1cddef20066b67346e17db284d AS final
WORKDIR /app
COPY --from=build /publish .
LABEL org.opencontainers.image.source="https://github.com/kaybi-gh/K7"
LABEL org.opencontainers.image.description="K7 one-shot import CLI (Plex, Jellyfin, Tautulli, Tracearr, Spotify)"
ENTRYPOINT ["./k7-import"]

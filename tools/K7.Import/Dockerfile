FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS build
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


FROM mcr.microsoft.com/dotnet/runtime:10.0-noble@sha256:8a153b5889d796b6450295b383596b13308c24c230515f8a7770ce1b94e0c460 AS final
WORKDIR /app
COPY --from=build /publish .
LABEL org.opencontainers.image.source="https://github.com/kaybi-gh/K7"
LABEL org.opencontainers.image.description="K7 one-shot import CLI (Plex, Jellyfin, Tautulli, Tracearr, Spotify)"
ENTRYPOINT ["./k7-import"]

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
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


FROM mcr.microsoft.com/dotnet/runtime:10.0-noble@sha256:a3f9f81954b9c810af2ed18849716c8dc74a4707bd0907fdbd69be81554c0f30 AS final
WORKDIR /app
COPY --from=build /publish .
LABEL org.opencontainers.image.source="https://github.com/kaybi-gh/K7"
LABEL org.opencontainers.image.description="K7 one-shot import CLI (Plex, Jellyfin, Tautulli, Tracearr, Spotify)"
ENTRYPOINT ["./k7-import"]

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
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


FROM mcr.microsoft.com/dotnet/runtime:10.0-noble@sha256:b89586dc17781f25531909993658aa8161205ae38b8cec8847df4a8221a403d5 AS final
WORKDIR /app
COPY --from=build /publish .
LABEL org.opencontainers.image.source="https://github.com/kaybi-gh/K7"
LABEL org.opencontainers.image.description="K7 one-shot import CLI (Plex, Jellyfin, Tautulli, Tracearr, Spotify)"
ENTRYPOINT ["./k7-import"]

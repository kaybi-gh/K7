FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS build
WORKDIR /src

RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Server/Web/K7.Server.Web.csproj                                    src/Server/Web/
COPY src/Server/Application/K7.Server.Application.csproj                     src/Server/Application/
COPY src/Server/Domain/K7.Server.Domain.csproj                               src/Server/Domain/
COPY src/Server/Infrastructure/Database/Context/*.csproj                     src/Server/Infrastructure/Database/Context/
COPY src/Server/Infrastructure/Database/Providers/Postgres/*.csproj          src/Server/Infrastructure/Database/Providers/Postgres/
COPY src/Server/Infrastructure/Database/Providers/Sqlite/*.csproj            src/Server/Infrastructure/Database/Providers/Sqlite/
COPY src/Server/Infrastructure/Configuration/*.csproj                        src/Server/Infrastructure/Configuration/
COPY src/Server/Infrastructure/ExternalServices/*.csproj                     src/Server/Infrastructure/ExternalServices/
COPY src/Server/Infrastructure/FileSystem/*.csproj                           src/Server/Infrastructure/FileSystem/
COPY src/Server/Infrastructure/MediaProcessing/*.csproj                      src/Server/Infrastructure/MediaProcessing/
COPY src/Shared/Aspire/ServiceDefaults/*.csproj                              src/Shared/Aspire/ServiceDefaults/
COPY src/Shared/K7.Shared/*.csproj                                           src/Shared/K7.Shared/
COPY src/Clients/Shared/K7.Clients.Shared.csproj                             src/Clients/Shared/
COPY src/Clients/Shared/UI/K7.Clients.Shared.UI.csproj                       src/Clients/Shared/UI/
COPY src/Clients/Web/*.csproj                                                src/Clients/Web/
RUN dotnet restore "src/Server/Web/K7.Server.Web.csproj"

COPY . .
RUN find . -name "libman.json" -execdir libman restore \;

ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=0.0.0
RUN dotnet publish "src/Server/Web/K7.Server.Web.csproj" \
    -c $BUILD_CONFIGURATION -o /publish --no-restore \
    -p:Version=${APP_VERSION}


# VS Fast Mode debug stage (F5 in Visual Studio with Docker profile)
FROM build AS dev
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080 8081


FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*


FROM runtime AS final
RUN groupadd -g 911 appgroup && useradd -u 911 -g appgroup -m appuser
WORKDIR /k7
COPY --from=build /publish .
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["/entrypoint.sh"]

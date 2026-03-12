FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
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
COPY src/Server/Infrastructure/FileSystem/*.csproj                           src/Server/Infrastructure/FileSystem/
COPY src/Server/Infrastructure/MediaProcessing/*.csproj                      src/Server/Infrastructure/MediaProcessing/
COPY src/Shared/Aspire/ServiceDefaults/*.csproj                              src/Shared/Aspire/ServiceDefaults/
COPY src/Shared/K7.Shared/*.csproj                                           src/Shared/K7.Shared/
COPY src/Clients/Shared/Models/*.csproj                                      src/Clients/Shared/Models/
COPY src/Clients/Shared/Components/*.csproj                                  src/Clients/Shared/Components/
COPY src/Clients/Shared/Pages/*.csproj                                       src/Clients/Shared/Pages/
COPY src/Clients/Shared/Services/*.csproj                                    src/Clients/Shared/Services/
COPY src/Clients/Web/*.csproj                                                src/Clients/Web/
RUN dotnet restore "src/Server/Web/K7.Server.Web.csproj"

COPY . .
RUN find . -name "libman.json" -execdir libman restore \;

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Server/Web/K7.Server.Web.csproj" \
    -c $BUILD_CONFIGURATION -o /publish --no-restore


# VS Fast Mode debug stage (F5 in Visual Studio with Docker profile)
FROM build AS dev
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg software-properties-common \
    && add-apt-repository -y ppa:mtg/essentia \
    && apt-get update \
    && apt-get install -y --no-install-recommends essentia-extractors \
    && apt-get purge -y software-properties-common && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*


FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu ffmpeg software-properties-common \
    && add-apt-repository -y ppa:mtg/essentia \
    && apt-get update \
    && apt-get install -y --no-install-recommends essentia-extractors \
    && apt-get purge -y software-properties-common && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*


FROM runtime AS final
RUN groupadd -g 911 appgroup && useradd -u 911 -g appgroup -m appuser
WORKDIR /k7
COPY --from=build /publish .
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
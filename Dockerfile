### Setup build-env layer
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build-env
WORKDIR /app

# VS debugs against this stage — install runtime dependencies conditionally
ARG LAUNCHING_FROM_VS=false
RUN if [ "$LAUNCHING_FROM_VS" = "true" ]; then \
      apt-get update && \
      apt-get install -y --no-install-recommends ffmpeg software-properties-common && \
      add-apt-repository -y ppa:mtg/essentia && \
      apt-get update && \
      apt-get install -y --no-install-recommends essentia-extractors && \
      apt-get purge -y software-properties-common && \
      apt-get autoremove -y && \
      rm -rf /var/lib/apt/lists/*; \
    fi

# Install LibMan tool
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy csproj and project files first to cache restore
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Server/Web/K7.Server.Web.csproj src/Server/Web/
COPY src/Server/Application/K7.Server.Application.csproj src/Server/Application/
COPY src/Server/Domain/K7.Server.Domain.csproj src/Server/Domain/
COPY src/Server/Infrastructure/Database/Context/K7.Server.Infrastructure.Database.Context.csproj src/Server/Infrastructure/Database/Context/
COPY src/Server/Infrastructure/Database/Providers/Postgres/K7.Server.Infrastructure.Database.Providers.Postgres.csproj src/Server/Infrastructure/Database/Providers/Postgres/
COPY src/Server/Infrastructure/Database/Providers/Sqlite/K7.Server.Infrastructure.Database.Providers.Sqlite.csproj src/Server/Infrastructure/Database/Providers/Sqlite/
COPY src/Server/Infrastructure/Configuration/K7.Server.Infrastructure.Configuration.csproj src/Server/Infrastructure/Configuration/
COPY src/Server/Infrastructure/FileSystem/K7.Server.Infrastructure.FileSystem.csproj src/Server/Infrastructure/FileSystem/
COPY src/Server/Infrastructure/MediaProcessing/K7.Server.Infrastructure.MediaProcessing.csproj src/Server/Infrastructure/MediaProcessing/
COPY src/Shared/Aspire/ServiceDefaults/K7.Shared.Aspire.ServiceDefaults.csproj src/Shared/Aspire/ServiceDefaults/
COPY src/Shared/K7.Shared/K7.Shared.csproj src/Shared/K7.Shared/
COPY src/Clients/Shared/Models/K7.Clients.Shared.Domain.csproj src/Clients/Shared/Models/
COPY src/Clients/Shared/Components/K7.Clients.Shared.Components.csproj src/Clients/Shared/Components/
COPY src/Clients/Shared/Pages/K7.Clients.Shared.Pages.csproj src/Clients/Shared/Pages/
COPY src/Clients/Shared/Services/K7.Clients.Shared.Services.csproj src/Clients/Shared/Services/
COPY src/Clients/Web/K7.Clients.Web.csproj src/Clients/Web/

# Restore dependencies
RUN dotnet restore "src/Server/Web/K7.Server.Web.csproj"

# Now copy the rest of the source code
COPY . .

# Build
ARG BUILD_CONFIGURATION=Release
# Restore Libman libs
RUN find . -name "libman.json" -execdir libman restore \;
RUN dotnet publish "src/Server/Web/K7.Server.Web.csproj" -c $BUILD_CONFIGURATION -o /k7 --no-restore

### Setup runtime layer
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble

# Install dependencies
RUN apt-get update && \
    apt-get install -y gosu ffmpeg software-properties-common && \
    add-apt-repository -y ppa:mtg/essentia && \
    apt-get update && \
    apt-get install -y essentia-extractors && \
    apt-get purge -y software-properties-common && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

# Add default user
RUN groupadd -g 911 appgroup && useradd -u 911 -g appgroup -m appuser

# Define working directory
WORKDIR /k7

# Copy binaries to working directory
COPY --from=build-env /k7 .

# Copy and define entrypoint
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
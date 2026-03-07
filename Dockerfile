### Setup build-env layer
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build-env
WORKDIR /app

# Install LibMan tool
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"

COPY . ./

# Build
RUN dotnet restore "./src/Server/Web/K7.Server.Web.csproj"
# On cherche libman.json partout dans l'arborescence
RUN find . -name "libman.json" -execdir libman restore \;
RUN dotnet publish "./src/Server/Web/K7.Server.Web.csproj" -c Release -o /k7 --no-restore

### Setup runtime layer
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble

# Install dependencies
RUN apt-get update && \
    apt-get install -y gosu ffmpeg && \
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
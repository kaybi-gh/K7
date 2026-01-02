### Setup build-env layer
FROM mcr.microsoft.com/dotnet/sdk:10.0-bookworm-slim AS build-env
WORKDIR /app
COPY . ./

# Build
RUN dotnet restore "./src/Server/Web/K7.Server.Web.csproj"
RUN dotnet publish "./src/Server/Web/K7.Server.Web.csproj" -c Release -o /k7 --no-restore

### Setup runtime layer
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim

# Install dependencies
RUN echo "deb http://deb.debian.org/debian/ bookworm main" > /etc/apt/sources.list.d/debian-stable.list && \
    echo "deb http://deb.debian.org/debian/ trixie main" > /etc/apt/sources.list.d/debian-testing.list && \
    apt-get update && \
    apt-get install -y gosu -t bookworm && \
    apt-get install -y -t trixie ffmpeg && \
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
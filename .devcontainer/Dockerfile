FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

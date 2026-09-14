FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

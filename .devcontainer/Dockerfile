FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

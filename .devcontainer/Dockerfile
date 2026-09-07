FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

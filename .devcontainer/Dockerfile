FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:2b1e13a22e46b55117f9cdb4b38e73172de08a30370166c8227d06cdaff82473

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

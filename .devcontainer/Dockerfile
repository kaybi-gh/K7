FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl git ca-certificates \
    && curl -fsSL https://essentia.upf.edu/extractors/essentia-extractors-v2.1_beta2-linux-x86_64.tar.gz \
       | tar xz --strip-components=1 --wildcards '*/streaming_extractor_music' -C /usr/local/bin/ \
    && mv /usr/local/bin/streaming_extractor_music /usr/local/bin/essentia_streaming_extractor_music \
    && chmod +x /usr/local/bin/essentia_streaming_extractor_music \
    && rm -rf /var/lib/apt/lists/*

# Install LibMan CLI & EF Core tools
RUN dotnet tool install --global Microsoft.Web.LibraryManager.Cli \
    && dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

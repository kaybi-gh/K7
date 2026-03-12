FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg software-properties-common && \
    add-apt-repository -y ppa:mtg/essentia && \
    apt-get update && \
    apt-get install -y --no-install-recommends essentia-extractors && \
    apt-get purge -y software-properties-common && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*

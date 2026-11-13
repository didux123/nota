# SPDX-License-Identifier: AGPL-3.0-only
# Nota headless (fork addition): engine + MCP over HTTP, no window, no audio device.
# Build from the repository root:
#   docker build -f docker/headless/Dockerfile -t nota-headless .
# Run:
#   docker run -d -p 3900:3900 -e NOTA_HEADLESS_TOKEN=... -v nota-data:/var/lib/nota nota-headless

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Same packages as scripts/build-linux.sh (JUCE wants the X11/ALSA headers even with no window).
RUN apt-get update \
 && apt-get install -y --no-install-recommends cmake ninja-build build-essential git ca-certificates \
    libasound2-dev libx11-dev libxext-dev libxrandr-dev libxinerama-dev libxcursor-dev \
    libfreetype6-dev libfontconfig1-dev \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
ENV CPM_SOURCE_CACHE=/cpm
RUN --mount=type=cache,target=/cpm \
    cmake -G Ninja -S src/native/nota.engine -B src/native/nota.engine/build \
      -DCMAKE_BUILD_TYPE=Release -DNOTA_BUILD_RENDER_PROBE=OFF \
 && cmake --build src/native/nota.engine/build --target nota_engine nota-scanworker
RUN dotnet publish src/managed/Nota.Headless -c Release -o /out --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
 && (apt-get install -y --no-install-recommends libasound2t64 || apt-get install -y --no-install-recommends libasound2) \
 && apt-get install -y --no-install-recommends libx11-6 libxext6 libxrandr2 libxinerama1 libxcursor1 \
    libfreetype6 libfontconfig1 ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /out /app
WORKDIR /app
ENV NOTA_DATA_DIR=/var/lib/nota \
    NOTA_HEADLESS_HOST=0.0.0.0 \
    NOTA_HEADLESS_PORT=3900
VOLUME ["/var/lib/nota"]
EXPOSE 3900
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s CMD curl -fs http://127.0.0.1:3900/health || exit 1
ENTRYPOINT ["dotnet", "/app/Nota.Headless.dll"]

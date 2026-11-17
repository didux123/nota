# Nota headless + Surge XT (VST3, GPL-3) et l'outil scan_plugins, sans recompiler le moteur.
FROM nota-headless:latest
USER root
RUN apt-get update \
 && curl -fsSL -o /tmp/surge.deb https://github.com/surge-synthesizer/releases-xt/releases/download/1.3.4/surge-xt-linux-x64-1.3.4.deb \
 && apt-get install -y --no-install-recommends /tmp/surge.deb \
 && rm -f /tmp/surge.deb && rm -rf /var/lib/apt/lists/*
COPY Nota.Headless.dll /app/Nota.Headless.dll

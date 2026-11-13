<!-- SPDX-License-Identifier: AGPL-3.0-only -->
# Nota headless (fork addition)

Runs Nota's engine without a window or an audio device and serves Nota's own MCP tools over
HTTP, so another program can build a song (tracks, instruments, MIDI clips, automation) and
render it offline with `export_master` / `export_stems`.

- Code: `src/managed/Nota.Headless` (no change to upstream files, so the fork stays easy to rebase).
- Extra tools: `reset_project` (empty project + tempo/signature), `headless_status`.
- Endpoints: `POST /mcp` (MCP Streamable HTTP, stateless), `GET /health`.
- Environment: `NOTA_HEADLESS_HOST` (default `0.0.0.0`), `NOTA_HEADLESS_PORT` (3900),
  `NOTA_HEADLESS_TOKEN` (bearer token, recommended), `NOTA_DATA_DIR`.

```bash
docker build -f docker/headless/Dockerfile -t nota-headless .
docker run -d --name nota-headless -p 3900:3900 -e NOTA_HEADLESS_TOKEN=change-me nota-headless
curl -s localhost:3900/health
```

Exported files are written inside the container: mount a volume where you export.
Bind to a private network only — the MCP tools can write files where the process can.

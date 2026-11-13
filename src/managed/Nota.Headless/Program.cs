// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Nota headless fork contributors. Based on Nota by Egor Khindikaynen; see LICENSES/.
//
// Nota without a window: the engine runs with no audio device (offline export works without one),
// and Nota's own MCP tools are served over HTTP so another program can build a song and render it.
//
// Environment:
//   NOTA_HEADLESS_HOST   address to bind (default 0.0.0.0 — meant for a private Docker network)
//   NOTA_HEADLESS_PORT   port (default 3900, Nota's usual MCP port)
//   NOTA_HEADLESS_TOKEN  if set, every request but /health needs "Authorization: Bearer <token>"
//   NOTA_DATA_DIR        Nota's data folder (settings, presets, kits cache)
//
// Endpoints: POST /mcp (MCP Streamable HTTP, stateless), GET /health.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nota.Application;
using Nota.Headless;
using Nota.Infrastructure;
using Nota.Mcp;

static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

var host = Env("NOTA_HEADLESS_HOST", "0.0.0.0");
var port = int.Parse(Env("NOTA_HEADLESS_PORT", "3900"));
var token = Env("NOTA_HEADLESS_TOKEN", "");

using var thread = new EngineThread(pumpPluginHost: OperatingSystem.IsLinux());
var engine = await thread.InvokeAsync(() => new NotaEngine());   // the engine thread owns it, like Nota.App's UI thread
Console.WriteLine($"[nota-headless] engine {NotaEngine.Version} ready (no audio device; offline render only)");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Parse(host), port));

// What Nota.App shares with the MCP tools (see Nota.App/Mcp/McpAppServices.cs), minus the UI-bound parts.
var s = builder.Services;
s.AddSingleton(engine);
s.AddSingleton<IAudioEngine>(engine);
s.AddSingleton<IEngineDispatch>(thread);
s.AddSingleton<IArrangementRefresh, NoRefresh>();
s.AddSingleton<ILogSink, LogSink>();
s.AddSingleton<ISettingsService, SettingsService>();
s.AddSingleton<IPluginCatalog, PluginCatalog>();
s.AddSingleton<IPluginStore, PluginStore>();
s.AddSingleton<IFactoryPresets, FactoryPresetCatalog>();
s.AddSingleton<IDrumKits, DrumKitService>();
s.AddSingleton<IPresetStore, PresetStore>();
s.AddSingleton<IPresetLibrary, PresetLibrary>();
s.AddSingleton<IAudioExporter, AudioExporter>();

s.AddMcpServer(o => { o.ServerInfo = new() { Name = "Nota (headless)", Version = "1" }; })
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly(typeof(NotaMcpServer).Assembly)   // Nota's own tools
    .WithToolsFromAssembly(typeof(HeadlessTools).Assembly);  // reset_project, headless_status

var app = builder.Build();

if (token.Length > 0)
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path != "/health" && ctx.Request.Headers.Authorization != $"Bearer {token}")
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next();
    });
}

app.MapGet("/health", () => Results.Json(new { ok = true, engine = NotaEngine.Version, tracks = engine.TrackCount }));
app.MapMcp("/mcp");

Console.WriteLine($"[nota-headless] MCP on http://{host}:{port}/mcp{(token.Length > 0 ? " (token required)" : "")}");
await app.RunAsync();
await thread.InvokeAsync(() => engine.Dispose());

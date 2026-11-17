// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Nota headless fork contributors. Based on Nota by Egor Khindikaynen; see LICENSES/.
//
// MCP tools that only make sense without a window: start every song from an empty project,
// and report the engine state.

using System.ComponentModel;
using ModelContextProtocol.Server;
using Nota.Application;
using Nota.Infrastructure;
using Nota.Mcp;

namespace Nota.Headless;

[McpServerToolType]
public sealed class HeadlessTools(IAudioEngine engine, IEngineDispatch dispatch, IPluginCatalog catalog)
{
    [McpServerTool(Name = "reset_project"), Description(
        "Headless only: clear the session to an empty project (like File → New), then set the tempo "
        + "and time signature. Call before building each new song.")]
    public Task<string> ResetProject(double bpm = 120, int numerator = 4, int denominator = 4) => dispatch.InvokeAsync(() =>
    {
        engine.StopTransport();
        engine.Reset();
        engine.SetBpm(Math.Clamp(bpm, 20, 300));
        engine.SetTimeSignature(numerator, denominator);
        return $"empty project at {bpm} BPM, {numerator}/{denominator}";
    });

    [McpServerTool(Name = "scan_plugins"), Description(
        "Headless only: scan VST3 plugins (out of process, with nota-scanworker) and return the catalog. "
        + "dir = a folder to add to the scan paths and search (default /usr/lib/vst3); retryFailed probes failed ones again.")]
    public Task<string[]> ScanPlugins(string dir = "/usr/lib/vst3", bool retryFailed = true) => Task.Run(() =>
    {
        bool known = false;
        for (int i = 0; i < catalog.ScanPathCount; i++) known |= catalog.ScanPath(i) == dir;
        if (!known && Directory.Exists(dir)) catalog.AddScanPath(dir);
        string worker = Path.Combine(AppContext.BaseDirectory, "nota-scanworker");
        catalog.Scan(worker, Directory.Exists(dir) ? dir : null, retryFailed);
        var list = new string[catalog.Count];
        for (int i = 0; i < list.Length; i++) list[i] = $"{i} | {catalog.Description(i)} | {catalog.Id(i)}";
        return list;
    });

    [McpServerTool(Name = "headless_status"), Description("Headless only: engine version, sample rate and track count.")]
    public Task<string> Status() => dispatch.InvokeAsync(() =>
        $"engine {NotaEngine.Version}, {engine.SampleRate} Hz, {engine.TrackCount} tracks, tempo {engine.Bpm}");
}

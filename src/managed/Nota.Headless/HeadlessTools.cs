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
public sealed class HeadlessTools(IAudioEngine engine, IEngineDispatch dispatch)
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

    [McpServerTool(Name = "headless_status"), Description("Headless only: engine version, sample rate and track count.")]
    public Task<string> Status() => dispatch.InvokeAsync(() =>
        $"engine {NotaEngine.Version}, {engine.SampleRate} Hz, {engine.TrackCount} tracks, tempo {engine.Bpm}");
}

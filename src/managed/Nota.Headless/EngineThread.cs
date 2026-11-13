// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Nota headless fork contributors. Based on Nota by Egor Khindikaynen; see LICENSES/.
//
// Stands in for the UI thread of Nota.App: one dedicated thread owns the engine, every MCP call is
// marshalled onto it (IEngineDispatch), and between calls it services JUCE's plugin message loop on
// Linux, which Nota.App does with an Avalonia timer (see App.axaml.cs, "PumpPacer").

using System.Collections.Concurrent;
using Nota.Infrastructure;
using Nota.Mcp;

namespace Nota.Headless;

public sealed class EngineThread : IEngineDispatch, IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly bool _pump;

    public EngineThread(bool pumpPluginHost)
    {
        _pump = pumpPluginHost;
        _thread = new Thread(Run) { IsBackground = true, Name = "nota-engine" };
        _thread.Start();
    }

    private void Run()
    {
        var idle = TimeSpan.FromMilliseconds(10);
        while (!_queue.IsCompleted)
        {
            if (_queue.TryTake(out var work, idle))
            {
                work();
                continue;
            }
            if (_pump)
            {
                // 10 ms while plugins post messages, 100 ms once the queue stays empty (as in Nota.App).
                try { idle = NotaEngine.PumpPluginHost() > 0 ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromMilliseconds(100); }
                catch { idle = TimeSpan.FromMilliseconds(100); }
            }
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (Thread.CurrentThread == _thread) return Task.FromResult(func());
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Action action) => InvokeAsync(() => { action(); return true; });

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
    }
}

/// <summary>No window to redraw.</summary>
public sealed class NoRefresh : IArrangementRefresh
{
    public void Refresh() { }
}

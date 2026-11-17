// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Nota headless fork contributors. Based on Nota by Egor Khindikaynen; see LICENSES/.
//
// Surge XT (hosted VST3) factory patches without a window: list the .fxp patches installed with the
// plugin and load one into a track's Surge instance. The VST3 state JUCE hands us is
// "VC2!" + length + XML whose <IComponent> holds the plugin's own chunk in juce::MemoryBlock base64;
// Surge's chunk is exactly the .fxp file after its 60-byte FXP header (it starts with "sub3").

using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Nota.Application;
using Nota.Mcp;

namespace Nota.Headless;

[McpServerToolType]
public sealed class SurgeTools(IAudioEngine engine, IEngineDispatch dispatch)
{
    private static readonly string Root = Environment.GetEnvironmentVariable("SURGE_PATCH_DIR") ?? "/usr/share/surge-xt";
    private const string Chars = ".ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+";

    [McpServerTool(Name = "surge_patches"), Description(
        "Headless only: list Surge XT factory / third-party patches as \"Category/Name\" (folder = patches_factory or patches_3rdparty). "
        + "filter = optional case-insensitive substring.")]
    public Task<string[]> Patches(string folder = "patches_factory", string filter = "") => Task.Run(() =>
    {
        string dir = Path.Combine(Root, Path.GetFileName(folder));
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.EnumerateFiles(dir, "*.fxp", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(dir, p)[..^4].Replace('\\', '/'))
            .Where(p => filter.Length == 0 || p.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
    });

    [McpServerTool(Name = "load_surge_patch"), Description(
        "Headless only: load a Surge XT patch (\"Category/Name\" from surge_patches) into the Surge instance of a track "
        + "(deviceIndex -1 = the track instrument, else an insert). Returns the patch name read back.")]
    public Task<string> Load(int trackId, string patch, int deviceIndex = -1, string folder = "patches_factory") => dispatch.InvokeAsync(() =>
    {
        string baseDir = Path.GetFullPath(Path.Combine(Root, Path.GetFileName(folder)));
        string file = Path.GetFullPath(Path.Combine(baseDir, patch + ".fxp"));
        if (!file.StartsWith(baseDir + Path.DirectorySeparatorChar) || !File.Exists(file))
            throw new ArgumentException($"unknown Surge patch: {patch}");
        byte[] fxp = File.ReadAllBytes(file);
        if (fxp.Length <= 64 || Encoding.ASCII.GetString(fxp, 60, 4) != "sub3") throw new InvalidDataException("not a Surge .fxp patch");
        byte[] chunk = fxp[60..];

        byte[] state = engine.GetPluginState(trackId, deviceIndex) ?? throw new InvalidOperationException("no plugin on that track/device");
        if (state.Length < 8 || Encoding.ASCII.GetString(state, 0, 4) != "VC2!") throw new InvalidDataException("unexpected plugin state");
        int len = BitConverter.ToInt32(state, 4);
        string xml = Encoding.UTF8.GetString(state, 8, Math.Min(len, state.Length - 8)).TrimEnd('\0');
        string newXml = Regex.Replace(xml, "<IComponent>.*?</IComponent>", "<IComponent>" + Encode(chunk) + "</IComponent>", RegexOptions.Singleline);
        byte[] body = Encoding.UTF8.GetBytes(newXml + "\0");
        var outState = new byte[8 + body.Length];
        Encoding.ASCII.GetBytes("VC2!").CopyTo(outState, 0);
        BitConverter.GetBytes(body.Length).CopyTo(outState, 4);
        body.CopyTo(outState, 8);
        engine.SetPluginState(trackId, deviceIndex, outState);

        byte[] back = engine.GetPluginState(trackId, deviceIndex) ?? Array.Empty<byte>();
        var m = Regex.Match(Encoding.UTF8.GetString(back), "<IComponent>(.*?)</IComponent>", RegexOptions.Singleline);
        string inner = m.Success ? Encoding.UTF8.GetString(Decode(m.Groups[1].Value.Trim())) : "";
        var name = Regex.Match(inner, "<meta name=\"([^\"]*)\"");
        return name.Success ? name.Groups[1].Value : Path.GetFileName(patch);
    });

    /// <summary>juce::MemoryBlock::toBase64Encoding: "size." then 6 bits per char, LSB first.</summary>
    private static string Encode(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 4 / 3 + 16).Append(b.Length).Append('.');
        int bits = b.Length * 8;
        for (int i = 0; i < bits; i += 6)
        {
            int v = 0;
            for (int k = 0; k < 6; k++)
            {
                int pos = i + k;
                if (pos < bits && (b[pos >> 3] & (1 << (pos & 7))) != 0) v |= 1 << k;
            }
            sb.Append(Chars[v]);
        }
        return sb.ToString();
    }

    private static byte[] Decode(string s)
    {
        int dot = s.IndexOf('.');
        if (dot <= 0 || !int.TryParse(s[..dot], out int size)) return Array.Empty<byte>();
        var outB = new byte[size];
        int pos = 0;
        foreach (char c in s[(dot + 1)..])
        {
            int v = Chars.IndexOf(c);
            if (v < 0) continue;
            for (int k = 0; k < 6 && pos < size * 8; k++, pos++)
                if ((v & (1 << k)) != 0) outB[pos >> 3] |= (byte)(1 << (pos & 7));
        }
        return outB;
    }
}

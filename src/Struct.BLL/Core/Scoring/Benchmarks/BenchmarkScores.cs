using System.Globalization;
using System.Text.RegularExpressions;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.BLL.Core.Scoring.Benchmarks;

/// <summary>
/// PassMark benchmark scores (CPU Mark + single-thread rating / G3D Mark) keyed by normalized model name.
/// Matches catalog components by name (CPU) or by the chip in parentheses (GPU), and enriches each matched
/// component with per-category 0..1 <c>BenchmarkScore</c> / <c>SingleThreadScore</c> specs consumed by
/// <see cref="PerformanceScorer"/>. Unmatched components are left untouched and fall back to the heuristic scorer.
/// </summary>
public class BenchmarkScores
{

    private readonly Dictionary<string, double> _cpu;
    private readonly Dictionary<string, double> _cpuSingle;
    private readonly Dictionary<string, double> _gpu;
    private readonly string[] _cpuKeys; // normalized keys, longest first (for containment matching)
    private readonly string[] _cpuSingleKeys;
    private readonly string[] _gpuKeys;

    /// <param name="cpuMarks">Raw CPU name → CPU Mark.</param>
    /// <param name="gpuMarks">Raw GPU chip name → G3D Mark.</param>
    /// <param name="cpuSingleThreadMarks">Raw CPU name → single-thread rating.</param>
    /// <param name="fixedReferences">Divide by the <see cref="References"/> parts instead of the best matched part.</param>
    public BenchmarkScores(IReadOnlyDictionary<string, double> cpuMarks, IReadOnlyDictionary<string, double> gpuMarks,
        IReadOnlyDictionary<string, double>? cpuSingleThreadMarks = null, bool fixedReferences = false)
    {
        _cpu = BuildMap(cpuMarks);
        _cpuSingle = BuildMap(cpuSingleThreadMarks ?? new Dictionary<string, double>());
        _gpu = BuildMap(gpuMarks);
        _cpuKeys = _cpu.Keys.OrderByDescending(k => k.Length).ToArray();
        _cpuSingleKeys = _cpuSingle.Keys.OrderByDescending(k => k.Length).ToArray();
        _gpuKeys = _gpu.Keys.OrderByDescending(k => k.Length).ToArray();
        if (!fixedReferences) return;
        _cpuReference = ReferenceMark(_cpu, References.CpuMark);
        _cpuSingleReference = ReferenceMark(_cpuSingle, References.ThreadMark);
        _gpuReference = ReferenceMark(_gpu, References.G3dMark);
    }

    /// <summary>
    /// The part each PassMark column is divided by: the best catalog part on 26.11.2026, frozen so that a score
    /// does not move when the catalog gains or loses its top part.
    /// </summary>
    public static readonly (string CpuMark, string ThreadMark, string G3dMark) References =
        ("AMD Ryzen Threadripper PRO 9995WX", "Intel Core Ultra 9 285K", "GeForce RTX 5090");

    private readonly double? _cpuReference;
    private readonly double? _cpuSingleReference;
    private readonly double? _gpuReference;

    private static double ReferenceMark(Dictionary<string, double> map, string name) =>
        map.TryGetValue(Normalize(name), out var mark) && mark > 0
            ? mark
            : throw new InvalidOperationException($"Benchmark reference part '{name}' is missing from the PassMark snapshot.");

    private static Dictionary<string, double> BuildMap(IReadOnlyDictionary<string, double> raw)
    {
        var map = new Dictionary<string, double>();
        foreach (var (name, mark) in raw)
            map[Normalize(name)] = mark; // last write wins on normalization collisions
        return map;
    }

    /// <summary>lowercase, drop "@ x.xxGHz" clock suffix, strip (R)/(TM), collapse non-alphanumerics.</summary>
    public static string Normalize(string s)
    {
        s = s.ToLowerInvariant();
        int at = s.IndexOf('@');
        if (at >= 0) s = s[..at];
        s = s.Replace("(r)", " ").Replace("(tm)", " ").Replace("™", " ").Replace("®", " ");
        s = Regex.Replace(s, "[^a-z0-9]+", " ");
        return Regex.Replace(s, "\\s+", " ").Trim();
    }

    /// <summary>Returns the text inside the first parentheses (the GPU chip), else the whole name.</summary>
    public static string ExtractGpuChip(string name)
    {
        var m = Regex.Match(name, "\\(([^)]*)\\)");
        return m.Success ? m.Groups[1].Value : name;
    }

    /// <summary>
    /// Writes a per-category 0..1 <see cref="Spec.BenchmarkScore"/> (raw mark ÷ the reference part's mark, or ÷ the best
    /// matched mark in that category when no references are fixed)
    /// onto every matched CPU/GPU component. Components with no match are left unchanged.
    /// </summary>
    public void Enrich(IReadOnlyList<Component> components)
    {
        EnrichCategory(components, Category.Cpu, Spec.BenchmarkScore, _cpuReference, c => MatchMark(_cpu, _cpuKeys, c.Name));
        EnrichCategory(components, Category.Cpu, Spec.SingleThreadScore, _cpuSingleReference, c => MatchMark(_cpuSingle, _cpuSingleKeys, c.Name));
        EnrichCategory(components, Category.Gpu, Spec.BenchmarkScore, _gpuReference, c =>
        {
            // New catalogs carry the chip in the Chipset spec (names are full board-partner strings
            // with no parentheses); older catalogs put it in parentheses in the name. Prefer the spec.
            if (c.TechnicalSpecs.TryGetValue(Spec.Chipset, out var chip) && !string.IsNullOrWhiteSpace(chip))
            {
                var bySpec = MatchMark(_gpu, _gpuKeys, chip);
                if (bySpec.HasValue) return bySpec;
            }
            return MatchMark(_gpu, _gpuKeys, ExtractGpuChip(c.Name));
        });
    }

    /// <summary>
    /// Looks a model up by exact normalized name, then by the longest benchmark key that appears as a
    /// whole-token substring of the query. Containment handles catalog names that carry extra marketing
    /// text, e.g. "AMD Ryzen 7 7800X3D 4.2 GHz 8-Core Processor" → key "AMD Ryzen 7 7800X3D".
    /// </summary>
    private static double? MatchMark(Dictionary<string, double> map, string[] keysByLengthDesc, string query)
    {
        var q = Normalize(query);
        if (q.Length == 0) return null;
        if (map.TryGetValue(q, out var exact)) return exact;

        var padded = " " + q + " ";
        foreach (var key in keysByLengthDesc)
            if (padded.Contains(" " + key + " "))
                return map[key];
        return null;
    }

    private static void EnrichCategory(IReadOnlyList<Component> components, Category category, string spec,
        double? reference, Func<Component, double?> lookup)
    {
        var matched = new List<(Component component, double mark)>();
        foreach (var c in components.Where(c => c.Category == category))
        {
            var mark = lookup(c);
            if (mark.HasValue) matched.Add((c, mark.Value));
        }
        if (matched.Count == 0) return;

        double max = reference ?? matched.Max(m => m.mark);
        if (max <= 0) return;

        // A part above the reference caps at 1: the scale stays fixed until the reference is moved on purpose.
        foreach (var (component, mark) in matched)
            component.TechnicalSpecs[spec] = Math.Min(mark / max, 1.0).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Loads the two PassMark CSVs (cpuName,cpuMark,threadMark / gpuName,G3Dmark).</summary>
    public static BenchmarkScores FromCsvFiles(string cpuCsvPath, string gpuCsvPath)
    {
        return new BenchmarkScores(
            ReadCsv(cpuCsvPath, "cpuName", "cpuMark"),
            ReadCsv(gpuCsvPath, "gpuName", "G3Dmark"),
            ReadCsv(cpuCsvPath, "cpuName", "threadMark"),
            fixedReferences: true);
    }

    private static Dictionary<string, double> ReadCsv(string path, string nameColumn, string markColumn)
    {
        var result = new Dictionary<string, double>();
        if (!File.Exists(path)) return result;

        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header == null) return result;

        var columns = SplitCsv(header.TrimStart('﻿'));
        int nameIdx = Array.FindIndex(columns, h => h.Equals(nameColumn, StringComparison.OrdinalIgnoreCase));
        int markIdx = Array.FindIndex(columns, h => h.Equals(markColumn, StringComparison.OrdinalIgnoreCase));
        if (nameIdx < 0 || markIdx < 0) return result;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = SplitCsv(line);
            if (fields.Length <= Math.Max(nameIdx, markIdx)) continue;
            if (double.TryParse(fields[markIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var mark) && mark > 0)
                result[fields[nameIdx]] = mark;
        }
        return result;
    }

    // Minimal CSV split that respects double-quoted fields (values may contain commas).
    private static string[] SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (ch == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

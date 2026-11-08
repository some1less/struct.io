using System.Text.Json.Nodes;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.BLL.Core.Scoring.Benchmarks;
using Struct.DAL.Context;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding;

public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IComponentParser> _parsers;
    private readonly PriceBoundsSettings _priceBounds;
    private readonly ILogger<DatabaseSeeder> _logger;
    private const int BatchSize = 1000;

    public DatabaseSeeder(AppDbContext context, IEnumerable<IComponentParser> parsers, PriceBoundsSettings priceBounds,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _parsers = parsers;
        _priceBounds = priceBounds;
        _logger = logger;
    }

    public async Task SeedFromDirectoryAsync(string baseDirectoryPath, BenchmarkScores? benchmarks = null)
    {
        // Never delete here: BuildComponent → Component cascades, so a reseed would wipe every SavedBuild.
        if (_context.Components.Any()) return;

        var componentsToAdd = ParseCatalog(await File.ReadAllTextAsync(baseDirectoryPath));

        /* filter duplicates */
        var uniqueComponents = componentsToAdd
            .DistinctBy(c => c.Name)
            .ToList();

        /* enrich CPU/GPU with real PassMark scores (heuristic stays as fallback for unmatched parts) */
        benchmarks?.Enrich(uniqueComponents);

        /* save in batches to prevent database lockups */
        for (int i = 0; i < uniqueComponents.Count; i += BatchSize)
        {
            var batch = uniqueComponents.Skip(i).Take(BatchSize).ToList();
            await _context.Components.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Seeded {Count} components.", uniqueComponents.Count);
    }

    public List<Component> ParseCatalog(string json)
    {
        var rootArray = JsonNode.Parse(json)?.AsArray()
            ?? throw new InvalidDataException("Seed catalog must be a JSON array.");
        var parsersByCategory = _parsers.ToDictionary(p => p.TargetFolderName, StringComparer.OrdinalIgnoreCase);
        var components = new List<Component>();

        foreach (var root in rootArray)
        {
            var categoryStr = root?["Category"]?.ToString();
            if (categoryStr == null) continue;

            // Map "Cpu" to "CPU", "Gpu" to "GPU", "Psu" to "PSU" to match TargetFolderName
            if (categoryStr.Equals("Cpu", StringComparison.OrdinalIgnoreCase)) categoryStr = "CPU";
            else if (categoryStr.Equals("Gpu", StringComparison.OrdinalIgnoreCase)) categoryStr = "GPU";
            else if (categoryStr.Equals("Psu", StringComparison.OrdinalIgnoreCase)) categoryStr = "PSU";
            else if (categoryStr.Equals("Case", StringComparison.OrdinalIgnoreCase)) categoryStr = "PCCase";
            else if (categoryStr.Equals("Cooler", StringComparison.OrdinalIgnoreCase)) categoryStr = "CPUCooler";
            else if (categoryStr.Equals("Ssd", StringComparison.OrdinalIgnoreCase)) categoryStr = "Storage";
            else if (categoryStr.Equals("Hdd", StringComparison.OrdinalIgnoreCase)) categoryStr = "Storage";

            if (parsersByCategory.TryGetValue(categoryStr, out var parser))
                components.Add(ParseComponent(root!, parser));
        }

        return components;
    }

    private Component ParseComponent(JsonNode root, IComponentParser parser)
    {
        var name = root["Name"]?.ToString() ?? "Unknown";
        var brand = root["Brand"]?.ToString() ?? "Unknown";

        if (brand.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            brand = InferBrandFromName(name);
        }

        var actualCategory = parser.DetermineCategory(root);
        var specs = parser.ParseSpecs(root);

        if (actualCategory == Category.Cpu)
        {
            InferCpuMemoryType(specs);
        }

        // Most-trusted price first: real PLN, then PCPartPicker USD × 4 (US list price → PL retail incl. VAT;
        // market effects such as the 2026 DRAM surge are real, not corrected), then the spec heuristic only
        // when no real price exists. Clamp to per-category sanity bounds.
        const decimal UsdToPln = 4.0m;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var anyNum = System.Globalization.NumberStyles.Any;

        decimal pricePln;
        if (root["PricePln"]?.ToString() is string plnStr && decimal.TryParse(plnStr, anyNum, inv, out var realPln) && realPln > 0)
        {
            pricePln = realPln;
        }
        else if (root["Price"]?.ToString() is string usdStr && decimal.TryParse(usdStr, anyNum, inv, out var usd) && usd > 0)
        {
            pricePln = usd * UsdToPln;
        }
        else
        {
            pricePln = ComponentPriceCalculator.CalculatePricePLN(actualCategory.ToString(), brand, name, specs, _logger);
        }

        var bounds = _priceBounds.PerCategory.GetValueOrDefault(actualCategory, _priceBounds.Fallback);
        pricePln = Math.Clamp(pricePln, bounds.Min, bounds.Max);

        return new Component
        {
            Name = name,
            Category = actualCategory,
            Brand = brand,
            TechnicalSpecs = specs,
            Price = pricePln
        };
    }

    private static string InferBrandFromName(string name)
    {
        string lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("intel") || lowerName.Contains("core i") ||
            lowerName.Contains("xeon") || lowerName.Contains("pentium") ||
            lowerName.Contains("celeron"))
            return "Intel";

        if (lowerName.Contains("amd") || lowerName.Contains("ryzen") ||
            lowerName.Contains("threadripper") || lowerName.Contains("athlon") ||
            lowerName.Contains(" a10") || lowerName.Contains(" a8"))
            return "AMD";

        return "Unknown";
    }

    private static void InferCpuMemoryType(Dictionary<string, string> specs)
    {
        if (specs.TryGetValue(Spec.MemoryType, out var memType) &&
            !string.IsNullOrEmpty(memType) &&
            !memType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return;

        if (!specs.TryGetValue(Spec.Socket, out var socket)) return;

        string s = socket.Replace(" ", "").ToUpperInvariant();

        if (s.Contains("AM5") || s.Contains("1851") || s.Contains("STR5"))
            specs[Spec.MemoryType] = "DDR5";
        else if (s.Contains("1700"))
            specs[Spec.MemoryType] = "DDR5";
        else if (s.Contains("AM4") || s.Contains("1200") || s.Contains("2066") ||
                 s.Contains("1151") || s.Contains("STR4") || s.Contains("STRX4") ||
                 s.Contains("2011-3"))
            specs[Spec.MemoryType] = "DDR4";
        else if (s.Contains("1150") || s.Contains("1155") || s.Contains("AM3") ||
                 s.Contains("FM2") || s == "LGA2011")
            specs[Spec.MemoryType] = "DDR3";
        else if (s.Contains("775") || s.Contains("AM2"))
            specs[Spec.MemoryType] = "DDR2";
    }
}
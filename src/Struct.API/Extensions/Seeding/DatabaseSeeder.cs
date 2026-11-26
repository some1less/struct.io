using System.Globalization;
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
    private readonly ILogger<DatabaseSeeder> _logger;
    private const int BatchSize = 1000;

    // Catalog categories whose parser has a different TargetFolderName (lookups ignore case).
    private static readonly Dictionary<string, string> ParserAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Case"] = "PCCase", ["Cooler"] = "CPUCooler", ["Ssd"] = "Storage", ["Hdd"] = "Storage"
    };

    public DatabaseSeeder(AppDbContext context, IEnumerable<IComponentParser> parsers, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _parsers = parsers;
        _logger = logger;
    }

    public async Task SeedFromDirectoryAsync(string baseDirectoryPath, BenchmarkScores? benchmarks = null)
    {
        // Never delete here: BuildComponent → Component cascades, so a reseed would wipe every SavedBuild.
        if (_context.Components.Any()) return;

        var uniqueComponents = PrepareCatalog(await ReadCatalogAsync(baseDirectoryPath), benchmarks);

        /* save in batches to prevent database lockups */
        for (int i = 0; i < uniqueComponents.Count; i += BatchSize)
        {
            var batch = uniqueComponents.Skip(i).Take(BatchSize).ToList();
            await _context.Components.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Seeded {Count} components.", uniqueComponents.Count);
    }

    public static async Task<string> ReadCatalogAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Seed catalog not found at '{path}'. Generate data/clean_database.json " +
                "(scraper/xkom_to_catalog.py, then scraper/excel_to_catalog.py) or point Seeding:CatalogPath at one.",
                path);
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>Parsed, de-duplicated by name, PassMark-enriched (the heuristic scores unmatched parts): what gets seeded.</summary>
    public List<Component> PrepareCatalog(string json, BenchmarkScores? benchmarks)
    {
        var components = ParseCatalog(json).DistinctBy(c => c.Name).ToList();
        // An API that starts on an empty catalog looks healthy and then fails every recommendation.
        if (components.Count == 0)
            throw new InvalidDataException("Seed catalog has no usable rows: each needs a known Category and a PricePln > 0.");
        benchmarks?.Enrich(components);
        return components;
    }

    public List<Component> ParseCatalog(string json)
    {
        var rootArray = JsonNode.Parse(json)?.AsArray()
            ?? throw new InvalidDataException("Seed catalog must be a JSON array.");
        var parsersByCategory = _parsers.ToDictionary(p => p.TargetFolderName, StringComparer.OrdinalIgnoreCase);
        var components = new List<Component>();

        foreach (var root in rootArray)
        {
            var category = root?["Category"]?.ToString();
            if (category == null) continue;

            if (parsersByCategory.TryGetValue(ParserAliases.GetValueOrDefault(category, category), out var parser)
                && ParseComponent(root!, parser) is { } component)
                components.Add(component);
        }

        return components;
    }

    private Component? ParseComponent(JsonNode root, IComponentParser parser)
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

        // Only the shop's real price is used: no conversion, estimate or clamp, so a row without one is left out.
        if (root["PricePln"]?.ToString() is not string plnStr
            || !decimal.TryParse(plnStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var pricePln)
            || pricePln <= 0)
        {
            _logger.LogWarning("Skipping {Name}: no PricePln.", name);
            return null;
        }

        return new Component
        {
            Name = name,
            Category = actualCategory,
            Brand = brand,
            TechnicalSpecs = specs,
            Price = pricePln,
            SourceUrl = root["SourceUrl"]?.ToString() is { Length: > 0 } url ? url : null
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
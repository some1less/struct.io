using System.Text.Json.Nodes;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.BLL.Core.Scoring.Benchmarks;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding;

public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IComponentParser> _parsers;
    private const int BatchSize = 1000;

    public DatabaseSeeder(AppDbContext context, IEnumerable<IComponentParser> parsers)
    {
        _context = context;
        _parsers = parsers;
    }

    public async Task SeedFromDirectoryAsync(string baseDirectoryPath, BenchmarkScores? benchmarks = null)
    {
        // Seed only when the catalog is empty. The previous wipe-and-reseed ran on every
        // startup and, because BuildComponent → Component is DeleteBehavior.Cascade, it
        // silently gutted every user's SavedBuild on each restart. Never delete here.
        if (_context.Components.Any()) return;

        var componentsToAdd = new List<Component>();
        var parsersByCategory = _parsers.ToDictionary(p => p.TargetFolderName, StringComparer.OrdinalIgnoreCase);
        int filteredCount = 0;

        if (!File.Exists(baseDirectoryPath)) return;

        try
        {
            var jsonString = await File.ReadAllTextAsync(baseDirectoryPath);
            var rootArray = JsonNode.Parse(jsonString)?.AsArray();
            if (rootArray == null) return;

            foreach (var root in rootArray)
            {
                if (root == null) continue;

                var categoryStr = root["Category"]?.ToString();
                if (categoryStr == null) continue;

                // Map "Cpu" to "CPU", "Gpu" to "GPU", "Psu" to "PSU" to match TargetFolderName
                if (categoryStr.Equals("Cpu", StringComparison.OrdinalIgnoreCase)) categoryStr = "CPU";
                else if (categoryStr.Equals("Gpu", StringComparison.OrdinalIgnoreCase)) categoryStr = "GPU";
                else if (categoryStr.Equals("Psu", StringComparison.OrdinalIgnoreCase)) categoryStr = "PSU";
                else if (categoryStr.Equals("Case", StringComparison.OrdinalIgnoreCase)) categoryStr = "PCCase";
                else if (categoryStr.Equals("Cooler", StringComparison.OrdinalIgnoreCase)) categoryStr = "CPUCooler";
                else if (categoryStr.Equals("Ssd", StringComparison.OrdinalIgnoreCase)) categoryStr = "Storage";
                else if (categoryStr.Equals("Hdd", StringComparison.OrdinalIgnoreCase)) categoryStr = "Storage";

                if (!parsersByCategory.TryGetValue(categoryStr, out var parser)) continue;

                var component = ParseComponent(root, parser);
                if (component != null)
                {
                    componentsToAdd.Add(component);
                }
                else
                {
                    filteredCount++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to read database: {ex.Message}");
            return;
        }

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

        Console.WriteLine($"[INFO] Seeded {uniqueComponents.Count} components (filtered {filteredCount} obsolete).");
    }

    private static Component? ParseComponent(JsonNode root, IComponentParser parser)
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

        decimal rawPriceUsd = 0;
        if (root["Price"]?.ToString() is string priceStr && decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrice))
        {
            rawPriceUsd = parsedPrice;
        }

        decimal pricePln = rawPriceUsd * 4.0m;

        decimal calculatedPrice = ComponentPriceCalculator.CalculatePricePLN(actualCategory.ToString(), brand, name, specs);
        
        // If PCPartPicker price is missing, or is an obvious outlier (e.g. scalper price for out of stock old hardware)
        if (pricePln <= 0 || pricePln > calculatedPrice * 1.5m || pricePln < calculatedPrice * 0.5m)
        {
            pricePln = calculatedPrice;
        }

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
        if (specs.TryGetValue("MemoryType", out var memType) &&
            !string.IsNullOrEmpty(memType) &&
            !memType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return;

        if (!specs.TryGetValue("Socket", out var socket)) return;

        string s = socket.Replace(" ", "").ToUpperInvariant();

        if (s.Contains("AM5") || s.Contains("1851") || s.Contains("STR5"))
            specs["MemoryType"] = "DDR5";
        else if (s.Contains("1700"))
            specs["MemoryType"] = "DDR5";
        else if (s.Contains("AM4") || s.Contains("1200") || s.Contains("2066") ||
                 s.Contains("1151") || s.Contains("STR4") || s.Contains("STRX4") ||
                 s.Contains("2011-3"))
            specs["MemoryType"] = "DDR4";
        else if (s.Contains("1150") || s.Contains("1155") || s.Contains("AM3") ||
                 s.Contains("FM2") || s == "LGA2011")
            specs["MemoryType"] = "DDR3";
        else if (s.Contains("775") || s.Contains("AM2"))
            specs["MemoryType"] = "DDR2";
    }
}
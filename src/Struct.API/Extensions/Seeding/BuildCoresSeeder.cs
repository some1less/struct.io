using System.Text.Json.Nodes;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding;

public class BuildCoresSeeder
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IComponentParser> _parsers;
    private const int BatchSize = 1000;

    public BuildCoresSeeder(AppDbContext context, IEnumerable<IComponentParser> parsers)
    {
        _context = context;
        _parsers = parsers;
    }

    public async Task SeedFromDirectoryAsync(string baseDirectoryPath)
    {
        if (_context.Components.Any()) return;

        var componentsToAdd = new List<Component>();
        var parsersByFolder = _parsers.ToDictionary(p => p.TargetFolderName);
        var foldersToParse = new[] { "CPU", "GPU", "Motherboard", "RAM", "PSU", "PCCase", "Storage", "CPUCooler" };

        foreach (var folder in foldersToParse)
        {
            var fullPath = Path.Combine(baseDirectoryPath, folder);
            if (!Directory.Exists(fullPath)) continue;
            if (!parsersByFolder.TryGetValue(folder, out var parser)) continue;

            var files = Directory.GetFiles(fullPath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var jsonString = await File.ReadAllTextAsync(file);
                    var root = JsonNode.Parse(jsonString);
                    if (root == null) continue;

                    var component = ParseComponent(root, parser);
                    if (component != null)
                    {
                        componentsToAdd.Add(component);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Skipping {Path.GetFileName(file)}: {ex.Message}");
                    continue;
                }
            }
        }

        /* filter duplicates */
        var uniqueComponents = componentsToAdd
            .DistinctBy(c => c.Name)
            .ToList();

        /* save in batches to prevent database lockups */
        for (int i = 0; i < uniqueComponents.Count; i += BatchSize)
        {
            var batch = uniqueComponents.Skip(i).Take(BatchSize).ToList();
            await _context.Components.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
        }

        Console.WriteLine($"[INFO] Seeded {uniqueComponents.Count} components in batches of {BatchSize}.");
    }

    private static Component? ParseComponent(JsonNode root, IComponentParser parser)
    {
        var name = root["metadata"]?["name"]?.ToString() ?? "Unknown";
        var brand = root["metadata"]?["manufacturer"]?.ToString() ?? "Unknown";

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

        var pricePln = ComponentPriceCalculator.CalculatePricePLN(actualCategory.ToString(), brand, name, specs);

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
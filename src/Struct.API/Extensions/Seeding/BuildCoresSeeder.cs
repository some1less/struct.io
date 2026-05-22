using System.Text.Json.Nodes;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding;

public class BuildCoresSeeder
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IComponentParser> _parsers;

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

        var foldersToParse = new[] { "CPU", "GPU", "Motherboard", "RAM", "PSU", "PCCase", "Storage" };

        foreach (var folder in foldersToParse)
        {
            var fullPath = Path.Combine(baseDirectoryPath, folder);
            if (!Directory.Exists(fullPath)) continue;

            if (!parsersByFolder.TryGetValue(folder, out var parser)) continue;

            var files = Directory.GetFiles(fullPath, "*.json");

            foreach (var file in files)
            {
                var jsonString = await File.ReadAllTextAsync(file);
                var root = JsonNode.Parse(jsonString);
                if (root == null) continue;

                var name = root["metadata"]?["name"]?.ToString() ?? "Unknown";
                var brand = root["metadata"]?["manufacturer"]?.ToString() ?? "Unknown";

                var actualCategory = parser.DetermineCategory(root);
                var specs = parser.ParseSpecs(root);
                
                componentsToAdd.Add(new Component
                {
                    Name = name,
                    Category = actualCategory,
                    Brand = brand,
                    TechnicalSpecs = specs
                });
            }
        }

        /* filter duplicates */
        var uniqueComponents = componentsToAdd
            .DistinctBy(c => c.Name)
            .ToList();

        await _context.Components.AddRangeAsync(uniqueComponents);
        await _context.SaveChangesAsync();
    }
}
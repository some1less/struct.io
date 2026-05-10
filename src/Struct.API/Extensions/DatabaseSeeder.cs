using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.API.Extensions;

public class DatabaseSeeder
{   
    /* Settings : context for interacting with db; logger for logs */
   public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        var seedDataPath = Path.Combine(Directory.GetCurrentDirectory(), "SeedData", "components.json");
            
        if (!File.Exists(seedDataPath))
        {
            logger.LogWarning("Seed file not found at {Path}", seedDataPath);
            return;
        }

        var jsonData = await File.ReadAllTextAsync(seedDataPath);
                
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var componentsFromJson = JsonSerializer.Deserialize<List<Component>>(jsonData, options);

        if (componentsFromJson == null || componentsFromJson.Count == 0)
        {
            logger.LogInformation("No components found in JSON.");
            return;
        }

        var uniqueComponentsFromJson = componentsFromJson
            .GroupBy(c => c.Name)
            .Select(g => g.First())
            .ToList();

        var existingComponentNames = await context.Components
            .Select(c => c.Name)
            .ToListAsync();

        var componentsToInsert = uniqueComponentsFromJson
            .Where(c => !existingComponentNames.Contains(c.Name))
            .ToList();

        if (componentsToInsert.Any())
        {
            await context.Components.AddRangeAsync(componentsToInsert);
            await context.SaveChangesAsync();

            var groupedComponents = componentsToInsert.GroupBy(c => c.Category);
            foreach (var group in groupedComponents)
            {
                logger.LogInformation("Seeded {Count} NEW items of type: {Category}", group.Count(), group.Key);
            }
            logger.LogInformation("Total NEW components seeded: {Total}", componentsToInsert.Count);
        }
        else 
        {
            logger.LogInformation("Database is already up-to-date. No new components to seed.");
        }
    }
}
using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CaseParser : IComponentParser
{
    public string TargetFolderName => "PCCase";

    public Category DetermineCategory(JsonNode root) => Category.Case;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { "FormFactor", ts?["FormFactor"]?.ToString() ?? "Unknown" },
            { "MaxGpuLength", ts?["MaxGpuLength"]?.ToString() ?? "0" },
            // Consumed by the cooler-height compatibility check. Current scraped data does not
            // include it yet (defaults to "0" => check is skipped); add it to the scraper to enable.
            { "MaxCoolerHeight", ts?["MaxCoolerHeight"]?.ToString() ?? "0" },
            { "SupportedMotherboards", ts?["SupportedMotherboards"]?.ToString() ?? "Unknown" },
            { "SidePanel", ts?["SidePanel"]?.ToString() ?? "None" }
        };
    }
}
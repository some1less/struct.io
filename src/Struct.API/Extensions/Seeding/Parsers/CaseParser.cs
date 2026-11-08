using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

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
            { Spec.FormFactor, ts?[Spec.FormFactor]?.ToString() ?? "Unknown" },
            { Spec.MaxGpuLength, ts?[Spec.MaxGpuLength]?.ToString() ?? "0" },
            // Consumed by the cooler-height compatibility check. Current scraped data does not
            // include it yet (defaults to "0" => check is skipped); add it to the scraper to enable.
            { Spec.MaxCoolerHeight, ts?[Spec.MaxCoolerHeight]?.ToString() ?? "0" },
            { Spec.SupportedMotherboards, ts?[Spec.SupportedMotherboards]?.ToString() ?? "Unknown" },
            { Spec.SidePanel, ts?[Spec.SidePanel]?.ToString() ?? "None" }
        };
    }
}
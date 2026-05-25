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
            { "SupportedMotherboards", ts?["SupportedMotherboards"]?.ToString() ?? "Unknown" },
            { "SidePanel", ts?["SidePanel"]?.ToString() ?? "None" }
        };
    }
}
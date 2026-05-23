using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CaseParser : IComponentParser
{
    public string TargetFolderName => "PCCase";

    public Category DetermineCategory(JsonNode root) => Category.Case;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var supportedMoboNode = root["supported_motherboard_form_factors"]?.AsArray();
        var supportedMobo = supportedMoboNode != null
            ? string.Join(",", supportedMoboNode.Select(x => x?.ToString()))
            : "Unknown";

        return new Dictionary<string, string>
        {
            { "FormFactor", root["form_factor"]?.ToString() ?? "Unknown" },
            { "MaxGpuLength", root["max_video_card_length"]?.ToString() ?? "0" },
            { "SupportedMotherboards", supportedMobo },
            { "SidePanel", root["side_panel"]?.ToString() ?? "None" }
        };
    }
}
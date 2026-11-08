using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class MotherboardParser : IComponentParser
{
    public string TargetFolderName => "Motherboard";

    public Category DetermineCategory(JsonNode root) => Category.Motherboard;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { Spec.Socket, ts?[Spec.Socket]?.ToString() ?? "Unknown" },
            { Spec.FormFactor, ts?[Spec.FormFactor]?.ToString() ?? "Unknown" },
            { Spec.RamType, ts?[Spec.RamType]?.ToString() ?? "Unknown" },
            { Spec.MaxRam, ts?[Spec.MaxRam]?.ToString() ?? "0" },
            { Spec.RamSlots, ts?[Spec.RamSlots]?.ToString() ?? "0" }
        };
    }
}
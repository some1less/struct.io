using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class StorageParser : IComponentParser
{
    public string TargetFolderName => "Storage";

    /* check storage_type = IMPORTANT */
    public Category DetermineCategory(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        var type = ts?[Spec.Type]?.ToString();

        if (type == "HDD")
        {
            return Category.Hdd;
        }

        return Category.Ssd;
    }

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { Spec.Capacity, ts?[Spec.Capacity]?.ToString() ?? "0" },
            { Spec.FormFactor, ts?[Spec.FormFactor]?.ToString() ?? "Unknown" },
            { Spec.Interface, ts?[Spec.Interface]?.ToString() ?? "Unknown" },
            { Spec.Type, ts?[Spec.Type]?.ToString() ?? "Unknown" }
        };
    }
}
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

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Capacity, Spec.FormFactor, Spec.Interface, Spec.Type);
}
using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class PsuParser : IComponentParser
{
    public string TargetFolderName => "PSU";

    public Category DetermineCategory(JsonNode root) => Category.Psu;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Wattage, Spec.FormFactor, Spec.Efficiency, Spec.Modular);
}
using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class RamParser : IComponentParser
{
    public string TargetFolderName => "RAM";

    public Category DetermineCategory(JsonNode root) => Category.Ram;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Type, Spec.Capacity, Spec.Speed, Spec.Modules);
}
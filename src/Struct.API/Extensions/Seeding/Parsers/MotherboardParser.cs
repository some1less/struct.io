using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class MotherboardParser : IComponentParser
{
    public string TargetFolderName => "Motherboard";

    public Category DetermineCategory(JsonNode root) => Category.Motherboard;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Socket, Spec.FormFactor, Spec.RamType, Spec.MaxRam, Spec.RamSlots);
}
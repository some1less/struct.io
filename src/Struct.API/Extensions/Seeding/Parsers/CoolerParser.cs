using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CoolerParser : IComponentParser
{
    public string TargetFolderName => "CPUCooler";

    public Category DetermineCategory(JsonNode root) => Category.Cooler;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.CpuSockets, Spec.Height, Spec.WaterCooled, Spec.RadiatorSize);
}
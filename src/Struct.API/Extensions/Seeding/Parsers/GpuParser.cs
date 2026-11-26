using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class GpuParser : IComponentParser
{
    public string TargetFolderName => "GPU";

    public Category DetermineCategory(JsonNode root) => Category.Gpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Chipset, Spec.Vram, Spec.CoreClock, Spec.Length, Spec.Tdp, Spec.Interface);
}
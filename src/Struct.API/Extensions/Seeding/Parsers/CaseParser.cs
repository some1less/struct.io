using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CaseParser : IComponentParser
{
    public string TargetFolderName => "PCCase";

    public Category DetermineCategory(JsonNode root) => Category.Case;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.FormFactor, Spec.MaxGpuLength, Spec.MaxCoolerHeight, Spec.SupportedMotherboards, Spec.SidePanel);
}
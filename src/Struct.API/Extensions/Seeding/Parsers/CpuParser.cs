using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CpuParser : IComponentParser
{
    public string TargetFolderName => "CPU";

    public Category DetermineCategory(JsonNode root) => Category.Cpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root) =>
        SpecCopy.Present(root, Spec.Cores, Spec.Threads, Spec.BaseClock, Spec.BoostClock, Spec.Tdp, Spec.Socket, Spec.MemoryType);
}
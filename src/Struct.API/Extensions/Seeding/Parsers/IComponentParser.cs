using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public interface IComponentParser
{
    string TargetFolderName { get; }

    Category DetermineCategory(JsonNode root);
    Dictionary<string, string> ParseSpecs(JsonNode root);
}
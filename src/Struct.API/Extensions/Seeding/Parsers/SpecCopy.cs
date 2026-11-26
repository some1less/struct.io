using System.Text.Json.Nodes;

namespace Struct.API.Extensions.Seeding.Parsers;

internal static class SpecCopy
{
    /// <summary>
    /// Copies the given keys out of the row's TechnicalSpecs, leaving absent ones absent: the engines then
    /// skip the rule or use their own fallback instead of reading a made-up "0" / "Unknown".
    /// </summary>
    public static Dictionary<string, string> Present(JsonNode root, params string[] keys)
    {
        var ts = root["TechnicalSpecs"];
        var specs = new Dictionary<string, string>();
        foreach (var key in keys)
        {
            if (ts?[key]?.ToString() is string value && !string.IsNullOrWhiteSpace(value))
                specs[key] = value;
        }
        return specs;
    }
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace Struct.BLL.Core.Specs;

/// <summary>The one way to read values out of <c>TechnicalSpecs</c>. Missing spec ⇒ null / fallback.</summary>
public static class SpecReader
{
    // First number not glued to a letter or another number: "DDR5-6000" → 6000, "2.1 GHz" → 2.1.
    private static readonly Regex Number = new(@"(?<![A-Za-z0-9.,])\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    public static string? GetString(IReadOnlyDictionary<string, string>? specs, string key) =>
        specs != null && specs.TryGetValue(key, out var value) ? value : null;

    public static double GetNumber(IReadOnlyDictionary<string, string>? specs, string key, double fallback)
    {
        var match = Number.Match(GetString(specs, key) ?? "");
        return match.Success
            ? double.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture)
            : fallback;
    }
}

using Struct.BLL.Core.Specs;

namespace Struct.Tests;

/// <summary>The single spec-number parser shared by the compatibility engine and the scorer.</summary>
public class SpecReaderTests
{
    private static Dictionary<string, string> Specs(string value) => new() { ["K"] = value };

    [Theory]
    [InlineData("6000", 6000)]
    [InlineData("DDR5-6000", 6000)] // a digit glued to letters ("DDR5") is part of a name, not the value
    [InlineData("2.1 GHz", 2.1)]
    [InlineData("1295 MHz", 1295)]
    [InlineData("2,5", 2.5)]
    public void GetNumber_ReadsFirstStandaloneNumber(string raw, double expected) =>
        Assert.Equal(expected, SpecReader.GetNumber(Specs(raw), "K", -1), precision: 6);

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    public void GetNumber_NoNumber_ReturnsFallback(string raw) =>
        Assert.Equal(-1, SpecReader.GetNumber(Specs(raw), "K", -1));

    [Fact]
    public void MissingKeyOrNullSpecs_ReturnsFallback()
    {
        Assert.Equal(-1, SpecReader.GetNumber(Specs("5"), "Other", -1));
        Assert.Equal(-1, SpecReader.GetNumber(null, "K", -1));
        Assert.Null(SpecReader.GetString(null, "K"));
    }
}

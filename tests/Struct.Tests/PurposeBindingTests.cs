using System.Text.Json;
using Struct.BLL.Core;
using Struct.BLL.Core.Recommendation.Models;

namespace Struct.Tests;

public class PurposeBindingTests
{
    private static RecommendationRequest Deserialize(string body) =>
        JsonSerializer.Deserialize<RecommendationRequest>(body, JsonSerializerOptions.Web)!;

    [Theory]
    [InlineData("\"Gaming\"", Purpose.Gaming)]
    [InlineData("\"work\"", Purpose.Work)]
    [InlineData("\"Office\"", Purpose.Office)]
    public void KnownPurpose_Binds_CaseInsensitively(string purpose, Purpose expected) =>
        Assert.Equal(expected, Deserialize($"{{\"budget\":6000,\"purpose\":{purpose}}}").Purpose);

    [Fact]
    public void MissingPurpose_DefaultsToGaming() =>
        Assert.Equal(Purpose.Gaming, Deserialize("{\"budget\":6000}").Purpose);

    [Fact]
    public void Purpose_SerializesAsName_ForTheFrontend() =>
        Assert.Contains("\"purpose\":\"Work\"",
            JsonSerializer.Serialize(new RecommendationResult { Purpose = Purpose.Work }, JsonSerializerOptions.Web));

    [Theory]
    [InlineData("\"banana\"")]
    [InlineData("\"Hybrid\"")]
    [InlineData("\"Gaming, Work\"")]
    [InlineData("1")]
    [InlineData("42")]
    public void UnknownPurpose_IsRejected(string purpose) =>
        Assert.Throws<JsonException>(() => Deserialize($"{{\"budget\":6000,\"purpose\":{purpose}}}"));
}

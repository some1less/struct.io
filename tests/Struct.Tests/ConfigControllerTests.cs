using Microsoft.AspNetCore.Mvc;
using Struct.API.Controllers;
using Struct.BLL.Core;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class ConfigControllerTests
{
    private static object? Read(IActionResult result, string property)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value!;
        return value.GetType().GetProperty(property)!.GetValue(value);
    }

    [Fact]
    public void Config_ListsThePurposesTheBottleneckPenaltyAppliesTo()
    {
        var shipped = new ConfigController(TestConfig.Recommendation, TestConfig.Objective).Get();
        var everywhere = new ConfigController(TestConfig.Recommendation,
            TestConfig.Objective with { BottleneckPurposes = null }).Get();

        Assert.Equal(new[] { Purpose.Gaming, Purpose.Work }, Read(shipped, "BottleneckPurposes"));
        Assert.Equal(Enum.GetValues<Purpose>(), Read(everywhere, "BottleneckPurposes"));
        Assert.Equal(TestConfig.Objective.BottleneckThreshold, Read(shipped, "BottleneckThreshold"));
    }
}

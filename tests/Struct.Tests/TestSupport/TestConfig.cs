using Microsoft.Extensions.Configuration;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;

namespace Struct.Tests.TestSupport;

/// <summary>Binds the API's shipped appsettings.json, so tests run on the same policy as production.</summary>
public static class TestConfig
{
    private static readonly IConfiguration Root = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build();

    private static T Bind<T>(string section) where T : class =>
        Root.GetSection(section).Get<T>(o => o.ErrorOnUnknownConfiguration = true)
        ?? throw new InvalidOperationException($"Missing config section '{section}'.");

    public static RecommendationSettings Recommendation => Bind<RecommendationSettings>("Engine:Recommendation");
    public static ObjectiveSettings Objective => Bind<ObjectiveSettings>("Engine:Objective");
    public static ExactSolverSettings ExactSolver => Bind<ExactSolverSettings>("Engine:ExactSolver");
    public static CompatibilitySettings Compatibility => Bind<CompatibilitySettings>("Engine:Compatibility");
}

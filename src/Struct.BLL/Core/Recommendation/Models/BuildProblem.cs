using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>One recommendation instance: candidate parts for every category in the profile's sequence.</summary>
public record BuildProblem(
    IReadOnlyDictionary<Category, IReadOnlyList<Component>> Candidates,
    PurposeProfile Profile,
    Purpose Purpose,
    decimal Budget);

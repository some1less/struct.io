using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Specs;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Runs the inner solver on the whole problem, then once per platform (board socket + memory type, from the board
/// candidates) with CPU, board and RAM restricted to it, and keeps the best by (filled slots, objective).
/// Greedy commits to a platform with its first picks and local search changes at most two slots at a time, so it
/// cannot move CPU, board and RAM together. The unrestricted run goes first and ties keep it: never worse than inner.
/// </summary>
public class MultiStartSolver : IBuildSolver
{
    private readonly IBuildSolver _inner;
    private readonly BuildObjective _objective;

    public MultiStartSolver(IBuildSolver inner, BuildObjective objective)
    {
        _inner = inner;
        _objective = objective;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var best = _inner.Solve(problem);
        var bestRank = Rank(best, problem);
        foreach (var platform in Platforms(problem))
        {
            var solution = _inner.Solve(Restrict(problem, platform));
            var rank = Rank(solution, problem);
            if (rank.CompareTo(bestRank) > 0) (best, bestRank) = (solution, rank);
        }
        return best;
    }

    private (int Filled, double Objective) Rank(BuildSolution solution, BuildProblem problem) =>
        (solution.Build.Components.Count(), _objective.Evaluate(solution.Build, problem.Profile.Weights, problem.Purpose));

    // A part without the spec stays only in the unrestricted run.
    private static IEnumerable<(string Socket, string RamType)> Platforms(BuildProblem problem) =>
        (problem.Candidates.GetValueOrDefault(Category.Motherboard) ?? [])
            .Select(Platform)
            .Where(p => p.Socket != null && p.RamType != null)
            .Select(p => (p.Socket!, p.RamType!))
            .Distinct();

    private static BuildProblem Restrict(BuildProblem problem, (string Socket, string RamType) platform) =>
        problem with
        {
            Candidates = problem.Candidates.ToDictionary(slot => slot.Key, slot => slot.Key switch
            {
                Category.Cpu => slot.Value.Where(cpu => Key(cpu, Spec.Socket) == platform.Socket).ToList(),
                Category.Motherboard => slot.Value.Where(board => Platform(board) == platform).ToList(),
                Category.Ram => slot.Value.Where(ram => Key(ram, Spec.Type) == platform.RamType).ToList(),
                _ => slot.Value
            })
        };

    private static (string? Socket, string? RamType) Platform(Component board) =>
        (Key(board, Spec.Socket), Key(board, Spec.RamType));

    // As CompatibilityEngine compares specs: spaces and case do not count.
    private static string? Key(Component part, string spec) =>
        SpecReader.GetString(part.TechnicalSpecs, spec)?.Replace(" ", "").ToUpperInvariant();
}

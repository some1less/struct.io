using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Greedy construction, then local search on (filled slots, <see cref="BuildObjective"/>) in two phases.
/// Completion fills an empty slot, alone or together with a change to one filled slot that frees the money or makes
/// it fit (repair, e.g. a cheaper GPU so the CPU fits); the fill with the best Δobjective goes first. It runs first
/// because a missing slot outweighs any score (as in the exact solver) and upgrades would spend what a repair needs.
/// Improvement then changes one slot at a time: a free move with a gain first, then the most Δobjective per extra
/// złoty, so leftover money goes where it buys the most, not on one pricey part.
/// </summary>
public class GreedyLocalSearchSolver : IBuildSolver
{
    private readonly GreedySolver _greedy;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly BuildObjective _objective;

    public GreedyLocalSearchSolver(GreedySolver greedy, ICompatibilityEngine compatibilityEngine, BuildObjective objective)
    {
        _greedy = greedy;
        _compatibilityEngine = compatibilityEngine;
        _objective = objective;
    }

    public BuildSolution Solve(BuildProblem problem)
    {
        var solution = _greedy.Solve(problem);
        var build = solution.Build;

        // (filled, objective) rises with every move and no move empties a slot ⇒ both loops terminate
        while (BestMove(problem, build, FillMoves(problem, build)) is { } fill)
            foreach (var part in fill) build.Assign(part);
        while (BestMove(problem, build, SingleMoves(problem)) is { } move)
            foreach (var part in move) build.Assign(part);

        solution.FailedSlots.RemoveAll(f => build[Enum.Parse<Category>(f.Category)] != null);
        return solution;
    }

    private static IEnumerable<Component[]> SingleMoves(BuildProblem problem) =>
        problem.Profile.Sequence.SelectMany(category => problem.Candidates[category]).Select(part => new[] { part });

    private static IEnumerable<Component[]> FillMoves(BuildProblem problem, BuildContext build)
    {
        var fills = problem.Profile.Sequence.Where(c => build[c] == null).SelectMany(c => problem.Candidates[c]).ToList();
        var swaps = problem.Profile.Sequence.Where(c => build[c] != null).SelectMany(c => problem.Candidates[c]);
        return fills.Select(fill => new[] { fill })
            .Concat(from swap in swaps from fill in fills select new[] { swap, fill });
    }

    private Component[]? BestMove(BuildProblem problem, BuildContext build, IEnumerable<Component[]> moves)
    {
        var weights = problem.Profile.Weights;
        double current = _objective.Evaluate(build, weights, problem.Purpose);
        decimal total = build.Components.Sum(c => c.Price);
        (int Filled, int Free, double Gain) bestRank = default;
        Component[]? best = null;

        foreach (var move in moves)
        {
            if (move.Any(part => part.Id == build[part.Category]?.Id)) continue;
            decimal extra = move.Sum(part => part.Price - (build[part.Category]?.Price ?? 0));
            if (total + extra > problem.Budget) continue;

            var trial = build.Clone();
            foreach (var part in move) trial.Assign(part);
            int filled = move.Count(part => build[part.Category] == null);
            double delta = _objective.Evaluate(trial, weights, problem.Purpose) - current;
            if (filled == 0 && delta <= 0) continue;

            // A fill is judged by what it does to the objective: "free" would favour the repair that saves the most money.
            var rank = filled > 0 ? (filled, 0, delta) : (0, extra <= 0 ? 1 : 0, extra <= 0 ? delta : delta / (double)extra);
            if (rank.CompareTo(bestRank) <= 0) continue;

            // CheckCompatibility re-assigns the last part onto a clone of the trial, so it validates the whole new build.
            if (!_compatibilityEngine.CheckCompatibility(trial, move[^1]).IsCompatible) continue;

            bestRank = rank;
            best = move;
        }

        return best;
    }
}

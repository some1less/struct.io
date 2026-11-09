using System.Diagnostics;
using Struct.BLL.Core;
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.Evaluation;

/// <summary>
/// Runs greedy, greedy + local search and exact (over all candidates) on budget × purpose scenarios and reports
/// each build against the best one found; plus TopK and λ sweeps. The best is the exact build unless exact timed out.
/// </summary>
public sealed class EvaluationHarness
{
    public const int AllCandidates = int.MaxValue;
    public static readonly decimal[] Budgets = [3000, 4000, 5000, 6000, 8000, 10000, 15000];
    public static readonly int[] TopKs = [10, 20, 40, AllCandidates];
    public static readonly double[] Lambdas = [0, 0.25, 0.5, 1.0];

    private readonly RecommendationSettings _recommendation;
    private readonly ObjectiveSettings _objective;
    private readonly ExactSolverSettings _exact;
    private readonly CompatibilityEngine _compatibility;
    // One cache for the objective and every solver, so all of them see identical scores.
    private readonly CachingScorer _scorer = new(new PerformanceScorer());

    public EvaluationHarness(IReadOnlyList<Component> catalog, RecommendationSettings recommendation,
        ObjectiveSettings objective, CompatibilitySettings compatibility, ExactSolverSettings exact)
    {
        // The upgrade pass skips a candidate with the chosen part's Id, and catalog rows all carry Id 0.
        for (int i = 0; i < catalog.Count; i++) catalog[i].Id = i + 1;
        Catalog = catalog;
        _recommendation = recommendation;
        _objective = objective;
        _exact = exact;
        _compatibility = new CompatibilityEngine(compatibility);
    }

    public IReadOnlyList<Component> Catalog { get; }

    public IEnumerable<EvaluationRow> Grid(IEnumerable<decimal> budgets) =>
        Scenarios(budgets).SelectMany(s => CompareSolvers(s.Budget, s.Purpose, _objective));

    public IEnumerable<EvaluationRow> LambdaSweep(IEnumerable<decimal> budgets) =>
        from s in Scenarios(budgets)
        from lambda in Lambdas
        from row in CompareSolvers(s.Budget, s.Purpose, _objective with { BottleneckWeight = lambda })
        select row;

    public IEnumerable<EvaluationRow> TopKSweep(IEnumerable<decimal> budgets)
    {
        var objective = new BuildObjective(_scorer, _objective);
        foreach (var (budget, purpose) in Scenarios(budgets))
        {
            var problem = Problem(budget, purpose);
            var runs = TopKs.Select(k => Measure(k, Exact(objective, k), problem, objective)).ToList();
            foreach (var row in Rows(runs, problem, _objective.BottleneckWeight)) yield return row;
        }
    }

    public void WriteAll(string outputDir)
    {
        WarmUp();
        Directory.CreateDirectory(outputDir);
        Write(Path.Combine(outputDir, "grid.csv"), Grid(Budgets));
        Write(Path.Combine(outputDir, "topk.csv"), TopKSweep(Budgets));
        Write(Path.Combine(outputDir, "lambda.csv"), LambdaSweep(Budgets));
    }

    // JIT and the score cache, once, so each measured run is the search alone.
    private void WarmUp()
    {
        foreach (var purpose in Enum.GetValues<Purpose>())
            foreach (var solver in Solvers(new BuildObjective(_scorer, _objective), TopKs[0]))
                solver.Solve(Problem(Budgets[0], purpose));
    }

    private static void Write(string path, IEnumerable<EvaluationRow> rows)
    {
        File.WriteAllLines(path, rows.Select(r => r.ToCsv()).Prepend(EvaluationRow.Header));
        Console.WriteLine($"wrote {path}");
    }

    private static IEnumerable<(decimal Budget, Purpose Purpose)> Scenarios(IEnumerable<decimal> budgets) =>
        from budget in budgets from purpose in Enum.GetValues<Purpose>() select (budget, purpose);

    private IEnumerable<EvaluationRow> CompareSolvers(decimal budget, Purpose purpose, ObjectiveSettings settings)
    {
        var objective = new BuildObjective(_scorer, settings);
        var problem = Problem(budget, purpose);
        var runs = Solvers(objective, AllCandidates)
            .Select(s => Measure(s is ExactBuildSolver ? AllCandidates : null, s, problem, objective))
            .ToList();
        return Rows(runs, problem, settings.BottleneckWeight);
    }

    private IBuildSolver[] Solvers(BuildObjective objective, int topK)
    {
        var greedy = new GreedySolver(_compatibility, _scorer, _recommendation);
        return [greedy, new GreedyLocalSearchSolver(greedy, _compatibility, objective), Exact(objective, topK)];
    }

    private ExactBuildSolver Exact(BuildObjective objective, int topK) =>
        new(_compatibility, objective, _scorer, _exact with { TopKPerCategory = topK });

    private BuildProblem Problem(decimal budget, Purpose purpose)
    {
        var profile = _recommendation.Profiles[purpose];
        var candidates = profile.Sequence.ToDictionary(c => c,
            c => (IReadOnlyList<Component>)Catalog.Where(p => p.Category == c).ToList());
        return new BuildProblem(candidates, profile, purpose, budget);
    }

    private static Run Measure(int? topK, IBuildSolver solver, BuildProblem problem, BuildObjective objective)
    {
        var clock = Stopwatch.StartNew();
        var solution = solver.Solve(problem);
        double ms = clock.Elapsed.TotalMilliseconds;
        string name = solver switch { GreedySolver => "greedy", GreedyLocalSearchSolver => "greedy+ls", _ => "exact" };
        return new Run(name, topK, solution, objective.Evaluate(solution.Build, problem.Profile.Weights, problem.Purpose), ms);
    }

    private static IEnumerable<EvaluationRow> Rows(List<Run> runs, BuildProblem problem, double lambda)
    {
        var best = runs.MaxBy(r => (r.Filled, r.Objective))!;
        return runs.Select(run => new EvaluationRow(
            problem.Budget, problem.Purpose, run.Solver, run.TopK, lambda, run.Objective,
            run.Filled == best.Filled && best.Objective > 0 ? run.Objective / best.Objective * 100 : null,
            run.Solution.TotalPrice, run.Solution.FailedSlots.Count == 0, run.Filled, run.Solution.TimedOut, run.Ms,
            run.Solution.Build.Cpu?.Name ?? "", run.Solution.Build.Gpu?.Name ?? ""));
    }

    private sealed record Run(string Solver, int? TopK, BuildSolution Solution, double Objective, double Ms)
    {
        public int Filled => Solution.Build.Components.Count();
    }
}

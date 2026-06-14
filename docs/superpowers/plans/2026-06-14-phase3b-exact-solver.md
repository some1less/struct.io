# Phase 3b — Exact Build Solver (Branch-and-Bound) + Comparison — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A hand-rolled branch-and-bound solver that finds the optimal build maximizing the same `BuildObjective` (subject to budget + pairwise compatibility), used to benchmark the greedy+local-search engine ("greedy reaches X% of optimal").

**Architecture:** Pure BLL evaluation tool, NOT wired into the live `/recommend` API. Shared config (`RecommendationProfiles`) and build helpers (`BuildContextOps`) are extracted so the solver and the engine use identical weights and build operations. The solver prunes via an optimistic upper bound (best-possible weighted score for unfilled categories, ignoring the always-non-positive bottleneck penalty) and searches the top-K candidates per category.

**Tech Stack:** C# / .NET 9, xUnit. No new external dependencies.

**Design source:** vault `Refactoring/Design — Phase 3.md` + the approved Phase 3b design.

---

## File Structure

- Create: `src/Struct.BLL/Core/Recommendation/RecommendationProfiles.cs` — public static `Weights` + `Sequences` (moved out of the engine).
- Create: `src/Struct.BLL/Core/Recommendation/BuildContextOps.cs` — static `Clone`/`Assign`/`Get` build helpers.
- Modify: `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs` — reference `RecommendationProfiles` + `BuildContextOps`.
- Modify: `src/Struct.BLL/Core/Recommendation/BuildObjective.cs` — use `BuildContextOps.Get`.
- Create: `src/Struct.BLL/Core/Recommendation/Models/ExactSolverSettings.cs` — `TopKPerCategory`.
- Create: `src/Struct.BLL/Core/Recommendation/Models/ExactBuildResult.cs` — `Build`, `Objective`, `IsComplete`.
- Create: `src/Struct.BLL/Core/Recommendation/ExactBuildSolver.cs` — the B&B solver.
- Create: `tests/Struct.Tests/ExactBuildSolverTests.cs` — solver + comparison tests.
- Modify: `tests/Struct.Tests/TestSupport/CatalogFactory.cs` — add `GreedyTrapCatalog()`.

---

## Task 1: Extract shared config + build helpers (refactor, keep 33 green)

**Files:** create `RecommendationProfiles.cs`, `BuildContextOps.cs`; modify `RecommendationEngine.cs`, `BuildObjective.cs`.

- [ ] **Step 1: Create `RecommendationProfiles`** with the weight matrix and sequences (verbatim copy of the engine's current `BaseWeights` and `Sequences`, made public static).

- [ ] **Step 2: Create `BuildContextOps`**:

```csharp
using Struct.BLL.Core.Compatibility;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>Shared build-context operations used by the engine and the exact solver.</summary>
public static class BuildContextOps
{
    public static BuildContext Clone(BuildContext b) => new()
    {
        Cpu = b.Cpu, Gpu = b.Gpu, Motherboard = b.Motherboard, Ram = b.Ram,
        Psu = b.Psu, Case = b.Case, Cooler = b.Cooler, Storage = b.Storage
    };

    public static void Assign(BuildContext ctx, Component candidate)
    {
        switch (candidate.Category)
        {
            case Category.Cpu: ctx.Cpu = candidate; break;
            case Category.Gpu: ctx.Gpu = candidate; break;
            case Category.Motherboard: ctx.Motherboard = candidate; break;
            case Category.Ram: ctx.Ram = candidate; break;
            case Category.Psu: ctx.Psu = candidate; break;
            case Category.Case: ctx.Case = candidate; break;
            case Category.Cooler: ctx.Cooler = candidate; break;
            case Category.Ssd: case Category.Hdd: ctx.Storage = candidate; break;
        }
    }

    public static Component? Get(BuildContext b, Category c) => c switch
    {
        Category.Cpu => b.Cpu,
        Category.Gpu => b.Gpu,
        Category.Motherboard => b.Motherboard,
        Category.Ram => b.Ram,
        Category.Psu => b.Psu,
        Category.Case => b.Case,
        Category.Cooler => b.Cooler,
        Category.Ssd or Category.Hdd => b.Storage,
        _ => null
    };
}
```

- [ ] **Step 3: Point the engine at the shared helpers.** In `RecommendationEngine.cs`: delete the private `BaseWeights`, `Sequences`, `CloneBuild`, `GetComponent`, `AssignToContext`; replace references with `RecommendationProfiles.Weights`, `RecommendationProfiles.Sequences`, `BuildContextOps.Clone/Get/Assign`. In `BuildObjective.cs`: delete its private `GetComponent`; use `BuildContextOps.Get`.

- [ ] **Step 4: Verify nothing regressed.** Run `dotnet build Struct.sln` (0/0) and `dotnet test tests/Struct.Tests/Struct.Tests.csproj` → 33 passed.

- [ ] **Step 5: Commit** (`Refactor: extract RecommendationProfiles + BuildContextOps for reuse`).

---

## Task 2: ExactBuildSolver (branch-and-bound) — sanity + constraints

**Files:** create `ExactSolverSettings.cs`, `ExactBuildResult.cs`, `ExactBuildSolver.cs`; test `ExactBuildSolverTests.cs`.

- [ ] **Step 1: Write the failing sanity test** — on a single-option-per-category catalog the solver returns exactly that build, complete, within budget.

```csharp
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class ExactBuildSolverTests
{
    private static ExactBuildSolver Solver() =>
        new(new CompatibilityEngine(), new BuildObjective(new PerformanceScorer(), new ObjectiveSettings()),
            new PerformanceScorer(), new ExactSolverSettings());

    private static Dictionary<Category, IReadOnlyList<Component>> Pools(IEnumerable<Component> catalog)
    {
        int id = 1;
        var list = catalog.ToList();
        foreach (var c in list) c.Id = id++;
        return list.GroupBy(c => c.Category)
                   .ToDictionary(g => g.Key, g => (IReadOnlyList<Component>)g.ToList());
    }

    [Fact]
    public void SingleOptionCatalog_ReturnsThatCompleteBuild_WithinBudget()
    {
        var result = Solver().Solve(
            Pools(CatalogFactory.FullCoherentCatalog()),
            RecommendationProfiles.Sequences["Gaming"],
            RecommendationProfiles.Weights["Gaming"],
            "Gaming", budget: 12000m);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.Build.Cpu);
        Assert.NotNull(result.Build.Gpu);
    }
}
```

- [ ] **Step 2: Run → FAIL** (types don't exist). `dotnet test --filter "FullyQualifiedName~ExactBuildSolverTests"`.

- [ ] **Step 3: Create the settings + result models.**

```csharp
// Models/ExactSolverSettings.cs
namespace Struct.BLL.Core.Recommendation.Models;
public class ExactSolverSettings { public int TopKPerCategory { get; init; } = 20; }
```

```csharp
// Models/ExactBuildResult.cs
using Struct.BLL.Core.Compatibility;
namespace Struct.BLL.Core.Recommendation.Models;
public class ExactBuildResult
{
    public BuildContext Build { get; init; } = new();
    public double Objective { get; init; }
    public bool IsComplete { get; init; }
}
```

- [ ] **Step 4: Implement `ExactBuildSolver`.**

```csharp
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Branch-and-bound solver: finds the build maximizing <see cref="BuildObjective"/> subject to a
/// total budget and pairwise compatibility, over the top-K candidates per category. Evaluation tool,
/// not wired into the live API.
/// </summary>
public class ExactBuildSolver
{
    private readonly ICompatibilityEngine _compat;
    private readonly BuildObjective _objective;
    private readonly IPerformanceScorer _scorer;
    private readonly ExactSolverSettings _settings;

    public ExactBuildSolver(ICompatibilityEngine compat, BuildObjective objective,
        IPerformanceScorer scorer, ExactSolverSettings settings)
    {
        _compat = compat; _objective = objective; _scorer = scorer; _settings = settings;
    }

    public ExactBuildResult Solve(
        IReadOnlyDictionary<Category, IReadOnlyList<Component>> candidatesByCategory,
        IReadOnlyList<Category> categories,
        IReadOnlyDictionary<Category, double> weights,
        string purpose,
        decimal budget)
    {
        // Prune each category to top-K by score (descending); record best score per category.
        var pools = new Dictionary<Category, List<Component>>();
        var bestScore = new Dictionary<Category, double>();
        foreach (var cat in categories)
        {
            var list = (candidatesByCategory.TryGetValue(cat, out var raw) ? raw : Array.Empty<Component>())
                .OrderByDescending(c => _scorer.CalculateScore(c, purpose))
                .Take(_settings.TopKPerCategory)
                .ToList();
            pools[cat] = list;
            bestScore[cat] = list.Count > 0 ? _scorer.CalculateScore(list[0], purpose) : 0.0;
        }

        int n = categories.Count;
        // suffix[i] = optimistic Σ weight·bestScore for categories[i..] (ignores the >=0 bottleneck penalty)
        var suffix = new double[n + 1];
        for (int i = n - 1; i >= 0; i--)
            suffix[i] = suffix[i + 1] + weights[categories[i]] * bestScore[categories[i]];

        var best = new ExactBuildResult { Build = new BuildContext(), Objective = double.NegativeInfinity, IsComplete = false };

        void Recurse(int idx, BuildContext partial, decimal spent, double weightedSoFar)
        {
            if (idx == n)
            {
                double obj = _objective.Evaluate(partial, weights, purpose);
                if (obj > best.Objective)
                    best = new ExactBuildResult { Build = BuildContextOps.Clone(partial), Objective = obj, IsComplete = IsComplete(partial, categories) };
                return;
            }

            // Optimistic upper bound: even if every remaining slot got its best score and no penalty,
            // this branch could not beat the incumbent ⇒ prune.
            if (weightedSoFar + suffix[idx] <= best.Objective) return;

            var cat = categories[idx];
            foreach (var candidate in pools[cat])
            {
                if (spent + candidate.Price > budget) continue;
                if (!_compat.CheckCompatibility(partial, candidate).IsCompatible) continue;

                var next = BuildContextOps.Clone(partial);
                BuildContextOps.Assign(next, candidate);
                Recurse(idx + 1, next, spent + candidate.Price, weightedSoFar + weights[cat] * _scorer.CalculateScore(candidate, purpose));
            }

            // Leave this category empty (only ever wins when no candidate is feasible — keeps the
            // solver robust and lets it return the best partial build when a full build is impossible).
            Recurse(idx + 1, partial, spent, weightedSoFar);
        }

        Recurse(0, new BuildContext(), 0m, 0.0);
        return best;
    }

    private static bool IsComplete(BuildContext b, IReadOnlyList<Category> categories) =>
        categories.All(c => BuildContextOps.Get(b, c) != null);
}
```

- [ ] **Step 5: Run → PASS.** `dotnet test --filter "FullyQualifiedName~ExactBuildSolverTests"`.

- [ ] **Step 6: Commit** (`Feat: exact branch-and-bound build solver`).

---

## Task 3: Prove the solver beats a greedy trap + matches/exceeds local-search

**Files:** modify `CatalogFactory.cs`; add tests to `ExactBuildSolverTests.cs`.

- [ ] **Step 1: Add a "greedy trap" catalog.** A catalog where greedy's locally-best first pick forces a globally worse build, but a better balanced combination exists within budget. In `CatalogFactory.cs`:

```csharp
    /// <summary>
    /// A catalog where the GPU-first greedy is tempted by an expensive GPU that eats the budget and
    /// forces weak choices elsewhere, while a slightly cheaper GPU leaves room for a far better overall
    /// build. Used to show the exact solver finds a strictly better objective than greedy+local-search.
    /// </summary>
    public static List<Component> GreedyTrapCatalog()
    {
        var catalog = FullCoherentCatalog()
            .Where(c => c.Category != Category.Gpu && c.Category != Category.Cpu && c.Category != Category.Psu)
            .ToList();
        catalog.Add(ComponentBuilder.New(Category.Psu, "Corsair RM1000e").Brand("Corsair").Price(700)
            .Spec("Wattage", "1000").Spec("Efficiency", "Gold").Spec("Modular", "Full"));
        // Two CPUs and two GPUs; the balanced mid combo beats the lopsided flagship-GPU combo on objective.
        catalog.Add(ComponentBuilder.New(Category.Cpu, "AMD Ryzen 5 budget").Brand("AMD").Price(900)
            .Spec("Socket", "AM5").Spec("MemoryType", "DDR5").Spec("TDP", "65")
            .Spec("Cores", "6").Spec("Threads", "12").Spec("BaseClock", "3.8").Spec("BoostClock", "4.6"));
        catalog.Add(ComponentBuilder.New(Category.Cpu, "AMD Ryzen 9 strong").Brand("AMD").Price(2600)
            .Spec("Socket", "AM5").Spec("MemoryType", "DDR5").Spec("TDP", "120")
            .Spec("Cores", "12").Spec("Threads", "24").Spec("BaseClock", "4.4").Spec("BoostClock", "5.6"));
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4070").Brand("NVIDIA").Price(2600)
            .Spec("VRAM", "12").Spec("CoreClock", "1920").Spec("TDP", "200").Spec("Length", "240"));
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4090").Brand("NVIDIA").Price(5200)
            .Spec("VRAM", "24").Spec("CoreClock", "2235").Spec("TDP", "450").Spec("Length", "304"));
        return catalog;
    }
```

- [ ] **Step 2: Write the failing comparison test.** Exact objective must be ≥ the engine's (greedy+local-search) objective on the same catalog/budget; and the exact build is within budget and complete. (This test passes only once the solver is correct.)

```csharp
    [Fact]
    public async Task ExactSolver_IsAtLeastAsGoodAs_GreedyPlusLocalSearch()
    {
        var catalog = CatalogFactory.GreedyTrapCatalog();
        var scorer = new PerformanceScorer();
        var settings = new ObjectiveSettings();
        var objective = new BuildObjective(scorer, settings);

        // Engine (greedy + local-search) result, re-scored with the same objective.
        var engine = new RecommendationEngine(new StubComponentRepository(catalog),
            new CompatibilityEngine(), scorer, objective);
        var engineResult = await engine.GenerateRecommendationAsync(
            new RecommendationRequest { Budget = 9000m, Purpose = "Gaming" });
        var engineBuild = BuildFromSlots(engineResult, catalog);
        double engineObjective = objective.Evaluate(engineBuild, RecommendationProfiles.Weights["Gaming"], "Gaming");

        // Exact solver result.
        var exact = new ExactBuildSolver(new CompatibilityEngine(), objective, scorer, new ExactSolverSettings())
            .Solve(Pools(catalog), RecommendationProfiles.Sequences["Gaming"],
                   RecommendationProfiles.Weights["Gaming"], "Gaming", 9000m);

        Assert.True(exact.IsComplete);
        Assert.True(exact.Objective >= engineObjective - 1e-9,
            $"exact {exact.Objective} should be >= engine {engineObjective}");
    }

    private static BuildContext BuildFromSlots(
        Struct.BLL.Core.Recommendation.Models.RecommendationResult result, List<Component> catalog)
    {
        var ctx = new BuildContext();
        foreach (var slot in result.Slots)
        {
            var comp = catalog.First(c => c.Name == slot.Recommendations.First().Component.Name);
            BuildContextOps.Assign(ctx, comp);
        }
        return ctx;
    }
```

Note: `Pools` reassigns ids on the catalog list; call `Pools(catalog)` only after the engine has run, or pass the engine a separate copy. To avoid id interference, build the engine's stub from `CatalogFactory.GreedyTrapCatalog()` (a fresh list) — see Step 3 adjustment.

- [ ] **Step 3: Adjust the test to use independent catalog instances** so id assignment in `Pools` doesn't mutate the engine's catalog: construct `var engineCatalog = CatalogFactory.GreedyTrapCatalog();` and `var solverCatalog = CatalogFactory.GreedyTrapCatalog();`, feed each to its own consumer.

- [ ] **Step 4: Run → it should PASS** once the solver from Task 2 is correct (exact is optimal by construction). If it fails because exact < engine, that is a real solver bug — debug before continuing.

- [ ] **Step 5: Add a strict-improvement assertion** demonstrating value: assert the exact build's objective is strictly greater than a deliberately bad baseline, or that exact's chosen GPU/CPU combination differs from the lopsided greedy pick. Keep only if it holds deterministically; otherwise keep the ≥ comparison as the core evidence.

- [ ] **Step 6: Full suite + build.** `dotnet build Struct.sln` (0/0); `dotnet test` → all green.

- [ ] **Step 7: Commit** (`Test: exact solver matches/exceeds greedy+local-search on a greedy trap`).

---

## Task 4: Verify, document, finish branch

- [ ] **Step 1:** `dotnet build Struct.sln` + full `dotnet test`, confirm green.
- [ ] **Step 2:** Write vault `Refactoring/Analysis — Phase 3b.md` (what changed: shared profiles/ops, exact B&B solver, comparison harness; evidence: test counts, build clean; decisions: top-K pruning, not wired to live API, optimistic bound correctness; hand-off: benchmark scoring still pending user data; optional beam search).
- [ ] **Step 3:** Tick Phase 3b in `Refactoring Plan — Phases.md`.
- [ ] **Step 4:** Commit the plan doc; then use `superpowers:finishing-a-development-branch` to merge/PR.

---

## Self-Review

- **Spec coverage:** exact B&B solver → Task 2; optimistic bound + top-K + budget/compat pruning → Task 2; comparison vs greedy+local-search → Task 3; not wired to live API → solver is a standalone class (no DI/controller changes); shared objective/weights → Task 1.
- **Placeholder scan:** none — full code for `BuildContextOps`, `ExactBuildSolver`, settings/result models, and tests is inline. Task 1 Step 1/3 describe a mechanical move of existing data (the engine's current `BaseWeights`/`Sequences`) — copied verbatim, not re-specified.
- **Type consistency:** `ExactBuildSolver(ICompatibilityEngine, BuildObjective, IPerformanceScorer, ExactSolverSettings)`; `Solve(IReadOnlyDictionary<Category,IReadOnlyList<Component>>, IReadOnlyList<Category>, IReadOnlyDictionary<Category,double>, string, decimal)`; `ExactBuildResult { Build, Objective, IsComplete }`; `BuildContextOps.Clone/Assign/Get`; `RecommendationProfiles.Weights/Sequences` — used identically across tasks.
- **Risk note:** Task 1 touches the freshly-merged engine; the 33-test suite is the guard. Task 3's greedy-trap numbers may need tuning so the scenario is genuinely a trap — if greedy already finds the optimum, adjust prices/specs until exact strictly wins, keeping the ≥ assertion as the floor.

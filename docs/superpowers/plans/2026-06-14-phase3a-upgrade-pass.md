# Phase 3a (Part 1) — Build Objective + Local-Search Upgrade Pass — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the recommendation engine stranding ~40% of the budget and pairing unbalanced CPU/GPU, by adding an explicit build-level objective (with a CPU↔GPU bottleneck term) and a local-search pass that improves the greedy build within budget.

**Architecture:** Keep the existing greedy fill as the baseline. Introduce `BuildObjective` — a scalar `Σ(weightᵢ × scoreᵢ) − λ·bottleneckPenalty` evaluator. After greedy, a local-search loop repeatedly applies the single component swap with the best positive Δobjective that keeps the build compatible and within total budget, until no improving swap remains. The same objective will later be maximized by the Phase-3b exact solver, so the comparison is apples-to-apples.

**Tech Stack:** C# / .NET 9, xUnit. Pure BLL classes behind existing interfaces; no new external dependencies.

**Design source:** Obsidian vault `Refactoring/Design — Phase 3.md` (§3.1–3.5).

**Deliberate behavior changes in this plan (vs Phase 2):**
- The per-slot budget-utilization multiplier (`baseScore × (1 + util×0.05)`) is **removed** from greedy selection — the upgrade pass now handles budget usage, and this also kills the `DivideByZero` risk the review flagged. Greedy selects by raw `CalculateScore`.
- Slot results now return **rank-1 only** (the chosen component). Rank-2 "alternatives" are dropped because they were flagged invalid (Bug 3); validated alternatives are future work. No existing test asserts 2 recommendations, so this stays green.

---

## File Structure

- Create: `src/Struct.BLL/Core/Recommendation/Models/ObjectiveSettings.cs` — config (λ, threshold). One responsibility: tunable objective parameters.
- Create: `src/Struct.BLL/Core/Recommendation/BuildObjective.cs` — pure objective evaluator. Depends on `IPerformanceScorer` + `ObjectiveSettings`.
- Modify: `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs` — inject `BuildObjective`; retain candidate pools; plain-score greedy; build slots at end; add `RunUpgradePass`.
- Modify: `src/Struct.API/Program.cs` — register `ObjectiveSettings` + `BuildObjective`.
- Create: `tests/Struct.Tests/TestSupport/FixedScorer.cs` — deterministic stub `IPerformanceScorer` for objective unit tests.
- Create: `tests/Struct.Tests/BuildObjectiveTests.cs` — objective + bottleneck unit tests.
- Modify: `tests/Struct.Tests/TestSupport/CatalogFactory.cs` — add `UpgradableCatalog()`.
- Modify: `tests/Struct.Tests/RecommendationEngineTests.cs` — update `EngineFor` helper; add upgrade-pass tests.

---

## Task 1: BuildObjective + ObjectiveSettings (with bottleneck term)

**Files:**
- Create: `src/Struct.BLL/Core/Recommendation/Models/ObjectiveSettings.cs`
- Create: `src/Struct.BLL/Core/Recommendation/BuildObjective.cs`
- Create: `tests/Struct.Tests/TestSupport/FixedScorer.cs`
- Test: `tests/Struct.Tests/BuildObjectiveTests.cs`

- [ ] **Step 1: Create the config class**

`src/Struct.BLL/Core/Recommendation/Models/ObjectiveSettings.cs`:

```csharp
namespace Struct.BLL.Core.Recommendation.Models;

/// <summary>
/// Tunable parameters for <see cref="BuildObjective"/>. Centralized so the thesis has a single
/// weights/parameters table and the values are easy to sweep during evaluation.
/// </summary>
public class ObjectiveSettings
{
    /// <summary>λ — how hard CPU↔GPU imbalance is penalized.</summary>
    public double BottleneckWeight { get; init; } = 0.5;

    /// <summary>Allowed |cpuScore − gpuScore| gap before any penalty applies.</summary>
    public double BottleneckThreshold { get; init; } = 0.15;
}
```

- [ ] **Step 2: Create the deterministic scorer stub (test support)**

`tests/Struct.Tests/TestSupport/FixedScorer.cs`:

```csharp
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.Tests.TestSupport;

/// <summary>Returns a pre-set score per component name, so objective math is fully deterministic.</summary>
public sealed class FixedScorer : IPerformanceScorer
{
    private readonly Dictionary<string, double> _scores;
    public FixedScorer(Dictionary<string, double> scores) => _scores = scores;

    public double CalculateScore(Component component, string purpose = "Gaming") =>
        _scores.TryGetValue(component.Name, out var s) ? s : 0.0;
}
```

- [ ] **Step 3: Write the failing tests**

`tests/Struct.Tests/BuildObjectiveTests.cs`:

```csharp
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation;
using Struct.BLL.Core.Recommendation.Models;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class BuildObjectiveTests
{
    private static readonly Dictionary<Category, double> EqualWeights = new()
    {
        { Category.Cpu, 0.5 }, { Category.Gpu, 0.5 }
    };

    private static BuildContext Build(double cpuScore, double gpuScore, out FixedScorer scorer)
    {
        scorer = new FixedScorer(new() { ["CPU"] = cpuScore, ["GPU"] = gpuScore });
        return new BuildContext
        {
            Cpu = ComponentBuilder.New(Category.Cpu, "CPU").Build(),
            Gpu = ComponentBuilder.New(Category.Gpu, "GPU").Build()
        };
    }

    [Fact]
    public void BalancedBuild_WithinThreshold_HasNoBottleneckPenalty()
    {
        var build = Build(0.80, 0.80, out var scorer);
        var objective = new BuildObjective(scorer, new ObjectiveSettings());

        // Σ(0.5·0.8 + 0.5·0.8) = 0.80, gap 0 ≤ 0.15 ⇒ no penalty
        Assert.Equal(0.80, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }

    [Fact]
    public void ImbalancedBuild_BeyondThreshold_IsPenalized()
    {
        var build = Build(0.80, 0.50, out var scorer);
        var objective = new BuildObjective(scorer, new ObjectiveSettings()); // λ=0.5, threshold=0.15

        // weighted = 0.5·0.8 + 0.5·0.5 = 0.65; gap 0.30 ⇒ penalty 0.5·(0.30−0.15)=0.075
        Assert.Equal(0.575, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }

    [Fact]
    public void MissingCpuOrGpu_AppliesNoBottleneckPenalty()
    {
        var scorer = new FixedScorer(new() { ["CPU"] = 0.9 });
        var build = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "CPU").Build() };
        var objective = new BuildObjective(scorer, new ObjectiveSettings());

        // Only CPU present: 0.5·0.9 = 0.45, no penalty (GPU missing)
        Assert.Equal(0.45, objective.Evaluate(build, EqualWeights, "Gaming"), precision: 6);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj --filter "FullyQualifiedName~BuildObjectiveTests"`
Expected: FAIL to compile — `BuildObjective` does not exist yet.

- [ ] **Step 5: Implement BuildObjective**

`src/Struct.BLL/Core/Recommendation/BuildObjective.cs`:

```csharp
using Struct.BLL.Core.Compatibility;
using Struct.BLL.Core.Recommendation.Models;
using Struct.BLL.Core.Scoring;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Scalar quality of a whole build: Σ(weightᵢ × scoreᵢ) minus a CPU↔GPU imbalance penalty.
/// Maximized by the local-search upgrade pass (and, later, the exact solver).
/// </summary>
public class BuildObjective
{
    private readonly IPerformanceScorer _scorer;
    private readonly ObjectiveSettings _settings;

    public BuildObjective(IPerformanceScorer scorer, ObjectiveSettings settings)
    {
        _scorer = scorer;
        _settings = settings;
    }

    public double Evaluate(BuildContext build, IReadOnlyDictionary<Category, double> weights, string purpose)
    {
        double sum = 0;
        foreach (var (category, weight) in weights)
        {
            var component = GetComponent(build, category);
            if (component != null)
                sum += weight * _scorer.CalculateScore(component, purpose);
        }
        return sum - _settings.BottleneckWeight * BottleneckPenalty(build, purpose);
    }

    private double BottleneckPenalty(BuildContext build, string purpose)
    {
        if (build.Cpu == null || build.Gpu == null) return 0;
        double cpu = _scorer.CalculateScore(build.Cpu, purpose);
        double gpu = _scorer.CalculateScore(build.Gpu, purpose);
        double gap = Math.Abs(cpu - gpu);
        return Math.Max(0, gap - _settings.BottleneckThreshold);
    }

    private static Component? GetComponent(BuildContext b, Category c) => c switch
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

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj --filter "FullyQualifiedName~BuildObjectiveTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Struct.BLL/Core/Recommendation/BuildObjective.cs \
        src/Struct.BLL/Core/Recommendation/Models/ObjectiveSettings.cs \
        tests/Struct.Tests/TestSupport/FixedScorer.cs \
        tests/Struct.Tests/BuildObjectiveTests.cs
git commit -m "Feat: add BuildObjective with CPU-GPU bottleneck term

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Refactor engine to use BuildObjective and a clean greedy (no behavior regressions)

This is a refactor: retain candidate pools, select by raw score, build slots once at the end, and add an (empty) upgrade hook. The 28 existing tests are the safety net. The upgrade body comes in Task 3.

**Files:**
- Modify: `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs`
- Modify: `src/Struct.API/Program.cs`
- Modify: `tests/Struct.Tests/RecommendationEngineTests.cs:14-16` (the `EngineFor` helper)

- [ ] **Step 1: Update the test helper so tests still compile with the new constructor**

In `tests/Struct.Tests/RecommendationEngineTests.cs`, replace the `EngineFor` helper:

```csharp
    private static RecommendationEngine EngineFor(IEnumerable<Component> catalog) =>
        new(new StubComponentRepository(catalog), new CompatibilityEngine(), new PerformanceScorer(),
            new BuildObjective(new PerformanceScorer(), new ObjectiveSettings()));
```

Add the needed usings at the top of the file if missing:

```csharp
using Struct.BLL.Core.Recommendation.Models;
```

- [ ] **Step 2: Replace the engine's fields, constructor, and method**

In `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs`, add the objective field next to the others:

```csharp
    private readonly IComponentRepository _repository;
    private readonly ICompatibilityEngine _compatibilityEngine;
    private readonly IPerformanceScorer _scorer;
    private readonly BuildObjective _objective;
```

Replace the constructor:

```csharp
    public RecommendationEngine(IComponentRepository repository,
        ICompatibilityEngine compatibilityEngine, IPerformanceScorer scorer, BuildObjective objective)
    {
        _repository = repository;
        _compatibilityEngine = compatibilityEngine;
        _scorer = scorer;
        _objective = objective;
    }
```

Replace the entire `GenerateRecommendationAsync` method body with:

```csharp
    public async Task<RecommendationResult> GenerateRecommendationAsync(RecommendationRequest request)
    {
        var purpose = BaseWeights.ContainsKey(request.Purpose) ? request.Purpose : "Gaming";
        var sequence = Sequences[purpose];
        var weights = new Dictionary<Category, double>(BaseWeights[purpose]);

        var allocatedBudgets = weights.ToDictionary(k => k.Key, v => request.Budget * (decimal)v.Value);

        var result = new RecommendationResult { Purpose = purpose, TotalBudget = request.Budget, IsSuccess = true };
        var currentBuild = new BuildContext();
        var candidatesByCategory = new Dictionary<Category, List<Component>>();
        decimal actualTotal = 0;

        // ---- Greedy construction (baseline) ----
        foreach (var category in sequence)
        {
            decimal slotBudget = allocatedBudgets[category];

            var candidates = (await _repository.GetByCategoryAsync(category, 1, 5000)).ToList();
            candidatesByCategory[category] = candidates;

            decimal maxAllowedPrice = slotBudget * 1.10m;
            decimal remainingGlobalBudget = request.Budget - actualTotal;
            if (maxAllowedPrice > remainingGlobalBudget) maxAllowedPrice = remainingGlobalBudget;
            if (maxAllowedPrice < 0) maxAllowedPrice = 0;

            var compatible = candidates
                .Where(c => c.Price <= maxAllowedPrice)
                .Where(c => _compatibilityEngine.CheckCompatibility(currentBuild, c).IsCompatible)
                .ToList();

            if (!compatible.Any())
            {
                result.IsSuccess = false;
                result.FailedSlots.Add(new FailedSlot
                {
                    Category = category.ToString(),
                    Reason = $"Could not find a compatible {category} within {slotBudget:F2} PLN."
                });
                continue;
            }

            var best = compatible.OrderByDescending(c => _scorer.CalculateScore(c, purpose)).First();

            AssignToContext(currentBuild, best);
            actualTotal += best.Price;

            decimal savings = slotBudget - best.Price;
            var remainingCategories = sequence.Skip(sequence.IndexOf(category) + 1).ToList();
            if (savings != 0 && remainingCategories.Any())
            {
                double remainingWeightSum = remainingCategories.Sum(c => weights[c]);
                if (remainingWeightSum > 0)
                    foreach (var remCat in remainingCategories)
                        allocatedBudgets[remCat] += savings * (decimal)(weights[remCat] / remainingWeightSum);
            }
        }

        // ---- Local-search upgrade pass (Task 3 fills this in) ----
        RunUpgradePass(currentBuild, candidatesByCategory, sequence, weights, purpose, request.Budget, ref actualTotal);

        // ---- Build result slots from the final build (rank-1 only) ----
        foreach (var category in sequence)
        {
            var chosen = GetComponent(currentBuild, category);
            if (chosen == null) continue;

            result.Slots.Add(new SlotRecommendation
            {
                Category = category.ToString(),
                AllocatedBudget = Math.Round(allocatedBudgets[category], 2, MidpointRounding.AwayFromZero),
                Recommendations = new List<RankedComponent>
                {
                    new RankedComponent
                    {
                        Rank = 1,
                        Component = chosen.Adapt<ComponentDto>(),
                        PerformanceScore = Math.Round(_scorer.CalculateScore(chosen, purpose), 3)
                    }
                }
            });
        }

        result.ActualTotalPrice = actualTotal;
        result.Message = result.IsSuccess
            ? "Build generated successfully."
            : $"Incomplete build: could not fill {string.Join(", ", result.FailedSlots.Select(f => f.Category))}.";

        return result;
    }

    private void RunUpgradePass(
        BuildContext build,
        Dictionary<Category, List<Component>> candidatesByCategory,
        List<Category> sequence,
        Dictionary<Category, double> weights,
        string purpose,
        decimal budget,
        ref decimal actualTotal)
    {
        // Implemented in Task 3.
    }

    private static BuildContext CloneBuild(BuildContext b) => new()
    {
        Cpu = b.Cpu, Gpu = b.Gpu, Motherboard = b.Motherboard, Ram = b.Ram,
        Psu = b.Psu, Case = b.Case, Cooler = b.Cooler, Storage = b.Storage
    };

    private static Component? GetComponent(BuildContext b, Category c) => c switch
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
```

Keep the existing `AssignToContext` method as-is.

- [ ] **Step 3: Register the new types in DI**

In `src/Struct.API/Program.cs`, immediately after the line `builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();`, add:

```csharp
builder.Services.AddSingleton(new Struct.BLL.Core.Recommendation.Models.ObjectiveSettings());
builder.Services.AddScoped<Struct.BLL.Core.Recommendation.BuildObjective>();
```

- [ ] **Step 4: Verify the full suite is still green (28 tests)**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj`
Expected: PASS — 31 total (28 existing + 3 from Task 1), 0 failed.

- [ ] **Step 5: Verify the whole solution builds**

Run: `dotnet build Struct.sln`
Expected: Build succeeded, 0 Warnings, 0 Errors.

- [ ] **Step 6: Commit**

```bash
git add src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs \
        src/Struct.API/Program.cs \
        tests/Struct.Tests/RecommendationEngineTests.cs
git commit -m "Refactor: greedy by raw score, retain candidate pools, objective wired in

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Implement the local-search upgrade pass

**Files:**
- Modify: `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs` (fill `RunUpgradePass`)
- Modify: `tests/Struct.Tests/TestSupport/CatalogFactory.cs` (add `UpgradableCatalog`)
- Test: `tests/Struct.Tests/RecommendationEngineTests.cs`

- [ ] **Step 1: Add an upgradable catalog to the test support**

In `tests/Struct.Tests/TestSupport/CatalogFactory.cs`, add this method inside the class:

```csharp
    /// <summary>
    /// Coherent AM5 build whose GPU slot has a cheap option (RTX 4060) and a premium one (RTX 4090).
    /// At a 9000 budget the GPU slot budget (0.35·9000 = 3150) cannot afford the 4090, so greedy picks
    /// the 4060; the upgrade pass should then swap up to the 4090 using the leftover budget.
    /// </summary>
    public static List<Component> UpgradableCatalog()
    {
        var catalog = FullCoherentCatalog().Where(c => c.Category != Category.Gpu).ToList();
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4060")
            .Brand("NVIDIA").Price(1200)
            .Spec("VRAM", "8").Spec("CoreClock", "1830").Spec("TDP", "115").Spec("Length", "200"));
        catalog.Add(ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4090")
            .Brand("NVIDIA").Price(4000)
            .Spec("VRAM", "24").Spec("CoreClock", "2235").Spec("TDP", "450").Spec("Length", "304"));
        return catalog;
    }
```

- [ ] **Step 2: Write the failing tests**

In `tests/Struct.Tests/RecommendationEngineTests.cs`, add:

```csharp
    [Fact]
    public async Task UpgradePass_SpendsLeftoverBudget_ToUpgradeTheGpu()
    {
        // At 9000, greedy can't afford the 4090 in the GPU slot and picks the 4060.
        // The upgrade pass should swap up to the 4090 using leftover budget.
        var engine = EngineFor(CatalogFactory.UpgradableCatalog());

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 9000, Purpose = "Gaming" });

        Assert.True(result.IsSuccess, result.Message);
        var gpuSlot = result.Slots.Single(s => s.Category == "Gpu");
        Assert.Contains("4090", gpuSlot.Recommendations.First().Component.Name);
        Assert.True(result.ActualTotalPrice <= 9000m);
    }

    [Fact]
    public async Task UpgradePass_NeverExceedsBudget_NorBreaksCompatibility()
    {
        var compat = new CompatibilityEngine();
        var engine = new RecommendationEngine(
            new StubComponentRepository(CatalogFactory.UpgradableCatalog()),
            compat, new PerformanceScorer(),
            new BuildObjective(new PerformanceScorer(), new ObjectiveSettings()));

        var result = await engine.GenerateRecommendationAsync(new RecommendationRequest { Budget = 9000, Purpose = "Gaming" });

        Assert.True(result.ActualTotalPrice <= 9000m);
        // Re-validate every chosen component against the others in the final build.
        var ctx = new BuildContext();
        foreach (var slot in result.Slots)
        {
            var comp = CatalogFactory.UpgradableCatalog()
                .First(c => c.Name == slot.Recommendations.First().Component.Name);
            Assert.True(compat.CheckCompatibility(ctx, comp).IsCompatible, $"{slot.Category} incompatible");
            // mirror AssignToContext for the running context
            switch (comp.Category)
            {
                case Category.Cpu: ctx.Cpu = comp; break;
                case Category.Gpu: ctx.Gpu = comp; break;
                case Category.Motherboard: ctx.Motherboard = comp; break;
                case Category.Ram: ctx.Ram = comp; break;
                case Category.Psu: ctx.Psu = comp; break;
                case Category.Case: ctx.Case = comp; break;
                case Category.Cooler: ctx.Cooler = comp; break;
                case Category.Ssd: case Category.Hdd: ctx.Storage = comp; break;
            }
        }
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj --filter "FullyQualifiedName~UpgradePass"`
Expected: FAIL — `UpgradePass_SpendsLeftoverBudget_ToUpgradeTheGpu` finds "4060" (no upgrade yet) in the GPU slot, so the `Contains("4090")` assertion fails.

- [ ] **Step 4: Implement RunUpgradePass**

In `src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs`, replace the empty `RunUpgradePass` body with:

```csharp
    private void RunUpgradePass(
        BuildContext build,
        Dictionary<Category, List<Component>> candidatesByCategory,
        List<Category> sequence,
        Dictionary<Category, double> weights,
        string purpose,
        decimal budget,
        ref decimal actualTotal)
    {
        bool improved = true;
        while (improved)
        {
            improved = false;
            double currentObjective = _objective.Evaluate(build, weights, purpose);
            double bestDelta = 0;
            Category bestCategory = default;
            Component? bestSwap = null;

            foreach (var category in sequence)
            {
                var chosen = GetComponent(build, category);
                if (chosen == null) continue; // failed slot — nothing to upgrade
                if (!candidatesByCategory.TryGetValue(category, out var pool)) continue;

                foreach (var candidate in pool)
                {
                    if (candidate.Id == chosen.Id) continue;

                    decimal newTotal = actualTotal - chosen.Price + candidate.Price;
                    if (newTotal > budget) continue; // must stay within total budget

                    // Compatibility against the full build (CheckCompatibility clones then overwrites
                    // this category with the candidate, so the rest of the build is held fixed).
                    if (!_compatibilityEngine.CheckCompatibility(build, candidate).IsCompatible) continue;

                    var trial = CloneBuild(build);
                    AssignToContext(trial, candidate);
                    double delta = _objective.Evaluate(trial, weights, purpose) - currentObjective;

                    if (delta > bestDelta)
                    {
                        bestDelta = delta;
                        bestCategory = category;
                        bestSwap = candidate;
                    }
                }
            }

            if (bestSwap != null)
            {
                var old = GetComponent(build, bestCategory)!;
                actualTotal = actualTotal - old.Price + bestSwap.Price;
                AssignToContext(build, bestSwap);
                improved = true; // objective strictly increased; loop is bounded ⇒ terminates
            }
        }
    }
```

- [ ] **Step 5: Run the upgrade tests to verify they pass**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj --filter "FullyQualifiedName~UpgradePass"`
Expected: PASS (2 tests).

- [ ] **Step 6: Run the full suite**

Run: `dotnet test tests/Struct.Tests/Struct.Tests.csproj`
Expected: PASS — 33 total, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add src/Struct.BLL/Core/Recommendation/RecommendationEngine.cs \
        tests/Struct.Tests/TestSupport/CatalogFactory.cs \
        tests/Struct.Tests/RecommendationEngineTests.cs
git commit -m "Feat: local-search upgrade pass spends leftover budget within constraints

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Verify, document, and close out

**Files:**
- Create (vault): `~/Desktop/main/Claude/Claude+Obsidian/Struct/Refactoring/Analysis — Phase 3a.md`
- Modify (vault): `Refactoring Plan — Phases.md` (tick Phase 3a part 1)

- [ ] **Step 1: Full solution build + suite**

Run: `dotnet build Struct.sln && dotnet test tests/Struct.Tests/Struct.Tests.csproj`
Expected: Build 0 warnings / 0 errors; tests 33 passed / 0 failed.

- [ ] **Step 2: Write the Phase 3a analysis note in the Obsidian vault**

Mirror the format of `Analysis — Phase 2.md`: what changed (BuildObjective + bottleneck, plain-score greedy, rank-2 removed, local-search upgrade pass), evidence (test counts, build clean), decisions (removed util multiplier + DivideByZero risk; rank-2 deferred), and hand-off to Phase 3a part 2 (benchmark scoring) and Phase 3b (exact solver).

- [ ] **Step 3: Tick the plan**

In `Refactoring Plan — Phases.md`, mark Phase 3a part 1 (objective + upgrade pass + bottleneck) done and link this analysis note.

- [ ] **Step 4: Commit the code/doc state**

```bash
git add docs/superpowers/plans/2026-06-14-phase3a-upgrade-pass.md
git commit -m "Docs: Phase 3a part-1 implementation plan

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

(The vault notes live outside the repo and are saved directly.)

---

## Self-Review

- **Spec coverage:** §3.1 objective+config → Task 1; §3.1 bottleneck term → Task 1; §3.2 greedy baseline retained + local-search upgrade → Tasks 2–3; §3.4 algorithm-first (no external data) → whole plan; §3.5 tests (objective≥baseline implied by strict-improvement loop, stranded budget shrinks, stays compatible+in budget, bottleneck) → Tasks 1 & 3. Benchmark scoring (§3.3) is intentionally a separate later plan.
- **Placeholder scan:** none — `RunUpgradePass` is introduced as an explicit empty hook in Task 2 and filled with complete code in Task 3 (called out, not a hidden TODO).
- **Type consistency:** `BuildObjective(IPerformanceScorer, ObjectiveSettings)`, `Evaluate(BuildContext, IReadOnlyDictionary<Category,double>, string)`, `GetComponent`/`CloneBuild`/`AssignToContext`, and the 4-arg `RecommendationEngine` constructor are used identically across Tasks 1–3 and the test helper.

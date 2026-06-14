# Phase 3a (Part 2) — Benchmark-Backed Scoring (validate → swap) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Replace the hand-tuned CPU/GPU scores with real PassMark data (CPU Mark / G3D Mark), keeping the heuristic as fallback for unmatched parts, and produce a heuristic-vs-benchmark rank-correlation as thesis evidence.

**Architecture:** A pure BLL `BenchmarkScores` type loads the two CSVs, normalizes/matches names (CPU by full name; GPU by the chip in parentheses; strip `@ …GHz` clock suffixes), and enriches `Component.TechnicalSpecs["BenchmarkScore"]` with a per-category-normalized 0..1 value at seed time. The `PerformanceScorer` returns that value for CPU/GPU when present, else the existing heuristic. A validation test reports Spearman ρ over the real data.

**Verified recon:** with these rules, **CPU match = 91/93 (97%)**, **GPU match = 196/196 (100%)**. Files: `src/Struct.API/Extensions/Seeding/benchmarks/CPU_benchmark_v4.csv` (`cpuName`,`cpuMark`) and `GPU_benchmarks_v7.csv` (`gpuName`,`G3Dmark`).

---

## File Structure
- Create: `src/Struct.BLL/Core/Scoring/Benchmarks/BenchmarkScores.cs` — load/normalize/match/enrich (in BLL so it's unit-testable).
- Modify: `src/Struct.BLL/Core/Scoring/PerformanceScorer.cs` — CPU/GPU read `BenchmarkScore`, else heuristic.
- Modify: `src/Struct.API/Extensions/Seeding/DatabaseSeeder.cs` — enrich components before save.
- Modify: `src/Struct.API/Program.cs` — load CSVs, pass to seeder.
- Modify: `src/Struct.API/Struct.API.csproj` — copy the CSVs to output.
- Test: `tests/Struct.Tests/BenchmarkScoresTests.cs`, additions to `PerformanceScorerTests.cs`, `tests/Struct.Tests/BenchmarkValidationTests.cs`.

---

## Task 1 — `BenchmarkScores`: normalize, match, enrich (BLL, TDD)
- [ ] Tests: `Normalize` strips `@…GHz`/punctuation/case; `ExtractGpuChip` pulls the parenthesised chip; `Enrich` writes a per-category 0..1 `BenchmarkScore` for matched CPU/GPU and leaves unmatched parts untouched.
- [ ] Implement `BenchmarkScores` with: ctor from raw `name→mark` maps (keys normalized internally); `static FromCsvFiles(cpuPath,gpuPath)`; `Enrich(IReadOnlyList<Component>)` (match → per-category max → store ratio).
- [ ] Run → green. Commit.

## Task 2 — Scorer swap (BLL, TDD)
- [ ] Tests: a CPU/GPU component carrying `BenchmarkScore` is scored by it (≈ sqrt of stored ratio); without it, the heuristic still applies (existing tests stay green).
- [ ] In `ScoreCpu`/`ScoreGpu`, first check `specs["BenchmarkScore"]` → `Math.Sqrt(Clamp(value,0,1))`; else current heuristic.
- [ ] Run full suite → green. Commit.

## Task 3 — Seed-time enrichment wiring (API)
- [ ] `Program.cs`: build `BenchmarkScores.FromCsvFiles(cpu,gpu)` from `ContentRootPath/Extensions/Seeding/benchmarks/...`; pass to seeder.
- [ ] `DatabaseSeeder.SeedFromDirectoryAsync(path, BenchmarkScores? benchmarks=null)`: after assembling components, `benchmarks?.Enrich(uniqueComponents)` before save.
- [ ] `.csproj`: replace the bare `<Folder>` with `<Content ... CopyToOutputDirectory="PreserveNewest">` for the two CSVs.
- [ ] `dotnet build Struct.sln` → 0/0. Commit.

## Task 4 — Validation study (Spearman ρ) + finish
- [ ] `BenchmarkValidationTests`: load the real catalog json + CSVs (locate repo root by walking up from the test base dir), score matched CPUs/GPUs both ways, compute Spearman ρ, assert ρ ≥ 0.6, and print the value.
- [ ] Full build + suite green. Write vault `Analysis — Phase 3a part 2`; mark Phase 3a part 2 done in the plan + Defense Prep note. Commit; finish branch.

---

## Self-Review
- Coverage: load/match/normalize → T1; swap with fallback → T2; seed wiring + output copy → T3; validation evidence → T4. Backward-compatible: components without `BenchmarkScore` hit the heuristic, so existing 35 tests stay green.
- Types: `BenchmarkScores(IReadOnlyDictionary<string,double> cpu, IReadOnlyDictionary<string,double> gpu)`, `FromCsvFiles(string,string)`, `Enrich(IReadOnlyList<Component>)`, `Normalize(string)`, `ExtractGpuChip(string)` — consistent across tasks.

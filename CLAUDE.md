# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Struct is a PJATK engineering-thesis project: an ASP.NET Core (.NET 9) Web API that recommends complete, compatible PC builds for a given budget and purpose. The **recommendation engine is the agreed core feature** — algorithm quality and its defensibility matter more than breadth of features. A Python scraper (`scraper/`) produces the component catalog; a C# seeder loads it into Postgres.

## Commands

```bash
# Build / test (run from repo root)
dotnet build Struct.sln
dotnet test tests/Struct.Tests/Struct.Tests.csproj
dotnet test --filter "FullyQualifiedName~RecommendationEngineTests"   # one class
dotnet test --filter "Name=Gaming_PrefersX3D_Over_24CoreI9"           # one test

# Run the API locally (expects Postgres reachable per appsettings.json)
dotnet run --project src/Struct.API           # Swagger UI at /swagger in Development

# Full stack via Docker (API + Postgres, reads .env for POSTGRES_*)
docker compose up --build

# EF Core migrations — migrations live in Struct.DAL, startup project is Struct.API
dotnet ef migrations add <Name> --project src/Struct.DAL --startup-project src/Struct.API
dotnet ef database update      --project src/Struct.DAL --startup-project src/Struct.API
```

Note the Postgres port mismatch: `appsettings.json` points at `localhost:5432`, but `compose.yaml` publishes the DB container on host port **5322**. Running the API on the host against the Dockerized DB requires overriding the connection string's port.

## Architecture

Classic 3-layer split, each a separate project referenced one direction only (`API → BLL → DAL`):

- **`src/Struct.API`** — controllers, `Program.cs` (DI registration + startup seeding), and the seeding pipeline. `AlgorithmsController` exposes the two algorithm endpoints: `POST /api/algorithms/recommend` and `POST /api/algorithms/validate-build`. The other controllers are standard CRUD over the entities.
- **`src/Struct.BLL`** — business logic. Two distinct halves:
  - `Services/` — CRUD services behind interfaces, mapping entities ↔ DTOs with **Mapster** (`.Adapt<T>()`, convention-based, no central config).
  - `Core/` — **the engines, and the heart of the thesis.** All three are pure classes behind interfaces (`ICompatibilityEngine`, `IPerformanceScorer`, `IRecommendationEngine`) with zero infrastructure dependencies — which is why they are unit-testable without a database.
- **`src/Struct.DAL`** — EF Core (`AppDbContext`), entity models, repositories, and migrations. Postgres via Npgsql; `Component.TechnicalSpecs` is a `Dictionary<string,string>` stored as `jsonb`.

### How the engines fit together

`RecommendationEngine.GenerateRecommendationAsync` is the orchestrator and the only place the three engines combine:

1. A **weight matrix** and a **fill sequence** per purpose (`Gaming` / `Work` / `Office`) are hardcoded as static dictionaries. The sequence is deliberately ordered (e.g. GPU-first for Gaming) so that by the time the PSU slot is filled, CPU+GPU TDP is known, and by the case slot, GPU length is known.
2. For each category slot it loads candidates, filters by an allocated **slot budget** (with 10% tolerance, capped by remaining global budget), filters by `CompatibilityEngine.CheckCompatibility` against the partial `BuildContext`, ranks survivors by `PerformanceScorer.CalculateScore`, and picks the best.
3. **Budget redistribution**: leftover (or overspend) from a slot cascades proportionally onto the remaining slots' budgets by their weights.

- `CompatibilityEngine` is **side-effect-free**: it clones the `BuildContext`, assigns the candidate, and runs socket / RAM / power / physical-clearance rules. A **missing spec means the check is skipped (silent pass)** — intentional, for dirty scraped data. Rules read string specs out of `TechnicalSpecs`.
- `PerformanceScorer` is **purpose-aware and currently hand-tuned**: ~50 magic numbers, name-substring tier lists (e.g. GPU "4090"), and a `Normalize` that applies a √ curve. Scores are only comparable *within* a category, not across.

### Component catalog flow

`scraper/generate_db.py` (PCPartPicker via the unofficial `pcpartpicker` lib, USD→PLN ×4, modern-socket filter) writes `clean_database.json`. At API startup, `Program.cs` runs `DatabaseSeeder.SeedFromDirectoryAsync` against `src/Struct.API/Extensions/Seeding/clean-db/clean_database.json`. The seeder dispatches each row to a per-category `IComponentParser` (`Parsers/`) that normalizes raw fields into the `TechnicalSpecs` dictionary the engines later read. **Seeding only runs into an empty `Components` table** — it must never delete existing components (saved builds reference them).

## Active refactoring context

This codebase is mid-refactor following a code review. **Read `~/Desktop/main/Claude/Claude+Obsidian/Struct/Refactoring/Refactoring Plan — Phases.md`** (Obsidian vault) before non-trivial work — it has the phase plan and rationale; the `Feedbacks/` folder beside it has the detailed findings.

- `tests/Struct.Tests` is a **characterization suite**: it pins *current* behavior, including known bugs. Tests named `_KnownBug` / `_KnownGap` assert today's incorrect behavior on purpose. When you fix one of those bugs, the corresponding test is **expected to fail** — update it as part of the fix; that diff is the record of what changed. Run `dotnet test` to confirm the baseline is green before changing engine code.
- Known issues actively being addressed (don't "discover" them as new): partial builds drop failed slots and report `ActualTotalPrice = 0`; displayed score can exceed 1.0; greedy allocation strands budget; cooler-height/RAM-slot compatibility unchecked; `ParseDouble` concatenates all digits in a string; invalid `Purpose` silently defaults to Gaming.
- There is **no authentication yet** — passwords are placeholder strings and controllers are open. Don't assume an auth/identity context exists.

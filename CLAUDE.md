# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Struct is a PJATK engineering-thesis project: an ASP.NET Core (.NET 9) Web API that recommends complete, compatible PC builds for a given budget and purpose. The **recommendation engine is the agreed core feature** — algorithm quality and its defensibility matter more than breadth of features. The component catalog is a hand-curated `data/catalog.xlsx` (local only, not in git), converted to JSON by `scraper/excel_to_catalog.py`; a C# seeder loads it into Postgres.

## Commands

```bash
# Build / test (run from repo root)
dotnet build Struct.sln
dotnet test tests/Struct.Tests/Struct.Tests.csproj
dotnet test --filter "FullyQualifiedName~RecommendationEngineTests"   # one class
dotnet test --filter "FullyQualifiedName~Gaming_PrefersX3D_Over_24CoreI9"   # one test (xUnit has no Name= filter)

# Run the API locally (expects Postgres reachable; the connection string with password lives in user-secrets)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=struct_db;Username=postgres;Password=<pw>" --project src/Struct.API
dotnet run --project src/Struct.API           # Swagger UI at /swagger in Development

# Full stack via Docker (API + Postgres, reads .env for POSTGRES_*)
docker compose up --build

# Frontend (from frontend/): lint, unit tests (vitest), E2E (Playwright; needs the API up via docker compose)
npm run lint && npm test
npm run test:e2e

# Catalog: xlsx -> seed JSON, and its tests (from scraper/; requirements.txt is uv-locked with hashes from requirements.in)
python excel_to_catalog.py --snapshot-date YYYY-MM-DD      # data/catalog.xlsx -> data/clean_database.json
pytest

# Evaluation harness: greedy vs greedy+LS vs exact grid + TopK/λ sweeps → data/eval/*.csv (gitignored)
dotnet run -c Release --project tools/Struct.Evaluation [path/to/clean_database.json]

# HTTP smoke checks against a running API
scripts/smoke.sh [http://localhost:8080]

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

`RecommendationEngine.GenerateRecommendationAsync` loads candidates for every slot of the purpose's profile, hands them to an `IBuildSolver` as a `BuildProblem`, and shapes the `BuildSolution` into the API result. Three solvers, all in DI: `GreedySolver` (baseline), `GreedyLocalSearchSolver` (greedy + upgrade pass; the one the live API uses via `IBuildSolver`), `ExactBuildSolver` (branch-and-bound over top-K, the reference optimum for evaluation). The greedy path:

1. A **weight matrix** and a **fill sequence** per purpose (`Gaming` / `Work` / `Office`) come from `appsettings.json` (`Engine:Recommendation:Profiles`), as do the slot tolerance, min budget, objective λ/θ, solver TopK and PSU formula. Settings are plain records bound once at startup (`ErrorOnUnknownConfiguration`); tests bind the same file via `TestConfig`. The sequence is deliberately ordered (e.g. GPU-first for Gaming) so that by the time the PSU slot is filled, CPU+GPU TDP is known, and by the case slot, GPU length is known.
2. For each category slot it filters candidates by an allocated **slot budget** (with `SlotBudgetTolerance`, capped by remaining global budget), filters by `CompatibilityEngine.CheckCompatibility` against the partial `BuildContext`, ranks survivors by `PerformanceScorer.CalculateScore`, and picks the best.
3. **Budget redistribution**: leftover (or overspend) from a slot cascades proportionally onto the remaining slots' budgets by their weights.

- `CompatibilityEngine` is **side-effect-free**: it clones the `BuildContext`, assigns the candidate, and runs socket / RAM / power / physical-clearance rules. A **missing spec means the check is skipped (silent pass)** — intentional, for dirty scraped data. Rules read string specs out of `TechnicalSpecs`.
- `PerformanceScorer` is **purpose-aware and currently hand-tuned**: ~50 magic numbers, name-substring tier lists (e.g. GPU "4090"), and a `Normalize` that applies a √ curve. Scores are only comparable *within* a category, not across.

### Component catalog flow

`data/catalog.xlsx` (hand-curated, one sheet per category, PLN prices) is the source of truth. **`data/` is gitignored on purpose: the repo is public and the dataset is the author's own work — never commit it or anything generated from it.** `scraper/excel_to_catalog.py` validates it and writes `data/clean_database.json` (`--snapshot-date` = the day prices were collected). The committed `Seeding/clean-db/clean_database.json` is still the old scraped catalog. The old PCPartPicker/Apify scrapers sit in `scraper/legacy/` for provenance only. At API startup, `Program.cs` runs `DatabaseSeeder.SeedFromDirectoryAsync` against `src/Struct.API/Extensions/Seeding/clean-db/clean_database.json`. The seeder dispatches each row to a per-category `IComponentParser` (`Parsers/`) that normalizes raw fields into the `TechnicalSpecs` dictionary the engines later read. **Seeding only runs into an empty `Components` table** — it must never delete existing components (saved builds reference them).

## Conventions

- **Keep code comments minimal.** Comment only a short *why* the code can't show. No changelog/history comments ("fixed in…", "used to…"), no restating what the code does — the git diff is the record.
- **GitHub flow:** work on a branch, open a PR into `main`, merge only when CI (`.github/workflows/ci.yml` orchestrates reusable `secrets.yml`, `backend.yml`, `frontend.yml`, `scraper.yml`, `e2e.yml`; the single required check is `CI result`; `sonar.yml` (SonarQube Cloud, project `some1less_struct.io`) is informational and not part of it) is green. The user merges.
- **npm on macOS drops Linux-only optional deps (`@emnapi/*`) from `package-lock.json`**, which breaks `npm ci` in CI. After any `npm install`, regenerate the lock in Linux: `docker run --rm -v <copy-of-frontend>:/app -w /app node:24 npm install --package-lock-only`, then confirm `npm ci --dry-run` there.
- **Commits: never add Claude as co-author.** No `Co-Authored-By: Claude` trailer and no "Generated with Claude Code" lines in commit messages or PR bodies.

## Active refactoring context

This codebase is mid-refactor following a code review. **Read `~/Desktop/main/Claude/Claude+Obsidian/Struct/Refactoring/Refactoring Plan — Phases.md`** (Obsidian vault) before non-trivial work — it has the phase plan and rationale; the `Feedbacks/` folder beside it has the detailed findings.

- `tests/Struct.Tests` is a **characterization suite**: it pins *current* behavior, including known bugs. Tests named `_KnownBug` / `_KnownGap` assert today's incorrect behavior on purpose. When you fix one of those bugs, the corresponding test is **expected to fail** — update it as part of the fix; that diff is the record of what changed. Run `dotnet test` to confirm the baseline is green before changing engine code.
- Known issues actively being addressed (don't "discover" them as new): partial builds drop failed slots and report `ActualTotalPrice = 0`; displayed score can exceed 1.0; greedy allocation strands budget; cooler-height/RAM-slot compatibility unchecked.
- There is **no authentication yet** — passwords are placeholder strings and controllers are open. Don't assume an auth/identity context exists.

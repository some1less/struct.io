# Struct Recommendation Visualizer — Design (2026-06-15)

A small React frontend that **visualizes the recommendation engine** for the thesis demo. Not
enterprise UI — the goal is to *show how the engine works*: the recommended build, per-component
details and scores, budget usage, and the CPU↔GPU bottleneck.

## Scope

- **In:** the recommend flow only. Input (budget + purpose) → full result visualization.
- **Out:** validate-build screen, saving builds, auth, live API wiring (stubbed behind a flag,
  mock by default).

## Stack

- Vite + React + TypeScript, Tailwind, shadcn/ui. Lives in a new `frontend/` folder.
- Matches the stack recorded in the vault note (`React + Tailwind + shadcn`).
- Designed with the `ui-ux-pro-max` skill.

## Data

- Builds against a **mock fixture** `src/data/mockResult.ts` — a realistic `RecommendationResult`
  using real catalog names/specs.
- One `src/lib/api.ts` exposes `getRecommendation(req): Promise<RecommendationResult>`, with a
  `USE_LIVE` flag. Mock by default; switching to live `POST /api/algorithms/recommend` is a
  one-line change later.

## API contract (mirrors the .NET DTOs)

```ts
type RecommendationRequest = { budget: number; purpose: "Gaming" | "Work" | "Office" };

type RecommendationResult = {
  purpose: string;
  totalBudget: number;
  actualTotalPrice: number;
  isSuccess: boolean;
  message: string;
  slots: SlotRecommendation[];
  failedSlots: { category: string; reason: string }[];
};
type SlotRecommendation = {
  category: string;
  allocatedBudget: number;
  recommendations: RankedComponent[];
};
type RankedComponent = {
  rank: number;
  performanceScore: number; // 0..1, sqrt-curved
  component: { id: number; name: string; category: string; brand: string; price: number;
               technicalSpecs: Record<string, string> };
};
```

Note: the API does **not** return a bottleneck number. The UI reproduces the engine's own term
client-side from the CPU and GPU slots' `performanceScore`:
`gap = |cpuScore − gpuScore|`, threshold `0.15` (matches `ObjectiveSettings`). This keeps the UI
honest and needs no backend change.

## Layout (single page, two zones)

1. **Control panel (top).** Budget input + slider (min **2400 PLN**, enforced like the API),
   Purpose segmented control (Gaming / Work / Office), "Build it" button. Purpose sets an accent theme.

2. **Result (animated in below):**
   - **Summary bar** — purpose badge; **budget-utilization meter** (`actualTotalPrice / totalBudget`)
     calling out leftover ("stranded budget"); total price; success/partial state.
   - **Bottleneck meter (centerpiece)** — CPU-vs-GPU balance gauge from the two slots'
     `performanceScore`; status chip **Balanced / CPU-bound / GPU-bound** using the 0.15 threshold.
   - **Build breakdown** — one `Card` per slot (CPU, GPU, Mobo, RAM, Storage, PSU, Case, Cooler):
     name + brand, price vs `allocatedBudget`, a **performance-score bar** (0–1), expandable spec
     sheet from `technicalSpecs`. Ranks 2–3 (if present) shown as collapsible "alternatives".
   - **Failed slots** — warning panel listing `failedSlots` (category + reason) when partial.

## Components (isolation)

- `App` — holds request state + result, orchestrates.
- `ControlPanel` — input, emits a `RecommendationRequest`.
- `SummaryBar`, `BottleneckMeter`, `SlotCard`, `SpecSheet`, `FailedSlots`, `ScoreBar`,
  `BudgetMeter` — presentational, props-only, each independently testable.
- `lib/api.ts` (data access), `lib/bottleneck.ts` (pure: scores → status), `data/mockResult.ts`.

## Success criteria

- `npm run dev` shows the full visualization against the mock for each purpose.
- Bottleneck status and budget meter update correctly with the data.
- Switching `USE_LIVE` to true makes it call the real endpoint (untested here, but wired).

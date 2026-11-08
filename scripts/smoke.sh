#!/usr/bin/env bash
# HTTP smoke checks against a running API (docker compose up).
# Usage: scripts/smoke.sh [base-url]; set SMOKE_REPORT=<file> to also write a Markdown table.
set -uo pipefail
BASE="${1:-http://localhost:8080}"

for _ in $(seq 1 60); do curl -fs "$BASE/api/config" >/dev/null && break; sleep 2; done

fail=0
rows=()
record() { # name, expected, actual, pass|fail
  if [[ "$4" == pass ]]; then echo "ok    $1 ($3)"; else echo "FAIL  $1: expected $2, got $3"; fail=1; fi
  rows+=("| $1 | $2 | $3 | $4 |")
}
check() { # name, expected status, curl args...
  local name=$1 expected=$2; shift 2
  local status
  status=$(curl -s -o /dev/null -w '%{http_code}' "$@")
  record "$name" "$expected" "$status" "$([[ "$status" == "$expected" ]] && echo pass || echo fail)"
}

check "GET /api/config" 200 "$BASE/api/config"
for p in Gaming Work Office; do
  check "recommend $p 12000" 200 -X POST "$BASE/api/algorithms/recommend" \
    -H 'Content-Type: application/json' -d "{\"budget\":12000,\"purpose\":\"$p\"}"
done
check "unknown purpose" 400 -X POST "$BASE/api/algorithms/recommend" \
  -H 'Content-Type: application/json' -d '{"budget":12000,"purpose":"banana"}'
check "budget below minimum" 400 -X POST "$BASE/api/algorithms/recommend" \
  -H 'Content-Type: application/json' -d '{"budget":1000,"purpose":"Gaming"}'

if curl -fs "$BASE/api/config" | grep -q '"minBudget"'; then
  record "config has minBudget" "field" "present" pass
else
  record "config has minBudget" "field" "missing" fail
fi

if [[ -n "${SMOKE_REPORT:-}" ]]; then
  {
    printf '### API smoke\n\n| Check | Expected | Actual | Result |\n|---|---:|---:|---|\n'
    printf '%s\n' "${rows[@]}"
    printf '\n'
  } > "$SMOKE_REPORT"
fi

exit $fail

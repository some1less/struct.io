#!/usr/bin/env bash
# HTTP smoke checks against a running API (docker compose up). Usage: scripts/smoke.sh [base-url]
set -uo pipefail
BASE="${1:-http://localhost:8080}"

for _ in $(seq 1 60); do curl -fs "$BASE/api/config" >/dev/null && break; sleep 2; done

fail=0
check() { # name, expected status, curl args...
  local name=$1 expected=$2; shift 2
  local status
  status=$(curl -s -o /dev/null -w '%{http_code}' "$@")
  if [[ "$status" == "$expected" ]]; then echo "ok    $name ($status)"; else echo "FAIL  $name: expected $expected, got $status"; fail=1; fi
}

check "GET /api/config" 200 "$BASE/api/config"
for p in Gaming Work Office; do
  check "recommend $p 12000" 200 -X POST "$BASE/api/algorithms/recommend" \
    -H 'Content-Type: application/json' -d "{\"budget\":12000,\"purpose\":\"$p\"}"
done
check "unknown purpose -> 400" 400 -X POST "$BASE/api/algorithms/recommend" \
  -H 'Content-Type: application/json' -d '{"budget":12000,"purpose":"banana"}'
check "budget below minimum -> 400" 400 -X POST "$BASE/api/algorithms/recommend" \
  -H 'Content-Type: application/json' -d '{"budget":1000,"purpose":"Gaming"}'

if curl -fs "$BASE/api/config" | grep -q '"minBudget"'; then echo "ok    config has minBudget"; else echo "FAIL  config has no minBudget"; fail=1; fi

exit $fail

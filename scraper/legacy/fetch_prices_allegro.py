"""
Fetch REAL Allegro (PLN) prices for the categories the USD-based seeder fix can't price well:
RAM (USD source is scalper-noisy) and storage SSD/HDD (no source price). CPU/GPU/etc. already use
real PCPartPicker prices via the seeder, so they're skipped here.

Strategy: dedup to spec buckets, query the working proxy actor `parseforge/allegro-scraper` (one run
per bucket, started in PARALLEL), keep NEW offers whose title matches the bucket's spec tokens, reject
whole-PC bundles, take a robust median, and write `PricePln` back into clean_database.json for every part
in the bucket (flat per spec bucket — same spec ≈ same price). A confidence gate leaves a part untouched
(falls back to its current price) when too few clean offers are found.

Run:  ./venv/bin/python fetch_prices_allegro.py            # dry run: buckets + cost, no spend
      ./venv/bin/python fetch_prices_allegro.py --go       # scrape + write
"""
import json, os, re, statistics, sys, time, urllib.request
from collections import defaultdict

ACTOR = "parseforge~allegro-scraper"
COST_PER_RESULT = 0.007
SAFETY = 0.30          # leave this much of the free budget untouched
HERE = os.path.dirname(__file__)
TOKEN = open(os.path.join(HERE, ".apify_token")).read().strip()
CATPATH = os.path.join(HERE, "..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")
TARGETS = ["Ssd", "Hdd"]  # RAM uses its real PCPartPicker price (reflects the 2026 DRAM surge)
GO = "--go" in sys.argv


def api(url, method="GET", body=None):
    data = json.dumps(body).encode() if body is not None else None
    headers = {"Authorization": f"Bearer {TOKEN}"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    with urllib.request.urlopen(req, timeout=90) as r:
        return json.loads(r.read().decode())


def remaining_budget():
    d = api("https://api.apify.com/v2/users/me/limits")["data"]
    return d["limits"]["maxMonthlyUsageUsd"] - d["current"]["monthlyUsageUsd"]


# ------------------------------------------------------------------ bucketing
def cap_label(gb):
    gb = int(gb)
    return f"{gb // 1000}TB" if gb >= 1000 and gb % 1000 == 0 else f"{gb}GB"


def build_buckets(cat_rows):
    """Return list of buckets: {key, phrase, tokens(required substrings), parts[]}."""
    buckets = {}
    for x in cat_rows:
        c, t = x["Category"], x["TechnicalSpecs"]
        if c == "Ram":
            key = (t.get("Type"), t.get("Capacity"), t.get("Speed"), t.get("Modules"))
            phrase = f"{t.get('Type')} {t.get('Speed')} {t.get('Capacity')}GB"
            tokens = [str(t.get("Type", "")).lower(), str(t.get("Speed", "")),
                      f"{t.get('Capacity')}gb"]
        elif c == "Ssd":
            nvme = "nvme" if "PCIe" in str(t.get("Interface", "")) or "NVMe" in str(t.get("Interface", "")) else "sata"
            cl = cap_label(t.get("Capacity", 0))
            key = ("Ssd", nvme, cl)
            phrase = f"dysk SSD {'M.2 NVMe' if nvme=='nvme' else 'SATA'} {cl}"
            tokens = ["ssd", cl.lower()]
        elif c == "Hdd":
            cl = cap_label(t.get("Capacity", 0))
            key = ("Hdd", cl)
            phrase = f"dysk HDD 3.5 {cl}"
            tokens = [cl.lower()]
        else:
            continue
        b = buckets.setdefault(key, {"key": key, "cat": c, "phrase": phrase, "tokens": tokens, "parts": []})
        b["parts"].append(x)
    return list(buckets.values())


# ------------------------------------------------------------------ filtering
BUNDLE = re.compile(r"\b(zestaw|komputer|pc gaming|ryzen|geforce|radeon|rtx|gtx|core i\d|laptop|notebook)\b", re.I)


def clean_price(bucket, items):
    """Median PLN of NEW, spec-matching, non-bundle offers. None if < 2 clean offers."""
    good = []
    for it in items:
        p = it.get("price")
        if not isinstance(p, (int, float)) or p <= 0:
            continue
        title = str(it.get("title", "")).lower()
        cond = str(it.get("condition", "")).lower()
        if cond and "us" in cond and "new" not in cond:      # skip used
            continue
        if BUNDLE.search(title) and bucket["cat"] in ("Ram", "Ssd", "Hdd"):
            continue
        if not all(tok and tok in title for tok in bucket["tokens"]):
            continue
        good.append(p)
    if len(good) < 2:
        return None, len(good)
    good.sort()
    if len(good) >= 6:                                       # drop 1 extreme each side
        good = good[1:-1]
    return round(statistics.median(good), 2), len(good)


# ------------------------------------------------------------------ Apify runs
def start(phrase, max_items):
    r = api(f"https://api.apify.com/v2/acts/{ACTOR}/runs", "POST",
            {"searchQuery": phrase, "maxItems": max_items, "sortBy": ""})["data"]
    return r["id"], r["defaultDatasetId"]


def wait_all(run_ids):
    pending = set(run_ids)
    for _ in range(120):
        if not pending:
            break
        time.sleep(5)
        for rid in list(pending):
            st = api(f"https://api.apify.com/v2/actor-runs/{rid}")["data"]["status"]
            if st in ("SUCCEEDED", "FAILED", "ABORTED", "TIMED-OUT"):
                pending.discard(rid)


def main():
    catalog = json.load(open(CATPATH))
    rows = [x for x in catalog if x["Category"] in TARGETS]
    buckets = []
    for cat in TARGETS:
        buckets += build_buckets([x for x in rows if x["Category"] == cat])

    # Skip buckets already fully priced from a previous run (don't pay twice).
    buckets = [b for b in buckets if not all("PricePln" in p for p in b["parts"])]

    rem = remaining_budget()
    # pick max_items so the whole job stays under (remaining - safety)
    affordable = max(1, int((rem - SAFETY) / (COST_PER_RESULT * len(buckets))))
    max_items = min(12, affordable)
    est = len(buckets) * max_items * COST_PER_RESULT

    print(f"remaining Apify budget: ${rem:.2f}  (safety ${SAFETY})")
    print(f"buckets: {len(buckets)}  ", {c: sum(1 for b in buckets if b['cat'] == c) for c in TARGETS})
    print(f"maxItems/bucket: {max_items}  ->  est cost ${est:.2f}")
    for b in buckets:
        print(f"  [{b['cat']}] {b['phrase']:30} covers {len(b['parts'])} part/s")
    if not GO:
        print("\nDRY RUN — re-run with --go to scrape + write.")
        return
    if est > rem - 0.05:
        print("ABORT: estimate exceeds remaining budget."); return

    CHUNK = 18  # stay under the free-tier 25-concurrent-run limit
    updated = skipped = 0
    print("\nresults:")
    for i in range(0, len(buckets), CHUNK):
        group = buckets[i:i + CHUNK]
        for b in group:
            try:
                b["run"], b["ds"] = start(b["phrase"], max_items)
            except Exception as e:
                b["ds"] = None
                print(f"  [start failed] {b['phrase']}: {e}")
        wait_all([b["run"] for b in group if b.get("run")])
        for b in group:
            if not b.get("ds"):
                skipped += len(b["parts"]); continue
            try:
                items = api(f"https://api.apify.com/v2/datasets/{b['ds']}/items?clean=true&format=json")
            except Exception:
                items = []
            price, n = clean_price(b, items)
            if price is None:
                skipped += len(b["parts"])
                print(f"  [{b['cat']}] {b['phrase']:30} -> NO confident match ({n} clean) — left as-is")
                continue
            for p in b["parts"]:
                p["PricePln"] = price
                p["PriceSource"] = "allegro"
                updated += 1
            print(f"  [{b['cat']}] {b['phrase']:30} -> {price} PLN  (from {n} offers, {len(b['parts'])} part/s)")

    json.dump(catalog, open(CATPATH, "w"), ensure_ascii=False, indent=2)
    print(f"\nwrote {updated} PricePln values ({skipped} left to fallback). budget now ${remaining_budget():.2f}")


if __name__ == "__main__":
    main()

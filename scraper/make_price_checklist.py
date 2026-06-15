"""
Generate a MANUAL price-collection checklist for CPU / GPU / RAM.

Dedups 120 parts -> ~72 rows: CPU per model, GPU per chipset, RAM per
(type,capacity,speed,modules) bucket. Each row has a ready Allegro search link
filtered to NEW offers. Open the link, read the typical price, type it into the
`price_pln` column. `apply_price_checklist.py` then writes those prices back into
clean_database.json (scaling board-partner / brand variants by their existing ratio).

Run:  ./venv/bin/python make_price_checklist.py   ->  writes price_checklist.csv
"""
import csv, json, os, re, urllib.parse
from collections import defaultdict

CAT = json.load(open(os.path.join(os.path.dirname(__file__), "..", "src", "Struct.API",
                "Extensions", "Seeding", "clean-db", "clean_database.json")))
OUT = os.path.join(os.path.dirname(__file__), "price_checklist.csv")


def allegro_url(phrase):
    q = urllib.parse.urlencode({"string": phrase, "stan": "nowe"})  # stan=nowe = NEW only
    return f"https://allegro.pl/listing?{q}"


def cpu_phrase(name):
    n = re.sub(r"\s*\(.*?\)\s*", " ", name)
    n = re.sub(r"\s+\d+(\.\d+)?\s*GHz.*$", "", n)   # drop "4.2 GHz 8-Core Processor"
    return re.sub(r"\s+", " ", n).strip()


rows = []

# --- CPU: one row per model ---
for x in (p for p in CAT if p["Category"] == "Cpu"):
    rows.append({"category": "Cpu", "group_key": x["Name"],
                 "search_phrase": cpu_phrase(x["Name"]), "n_parts": 1,
                 "example_part": x["Name"]})

# --- GPU: one row per chipset ---
gpu = defaultdict(list)
for x in (p for p in CAT if p["Category"] == "Gpu"):
    gpu[x["TechnicalSpecs"].get("Chipset", x["Name"])].append(x)
for chip, parts in gpu.items():
    vram = parts[0]["TechnicalSpecs"].get("VRAM", "")
    rows.append({"category": "Gpu", "group_key": chip,
                 "search_phrase": f"{chip} {vram}GB".strip(), "n_parts": len(parts),
                 "example_part": parts[0]["Name"]})

# --- RAM: one row per (type, capacity, speed, modules) bucket ---
ram = defaultdict(list)
for x in (p for p in CAT if p["Category"] == "Ram"):
    t = x["TechnicalSpecs"]
    ram[(t.get("Type"), t.get("Capacity"), t.get("Speed"), t.get("Modules"))].append(x)
for (typ, cap, spd, mods), parts in ram.items():
    key = f"{typ} {cap}GB {spd} ({mods}x{int(int(cap)//max(1,int(mods)))}GB)"
    rows.append({"category": "Ram", "group_key": key,
                 "search_phrase": f"{typ} {spd} {cap}GB", "n_parts": len(parts),
                 "example_part": parts[0]["Name"]})

order = {"Cpu": 0, "Gpu": 1, "Ram": 2}
rows.sort(key=lambda r: (order[r["category"]], r["group_key"]))

with open(OUT, "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["category", "group_key", "search_phrase", "n_parts", "price_pln", "allegro_url", "example_part"])
    for r in rows:
        w.writerow([r["category"], r["group_key"], r["search_phrase"], r["n_parts"],
                    "", allegro_url(r["search_phrase"]), r["example_part"]])

n = {c: sum(1 for r in rows if r["category"] == c) for c in ("Cpu", "Gpu", "Ram")}
print(f"wrote {len(rows)} rows -> {OUT}")
print(f"  CPU {n['Cpu']} (per model) · GPU {n['Gpu']} (per chipset) · RAM {n['Ram']} (per spec bucket)")
print("\nfirst rows of each category:")
for cat in ("Cpu", "Gpu", "Ram"):
    print(f"  [{cat}]")
    for r in [x for x in rows if x["category"] == cat][:3]:
        print(f"     {r['search_phrase']:34} (covers {r['n_parts']} part/s)")

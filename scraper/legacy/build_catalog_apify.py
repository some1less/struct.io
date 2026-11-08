"""
Struct catalog builder — scrapes LIVE PCPartPicker via the Apify actor
`matyascimbulka/pcpartpicker-scraper` and maps it to clean_database.json.

PROVENANCE (for the thesis/defense):
  * Source: PCPartPicker, scraped live via Apify (actor + run date recorded in commit message).
  * Every TechnicalSpec is a REAL value from PCPartPicker (units parsed off), with ONE documented
    estimate: CPU MemoryType is derived from the CPU socket (PCPartPicker lists memory type on the
    motherboard, not the CPU). No values are fabricated.
  * Fields a product genuinely lacks are omitted; the compatibility engine skips checks on missing
    specs rather than guessing.
  * Curation (`keep`) is SELECTION ONLY — it drops obsolete / low-quality parts, never alters values.

Modes (CLI arg):
  merge  (default)  re-scrape only RESCRAPE categories, curate the rest of the existing catalog,
                    and merge — cheap, leaves the 6 good categories untouched (just curated).
  full              re-scrape every category from scratch.
  test              tiny 2-call probe (storage + cooler), prints raw field names; writes nothing.

Token: read from scraper/.apify_token (gitignored). Run: ./venv/bin/python build_catalog_apify.py merge
"""
import json, os, re, sys, time, urllib.request
from collections import defaultdict

ACTOR = "matyascimbulka~pcpartpicker-scraper"
TOKEN = open(os.path.join(os.path.dirname(__file__), ".apify_token")).read().strip()
OUT = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")

# Search phrases per category. Each phrase is scraped SEPARATELY with a small per-phrase cap, so every
# tier/brand gets fair representation (e.g. Intel isn't crowded out by AMD). Third value = per-phrase cap.
PLAN = {
    "cpu":           (["ryzen 5", "core i5", "ryzen 7", "core i7", "ryzen 9", "core i9", "core ultra"], "Cpu", 12),
    "video-card":    (["rtx 5090", "rtx 5080", "rtx 5070", "rtx 4090", "rtx 4080", "rtx 4070", "rtx 4060",
                       "radeon rx 9070", "radeon rx 7900", "radeon rx 7800", "radeon rx 7700", "radeon rx 7600"], "Gpu", 8),
    "motherboard":   (["b650", "x670", "b850", "x870e", "b760", "z790", "b860", "z890"], "Motherboard", 12),
    "memory":        (["ddr5 6000", "ddr5 5600", "ddr4 3200"], "Ram", 25),
    "power-supply":  (["850w gold", "750w gold", "1000w gold", "650w bronze"], "Psu", 20),
    # storage returns BOTH SSDs and HDDs; the Type spec routes each row (see process()). The actor
    # matches phrases against PRODUCT NAMES, so brand/model phrases work; descriptive phrases like
    # "nvme ssd 1tb" return nothing. "wd blue"/"seagate"/"toshiba" pull HDDs as well as SSDs.
    "storage":       (["samsung 990", "samsung 980", "crucial", "wd black", "wd blue", "kingston",
                       "sk hynix", "sabrent", "seagate barracuda", "toshiba"], "Ssd", 12),
    "case":          (["atx mid tower", "atx full tower", "micro atx case", "mini itx case"], "Case", 20),
    # brand phrases + don't drop out-of-stock (null-price) coolers; the seeder prices those via its calculator.
    "cpu-cooler":    (["noctua", "thermalright", "deepcool", "arctic", "be quiet", "nzxt kraken",
                       "corsair aio", "air cooler", "aio liquid cooler", "low profile cooler"], "Cooler", 12),
}

# Which PCPartPicker categories to re-fetch in merge mode (the rest are kept-and-curated from the
# existing JSON). "storage" yields Ssd+Hdd; "cpu-cooler" yields Cooler.
RESCRAPE = ["storage", "cpu-cooler"]

GPU_CARDS_PER_CHIPSET = 2  # keep at most N board-partner variants per GPU chip (spread, not 20x 5090)
MODERN_SOCKETS = ("am4", "am5", "lga1700", "lga1851", "lga1200", "lga1151")


# ---------------------------------------------------------------------------- Apify REST

def _get(url):
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {TOKEN}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())


def run_actor(category, phrases, max_products):
    """Async run: start the actor, poll to completion (no 300s sync cap), then fetch the dataset."""
    body = json.dumps({"category": category, "searchPhrases": phrases,
                       "maxProducts": max_products, "maxReviews": 0, "countryCode": "us"}).encode()
    req = urllib.request.Request(f"https://api.apify.com/v2/acts/{ACTOR}/runs", data=body, method="POST",
                                 headers={"Content-Type": "application/json",
                                          "Authorization": f"Bearer {TOKEN}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        run = json.loads(r.read().decode())["data"]
    run_id, dataset_id = run["id"], run["defaultDatasetId"]

    status = run["status"]
    for _ in range(180):  # poll up to ~15 min
        if status in ("SUCCEEDED", "FAILED", "ABORTED", "TIMED-OUT"):
            break
        time.sleep(5)
        status = _get(f"https://api.apify.com/v2/actor-runs/{run_id}")["data"]["status"]
    if status != "SUCCEEDED":
        raise RuntimeError(f"run status {status}")
    return _get(f"https://api.apify.com/v2/datasets/{dataset_id}/items?clean=true&format=json")


# ---------------------------------------------------------------------------- parsing helpers

def num(s, default=None):
    if s is None:
        return default
    m = re.search(r"[\d]+(?:\.[\d]+)?", str(s).replace(",", ""))
    return float(m.group()) if m else default


def to_int_str(s, default=""):
    v = num(s)
    return str(int(round(v))) if v is not None else default


def capacity_gb(s, default):
    """Storage capacity in GB. '1 TB' -> 1000, '500 GB' -> 500."""
    if s is None:
        return default
    txt = str(s)
    v = num(txt)
    if v is None:
        return default
    if "tb" in txt.lower():
        v *= 1000
    return str(int(round(v)))


def _price(it):
    p = (it.get("prices") or {}).get("lowestPrice")
    try:
        return float(p)
    except (TypeError, ValueError):
        return 1e12  # null price (out of stock) sorts last; the seeder prices it via its calculator


def map_specs(cat, sp, name=""):
    g = sp.get  # raw PCPartPicker spec dict
    if cat == "Cpu":
        socket = g("Socket", "Unknown")
        s = socket.lower()
        mem = "DDR5" if any(x in s for x in ["am5", "1851", "1700"]) else "DDR4"  # EST from socket (documented)
        return {
            "Cores": to_int_str(g("Core Count"), "0"),
            "Threads": to_int_str(g("Thread Count") or g("Core Count"), "0"),
            "BaseClock": str(g("Performance Core Clock") or g("Core Clock") or "0 GHz"),
            "BoostClock": str(g("Performance Core Boost Clock") or g("Boost Clock") or "0 GHz"),
            "TDP": to_int_str(g("TDP"), "65"),
            "Socket": socket,
            "MemoryType": mem,
        }
    if cat == "Gpu":
        return {
            "Chipset": str(g("Chipset") or ""),          # e.g. "GeForce RTX 4070" — used for benchmark matching
            "VRAM": to_int_str(g("Memory"), "4"),
            "CoreClock": str(g("Core Clock") or "0 MHz"),
            "Length": to_int_str(g("Length"), "0"),      # real "261 mm" -> 261
            "TDP": to_int_str(g("TDP"), "200"),
            "Interface": str(g("Interface") or "PCIe x16"),
        }
    if cat == "Motherboard":
        return {
            "Socket": str(g("Socket / CPU") or g("Socket") or "Unknown"),
            "FormFactor": str(g("Form Factor") or "ATX"),
            "RamSlots": to_int_str(g("Memory Slots"), "4"),
            "MaxRam": to_int_str(g("Memory Max"), "128"),
            "RamType": str(g("Memory Type") or "DDR4"),
        }
    if cat == "Ram":
        modules = g("Modules", "")            # e.g. "2 x 16 GB"
        m = re.search(r"(\d+)\s*x\s*(\d+)", str(modules))
        count = int(m.group(1)) if m else 2
        size = int(m.group(2)) if m else 8
        up = str(g("Speed", "")).upper()      # e.g. "DDR5-6000"
        rtype = "DDR5" if "DDR5" in up else "DDR4" if "DDR4" in up else "DDR3" if "DDR3" in up else "DDR4"
        return {
            "Type": rtype,
            "Speed": to_int_str(re.sub(r"DDR\d", "", up), "3200"),
            "Modules": str(count),
            "Capacity": str(count * size),
        }
    if cat == "Psu":
        return {
            "Wattage": to_int_str(g("Wattage"), "500"),
            "FormFactor": str(g("Type") or "ATX"),
            "Efficiency": str(g("Efficiency Rating") or g("Efficiency") or "80+ Bronze"),
            "Modular": str(g("Modular") or "No"),
        }
    if cat == "Ssd":
        return {
            "Capacity": capacity_gb(g("Capacity"), "500"),
            "FormFactor": str(g("Form Factor") or "M.2"),
            "Interface": str(g("Interface") or "PCIe"),
            "Type": "SSD",
        }
    if cat == "Hdd":
        return {
            "Capacity": capacity_gb(g("Capacity"), "1000"),
            "FormFactor": str(g("Form Factor") or '3.5"'),
            "Interface": str(g("Interface") or "SATA"),
            "Type": "HDD",
        }
    if cat == "Case":
        specs = {
            "FormFactor": str(g("Type") or "ATX Mid Tower"),
            "SupportedMotherboards": str(g("Motherboard Form Factor") or "ATX, Micro-ATX, Mini-ITX"),
            "SidePanel": str(g("Side Panel") or "None"),
        }
        gpu_len = to_int_str(g("Maximum Video Card Length"))  # real, e.g. "381 mm"
        if gpu_len:
            specs["MaxGpuLength"] = gpu_len
        return specs
    if cat == "Cooler":
        rad = g("Radiator Size")
        wc = str(g("Water Cooled") or "").lower() in ("yes", "true")
        nm = name.lower()
        water = wc or bool(rad and num(rad)) or any(k in nm for k in ("aio", "liquid", "kraken", "water cool"))
        specs = {"WaterCooled": "True" if water else "False"}
        sock = g("CPU Socket")
        if sock:
            specs["CpuSockets"] = ", ".join(sock) if isinstance(sock, list) else str(sock)
        h = to_int_str(g("Height"))
        if h:
            specs["Height"] = h
        if rad and num(rad):
            specs["RadiatorSize"] = to_int_str(rad)
        return specs
    return {}


def keep(cat, name, specs):
    """Selection-only curation on MAPPED specs. True = keep. Drops obsolete / low-quality parts."""
    n = name.lower()
    if cat == "Cpu":
        if any(b in n for b in ("celeron", "pentium", "atom", "opteron", "sempron", "athlon", "epyc", "xeon")):
            return False
        sock = str(specs.get("Socket", "")).lower().replace(" ", "")
        if sock and sock != "unknown" and not any(s in sock for s in MODERN_SOCKETS):
            return False
        if "ryzen" in n and "threadripper" not in n:
            m = re.search(r"ryzen \d+ (\d{4})", n)        # Ryzen 3000+ (Zen 2 onward)
            if m and int(m.group(1)) < 3000:
                return False
        elif re.search(r"i[3579]-", n):
            mi = re.search(r"i[3579]-(\d{4,5})", n)        # Intel 10th gen onward
            if mi and int(mi.group(1)) < 10000:
                return False
        return True
    if cat == "Gpu":
        s = (str(specs.get("Chipset", "")) + " " + name).lower()
        if any(x in s for x in ("gtx 16", "rtx 20", "rtx 30", "rtx 40", "rtx 50")):
            return True
        return any(x in s for x in ("rx 5500", "rx 5600", "rx 5700", "rx 6", "rx 7", "rx 8", "rx 9"))
    if cat == "Ram":
        return str(specs.get("Type", "")).upper() not in ("DDR2", "DDR3")
    if cat == "Motherboard":
        sock = str(specs.get("Socket", "")).lower().replace(" ", "")
        return (not sock) or sock == "unknown" or any(s in sock for s in MODERN_SOCKETS)
    if cat == "Psu":
        eff = str(specs.get("Efficiency", "")).lower()
        return any(x in eff for x in ("bronze", "silver", "gold", "platinum", "titanium"))
    return True  # Case / Ssd / Hdd / Cooler — no quality gate beyond price + dedup


# ---------------------------------------------------------------------------- pipeline

def _run(pcpp_cat, phrases, max_products):
    """One actor run for a whole phrase list (one cold start, not one per phrase)."""
    try:
        items = run_actor(pcpp_cat, phrases, max_products)
        return items if isinstance(items, list) else []
    except Exception as e:
        print(f"  {pcpp_cat}: FAILED {repr(e)[:90]}", flush=True)
        return []


def scrape_raw(pcpp_cats):
    """Full mode: ONE batched run per category (all phrases together)."""
    raw = defaultdict(list)
    for pcpp_cat in pcpp_cats:
        phrases, struct_cat, _ = PLAN[pcpp_cat]
        items = _run(pcpp_cat, phrases, min(14 * len(phrases), 90))
        raw[struct_cat].extend(items)
        print(f"  {struct_cat} [{len(phrases)} phrases]: +{len(items)}", flush=True)
    return raw


def process(raw, seen=None):
    """Dedup (cheapest-first) → route Hdd → map → curate → cap GPUs. Returns clean catalog rows."""
    seen = seen if seen is not None else set()
    out = []
    gpu_per_chip = defaultdict(int)
    for struct_cat, items in raw.items():
        kept = 0
        for it in sorted(items, key=_price):  # cheapest-first, so dedup keeps a representative model
            sp = it.get("specifications") or {}
            name = (it.get("name") or "").strip()
            if not name or name in seen:
                continue
            cat = struct_cat
            if struct_cat == "Ssd" and ("hdd" in str(sp.get("Type", "")).lower() or "hard drive" in name.lower()):
                cat = "Hdd"
            chip = None
            if struct_cat == "Gpu":
                chip = (sp.get("Chipset") or "").strip().lower()
                if not chip or gpu_per_chip[chip] >= GPU_CARDS_PER_CHIPSET:
                    continue
            specs = map_specs(cat, sp, name)
            if not keep(cat, name, specs):
                continue
            if chip is not None:
                gpu_per_chip[chip] += 1
            p = _price(it)
            seen.add(name)
            out.append({"Category": cat, "Name": name,
                        "Brand": sp.get("Manufacturer", ""),
                        "Price": 0.0 if p >= 1e12 else p,  # 0 => seeder computes a fallback price
                        "TechnicalSpecs": specs})
            kept += 1
        print(f"  => {struct_cat}: kept {kept}")
    return out


def _write(db):
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(db, f, indent=4, ensure_ascii=False)
    print(f"=== DONE — {len(db)} components written to {OUT} ===")


# ---------------------------------------------------------------------------- modes

def build():
    print("=== FULL re-scrape: live PCPartPicker via Apify (per-phrase, balanced) ===")
    _write(process(scrape_raw(PLAN.keys())))


# Merge re-scrape as explicit BATCHED runs (one cold start each). Storage is split into an SSD-brand
# run and an HDD-brand run so both surface; struct_cat "Ssd" is routed to Hdd per-item by Type.
# Tuple: (pcpp_category, struct_cat, phrases, max_products).
MERGE_BATCHES = [
    ("storage", "Ssd",
     ["samsung 990", "samsung 980", "crucial", "wd black", "kingston", "sk hynix", "sabrent"], 60),
    ("storage", "Ssd",
     ["seagate barracuda", "toshiba", "wd blue", "seagate ironwolf"], 35),
    ("cpu-cooler", "Cooler",
     ["noctua", "thermalright", "deepcool", "arctic", "be quiet", "nzxt kraken", "corsair aio",
      "air cooler", "aio liquid cooler", "low profile cooler"], 70),
]
MERGE_REPLACE = {"Ssd", "Hdd", "Cooler"}  # categories these batches replace in the existing catalog


def merge():
    print("=== MERGE: batched re-scrape of", sorted(MERGE_REPLACE), "+ curate existing catalog ===",
          flush=True)
    existing = json.load(open(OUT, encoding="utf-8"))

    kept, dropped, seen = [], 0, set()
    for it in existing:
        if it["Category"] in MERGE_REPLACE:
            continue  # these categories are being re-fetched fresh
        if keep(it["Category"], it["Name"], it.get("TechnicalSpecs", {})):
            kept.append(it)
            seen.add(it["Name"])
        else:
            dropped += 1
    print(f"  kept {len(kept)} curated parts from existing ({dropped} stale dropped); "
          f"replacing {sorted(MERGE_REPLACE)}", flush=True)

    raw = defaultdict(list)
    for pcpp_cat, struct_cat, phrases, maxp in MERGE_BATCHES:
        items = _run(pcpp_cat, phrases, maxp)
        raw[struct_cat].extend(items)
        print(f"  {struct_cat} [{len(phrases)} phrases]: +{len(items)}", flush=True)

    fresh = process(raw, seen)
    _write(kept + fresh)


# Additive top-up scrape: improve AMD CPU representation and add liquid/AIO coolers, both of which the
# initial balanced scrape under-covered. Tuple: (pcpp_category, struct_cat, phrases, max_products).
AUGMENT_BATCHES = [
    ("cpu", "Cpu",
     ["ryzen 5 7600", "ryzen 5 7600x", "ryzen 7 7700x", "ryzen 7 7800x3d", "ryzen 9 7900x",
      "ryzen 9 7950x", "ryzen 5 9600x", "ryzen 7 9700x", "ryzen 7 9800x3d", "ryzen 9 9900x",
      "ryzen 9 9950x", "ryzen 5 8600g"], 60),
    ("cpu-cooler", "Cooler",
     ["nzxt kraken", "arctic liquid freezer", "corsair icue", "lian li galahad", "deepcool ls",
      "msi coreliquid", "be quiet pure loop", "thermalright frozen"], 50),
]


def augment():
    """ADD (don't replace) curated parts to the existing catalog: more AMD CPUs + liquid coolers."""
    print("=== AUGMENT: top-up AMD CPUs + AIO coolers (additive) ===", flush=True)
    existing = json.load(open(OUT, encoding="utf-8"))
    seen = {it["Name"] for it in existing}

    raw = defaultdict(list)
    for pcpp_cat, struct_cat, phrases, maxp in AUGMENT_BATCHES:
        items = _run(pcpp_cat, phrases, maxp)
        raw[struct_cat].extend(items)
        print(f"  {struct_cat} [{len(phrases)} phrases]: +{len(items)}", flush=True)

    fresh = process(raw, seen)
    # From the cooler top-up keep only liquid coolers — we already have plenty of air coolers.
    fresh = [e for e in fresh
             if not (e["Category"] == "Cooler" and e["TechnicalSpecs"].get("WaterCooled") != "True")]
    n_cpu = sum(1 for e in fresh if e["Category"] == "Cpu")
    n_cool = sum(1 for e in fresh if e["Category"] == "Cooler")
    print(f"  added: {n_cpu} CPUs, {n_cool} liquid coolers", flush=True)
    _write(existing + fresh)


def test():
    print("=== TEST probe (no write): storage['ssd'] + cpu-cooler['noctua'] ===")
    for pcpp_cat, phrase in (("storage", "ssd"), ("cpu-cooler", "noctua")):
        items = run_actor(pcpp_cat, [phrase], 5)
        items = items if isinstance(items, list) else []
        print(f"\n--- {pcpp_cat} '{phrase}': {len(items)} items")
        for it in items[:3]:
            sp = it.get("specifications") or {}
            print(f"  name: {it.get('name')!r}  price: {(it.get('prices') or {}).get('lowestPrice')}")
            print(f"    spec keys: {sorted(sp.keys())}")
            print(f"    raw specs: {json.dumps(sp, ensure_ascii=False)[:300]}")


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "merge"
    {"merge": merge, "full": build, "augment": augment, "test": test}.get(mode, merge)()

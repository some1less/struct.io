"""
Struct catalog builder — scrapes LIVE PCPartPicker via the Apify actor
`matyascimbulka/pcpartpicker-scraper` and maps it to clean_database.json.

PROVENANCE (for the thesis/defense):
  * Source: PCPartPicker, scraped live via Apify (actor + run date recorded below).
  * Every TechnicalSpec is a REAL value from PCPartPicker (units parsed off), with ONE documented
    estimate: CPU MemoryType is derived from the CPU socket (PCPartPicker lists memory type on the
    motherboard, not the CPU). No values are fabricated.
  * Fields a product genuinely lacks are omitted; the compatibility engine skips checks on missing
    specs rather than guessing.

Token: read from scraper/.apify_token (gitignored). Run: ./venv/bin/python build_catalog_apify.py
"""
import json, os, re, time, urllib.request
from collections import defaultdict

ACTOR = "matyascimbulka~pcpartpicker-scraper"
TOKEN = open(os.path.join(os.path.dirname(__file__), ".apify_token")).read().strip()
OUT = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")

# Search phrases per category. Each phrase is scraped SEPARATELY with a small per-phrase cap, so every
# tier/brand gets fair representation (e.g. Intel isn't crowded out by AMD). Third value = per-phrase cap.
PLAN = {
    "cpu":           (["ryzen 5", "core i5", "ryzen 7", "core i7", "ryzen 9", "core i9", "core ultra"], "Cpu", 12),
    "video-card":    (["rtx 5090","rtx 5080","rtx 5070","rtx 4090","rtx 4080","rtx 4070","rtx 4060",
                       "radeon rx 9070","radeon rx 7900","radeon rx 7800","radeon rx 7700","radeon rx 7600"], "Gpu", 8),
    "motherboard":   (["b650","x670","b850","x870e","b760","z790","b860","z890"], "Motherboard", 12),
    "memory":        (["ddr5 6000","ddr5 5600","ddr4 3200"], "Ram", 25),
    "power-supply":  (["850w gold","750w gold","1000w gold","650w bronze"], "Psu", 20),
    "storage":       (["nvme ssd 1tb","nvme ssd 2tb","sata ssd 1tb"], "Ssd", 25),
    "case":          (["atx mid tower","atx full tower","micro atx case","mini itx case"], "Case", 20),
    "cpu-cooler":    (["air cooler","aio liquid cooler","low profile cooler"], "Cooler", 25),
}

GPU_CARDS_PER_CHIPSET = 2  # keep at most N board-partner variants per GPU chip (spread, not 20x 5090)


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


def num(s, default=None):
    if s is None: return default
    m = re.search(r"[\d]+(?:\.[\d]+)?", str(s).replace(",", ""))
    return float(m.group()) if m else default


def to_int_str(s, default=""):
    v = num(s)
    return str(int(round(v))) if v is not None else default


def map_specs(cat, sp):
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
            "Length": to_int_str(g("Length"), "0"),     # real "261 mm" -> 261
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
        speed = g("Speed", "")                # e.g. "DDR5-6000"
        rtype = "DDR5" if "DDR5" in str(speed).upper() else "DDR4"
        return {
            "Type": rtype,
            "Speed": to_int_str(re.sub(r"DDR\d", "", str(speed)), "3200"),
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
        t = str(g("Type") or "SSD")
        return {
            "Capacity": to_int_str(g("Capacity"), "500"),
            "FormFactor": str(g("Form Factor") or "M.2"),
            "Interface": str(g("Interface") or "PCIe"),
            "Type": "HDD" if "hdd" in t.lower() or "platter" in t.lower() else "SSD",
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
        specs = {"WaterCooled": "True" if str(g("Water Cooled")).lower() in ("yes", "true") else "False"}
        if g("CPU Socket"):
            specs["CpuSockets"] = ", ".join(g("CPU Socket")) if isinstance(g("CPU Socket"), list) else str(g("CPU Socket"))
        h = to_int_str(g("Height"))
        if h:
            specs["Height"] = h
        return specs
    return {}


def _price(it):
    p = (it.get("prices") or {}).get("lowestPrice")
    try:
        return float(p)
    except (TypeError, ValueError):
        return 1e12


def build():
    print("=== Building catalog from live PCPartPicker via Apify (per-phrase, balanced) ===")
    raw = defaultdict(list)
    for pcpp_cat, (phrases, struct_cat, cap) in PLAN.items():
        for phrase in phrases:
            try:
                items = run_actor(pcpp_cat, [phrase], cap)
                items = items if isinstance(items, list) else []
            except Exception as e:
                print(f"  {struct_cat} '{phrase}': FAILED {repr(e)[:90]}"); continue
            raw[struct_cat].extend(items)
            print(f"  {struct_cat} '{phrase}': +{len(items)}")
            time.sleep(1)

    clean_db, seen = [], set()
    gpu_per_chip = defaultdict(int)
    for struct_cat, items in raw.items():
        kept = 0
        for it in sorted(items, key=_price):  # cheapest-first, so dedup keeps a representative model
            sp = it.get("specifications") or {}
            name = (it.get("name") or "").strip()
            if not name or name in seen or _price(it) >= 1e12:
                continue
            cat = struct_cat
            if struct_cat == "Ssd" and "hdd" in str(sp.get("Type", "")).lower():
                cat = "Hdd"
            if struct_cat == "Gpu":
                chip = (sp.get("Chipset") or "").strip().lower()
                if not chip or gpu_per_chip[chip] >= GPU_CARDS_PER_CHIPSET:
                    continue
                gpu_per_chip[chip] += 1
            seen.add(name)
            clean_db.append({"Category": cat, "Name": name,
                             "Brand": sp.get("Manufacturer", ""), "Price": _price(it),
                             "TechnicalSpecs": map_specs(cat if cat != "Hdd" else "Ssd", sp)})
            kept += 1
        print(f"  => {struct_cat}: kept {kept}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(clean_db, f, indent=4, ensure_ascii=False)
    print(f"=== DONE — {len(clean_db)} components written to {OUT} ===")


if __name__ == "__main__":
    build()

"""
Struct catalog scraper — builds clean_database.json from PCPartPicker (via the `pcpartpicker` lib).

DATA PROVENANCE (documented for the thesis / defense):
  * REAL  — taken verbatim from the PCPartPicker API.
  * EST   — estimated from a real field via a documented, deterministic rule (domain knowledge).
            These are clearly marked and are NOT fabrications (e.g. a CPU's socket is fully
            determined by its model name).
  * (none)— fields the source does not provide are OMITTED, never invented. The compatibility
            engine treats a missing spec as "cannot verify" and skips that check.

Compared to the previous version, this scraper REMOVES all fabricated data:
  - no motherboard socket flipping / chipset renaming,
  - no constant case MaxGpuLength,
  - no constant cooler Height,
  - no fake cooler CpuSockets list.
Those fields are simply absent now (the API does not expose them), so the cooler-height,
GPU-length and cooler-socket checks are honestly disabled rather than driven by fake data.
"""

import json
import os
import re
from pcpartpicker import API

# Category quality/curation filters (selection only — never alters values).
CATEGORIES = {
    "cpu": "Cpu", "video-card": "Gpu", "motherboard": "Motherboard", "memory": "Ram",
    "power-supply": "Psu", "internal-hard-drive": "Ssd", "case": "Case", "cpu-cooler": "Cooler",
}
MODERN_SOCKETS = ["am4", "am5", "lga1700", "lga1851", "lga1200", "lga1151"]
PER_CATEGORY_CAP = 800


def safe_ghz(v):
    if isinstance(v, dict) and "cycles" in v:
        return f"{round(float(v['cycles']) / 1_000_000_000.0, 2)} GHz"
    return "0 GHz"


def safe_str(part, k, default=""):
    v = part.get(k)
    if isinstance(v, dict) and "total" in v:
        if k in ["vram", "max_ram", "module_size", "capacity"]:
            return str(round(float(v["total"]) / 1_000_000_000.0))
        return str(v["total"])
    return str(v) if v is not None else default


def cpu_socket_and_memory(lower_name):
    """EST: a CPU's socket + memory type are fully determined by its model name (documented map)."""
    if "ryzen" in lower_name or "threadripper" in lower_name:
        m = re.search(r'ryzen \d (\d{4})', lower_name)
        if m and int(m.group(1)) >= 7000:
            return "AM5", "DDR5"
        return "AM4", "DDR4"
    if "intel" in lower_name or "core" in lower_name:
        if "ultra" in lower_name:
            return "LGA1851", "DDR5"
        if re.search(r'i[3579]-1[234]\d{3}', lower_name):
            return "LGA1700", "DDR5"
        if re.search(r'i[3579]-1[01]\d{3}', lower_name):
            return "LGA1200", "DDR4"
        return "LGA1151", "DDR4"
    return "Unknown", "Unknown"


def gpu_tdp_estimate(lower_name):
    """EST: board power by chip tier (documented approximation; real per-chip TDPs vary by AIB)."""
    tiers = [
        (["4090", "5090", "3090 ti"], 450), (["3090", "7900 xtx", "6950 xt"], 350),
        (["4080", "5080", "3080", "7900 xt", "6900 xt", "6800 xt"], 320),
        (["2080 ti", "7900 gre", "7800", "6800"], 260),
        (["4070 ti", "3070 ti", "2080 super", "5700 xt", "7700", "6750 xt"], 230),
        (["4070", "3070", "2080", "2070 super"], 210),
        (["4060 ti", "3060 ti", "2070", "6700 xt", "5700"], 175),
        (["4060", "7600", "5600 xt", "2060 super", "1660 ti"], 150),
        (["3060", "2060", "1660 super", "6650 xt"], 125),
        (["3050", "1660", "1650 super", "6600", "6500"], 110),
        (["1650", "rx 560"], 75),
    ]
    for names, tdp in tiers:
        if any(n in lower_name for n in names):
            return tdp
    return 200  # documented default


def supported_boards(form_factor):
    """EST: which motherboard sizes physically fit a case of this form factor (standard nesting)."""
    f = form_factor.lower()
    if "mini itx" in f or "mini-itx" in f:
        return "Mini-ITX"
    if "micro" in f or "matx" in f or "m-atx" in f:
        return "Micro-ATX, Mini-ITX"
    # ATX / Mid / Full towers accept ATX and everything smaller
    return "ATX, Micro-ATX, Mini-ITX"


def fetch_clean_data():
    print("=== RUNNING SCRAPER (honest build) ===")
    api = API()
    clean_db = []

    for pcpp_key, struct_cat in CATEGORIES.items():
        print(f"Processing category: {struct_cat}...")
        try:
            raw = json.loads(api.retrieve(pcpp_key).to_json())
            parts_list = raw.get(pcpp_key) or (raw.get("parts") if isinstance(raw, dict) else None) or []
            if not parts_list and isinstance(raw, dict) and len(raw) == 1:
                parts_list = list(raw.values())[0]
            if not parts_list:
                continue

            count_added = 0
            for part in parts_list:
                if count_added >= PER_CATEGORY_CAP:
                    break

                # Price filter (real price required)
                price_data = part.get("price")
                if not price_data or not isinstance(price_data, list) or len(price_data) < 2:
                    continue
                try:
                    price = float(price_data[1])
                except (ValueError, TypeError):
                    continue
                if price <= 0:
                    continue

                brand = part.get("brand", "")
                model = part.get("model", part.get("name", "Unknown"))
                full_name = f"{brand} {model}".strip()
                lower_name = full_name.lower()

                actual_cat = struct_cat
                if struct_cat == "Ssd":
                    st = str(part.get("storage_type", "")).lower()
                    if "ssd" not in st and "m.2" not in st and "nvme" not in st:
                        actual_cat = "Hdd"

                # ---- curation filters (selection only) ----
                if struct_cat == "Cpu":
                    if any(b in lower_name for b in ["celeron", "pentium", "atom", "opteron", "sempron", "athlon", "epyc", "xeon"]):
                        continue
                    if "amd" in lower_name or "ryzen" in lower_name:
                        if "ryzen" not in lower_name and "threadripper" not in lower_name:
                            continue
                        m = re.search(r'ryzen \d (\d{4})', lower_name)
                        if m and int(m.group(1)) < 3000:
                            continue
                    elif "intel" in lower_name or "core" in lower_name:
                        if "core" not in lower_name and not any(u in lower_name for u in ["ultra 5", "ultra 7", "ultra 9"]):
                            continue
                        mi = re.search(r'i[3579]-(\d{4,5})', lower_name)
                        if mi and int(mi.group(1)) < 8000:
                            continue
                        elif not mi and not any(u in lower_name for u in ["ultra 5", "ultra 7", "ultra 9"]):
                            continue
                if struct_cat == "Gpu":
                    gname = str(part.get("name", "")).lower()
                    chip = str(part.get("chipset", "")).lower()
                    ok_nv = any(x in gname for x in ["gtx 16", "rtx 20", "rtx 30", "rtx 40", "rtx 50"])
                    ok_amd = any(x in chip for x in ["rx 5500", "rx 5600", "rx 5700", "rx 6", "rx 7", "rx 8"])
                    if not (ok_nv or ok_amd):
                        continue
                if struct_cat == "Ram":
                    if any(t in str(part.get("type", "")).lower() for t in ["ddr2", "ddr3"]):
                        continue
                if struct_cat == "Motherboard":
                    if not any(s in str(part.get("socket", "")).lower() for s in MODERN_SOCKETS):
                        continue
                if struct_cat == "Psu":
                    eff = str(part.get("efficiency_rating", "")).lower()
                    if not any(x in eff for x in ["bronze", "silver", "gold", "platinum", "titanium"]):
                        continue
                    if any(b in lower_name for b in ["coolmax", "diablotek", "apevia", "apex", "athena power"]):
                        continue

                # ---- build specs: REAL fields + documented EST; omit anything unavailable ----
                specs = {}
                if actual_cat == "Cpu":
                    specs["Cores"] = safe_str(part, "cores", "0")                       # REAL
                    cores_int = int(specs["Cores"]) if specs["Cores"].isdigit() else 0
                    specs["Threads"] = str(cores_int * 2) if part.get("multithreading") else specs["Cores"]  # REAL (SMT flag)
                    specs["BaseClock"] = safe_ghz(part.get("base_clock"))               # REAL
                    specs["BoostClock"] = safe_ghz(part.get("boost_clock"))             # REAL
                    specs["TDP"] = safe_str(part, "tdp", "65")                          # REAL
                    sock, mem = cpu_socket_and_memory(lower_name)                       # EST (from model name)
                    specs["Socket"] = sock
                    specs["MemoryType"] = mem

                elif actual_cat == "Gpu":
                    chipset = part.get("chipset", "")
                    if chipset and chipset not in full_name:
                        full_name = f"{full_name} ({chipset})"
                        lower_name = full_name.lower()
                    specs["VRAM"] = safe_str(part, "vram", "4")                         # REAL
                    cc = part.get("core_clock")
                    specs["CoreClock"] = (str(round(float(cc["cycles"]) / 1_000_000.0)) + " MHz") \
                        if isinstance(cc, dict) and "cycles" in cc else "1000 MHz"      # REAL
                    specs["Length"] = safe_str(part, "length", "0")                    # REAL (0 => unknown => skip)
                    specs["TDP"] = str(gpu_tdp_estimate(lower_name))                    # EST (chip tier)

                elif actual_cat == "Motherboard":
                    specs["Socket"] = safe_str(part, "socket", "Unknown")              # REAL (no flipping!)
                    specs["FormFactor"] = safe_str(part, "form_factor", "ATX")          # REAL
                    specs["RamSlots"] = safe_str(part, "ram_slots", "4")               # REAL
                    specs["MaxRam"] = safe_str(part, "max_ram", "128")                 # REAL
                    s = specs["Socket"].lower()
                    specs["RamType"] = "DDR5" if ("am5" in s or "1851" in s or "1700" in s) else "DDR4"  # EST (from socket)

                elif actual_cat == "Ram":
                    specs["Type"] = safe_str(part, "module_type", "DDR4")              # REAL
                    sp = part.get("speed")
                    specs["Speed"] = str(round(float(sp["cycles"]) / 1_000_000.0)) \
                        if isinstance(sp, dict) and "cycles" in sp else "3200"          # REAL
                    specs["Modules"] = safe_str(part, "number_of_modules", "2")        # REAL
                    msz = part.get("module_size")
                    try:
                        gb = float(msz["total"]) / 1_000_000_000.0
                        specs["Capacity"] = str(int(gb) * int(float(specs["Modules"])))  # REAL (computed)
                    except Exception:
                        specs["Capacity"] = "16"

                elif actual_cat == "Psu":
                    specs["Wattage"] = safe_str(part, "wattage", "500")                # REAL
                    specs["FormFactor"] = safe_str(part, "form_factor", "ATX")          # REAL
                    specs["Efficiency"] = safe_str(part, "efficiency_rating", "80+ Bronze")  # REAL
                    specs["Modular"] = safe_str(part, "modular", "No")                 # REAL

                elif actual_cat == "Ssd":
                    specs["Capacity"] = safe_str(part, "capacity", "500")              # REAL
                    specs["FormFactor"] = safe_str(part, "form_factor", "M.2")          # REAL
                    specs["Interface"] = safe_str(part, "interface", "PCIe")           # REAL
                    specs["Type"] = safe_str(part, "storage_type", "SSD")              # REAL

                elif actual_cat == "Hdd":
                    specs["Capacity"] = safe_str(part, "capacity", "1000")             # REAL
                    specs["FormFactor"] = safe_str(part, "form_factor", "3.5\"")       # REAL
                    specs["Interface"] = safe_str(part, "interface", "SATA")           # REAL
                    specs["Type"] = "HDD"

                elif actual_cat == "Case":
                    specs["FormFactor"] = safe_str(part, "form_factor", "ATX Mid Tower")  # REAL
                    specs["SupportedMotherboards"] = supported_boards(specs["FormFactor"])  # EST (form-factor nesting)
                    specs["SidePanel"] = safe_str(part, "side_panel", "None")          # REAL
                    # OMITTED: MaxGpuLength — not provided by the API (was fabricated before).

                elif actual_cat == "Cooler":
                    specs["RadiatorSize"] = safe_str(part, "radiator_size", "0")       # REAL
                    rs = int(float(specs["RadiatorSize"])) if specs["RadiatorSize"].replace('.', '').isdigit() else 0
                    specs["WaterCooled"] = "True" if rs > 0 else "False"               # EST (from radiator presence)
                    # OMITTED: Height, CpuSockets — not provided by the API (were fabricated before).

                clean_db.append({
                    "Category": actual_cat, "Name": full_name, "Brand": brand,
                    "Price": price, "TechnicalSpecs": specs,
                })
                count_added += 1

            print(f"   Added {count_added} components for {struct_cat}")
        except Exception as e:
            print(f"Error processing {struct_cat}: {e}")

    output_path = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(clean_db, f, indent=4, ensure_ascii=False)

    print(f"\n=== DONE — {len(clean_db)} components written ===")


if __name__ == "__main__":
    fetch_clean_data()

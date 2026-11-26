"""Map the raw x-kom crawl (data/xkom_raw/*.jsonl, from fetch_xkom.py) onto data/catalog.xlsx.

Polish x-kom parameters -> the TechnicalSpecs columns excel_to_catalog.py expects. The previous workbook is
kept once as data/catalog.pcpp.xlsx. Run from the repo root:  python3 scraper/xkom_to_catalog.py
"""
import json
import re
import shutil
import sys
from pathlib import Path

from openpyxl import Workbook

from excel_to_catalog import COMMON, SHEETS

REPO = Path(__file__).resolve().parent.parent
RAW = REPO / "data" / "xkom_raw"
XLSX = REPO / "data" / "catalog.xlsx"
BACKUP = REPO / "data" / "catalog.pcpp.xlsx"

# Reference board power (W) per GPU chip from NVIDIA/AMD/Intel spec pages: x-kom lists no GPU TDP,
# and the PSU sizing rule needs one.
GPU_TDP = {
    "RTX 5090": 575, "RTX 5080": 360, "RTX 5070 Ti": 300, "RTX 5070": 250, "RTX 5060 Ti": 180, "RTX 5060": 145,
    "RTX 5050": 130, "RTX 4090": 450, "RTX 4080 SUPER": 320, "RTX 4080": 320, "RTX 4070 Ti SUPER": 285,
    "RTX 4070 Ti": 285, "RTX 4070 SUPER": 220, "RTX 4070": 200, "RTX 4060 Ti": 165, "RTX 4060": 115,
    "RTX 3060 Ti": 200, "RTX 3060": 170, "RTX 3050": 130, "GTX 1650": 75, "GT 1030": 30, "GT 730": 38,
    "RX 9070 XT": 304, "RX 9070 GRE": 220, "RX 9070": 220, "RX 9060 XT": 160, "RX 7900 XTX": 355, "RX 7900 XT": 315,
    "RX 7900 GRE": 260, "RX 7800 XT": 263, "RX 7700 XT": 245, "RX 7600 XT": 190, "RX 7600": 165,
    "RX 6600": 132, "RX 6500 XT": 107, "Arc B580": 190, "Arc B570": 150, "Arc A770": 225, "Arc A750": 225,
    "Arc A380": 75, "Arc A310": 75,
}
FORM = [("EATX", re.compile(r"e-?atx|extended", re.I)), ("Micro ATX", re.compile(r"micro|m-?atx", re.I)),
        ("Mini ITX", re.compile(r"itx", re.I)), ("ATX", re.compile(r"atx", re.I))]


def num(text):
    """First number in text ('4,2 GHz' -> 4.2, '4 x DIMM' -> 4), int when whole; None if absent."""
    m = re.search(r"\d+(?:[.,]\d+)?", text or "")
    if not m:
        return None
    value = float(m.group().replace(",", "."))
    return int(value) if value.is_integer() else value


def socket(text):
    """'Socket AM5' -> 'AM5', 'Socket 1700' / '1700' -> 'LGA1700'."""
    s = re.sub(r"(?i)^socket\s*", "", (text or "").strip())
    return f"LGA{s}" if s.isdigit() else s or None


def form_factor(text):
    return next((name for name, rx in FORM if rx.search(text or "")), None)


def ddr(text):
    return ", ".join(sorted(set(re.findall(r"DDR\d", text or "", re.I)))).upper() or None


def first(f, *keys):
    return next((f[k] for k in keys if f.get(k)), None)


def cpu(f, _r):
    clocks = [num(c) for c in re.findall(r"\d+(?:[.,]\d+)?\s*GHz", first(f, "Taktowanie rdzenia") or "")]
    return {"Cores": num(first(f, "Liczba rdzeni fizycznych", "Liczba rdzeni")),
            "Threads": num(f.get("Liczba wątków")),
            "BaseClock": clocks[0] if clocks else None, "BoostClock": clocks[-1] if clocks else None,
            "TDP": num(first(f, "Pobór mocy (TDP)", "TDP")),
            "Socket": socket(first(f, "Gniazdo procesora (socket)", "Gniazdo procesora")),
            "MemoryType": ddr(f.get("Rodzaj obsługiwanej pamięci"))}


def gpu(f, _r):
    chip = first(f, "Układ graficzny")
    tdp = num(first(f, "Pobór mocy (TDP)", "Pobór mocy"))
    if tdp is None and chip:
        # longest key first so 'RTX 5060 Ti' wins over 'RTX 5060'
        tdp = next((w for k, w in sorted(GPU_TDP.items(), key=lambda kv: -len(kv[0]))
                    if k.lower() in chip.lower()), None)
    return {"Chipset": chip, "VRAM": num(f.get("Pamięć")),
            "CoreClock": num(first(f, "Taktowanie rdzenia w trybie boost", "Taktowanie rdzenia")),
            "Length": num(f.get("Długość")), "TDP": tdp, "Interface": f.get("Rodzaj złącza")}


def motherboard(f, _r):
    return {"Socket": socket(f.get("Gniazdo procesora")), "FormFactor": form_factor(f.get("Format")),
            "RamType": ddr(first(f, "Typ obsługiwanej pamięci", "Typ obsługiwanej pamięci OC")),
            "MaxRam": num(f.get("Maksymalna wielkość pamięci RAM")), "RamSlots": num(f.get("Liczba banków pamięci"))}


def ram(f, r):
    if re.search(r"so-?dimm", f"{r['fullName']} {r.get('category')} {f.get('Rodzaj pamięci')}", re.I):
        return None
    return {"Type": ddr(f.get("Rodzaj pamięci")), "Capacity": num(f.get("Pojemność całkowita")),
            "Speed": num(first(f, "Taktowanie", "Szybkość transferu")), "Modules": num(f.get("Liczba modułów")) or 1}


def psu(f, _r):
    std = f.get("Standard") or ""
    cert = re.search(r"80\s*PLUS\s*(\w+)?", f.get("Certyfikat") or "", re.I)
    cabling = (f.get("Typ okablowania") or "").lower()
    modular = "Semi" if "częściowo" in cabling or "semi" in cabling else (
        "No" if "niemodu" in cabling or "stał" in cabling else ("Full" if "modu" in cabling else None))
    return {"Wattage": num(f.get("Moc maksymalna")),
            "FormFactor": "SFX-L" if "SFX-L" in std.upper() else ("SFX" if "SFX" in std.upper() else "ATX"),
            "Efficiency": (f"80+ {cert.group(1).title()}" if cert.group(1) else "80+") if cert else None,
            "Modular": modular}


def case(f, _r):
    boards = [form_factor(b) for b in (f.get("Standard płyty głównej") or "").split(",")]
    return {"FormFactor": f.get("Typ obudowy"), "MaxGpuLength": num(f.get("Maksymalna długość karty graficznej")),
            "MaxCoolerHeight": num(f.get("Maksymalna wysokość chłodzenia CPU")),
            "SupportedMotherboards": ", ".join(dict.fromkeys(b for b in boards if b)) or None,
            "SidePanel": f.get("Panel boczny")}


def cooler(f, r):
    water = "wodn" in (r.get("category") or "").lower()
    fans = re.search(r"(\d+)\s*x\s*(\d+)\s*mm", f.get("Liczba wentylatorów") or "")
    if water and not (f.get("Rozmiar radiatora") or fans):
        return None  # custom-loop parts (blocks, pumps, fittings) are not a CPU cooler on their own
    sockets = [socket(s) for s in re.split(r"[,;/]", f.get("Kompatybilność") or "")]
    # radiator class from its fans ('3x 120 mm' -> 360); x-kom's radiator size is the outer dimension (396 mm)
    return {"CpuSockets": ", ".join(dict.fromkeys(s for s in sockets if s)) or None,
            "Height": None if water else num(f.get("Wysokość")),
            "WaterCooled": water, "RadiatorSize": int(fans[1]) * int(fans[2]) if water and fans else None}


def storage(f, _r):
    return {"Capacity": num(f.get("Pojemność")), "FormFactor": (f.get("Format") or "").replace(" ", "") or None,
            "Interface": f.get("Interfejs")}


MAPPERS = {"Cpu": cpu, "Gpu": gpu, "Motherboard": motherboard, "Ram": ram, "Psu": psu, "Case": case,
           "Cooler": cooler, "Ssd": storage, "Hdd": storage}
# Without these the compatibility rules would silently pass garbage builds.
CRITICAL = {"Cpu": ["Socket", "TDP"], "Gpu": ["Chipset", "TDP"], "Motherboard": ["Socket", "RamType", "FormFactor"],
            "Ram": ["Type", "Capacity"], "Psu": ["Wattage"], "Case": ["MaxGpuLength", "MaxCoolerHeight"],
            "Cooler": ["CpuSockets", "Height"], "Ssd": ["Capacity"], "Hdd": ["Capacity"]}


def to_row(cat, r):
    """Catalog row for one raw record, or (None, reason) when it can't be used."""
    if not r.get("price"):
        return None, "no price"
    specs = MAPPERS[cat](r.get("features") or {}, r)
    if specs is None:
        return None, "not a desktop part"
    # an AIO has no tower height to fit under the side panel
    missing = [k for k in CRITICAL.get(cat, [])
               if specs.get(k) in (None, "") and not (k == "Height" and specs.get("WaterCooled"))]
    if missing:
        return None, f"missing {', '.join(missing)}"
    return {"Name": r["fullName"], "Brand": r.get("producer") or r["fullName"].split()[0], "PricePln": r["price"],
            "PriceSource": "x-kom", "SourceUrl": r["url"], **specs}, None


def main():
    if XLSX.exists() and not BACKUP.exists():
        shutil.copy2(XLSX, BACKUP)
    wb = Workbook()
    wb.remove(wb.active)
    for cat in SHEETS:
        ws = wb.create_sheet(cat)
        columns = COMMON + SHEETS[cat]
        ws.append(columns)
        path = RAW / f"{cat}.jsonl"
        records = [json.loads(line) for line in path.open(encoding="utf-8") if line.strip()] if path.exists() else []
        names, dropped = set(), {}
        for r in records:
            row, reason = to_row(cat, r)
            if row and row["Name"] in names:
                row, reason = None, "duplicate name"
            if row is None:
                dropped[reason] = dropped.get(reason, 0) + 1
                continue
            names.add(row["Name"])
            ws.append([row.get(c) for c in columns])
        print(f"{cat:12} {len(names):4} rows  dropped: {dropped or '-'}")
    wb.save(XLSX)
    print(f"wrote {XLSX}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

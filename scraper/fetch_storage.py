"""
Build the catalog's STORAGE entries (SSD/HDD) and merge them into clean_database.json.

WHY A SEPARATE SOURCE: the PCPartPicker Apify actor used for the rest of the catalog does not return
anything for its `storage` category (verified — every phrase yields 0 rows). Storage is instead sourced
from PassMark's public drive catalog (harddrivebenchmark.net), fetched live via the same cookie-primed
`/data/` JSON endpoint the site's own grid uses. This is real, current data.

PROVENANCE / honesty:
  * REAL — drive name, capacity, and SSD/HDD type come verbatim from PassMark's catalog.
  * EST  — FormFactor + Interface are inferred per product line by a documented rule (PassMark does not
           expose them). HDDs are 3.5"/SATA; SATA-SSD lines are 2.5"/SATA; the rest are M.2/NVMe.
  * Price is left 0 — the C# seeder's ComponentPriceCalculator derives a PLN price (same as any
           out-of-stock part), so no prices are invented here.
Curation is whitelist-based: only recognizable consumer product lines, deduped to a few capacities each.

Run: ./venv/bin/python fetch_storage.py
"""
import json
import os
import re
import urllib.request
from http.cookiejar import CookieJar

UA = ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/124.0 Safari/537.36")
PAGE = "https://www.harddrivebenchmark.net/hdd-mega-page.html"
DATA = "https://www.harddrivebenchmark.net/data/"
CATALOG = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "clean-db", "clean_database.json")

# Consumer product lines to keep: (brand, name-substring, FormFactor, Interface) for SSDs.
# The substring must contain the brand so off-brand collisions (e.g. "ACER SWIFT 980") are excluded.
NVME = "M.2-2280"
SSD_LINES = [
    ("Samsung", "Samsung 990 Pro", NVME, "PCIe 4.0 X4"),
    ("Samsung", "Samsung 990 EVO", NVME, "PCIe 4.0 X4"),
    ("Samsung", "Samsung 980 PRO", NVME, "PCIe 4.0 X4"),
    ("Western Digital", "WD BLACK SN850X", NVME, "PCIe 4.0 X4"),
    ("Western Digital", "WD BLACK SN770", NVME, "PCIe 4.0 X4"),
    ("Western Digital", "WD Blue SN5000", NVME, "PCIe 4.0 X4"),
    ("Crucial", "Crucial P3 Plus", NVME, "PCIe 4.0 X4"),
    ("Crucial", "Crucial P3", NVME, "PCIe 3.0 X4"),
    ("Crucial", "Crucial T500", NVME, "PCIe 4.0 X4"),
    ("Crucial", "Crucial T700", NVME, "PCIe 5.0 X4"),
    ("Kingston", "Kingston NV2", NVME, "PCIe 4.0 X4"),
    ("Kingston", "KINGSTON KC3000", NVME, "PCIe 4.0 X4"),
    ("Sabrent", "Sabrent Rocket 4", NVME, "PCIe 4.0 X4"),
    ("Lexar", "Lexar NM790", NVME, "PCIe 4.0 X4"),
    ("Solidigm", "Solidigm P44 Pro", NVME, "PCIe 4.0 X4"),
    ("Corsair", "Corsair MP600", NVME, "PCIe 4.0 X4"),
    ("Corsair", "Corsair MP700", NVME, "PCIe 5.0 X4"),
    ("ADATA", "ADATA LEGEND 960", NVME, "PCIe 4.0 X4"),
    ("SK Hynix", "SK Hynix Platinum P41", NVME, "PCIe 4.0 X4"),
    # SATA SSDs (2.5")
    ("Crucial", "Crucial MX500", '2.5"', "SATA III"),
    ("Crucial", "Crucial BX500", '2.5"', "SATA III"),
    ("Kingston", "Kingston A400", '2.5"', "SATA III"),
]
# HDD lines (3.5"/SATA forced regardless of any SSD variants sharing the line — Type field gates).
HDD_LINES = [
    ("Seagate", "Seagate BarraCuda"),
    ("Seagate", "Seagate IronWolf"),
    ("Western Digital", "WD Red"),
    ("Western Digital", "WD Blue"),
    ("Toshiba", "Toshiba"),
]
MAX_PER_LINE = 3  # cap capacities per product line for variety, not bulk


def fetch_drives():
    jar = CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    opener.addheaders = [("User-Agent", UA)]
    opener.open(PAGE, timeout=60).read()  # prime session cookie
    req = urllib.request.Request(DATA, headers={
        "User-Agent": UA, "Referer": PAGE, "X-Requested-With": "XMLHttpRequest",
        "Accept": "application/json, text/javascript, */*; q=0.01",
    })
    return json.loads(opener.open(req, timeout=90).read().decode())["data"]


def capacity_gb(name):
    m = re.search(r"(\d+(?:\.\d+)?)\s*TB", name, re.I)
    if m:
        return int(round(float(m.group(1)) * 1000))
    m = re.search(r"(\d+)\s*GB", name, re.I)
    return int(m.group(1)) if m else None


def build():
    print("=== Building STORAGE from PassMark drive catalog (harddrivebenchmark.net) ===")
    drives = fetch_drives()
    print(f"  fetched {len(drives)} drives")

    entries = []
    for is_hdd, lines in ((False, SSD_LINES), (True, HDD_LINES)):
        for spec in lines:
            brand, sub = spec[0], spec[1]
            ff = None if is_hdd else spec[2]
            iface = None if is_hdd else spec[3]
            want_type = "HDD" if is_hdd else "SSD"

            picks = {}
            for r in drives:
                name = (r.get("name") or "").strip()
                if sub.lower() not in name.lower():
                    continue
                if (r.get("type") or "") != want_type:
                    continue
                cap = capacity_gb(name)
                if not cap or cap < 240 or cap > 24000:
                    continue
                if cap in picks:
                    continue  # one drive per capacity per line
                picks[cap] = (name, cap)
            for name, cap in sorted(picks.values(), key=lambda x: x[1])[:MAX_PER_LINE]:
                specs = {
                    "Capacity": str(cap),
                    "FormFactor": '3.5"' if is_hdd else ff,
                    "Interface": "SATA III" if is_hdd else iface,
                    "Type": want_type,
                }
                entries.append({"Category": "Hdd" if is_hdd else "Ssd", "Name": name,
                                "Brand": brand, "Price": 0.0, "TechnicalSpecs": specs})

    # merge: drop any existing storage, append the freshly built set
    catalog = json.load(open(CATALOG, encoding="utf-8"))
    kept = [c for c in catalog if c["Category"] not in ("Ssd", "Hdd")]
    merged = kept + entries
    with open(CATALOG, "w", encoding="utf-8") as f:
        json.dump(merged, f, indent=4, ensure_ascii=False)

    n_ssd = sum(1 for e in entries if e["Category"] == "Ssd")
    n_hdd = sum(1 for e in entries if e["Category"] == "Hdd")
    print(f"  built {len(entries)} storage entries (SSD {n_ssd}, HDD {n_hdd})")
    print(f"=== DONE — catalog now {len(merged)} parts ({CATALOG}) ===")


if __name__ == "__main__":
    build()

"""Convert data/catalog.xlsx (one sheet per category) into the clean_database.json the C# seeder reads.

Run: python scraper/excel_to_catalog.py --snapshot-date 2026-11-08
"""
import argparse
import json
import re
import sys
from datetime import date
from pathlib import Path

from openpyxl import load_workbook

REPO = Path(__file__).resolve().parent.parent
XLSX = REPO / "data" / "catalog.xlsx"
OUTPUT = REPO / "src" / "Struct.API" / "Extensions" / "Seeding" / "clean-db" / "clean_database.json"

COMMON = ["Name", "Brand", "PricePln", "PriceSource", "SourceUrl"]

# Spec columns = the TechnicalSpecs keys each C# parser reads (src/Struct.BLL/Core/Specs/Spec.cs).
SHEETS = {
    "Cpu": ["Cores", "Threads", "BaseClock", "BoostClock", "TDP", "Socket", "MemoryType"],
    "Gpu": ["Chipset", "VRAM", "CoreClock", "Length", "TDP", "Interface"],
    "Motherboard": ["Socket", "FormFactor", "RamType", "MaxRam", "RamSlots"],
    "Ram": ["Type", "Capacity", "Speed", "Modules"],
    "Psu": ["Wattage", "FormFactor", "Efficiency", "Modular"],
    "Case": ["FormFactor", "MaxGpuLength", "MaxCoolerHeight", "SupportedMotherboards", "SidePanel"],
    "Cooler": ["CpuSockets", "Height", "WaterCooled", "RadiatorSize"],
    "Ssd": ["Capacity", "FormFactor", "Interface"],
    "Hdd": ["Capacity", "FormFactor", "Interface"],
}
NUMERIC = {"Cores", "Threads", "BaseClock", "BoostClock", "TDP", "VRAM", "CoreClock", "Length", "MaxRam",
           "RamSlots", "Capacity", "Speed", "Modules", "Wattage", "MaxGpuLength", "MaxCoolerHeight", "Height",
           "RadiatorSize"}
REQUIRED = {"Name", "Brand", "PriceSource", "SourceUrl"}
# StorageParser tells SSD from HDD by Type; the sheet already says which.
FIXED_SPECS = {"Ssd": {"Type": "SSD"}, "Hdd": {"Type": "HDD"}}
PLAIN_NUMBER = re.compile(r"\d+(\.\d+)?")
LIST_SYNTAX = re.compile(r"[\[\]'\"]")


class CatalogError(Exception):
    def __init__(self, errors):
        super().__init__("\n".join(errors))
        self.errors = errors


def convert(path, snapshot_date):
    wb = load_workbook(path, data_only=True)
    errors, seen, catalog = [], {}, []
    for sheet in SHEETS:
        if sheet in wb.sheetnames:
            catalog += _read_sheet(wb[sheet], sheet, snapshot_date.isoformat(), errors, seen)
        else:
            errors.append(f"{sheet}: sheet is missing")
    if errors:
        raise CatalogError(errors)
    return catalog


def _read_sheet(ws, sheet, snapshot, errors, seen):
    rows = ws.iter_rows(values_only=True)
    columns = [_clean(c) for c in next(rows, ())]
    header_errors = [f"missing column {c}" for c in COMMON if c not in columns]
    header_errors += [f"unknown column {c}" for c in columns if c and c not in COMMON and c not in SHEETS[sheet]]
    errors += [f"{sheet}, row 1: {e}" for e in header_errors]
    if header_errors:
        return []

    items = []
    for number, values in enumerate(rows, start=2):
        cells = {c: _clean(v) for c, v in zip(columns, values) if c}
        if all(v is None for v in cells.values()):
            continue
        where = f"{sheet}, row {number}"
        row_errors = _check_row(cells, SHEETS[sheet], where, seen)
        errors += [f"{where}: {e}" for e in row_errors]
        if not row_errors:
            items.append(_item(sheet, cells, snapshot))
    return items


def _check_row(cells, specs, where, seen):
    errors = []
    name = cells.get("Name")
    if name in seen:
        errors.append(f"Name {name!r} duplicates {seen[name]}")
    elif name is not None:
        seen[name] = where
    errors += [e for e in (_check_cell(c, cells.get(c)) for c in COMMON + specs) if e]
    return errors


def _check_cell(column, value):
    if column == "PricePln":
        price = _number(value)
        return None if price is not None and price > 0 else f"PricePln must be > 0, got {value!r}"
    if value is None:
        return f"{column} is required" if column in REQUIRED else None
    if column in NUMERIC and _number(value) is None:
        return f"{column} must be a number without units, got {value!r}"
    if column == "SupportedMotherboards" and LIST_SYNTAX.search(str(value)):
        return f"SupportedMotherboards must be comma-separated, got {value!r}"
    return None


def _item(sheet, cells, snapshot):
    specs = {c: str(cells[c]) for c in SHEETS[sheet] if cells.get(c) is not None}
    return {
        "Category": sheet,
        "Name": str(cells["Name"]),
        "Brand": str(cells["Brand"]),
        "PricePln": _number(cells["PricePln"]),
        "PriceSource": str(cells["PriceSource"]),
        "SourceUrl": str(cells["SourceUrl"]),
        "SnapshotDate": snapshot,
        "TechnicalSpecs": specs | FIXED_SPECS.get(sheet, {}),
    }


def _clean(value):
    if isinstance(value, str):
        return value.strip() or None
    return value


def _number(value):
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return value
    if isinstance(value, str) and PLAIN_NUMBER.fullmatch(value):
        return float(value)
    return None


def main(argv=None, xlsx=XLSX, output=OUTPUT):
    parser = argparse.ArgumentParser(description=f"Convert {XLSX.name} into {OUTPUT.name}.")
    parser.add_argument("--snapshot-date", required=True, type=date.fromisoformat,
                        help="day the prices were collected, YYYY-MM-DD")
    args = parser.parse_args(argv)
    try:
        catalog = convert(xlsx, args.snapshot_date)
    except CatalogError as e:
        print(e, file=sys.stderr)
        return 1
    output.write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {len(catalog)} components to {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

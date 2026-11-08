import json
from datetime import date

import pytest
from openpyxl import Workbook

from excel_to_catalog import COMMON, SHEETS, CatalogError, convert, main

SNAPSHOT = date(2026, 11, 8)


def header(sheet):
    return COMMON + SHEETS[sheet]


def make_book(tmp_path, drop=(), **sheets):
    wb = Workbook()
    wb.remove(wb.active)
    for name in SHEETS:
        if name in drop:
            continue
        ws = wb.create_sheet(name)
        for values in sheets.get(name, [header(name)]):
            ws.append(values)
    wb.create_sheet("README").append(["ignored"])
    path = tmp_path / "catalog.xlsx"
    wb.save(path)
    return path


def row(name, price=100, url="https://x-kom.pl/p/1", specs=()):
    return [name, "AMD", price, "xkom-2026-11", url, *specs]


def errors_of(path):
    with pytest.raises(CatalogError) as exc:
        convert(path, SNAPSHOT)
    return exc.value.errors


def test_converts_rows_to_seeder_json(tmp_path):
    path = make_book(
        tmp_path,
        Cpu=[header("Cpu"), [None, " "], row("Ryzen 5 7600", 899.99, specs=[6, "12", 3.8, 5.1, 65, " AM5 ", "DDR5"])],
        Case=[header("Case"), row("Lancool 216", specs=["ATX Mid Tower", 392, None, "ATX, Micro ATX", "Mesh"])],
        Cooler=[header("Cooler"), row("Peerless Assassin", specs=["AM5, LGA1700", 155, False, None])],
        Hdd=[header("Hdd"), row("IronWolf 4TB", specs=[4000.0, '3.5"', "SATA III"])],
    )

    catalog = convert(path, SNAPSHOT)

    assert [c["Name"] for c in catalog] == ["Ryzen 5 7600", "Lancool 216", "Peerless Assassin", "IronWolf 4TB"]
    assert catalog[0] == {
        "Category": "Cpu", "Name": "Ryzen 5 7600", "Brand": "AMD", "PricePln": 899.99,
        "PriceSource": "xkom-2026-11", "SourceUrl": "https://x-kom.pl/p/1", "SnapshotDate": "2026-11-08",
        "TechnicalSpecs": {"Cores": "6", "Threads": "12", "BaseClock": "3.8", "BoostClock": "5.1",
                           "TDP": "65", "Socket": "AM5", "MemoryType": "DDR5"},
    }
    assert catalog[1]["TechnicalSpecs"] == {"FormFactor": "ATX Mid Tower", "MaxGpuLength": "392",
                                            "SupportedMotherboards": "ATX, Micro ATX", "SidePanel": "Mesh"}
    assert catalog[2]["TechnicalSpecs"]["WaterCooled"] == "False"
    assert catalog[3]["TechnicalSpecs"] == {"Capacity": "4000", "FormFactor": '3.5"', "Interface": "SATA III",
                                            "Type": "HDD"}


def test_reports_every_error_with_sheet_and_row(tmp_path):
    path = make_book(
        tmp_path,
        Cpu=[header("Cpu"),
             row("Ryzen 5 7600", specs=["6", "12", "3.8 GHz"]),
             row("Ryzen 5 7600", price=0, specs=[True]),
             row("Core i5", price="tbd", url=None),
             row(None, price="1299.5")],
        Case=[header("Case"), row("Lancool 216", specs=["ATX", 392, 170, "['ATX', 'Micro ATX']"])],
    )

    assert errors_of(path) == [
        "Cpu, row 2: BaseClock must be a number without units, got '3.8 GHz'",
        "Cpu, row 3: Name 'Ryzen 5 7600' duplicates Cpu, row 2",
        "Cpu, row 3: PricePln must be > 0, got 0",
        "Cpu, row 3: Cores must be a number without units, got True",
        "Cpu, row 4: PricePln must be > 0, got 'tbd'",
        "Cpu, row 4: SourceUrl is required",
        "Cpu, row 5: Name is required",
        "Case, row 2: SupportedMotherboards must be comma-separated, got \"['ATX', 'Micro ATX']\"",
    ]


def test_rejects_unknown_columns_and_missing_sheets(tmp_path):
    path = make_book(tmp_path, drop=["Psu"],
                     Gpu=[["Name", "Brand", "PricePln", "PriceSource", "VRAM", "MaxCoolerHieght"], row("RTX 5070")])

    assert errors_of(path) == [
        "Gpu, row 1: missing column SourceUrl",
        "Gpu, row 1: unknown column MaxCoolerHieght",
        "Psu: sheet is missing",
    ]


def test_main_writes_catalog(tmp_path, capsys):
    path = make_book(tmp_path, Ram=[header("Ram"), row("Fury 32GB", specs=["DDR5", 32, 6000, 2])])
    out = tmp_path / "clean_database.json"

    assert main([str(path), str(out), "--snapshot-date", "2026-11-08"]) == 0

    assert json.loads(out.read_text(encoding="utf-8"))[0]["TechnicalSpecs"]["Speed"] == "6000"
    assert "1 components" in capsys.readouterr().out


def test_main_fails_without_writing(tmp_path, capsys):
    path = make_book(tmp_path, Ram=[header("Ram"), row("Fury 32GB", price=None)])
    out = tmp_path / "clean_database.json"

    assert main([str(path), str(out), "--snapshot-date", "2026-11-08"]) == 1

    assert not out.exists()
    assert "Ram, row 2: PricePln must be > 0, got None" in capsys.readouterr().err

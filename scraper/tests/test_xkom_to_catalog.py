import pytest

from excel_to_catalog import _check_row, COMMON, SHEETS
from xkom_to_catalog import form_factor, num, socket, to_row


@pytest.mark.parametrize("text, expected", [("4,2 GHz", 4.2), ("4 x DIMM", 4), ("650 W", 650), ("brak", None)])
def test_num(text, expected):
    assert num(text) == expected


@pytest.mark.parametrize("text, expected", [("Socket AM5", "AM5"), ("Socket 1700", "LGA1700"), (" 1851", "LGA1851")])
def test_socket(text, expected):
    assert socket(text) == expected


@pytest.mark.parametrize("text, expected", [("microATX", "Micro ATX"), ("Mini-ITX", "Mini ITX"),
                                            ("E-ATX", "EATX"), ("ATX", "ATX")])
def test_form_factor(text, expected):
    assert form_factor(text) == expected


def record(features, **extra):
    return {"fullName": "X", "producer": "AMD", "price": 1269, "url": "https://www.x-kom.pl/p/1.html",
            "category": "Procesor", "features": features, **extra}


def test_cpu_row_passes_converter_validation():
    row, reason = to_row("Cpu", record({
        "Gniazdo procesora (socket)": "Socket AM5", "Taktowanie rdzenia": "4.2 GHz (5.0 GHz w trybie turbo)",
        "Liczba rdzeni fizycznych": "8 rdzeni", "Liczba wątków": "16 wątków",
        "Rodzaj obsługiwanej pamięci": "DDR5-5200", "Pobór mocy (TDP)": "120 W"}))
    assert reason is None
    assert (row["Socket"], row["BaseClock"], row["BoostClock"], row["TDP"]) == ("AM5", 4.2, 5.0, 120)
    assert _check_row(row, SHEETS["Cpu"], "Cpu, row 2", {}) == []
    assert set(row) == set(COMMON + SHEETS["Cpu"])


def test_gpu_tdp_prefers_longest_chip_name():
    row, _ = to_row("Gpu", record({"Układ graficzny": "GeForce RTX 5060 Ti", "Pamięć": "16 GB"}))
    assert row["TDP"] == 180


def test_case_and_cooler_sockets_normalised():
    case, _ = to_row("Case", record({"Standard płyty głównej": "ATX, microATX, ITX",
                                     "Maksymalna wysokość chłodzenia CPU": "161 mm",
                                     "Maksymalna długość karty graficznej": "400 mm"}))
    cooler, _ = to_row("Cooler", record({"Kompatybilność": "1700, 1851, AM5", "Wysokość": "152 mm"},
                                        category="Chłodzenie procesora"))
    assert case["SupportedMotherboards"] == "ATX, Micro ATX, Mini ITX" and case["MaxCoolerHeight"] == 161
    assert cooler["CpuSockets"] == "LGA1700, LGA1851, AM5" and cooler["Height"] == 152


@pytest.mark.parametrize("speed_field", [{"Taktowanie": "6000 MHz"}, {"Szybkość transferu": "6000 MT/s"}])
def test_ram_speed_from_either_field(speed_field):
    row, _ = to_row("Ram", record({"Rodzaj pamięci": "DDR5", "Pojemność całkowita": "32 GB", **speed_field}))
    assert row["Speed"] == 6000


@pytest.mark.parametrize("features, radiator", [
    ({"Rozmiar radiatora": "396 x 120 x 27 mm", "Liczba wentylatorów": "3x 120 mm"}, 360),
    ({"Liczba wentylatorów": "2x 140 mm"}, 280),  # some AIO pages omit the radiator size
])
def test_aio_is_a_cooler_with_radiator_class_from_its_fans(features, radiator):
    row, reason = to_row("Cooler", record({"Kompatybilność": "1700, AM5", **features}, category="Chłodzenie wodne"))
    assert reason is None
    assert (row["WaterCooled"], row["RadiatorSize"], row["Height"]) == (True, radiator, None)


@pytest.mark.parametrize("cat, rec, reason", [
    ("Ram", record({"Rodzaj pamięci": "DDR5 SO-DIMM"}), "not a desktop part"),
    ("Cooler", record({"Kompatybilność": "AM5"}, category="Blok wodny"), "not a desktop part"),
    ("Cpu", record({}, price=None), "no price"),
    ("Motherboard", record({"Gniazdo procesora": "Socket AM5"}), "missing RamType, FormFactor"),
    ("Cpu", record({"Gniazdo procesora (socket)": "Socket AM5"}), "missing TDP"),
    ("Case", record({"Standard płyty głównej": "ATX"}), "missing MaxGpuLength, MaxCoolerHeight"),
    ("Cooler", record({"Kompatybilność": "AM5"}, category="Chłodzenie procesora"), "missing Height"),
])
def test_unusable_records_are_dropped_with_reason(cat, rec, reason):
    assert to_row(cat, rec) == (None, reason)

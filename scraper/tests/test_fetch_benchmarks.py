import csv
import json

import fetch_benchmarks as fb


class FakeResponse:
    def __init__(self, body):
        self.body = body

    def read(self):
        return self.body


class FakeOpener:
    def __init__(self, rows):
        self.body = json.dumps({"data": rows}).encode()
        self.addheaders = []

    def open(self, req, timeout):
        return FakeResponse(self.body)


def test_parse_mark_reads_thousands_separators_and_rejects_junk():
    assert fb.parse_mark("68,507") == 68507
    assert fb.parse_mark(4420.0) == 4420
    assert fb.parse_mark("NA") is None
    assert fb.parse_mark(None) is None
    assert fb.parse_mark("0") is None


def test_fetch_cpu_writes_cpu_mark_and_thread_mark(monkeypatch, tmp_path):
    rows = [
        {"name": " AMD Ryzen 7 9800X3D ", "cpumark": "39,910", "thread": "4,420"},
        {"name": "Old CPU", "cpumark": "1,000", "thread": "NA"},
        {"name": "No Mark CPU", "cpumark": "NA", "thread": "3,000"},
        {"name": "", "cpumark": "5,000", "thread": "2,000"},
    ]
    monkeypatch.setattr(fb.urllib.request, "build_opener", lambda *handlers: FakeOpener(rows))
    monkeypatch.setattr(fb, "OUT_DIR", str(tmp_path))

    assert fb.fetch("cpu") == 2
    with open(tmp_path / "CPU_benchmark_v4.csv", newline="", encoding="utf-8") as f:
        assert list(csv.reader(f)) == [
            ["cpuName", "cpuMark", "threadMark"],
            ["AMD Ryzen 7 9800X3D", "39910", "4420"],  # trimmed name, separators dropped
            ["Old CPU", "1000", ""],                   # optional mark missing -> blank
        ]                                              # no required mark / no name -> skipped


def test_main_refreshes_only_the_kinds_given(monkeypatch, tmp_path):
    fetched = []
    monkeypatch.setattr(fb, "fetch", fetched.append)
    monkeypatch.setattr(fb, "OUT_DIR", str(tmp_path))

    fb.main(["cpu"])
    fb.main([])

    assert fetched == ["cpu", "cpu", "gpu"]

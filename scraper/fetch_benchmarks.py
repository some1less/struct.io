"""
Refresh the PassMark benchmark CSVs that the scorer uses (CPU Mark + single-thread rating / G3D Mark).

PROVENANCE (thesis/defense): data is the public PassMark "mega page" dataset, fetched live from the
same JSON endpoint the page's DataTables grid uses (`/data/`). PassMark blocks naive requests, so we
send a browser User-Agent and first load the mega page to obtain the session cookie, exactly as a
browser would. No values are altered — we only project the name and mark columns into the CSV shape
`BenchmarkScores.FromCsvFiles` reads.

Run: ./venv/bin/python fetch_benchmarks.py [cpu] [gpu]   (no argument refreshes both)
Output: src/Struct.API/Extensions/Seeding/benchmarks/{CPU_benchmark_v4.csv, GPU_benchmarks_v7.csv}
"""
import csv
import json
import os
import sys
import urllib.request
from http.cookiejar import CookieJar

UA = ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/124.0 Safari/537.36")
OUT_DIR = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "benchmarks")

SOURCES = {
    # (mega page used to prime the cookie, data endpoint, out file, name header,
    #  [(json mark field, csv header)] — the first mark is required, the rest are written blank when missing)
    "cpu": ("https://www.cpubenchmark.net/CPU_mega_page.html",
            "https://www.cpubenchmark.net/data/", "CPU_benchmark_v4.csv", "cpuName",
            [("cpumark", "cpuMark"), ("thread", "threadMark")]),
    "gpu": ("https://www.videocardbenchmark.net/GPU_mega_page.html",
            "https://www.videocardbenchmark.net/data/", "GPU_benchmarks_v7.csv", "gpuName",
            [("g3d", "G3Dmark")]),
}


def parse_mark(value):
    try:
        mark = float(str(value).replace(",", ""))
    except (TypeError, ValueError):
        return None
    return int(mark) if mark > 0 else None


def fetch(kind):
    page, data_url, out_file, name_col, marks = SOURCES[kind]
    jar = CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    opener.addheaders = [("User-Agent", UA)]

    opener.open(page, timeout=60).read()  # prime session cookie like a browser
    req = urllib.request.Request(data_url, headers={
        "User-Agent": UA, "Referer": page,
        "X-Requested-With": "XMLHttpRequest",
        "Accept": "application/json, text/javascript, */*; q=0.01",
    })
    rows = json.loads(opener.open(req, timeout=90).read().decode())["data"]

    out_path = os.path.join(OUT_DIR, out_file)
    kept = 0
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([name_col] + [header for _, header in marks])
        for r in rows:
            name = (r.get("name") or "").strip()
            values = [parse_mark(r.get(field)) for field, _ in marks]
            if not name or values[0] is None:
                continue
            w.writerow([name] + ["" if v is None else v for v in values])
            kept += 1
    print(f"  {kind.upper()}: {len(rows)} rows -> {kept} written to {out_file}")
    return kept


if __name__ == "__main__":
    print("=== Refreshing PassMark benchmark CSVs (live, current) ===")
    os.makedirs(OUT_DIR, exist_ok=True)
    for kind in sys.argv[1:] or ("cpu", "gpu"):
        fetch(kind)
    print("=== DONE ===")

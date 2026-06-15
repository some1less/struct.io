"""
Refresh the PassMark benchmark CSVs that the scorer uses (CPU Mark / G3D Mark).

PROVENANCE (thesis/defense): data is the public PassMark "mega page" dataset, fetched live from the
same JSON endpoint the page's DataTables grid uses (`/data/`). PassMark blocks naive requests, so we
send a browser User-Agent and first load the mega page to obtain the session cookie, exactly as a
browser would. No values are altered — we only project the (name, mark) columns into the CSV shape the
existing `BenchmarkScores.FromCsvFiles` loader expects (so no .NET wiring changes).

Run: ./venv/bin/python fetch_benchmarks.py
Output: src/Struct.API/Extensions/Seeding/benchmarks/{CPU_benchmark_v4.csv, GPU_benchmarks_v7.csv}
"""
import csv
import json
import os
import urllib.request
from http.cookiejar import CookieJar

UA = ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/124.0 Safari/537.36")
OUT_DIR = os.path.join("..", "src", "Struct.API", "Extensions", "Seeding", "benchmarks")

SOURCES = {
    # (mega page used to prime the cookie, data endpoint, mark field, out file, name+mark headers)
    "cpu": ("https://www.cpubenchmark.net/CPU_mega_page.html",
            "https://www.cpubenchmark.net/data/", "cpumark",
            "CPU_benchmark_v4.csv", ("cpuName", "cpuMark")),
    "gpu": ("https://www.videocardbenchmark.net/GPU_mega_page.html",
            "https://www.videocardbenchmark.net/data/", "g3d",
            "GPU_benchmarks_v7.csv", ("gpuName", "G3Dmark")),
}


def fetch(kind):
    page, data_url, mark_field, out_file, (name_col, mark_col) = SOURCES[kind]
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
        w.writerow([name_col, mark_col])
        for r in rows:
            name = (r.get("name") or "").strip()
            mark = r.get(mark_field)
            try:
                mark = float(str(mark).replace(",", ""))
            except (TypeError, ValueError):
                continue
            if not name or mark <= 0:
                continue
            w.writerow([name, int(mark)])
            kept += 1
    print(f"  {kind.upper()}: {len(rows)} rows -> {kept} written to {out_file}")
    return kept


if __name__ == "__main__":
    print("=== Refreshing PassMark benchmark CSVs (live, current) ===")
    os.makedirs(OUT_DIR, exist_ok=True)
    for kind in ("cpu", "gpu"):
        fetch(kind)
    print("=== DONE ===")

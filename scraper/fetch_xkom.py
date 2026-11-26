"""Crawl x-kom.pl category listings + product pages into data/xkom_raw/<Category>.jsonl (raw, unmapped).

Resumable: products already in a file are skipped. Run from the repo root:
    python3 scraper/fetch_xkom.py [--per-cat 100]
"""
import argparse
import datetime
import json
import os
import re
import sys
import time
import urllib.request

BASE = "https://www.x-kom.pl"
UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "xkom_raw")
DELAY = 2.0
# Category -> [(listing path, share of --per-cat)]; RAM and water cooling over-fetch because SO-DIMM and
# custom-loop parts are dropped at mapping.
CATEGORIES = {
    "Cpu": [("/g-5/c/11-procesory.html", 1.0)],
    "Gpu": [("/g-5/c/345-karty-graficzne.html", 1.0)],
    "Motherboard": [("/g-5/c/14-plyty-glowne.html", 1.0)],
    "Ram": [("/g-5/c/28-pamieci-ram.html", 1.5)],
    "Psu": [("/g-5/c/2493-zasilacze-komputerowe.html", 1.0)],
    "Case": [("/g-5/c/388-obudowy-komputerowe.html", 1.0)],
    "Cooler": [("/g-5/c/105-chlodzenia-procesorow.html", 0.7), ("/g-5/c/2650-chlodzenia-wodne.html", 0.5)],
    "Ssd": [("/g-5/c/1779-dyski-ssd.html", 1.0)],
    "Hdd": [("/g-5/c/1580-dyski-hdd.html", 1.0)],
}


def state(path):
    """The page's embedded __INITIAL_STATE__['app'] JSON, or None after 3 failed attempts."""
    for attempt in range(3):
        try:
            req = urllib.request.Request(BASE + path, headers={"User-Agent": UA, "Accept-Language": "pl-PL,pl;q=0.9"})
            html = urllib.request.urlopen(req, timeout=30).read().decode("utf-8")
            m = re.search(r"window\.__INITIAL_STATE__\['app'\] = ", html)
            return json.JSONDecoder().raw_decode(html[m.end():])[0]
        except Exception as e:
            print(f"  attempt {attempt + 1} {path}: {e}", file=sys.stderr, flush=True)
            if getattr(e, "code", None) == 404:
                return None  # the product is gone; retrying will not bring it back
            if attempt < 2:
                time.sleep(DELAY * 5 * (attempt + 1))
        finally:
            time.sleep(DELAY)
    return None


def listing(path, n):
    """First n (id, webUrl) pairs of a category listing in x-kom's default order."""
    found, page = {}, 1
    while len(found) < n:
        s = state(f"{path}?page={page}")
        if not s:
            break
        ids = [x["id"] for x in (s.get("productsLists") or {}).get("listingContainer", [])]
        products = s.get("products") or {}
        for i in ids:
            if "webUrl" in (products.get(i) or {}):
                found.setdefault(i, products[i]["webUrl"])
        if not ids or page >= (s.get("listing") or {}).get("totalPages", page):
            break
        page += 1
    return list(found.items())[:n]


def record(pid, p):
    price_info = p.get("priceInfo") or {}
    return {
        "id": pid,
        "fullName": p["fullName"],
        "producer": (p.get("producer") or {}).get("name"),
        "producerCode": p.get("producerCode"),
        "price": p.get("price") if price_info.get("isPriceVisible", True) else None,
        "oldPrice": p.get("oldPrice"),
        "minPrice30d": price_info.get("minPrice"),
        "availability": p.get("availabilityStatus"),
        "url": BASE + p["webUrl"],
        "category": (p.get("category") or {}).get("singularName"),
        "features": {f["key"]: ", ".join(v["name"] for v in f.get("values") or []) for f in p.get("features") or []},
        "scrapedAt": datetime.date.today().isoformat(),
    }


def saved_ids(out):
    """Ids already in out. A crash mid-write leaves a torn last line; it is dropped so the file stays valid
    jsonl and new records start on a clean line, and that product is fetched again."""
    if not os.path.exists(out):
        return set()
    with open(out, encoding="utf-8") as f:
        lines = [line for line in f if line.strip()]
    good = []
    for line in lines:
        try:
            good.append(json.loads(line))
        except ValueError:
            pass
    if len(good) < len(lines) or (lines and not lines[-1].endswith("\n")):
        with open(out, "w", encoding="utf-8") as f:
            f.writelines(json.dumps(r, ensure_ascii=False) + "\n" for r in good)
    return {r["id"] for r in good}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--per-cat", type=int, default=100)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    for cat, sources in CATEGORIES.items():
        out = os.path.join(OUT, f"{cat}.jsonl")
        done = saved_ids(out)
        with open(out, "a", encoding="utf-8") as f:
            for path, share in sources:
                todo = [(i, url) for i, url in listing(path, round(args.per_cat * share)) if i not in done]
                print(f"{cat}: {path} -> {len(todo)} to fetch", flush=True)
                for n, (pid, url) in enumerate(todo, 1):
                    s = state(url)
                    p = (s or {}).get("products", {}).get(pid)
                    if not p or "fullName" not in p:
                        print(f"  skip {pid} (no product data)", file=sys.stderr, flush=True)
                        continue
                    f.write(json.dumps(record(pid, p), ensure_ascii=False) + "\n")
                    f.flush()
                    done.add(pid)
                    if n % 20 == 0:
                        print(f"  {cat} {n}/{len(todo)}", flush=True)


if __name__ == "__main__":
    main()

import json
import urllib.error

import fetch_xkom as fx


class FakeResponse:
    def __init__(self, body):
        self.body = body

    def read(self):
        return self.body


def page(app):
    return f"<script>window.__INITIAL_STATE__['app'] = {json.dumps(app)};</script>".encode()


def test_state_reads_the_embedded_json(monkeypatch):
    monkeypatch.setattr(fx.time, "sleep", lambda s: None)
    monkeypatch.setattr(fx.urllib.request, "urlopen", lambda req, timeout: FakeResponse(page({"products": {"1": {}}})))
    assert fx.state("/p/1.html") == {"products": {"1": {}}}


def test_state_gives_up_after_three_failures(monkeypatch):
    calls = []

    def down(req, timeout):
        calls.append(req.full_url)
        raise OSError("down")

    monkeypatch.setattr(fx.time, "sleep", lambda s: None)
    monkeypatch.setattr(fx.urllib.request, "urlopen", down)
    assert fx.state("/p/1.html") is None
    assert calls == ["https://www.x-kom.pl/p/1.html"] * 3


def test_state_does_not_retry_a_missing_page(monkeypatch):
    calls = []

    def gone(req, timeout):
        calls.append(req.full_url)
        raise urllib.error.HTTPError(req.full_url, 404, "Not Found", None, None)

    monkeypatch.setattr(fx.time, "sleep", lambda s: None)
    monkeypatch.setattr(fx.urllib.request, "urlopen", gone)
    assert fx.state("/p/1.html") is None
    assert len(calls) == 1


def fake_listing_state(pages):
    def state(path):
        ids = pages[int(path.rsplit("=", 1)[1])]
        return {"productsLists": {"listingContainer": [{"id": i} for i in ids]},
                "products": {i: {"webUrl": f"/p/{i}.html"} for i in ids},
                "listing": {"totalPages": len(pages)}}
    return state


def test_listing_stops_once_it_has_enough_ids(monkeypatch):
    monkeypatch.setattr(fx, "state", fake_listing_state({1: ["a", "b"], 2: ["c", "d"], 3: ["e"]}))
    assert fx.listing("/c/x.html", 3) == [("a", "/p/a.html"), ("b", "/p/b.html"), ("c", "/p/c.html")]


def test_listing_stops_at_the_last_page(monkeypatch):
    monkeypatch.setattr(fx, "state", fake_listing_state({1: ["a"], 2: ["b"]}))
    assert [i for i, _ in fx.listing("/c/x.html", 10)] == ["a", "b"]


def test_listing_skips_ids_without_product_data(monkeypatch):
    monkeypatch.setattr(fx, "state", lambda path: {"productsLists": {"listingContainer": [{"id": "a"}, {"id": "b"}]},
                                                   "products": {"a": {"webUrl": "/p/a.html"}}})
    assert fx.listing("/c/x.html", 5) == [("a", "/p/a.html")]


def test_record_hides_an_invisible_price_and_flattens_features():
    product = {"fullName": "AMD Ryzen 5 7600", "producer": {"name": "AMD"}, "price": 999,
               "priceInfo": {"isPriceVisible": False}, "webUrl": "/p/1.html", "category": {"singularName": "Procesor"},
               "features": [{"key": "Socket", "values": [{"name": "AM5"}]},
                            {"key": "Pamięć", "values": [{"name": "DDR5"}, {"name": "DDR4"}]}]}
    r = fx.record("1", product)
    assert (r["price"], r["producer"], r["url"]) == (None, "AMD", "https://www.x-kom.pl/p/1.html")
    assert r["features"] == {"Socket": "AM5", "Pamięć": "DDR5, DDR4"}


def crawl_cpu_after(monkeypatch, tmp_path, existing):
    """Runs main() over a two-product listing with Cpu.jsonl already holding `existing`; returns the URLs fetched
    and the ids in the file afterwards."""
    out = tmp_path / "Cpu.jsonl"
    out.write_text(existing, encoding="utf-8")
    fetched = []

    def state(url):
        fetched.append(url)
        return {"products": {"2": {"fullName": "AMD Ryzen 7 7800X3D", "webUrl": url}}}

    monkeypatch.setattr(fx, "OUT", str(tmp_path))
    monkeypatch.setattr(fx, "CATEGORIES", {"Cpu": [("/c/cpu.html", 1.0)]})
    monkeypatch.setattr(fx, "listing", lambda path, n: [("1", "/p/1.html"), ("2", "/p/2.html")])
    monkeypatch.setattr(fx, "state", state)
    monkeypatch.setattr(fx.sys, "argv", ["fetch_xkom.py", "--per-cat", "2"])
    fx.main()
    return fetched, [json.loads(line)["id"] for line in out.read_text(encoding="utf-8").splitlines()]


def test_main_resumes_without_refetching_saved_products(monkeypatch, tmp_path):
    assert crawl_cpu_after(monkeypatch, tmp_path, json.dumps({"id": "1"}) + "\n") == (["/p/2.html"], ["1", "2"])


def test_main_drops_a_torn_last_line_and_fetches_that_product_again(monkeypatch, tmp_path):
    torn = json.dumps({"id": "1"}) + "\n" + '{"id": "2", "fullNa'
    assert crawl_cpu_after(monkeypatch, tmp_path, torn) == (["/p/2.html"], ["1", "2"])

#!/usr/bin/env python3
"""Markdown job summaries for CI, appended to $GITHUB_STEP_SUMMARY (stdout when unset).

  ci_summary.py steps <title> <Step=outcome>...         table of step outcomes
  ci_summary.py tests <title> <report-glob>...          TRX (dotnet) or JUnit XML (vitest, Playwright)
  ci_summary.py gitleaks <log> <report.json> <image>    secret-scan result
  ci_summary.py result                                  overall table from $NEEDS; exits 1 unless all passed
"""
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

LABEL = {"success": "passed", "failure": "failed", "cancelled": "cancelled", "skipped": "skipped"}
STAGES = {
    "secrets": "Secret scan (gitleaks)",
    "backend": "Backend: build, xUnit",
    "frontend": "Frontend: lint, build, vitest",
    "e2e": "E2E: docker compose, smoke, Playwright",
}


def write(lines):
    out = os.environ.get("GITHUB_STEP_SUMMARY")
    with open(out, "a") if out else sys.stdout as f:
        f.write("\n".join(lines) + "\n\n")


def cell(text):
    return (str(text).strip().splitlines() or [""])[0].replace("|", "\\|")[:200]


def steps(title, *pairs):
    rows = [f"| {name} | {LABEL.get(outcome, outcome or 'not run')} |"
            for name, _, outcome in (p.partition("=") for p in pairs)]
    write([f"### {title}", "", "| Step | Result |", "|---|---|", *rows])


def local(tag):
    return tag.rsplit("}", 1)[-1]


def trx_seconds(value):
    h, m, s = (value or "0:0:0").split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def parse(path):
    root = ET.parse(path).getroot()
    if local(root.tag) == "TestRun":
        for r in root.iter():
            if local(r.tag) == "UnitTestResult":
                outcome = {"Passed": "passed", "Failed": "failed"}.get(r.get("outcome"), "skipped")
                message = next((e.text or "" for e in r.iter() if local(e.tag) == "Message"), "")
                yield r.get("testName"), outcome, trx_seconds(r.get("duration")), message
        return
    for case in root.iter("testcase"):
        failure = next((e for e in case if e.tag in ("failure", "error")), None)
        if failure is not None:
            outcome, message = "failed", failure.get("message") or failure.text or ""
        else:
            outcome, message = ("skipped" if case.find("skipped") is not None else "passed"), ""
        name = " > ".join(filter(None, [case.get("classname"), case.get("name")]))
        yield name, outcome, float(case.get("time") or 0), message


def tests(title, *patterns):
    paths = sorted(p for pattern in patterns for p in glob.glob(pattern))
    lines = [f"### {title}", ""]
    if not paths:
        write(lines + ["No test report was produced (the run stopped before tests)."])
        return
    cases = [c for path in paths for c in parse(path)]
    count = {k: sum(1 for c in cases if c[1] == k) for k in ("passed", "failed", "skipped")}
    lines += [
        "| Total | Passed | Failed | Skipped | Duration |",
        "|------:|-------:|-------:|--------:|---------:|",
        f"| {len(cases)} | {count['passed']} | {count['failed']} | {count['skipped']} "
        f"| {sum(c[2] for c in cases):.1f} s |",
    ]
    failed = [c for c in cases if c[1] == "failed"]
    if failed:
        lines += ["", "| Failed test | Message |", "|---|---|"]
        lines += [f"| {cell(name)} | {cell(message)} |" for name, _, _, message in failed]
    write(lines)


def gitleaks(log_path, report_path, image):
    log = open(log_path).read() if os.path.exists(log_path) else ""
    commits = re.search(r"(\d+) commits scanned", log)
    findings = json.load(open(report_path)) if os.path.exists(report_path) else None
    lines = ["### Secret scan", ""]
    if findings is None:
        write(lines + [f"`{image}` did not produce a report; see the step log."])
        return
    scanned = f"{commits.group(1)} commits" if commits else "full history"
    lines.append(f"`{image}` scanned {scanned}: {len(findings)} finding(s).")
    if findings:
        lines += ["", "| Rule | File | Line | Commit |", "|---|---|---:|---|"]
        lines += [f"| {cell(f['RuleID'])} | {cell(f['File'])} | {f['StartLine']} | {f['Commit'][:8]} |"
                  for f in findings]
    write(lines)


def result():
    needs = json.loads(os.environ["NEEDS"])
    ok = all(v["result"] == "success" for v in needs.values())
    order = [k for k in STAGES if k in needs] + [k for k in needs if k not in STAGES]
    rows = [f"| {STAGES.get(k, k)} | {LABEL.get(needs[k]['result'], needs[k]['result'])} |" for k in order]
    write([f"## CI {'passed' if ok else 'failed'}", "", "| Stage | Result |", "|---|---|", *rows,
           "", "Per-stage details are in the job summaries below."])
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    command, *args = sys.argv[1:]
    {"steps": steps, "tests": tests, "gitleaks": gitleaks, "result": result}[command](*args)

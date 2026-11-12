#!/usr/bin/env python3
"""Validate fresh TRX report(s); fail on zero execution, skips, or failures."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

NAMESPACE = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
COUNTER_NAMES = (
    "total",
    "executed",
    "passed",
    "failed",
    "error",
    "timeout",
    "aborted",
    "inconclusive",
    "notExecuted",
    "disconnected",
)


def report_files(path):
    if path.is_dir():
        return sorted(path.rglob("*.trx"))
    if path.is_file():
        return [path]
    return []


def read_counters(report):
    tree = ET.parse(report)
    counters = tree.getroot().find(f".//{NAMESPACE}ResultSummary/{NAMESPACE}Counters")
    if counters is None:
        raise ValueError(f"{report}: missing ResultSummary/Counters")

    result = {}
    for name in COUNTER_NAMES:
        raw = counters.get(name)
        if raw is None:
            result[name] = 0
            continue
        try:
            result[name] = int(raw)
        except ValueError as exc:
            raise ValueError(f"{report}: invalid counter {name}={raw!r}") from exc
    return result


def main(argv):
    if len(argv) != 2:
        print("usage: check_trx.py <report.trx|results-dir>", file=sys.stderr)
        return 2

    path = Path(argv[1])
    reports = report_files(path)
    if not reports:
        print(f"error: no TRX report found at {path}", file=sys.stderr)
        return 1

    totals = {name: 0 for name in COUNTER_NAMES}
    for report in reports:
        try:
            if report.stat().st_size == 0:
                print(f"error: empty TRX report: {report}", file=sys.stderr)
                return 1
            counters = read_counters(report)
        except (ET.ParseError, OSError, ValueError) as exc:
            print(f"error: {exc}", file=sys.stderr)
            return 1

        for name in COUNTER_NAMES:
            totals[name] += counters[name]

    print("TRX reports: " + ", ".join(str(report) for report in reports))
    print("TRX counters: " + " ".join(f"{name}={totals[name]}" for name in COUNTER_NAMES))

    problems = []
    if totals["total"] <= 0:
        problems.append("total<=0")
    if totals["executed"] <= 0:
        problems.append("executed<=0")
    if totals["passed"] <= 0:
        problems.append("passed<=0")
    for name in ("failed", "error", "timeout", "aborted", "inconclusive", "notExecuted", "disconnected"):
        if totals[name] != 0:
            problems.append(f"{name}!=0")
    if totals["executed"] != totals["total"]:
        problems.append("executed!=total")
    if totals["passed"] != totals["total"]:
        problems.append("passed!=total")

    if problems:
        print("error: TRX report does not prove a complete green run: " + ", ".join(problems), file=sys.stderr)
        return 1

    print("TRX report proves a complete green run")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

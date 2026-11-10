#!/usr/bin/env python3
"""Offline checks for the current design/issue plan; never gameplay acceptance."""
from pathlib import Path
from urllib.parse import unquote, urlsplit
import argparse
import json
import re

EXPECTED_IDS = {"GOAL", "V1", "F-01", "F-02", "F-03"} | {
    f"V1-{i:02}" for i in range(1, 13)
}
PUBLISHED_IDS = EXPECTED_IDS - {"F-01", "F-02", "F-03"}
REPOSITORY = "sagitrs/ps-mod-kourindou"
MAIN_ISSUE = {"number": 1, "url": f"https://github.com/{REPOSITORY}/issues/1"}


def no_duplicate_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def nonfinite(value):
    raise ValueError(f"non-finite JSON value: {value}")


def check(root):
    root = root.resolve(strict=True)
    if not root.is_dir():
        raise ValueError("root is not a directory")
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    def owned_file(path):
        # Reject symlink components, even if their targets happen to be in-tree.
        resolved = path.resolve()
        if root not in resolved.parents:
            raise ValueError(f"path escapes root: {path.name}")
        current = path
        while current != root:
            if current.is_symlink():
                raise ValueError(f"symlink input: {current.name}")
            current = current.parent
        return path

    def read(path):
        return owned_file(path).read_text(encoding="utf-8")

    # This is a declared document set, not a scan of .git, builds or game assets.
    markdown = {root / "README.md", root / "tools/docs/README.md"}
    for folder in ("docs", "planning"):
        base = root / folder
        if base.is_symlink() or not base.is_dir():
            raise ValueError(f"missing/invalid document directory: {folder}")
        markdown.update(base.rglob("*.md"))
    local_links = external_links = 0
    for path in sorted(markdown):
        text = read(path)
        require(bool(text.strip()), f"empty document: {path.relative_to(root)}")
        require(not re.search(r"(?m)[ \t]+$", text), f"trailing whitespace: {path.relative_to(root)}")
        for link in re.findall(r"\[[^\]\n]+\]\(([^)\n]+)\)", text):
            url = urlsplit(link)
            if url.scheme or url.netloc:
                require(url.scheme in ("http", "https") and bool(url.netloc), f"invalid external URL: {link}")
                external_links += 1
                continue
            local_links += 1
            require(not Path(unquote(url.path)).is_absolute(), f"absolute local link: {link}")
            require(not url.fragment, f"fragment links need an anchor check: {link}")
            target = path.parent / unquote(url.path) if url.path else path
            resolved = target.resolve()
            require(root in resolved.parents, f"local link escapes root: {link}")
            if root in resolved.parents:
                require(owned_file(target).is_file(), f"broken local link: {path.relative_to(root)} -> {link}")

    index_path = root / "planning/issues.json"
    if owned_file(index_path).stat().st_size > 1024 * 1024:
        raise ValueError("issue index exceeds 1 MiB")
    index = json.loads(read(index_path), object_pairs_hook=no_duplicate_keys, parse_constant=nonfinite)
    require(index["schema_version"] == 3 and index["kind"] == "issue-plan", "unsupported index schema")
    require(index["remote_repository"] == REPOSITORY, "repository mismatch")
    require(index["publication_status"] == "v1-issues-created", "publication scope mismatch")
    entries = index["issues"]
    if not isinstance(entries, list) or not all(isinstance(x, dict) for x in entries):
        raise ValueError("issues must be a list of objects")
    ids = [entry["id"] for entry in entries]
    require(len(ids) == len(set(ids)) == len(EXPECTED_IDS) and set(ids) == EXPECTED_IDS and index["count"] == len(entries), "issue inventory mismatch")
    by_id = {entry["id"]: entry for entry in entries}

    def table_ids(file, prefix):
        return set(re.findall(r"^\| (" + prefix + r"\d{2}) \|", read(root / "docs" / file), re.M))

    requirements = table_ids("01-product-design.md", "R")
    acceptance = table_ids("05-validation.md", "AC")
    decisions = table_ids("03-decisions.md", "D")
    require(requirements == {f"R{i:02}" for i in range(1, 11)}, "requirement table incomplete")
    require(acceptance == {f"AC{i:02}" for i in range(1, 21)}, "acceptance table incomplete")
    require(decisions == {f"D{i:02}" for i in range(1, 14)}, "decision table incomplete")
    remote_count = 0
    remote_numbers = []
    for entry in entries:
        id = entry["id"]
        parent = None if id == "GOAL" else ("V1" if id.startswith("V1-") else "GOAL")
        require(entry["parent"] == parent, f"wrong parent: {id}")
        remote = entry["remote_issue"]
        valid_remote = False
        if id in PUBLISHED_IDS:
            valid_remote = (
                isinstance(remote, dict) and set(remote) == {"number", "url"}
                and type(remote["number"]) is int and remote["number"] > 0
                and remote["url"] == f"https://github.com/{REPOSITORY}/issues/{remote['number']}"
            )
            require(valid_remote, f"invalid issue mapping: {id}")
            if valid_remote:
                remote_numbers.append(remote["number"])
        else:
            require(remote is None, f"unpublished task has remote mapping: {id}")
        if id == "GOAL":
            require(valid_remote and remote == MAIN_ISSUE, "main issue mapping mismatch")
        remote_count += remote is not None
        for key, allowed in (("requirements", requirements), ("acceptance", acceptance), ("decisions", decisions)):
            require(set(entry[key]) <= allowed, f"unknown {key}: {id}")
        require(set(entry["depends_on"]) <= set(ids), f"dangling dependency: {id}")
        require(id not in entry["depends_on"], f"self dependency: {id}")
        require(entry["body_file"] == f"issues/{id}.md", f"body path mismatch: {id}")
        # Use the constrained path, never an arbitrary path from the JSON.
        body = read(root / "planning/issues" / f"{id}.md")
        require(body.startswith("# " + entry["title"] + "\n"), f"title mismatch: {id}")
        require(body.count(f"<!-- kourindou-plan:{id} -->") == 1, f"marker mismatch: {id}")
        if valid_remote:
            require(f"[Issue #{remote['number']}]({remote['url']})" in body, f"body issue mapping missing: {id}")
        require("- [ ] " in body and "- [x]" not in body.lower(), f"acceptance is missing/already checked: {id}")
        for ref in ([parent] if parent else []) + entry["depends_on"]:
            require(f"[{ref}]({ref}.md)" in body, f"body relation missing: {id} -> {ref}")
    require(remote_count == len(PUBLISHED_IDS), "published issue count mismatch")
    require(len(remote_numbers) == len(set(remote_numbers)), "duplicate remote issue number")

    for graph in ("parent", "depends_on"):
        active, finished = set(), set()

        def visit(id):
            if id in active:
                errors.append(f"{graph} cycle: {id}")
                return
            if id in finished or id not in by_id:
                return
            active.add(id)
            value = by_id[id][graph]
            for child in value if isinstance(value, list) else ([value] if value else []):
                visit(child)
            active.remove(id)
            finished.add(id)

        for id in ids:
            visit(id)
    leaves = [entry for entry in entries if re.fullmatch(r"V1-\d{2}", entry["id"])]
    for key, expected in (("requirements", requirements), ("acceptance", acceptance)):
        covered = set().union(*(set(entry[key]) for entry in leaves))
        require(covered == expected, f"V1 {key} coverage incomplete")
    return {
        "scope": "document structure only; no game tests or remote API checks",
        "ok": not errors,
        "errors": errors,
        "counts": {
            "markdown_files": len(markdown), "local_links": local_links,
            "external_links_not_fetched": external_links, "planned_issues": len(entries),
            "mapped_issues": remote_count, "unpublished_drafts": len(entries) - remote_count,
            "v1_tasks": len(leaves), "requirements": len(requirements),
            "product_acceptance_cases_not_run": len(acceptance), "decision_entries": len(decisions),
        },
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    try:
        result = check(args.root)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RecursionError) as error:
        print(json.dumps({"ok": False, "input_error": str(error)}, ensure_ascii=False, indent=2))
        return 2
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

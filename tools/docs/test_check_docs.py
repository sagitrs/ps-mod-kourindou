#!/usr/bin/env python3
"""Exercise the actual document-check CLI, including rejection/invalid input."""
from pathlib import Path
import json
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[2]
CHECKER = Path(__file__).with_name("check_docs.py")


class DocumentChecks(unittest.TestCase):
    def setUp(self):
        temporary_root = Path.home() / "tmp"
        temporary_root.mkdir(mode=0o700, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="kourindou-doc-", dir=temporary_root)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "input"
        self.root.mkdir()
        shutil.copyfile(REPO / "README.md", self.root / "README.md")
        for folder in ("docs", "planning", "tools/docs"):
            shutil.copytree(REPO / folder, self.root / folder)

    def run_check(self, expected, root=None, default=False):
        command = [sys.executable, str(CHECKER)]
        if not default:
            command.append(str(self.root if root is None else root))
        process = subprocess.run(command, cwd=self.temporary.name, capture_output=True, text=True, timeout=15)
        self.assertEqual(process.returncode, expected, process.stdout + process.stderr)
        result = json.loads(process.stdout)
        self.assertIs(result["ok"], expected == 0)
        return result

    def update_index(self, change):
        path = self.root / "planning/issues.json"
        data = json.loads(path.read_text())
        change(data)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")

    def test_valid_fixture(self):
        result = self.run_check(0)
        self.assertEqual(result["counts"]["planned_issues"], 17)
        self.assertEqual(result["counts"]["mapped_main_issues"], 1)
        self.assertEqual(result["counts"]["unpublished_drafts"], 16)

    def test_default_root_from_other_directory(self):
        self.run_check(0, default=True)

    def test_missing_index_is_input_failure(self):
        (self.root / "planning/issues.json").unlink()
        self.run_check(2)

    def test_invalid_json(self):
        (self.root / "planning/issues.json").write_text("{not json")
        self.run_check(2)

    def test_duplicate_json_key(self):
        (self.root / "planning/issues.json").write_text('{"count":17,"count":18}')
        self.run_check(2)

    def test_nonfinite_json(self):
        (self.root / "planning/issues.json").write_text('{"count":NaN}')
        self.run_check(2)

    def test_schema_mismatch(self):
        self.update_index(lambda d: d.update(schema_version=999))
        self.assertIn("unsupported index schema", self.run_check(1)["errors"])

    def test_duplicate_task(self):
        def duplicate(data):
            data["issues"].append(next(x for x in data["issues"] if x["id"] == "V1-05"))
            data["count"] += 1
        self.update_index(duplicate)
        self.assertIn("issue inventory mismatch", self.run_check(1)["errors"])

    def test_missing_task(self):
        def omit(data):
            data["issues"] = [x for x in data["issues"] if x["id"] != "V1-05"]
            data["count"] -= 1
        self.update_index(omit)
        self.assertIn("issue inventory mismatch", self.run_check(1)["errors"])

    def test_dependency_cycle(self):
        self.update_index(lambda d: next(x for x in d["issues"] if x["id"] == "V1-01")["depends_on"].append("V1-02"))
        self.assertTrue(any("depends_on cycle" in e for e in self.run_check(1)["errors"]))

    def test_broken_local_link(self):
        with (self.root / "README.md").open("a") as f:
            f.write("\n[missing](docs/absent.md)\n")
        self.assertTrue(any("broken local link" in e for e in self.run_check(1)["errors"]))

    def test_link_cannot_escape_root(self):
        with (self.root / "README.md").open("a") as f:
            f.write("\n[outside](../outside.md)\n")
        self.assertTrue(any("escapes root" in e for e in self.run_check(1)["errors"]))

    def test_missing_issue_body_is_input_failure(self):
        (self.root / "planning/issues/V1-08.md").unlink()
        self.run_check(2)

    def test_unknown_requirement(self):
        self.update_index(lambda d: d["issues"][1]["requirements"].append("R99"))
        self.run_check(1)

    def test_wrong_parent(self):
        self.update_index(lambda d: d["issues"][1].update(parent="V1-01"))
        self.run_check(1)

    def test_unpublished_task_cannot_claim_remote_issue(self):
        self.update_index(lambda d: d["issues"][1].update(remote_issue={"number":2,"url":"https://github.com/sagitrs/ps-mod-kourindou/issues/2"}))
        self.run_check(1)

    def test_wrong_main_issue(self):
        self.update_index(lambda d: d["issues"][0]["remote_issue"].update(number=999))
        self.run_check(1)

    def test_boolean_is_not_issue_number(self):
        self.update_index(lambda d: d["issues"][0]["remote_issue"].update(number=True))
        self.run_check(1)

    def test_acceptance_is_not_already_done(self):
        path = self.root / "planning/issues/V1-08.md"
        path.write_text(path.read_text().replace("- [ ]", "- [x]", 1))
        self.run_check(1)

    def test_title_mismatch(self):
        self.update_index(lambda d: d["issues"][0].update(title="different title"))
        self.run_check(1)

    def test_coverage_hole(self):
        def remove(data):
            for entry in data["issues"]:
                if entry["id"].startswith("V1-"):
                    entry["acceptance"] = [x for x in entry["acceptance"] if x != "AC19"]
        self.update_index(remove)
        self.assertIn("V1 acceptance coverage incomplete", self.run_check(1)["errors"])

    def test_body_path_is_constrained(self):
        self.update_index(lambda d: d["issues"][0].update(body_file="../../outside.md"))
        self.run_check(1)

    def test_symlink_document_is_invalid_input(self):
        path = self.root / "docs/01-product-design.md"
        path.unlink()
        path.symlink_to(self.root / "README.md")
        self.run_check(2)

    def test_symlink_link_target_is_invalid_input(self):
        (self.root / "alias.txt").symlink_to(self.root / "README.md")
        with (self.root / "README.md").open("a") as f:
            f.write("\n[alias](alias.txt)\n")
        self.run_check(2)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(DocumentChecks)
    if suite.countTestCases() == 0:
        print("Document self-test suite is empty", file=sys.stderr)
        raise SystemExit(2)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() else 1)

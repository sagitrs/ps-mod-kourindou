#!/usr/bin/env python3
"""Exercise the preflight/evidence validator with synthetic inputs only."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest

TOOL = Path(__file__).with_name("preflight.py")
SCENARIOS = Path(__file__).with_name("scenarios.json")
TMP_ROOT = Path.home() / "tmp"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class PreflightTests(unittest.TestCase):
    def setUp(self):
        TMP_ROOT.mkdir(mode=0o700, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="kourindou-preflight-", dir=TMP_ROOT)
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "inputs"
        self.root.mkdir()
        self.reference_root = self.root / "game-reference"
        (self.reference_root / "Managed").mkdir(parents=True)
        self.assembly = self.reference_root / "Managed" / "Assembly-CSharp.dll"
        self.assembly.write_bytes(b"synthetic-assembly")
        self.candidate = self.root / "candidate" / "package.zip"
        self.candidate.parent.mkdir()
        self.candidate.write_bytes(b"synthetic-package")
        self.test_save = self.root / "test-save"
        self.test_save.mkdir()
        self.evidence_dir = self.root / "evidence"
        self.evidence_dir.mkdir()
        self.observed = self.evidence_dir / "observed.txt"
        self.observed.write_text("synthetic observation\n", encoding="utf-8")
        self.fixture = self.root / "fixture.bin"
        self.fixture.write_bytes(b"synthetic-fixture")
        self.manifest = self.valid_manifest()
        self.manifest_path = self.base / "manifest.json"
        self.write_json(self.manifest_path, self.manifest)

    def write_json(self, path: Path, value) -> None:
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def valid_manifest(self) -> dict:
        return {
            "schemaVersion": 1,
            "inputRoot": str(self.root),
            "sourceSha": "47a3f6a8360309718990cb15ef64b1fb229d4cc1",
            "gameBuildId": "1794536281",
            "expectedGameBuildId": "1794536281",
            "platform": "linux-proton",
            "protonVersion": "9.0-4",
            "referenceRoot": "game-reference",
            "references": [
                {"path": "Managed/Assembly-CSharp.dll", "sha256": sha256(self.assembly.read_bytes())},
            ],
            "candidatePackage": {"path": "candidate/package.zip", "sha256": sha256(self.candidate.read_bytes())},
            "testSave": {
                "root": "test-save",
                "isolatedCopy": True,
                "cloudSyncDisabled": True,
                "dailySaveUsed": False,
            },
        }

    def valid_record(self) -> dict:
        return {
            "schemaVersion": 1,
            "scenarioId": "single-character-native-flow",
            "layer": "game-scenario",
            "status": "PASS",
            "sourceSha": "47a3f6a8360309718990cb15ef64b1fb229d4cc1",
            "candidatePackageSha256": sha256(self.candidate.read_bytes()),
            "gameBuildId": "1794536281",
            "platform": "linux-proton",
            "protonVersion": "9.0-4",
            "fixture": {
                "id": "fixture-1",
                "digest": sha256(self.fixture.read_bytes()),
                "isolation": "private-test-prefix",
                "cloud": "disabled",
            },
            "expectedSource": "tools/preflight/scenarios.json#single-character-native-flow",
            "steps": ["launch", "native event", "save", "restart", "disable", "vanilla load"],
            "actual": "observed native event, save, restart and second-slot isolation",
            "evidencePaths": ["evidence/observed.txt"],
            "executor": "synthetic-test",
        }

    def run_cli(self, *args):
        process = subprocess.run(
            [sys.executable, str(TOOL), *args],
            cwd=self.base,
            capture_output=True,
            text=True,
            timeout=60,
        )
        try:
            payload = json.loads(process.stdout)
        except json.JSONDecodeError as exc:
            self.fail(f"CLI did not emit JSON: {process.stdout!r} / {process.stderr!r}: {exc}")
        return process.returncode, payload

    def check_inputs(self):
        return self.run_cli("check-inputs", "--allowed-root", str(self.root), str(self.manifest_path))

    def write_record(self, record: dict) -> Path:
        path = self.base / "record.json"
        self.write_json(path, record)
        return path

    def test_valid_inputs_pass_tool_only(self):
        code, payload = self.check_inputs()
        self.assertEqual(code, 0, payload)
        self.assertEqual(payload["status"], "PASS")
        self.assertEqual(payload["layer"], "preflight-tool")
        self.assertEqual(payload["gameExecution"], "UNCOVERED")
        self.assertEqual(payload["errors"], [])

    def test_missing_build_id_is_environment_invalid(self):
        manifest = self.valid_manifest()
        del manifest["gameBuildId"]
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertEqual(payload["status"], "ENVIRONMENT_INVALID")
        self.assertTrue(any("gameBuildId" in error for error in payload["errors"]))

    def test_build_id_mismatch_is_environment_invalid(self):
        manifest = self.valid_manifest()
        manifest["expectedGameBuildId"] = "9999999999"
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("does not match" in error for error in payload["errors"]))

    def test_missing_reference_root_is_environment_invalid(self):
        manifest = self.valid_manifest()
        manifest["referenceRoot"] = "missing-reference"
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertEqual(payload["status"], "ENVIRONMENT_INVALID")

    def test_reference_digest_mismatch_is_environment_invalid(self):
        manifest = self.valid_manifest()
        manifest["references"][0]["sha256"] = "0" * 64
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("digest mismatch" in error for error in payload["errors"]))

    def test_candidate_digest_mismatch_is_environment_invalid(self):
        manifest = self.valid_manifest()
        manifest["candidatePackage"]["sha256"] = "0" * 64
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("candidatePackage digest mismatch" in error for error in payload["errors"]))

    def test_relative_path_escape_is_rejected(self):
        manifest = self.valid_manifest()
        manifest["references"][0]["path"] = "../outside.dll"
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("inside the input root" in error for error in payload["errors"]))

    def test_symlink_escape_is_rejected(self):
        outside = self.base / "outside"
        outside.mkdir()
        secret = outside / "secret.dll"
        secret.write_bytes(b"synthetic-outside")
        link = self.reference_root / "Managed" / "link.dll"
        os.symlink(secret, link)
        manifest = self.valid_manifest()
        manifest["references"][0]["path"] = "Managed/link.dll"
        manifest["references"][0]["sha256"] = sha256(secret.read_bytes())
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("symlink" in error for error in payload["errors"]))

    def test_linux_proton_requires_proton_version(self):
        manifest = self.valid_manifest()
        del manifest["protonVersion"]
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("protonVersion" in error for error in payload["errors"]))

    def test_cloud_and_daily_save_boundary_is_rejected(self):
        manifest = self.valid_manifest()
        manifest["testSave"]["cloudSyncDisabled"] = False
        manifest["testSave"]["dailySaveUsed"] = True
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("cloudSyncDisabled" in error for error in payload["errors"]))
        self.assertTrue(any("dailySaveUsed" in error for error in payload["errors"]))

    def test_input_root_must_match_allowed_root(self):
        other = self.base / "other-inputs"
        other.mkdir()
        manifest = self.valid_manifest()
        manifest["inputRoot"] = str(other)
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("exactly to --allowed-root" in error for error in payload["errors"]))

    def test_duplicate_reference_path_is_rejected(self):
        manifest = self.valid_manifest()
        manifest["references"].append(dict(manifest["references"][0]))
        self.write_json(self.manifest_path, manifest)
        code, payload = self.check_inputs()
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("duplicated" in error for error in payload["errors"]))

    def test_valid_game_evidence_is_game_pass(self):
        record_path = self.write_record(self.valid_record())
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 0, payload)
        self.assertTrue(payload["gamePass"])
        self.assertEqual(payload["status"], "PASS")

    def test_game_evidence_missing_actual_is_rejected(self):
        record = self.valid_record()
        del record["actual"]
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertFalse(payload["gamePass"])

    def test_game_evidence_expected_copied_from_actual_is_rejected(self):
        record = self.valid_record()
        record["expectedSource"] = record["actual"]
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("copy of actual" in error for error in payload["errors"]))

    def test_game_evidence_missing_candidate_digest_is_rejected(self):
        record = self.valid_record()
        del record["candidatePackageSha256"]
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertFalse(payload["gamePass"])

    def test_uncovered_record_is_valid_but_not_game_pass(self):
        record = {
            "schemaVersion": 1,
            "scenarioId": "vanilla-smoke",
            "layer": "game-scenario",
            "status": "UNCOVERED",
            "notes": "No real game session was executed.",
        }
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 0, payload)
        self.assertFalse(payload["gamePass"])

    def test_pure_core_record_is_not_game_pass(self):
        record = {
            "schemaVersion": 1,
            "layer": "pure-core",
            "status": "PASS",
            "ciRun": "https://github.com/sagitrs/ps-mod-kourindou/actions/runs/1",
        }
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 0, payload)
        self.assertFalse(payload["gamePass"])

    def test_unknown_scenario_is_rejected(self):
        record = {
            "schemaVersion": 1,
            "scenarioId": "unknown-scenario",
            "layer": "game-scenario",
            "status": "UNCOVERED",
        }
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("scenarioId" in error for error in payload["errors"]))

    def test_evidence_manifest_mismatch_is_rejected(self):
        record = self.valid_record()
        record["gameBuildId"] = "9999999999"
        record_path = self.write_record(record)
        code, payload = self.run_cli(
            "check-evidence",
            "--allowed-root",
            str(self.root),
            "--manifest",
            str(self.manifest_path),
            str(record_path),
        )
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("does not match the manifest" in error for error in payload["errors"]))

    def test_expected_source_needs_provenance(self):
        record = self.valid_record()
        record["expectedSource"] = "the actual result is the expectation"
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("expectedSource" in error for error in payload["errors"]))

    def test_evidence_path_escape_is_rejected(self):
        record = self.valid_record()
        record["evidencePaths"] = ["../outside.txt"]
        record_path = self.write_record(record)
        code, payload = self.run_cli("check-evidence", "--allowed-root", str(self.root), str(record_path))
        self.assertEqual(code, 1, payload)
        self.assertTrue(any("inside the input root" in error for error in payload["errors"]))

    def test_scenario_contract_has_required_ids_and_undecided_boundaries(self):
        scenarios = json.loads(SCENARIOS.read_text(encoding="utf-8"))
        by_id = {item["id"]: item for item in scenarios["scenarios"]}
        self.assertEqual(set(by_id), {"vanilla-smoke", "single-character-native-flow"})
        self.assertTrue(by_id["vanilla-smoke"]["steps"])
        flow = by_id["single-character-native-flow"]
        self.assertTrue(flow["steps"])
        joined = " ".join(flow["notAsserted"])
        for decision in ("D02", "D03", "D06", "D08"):
            self.assertIn(decision, joined)


if __name__ == "__main__":
    unittest.main()

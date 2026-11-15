#!/usr/bin/env python3
"""Validate operator-provided game-test inputs and evidence records.

This tool is a preflight guard, not a game test. Synthetic fixtures prove the
tool's checks only; they do not prove game assemblies, Mod compatibility, or
product acceptance.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath

SCHEMA_VERSION = 1
SHA256_RE = re.compile(r"^[0-9a-fA-F]{64}$")
GIT_SHA_RE = re.compile(r"^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$")
PLATFORMS = {"windows", "linux-proton"}
LAYERS = {"pure-core", "preflight-tool", "real-integration", "game-scenario"}
STATUSES = {"PASS", "PRODUCT_FAIL", "ENVIRONMENT_INVALID", "UNCOVERED"}
CLOUD_VALUES = {"disabled", "off", "none"}
ISOLATION_VALUES = {
    "private-test-prefix",
    "isolated",
    "isolated-test-prefix",
    "disposable-test-prefix",
}
ISSUE_PR_RE = re.compile(r"^https://github\.com/sagitrs/ps-mod-kourindou/(?:issues|pull)/\d+$")
REPO_ROOT = Path(__file__).resolve().parents[2]


class PreflightError(Exception):
    """Input or path error that makes the check environment invalid."""


def _read_json(path: Path) -> object:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise PreflightError(f"input file missing: {path}") from exc
    except json.JSONDecodeError as exc:
        raise PreflightError(f"invalid JSON in {path}: {exc}") from exc
    except OSError as exc:
        raise PreflightError(f"cannot read {path}: {exc}") from exc


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        raise PreflightError(f"cannot read {path}: {exc}") from exc
    return digest.hexdigest()


def _safe_root(value: object, label: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise PreflightError(f"{label} must be a non-empty absolute path")
    path = Path(value)
    if not path.is_absolute():
        raise PreflightError(f"{label} must be absolute: {value}")
    if path.is_symlink():
        raise PreflightError(f"{label} must not be a symlink: {value}")
    try:
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise PreflightError(f"{label} cannot be resolved: {exc}") from exc
    if not resolved.is_dir():
        raise PreflightError(f"{label} must be a directory: {value}")
    return resolved


def _safe_join(root: Path, relative: object, label: str, expect: str) -> Path:
    if not isinstance(relative, str) or not relative:
        raise PreflightError(f"{label} must be a non-empty relative path")
    if "\\" in relative:
        raise PreflightError(f"{label} must use POSIX separators: {relative}")
    rel = PurePosixPath(relative)
    if rel.is_absolute() or any(part in ("", ".", "..") for part in rel.parts):
        raise PreflightError(f"{label} must stay inside the input root: {relative}")

    target = root.joinpath(*rel.parts)
    current = root
    for part in rel.parts:
        current = current / part
        if current.is_symlink():
            raise PreflightError(f"{label} must not traverse a symlink: {relative}")

    try:
        resolved = target.resolve(strict=True)
    except OSError as exc:
        raise PreflightError(f"{label} does not exist: {relative}") from exc
    if root not in resolved.parents:
        raise PreflightError(f"{label} escapes the input root: {relative}")
    if expect == "file" and not resolved.is_file():
        raise PreflightError(f"{label} must be a file: {relative}")
    if expect == "dir" and not resolved.is_dir():
        raise PreflightError(f"{label} must be a directory: {relative}")
    return resolved


def _check(name: str, condition: bool, message: str, errors: list[str], checks: list[dict]) -> bool:
    checks.append({"name": name, "ok": bool(condition)})
    if not condition:
        errors.append(message)
    return bool(condition)


def _string_field(value: object, label: str, errors: list[str]) -> str | None:
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{label} must be a non-empty string")
        return None
    return value.strip()


def _build_id(value: object) -> str | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str) and value.strip():
        return value.strip()
    return None


def _result(errors: list[str], checks: list[dict], status: str | None = None) -> dict:
    return {
        "tool": "preflight",
        "schemaVersion": SCHEMA_VERSION,
        "layer": "preflight-tool",
        "status": status or ("PASS" if not errors else "ENVIRONMENT_INVALID"),
        "gameExecution": "UNCOVERED",
        "errors": errors,
        "checks": checks,
    }


def check_inputs(allowed_root_arg: str, manifest_path: Path) -> dict:
    errors: list[str] = []
    checks: list[dict] = []
    manifest = _read_json(manifest_path)
    if not isinstance(manifest, dict):
        raise PreflightError("manifest root must be an object")

    _check(
        "manifest-schema",
        manifest.get("schemaVersion") == SCHEMA_VERSION,
        f"manifest schemaVersion must be {SCHEMA_VERSION}",
        errors,
        checks,
    )

    try:
        allowed_root = _safe_root(allowed_root_arg, "--allowed-root")
    except PreflightError as exc:
        errors.append(str(exc))
        checks.append({"name": "allowed-root", "ok": False})
        return _result(errors, checks)

    checks.append({"name": "allowed-root", "ok": True})

    input_root_value = manifest.get("inputRoot")
    try:
        input_root = _safe_root(input_root_value, "inputRoot")
    except PreflightError as exc:
        errors.append(str(exc))
        checks.append({"name": "input-root", "ok": False})
        return _result(errors, checks)

    if _check(
        "input-root-matches-allowed-root",
        input_root == allowed_root,
        "inputRoot must resolve exactly to --allowed-root",
        errors,
        checks,
    ) is False:
        return _result(errors, checks)

    source_sha = manifest.get("sourceSha")
    _check(
        "source-sha",
        isinstance(source_sha, str) and bool(GIT_SHA_RE.fullmatch(source_sha)),
        "sourceSha must be a 40- or 64-character hexadecimal Git SHA",
        errors,
        checks,
    )

    game_build_id = _build_id(manifest.get("gameBuildId"))
    expected_game_build_id = _build_id(manifest.get("expectedGameBuildId"))
    _check("game-build-id", game_build_id is not None, "gameBuildId must be a non-empty string or integer", errors, checks)
    _check(
        "expected-game-build-id",
        expected_game_build_id is not None,
        "expectedGameBuildId must be a non-empty string or integer",
        errors,
        checks,
    )
    if game_build_id is not None and expected_game_build_id is not None:
        _check(
            "game-build-id-match",
            game_build_id == expected_game_build_id,
            f"gameBuildId {game_build_id!r} does not match expectedGameBuildId {expected_game_build_id!r}",
            errors,
            checks,
        )

    platform = manifest.get("platform")
    platform_ok = _check(
        "platform",
        isinstance(platform, str) and platform in PLATFORMS,
        f"platform must be one of {sorted(PLATFORMS)}",
        errors,
        checks,
    )
    if platform_ok and platform == "linux-proton":
        _check(
            "proton-version",
            isinstance(manifest.get("protonVersion"), str) and bool(manifest["protonVersion"].strip()),
            "protonVersion is required for platform linux-proton",
            errors,
            checks,
        )

    reference_root_value = manifest.get("referenceRoot")
    try:
        reference_root = _safe_join(input_root, reference_root_value, "referenceRoot", "dir")
        checks.append({"name": "reference-root", "ok": True})
    except PreflightError as exc:
        errors.append(str(exc))
        checks.append({"name": "reference-root", "ok": False})
        reference_root = None

    references = manifest.get("references")
    if not isinstance(references, list) or not references:
        errors.append("references must be a non-empty list")
        checks.append({"name": "references", "ok": False})
    elif reference_root is not None:
        seen_paths: set[Path] = set()
        for index, reference in enumerate(references):
            if not isinstance(reference, dict):
                errors.append(f"references[{index}] must be an object")
                checks.append({"name": f"references[{index}]", "ok": False})
                continue
            relative = reference.get("path")
            expected = reference.get("sha256")
            try:
                target = _safe_join(reference_root, relative, f"references[{index}].path", "file")
            except PreflightError as exc:
                errors.append(str(exc))
                checks.append({"name": f"references[{index}].path", "ok": False})
                continue
            if target in seen_paths:
                errors.append(f"references[{index}].path is duplicated: {relative}")
                checks.append({"name": f"references[{index}].path", "ok": False})
                continue
            seen_paths.add(target)
            if not isinstance(expected, str) or not SHA256_RE.fullmatch(expected):
                errors.append(f"references[{index}].sha256 must be a 64-character hex digest")
                checks.append({"name": f"references[{index}].sha256", "ok": False})
                continue
            actual = _sha256_file(target)
            _check(
                f"references[{index}].digest",
                actual.lower() == expected.lower(),
                f"references[{index}] digest mismatch: expected {expected.lower()}, actual {actual}",
                errors,
                checks,
            )

    candidate = manifest.get("candidatePackage")
    if not isinstance(candidate, dict):
        errors.append("candidatePackage must be an object")
        checks.append({"name": "candidate-package", "ok": False})
    else:
        try:
            candidate_path = _safe_join(input_root, candidate.get("path"), "candidatePackage.path", "file")
            checks.append({"name": "candidate-package-path", "ok": True})
            expected = candidate.get("sha256")
            if not isinstance(expected, str) or not SHA256_RE.fullmatch(expected):
                errors.append("candidatePackage.sha256 must be a 64-character hex digest")
                checks.append({"name": "candidate-package-sha256", "ok": False})
            else:
                actual = _sha256_file(candidate_path)
                _check(
                    "candidate-package-digest",
                    actual.lower() == expected.lower(),
                    f"candidatePackage digest mismatch: expected {expected.lower()}, actual {actual}",
                    errors,
                    checks,
                )
        except PreflightError as exc:
            errors.append(str(exc))
            checks.append({"name": "candidate-package-path", "ok": False})

    test_save = manifest.get("testSave")
    if not isinstance(test_save, dict):
        errors.append("testSave must be an object")
        checks.append({"name": "test-save", "ok": False})
    else:
        try:
            _safe_join(input_root, test_save.get("root"), "testSave.root", "dir")
            checks.append({"name": "test-save-root", "ok": True})
        except PreflightError as exc:
            errors.append(str(exc))
            checks.append({"name": "test-save-root", "ok": False})
        _check("test-save-isolated-copy", test_save.get("isolatedCopy") is True, "testSave.isolatedCopy must be true", errors, checks)
        _check(
            "test-save-cloud-disabled",
            test_save.get("cloudSyncDisabled") is True,
            "testSave.cloudSyncDisabled must be true",
            errors,
            checks,
        )
        _check(
            "test-save-not-daily",
            test_save.get("dailySaveUsed") is False,
            "testSave.dailySaveUsed must be false",
            errors,
            checks,
        )

    return _result(errors, checks)


def _record_string(record: dict, key: str, errors: list[str]) -> str | None:
    return _string_field(record.get(key), key, errors)


def _record_sha(record: dict, key: str, errors: list[str]) -> str | None:
    value = record.get(key)
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        errors.append(f"{key} must be a 64-character hex digest")
        return None
    return value.lower()


def _expected_source_error(value: object, scenario_id: object) -> str | None:
    if not isinstance(value, str) or not value.strip():
        return "expectedSource must be a non-empty string"
    if value.startswith("tools/preflight/scenarios.json#"):
        fragment = value.split("#", 1)[1]
        if fragment != scenario_id:
            return "expectedSource scenario fragment must equal scenarioId"
        return None
    if value.startswith("docs/"):
        relative = value.split("#", 1)[0]
        rel = PurePosixPath(relative)
        if rel.is_absolute() or any(part in ("", ".", "..") for part in rel.parts):
            return "expectedSource docs path must stay inside the repository"
        if not (REPO_ROOT / relative).is_file():
            return f"expectedSource docs path does not exist: {relative}"
        return None
    if ISSUE_PR_RE.fullmatch(value):
        return None
    return "expectedSource must reference the scenario contract, a docs file, or an issue/PR with a number"


def check_evidence(allowed_root_arg: str, record_path: Path, manifest_path: Path | None) -> dict:
    errors: list[str] = []
    checks: list[dict] = []
    record = _read_json(record_path)
    if not isinstance(record, dict):
        raise PreflightError("evidence record root must be an object")

    _check(
        "record-schema",
        record.get("schemaVersion") == SCHEMA_VERSION,
        f"record schemaVersion must be {SCHEMA_VERSION}",
        errors,
        checks,
    )
    layer = record.get("layer")
    status = record.get("status")
    _check("layer", isinstance(layer, str) and layer in LAYERS, f"layer must be one of {sorted(LAYERS)}", errors, checks)
    _check("status", isinstance(status, str) and status in STATUSES, f"status must be one of {sorted(STATUSES)}", errors, checks)

    scenarios_path = Path(__file__).with_name("scenarios.json")
    scenarios = _read_json(scenarios_path)
    scenarios_by_id = {
        item["id"]: item
        for item in scenarios.get("scenarios", [])
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    }
    scenario = None
    scenario_id = None
    if layer in ("real-integration", "game-scenario"):
        scenario_id = record.get("scenarioId")
        if _check(
            "scenario-id",
            isinstance(scenario_id, str) and scenario_id in scenarios_by_id,
            f"scenarioId must be one of {sorted(scenarios_by_id)}",
            errors,
            checks,
        ):
            scenario = scenarios_by_id[scenario_id]

    try:
        allowed_root = _safe_root(allowed_root_arg, "--allowed-root")
        checks.append({"name": "allowed-root", "ok": True})
    except PreflightError as exc:
        errors.append(str(exc))
        checks.append({"name": "allowed-root", "ok": False})
        return _evidence_result(record, errors, checks)

    requires_real = layer in ("real-integration", "game-scenario") and status == "PASS"

    if requires_real:
        source_sha = record.get("sourceSha")
        _check(
            "source-sha",
            isinstance(source_sha, str) and bool(GIT_SHA_RE.fullmatch(source_sha)),
            "sourceSha must be a 40- or 64-character hexadecimal Git SHA",
            errors,
            checks,
        )
        candidate_sha = _record_sha(record, "candidatePackageSha256", errors)
        game_build_id = _build_id(record.get("gameBuildId"))
        _check("game-build-id", game_build_id is not None, "gameBuildId must be a non-empty string or integer", errors, checks)
        platform = record.get("platform")
        _check("platform", isinstance(platform, str) and platform in PLATFORMS, f"platform must be one of {sorted(PLATFORMS)}", errors, checks)
        if platform == "linux-proton":
            _check(
                "proton-version",
                isinstance(record.get("protonVersion"), str) and bool(record["protonVersion"].strip()),
                "protonVersion is required for platform linux-proton",
                errors,
                checks,
            )

        fixture = record.get("fixture")
        if not isinstance(fixture, dict):
            errors.append("fixture must be an object")
            checks.append({"name": "fixture", "ok": False})
        else:
            _string_field(fixture.get("id"), "fixture.id", errors)
            fixture_digest = fixture.get("digest")
            _check(
                "fixture-digest",
                isinstance(fixture_digest, str) and bool(SHA256_RE.fullmatch(fixture_digest)),
                "fixture.digest must be a 64-character hex digest",
                errors,
                checks,
            )
            isolation = fixture.get("isolation")
            _check(
                "fixture-isolation",
                isinstance(isolation, str) and isolation in ISOLATION_VALUES,
                f"fixture.isolation must be one of {sorted(ISOLATION_VALUES)}",
                errors,
                checks,
            )
            cloud = fixture.get("cloud")
            _check(
                "fixture-cloud",
                isinstance(cloud, str) and cloud in CLOUD_VALUES,
                f"fixture.cloud must be one of {sorted(CLOUD_VALUES)}",
                errors,
                checks,
            )

        expected_source = _record_string(record, "expectedSource", errors)
        actual = _record_string(record, "actual", errors)
        source_error = _expected_source_error(expected_source, scenario_id)
        _check(
            "expected-source-provenance",
            source_error is None,
            source_error or "expectedSource must reference the scenario contract, docs, or an issue/PR",
            errors,
            checks,
        )
        _check(
            "expected-source-not-actual",
            isinstance(expected_source, str) and isinstance(actual, str) and expected_source != actual,
            "expectedSource must not be a copy of actual",
            errors,
            checks,
        )

        steps = record.get("steps")
        steps_ok = (
            isinstance(steps, list)
            and bool(steps)
            and all(isinstance(step, str) and step.strip() for step in steps)
        )
        _check("steps", steps_ok, "steps must be a non-empty list of non-empty strings", errors, checks)
        if steps_ok and scenario is not None:
            missing_steps = [step for step in scenario.get("steps", []) if step not in steps]
            _check(
                "scenario-steps",
                not missing_steps,
                f"steps missing contract entries: {missing_steps}",
                errors,
                checks,
            )

        evidence = record.get("evidence")
        if not isinstance(evidence, dict) or not evidence:
            errors.append("evidence must be a non-empty object mapping required evidence labels to files")
            checks.append({"name": "evidence", "ok": False})
        else:
            for label, entry in evidence.items():
                if not isinstance(label, str) or not label.strip():
                    errors.append("evidence keys must be non-empty strings")
                    checks.append({"name": "evidence-key", "ok": False})
                    continue
                if not isinstance(entry, dict):
                    errors.append(f"evidence[{label}] must be an object with path and sha256")
                    checks.append({"name": f"evidence[{label}]", "ok": False})
                    continue
                try:
                    target = _safe_join(allowed_root, entry.get("path"), f"evidence[{label}].path", "file")
                    checks.append({"name": f"evidence[{label}].path", "ok": True})
                except PreflightError as exc:
                    errors.append(str(exc))
                    checks.append({"name": f"evidence[{label}].path", "ok": False})
                    continue
                expected = entry.get("sha256")
                if not isinstance(expected, str) or not SHA256_RE.fullmatch(expected):
                    errors.append(f"evidence[{label}].sha256 must be a 64-character hex digest")
                    checks.append({"name": f"evidence[{label}].sha256", "ok": False})
                    continue
                actual = _sha256_file(target)
                _check(
                    f"evidence[{label}].digest",
                    actual.lower() == expected.lower(),
                    f"evidence[{label}] digest mismatch: expected {expected.lower()}, actual {actual}",
                    errors,
                    checks,
                )

            if scenario is not None:
                missing_evidence = [
                    label
                    for label in scenario.get("requiredEvidence", [])
                    if label not in evidence
                ]
                _check(
                    "required-evidence",
                    not missing_evidence,
                    f"evidence missing required entries: {missing_evidence}",
                    errors,
                    checks,
                )

        _record_string(record, "executor", errors)

        if manifest_path is not None:
            manifest = _read_json(manifest_path)
            if not isinstance(manifest, dict):
                errors.append("manifest root must be an object")
            else:
                manifest_source = manifest.get("sourceSha")
                _check(
                    "manifest-source-sha",
                    isinstance(manifest_source, str) and isinstance(source_sha, str) and manifest_source.lower() == source_sha.lower(),
                    "sourceSha does not match the manifest",
                    errors,
                    checks,
                )
                manifest_candidate = manifest.get("candidatePackage")
                manifest_candidate_sha = manifest_candidate.get("sha256") if isinstance(manifest_candidate, dict) else None
                _check(
                    "manifest-candidate-sha",
                    isinstance(manifest_candidate_sha, str) and isinstance(candidate_sha, str)
                    and manifest_candidate_sha.lower() == candidate_sha.lower(),
                    "candidatePackageSha256 does not match the manifest",
                    errors,
                    checks,
                )
                manifest_build = _build_id(manifest.get("gameBuildId"))
                _check(
                    "manifest-game-build-id",
                    manifest_build is not None and game_build_id == manifest_build,
                    "gameBuildId does not match the manifest",
                    errors,
                    checks,
                )
                _check(
                    "manifest-platform",
                    manifest.get("platform") == platform,
                    "platform does not match the manifest",
                    errors,
                    checks,
                )
                if platform == "linux-proton":
                    _check(
                        "manifest-proton-version",
                        manifest.get("protonVersion") == record.get("protonVersion"),
                        "protonVersion does not match the manifest",
                        errors,
                        checks,
                    )
    elif layer == "pure-core" and status == "PASS":
        _record_string(record, "ciRun", errors)
    elif layer == "preflight-tool" and status == "PASS":
        _record_string(record, "command", errors)

    return _evidence_result(record, errors, checks)


def _evidence_result(record: dict, errors: list[str], checks: list[dict]) -> dict:
    layer = record.get("layer") if isinstance(record.get("layer"), str) else None
    status = record.get("status") if isinstance(record.get("status"), str) else None
    game_pass = layer == "game-scenario" and status == "PASS" and not errors
    return {
        "tool": "preflight",
        "schemaVersion": SCHEMA_VERSION,
        "layer": layer,
        "status": status if status in STATUSES else "ENVIRONMENT_INVALID",
        "gamePass": game_pass,
        "errors": errors,
        "checks": checks,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate game-test inputs and evidence records.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    inputs = subparsers.add_parser("check-inputs", help="validate an operator-provided input manifest")
    inputs.add_argument("--allowed-root", required=True)
    inputs.add_argument("manifest", type=Path)

    evidence = subparsers.add_parser("check-evidence", help="validate an evidence record")
    evidence.add_argument("--allowed-root", required=True)
    evidence.add_argument("--manifest", type=Path)
    evidence.add_argument("record", type=Path)

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        if args.command == "check-inputs":
            result = check_inputs(args.allowed_root, args.manifest)
        else:
            result = check_evidence(args.allowed_root, args.record, args.manifest)
    except PreflightError as exc:
        result = _result([str(exc)], [], status="ENVIRONMENT_INVALID")
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1

    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not result["errors"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

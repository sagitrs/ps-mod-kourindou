#!/usr/bin/env python3
"""Exercise the pure-core wrapper, TRX guard, and failure propagation."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import unittest

WRAPPER = Path(__file__).with_name("verify-pure-core.sh")
CHECK_TRX = Path(__file__).with_name("check_trx.py")

GREEN_COUNTERS = (
    'total="1" executed="1" passed="1" failed="0" error="0" timeout="0" '
    'aborted="0" inconclusive="0" notExecuted="0" disconnected="0"'
)
ZERO_COUNTERS = (
    'total="0" executed="0" passed="0" failed="0" error="0" timeout="0" '
    'aborted="0" inconclusive="0" notExecuted="0" disconnected="0"'
)
SKIPPED_COUNTERS = (
    'total="1" executed="0" passed="0" failed="0" error="0" timeout="0" '
    'aborted="0" inconclusive="0" notExecuted="1" disconnected="0"'
)
FAILED_COUNTERS = (
    'total="1" executed="1" passed="0" failed="1" error="0" timeout="0" '
    'aborted="0" inconclusive="0" notExecuted="0" disconnected="0"'
)

FAKE_DOTNET = """#!/usr/bin/env bash
set -euo pipefail
step="${1:-unknown}"
if [ "$step" = "--version" ]; then
  echo "10.0.404-fake"
  exit 0
fi
echo "fake-dotnet step=$step"
if [ -n "${FAKE_DOTNET_FAIL_STEP:-}" ] && [ "$step" = "$FAKE_DOTNET_FAIL_STEP" ]; then
  exit "${FAKE_DOTNET_FAIL_CODE:-99}"
fi
if [ "$step" = "test" ]; then
  results_dir=""
  previous=""
  for arg in "$@"; do
    if [ "$previous" = "--results-directory" ]; then results_dir="$arg"; fi
    previous="$arg"
  done
  if [ "${FAKE_DOTNET_SKIP_TRX:-}" = "1" ]; then
    exit 0
  fi
  if [ -z "$results_dir" ]; then
    echo "fake-dotnet: missing --results-directory" >&2
    exit 88
  fi
  mkdir -p "$results_dir"
  mode="${FAKE_DOTNET_TRX_MODE:-green}"
  case "$mode" in
    green) counters='total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" notExecuted="0" disconnected="0"' ;;
    zero) counters='total="0" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" notExecuted="0" disconnected="0"' ;;
    skipped) counters='total="1" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" notExecuted="1" disconnected="0"' ;;
    failed) counters='total="1" executed="1" passed="0" failed="1" error="0" timeout="0" aborted="0" inconclusive="0" notExecuted="0" disconnected="0"' ;;
    *) echo "fake-dotnet: bad TRX mode $mode" >&2; exit 89 ;;
  esac
  printf '%s\\n' \\
    '<?xml version="1.0" encoding="utf-8"?>' \\
    '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">' \\
    '  <ResultSummary>' \\
    "    <Counters $counters />" \\
    '  </ResultSummary>' \\
    '</TestRun>' > "$results_dir/pure-core.trx"
fi
exit 0
"""


def write_trx(path, counters):
    path.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">\n'
        '  <ResultSummary>\n'
        f'    <Counters {counters} />\n'
        '  </ResultSummary>\n'
        '</TestRun>\n',
        encoding="utf-8",
    )


class PureCoreVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="kourindou-ci-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.fake = self.root / "dotnet"
        self.fake.write_text(FAKE_DOTNET, encoding="utf-8")
        self.fake.chmod(0o755)
        self.env = os.environ.copy()
        self.env["DOTNET"] = str(self.fake)
        self.env.pop("FAKE_DOTNET_FAIL_STEP", None)
        self.env.pop("FAKE_DOTNET_FAIL_CODE", None)
        self.env.pop("FAKE_DOTNET_SKIP_TRX", None)
        self.env.pop("FAKE_DOTNET_TRX_MODE", None)

    def run_wrapper(self, *args, fail_step=None, fail_code=99, skip_trx=False, trx_mode="green"):
        env = self.env.copy()
        if fail_step is not None:
            env["FAKE_DOTNET_FAIL_STEP"] = fail_step
            env["FAKE_DOTNET_FAIL_CODE"] = str(fail_code)
        if skip_trx:
            env["FAKE_DOTNET_SKIP_TRX"] = "1"
        env["FAKE_DOTNET_TRX_MODE"] = trx_mode
        return subprocess.run(
            [str(WRAPPER), *args],
            cwd=self.root,
            env=env,
            capture_output=True,
            text=True,
            timeout=60,
        )

    def run_check_trx(self, path):
        return subprocess.run(
            [sys.executable, str(CHECK_TRX), str(path)],
            cwd=self.root,
            capture_output=True,
            text=True,
            timeout=60,
        )

    def test_success_path_returns_zero(self):
        result = self.run_wrapper("--skip-restore")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("pure-core verification passed", result.stdout)
        self.assertIn("TRX report proves a complete green run", result.stdout)

    def test_restore_failure_propagates(self):
        result = self.run_wrapper(fail_step="restore", fail_code=25)
        self.assertEqual(result.returncode, 25, result.stdout + result.stderr)
        self.assertIn("restore: failed (exit 25)", result.stderr)

    def test_build_failure_propagates(self):
        result = self.run_wrapper("--skip-restore", fail_step="build", fail_code=23)
        self.assertEqual(result.returncode, 23, result.stdout + result.stderr)
        self.assertIn("build: failed (exit 23)", result.stderr)

    def test_test_failure_propagates(self):
        result = self.run_wrapper("--skip-restore", fail_step="test", fail_code=24)
        self.assertEqual(result.returncode, 24, result.stdout + result.stderr)
        self.assertIn("test: failed (exit 24)", result.stderr)

    def test_missing_dotnet_fails_clearly(self):
        env = self.env.copy()
        env["DOTNET"] = str(self.root / "missing-dotnet")
        result = subprocess.run(
            [str(WRAPPER), "--skip-restore"],
            cwd=self.root,
            env=env,
            capture_output=True,
            text=True,
            timeout=60,
        )
        self.assertEqual(result.returncode, 127, result.stdout + result.stderr)
        self.assertIn("dotnet executable not found", result.stderr)

    def test_unknown_argument_returns_usage_error(self):
        result = self.run_wrapper("--unknown")
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("unknown argument", result.stderr)

    def test_help_returns_zero(self):
        result = self.run_wrapper("--help")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Usage:", result.stdout)

    def test_missing_trx_report_is_not_green(self):
        result = self.run_wrapper("--skip-restore", skip_trx=True)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("no TRX report found", result.stderr)
        self.assertIn("test-report: failed (exit 1)", result.stderr)

    def test_zero_test_report_is_not_green(self):
        result = self.run_wrapper("--skip-restore", trx_mode="zero")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("total<=0", result.stderr)

    def test_all_skipped_report_is_not_green(self):
        result = self.run_wrapper("--skip-restore", trx_mode="skipped")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("notExecuted!=0", result.stderr)

    def test_failed_report_is_not_green(self):
        result = self.run_wrapper("--skip-restore", trx_mode="failed")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("failed!=0", result.stderr)

    def test_check_trx_accepts_complete_green_report(self):
        report = self.root / "green.trx"
        write_trx(report, GREEN_COUNTERS)
        result = self.run_check_trx(report)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("TRX report proves a complete green run", result.stdout)

    def test_check_trx_rejects_missing_file(self):
        result = self.run_check_trx(self.root / "missing.trx")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("no TRX report found", result.stderr)

    def test_check_trx_rejects_malformed_xml(self):
        report = self.root / "malformed.trx"
        report.write_text("not xml", encoding="utf-8")
        result = self.run_check_trx(report)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("error:", result.stderr)


if __name__ == "__main__":
    unittest.main()

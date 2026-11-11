#!/usr/bin/env python3
"""Exercise the pure-core verification wrapper, including failure propagation."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest

WRAPPER = Path(__file__).with_name("verify-pure-core.sh")

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
exit 0
"""


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

    def run_wrapper(self, *args, fail_step=None, fail_code=99):
        env = self.env.copy()
        if fail_step is not None:
            env["FAKE_DOTNET_FAIL_STEP"] = fail_step
            env["FAKE_DOTNET_FAIL_CODE"] = str(fail_code)
        return subprocess.run(
            [str(WRAPPER), *args],
            cwd=self.root,
            env=env,
            capture_output=True,
            text=True,
            timeout=60,
        )

    def test_success_path_returns_zero(self):
        result = self.run_wrapper("--skip-restore")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("pure-core verification passed", result.stdout)

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


if __name__ == "__main__":
    unittest.main()

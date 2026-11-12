#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: tools/ci/verify-pure-core.sh [--configuration <Debug|Release>] [--skip-restore]

Runs only the game-independent core build and tests. This script contains no
game assembly references and does not claim Mod loading or in-game acceptance.
It runs tests with a fresh TRX report in a new temporary directory, then checks
that at least one test actually executed and that the run is completely green.
It exits non-zero if any step fails, preserving the failing step's exit code.
EOF
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet_bin="${DOTNET:-dotnet}"
configuration="Release"
skip_restore="false"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --configuration)
      if [ "$#" -lt 2 ]; then
        echo "error: --configuration requires a value" >&2
        exit 2
      fi
      configuration="$2"
      shift 2
      ;;
    --skip-restore)
      skip_restore="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

cd "$repo_root"

if ! command -v "$dotnet_bin" >/dev/null 2>&1; then
  echo "error: dotnet executable not found: $dotnet_bin" >&2
  echo "hint: set DOTNET to the pinned SDK path or add it to PATH" >&2
  exit 127
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "error: python3 is required to validate the TRX test report" >&2
  exit 127
fi

dotnet_version="$("$dotnet_bin" --version)"
echo "dotnet: $dotnet_version"
echo "configuration: $configuration"

run_step() {
  local name="$1"
  shift
  echo "--- $name ---"
  if "$@"; then
    echo "--- $name: ok ---"
    return 0
  else
    local status=$?
    echo "--- $name: failed (exit $status) ---" >&2
    exit "$status"
  fi
}

if [ "$skip_restore" != "true" ]; then
  run_step "restore" "$dotnet_bin" restore Kourindou.sln --nologo
fi

run_step "build" "$dotnet_bin" build Kourindou.sln --no-restore --nologo --configuration "$configuration" -m:2

report_dir="$(mktemp -d "${TMPDIR:-/tmp}/kourindou-pure-core-trx.XXXXXX")"
trap 'rm -rf "$report_dir"' EXIT
echo "report dir: $report_dir"

run_step "test" "$dotnet_bin" test Kourindou.sln --no-build --no-restore --nologo --configuration "$configuration" -m:2 --logger "trx;LogFileName=pure-core.trx" --results-directory "$report_dir"
run_step "test-report" python3 tools/ci/check_trx.py "$report_dir"

echo "pure-core verification passed"

#!/bin/bash
# Verify the Unity acceptance tooling itself, without a Unity Editor.
#
# This is the gate that must pass BEFORE Run-UnityGates.ps1 is trusted on Windows:
#   1. syntax     - every acceptance script parses (PowerShell grammar; PowerShell's own parser too
#                   when the host can load it, reported honestly when it cannot)
#   2. reporting  - the pass/fail rules are executed: exit codes, NUnit validation, required
#                   encounter cases, skipped visibility, no manual-playthrough claim, spaced paths
#
# What this proves: the runner's syntax is valid and its reporting cannot pass a failed run.
# What it does NOT prove: that PowerShell executes it as intended, that Unity runs, or that the
# game works. Those need the Windows host (Docs: Documentation/UnityValidation/README.md).
#
# usage: Tools/UnityValidation/verify.sh
# exit:  0 = tooling verified, 1 = a check failed, 2 = a prerequisite is missing
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 2
ROOT=$PWD
PY=$ROOT/.venv/bin/python

if [ ! -x "$PY" ]; then
  echo "missing .venv; run: python3 Tools/OfflineCompile/setup_toolchain.py" >&2
  exit 2
fi

echo "=== 1/3 PowerShell syntax (PowerShell grammar) ==="
"$PY" Tools/UnityValidation/check_powershell_syntax.py
SYNTAX=$?
echo
if [ $SYNTAX -ne 0 ]; then
  echo "ACCEPTANCE TOOLING FAILED - syntax check exited $SYNTAX" >&2
  exit 1
fi

echo "=== 2/3 Authoritative parser (PowerShell's own, when this host can load it) ==="
if [ -f "$ROOT/Tools/.toolchain/pwsh/System.Management.Automation.dll" ]; then
  bash Tools/UnityValidation/check_powershell.sh
  PARSER=$?
  if [ $PARSER -eq 1 ]; then
    echo "ACCEPTANCE TOOLING FAILED - PowerShell's parser reported syntax errors" >&2
    exit 1
  elif [ $PARSER -eq 2 ]; then
    echo "SKIPPED: PowerShell's own parser cannot load on this host (Windows-only assembly)."
    echo "         The Windows run parses the scripts before every gate; run this there for the"
    echo "         authoritative verdict."
  fi
else
  echo "SKIPPED: parser not staged (python3 Tools/UnityValidation/setup_pssyntax.py)"
fi
echo

echo "=== 3/3 Reporting-logic regressions ==="
"$PY" -m unittest Tools.test_powershell_syntax Tools.test_acceptance_reporting
TESTS=$?
echo
if [ $TESTS -ne 0 ]; then
  echo "ACCEPTANCE TOOLING FAILED - reporting regressions exited $TESTS" >&2
  exit 1
fi

echo "ACCEPTANCE TOOLING VERIFIED - scripts parse and the reporting rules hold."
echo "Unity/Android behaviour is NOT verified by this script; run Tools/UnityValidation/Run-UnityGates.ps1 on Windows."

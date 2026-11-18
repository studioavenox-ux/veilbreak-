#!/bin/bash
# Syntax-check the Unity acceptance PowerShell scripts with PowerShell's own parser.
#
# Why this exists: the acceptance runner can only be *executed* on the Windows machine that has
# Unity. This script still validates its syntax here, using
# System.Management.Automation.Language.Parser (the same parser pwsh uses before executing), so a
# broken param block or unclosed brace is caught before the Windows run wastes a build.
#
# What it proves: the scripts parse. What it does NOT prove: that they run, or that PowerShell
# semantics behave as intended. That needs the real host.
#
# usage: Tools/UnityValidation/check_powershell.sh
# exit:  0 = all scripts parsed, 1 = syntax error, 2 = parser unavailable (treated as failure)
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 2
ROOT=$PWD
TOOL=$ROOT/Tools/.toolchain
PS=$TOOL/pwsh
OUT=$TOOL/out/pscheck
HARNESS=$TOOL/out/harness/offlinecompile

if [ ! -x "$HARNESS" ]; then
  echo "toolchain missing; run: python3 Tools/OfflineCompile/setup_toolchain.py" >&2
  exit 2
fi
if [ ! -f "$PS/System.Management.Automation.dll" ]; then
  echo "PowerShell parser not staged; run: python3 Tools/UnityValidation/setup_pssyntax.py" >&2
  exit 2
fi

# Collect every script the acceptance pipeline owns.
SCRIPTS=()
while IFS= read -r file; do SCRIPTS+=("$file"); done < <(find "$ROOT/Tools/UnityValidation" -name '*.ps1' -o -name '*.psm1' | sort)
if [ ${#SCRIPTS[@]} -eq 0 ]; then
  echo "no PowerShell scripts found" >&2
  exit 2
fi
echo "checking ${#SCRIPTS[@]} script(s)"

mkdir -p "$OUT"
export LD_LIBRARY_PATH=$TOOL
OC_REFDIR=$TOOL/bcl OC_OUT=$OUT OC_KIND=exe OC_LANGVER=10.0 OC_QUIET=1 \
  OC_CORELIB=$TOOL/bcl/System.Private.CoreLib.dll \
  "$HARNESS" --sources "$ROOT/Tools/UnityValidation" PowerShellSyntax "DEBUG" "" \
  >"$OUT/build.log" 2>&1
if [ $? -ne 0 ]; then
  echo "syntax checker failed to compile - see $OUT/PowerShellSyntax.errors.txt" >&2
  tail -5 "$OUT/build.log" >&2
  exit 2
fi

# Reuse the patched self-contained .NET 10 host, exactly as the simulation harness does.
APPDIR=$OUT/app
mkdir -p "$APPDIR"
if [ ! -x "$APPDIR/pscheck" ]; then
  python3 - "$APPDIR" "$ROOT" <<'PY'
import json, os, pathlib, sys
appdir, root = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
host = root / 'Tools' / '.toolchain' / 'roslyn-host'
apphost = (host / 'dprint-plugin-roslyn').read_bytes()
(appdir / 'pscheck').write_bytes(apphost.replace(b'dprint-plugin-roslyn.dll', b'PowerShellSyntax.dll\0\0\0\0'))
os.chmod(appdir / 'pscheck', 0o755)
deps = json.loads((host / 'dprint-plugin-roslyn.deps.json').read_text())
key = next(k for k in deps['targets'] if 'linux-x64' in k)
target = {k: v for k, v in deps['targets'][key].items() if k.startswith('runtimepack.')}
target['PowerShellSyntax/1.0.0'] = {'runtime': {'PowerShellSyntax.dll': {}}}
deps['targets'][key] = target
deps['libraries'] = {'PowerShellSyntax/1.0.0': {'type': 'project', 'serviceable': False, 'sha512': ''},
                     **{k: v for k, v in deps['libraries'].items() if k.startswith('runtimepack.')}}
(appdir / 'pscheck.deps.json').write_text(json.dumps(deps, indent=2))
(appdir / 'pscheck.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': 'net10.0',
    'includedFrameworks': [{'name': 'Microsoft.NETCore.App', 'version': '10.0.11'}],
    'configProperties': {'System.Globalization.Invariant': True}}}, indent=2))
for entry in host.iterdir():
    link = appdir / entry.name
    if not link.exists() and entry.name not in ('pscheck', 'PowerShellSyntax.dll'):
        os.symlink(os.path.relpath(entry.resolve(), appdir.resolve()), link)
PY
fi
cp "$OUT/PowerShellSyntax.dll" "$APPDIR/PowerShellSyntax.dll"

echo
(cd "$APPDIR" && LD_LIBRARY_PATH=$TOOL ./pscheck "$PS" "${SCRIPTS[@]}")
exit $?

#!/bin/bash
# Builds and runs the VEILBREAK headless gameplay simulation.
#
# Compiles MiniEngine.cs + the REAL gameplay scripts from Assets/VEILBREAK/Scripts + SimMain.cs
# into one console executable, then runs it. Presentation scripts (scene art, HUD, audio) are
# excluded because they need a real renderer; the gameplay logic under test is compiled as-is.
#
# usage: Tools/HeadlessSim/build_sim.sh [--rebuild]
# exit:  0 = every simulation check passed
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 2
ROOT=$PWD
TOOL=$ROOT/Tools/.toolchain
OUT=$TOOL/out/sim
HARNESS=$TOOL/out/harness/offlinecompile
BIN=$OUT/veilsim
SIM=$ROOT/Tools/HeadlessSim

if [ ! -x "$HARNESS" ]; then
  echo "toolchain missing; run: python3 Tools/OfflineCompile/setup_toolchain.py" >&2
  exit 2
fi

# Every gameplay and UI script is simulated. Only the two art/audio scripts that need a real
# renderer and mesh pipeline are replaced by simulation stand-ins.
GAMEPLAY=""
for file in "$ROOT"/Assets/VEILBREAK/Scripts/*/*.cs; do
  case "$(basename "$file")" in
    PrimitiveFactory.cs|PrototypeSceneArt.cs|EncounterAudio.cs) continue ;;
  esac
  GAMEPLAY="$GAMEPLAY,$file"
done
GAMEPLAY=${GAMEPLAY#,}
echo "simulated scripts: $(echo "$GAMEPLAY" | tr ',' '\n' | wc -l)"

export LD_LIBRARY_PATH=$TOOL OC_LANGVER=10.0
export OC_REFDIR=$TOOL/bcl OC_SKIP="Microsoft.CodeAnalysis.dll,Microsoft.CodeAnalysis.CSharp.dll,Microsoft.CodeAnalysis.VisualBasic.dll,Microsoft.CodeAnalysis.Workspaces.dll,Microsoft.CodeAnalysis.CSharp.Workspaces.dll,Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll,Microsoft.CodeAnalysis.Features.dll,Microsoft.CodeAnalysis.FlowAnalysis.Utilities.dll,humanizer.dll,Newtonsoft.Json.dll,SQLitePCLRaw.core.dll,SQLitePCLRaw.batteries_v2.dll,SQLitePCLRaw.provider.e_sqlite3.dll,Roslyn Library.dll,SymmetricKeyAlgorithmProvider.dll"
export OC_OUT=$OUT OC_QUIET=1 OC_CORELIB=$TOOL/bcl/System.Private.CoreLib.dll OC_KIND=exe OC_PDB=1

OC_EXTRA_SOURCES="$SIM/MiniEngine.cs,$SIM/EngineSelfTest.cs,$GAMEPLAY,$SIM/SimMain.cs" \
"$HARNESS" --sources - veilsim "DEBUG;UNITY_2017_1_OR_NEWER;UNITY_6000_0_OR_NEWER;ENABLE_PHYSICS;ENABLE_LEGACY_INPUT_MANAGER" "" 2>&1 | tail -20; BUILD_RC=${PIPESTATUS[0]}
[ "${BUILD_RC:-1}" -ne 0 ] && {
  echo "SIMULATION BUILD FAILED - see $OUT/veilsim.errors.txt"; exit 1; }

# Reuse the patched self-contained .NET 10 host to launch the emitted assembly.
APPDIR=$OUT/app
mkdir -p "$APPDIR"
if [ ! -x "$APPDIR/veilsim" ]; then
  cp "$ROOT/Tools/.toolchain/roslyn-host/dprint-plugin-roslyn" "$APPDIR/veilsim" 2>/dev/null || true
  python3 - "$APPDIR" "$ROOT" <<'PY'
import json, os, pathlib, shutil, sys
appdir, root = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
host = root / 'Tools' / '.toolchain' / 'roslyn-host'
apphost = (host / 'dprint-plugin-roslyn').read_bytes()
(appdir / 'veilsim').write_bytes(apphost.replace(b'dprint-plugin-roslyn.dll', b'veilsim.dll\0' + b'\0' * 5))
os.chmod(appdir / 'veilsim', 0o755)
deps = json.loads((host / 'dprint-plugin-roslyn.deps.json').read_text())
key = next(k for k in deps['targets'] if 'linux-x64' in k)
target = {k: v for k, v in deps['targets'][key].items() if k.startswith('runtimepack.')}
target['veilsim/1.0.0'] = {'runtime': {'veilsim.dll': {}}}
deps['targets'][key] = target
deps['libraries'] = {'veilsim/1.0.0': {'type': 'project', 'serviceable': False, 'sha512': ''},
                     **{k: v for k, v in deps['libraries'].items() if k.startswith('runtimepack.')}}
(appdir / 'veilsim.deps.json').write_text(json.dumps(deps, indent=2))
(appdir / 'veilsim.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
    'tfm': 'net10.0',
    'includedFrameworks': [{'name': 'Microsoft.NETCore.App', 'version': '10.0.11'}],
    'configProperties': {'System.Globalization.Invariant': True}}}, indent=2))
for entry in host.iterdir():
    link = appdir / entry.name
    if not link.exists() and entry.name not in ('veilsim', 'veilsim.dll'):
        os.symlink(os.path.relpath(entry.resolve(), appdir.resolve()), link)
PY
fi
cp "$OUT/veilsim.dll" "$APPDIR/veilsim.dll"
cp "$OUT/veilsim.pdb" "$APPDIR/veilsim.pdb" 2>/dev/null

echo
(cd "$APPDIR" && LD_LIBRARY_PATH=$TOOL ./veilsim)
exit $?

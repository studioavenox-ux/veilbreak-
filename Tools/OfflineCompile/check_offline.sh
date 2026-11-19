#!/bin/bash
# VEILBREAK offline typecheck.
#
# Stage 1 builds UnityEngine and UnityEngine.UI reference assemblies from Unity's own C# source
# (UnityCsReference + uGUI, branch 6000.3). Stage 2 compiles the runtime, Editor and test
# assemblies against those REAL engine assemblies, plus two signature stubs for surfaces that are
# not redistributable: UnityEditor (closed native editor modules) and NUnit + Unity Test Framework.
#
# What this DOES prove: every scripting API call, signature, override, generic constraint and
# C# language feature used by this project resolves against Unity 6.3 at C# 10.
# What this does NOT prove: Unity import, Play Mode behaviour, rendering, physics or Android device
# behaviour. Those need the Unity Editor and hardware. Never record them as verified from here.
#
# usage: Tools/OfflineCompile/check_offline.sh [runtime|references|all]
# exit:  0 = every checked assembly compiled with zero errors
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 2
ROOT=$PWD
TOOL=$ROOT/Tools/.toolchain
OUT=$TOOL/out
REF=$TOOL/refsrc
HARNESS=$OUT/harness/offlinecompile
PATCHES=$ROOT/Tools/OfflineCompile/patches
STUBS=$ROOT/Tools/OfflineCompile
REFS=$OUT/reference
GAME=$OUT/game

export LD_LIBRARY_PATH=$TOOL

if [ ! -x "$HARNESS" ]; then
  echo "toolchain missing; run: python3 Tools/OfflineCompile/setup_toolchain.py" >&2
  exit 2
fi

REFDIR=$TOOL/bcl
# Assemblies the Roslyn host itself uses must never leak into a game reference set.
SKIP_HOST="Microsoft.CodeAnalysis.dll,Microsoft.CodeAnalysis.CSharp.dll,Microsoft.CodeAnalysis.VisualBasic.dll,Microsoft.CodeAnalysis.Workspaces.dll,Microsoft.CodeAnalysis.CSharp.Workspaces.dll,Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll,Microsoft.CodeAnalysis.Features.dll,Microsoft.CodeAnalysis.FlowAnalysis.Utilities.dll,humanizer.dll,Newtonsoft.Json.dll,SQLitePCLRaw.core.dll,SQLitePCLRaw.batteries_v2.dll,SQLitePCLRaw.provider.e_sqlite3.dll,Roslyn Library.dll,SymmetricKeyAlgorithmProvider.dll"
# Unity 6.x compiles player scripts as C# 10; match it so unsupported syntax fails here too.
export OC_LANGVER=10.0
export OC_REFDIR=$REFDIR
export OC_SKIP=$SKIP_HOST
export OC_OUT=$OUT
export OC_QUIET=1
export OC_CORELIB=$REFDIR/System.Private.CoreLib.dll

ENGINE_DEFS="UNITY_2017_1_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;ENABLE_UNITYEVENTS;ENABLE_PHYSICS;ENABLE_AUDIO;ENABLE_ANIMATION;ENABLE_LEGACY_INPUT_MANAGER"
GAME_DEFS="$ENGINE_DEFS;UNITY_ANDROID;CSHARP_7_3_OR_NEWER"
EDITOR_DEFS="$ENGINE_DEFS;UNITY_EDITOR;UNITY_ANDROID;CSHARP_7_3_OR_NEWER"
TEST_DEFS="$GAME_DEFS;UNITY_INCLUDE_TESTS"

step() { printf '%-24s ' "$1"; }

# --- Stage 1: engine reference assemblies from real Unity source ------------------------------
build_engine() {
  mkdir -p "$REFS"
  step "UnityEngine"
  OC_OUT=$REFS OC_PATCHDIR=$PATCHES "$HARNESS" --project "$REF/UnityCsReference/Projects/CSharp/UnityEngine.csproj" \
    UnityEngine "" "$ENGINE_DEFS;ENABLE_MONO;UNITY_STANDALONE;PLATFORM_SUPPORTS_MONO" | tail -2 || return 1

  # UGUI is compiled for the player: UNITY_EDITOR is deliberately absent, because several
  # components only reference UnityEditor inside `#if UNITY_EDITOR`, which we cannot resolve here.
  step "UnityEngine.UI"
  OC_OUT=$REFS OC_PATCHDIR=$PATCHES "$HARNESS" --sources "$REF/uGUI/com.unity.ugui/Runtime/UGUI" UnityEngine.UI \
    "$ENGINE_DEFS;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_TILEMAP;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT" \
    "$REFS/UnityEngine.dll" | tail -2 || return 1
}

# --- Stage 2: stubs for surfaces Unity does not redistribute ----------------------------------
build_stubs() {
  mkdir -p "$GAME"
  step "UnityEditor stubs"
  OC_OUT=$GAME "$HARNESS" --sources - UnityEditorStubs "$EDITOR_DEFS" "$REFS/UnityEngine.dll" \
    "$STUBS/UnityEditorStubs.cs" | tail -1 || return 1
  step "Test framework stubs"
  OC_OUT=$GAME "$HARNESS" --sources - UnityTestStubs "$ENGINE_DEFS" "$REFS/UnityEngine.dll" \
    "$STUBS/UnityTestStubs.cs" | tail -1 || return 1
}

# --- Stage 3: the project itself --------------------------------------------------------------
build_game() {
  mkdir -p "$GAME"
  step "VEILBREAK.Runtime"
  OC_OUT=$GAME "$HARNESS" --sources "$ROOT/Assets/VEILBREAK/Scripts" VEILBREAK.Runtime "$GAME_DEFS" \
    "$REFS/UnityEngine.dll,$REFS/UnityEngine.UI.dll" | tail -2 || return 1
  step "VEILBREAK.Editor"
  OC_OUT=$GAME "$HARNESS" --sources "$ROOT/Assets/VEILBREAK/Editor" VEILBREAK.Editor "$EDITOR_DEFS" \
    "$REFS/UnityEngine.dll,$GAME/VEILBREAK.Runtime.dll,$GAME/UnityEditorStubs.dll" | tail -2 || return 1
  step "VEILBREAK.Tests"
  OC_OUT=$GAME "$HARNESS" --sources "$ROOT/Assets/VEILBREAK/Tests" VEILBREAK.Tests "$TEST_DEFS" \
    "$REFS/UnityEngine.dll,$REFS/UnityEngine.UI.dll,$GAME/VEILBREAK.Runtime.dll,$GAME/UnityTestStubs.dll,$GAME/UnityEditorStubs.dll" \
    | tail -2 || return 1
}

mode=${1:-all}
case $mode in
  references) build_engine ;;
  runtime) build_stubs && build_game ;;
  all) build_engine && build_stubs && build_game ;;
  *) echo "usage: $0 [runtime|references|all]" >&2; exit 2 ;;
esac || { echo; echo "OFFLINE TYPECHECK FAILED (mode: $mode)"; exit 1; }

echo
echo "OFFLINE TYPECHECK PASSED (mode: $mode)"
echo "Engine: real Unity 6000.3 C# source. Editor/tests: signature stubs."
echo "Unity import, Play Mode tests and Android device behaviour still require the Editor and hardware."

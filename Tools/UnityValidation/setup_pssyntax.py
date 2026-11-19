#!/usr/bin/env python3
"""Stage the PowerShell parsing toolchain used by Tools/UnityValidation/check_powershell.sh.

Two independent syntax checks are staged, because they fail differently:

  1. tree-sitter-powershell (PyPI) - Microsoft's own PowerShell tree-sitter grammar. This is the
     PRIMARY check here: the grammar is real, it runs on this Linux host, and it catches unclosed
     blocks, broken string interpolation and malformed param blocks.
  2. System.Management.Automation.Language.Parser - PowerShell's authoritative parser, from
     @dsh-ops/pwsh-win32-x64 on npm (the only PowerShell distribution reachable from this sandbox
     without GitHub release assets). The assembly is ReadyToRun-compiled for Windows, so it cannot
     execute here; on a Linux/macOS or Windows pwsh host it does, and the checker then uses it.
     WHEN IT IS UNAVAILABLE THE CHECKER REPORTS "SKIPPED", NEVER "PASSED".

Nothing here executes PowerShell, and nothing ships in the APK. Downloads land in Tools/.toolchain
(git-ignored). Reviewed use only.
"""
import pathlib
import shutil
import subprocess
import sys
import tarfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
TOOL = ROOT / 'Tools' / '.toolchain'
PS = TOOL / 'pwsh'
PACKAGE = '@dsh-ops/pwsh-win32-x64@0.2.1'


def stage_tree_sitter():
    """Install the PowerShell tree-sitter grammar into the repo venv (idempotent)."""
    venv = ROOT / '.venv'
    pip = venv / 'bin' / 'pip'
    if not pip.exists():
        print('no .venv; run Tools/OfflineCompile/setup_toolchain.py first')
        return False
    probe = subprocess.run([str(venv / 'bin' / 'python'), '-c',
                            'import tree_sitter_powershell, tree_sitter'],
                           capture_output=True, text=True)
    if probe.returncode == 0:
        print('tree-sitter PowerShell grammar already installed')
        return True
    subprocess.run([str(pip), 'install', '-q', 'tree_sitter', 'tree_sitter_powershell'], check=True)
    print('installed tree-sitter + tree_sitter_powershell')
    return True


def main():
    stage_tree_sitter()
    PS.mkdir(parents=True, exist_ok=True)
    target = PS / 'System.Management.Automation.dll'
    if target.exists() and target.stat().st_size > 0:
        print('already staged:', target)
        return 0

    work = TOOL / 'npmtmp'
    work.mkdir(parents=True, exist_ok=True)
    subprocess.run(['npm', 'pack', PACKAGE, '--silent'], cwd=work, check=True)
    archives = sorted(work.glob('*.tgz'), key=lambda p: p.stat().st_mtime)
    if not archives:
        print('download did not produce an archive', file=sys.stderr)
        return 1
    archive = archives[-1]
    print('downloaded', archive.name, f'{archive.stat().st_size / 1e6:.0f} MB')

    staged = 0
    with tarfile.open(archive) as tar:
        for member in tar.getmembers():
            parts = pathlib.PurePosixPath(member.name).parts
            if member.isdir() or not parts:
                continue
            base = parts[-1]
            if not (base.endswith('.dll') or base.endswith('.json')):
                continue
            if not any('bin' == part.lower() for part in parts[:-1]) and len(parts) > 2:
                continue
            source = tar.extractfile(member)
            if source is None:
                continue
            # Flatten: only the name matters for assembly resolution.
            (PS / base).write_bytes(source.read())
            staged += 1
    shutil.rmtree(work, ignore_errors=True)
    print(f'staged {staged} PowerShell assemblies at {PS}')
    if not target.exists():
        print('System.Management.Automation.dll was not in the package', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())

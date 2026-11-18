#!/usr/bin/env python3
"""Bootstrap the offline C# typecheck harness used by check_offline.sh.

This is DEVELOPER tooling for a source-only sandbox without a Unity Editor. It downloads:
  * a .NET runtime (via the pypi `dotnetcore2` wheel) to host Roslyn,
  * Roslyn (via the npm package @dprint/roslyn-linux-x64-glibc, which also provides a
    self-contained .NET 10 native host used to run the harness itself).
None of it is a game dependency, nothing here ships in the APK, and downloads land in
Tools/.toolchain (git-ignored). Reviewed use only.
"""
import json
import os
import pathlib
import shutil
import subprocess
import sys
import tarfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
TOOL = ROOT / 'Tools' / '.toolchain'
DPRINT = TOOL / 'roslyn-host'
RUNTIME = TOOL / 'runtime'
OUT = TOOL / 'out'


def run(cmd, cwd=None, quiet=True):
    print('+', ' '.join(str(c) for c in cmd)[:200])
    result = subprocess.run(cmd, cwd=cwd, capture_output=quiet, text=True)
    if result.returncode != 0 and quiet:
        print(result.stdout[-4000:]); print(result.stderr[-4000:])
    result.check_returncode()
    return result


def npm_pack(package, target):
    target.mkdir(parents=True, exist_ok=True)
    run(['npm', 'pack', package, '--silent'], cwd=target)
    archive = sorted(target.glob('*.tgz'))[0]
    with tarfile.open(archive) as tar:
        tar.extractall(target)
    return target / 'package'


def main():
    TOOL.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)

    venv = ROOT / '.venv'
    if not (venv / 'bin' / 'python').exists():
        run([sys.executable, '-m', 'venv', str(venv)])
    pip = venv / 'bin' / 'pip'
    venv_python = venv / 'bin' / 'python'
    probe = subprocess.run([str(venv_python), '-c', 'import dotnetcore2, clr_loader, pythonnet'],
                           capture_output=True, text=True)
    if probe.returncode != 0:
        run([str(pip), 'install', '-q', 'dotnetcore2', 'clr-loader', 'pythonnet'])
    runtime_bin = pathlib.Path(run([str(venv_python), '-c',
                                    'import dotnetcore2,os;print(os.path.join(os.path.dirname(dotnetcore2.__file__),"bin"))'],
                                   quiet=True).stdout.strip())
    runtime_fx = runtime_bin / 'shared' / 'Microsoft.NETCore.App' / '3.1.23'
    if not runtime_fx.exists():
        raise SystemExit('dotnetcore2 runtime missing: ' + str(runtime_fx))

    # Self-contained .NET 10 host: native libs + Roslyn 5.9 + System.Collections.Immutable 10.
    if not (DPRINT / 'Microsoft.CodeAnalysis.dll').exists():
        package = npm_pack('@dprint/roslyn-linux-x64-glibc', TOOL / 'npm-roslyn')
        dprint = DPRINT
        dprint.mkdir(parents=True, exist_ok=True)
        for entry in package.iterdir():
            shutil.copy2(entry, dprint / entry.name)
        shutil.rmtree(TOOL / 'npm-roslyn')

    # OSSL 1.1 shim for the .NET Core 3.1 host (modern distros ship OpenSSL 3 only).
    if not (TOOL / 'libcrypto-2ade47cd.so.1.1').exists():
        wheel_dir = TOOL / 'wheel'
        wheel_dir.mkdir(parents=True, exist_ok=True)
        run([str(pip), 'download', '--no-deps', '--only-binary', ':all:', '--python-version', '311',
             '--implementation', 'cp', '--abi', 'cp311', '--platform', 'manylinux2014_x86_64',
             'psycopg2-binary==2.9.5', '-d', str(wheel_dir)])
        wheel = sorted(wheel_dir.glob('*.whl'))[0]
        extracted = TOOL / 'wheel-extract'
        with __import__('zipfile').ZipFile(wheel) as archive:
            archive.extractall(extracted)
        libs = next(extracted.glob('psycopg2_binary.libs'))
        # libssl.so.1.1 has a DT_NEEDED on libcrypto's hash-suffixed soname, so keep both names.
        for src, dst in (('libssl-3a880ada.so.1.1', 'libssl.so.1.1'), ('libssl-3a880ada.so.1.1', 'libssl-3a880ada.so.1.1'),
                         ('libcrypto-2ade47cd.so.1.1', 'libcrypto.so.1.1'), ('libcrypto-2ade47cd.so.1.1', 'libcrypto-2ade47cd.so.1.1')):
            shutil.copy2(libs / src, TOOL / dst)
        shutil.rmtree(wheel_dir)
        shutil.rmtree(extracted)

    # Build the harness executable from OfflineCompile.cs using a throwaway Roslyn host.
    harness_dir = OUT / 'harness'
    harness_dir.mkdir(parents=True, exist_ok=True)
    executable = harness_dir / 'offlinecompile'
    if not executable.exists() or not (harness_dir / 'offlinecompile.dll').exists():
        executable.unlink(missing_ok=True)
        build_dir = TOOL / 'npm-roslyn-build'
        package = npm_pack('gg.sunday.roslyn', build_dir)
        apphost = (DPRINT / 'dprint-plugin-roslyn').read_bytes().replace(
            b'dprint-plugin-roslyn.dll', b'offlinecompile.dll\0' + b'\0' * 2)
        (harness_dir / 'offlinecompile').write_bytes(apphost)
        os.chmod(harness_dir / 'offlinecompile', 0o755)

        deps = json.loads((DPRINT / 'dprint-plugin-roslyn.deps.json').read_text())
        app_runtime = deps['targets']['.NETCoreApp,Version=v10.0/linux-x64']
        target = {k: v for k, v in app_runtime.items() if k.startswith('runtimepack.')}
        target['offlinecompile/1.0.0'] = {'runtime': {
            'offlinecompile.dll': {},
            'Microsoft.CodeAnalysis.dll': {},
            'Microsoft.CodeAnalysis.CSharp.dll': {},
            'System.Collections.Immutable.dll': {},
        }}
        deps['targets']['.NETCoreApp,Version=v10.0/linux-x64'] = target
        deps['libraries'] = {
            'offlinecompile/1.0.0': {'type': 'project', 'serviceable': False, 'sha512': ''},
            **{k: v for k, v in deps['libraries'].items() if k.startswith('runtimepack.')},
            'Microsoft.CodeAnalysis.Common/5.9.0': {'type': 'reference', 'serviceable': False, 'sha512': ''},
            'Microsoft.CodeAnalysis.CSharp/5.9.0': {'type': 'reference', 'serviceable': False, 'sha512': ''},
        }
        deps['targets']['.NETCoreApp,Version=v10.0/linux-x64']['runtimepack.Microsoft.NETCore.App.Runtime.linux-x64/10.0.11']['runtime'].pop(
            'System.Collections.Immutable.dll', None)
        (harness_dir / 'offlinecompile.deps.json').write_text(json.dumps(deps, indent=2))
        (harness_dir / 'offlinecompile.runtimeconfig.json').write_text(json.dumps({
            'runtimeOptions': {
                'tfm': 'net10.0',
                'includedFrameworks': [{'name': 'Microsoft.NETCore.App', 'version': '10.0.11'}],
                'configProperties': {'System.Globalization.Invariant': True},
            }
        }, indent=2))
        for name in ('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll',
                     'System.Collections.Immutable.dll', 'System.Reflection.Metadata.dll'):
            shutil.copy2(DPRINT / name, harness_dir / name)

        compile_script = TOOL / 'build_harness.py'
        compile_script.write_text(BUILD_HARNESS)
        env = dict(os.environ)
        env.update({
            'DOTNET_ROOT': str(runtime_bin),
            'PATH': str(runtime_bin) + ':' + env['PATH'],
            'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT': '1',
            'LD_LIBRARY_PATH': str(TOOL),
            'PYTHONPATH': str(ROOT / '.venv' / 'lib' / 'python3.11' / 'site-packages'),
            'OC_PACKAGE': str(package),
            'OUT_DIR': str(harness_dir),
        })
        # The patched apphost is self-contained: it resolves the framework from its own directory.
        for entry in DPRINT.iterdir():
            link = harness_dir / entry.name
            if not link.exists() and entry.name not in ('offlinecompile', 'offlinecompile.dll'):
                os.symlink(os.path.relpath(entry.resolve(), harness_dir.resolve()), link)
        result = subprocess.run([str(venv_python), str(compile_script)], cwd=str(ROOT), env=env,
                                capture_output=True, text=True)
        if result.returncode != 0:
            print(result.stdout[-4000:]); print(result.stderr[-4000:])
        result.check_returncode()
        shutil.rmtree(build_dir)

    # Base class library for reference builds. Both stages use ONE consistent framework set: mixing
    # .NET Core 3.1 and .NET 10 facades produces CS0433 duplicate-type errors, and the .NET 10 set
    # carries RequiredMemberAttribute, which the patches/ aliases below resolve.
    framework = TOOL / 'bcl'
    framework.mkdir(parents=True, exist_ok=True)
    copied = 0
    for dll in sorted(DPRINT.glob('*.dll')):
        target_file = framework / dll.name
        if not target_file.exists():
            shutil.copy2(dll, target_file)
            copied += 1
    if copied:
        print('staged', copied, 'BCL assemblies at', framework)

    print('toolchain ready at', TOOL)


BUILD_HARNESS = r'''
import glob, os, sys
import clr_loader, pythonnet
cfg = os.path.join(os.environ['OC_PACKAGE'], '..', 'roslynrc.json')
open(cfg, 'w').write('{"runtimeOptions":{"tfm":"netcoreapp3.1","framework":{"name":"Microsoft.NETCore.App","version":"3.1.23"},"configProperties":{"System.Globalization.Invariant":true}}}')
pythonnet.set_runtime(clr_loader.get_coreclr(runtime_config=cfg))
import clr
pkg = os.environ['OC_PACKAGE']
clr.AddReference(os.path.join(pkg, 'Microsoft.CodeAnalysis.dll'))
clr.AddReference(os.path.join(pkg, 'Microsoft.CodeAnalysis.CSharp.dll'))
from Microsoft.CodeAnalysis import SyntaxTree, MetadataReference, OutputKind
from Microsoft.CodeAnalysis.CSharp import CSharpCompilation, CSharpCompilationOptions, CSharpParseOptions, LanguageVersion, CSharpSyntaxTree
from System.Collections.Generic import List as NetList
from System import IO
repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
source = os.path.join(repo, 'Tools', 'OfflineCompile', 'OfflineCompile.cs')
trees = NetList[SyntaxTree]()
trees.Add(CSharpSyntaxTree.ParseText(open(source).read(), CSharpParseOptions(LanguageVersion.CSharp8), source))
refs = NetList[MetadataReference]()
fx = os.path.join(os.environ['DOTNET_ROOT'], 'shared', 'Microsoft.NETCore.App', '3.1.23')
for dll in sorted(glob.glob(os.path.join(fx, '*.dll'))):
    try: refs.Add(MetadataReference.CreateFromFile(dll))
    except Exception: pass
for dll in ('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll'):
    refs.Add(MetadataReference.CreateFromFile(os.path.join(pkg, dll)))
comp = CSharpCompilation.Create('offlinecompile', trees, refs, CSharpCompilationOptions(OutputKind.ConsoleApplication))
errors = [d for d in comp.GetDiagnostics() if d.Severity.ToString() == 'Error']
for e in errors[:10]: print(e)
if errors: raise SystemExit('harness compile failed')
out = os.environ['OUT_DIR']
with IO.FileStream(os.path.join(out, 'offlinecompile.dll'), IO.FileMode.Create) as s:
    print('emit', comp.Emit(s).Success)
'''


if __name__ == '__main__':
    main()

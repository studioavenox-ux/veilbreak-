#!/usr/bin/env python3
"""Fetch Unity 6000.3 C# reference source + uGUI so real reference assemblies can be built offline.

Unity ships the managed API as source in Unity-Technologies/UnityCsReference and the UI package in
Unity-Technologies/uGUI. Compiling a subset of that source gives a reference assembly that catches
genuine API misuse. This is developer tooling: raw sources land in Tools/.toolchain (git-ignored),
the small local patches live in Tools/OfflineCompile/patches, and no engine code is redistributed.
"""
import io
import pathlib
import tarfile
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
TOOL = ROOT / 'Tools' / '.toolchain' / 'refsrc'
BRANCH = '6000.3'

SOURCES = {
    'UnityCsReference': f'https://codeload.github.com/Unity-Technologies/UnityCsReference/tar.gz/refs/heads/{BRANCH}',
    'uGUI': f'https://codeload.github.com/Unity-Technologies/uGUI/tar.gz/refs/heads/{BRANCH}',
}


def fetch(name, url):
    target = TOOL / name
    if target.exists():
        print('have', name)
        return target
    print('download', url)
    with urllib.request.urlopen(url, timeout=300) as response:
        payload = response.read()
    with tarfile.open(fileobj=io.BytesIO(payload), mode='r:gz') as tar:
        members = [m for m in tar.getmembers() if m.name.count('/') >= 1]
        for member in members:
            parts = member.name.split('/')
            member.name = '/'.join(parts[1:]) if len(parts) > 1 else parts[0]
            member.path = member.name
            if not member.isfile() and not member.isdir():
                continue
            if member.isfile():
                if member.name.endswith('.cs') or member.name.endswith('.csproj') or member.name.endswith('.asmdef'):
                    tar.extract(member, target)
    return target


def main():
    TOOL.mkdir(parents=True, exist_ok=True)
    for name, url in SOURCES.items():
        fetch(name, url)
    print('reference sources ready at', TOOL)


if __name__ == '__main__':
    main()

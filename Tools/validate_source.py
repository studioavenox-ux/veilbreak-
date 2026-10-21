#!/usr/bin/env python3
"""Source-only checks. NOT a substitute for Unity compilation or gameplay tests.
Optional syntax parser: pip install tree-sitter tree-sitter-c-sharp
"""
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors = []
files = sorted((ROOT / 'Assets').rglob('*.cs'))
for path in (ROOT / 'Packages').glob('*.json'):
    json.loads(path.read_text())
for path in (ROOT / 'Assets').rglob('*.asmdef'):
    json.loads(path.read_text())
ET.parse(ROOT / 'Assets/VEILBREAK/link.xml')
try:
    import tree_sitter_c_sharp
    from tree_sitter import Language, Parser
    parser = Parser(Language(tree_sitter_c_sharp.language()))
except ImportError:
    parser = None
    print('SKIP C# syntax parser: install tree-sitter and tree-sitter-c-sharp')
for path in files:
    source = path.read_text()
    if parser and parser.parse(source.encode()).root_node.has_error:
        errors.append(f'C# parse error: {path.relative_to(ROOT)}')
    classes = re.findall(r'\b(?:class|enum)\s+(\w+)', source)
    if path.stem not in classes:
        errors.append(f'File/class mismatch: {path.name}')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        errors.append(f'Missing script meta: {path.name}')
# Every checked-in Unity asset and directory should have a stable GUID.
guids = []
for path in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
    if not match: errors.append(f'Invalid GUID: {path}')
    else: guids.append(match[1])
if len(guids) != len(set(guids)): errors.append('Duplicate meta GUID')
for banned in ('Library', 'Temp', 'Obj', 'Logs', 'node_modules'):
    if (ROOT / banned).exists(): errors.append(f'Unexpected generated directory: {banned}')
if errors:
    raise SystemExit('\n'.join(errors))
print(f'PASS: {len(files)} C# files checked; JSON/XML valid; {len(guids)} unique Unity asset GUIDs.')
print('C# syntax parsed.' if parser else 'C# syntax parsing NOT performed.')
print('Unity compilation, Unity tests, Android build and device performance NOT run by this checker.')

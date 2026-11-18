#!/usr/bin/env python3
"""Syntax-check the Unity acceptance PowerShell scripts.

Two checks run, because they fail differently:

  * tree-sitter-powershell - Microsoft's PowerShell grammar, parsed here and now. This is the
    primary check and its verdict decides the exit code.
  * System.Management.Automation.Language.Parser (PowerShell's own parser) - used when it can load
    on this host. The staged assembly is ReadyToRun for Windows, so on this Linux sandbox it
    cannot load; that is reported honestly as SKIPPED, and on a Windows/pwsh host it is used.

Neither check executes the scripts. What they prove: the scripts parse, so a broken param block or
unclosed brace fails here instead of during the Windows Unity run.
What they do NOT prove: that the scripts behave correctly. Only a real run does that
(`Tools/UnityValidation/Tests/UnityAcceptance.Tests.ps1` covers the reporting logic).

usage: .venv/bin/python Tools/UnityValidation/check_powershell_syntax.py [files...]
exit:  0 parsed clean, 1 syntax error, 2 checker unavailable
"""
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]


def collect():
    """Every acceptance script that must parse.

    Tests/fixtures is excluded on purpose: it holds deliberately broken files that the checker
    itself is tested against, so including them would make the repo fail its own syntax gate.
    """
    folder = ROOT / 'Tools' / 'UnityValidation'
    files = sorted(folder.rglob('*.ps1')) + sorted(folder.rglob('*.psm1'))
    return [p for p in files if p.is_file() and 'fixtures' not in p.parts]


def tree_sitter_check(files):
    """Returns (ok, message) or (None, reason) when the grammar is unavailable."""
    try:
        import tree_sitter
        import tree_sitter_powershell
    except ImportError as error:
        return None, f'tree-sitter PowerShell grammar unavailable ({error})'

    language = tree_sitter.Language(tree_sitter_powershell.language())
    parser = tree_sitter.Parser(language)
    failures = 0
    for path in files:
        source = path.read_bytes()
        tree = parser.parse(source)
        if not tree.root_node.has_error:
            print(f'PARSED   {path.name}')
            continue
        failures += 1
        print(f'FAILED   {path.name}')
        for problem in find_errors(tree.root_node):
            line, column, kind = problem[0], problem[1], problem[2]
            # The node's own text is the clearest description of what the parser choked on.
            print(f'         {line}:{column} {kind}')
    print(f'tree-sitter-powershell: {len(files)} file(s), {failures} with syntax errors')
    return failures == 0, f'{len(files)} file(s)'


def find_errors(node, depth=0):
    """Yields (line, column, text) for every ERROR or MISSING node, shallowest first.

    The offending text is included because tree-sitter reports a node type (often just the token it
    could not place), which is far more useful with the surrounding source attached.
    """
    if node.is_error or node.is_missing:
        snippet = node.text.decode('utf-8', 'replace').strip().splitlines()
        where = snippet[0][:60] if snippet else node.type
        # Name the kind explicitly: a MISSING node's type is the token that is absent, which reads as
        # nonsense on its own (e.g. a bare '"}"').
        kind = 'MISSING' if node.is_missing else 'ERROR'
        yield (node.start_point[0] + 1, node.start_point[1] + 1, f'{kind} near {where!r}')
        return
    for child in node.children:
        yield from find_errors(child, depth + 1)


def run_parser_binary(files):
    """Runs the compiled PowerShellSyntax helper; returns its exit code or None if unavailable."""
    import subprocess
    script = ROOT / 'Tools' / 'UnityValidation' / 'check_powershell.sh'
    if not script.exists():
        return None
    result = subprocess.run(['bash', str(script)] + [str(f) for f in files],
                            capture_output=True, text=True)
    # The parser helper prints SKIPPED/FAILED lines; only surface them when the helper actually ran.
    if result.returncode != 2:
        print(result.stdout.strip())
    if result.stderr.strip():
        print(result.stderr.strip())
    return result.returncode


def main(argv):
    files = [pathlib.Path(a) for a in argv[1:]] or collect()
    missing = [p for p in files if not p.is_file()]
    if missing:
        # An absent file must fail loudly rather than be skipped: "not found" is not "clean".
        for path in missing:
            print(f'MISSING  {path}')
        print(f'POWERSHELL SYNTAX FAILED - {len(missing)} file(s) not found')
        return 1
    if not files:
        print('no PowerShell scripts found', file=sys.stderr)
        return 2

    ok, detail = tree_sitter_check(files)
    if ok is None:
        print(f'POWERSHELL SYNTAX SKIPPED - {detail}')
        print('run: python3 Tools/UnityValidation/setup_pssyntax.py')
        return 2

    # The authoritative parser is a bonus: its verdict can only make things stricter, never pass
    # something tree-sitter rejected.
    parser_code = run_parser_binary(files)
    if parser_code not in (None, 0, 2):
        print('System.Management.Automation parser reported syntax errors')
        return 1

    print()
    if not ok:
        print(f'POWERSHELL SYNTAX FAILED - {detail} checked by tree-sitter-powershell')
        return 1
    print(f'POWERSHELL SYNTAX PASSED - {detail} checked by tree-sitter-powershell')
    if parser_code == 2:
        print('NOTE: PowerShell\'s own parser could not load on this host (Windows-only assembly);')
        print('      it must be run on Windows before the gates (see Run-UnityGates.ps1).')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))

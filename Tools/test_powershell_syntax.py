"""Regression tests for the PowerShell syntax checker used by the Unity acceptance runner.

These prove the checker actually detects the errors it claims to detect, and that it never reports
PASS while skipping - the failure mode that would let a broken acceptance runner reach Windows.
"""
import contextlib
import importlib.util
import io
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# Import the checker by path: `unittest discover -s Tools` puts Tools/ on sys.path, not the repo
# root, so a package import would not resolve in every invocation style.
_SPEC = importlib.util.spec_from_file_location(
    'check_powershell_syntax', ROOT / 'Tools' / 'UnityValidation' / 'check_powershell_syntax.py')
checker = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(checker)
FIXTURES = ROOT / 'Tools' / 'UnityValidation' / 'Tests' / 'fixtures'


def run(paths):
    """Runs the checker over the given files and returns (exit_code, stdout)."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        code = checker.main(['check_powershell_syntax.py'] + [str(p) for p in paths])
    return code, buffer.getvalue()


class PowerShellSyntaxTests(unittest.TestCase):
    def test_valid_multiline_script_parses(self):
        code, output = run([FIXTURES / 'good-multiline.ps1'])
        self.assertEqual(0, code, output)
        self.assertIn('PARSED', output)
        self.assertIn('POWERSHELL SYNTAX PASSED', output)

    def test_unclosed_brace_is_rejected(self):
        code, output = run([FIXTURES / 'bad-unclosed-brace.ps1'])
        self.assertEqual(1, code, output)
        self.assertIn('FAILED', output)
        self.assertIn('POWERSHELL SYNTAX FAILED', output)

    def test_unclosed_string_is_rejected(self):
        code, output = run([FIXTURES / 'bad-unclosed-string.ps1'])
        self.assertEqual(1, code, output)
        self.assertIn('FAILED', output)

    def test_malformed_param_block_is_rejected(self):
        code, output = run([FIXTURES / 'bad-param-block.ps1'])
        self.assertEqual(1, code, output)
        self.assertIn('FAILED', output)

    def test_one_bad_file_fails_a_batch_of_good_ones(self):
        code, output = run([
            FIXTURES / 'good-multiline.ps1',
            FIXTURES / 'bad-unclosed-brace.ps1',
        ])
        self.assertEqual(1, code, output)
        self.assertIn('1 with syntax errors', output)

    def test_error_reports_a_line_and_column(self):
        code, output = run([FIXTURES / 'bad-unclosed-brace.ps1'])
        self.assertEqual(1, code)
        self.assertRegex(output, r'\d+:\d+ (ERROR|MISSING) near')

    def test_repository_scripts_all_parse(self):
        """The real acceptance scripts must be syntactically clean."""
        files = checker.collect()
        self.assertGreaterEqual(len(files), 3, 'expected the runner, module and test script')
        names = {p.name for p in files}
        self.assertIn('Run-UnityGates.ps1', names)
        self.assertIn('UnityGates.psm1', names)
        code, output = run(files)
        self.assertEqual(0, code, output)
        self.assertIn('POWERSHELL SYNTAX PASSED', output)

    def test_skipping_is_not_reported_as_passing(self):
        """If the grammar is unavailable the checker must exit 2 and say SKIPPED."""
        original = checker.tree_sitter_check

        def unavailable(files):
            return None, 'grammar unavailable in this test'

        checker.tree_sitter_check = unavailable
        try:
            code, output = run([FIXTURES / 'good-multiline.ps1'])
        finally:
            checker.tree_sitter_check = original
        self.assertEqual(2, code, 'an unavailable checker must not exit 0')
        self.assertIn('SKIPPED', output)
        self.assertNotIn('POWERSHELL SYNTAX PASSED', output)

    def test_missing_file_is_an_error_not_a_pass(self):
        code, output = run([FIXTURES / 'does-not-exist.ps1'])
        self.assertEqual(1, code, output)


class AcceptanceRunnerContractTests(unittest.TestCase):
    """Static checks that the runner actually implements the required reporting rules."""

    def setUp(self):
        self.runner = (ROOT / 'Tools' / 'UnityValidation' / 'Run-UnityGates.ps1').read_text()
        self.module = (ROOT / 'Tools' / 'UnityValidation' / 'UnityGates.psm1').read_text()

    def test_runner_checks_unity_exit_code_for_both_platforms(self):
        self.assertIn('Invoke-TestPlatform', self.runner)
        self.assertIn('$run.ExitCode', self.runner,
                      'the test gate must receive Unity\'s exit code')
        self.assertEqual(2, self.runner.count('Invoke-TestPlatform -Platform'),
                         'EditMode and PlayMode must both go through the checked path')

    def test_gate_requires_zero_failures_and_a_pass(self):
        self.assertIn('$Results.Failed -gt 0', self.module)
        self.assertIn('$Results.Passed -lt 1', self.module)
        self.assertIn('$ExitCode -ne 0', self.module)

    def test_runner_requires_all_five_encounter_cases(self):
        self.assertIn('$requiredEncounterCases', self.runner)
        self.assertIn('Test-RequiredCases', self.runner)
        for case in ('TitlePauseWinLoseAndRepeatedRestartResetTheEncounter',
                     'GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce',
                     'MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing',
                     'BossWarningOverridesRecentTutorialAndShiftMessages',
                     'BootstrapProducesExactlyTheLayoutGeometry'):
            self.assertIn(case, self.runner)

    def test_runner_does_not_claim_a_manual_playthrough(self):
        lowered = self.runner.lower()
        self.assertNotIn('manual playthrough passed', lowered)
        self.assertIn('not a manual playthrough', self.module.lower())
        self.assertIn('IncludeManualChecklist', self.runner)

    def test_runner_keeps_skipped_tests_visible(self):
        self.assertIn('SkippedCaseNames', self.module)
        self.assertIn('skipped', self.module)

    def test_result_file_is_removed_before_each_run(self):
        """A stale XML from an earlier run must never be accepted as this run's evidence."""
        self.assertIn('Remove-Item -LiteralPath $results -Force', self.runner)

    def test_paths_use_literal_and_quoted_forms(self):
        # Spaces: every file operation must be -LiteralPath, and paths handed to Unity must reach it
        # wrapped in double quotes. The runner escapes them for PowerShell as "`"$Path`"" so the
        # child process receives a single quoted argument.
        self.assertNotIn('Test-Path $', self.runner)
        self.assertIn('-LiteralPath', self.runner)
        self.assertIn('`"$ProjectPath`"', self.runner,
                      'the project path handed to Unity must be quote-escaped for spaces')
        self.assertIn('`"$results`"', self.runner,
                      'the test-results path handed to Unity must be quote-escaped for spaces')
        self.assertIn("$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).ProviderPath", self.runner,
                      'the project path must be normalised before use so comparisons are stable')

    def test_exit_code_is_derived_from_the_rows(self):
        self.assertIn('exit (Get-ExitCode -Rows $rows)', self.runner)


if __name__ == '__main__':
    unittest.main()

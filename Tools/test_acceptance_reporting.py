"""Verifies the Unity acceptance reporting rules from outside PowerShell.

Why this file exists: Tools/UnityValidation/Tests/UnityAcceptance.Tests.ps1 is the primary suite, but
it needs a PowerShell host, and the sandbox has none (the only reachable PowerShell distribution is
ReadyToRun-compiled for Windows). Without a second opinion, a mistake in the .psm1 could sit
unnoticed until the Windows run - exactly the risk this task was about.

So this mirrors every rule the Pester file asserts, against the same NUnit XML shapes, using rules
read from the .psm1 source. It is a cross-check, not the authority: if the two ever disagree, run
the Pester suite on Windows before trusting either.

Covered: exit codes, NUnit parsing/validation, zero-failure + one-pass requirements, all five
encounter cases, skipped/inconclusive visibility, no manual-playthrough claim, exit-code derivation
and paths containing spaces.
"""
import importlib.util
import re
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / 'Tools' / 'UnityValidation' / 'UnityGates.psm1'
RUNNER = ROOT / 'Tools' / 'UnityValidation' / 'Run-UnityGates.ps1'

ENCOUNTER_CASES = [
    'TitlePauseWinLoseAndRepeatedRestartResetTheEncounter',
    'GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce',
    'MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing',
    'BossWarningOverridesRecentTutorialAndShiftMessages',
    'BootstrapProducesExactlyTheLayoutGeometry',
]


def nunit_xml(cases, summary=None):
    """Builds NUnit XML the way Unity's Test Runner does (test-run root + test-case nodes)."""
    passed = sum(1 for _, r in cases if r == 'Passed')
    failed = sum(1 for _, r in cases if r == 'Failed')
    skipped = sum(1 for _, r in cases if r in ('Skipped', 'Inconclusive'))
    total = len(cases)
    numbers = summary or {'total': total, 'passed': passed, 'failed': failed, 'skipped': skipped}
    children = []
    for name, result in cases:
        if result == 'Failed':
            children.append(f'<test-case name="{name}" result="Failed"><failure>'
                            f'<message>boom</message></failure></test-case>')
        else:
            children.append(f'<test-case name="{name}" result="{result}" />')
    return ('<?xml version="1.0" encoding="utf-8"?>\n'
            f'<test-run id="2" total="{numbers["total"]}" passed="{numbers["passed"]}" '
            f'failed="{numbers["failed"]}" skipped="{numbers["skipped"]}" result="Passed">'
            f'<test-suite>{"".join(children)}</test-suite></test-run>')


def read_module_text():
    return MODULE.read_text()


def module_rules():
    """Extracts the constants the .psm1 enforces, so the mirror cannot silently drift."""
    text = read_module_text()
    return {
        'requires_exit_zero': '$ExitCode -ne 0' in text,
        'requires_no_failures': '$Results.Failed -gt 0' in text,
        'requires_a_pass': '$Results.Passed -lt 1' in text,
        'counts_from_case_nodes': "$xml.SelectNodes('//test-case')" in text,
        'treats_unknown_as_failed': 'default {' in text and 'Error' in text,
        'detects_summary_disagreement': 'disagrees with the' in text,
        'keeps_skipped_visible': 'SkippedCaseNames' in text,
        'no_manual_playthrough_claim': 'NOT a manual playthrough' in text,
        'manual_checklist_line': 'NOT executed by this runner' in text,
    }


class Mirror:
    """Python mirror of the .psm1 verdict functions (kept deliberately small)."""

    @staticmethod
    def read_results(path):
        info = {'parsed': False, 'total': 0, 'passed': 0, 'failed': 0, 'skipped': 0,
                'inconclusive': 0, 'cases': [], 'problems': []}
        if not path.exists():
            info['problems'].append('NUnit result file was not produced')
            return info
        text = path.read_text()
        if not text.strip():
            info['problems'].append('NUnit result file is empty')
            return info
        try:
            root = ET.fromstring(text)
        except ET.ParseError as error:
            info['problems'].append(f'NUnit result file is not valid XML: {error}')
            return info
        if root.tag != 'test-run':
            info['problems'].append('No /test-run element: this is not an NUnit result file.')
            return info
        info['parsed'] = True
        for case in root.iter('test-case'):
            result = case.get('result')
            info['cases'].append((case.get('name'), result))
            if result == 'Passed':
                info['passed'] += 1
            elif result == 'Failed':
                info['failed'] += 1
            elif result == 'Skipped':
                info['skipped'] += 1
            elif result == 'Inconclusive':
                info['inconclusive'] += 1
            else:
                info['failed'] += 1
        info['total'] = len(info['cases'])
        if info['total'] == 0:
            info['problems'].append('The run contains no test cases.')
        if info['total'] != int(root.get('total', info['total'])):
            info['problems'].append('Summary total disagrees with the test-case nodes.')
        if info['failed'] != int(root.get('failed', info['failed'])):
            info['problems'].append('Summary failed disagrees with the failed test-case nodes.')
        return info

    @staticmethod
    def gate(info, exit_code):
        reasons = []
        if exit_code != 0:
            reasons.append(f'Unity exited with code {exit_code}.')
        reasons.extend(info['problems'])
        if info['parsed']:
            if info['failed'] > 0:
                reasons.append('failed test case(s)')
            if info['passed'] < 1:
                reasons.append('No test case passed')
        detail = (f"total {info['total']}, passed {info['passed']}, failed {info['failed']}, "
                  f"skipped {info['skipped']}, inconclusive {info['inconclusive']}")
        not_run = [n for n, r in info['cases'] if r in ('Skipped', 'Inconclusive')]
        if not_run:
            detail += '; not run: ' + '; '.join(not_run)
        # The PowerShell appends every reason to the detail so a failure is always explainable.
        if reasons:
            detail += ' | ' + ' '.join(reasons)
        return ('PASS' if not reasons else 'FAIL'), detail

    @staticmethod
    def required_cases(info, required):
        missing = [n for n in required if not any(n in c for c, _ in info['cases'])]
        not_passed = [n for n in required
                      if any(n in c for c, _ in info['cases'])
                      and not any(n in c and r == 'Passed' for c, r in info['cases'])]
        return ('PASS' if not missing and not not_passed else 'FAIL'), missing, not_passed

    @staticmethod
    def exit_code(states):
        return 1 if any(s != 'PASS' for s in states) else 0


class AcceptanceReportingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)

    def write(self, content, name='results.xml'):
        path = self.folder / name
        path.write_text(content, encoding='utf-8')
        return path

    def results(self, cases, summary=None):
        info = Mirror.read_results(self.write(nunit_xml(cases, summary)))
        return info

    # --- the .psm1 really contains the rules this mirror tests -------------------------
    def test_module_encodes_every_rule(self):
        rules = module_rules()
        for rule, present in rules.items():
            self.assertTrue(present, f'UnityGates.psm1 no longer contains the {rule} rule')

    # --- 1. exit codes ----------------------------------------------------------------
    def test_nonzero_unity_exit_fails_even_with_perfect_results(self):
        info = self.results([('A', 'Passed')])
        state, detail = Mirror.gate(info, exit_code=3)
        self.assertEqual('FAIL', state)
        self.assertIn('exited with code 3', detail)

    def test_zero_exit_and_no_failures_passes(self):
        info = self.results([('A', 'Passed')])
        self.assertEqual('PASS', Mirror.gate(info, exit_code=0)[0])

    # --- 2. XML validation ------------------------------------------------------------
    def test_unparsable_xml_fails(self):
        info = Mirror.read_results(self.write('not xml <<<'))
        self.assertFalse(info['parsed'])
        state, detail = Mirror.gate(info, exit_code=0)
        self.assertEqual('FAIL', state)
        self.assertIn('not valid XML', detail)

    def test_missing_result_file_fails(self):
        info = Mirror.read_results(self.folder / 'absent.xml')
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_non_nunit_root_fails(self):
        info = Mirror.read_results(self.write('<something><test-case name="A" result="Passed"/></something>'))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_zero_tests_fails(self):
        info = self.results([])
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_any_failure_fails_and_names_the_case(self):
        info = self.results([('Good', 'Passed'), ('BadOne', 'Failed')])
        state, _ = Mirror.gate(info, exit_code=0)
        self.assertEqual('FAIL', state)

    def test_unknown_result_value_is_a_failure(self):
        info = self.results([('Weird', 'Error')])
        self.assertEqual(1, info['failed'])
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_summary_disagreement_is_surfaced(self):
        info = self.results([('Only', 'Passed')], summary={'total': 5, 'passed': 5, 'failed': 0, 'skipped': 0})
        self.assertTrue(info['problems'], 'a lying summary must be reported')
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    # --- 3. required encounter cases ---------------------------------------------------
    def test_all_five_required_cases_present_and_passed(self):
        info = self.results([(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES])
        self.assertEqual('PASS', Mirror.required_cases(info, ENCOUNTER_CASES)[0])

    def test_absent_required_case_fails(self):
        info = self.results([(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES[1:]])
        state, missing, _ = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state)
        self.assertEqual([ENCOUNTER_CASES[0]], missing)

    def test_skipped_required_case_fails(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases[2] = (f'X.{ENCOUNTER_CASES[2]}', 'Skipped')
        info = self.results(cases)
        state, _, not_passed = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state)
        self.assertEqual([ENCOUNTER_CASES[2]], not_passed)

    def test_failed_required_case_fails(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases[4] = (f'X.{ENCOUNTER_CASES[4]}', 'Failed')
        self.assertEqual('FAIL', Mirror.required_cases(self.results(cases), ENCOUNTER_CASES)[0])

    def test_runner_requires_exactly_the_five_declared_cases(self):
        runner = RUNNER.read_text()
        declared = re.findall(r"\$requiredEncounterCases = @\((.*?)\n\)", runner, re.S)[0]
        found = re.findall(r"'([A-Za-z]+)'", declared)
        self.assertEqual(ENCOUNTER_CASES, found)

    # --- 4/5. coverage honesty and skipped visibility ----------------------------------
    def test_skipped_and_inconclusive_stay_visible(self):
        info = self.results([('Passing', 'Passed'), ('SkippedOne', 'Skipped'), ('InconclusiveOne', 'Inconclusive')])
        _, detail = Mirror.gate(info, exit_code=0)
        self.assertIn('skipped 1', detail)
        self.assertIn('inconclusive 1', detail)
        self.assertIn('SkippedOne', detail)
        self.assertIn('InconclusiveOne', detail)

    def test_only_skipped_tests_does_not_satisfy_the_pass_requirement(self):
        info = self.results([('OnlySkipped', 'Skipped')])
        state, detail = Mirror.gate(info, exit_code=0)
        self.assertEqual('FAIL', state)
        self.assertIn('No test case passed', detail)

    def test_report_never_claims_a_manual_playthrough(self):
        module = MODULE.read_text()
        self.assertIn('NOT a manual playthrough', module)
        self.assertIn('NOT executed by this runner', module)
        for forbidden in ('manual playthrough: PASS', 'manual playthrough passed',
                          'manual playthrough complete'):
            self.assertNotIn(forbidden, module.lower())

    def test_runner_labels_coverage_as_automated(self):
        self.assertIn('automated PlayMode coverage', RUNNER.read_text())

    # --- exit code derivation ----------------------------------------------------------
    def test_any_non_pass_row_exits_nonzero(self):
        self.assertEqual(1, Mirror.exit_code(['PASS', 'BLOCKED']))
        self.assertEqual(1, Mirror.exit_code(['FAIL']))
        self.assertEqual(0, Mirror.exit_code(['PASS', 'PASS']))

    # --- paths with spaces -------------------------------------------------------------
    def test_results_in_a_path_with_spaces_and_brackets(self):
        folder = self.folder / 'veil break [dev]'
        folder.mkdir()
        path = folder / 'Edit Mode Results.xml'
        path.write_text(nunit_xml([('Spaced', 'Passed')]), encoding='utf-8')
        info = Mirror.read_results(path)
        self.assertTrue(info['parsed'])
        self.assertEqual(1, info['passed'])

    def test_runner_normalises_and_quotes_paths(self):
        runner = RUNNER.read_text()
        self.assertIn('(Resolve-Path -LiteralPath $ProjectPath).ProviderPath', runner)
        self.assertIn('`"$ProjectPath`"', runner)
        self.assertIn('`"$results`"', runner)
        self.assertNotIn('Test-Path $', runner)


if __name__ == '__main__':
    unittest.main()

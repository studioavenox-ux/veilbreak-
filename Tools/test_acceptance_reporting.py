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


def nunit_xml(cases, summary=None, testcasecount=None):
    """Builds NUnit XML the way Unity's Test Runner does (test-run root + test-case nodes).

    `summary` overrides individual attributes so tests can deliberately write a summary that
    disagrees with the case nodes, which is exactly what the acceptance gate must reject.
    """
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
    count_attr = f' testcasecount="{testcasecount}"' if testcasecount is not None else ''
    derived = {'total': total, 'passed': passed, 'failed': failed, 'skipped': skipped}
    # Required attributes always appear; optional ones (skipped, inconclusive, warnings) appear only
    # when the test supplies them, so "absent attribute" cases can be exercised.
    required = ('total', 'passed', 'failed')
    attributes = ''.join(
        f' {key}="{numbers.get(key, derived[key])}"' for key in required)
    optional = ''.join(
        f' {key}="{value}"' for key, value in numbers.items()
        if key not in required)
    return ('<?xml version="1.0" encoding="utf-8"?>\n'
            f'<test-run id="2"{attributes}{optional}{count_attr} result="Passed">'
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
        # Added in the final audit: every summary count is cross-checked, not just total/failed.
        'compares_passed_summary': "-Label 'passed'" in text,
        'compares_skipped_summary': "-Label 'skipped'" in text,
        'compares_inconclusive_summary': "-Label 'inconclusive'" in text,
        'compares_warnings_summary': "-Label 'warnings'" in text,
        'compares_testcasecount': 'testcasecount (' in text,
        'warnings_are_separate': 'WarningCaseNames' in text,
        'duplicate_names_detected': 'Duplicate test-case name(s)' in text,
        'required_cases_report_ambiguity': 'matched more than one distinct test' in text,
        'required_cases_report_conflicts': 'had conflicting results' in text,
        # The old code read $node.GetAttribute('x') before testing presence, which returns $null and
        # throws on [int]$null under StrictMode. Attribute reads must go through Get-XmlAttribute.
        'guarded_attribute_access': 'function Get-XmlAttribute' in text,
        'no_unguarded_getattribute': not any(
            line.strip().startswith('if') and '.GetAttribute(' in line
            for line in text.splitlines()),
    }


class Mirror:
    """Faithful Python port of the .psm1 verdict logic.

    Ported rather than restated: the same counting rules, the same "summary must agree with the
    nodes" checks, the same required-case matching. Kept in step by module_rules(), which fails if
    the .psm1 loses a rule this port relies on.
    """

    @staticmethod
    def _attr(node, name):
        return node.get(name)

    @staticmethod
    def _as_count(value):
        """Mirrors ConvertTo-SummaryCount: absent/blank/non-numeric makes no claim."""
        if value is None or str(value).strip() == '':
            return None
        try:
            return int(str(value).strip())
        except ValueError:
            return None

    @classmethod
    def read_results(cls, path):
        info = {
            'parsed': False, 'total': 0, 'passed': 0, 'failed': 0,
            'skipped': 0, 'inconclusive': 0, 'warnings': 0,
            'cases': [], 'failed_names': [], 'skipped_names': [], 'warning_names': [],
            'duplicate_names': [], 'problems': [],
        }
        path = Path(path)
        if not path.exists():
            info['problems'].append('NUnit result file was not produced')
            return info
        text = path.read_text(encoding='utf-8')
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
        cases = list(root.iter('test-case'))
        info['cases'] = cases
        info['total'] = len(cases)

        seen = set()
        for case in cases:
            name = case.get('name') or ''
            outcome = case.get('result') or ''
            key = f'{name}|{outcome}'
            if key in seen:
                info['duplicate_names'].append(name)
            seen.add(key)

            if outcome == 'Passed':
                info['passed'] += 1
            elif outcome == 'Failed':
                info['failed'] += 1
                info['failed_names'].append(name)
            elif outcome == 'Skipped':
                info['skipped'] += 1
                info['skipped_names'].append(name)
            elif outcome == 'Inconclusive':
                info['inconclusive'] += 1
                info['skipped_names'].append(name)
            elif outcome == 'Warning':
                info['warnings'] += 1
                info['warning_names'].append(name)
            else:
                label = 'no result attribute' if not outcome.strip() else outcome
                info['failed'] += 1
                info['failed_names'].append(f'{name} [{label}]')

        if info['duplicate_names']:
            unique = ', '.join(sorted(set(info['duplicate_names'])))
            info['problems'].append(f'Duplicate test-case name(s) in the result file: {unique}')

        for required in ('total', 'passed', 'failed'):
            if root.get(required) is None:
                info['problems'].append(f"test-run is missing the '{required}' attribute.")
        if info['total'] == 0:
            info['problems'].append('The run contains no test cases.')

        for label, actual in (('total', info['total']), ('passed', info['passed']),
                              ('failed', info['failed']), ('skipped', info['skipped']),
                              ('inconclusive', info['inconclusive']), ('warnings', info['warnings'])):
            summary = cls._as_count(root.get(label))
            if summary is None:
                continue
            if summary != actual:
                info['problems'].append(
                    f'Summary {label} ({summary}) disagrees with the {actual} {label} test-case node(s).')

        testcasecount = cls._as_count(root.get('testcasecount'))
        if testcasecount is not None and testcasecount != info['total']:
            info['problems'].append(
                f'Summary testcasecount ({testcasecount}) disagrees with the {info["total"]} test-case nodes.')

        return info

    @classmethod
    def gate(cls, info, exit_code):
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
                  f"skipped {info['skipped']}, inconclusive {info['inconclusive']}, "
                  f"warnings {info['warnings']}")
        if info['skipped_names']:
            detail += '; not run: ' + '; '.join(info['skipped_names'])
        if info['warning_names']:
            detail += '; warned: ' + '; '.join(info['warning_names'])
        if reasons:
            detail += ' | ' + ' '.join(reasons)
        return ('PASS' if not reasons else 'FAIL'), detail

    @classmethod
    def required_cases(cls, info, required):
        missing, not_passed, ambiguous, conflicting = [], [], [], []
        for name in required:
            matches = [c for c in info['cases'] if name in (c.get('name') or '')]
            if not matches:
                missing.append(name)
                continue
            distinct = sorted({c.get('name') or '' for c in matches})
            if len(distinct) > 1:
                ambiguous.append(f'{name} -> {len(distinct)} distinct cases')
                continue
            outcomes = [c.get('result') or '' for c in matches]
            unique = sorted(set(outcomes))
            if len(unique) > 1:
                conflicting.append(f'{name} [{",".join(outcomes)}]')
                continue
            if unique[0] != 'Passed':
                not_passed.append(f'{name} [{",".join(outcomes)}]')
        state = 'PASS' if not (missing or not_passed or ambiguous or conflicting) else 'FAIL'
        return state, missing, not_passed, ambiguous, conflicting

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

    def results(self, cases, summary=None, testcasecount=None):
        info = Mirror.read_results(self.write(nunit_xml(cases, summary, testcasecount)))
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

    # --- 2b. summary-vs-node consistency (final audit) ----------------------------------
    def test_inflated_passed_summary_fails_the_gate(self):
        """The gap this audit found: passed was required but never compared."""
        info = self.results([('Only', 'Passed')],
                            summary={'total': 1, 'passed': 9, 'failed': 0, 'skipped': 0})
        self.assertTrue(any('Summary passed (9)' in p for p in info['problems']),
                        f'expected a passed-summary problem, got {info["problems"]}')
        state, detail = Mirror.gate(info, exit_code=0)
        self.assertEqual('FAIL', state)
        self.assertIn('Summary passed', detail)

    def test_deflated_passed_summary_fails_even_with_a_real_pass(self):
        # Under-reporting is equally wrong: it would let a partially lost result file look sane.
        info = self.results([('A', 'Passed'), ('B', 'Passed')],
                            summary={'total': 2, 'passed': 1, 'failed': 0, 'skipped': 0})
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_matching_passed_summary_is_accepted(self):
        info = self.results([('A', 'Passed'), ('B', 'Passed')])
        self.assertEqual([], info['problems'])
        self.assertEqual('PASS', Mirror.gate(info, exit_code=0)[0])

    def test_inconsistent_skipped_summary_fails(self):
        info = self.results([('A', 'Passed'), ('B', 'Skipped')],
                            summary={'total': 2, 'passed': 1, 'failed': 0, 'skipped': 0})
        self.assertTrue(any('Summary skipped' in p for p in info['problems']))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_inconsistent_inconclusive_summary_fails(self):
        info = self.results([('A', 'Passed'), ('B', 'Inconclusive')],
                            summary={'total': 2, 'passed': 1, 'failed': 0, 'skipped': 0,
                                     'inconclusive': 5})
        self.assertTrue(any('Summary inconclusive' in p for p in info['problems']))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_absent_skipped_and_inconclusive_attributes_make_no_claim(self):
        # "where available": older NUnit omits these, and that must not be an inconsistency.
        info = self.results([('A', 'Passed'), ('B', 'Skipped'), ('C', 'Inconclusive')],
                            summary={'total': 3, 'passed': 1, 'failed': 0})
        self.assertEqual([], info['problems'], info['problems'])
        self.assertEqual('PASS', Mirror.gate(info, exit_code=0)[0])

    def test_inconsistent_warnings_summary_fails(self):
        info = self.results([('A', 'Passed'), ('W', 'Warning')],
                            summary={'total': 2, 'passed': 1, 'failed': 0, 'skipped': 0,
                                     'warnings': 0})
        self.assertTrue(any('Summary warnings' in p for p in info['problems']))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_non_numeric_summary_attribute_is_not_trusted_as_a_number(self):
        # A junk value makes no claim; it must not crash and must not be read as 0.
        info = self.results([('A', 'Passed')],
                            summary={'total': 1, 'passed': 'many', 'failed': 0, 'skipped': 0})
        self.assertFalse(any('Summary passed' in p for p in info['problems']))
        self.assertEqual('PASS', Mirror.gate(info, exit_code=0)[0])

    def test_inconsistent_testcasecount_fails(self):
        info = self.results([('A', 'Passed'), ('B', 'Passed')],
                            summary={'total': 2, 'passed': 2, 'failed': 0, 'skipped': 0},
                            testcasecount=7)
        self.assertTrue(any('testcasecount (7)' in p for p in info['problems']))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    # --- 2c. Warning results -----------------------------------------------------------
    def test_warning_is_counted_separately_and_named_not_dropped(self):
        info = self.results([('Fine', 'Passed'), ('Warned', 'Warning')])
        self.assertEqual(1, info['warnings'])
        self.assertEqual(1, info['passed'])
        self.assertEqual(0, info['failed'], 'a Warning is pass-like, not a failure')
        self.assertEqual(['Warned'], info['warning_names'])
        state, detail = Mirror.gate(info, exit_code=0)
        self.assertEqual('PASS', state)
        self.assertIn('warnings 1', detail)
        self.assertIn('warned: Warned', detail)

    def test_a_run_of_only_warnings_does_not_satisfy_the_pass_requirement(self):
        info = self.results([('Warned', 'Warning')])
        state, detail = Mirror.gate(info, exit_code=0)
        self.assertEqual('FAIL', state)
        self.assertIn('No test case passed', detail)

    # --- 3. required encounter cases ---------------------------------------------------
    def test_all_five_required_cases_present_and_passed(self):
        info = self.results([(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES])
        self.assertEqual('PASS', Mirror.required_cases(info, ENCOUNTER_CASES)[0])

    def test_absent_required_case_fails(self):
        info = self.results([(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES[1:]])
        state, missing, _, _, _ = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state)
        self.assertEqual([ENCOUNTER_CASES[0]], missing)

    def test_skipped_required_case_fails(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases[2] = (f'X.{ENCOUNTER_CASES[2]}', 'Skipped')
        info = self.results(cases)
        state, _, not_passed, _, _ = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state)
        # The mirror names the offending outcome, matching the PowerShell wording.
        self.assertEqual([f'{ENCOUNTER_CASES[2]} [Skipped]'], not_passed)

    def test_failed_required_case_fails(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases[4] = (f'X.{ENCOUNTER_CASES[4]}', 'Failed')
        self.assertEqual('FAIL', Mirror.required_cases(self.results(cases), ENCOUNTER_CASES)[0])

    def test_duplicate_identical_case_nodes_are_detected(self):
        info = self.results([('Same.Name', 'Passed'), ('Same.Name', 'Passed')])
        self.assertTrue(any('Duplicate test-case name' in p for p in info['problems']))
        self.assertEqual('FAIL', Mirror.gate(info, exit_code=0)[0])

    def test_required_case_with_conflicting_results_fails(self):
        """A passed copy must not win just because it was listed first."""
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases.append((f'X.{ENCOUNTER_CASES[1]}', 'Failed'))
        info = self.results(cases)
        state, _, _, _, conflicting = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state)
        self.assertEqual([ENCOUNTER_CASES[1]], [c.split(' [')[0] for c in conflicting])

    def test_required_case_matching_two_distinct_cases_is_ambiguous(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases.append((f'Y.{ENCOUNTER_CASES[0]}', 'Passed'))
        info = self.results(cases)
        state, _, _, ambiguous, _ = Mirror.required_cases(info, ENCOUNTER_CASES)
        self.assertEqual('FAIL', state, 'two distinct cases cannot both prove one required case')
        self.assertEqual([ENCOUNTER_CASES[0]], [a.split(' ->')[0] for a in ambiguous])

    def test_required_case_duplicate_identical_result_is_still_required_to_pass(self):
        cases = [(f'X.{n}', 'Passed') for n in ENCOUNTER_CASES]
        cases.append((f'X.{ENCOUNTER_CASES[0]}', 'Passed'))
        info = self.results(cases)
        state, _, _, ambiguous, conflicting = Mirror.required_cases(info, ENCOUNTER_CASES)
        # Same case, same result: not ambiguous and not conflicting, so the requirement is met...
        self.assertEqual([], ambiguous)
        self.assertEqual([], conflicting)
        self.assertEqual('PASS', state)
        # ...but the duplicated node is still surfaced by the parser.
        self.assertTrue(any('Duplicate test-case name' in p for p in info['problems']))

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

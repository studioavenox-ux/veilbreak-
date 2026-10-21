"""Regression tests for the source checker only; these do not execute C# or Unity."""
import contextlib
import io
import json
from pathlib import Path
import runpy
import shutil
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


class SourceValidatorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for folder in ('Assets', 'Packages'):
            shutil.copytree(ROOT / folder, self.root / folder)
        (self.root / 'Tools').mkdir()
        shutil.copy(ROOT / 'Tools/validate_source.py', self.root / 'Tools/validate_source.py')

    def run_checker(self, tracked=b''):
        # Model git's tracked-file list without creating or modifying any repository.
        with patch('subprocess.check_output', return_value=tracked), contextlib.redirect_stdout(io.StringIO()):
            runpy.run_path(str(self.root / 'Tools/validate_source.py'), run_name='__main__')

    def test_current_sources_pass(self):
        self.run_checker()

    def test_package_name_is_not_an_assembly_name(self):
        path = self.root / 'Assets/VEILBREAK/Scripts/VEILBREAK.Runtime.asmdef'
        definition = json.loads(path.read_text())
        definition['references'] = ['Unity.ugui']
        path.write_text(json.dumps(definition))
        with self.assertRaisesRegex(SystemExit, 'Unknown assembly reference Unity.ugui'):
            self.run_checker()

    def test_ignored_local_unity_caches_are_allowed(self):
        for folder in ('Library', 'Temp', 'Logs'):
            (self.root / folder).mkdir()
        self.run_checker()

    def test_tracked_generated_cache_is_rejected(self):
        with self.assertRaisesRegex(SystemExit, 'Tracked generated cache: Library/example'):
            self.run_checker(b'Library/example\0')


if __name__ == '__main__':
    unittest.main()

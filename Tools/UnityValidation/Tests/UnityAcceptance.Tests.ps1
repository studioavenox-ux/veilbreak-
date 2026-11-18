<#
.SYNOPSIS
    Regression tests for the Unity acceptance reporting logic (Tools/UnityValidation/UnityGates.psm1).

.DESCRIPTION
    These tests need no Unity Editor and no network: they feed synthetic NUnit XML and gate
    arguments into the module and assert the verdicts. They exist because the runner's first
    version could report PASS while Unity had failed, while the results were unparsable, or while a
    required encounter case was missing - all three are now regressions below.

    Runs under Pester when it is installed. When Pester is absent it falls back to a tiny built-in
    harness so the checks still execute; the output says which engine ran.

    Usage:
        pwsh -File Tools/UnityValidation/Tests/UnityAcceptance.Tests.ps1
        Invoke-Pester Tools/UnityValidation/Tests
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$modulePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'UnityGates.psm1'
Import-Module $modulePath -Force

# --- shared fixtures -------------------------------------------------------------------
function New-NUnitXml {
    param([Parameter(Mandatory)][object[]]$Cases)
    $nodes = foreach ($case in $Cases) {
        $result = $case.Result
        $name = $case.Name
        # A failed case carries a <failure> element, exactly like a real NUnit run.
        if ($result -eq 'Failed') {
            "<test-case id=`"1`" name=`"$name`" result=`"Failed`"><failure><message>boom</message></failure></test-case>"
        } else {
            "<test-case id=`"1`" name=`"$name`" result=`"$result`" />"
        }
    }
    $passed = @($Cases | Where-Object { $_.Result -eq 'Passed' }).Count
    $failed = @($Cases | Where-Object { $_.Result -eq 'Failed' }).Count
    $skipped = @($Cases | Where-Object { $_.Result -in @('Skipped', 'Inconclusive') }).Count
    $total = $Cases.Count
    $outcome = if ($failed -gt 0) { 'Failed' } else { 'Passed' }
    @"
<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="$total" total="$total" passed="$passed" failed="$failed" skipped="$skipped" inconclusive="0" result="$outcome">
  <test-suite type="TestSuite" name="Veilbreak">
    $($nodes -join "`n")
  </test-suite>
</test-run>
"@
}

function Save-Xml {
    param([string]$Content)
    $path = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString() + '.xml')
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8
    return $path
}

$script:tempFiles = New-Object System.Collections.Generic.List[string]
function New-Results {
    param([Parameter(Mandatory)][object[]]$Cases)
    $path = Save-Xml (New-NUnitXml -Cases $Cases)
    $script:tempFiles.Add($path)
    return Read-NUnitResults -Path $path
}

$encounterCases = @(
    'TitlePauseWinLoseAndRepeatedRestartResetTheEncounter'
    'GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce'
    'MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing'
    'BossWarningOverridesRecentTutorialAndShiftMessages'
    'BootstrapProducesExactlyTheLayoutGeometry'
)

function New-EncounterCases {
    param([string]$Result = 'Passed', [switch]$OmitFirst)
    $cases = foreach ($name in $encounterCases) {
        if ($OmitFirst -and $name -eq $encounterCases[0]) { continue }
        @{ Name = "Veilbreak.Tests.EncounterReliabilityTests.$name"; Result = $Result }
    }
    return @($cases)
}

# --- the assertions (engine-agnostic) --------------------------------------------------
$script:tests = New-Object System.Collections.Generic.List[object]
function Test-Case {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][scriptblock]$Body)
    $script:tests.Add([pscustomobject]@{ Name = $Name; Body = $Body })
}
function Assert-Equal {
    param($Expected, $Actual, [string]$Because = '')
    if ($Expected -ne $Actual) {
        throw "expected '$Expected' but was '$Actual' $Because"
    }
}
function Assert-True {
    param([bool]$Condition, [string]$Because = '')
    if (-not $Condition) { throw "expected true $Because" }
}
function Assert-False {
    param([bool]$Condition, [string]$Because = '')
    if ($Condition) { throw "expected false $Because" }
}
function Assert-Match {
    param([string]$Text, [string]$Pattern, [string]$Because = '')
    if ($Text -notmatch $Pattern) { throw "expected '$Text' to match '$Pattern' $Because" }
}
function Assert-NotMatch {
    param([string]$Text, [string]$Pattern, [string]$Because = '')
    if ($Text -match $Pattern) { throw "expected '$Text' NOT to match '$Pattern' $Because" }
}

# --- 1. exit codes ---------------------------------------------------------------------
Test-Case 'Unity exit code alone fails the gate even with perfect results' {
    $results = New-Results -Cases @(@{ Name = 'A'; Result = 'Passed' })
    $gate = Test-NUnitGate -Results $results -ExitCode 3 -Platform 'EditMode'
    Assert-Equal 'FAIL' $gate.State 'a non-zero Unity exit code must fail the gate'
    Assert-Match $gate.Detail 'exit(ed)? code 3|Unity exited with code 3'
}

Test-Case 'Exit code zero with zero failures passes the gate' {
    $results = New-Results -Cases @(@{ Name = 'A'; Result = 'Passed' })
    $gate = Test-NUnitGate -Results $results -ExitCode 0 -Platform 'EditMode'
    Assert-Equal 'PASS' $gate.State
}

# --- 2. NUnit parsing and pass requirements --------------------------------------------
Test-Case 'Unparsable NUnit XML fails the gate' {
    $path = Save-Xml 'this is not xml at all <<<'
    $script:tempFiles.Add($path)
    $results = Read-NUnitResults -Path $path
    Assert-False $results.Parsed
    $gate = Test-NUnitGate -Results $results -ExitCode 0 -Platform 'PlayMode'
    Assert-Equal 'FAIL' $gate.State 'invalid XML must never pass'
    Assert-Match $gate.Detail 'not valid XML'
}

Test-Case 'A missing NUnit result file fails the gate' {
    $results = Read-NUnitResults -Path (Join-Path ([IO.Path]::GetTempPath()) 'definitely-not-here.xml')
    Assert-False $results.Parsed
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State
}

Test-Case 'XML without a test-run root fails the gate' {
    $path = Save-Xml '<?xml version="1.0"?><something><test-case name="A" result="Passed" /></something>'
    $script:tempFiles.Add($path)
    $results = Read-NUnitResults -Path $path
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State 'a file that is not an NUnit result must not be trusted'
    Assert-Match $gate.Detail 'No /test-run element'
}

Test-Case 'Zero tests run fails the gate' {
    $results = New-Results -Cases @()
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State 'a silent empty run must not pass'
    Assert-Match $gate.Detail 'no test cases|No test case passed'
}

Test-Case 'Any failed case fails the gate and names the case' {
    $results = New-Results -Cases @(
        @{ Name = 'Good'; Result = 'Passed' }
        @{ Name = 'BadOne'; Result = 'Failed' }
    )
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State
    Assert-Match $gate.Detail 'BadOne'
}

Test-Case 'An unknown result value counts as a failure, never a pass' {
    $results = New-Results -Cases @(@{ Name = 'Weird'; Result = 'Error' })
    Assert-Equal 1 $results.Failed 'Error must be treated as a failure'
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State
}

Test-Case 'A summary that disagrees with the case nodes is surfaced' {
    $path = Save-Xml @'
<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" total="5" passed="5" failed="0" result="Passed">
  <test-suite><test-case name="Only" result="Passed" /></test-suite>
</test-run>
'@
    $script:tempFiles.Add($path)
    $results = Read-NUnitResults -Path $path
    Assert-True ($results.Problems.Count -gt 0) 'a lying summary must be reported'
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State
}

# --- 3. required encounter cases -------------------------------------------------------
Test-Case 'All five required encounter cases present and passed passes' {
    $results = New-Results -Cases (New-EncounterCases)
    $check = Test-RequiredCases -Results $results -Required $encounterCases
    Assert-Equal 'PASS' $check.State
}

Test-Case 'A required encounter case that is absent fails' {
    $results = New-Results -Cases (New-EncounterCases -OmitFirst)
    $check = Test-RequiredCases -Results $results -Required $encounterCases
    Assert-Equal 'FAIL' $check.State 'a missing case is a failure, not an omission'
    Assert-Match $check.Detail ([regex]::Escape($encounterCases[0]))
}

Test-Case 'A required encounter case that was skipped fails' {
    $cases = @(New-EncounterCases)
    $cases[2] = @{ Name = "X.$($encounterCases[2])"; Result = 'Skipped' }
    $results = New-Results -Cases $cases
    $check = Test-RequiredCases -Results $results -Required $encounterCases
    Assert-Equal 'FAIL' $check.State
    Assert-Match $check.Detail 'did not pass'
}

Test-Case 'A required encounter case that failed fails' {
    $cases = @(New-EncounterCases)
    $cases[4] = @{ Name = "X.$($encounterCases[4])"; Result = 'Failed' }
    $results = New-Results -Cases $cases
    $check = Test-RequiredCases -Results $results -Required $encounterCases
    Assert-Equal 'FAIL' $check.State
}

# --- 4/5. coverage honesty and skipped visibility --------------------------------------
Test-Case 'Skipped and inconclusive tests stay visible in the detail' {
    $results = New-Results -Cases @(
        @{ Name = 'Passing'; Result = 'Passed' }
        @{ Name = 'SkippedOne'; Result = 'Skipped' }
        @{ Name = 'InconclusiveOne'; Result = 'Inconclusive' }
    )
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Match $gate.Detail 'skipped 1'
    Assert-Match $gate.Detail 'inconclusive 1'
    Assert-Match $gate.Detail 'SkippedOne'
    Assert-Match $gate.Detail 'InconclusiveOne'
}

Test-Case 'A run of only skipped tests does not satisfy the pass requirement' {
    $results = New-Results -Cases @(@{ Name = 'OnlySkipped'; Result = 'Skipped' })
    $gate = Test-NUnitGate -Results $results -ExitCode 0
    Assert-Equal 'FAIL' $gate.State 'skipped is not passed'
    Assert-Match $gate.Detail 'No test case passed'
}

Test-Case 'The report never claims a manual playthrough' {
    $rows = @(
        New-GateRow 'playmode-tests' 'PASS' 'PlayMode: total 18, passed 18'
        New-GateRow 'encounter-playthrough' 'PASS' 'covered by: X'
    )
    $report = Format-GateReport -Rows $rows
    Assert-Match $report 'automated PlayMode coverage is NOT a manual playthrough'
    Assert-NotMatch $report '(?i)manual playthrough\s*(passed|complete|verified|: OK)'
}

Test-Case 'The manual checklist is reported as not executed, not as passed' {
    $rows = @(New-GateRow 'playmode-tests' 'PASS' 'fine')
    $report = Format-GateReport -Rows $rows -IncludeManualChecklist
    Assert-Match $report 'MANUAL:\s+the manual Editor/mobile acceptance checklist.*NOT executed'
    Assert-NotMatch $report 'MANUAL:\s*PASS'
}

# --- exit codes of the runner ----------------------------------------------------------
Test-Case 'Any non-PASS row makes the runner exit non-zero' {
    $rows = @(
        New-GateRow 'a' 'PASS' ''
        New-GateRow 'b' 'BLOCKED' ''
    )
    Assert-Equal 1 (Get-ExitCode -Rows $rows)
    Assert-Equal 0 (Get-ExitCode -Rows @(New-GateRow 'a' 'PASS' ''))
}

Test-Case 'The table keeps every row, including failures and skips' {
    $rows = @(
        New-GateRow 'editmode-tests' 'FAIL' 'one failed'
        New-GateRow 'playmode-tests' 'PASS' 'ok'
        New-GateRow 'android-apk-build' 'BLOCKED' 'no SDK'
    )
    $report = Format-GateReport -Rows $rows
    foreach ($name in @('editmode-tests', 'playmode-tests', 'android-apk-build')) {
        Assert-Match $report $name
    }
    Assert-Match $report 'SUMMARY:'
    Assert-Match $report 'FAIL=1'
    Assert-Match $report 'PASS=1'
    Assert-Match $report 'BLOCKED=1'
}

# --- paths with spaces -----------------------------------------------------------------
Test-Case 'Results are read from a path containing spaces and brackets' {
    $directory = Join-Path ([IO.Path]::GetTempPath()) ("veil break [dev] $([guid]::NewGuid().ToString('N').Substring(0,8))")
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $path = Join-Path $directory 'Edit Mode Results.xml'
    Set-Content -LiteralPath $path -Value (New-NUnitXml -Cases @(@{ Name = 'Spaced'; Result = 'Passed' })) -Encoding UTF8
    try {
        $results = Read-NUnitResults -Path $path
        Assert-True $results.Parsed 'a spaced/bracketed path must still parse'
        Assert-Equal 1 $results.Passed
    } finally {
        Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- run ------------------------------------------------------------------------------
$failed = 0
$pester = Get-Module -ListAvailable -Name Pester | Select-Object -First 1
if ($pester -and $pester.Version.Major -ge 5) {
    Write-Host "Harness: Pester $($pester.Version)"
    $container = New-PesterContainer -ScriptBlock {
        param($tests)
        foreach ($t in $tests) {
            Describe $t.Name { It 'holds' { & $t.Body } }
        }
    } -Data @{ tests = $script:tests }
    $config = New-PesterConfiguration
    $config.Run.Container = $container
    $config.Run.PassThru = $true
    $config.Output.Verbosity = 'Normal'
    $result = Invoke-Pester -Configuration $config
    $failed = $result.FailedCount
    $executed = $result.TotalCount
} else {
    Write-Host 'Harness: built-in fallback (Pester 5 not installed)'
    $executed = 0
    foreach ($test in $script:tests) {
        $executed++
        try {
            & $test.Body
            Write-Host ("PASS  " + $test.Name) -ForegroundColor Green
        } catch {
            $failed++
            Write-Host ("FAIL  " + $test.Name + " :: " + $_.Exception.Message) -ForegroundColor Red
        }
    }
    Write-Host "PSENGINE RESULT executed=$executed failed=$failed"
}

foreach ($file in $script:tempFiles) { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
if ($failed -gt 0) { exit 1 }
exit 0

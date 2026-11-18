<#
.SYNOPSIS
    Pure reporting logic for the VEILBREAK Unity acceptance gates.

.DESCRIPTION
    Kept separate from Run-UnityGates.ps1 so the pass/fail decisions can be exercised by
    Tests/UnityAcceptance.Tests.ps1 without a Unity Editor. Nothing here launches a process.

    Design rules encoded below (each has a regression test):
      * A gate passes only when the tool actually succeeded: exit code AND artefact AND content.
      * NUnit XML must parse, must name a run, and must report >= 1 passed test with 0 failed.
      * Skipped and inconclusive tests stay visible - they are never folded into "passed" or
        silently dropped from the summary line.
      * Required encounter cases must each be present AND passed; a missing case is a failure,
        not an absence.
      * Automated PlayMode coverage is reported as automated coverage. It is never described as a
        manual playthrough.
#>

function Get-ChildElements {
    param($Node, [string]$Name)
    # SelectNodes with an XPath predicate keeps this working for both element and attribute lookups.
    if ($null -eq $Node) { return @() }
    return @($Node.SelectNodes(".//$Name"))
}

function Read-NUnitResults {
    <#
    Parses an NUnit 3 result file (the format Unity's Test Runner writes).
    Returns an object with Counts, Cases and Problems so the caller can decide, not this function.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $problems = New-Object System.Collections.Generic.List[string]
    $result = [pscustomobject]@{
        Parsed          = $false
        FileExists      = (Test-Path -LiteralPath $Path)
        RunName         = $null
        Total           = 0
        Passed          = 0
        Failed          = 0
        Skipped         = 0
        Inconclusive    = 0
        Warnings        = 0
        Cases           = @()
        FailedCaseNames = @()
        SkippedCaseNames = @()
        Problems        = $problems
    }

    if (-not $result.FileExists) {
        $problems.Add("NUnit result file was not produced: $Path")
        return $result
    }

    $text = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($text)) {
        $problems.Add("NUnit result file is empty: $Path")
        return $result
    }

    try {
        [xml]$xml = $text
    } catch {
        $problems.Add("NUnit result file is not valid XML: $($_.Exception.Message)")
        return $result
    }

    $run = $xml.SelectSingleNode('/test-run')
    if ($null -eq $run) {
        $problems.Add("No /test-run element: this is not an NUnit result file.")
        return $result
    }

    $result.Parsed = $true
    $result.RunName = $run.GetAttribute('testcasecount') + ' cases, result ' + $run.GetAttribute('result')

    # Counts come from the actual test-case nodes, not from the summary attributes, so a truncated
    # or hand-edited summary cannot inflate the numbers.
    $cases = @($xml.SelectNodes('//test-case'))
    $result.Cases = $cases
    foreach ($case in $cases) {
        $outcome = $case.GetAttribute('result')
        switch ($outcome) {
            'Passed'       { $result.Passed++ }
            'Failed'       { $result.Failed++; $result.FailedCaseNames += $case.GetAttribute('name') }
            'Skipped'      { $result.Skipped++; $result.SkippedCaseNames += $case.GetAttribute('name') }
            'Inconclusive' { $result.Inconclusive++; $result.SkippedCaseNames += $case.GetAttribute('name') }
            'Warning'      { $result.Warnings++ }
            default {
                # Anything else (e.g. 'Error') is a failure to be investigated, never a pass.
                $result.Failed++
                $result.FailedCaseNames += "$($case.GetAttribute('name')) [$outcome]"
            }
        }
    }
    $result.Total = $cases.Count

    foreach ($required in @('total', 'passed', 'failed')) {
        if (-not $run.HasAttribute($required)) { $problems.Add("test-run is missing the '$required' attribute.") }
    }
    if ($result.Total -eq 0) { $problems.Add("The run contains no test cases.") }
    if ($result.Total -ne [int]$run.GetAttribute('total') -and $run.HasAttribute('total')) {
        $problems.Add("Summary total ($($run.GetAttribute('total'))) disagrees with the $($result.Total) test-case nodes.")
    }
    if ($result.Failed -ne [int]$run.GetAttribute('failed') -and $run.HasAttribute('failed')) {
        $problems.Add("Summary failed ($($run.GetAttribute('failed'))) disagrees with the $($result.Failed) failed test-case nodes.")
    }
    return $result
}

function Test-NUnitGate {
    <#
    Turns parsed results into a gate verdict. A platform gate passes only when the results parsed,
    there are no failed cases, and at least one case actually passed. Skipped/inconclusive cases do
    not satisfy "at least one passed", and they remain listed in the detail.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Results,
        [Parameter(Mandatory)][int]$ExitCode,
        [string]$Platform = 'Test'
    )

    $reasons = New-Object System.Collections.Generic.List[string]
    if ($ExitCode -ne 0) { $reasons.Add("Unity exited with code $ExitCode.") }
    foreach ($problem in $Results.Problems) { $reasons.Add($problem) }
    if ($Results.Parsed) {
        if ($Results.Failed -gt 0) { $reasons.Add("$($Results.Failed) failed test case(s): $($Results.FailedCaseNames -join '; ')") }
        if ($Results.Passed -lt 1) { $reasons.Add("No test case passed (0 passed, $($Results.Skipped) skipped, $($Results.Inconclusive) inconclusive).") }
    }

    $state = if ($reasons.Count -eq 0) { 'PASS' } else { 'FAIL' }
    $detail = "$Platform: total $($Results.Total), passed $($Results.Passed), failed $($Results.Failed), " +
              "skipped $($Results.Skipped), inconclusive $($Results.Inconclusive), warnings $($Results.Warnings); " +
              "Unity exit $ExitCode"
    if ($Results.SkippedCaseNames.Count -gt 0) { $detail += "; not run: $($Results.SkippedCaseNames -join '; ')" }
    if ($reasons.Count -gt 0) { $detail += " | " + ($reasons -join ' ') }

    return [pscustomobject]@{
        State  = $state
        Detail = $detail
        Reasons = $reasons
    }
}

function Test-RequiredCases {
    <#
    Every required case must be present by name and must have result 'Passed'.
    Absent, skipped, inconclusive or failed are all failures with distinct wording.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Results,
        [Parameter(Mandatory)][string[]]$Required
    )

    $missing = New-Object System.Collections.Generic.List[string]
    $notPassed = New-Object System.Collections.Generic.List[string]
    foreach ($name in $Required) {
        $matches = @($Results.Cases | Where-Object { $_.GetAttribute('name') -like "*$name*" })
        if ($matches.Count -eq 0) { $missing.Add($name); continue }
        $passed = @($matches | Where-Object { $_.GetAttribute('result') -eq 'Passed' })
        if ($passed.Count -eq 0) {
            $outcomes = ($matches | ForEach-Object { $_.GetAttribute('result') }) -join ','
            $notPassed.Add("$name [$outcomes]")
        }
    }

    $reasons = New-Object System.Collections.Generic.List[string]
    if ($missing.Count -gt 0) { $reasons.Add("Required case(s) absent: $($missing -join ', ')") }
    if ($notPassed.Count -gt 0) { $reasons.Add("Required case(s) did not pass: $($notPassed -join ', ')") }

    $state = if ($reasons.Count -eq 0) { 'PASS' } else { 'FAIL' }
    $detail = if ($state -eq 'PASS') {
        "all $($Required.Count) required encounter cases present and passed: $($Required -join ', ')"
    } else {
        $reasons -join ' | '
    }
    return [pscustomobject]@{ State = $state; Detail = $detail; Reasons = $reasons; Missing = $missing; NotPassed = $notPassed }
}

function New-GateRow {
    param([string]$Name, [string]$State, [string]$Detail)
    [pscustomobject]@{ Name = $Name; State = $State; Detail = $Detail }
}

function Format-GateReport {
    <#
    Fixed-width table plus an explicit coverage-classification line.

    The classification line exists so nobody can read automated PlayMode coverage as a manual
    playthrough: the manual checklist is always reported separately, and always BLOCKED here.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Rows,
        [switch]$IncludeManualChecklist
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('GATE                          STATE     DETAIL')
    $lines.Add('----------------------------  --------  --------------------------------------------')
    foreach ($row in $Rows) {
        $lines.Add(('{0,-28}  {1,-8}  {2}' -f $row.Name, $row.State, $row.Detail))
    }

    $counts = @{}
    foreach ($row in $Rows) {
        if (-not $counts.ContainsKey($row.State)) { $counts[$row.State] = 0 }
        $counts[$row.State]++
    }
    $lines.Add('')
    $summaryParts = @($counts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" })
    $lines.Add('SUMMARY: ' + ($summaryParts -join ' '))
    $lines.Add('COVERAGE: automated PlayMode coverage is NOT a manual playthrough; it is reported as automated coverage only.')

    if ($IncludeManualChecklist) {
        $lines.Add('MANUAL:   the manual Editor/mobile acceptance checklist in Development.md was NOT executed by this runner.')
    }
    return ($lines -join [Environment]::NewLine)
}

function Get-ExitCode {
    param([object[]]$Rows)
    $bad = @($Rows | Where-Object { $_.State -ne 'PASS' })
    if ($bad.Count -gt 0) { return 1 }
    return 0
}

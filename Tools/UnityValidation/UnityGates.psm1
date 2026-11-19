<#
.SYNOPSIS
    Pure reporting logic for the VEILBREAK Unity acceptance gates.

.DESCRIPTION
    Kept separate from Run-UnityGates.ps1 so the pass/fail decisions can be exercised by
    Tests/UnityAcceptance.Tests.ps1 without a Unity Editor. Nothing here launches a process.

    Design rules encoded below (each has a regression test):
      * A gate passes only when the tool actually succeeded: exit code AND artefact AND content.
      * NUnit XML must parse, must name a run, and must report >= 1 passed test with 0 failed.
      * Present numeric summary counts are checked against the test-case nodes. A Failed/Error
        run result cannot pass even when all case nodes passed (e.g. a suite teardown failure).
      * Skipped and inconclusive tests stay visible - they are never folded into "passed" or
        silently dropped from the summary line.
      * Required encounter cases must each be present AND passed; a missing case is a failure,
        not an absence.
      * Automated PlayMode coverage is reported as automated coverage. It is never described as a
        manual playthrough.
#>

# --- XML helpers -----------------------------------------------------------------------
# Attribute access goes through these on purpose: reading $node.GetAttribute('x') before checking
# $node.HasAttribute('x') returns $null, and [int]$null throws under Set-StrictMode. The Attributes
# collection returns $null for a missing attribute, which is the behaviour wanted here.

function Get-XmlAttribute {
    param($Node, [string]$Name)
    if ($null -eq $Node) { return $null }
    $attributes = $Node.Attributes
    if ($null -eq $attributes) { return $null }
    $attribute = $attributes[$Name]
    if ($null -eq $attribute) { return $null }
    return $attribute.Value
}

function ConvertTo-SummaryCount {
    <# Returns the attribute as an int, or $null when absent, blank or not a number. #>
    param($Value)
    if ($null -eq $Value -or "$Value".Trim() -eq '') { return $null }
    $parsed = 0
    if ([int]::TryParse("$Value".Trim(), [ref]$parsed)) { return $parsed }
    return $null
}

function Compare-SummaryCount {
    <#
    Compares one summary attribute against the count taken from the test-case nodes.
    "Where available" is deliberate: NUnit omits attributes it does not use (some versions write
    neither warnings nor inconclusive), and a missing attribute is not an inconsistency - it makes no
    claim. Only a present, numeric, different value is reported.
    #>
    param(
        [Parameter(Mandatory)][string]$Label,
        $SummaryValue,
        [Parameter(Mandatory)][int]$Actual,
        [Parameter(Mandatory)]$Problems
    )
    $summary = ConvertTo-SummaryCount -Value $SummaryValue
    if ($null -eq $summary) { return }
    if ($summary -ne $Actual) {
        $Problems.Add("Summary $Label ($summary) disagrees with the $Actual $Label test-case node(s).")
    }
}

function Read-NUnitResults {
    <#
    Parses an NUnit 3 result file (Unity's Test Runner format) without trusting its summary.

    Every count is derived from the test-case nodes first; each summary attribute is then compared
    against the derived count and any disagreement becomes a Problem (which fails the gate).
    Attributes that are absent or not numeric make no claim and are not reported as inconsistent.
    Run-level Failed/Error results also fail the gate when no test case failed (see below).

    NUnit result vocabulary and how each is treated:
      Passed       - satisfies the pass requirement.
      Failed       - fails the gate; the case is named.
      Error        - not a normal NUnit result, but treated as Failed (never a pass).
      Skipped      - listed by name; does NOT satisfy the pass requirement.
      Inconclusive - listed by name; does NOT satisfy the pass requirement.
      Warning      - NUnit's "passed with a warning". It is pass-like, so it is counted separately
                     and listed by name, keeping the warning visible instead of folding it into the
                     pass count or dropping it.
      anything else (including an empty result) - counted as Failed, with the raw value shown.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $problems = New-Object System.Collections.Generic.List[string]
    $result = [pscustomobject]@{
        Parsed             = $false
        FileExists         = (Test-Path -LiteralPath $Path)
        RunName            = $null
        RunResult          = $null
        TestCaseCount      = $null
        Total              = 0
        Passed             = 0
        Failed             = 0
        Skipped            = 0
        Inconclusive       = 0
        Warnings           = 0
        Cases              = @()
        FailedCaseNames    = @()
        SkippedCaseNames   = @()
        WarningCaseNames   = @()
        DuplicateCaseNames = @()
        Problems           = $problems
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
    $result.RunResult = Get-XmlAttribute -Node $run -Name 'result'
    $result.TestCaseCount = ConvertTo-SummaryCount -Value (Get-XmlAttribute -Node $run -Name 'testcasecount')
    $result.RunName = "$($result.TestCaseCount) cases, result $($result.RunResult)"

    # Count from the case nodes, never from the summary attributes.
    $cases = @($xml.SelectNodes('//test-case'))
    $result.Cases = $cases
    $result.Total = $cases.Count

    $seen = @{}
    foreach ($case in $cases) {
        $name = "$(Get-XmlAttribute -Node $case -Name 'name')"
        $outcome = "$(Get-XmlAttribute -Node $case -Name 'result')"

        # A duplicated test-case name means the file cannot be attributed case by case, so it is
        # surfaced rather than silently collapsed into one result.
        $key = $name + '|' + $outcome
        if ($seen.ContainsKey($key)) { $result.DuplicateCaseNames += $name } else { $seen[$key] = $true }

        switch ($outcome) {
            'Passed'       { $result.Passed++ }
            'Failed'       { $result.Failed++; $result.FailedCaseNames += $name }
            'Skipped'      { $result.Skipped++; $result.SkippedCaseNames += $name }
            'Inconclusive' { $result.Inconclusive++; $result.SkippedCaseNames += $name }
            'Warning'      { $result.Warnings++; $result.WarningCaseNames += $name }
            default {
                $label = if ([string]::IsNullOrWhiteSpace($outcome)) { 'no result attribute' } else { $outcome }
                $result.Failed++
                $result.FailedCaseNames += "$name [$label]"
            }
        }
    }

    if ($result.DuplicateCaseNames.Count -gt 0) {
        $unique = (@($result.DuplicateCaseNames) | Select-Object -Unique) -join ', '
        $problems.Add("Duplicate test-case name(s) in the result file: $unique")
    }

    foreach ($required in @('total', 'passed', 'failed')) {
        if ($null -eq (Get-XmlAttribute -Node $run -Name $required)) {
            $problems.Add("test-run is missing the '$required' attribute.")
        }
    }
    if ($result.Total -eq 0) { $problems.Add("The run contains no test cases.") }

    Compare-SummaryCount -Label 'total'        -SummaryValue (Get-XmlAttribute -Node $run -Name 'total')        -Actual $result.Total        -Problems $problems
    Compare-SummaryCount -Label 'passed'       -SummaryValue (Get-XmlAttribute -Node $run -Name 'passed')       -Actual $result.Passed       -Problems $problems
    Compare-SummaryCount -Label 'failed'       -SummaryValue (Get-XmlAttribute -Node $run -Name 'failed')       -Actual $result.Failed       -Problems $problems
    Compare-SummaryCount -Label 'skipped'      -SummaryValue (Get-XmlAttribute -Node $run -Name 'skipped')      -Actual $result.Skipped      -Problems $problems
    Compare-SummaryCount -Label 'inconclusive' -SummaryValue (Get-XmlAttribute -Node $run -Name 'inconclusive') -Actual $result.Inconclusive -Problems $problems
    Compare-SummaryCount -Label 'warnings'     -SummaryValue (Get-XmlAttribute -Node $run -Name 'warnings')     -Actual $result.Warnings     -Problems $problems

    # testcasecount is NUnit's own tally of cases; it must agree when present.
    if ($null -ne $result.TestCaseCount -and $result.TestCaseCount -ne $result.Total) {
        $problems.Add("Summary testcasecount ($($result.TestCaseCount)) disagrees with the $($result.Total) test-case nodes.")
    }

    # A Failed/Error run can reflect a suite-level failure even when every case passed (for example,
    # teardown failed). Never report that run as a pass. A Passed run with failed cases is also
    # flagged. Warning/Skipped/Inconclusive run results are deliberately not
    # cross-checked: their precedence rules vary by NUnit version and they never change the verdict
    # on their own. An absent result makes no claim. Comparison is case-insensitive, matching the
    # case-insensitive result matching above.
    if ($result.RunResult -eq 'Failed' -or $result.RunResult -eq 'Error') {
        if ($result.Failed -eq 0) {
            $problems.Add("test-run result is $($result.RunResult) but no test-case failed.")
        }
    } elseif ($result.RunResult -eq 'Passed') {
        if ($result.Failed -gt 0) {
            $problems.Add("test-run result is Passed but $($result.Failed) test-case(s) failed.")
        }
    }

    return $result
}

function Test-NUnitGate {
    <#
    Turns parsed results into a gate verdict. A platform gate passes only when the results parsed,
    there are no failed cases, and at least one case actually passed.

    Skipped/inconclusive cases do not satisfy "at least one passed" and stay listed in the detail.
    Warning cases are pass-like, so they are reported by name rather than being silently counted as
    a clean pass or hidden.
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
        if ($Results.Failed -gt 0) {
            $reasons.Add("$($Results.Failed) failed test case(s): $($Results.FailedCaseNames -join '; ')")
        }
        if ($Results.Passed -lt 1) {
            $reasons.Add("No test case passed (0 passed, $($Results.Skipped) skipped, $($Results.Inconclusive) inconclusive).")
        }
    }

    $state = if ($reasons.Count -eq 0) { 'PASS' } else { 'FAIL' }
    $detail = "$Platform: total $($Results.Total), passed $($Results.Passed), failed $($Results.Failed), " +
              "skipped $($Results.Skipped), inconclusive $($Results.Inconclusive), warnings $($Results.Warnings); " +
              "Unity exit $ExitCode"
    if ($Results.SkippedCaseNames.Count -gt 0) { $detail += "; not run: $($Results.SkippedCaseNames -join '; ')" }
    if ($Results.WarningCaseNames.Count -gt 0) { $detail += "; warned: $($Results.WarningCaseNames -join '; ')" }
    if ($reasons.Count -gt 0) { $detail += " | " + ($reasons -join ' ') }

    return [pscustomobject]@{
        State   = $state
        Detail  = $detail
        Reasons = $reasons
    }
}

function Test-RequiredCases {
    <#
    Every required case must be present and must have passed at least once.

    Matching and conflict rules, in order:
      * A required name matches a test case by substring, as NUnit reports fully qualified names.
      * Ambiguity is a failure: if one required name matches more than one DISTINCT case, the file
        cannot prove which one ran. Conflicting results (e.g. the same case passed and failed) are
        reported explicitly instead of a pass winning by ordering.
      * A duplicate of the same case with the same result is not a conflict - it is still required to
        be passed, and the duplicate is already surfaced by Read-NUnitResults.
      * No match, or any match that is not Passed, fails with distinct wording.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Results,
        [Parameter(Mandatory)][string[]]$Required
    )

    $missing = New-Object System.Collections.Generic.List[string]
    $notPassed = New-Object System.Collections.Generic.List[string]
    $ambiguous = New-Object System.Collections.Generic.List[string]
    $conflicting = New-Object System.Collections.Generic.List[string]

    foreach ($name in $Required) {
        $matches = @($Results.Cases | Where-Object {
            "$(Get-XmlAttribute -Node $_ -Name 'name')" -like "*$name*"
        })
        if ($matches.Count -eq 0) { $missing.Add($name); continue }

        $distinctNames = @($matches | ForEach-Object { "$(Get-XmlAttribute -Node $_ -Name 'name')" } | Select-Object -Unique)
        if ($distinctNames.Count -gt 1) {
            $ambiguous.Add("$name -> $($distinctNames.Count) distinct cases ($($distinctNames -join ' | '))")
            continue
        }

        $outcomes = @($matches | ForEach-Object { "$(Get-XmlAttribute -Node $_ -Name 'result')" })
        $uniqueOutcomes = @($outcomes | Select-Object -Unique)
        if ($uniqueOutcomes.Count -gt 1) {
            $conflicting.Add("$name [$($outcomes -join ',')]")
            continue
        }
        if ($uniqueOutcomes[0] -ne 'Passed') {
            $notPassed.Add("$name [$($outcomes -join ',')]")
        }
    }

    $reasons = New-Object System.Collections.Generic.List[string]
    if ($missing.Count -gt 0) { $reasons.Add("Required case(s) absent: $($missing -join ', ')") }
    if ($ambiguous.Count -gt 0) { $reasons.Add("Required case(s) matched more than one distinct test: $($ambiguous -join '; ')") }
    if ($conflicting.Count -gt 0) { $reasons.Add("Required case(s) had conflicting results: $($conflicting -join '; ')") }
    if ($notPassed.Count -gt 0) { $reasons.Add("Required case(s) did not pass: $($notPassed -join ', ')") }

    $state = if ($reasons.Count -eq 0) { 'PASS' } else { 'FAIL' }
    $detail = if ($state -eq 'PASS') {
        "all $($Required.Count) required encounter cases present and passed: $($Required -join ', ')"
    } else {
        $reasons -join ' | '
    }

    return [pscustomobject]@{
        State       = $state
        Detail      = $detail
        Reasons     = $reasons
        Missing     = $missing
        NotPassed   = $notPassed
        Ambiguous   = $ambiguous
        Conflicting = $conflicting
    }
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

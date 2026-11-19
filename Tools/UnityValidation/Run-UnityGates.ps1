<#
.SYNOPSIS
    Runs the Unity 6000.3.26f1 acceptance gates for VEILBREAK and records real evidence.

.DESCRIPTION
    Nothing here is simulated. Each gate launches the real Editor and passes only when the tool
    succeeded AND its artefact exists AND its content is correct. It exists because the development
    sandbox has no Unity Editor, Android SDK or device: these gates can only run on the Windows
    machine that hosts:

        D:\UnityEditors\6000.3.26f1\Editor\Unity.exe

    Gates, in order:
      1. Import + compile   -batchmode -quit, zero 'error CS' occurrences in the log
      2. Generate Veil Arena twice and compare the SHA-256 of the resulting scene
      3. EditMode Test Runner  -> NUnit XML, must parse, 0 failed, >= 1 passed
      4. PlayMode Test Runner  -> NUnit XML, must parse, 0 failed, >= 1 passed
      5. Encounter coverage    -> five named play-mode cases must be present AND passed
      6. Android development APK build, with the BuildReport summary captured

    Rules the reporting enforces (see UnityGates.psm1 and Tests/UnityAcceptance.Tests.ps1):
      * a non-zero Unity exit code fails the gate even if the XML looks clean;
      * NUnit XML must parse and must report at least one passed test;
      * skipped/inconclusive tests stay listed, never folded into "passed";
      * automated PlayMode coverage is never described as a manual playthrough.

.PARAMETER ProjectPath
    Unity project root. Defaults to the repository containing this script.
    Paths with spaces, brackets and ampersands are supported.

.PARAMETER UnityPath
    Full path to Unity.exe. Defaults to the pinned development Editor.

.PARAMETER EvidencePath
    Where to write logs/XML/APK copies. Defaults to Documentation\UnityValidation\<timestamp>.

.PARAMETER KeepGoing
    Continue after a failed gate so every gate reports instead of stopping at the first.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\UnityValidation\Run-UnityGates.ps1

.EXAMPLE
    # Project path containing spaces, with evidence kept somewhere else
    powershell -ExecutionPolicy Bypass -File Tools\UnityValidation\Run-UnityGates.ps1 `
        -ProjectPath 'C:\Work\veil break [dev]\repo' -EvidencePath 'D:\evidence\veil break'
#>
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$UnityPath = 'D:\UnityEditors\6000.3.26f1\Editor\Unity.exe',
    [string]$EvidencePath,
    [switch]$KeepGoing
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'UnityGates.psm1') -Force

if (-not $ProjectPath) {
    $ProjectPath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}
# Normalise once so every downstream comparison and log line uses the same absolute form.
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).ProviderPath
if (-not $EvidencePath) {
    $EvidencePath = Join-Path $ProjectPath 'Documentation\UnityValidation' |
        Join-Path -ChildPath (Get-Date -Format 'yyyyMMdd-HHmmss')
}
New-Item -ItemType Directory -Force -Path $EvidencePath | Out-Null
$EvidencePath = (Resolve-Path -LiteralPath $EvidencePath).ProviderPath

# Cases that together constitute full automated encounter coverage.
$requiredEncounterCases = @(
    'TitlePauseWinLoseAndRepeatedRestartResetTheEncounter'
    'GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce'
    'MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing'
    'BossWarningOverridesRecentTutorialAndShiftMessages'
    'BootstrapProducesExactlyTheLayoutGeometry'
)

$rows = New-Object System.Collections.Generic.List[object]
function Add-Row {
    param([string]$Name, [string]$State, [string]$Detail)
    $rows.Add((New-GateRow -Name $Name -State $State -Detail $Detail))
    $colour = switch ($State) {
        'PASS' { 'Green' }
        'BLOCKED' { 'Yellow' }
        default { 'Red' }
    }
    Write-Host ("[{0}] {1} - {2}" -f $State, $Name, $Detail) -ForegroundColor $colour
}

function Save-Summary {
    param([string]$Path)
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("generated`t$(Get-Date -Format o)")
    $lines.Add("project`t$ProjectPath")
    $lines.Add("unity`t$UnityPath")
    $lines.Add("evidence`t$Path")
    $lines.Add('')
    foreach ($row in $rows) { $lines.Add(("{0}`t{1}`t{2}" -f $row.Name, $row.State, $row.Detail)) }
    $lines.Add('')
    $lines.Add('coverage' + "`t" + 'automated PlayMode coverage only; NOT a manual playthrough')
    $lines.Add('manual' + "`t" + 'manual Editor/mobile checklist NOT executed by this runner')
    Set-Content -LiteralPath (Join-Path $Path 'summary.tsv') -Value $lines -Encoding UTF8
}

function Invoke-Unity {
    <#
    Starts Unity and returns its exit code plus the log text.
    -ArgumentList is a string array, so PowerShell quotes each element; paths containing spaces
    survive without manual escaping. -LiteralPath is used for every file operation.
    #>
    param([string]$Label, [string[]]$Arguments)

    $log = Join-Path $EvidencePath "$Label.log"
    $all = @('-projectPath', "`"$ProjectPath`"", '-logFile', "`"$log`"", '-batchmode') + $Arguments
    Write-Host "Unity ($Label): $UnityPath $($all -join ' ')"
    $process = Start-Process -FilePath $UnityPath -ArgumentList $all -Wait -PassThru -NoNewWindow
    $text = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
    return [pscustomobject]@{ ExitCode = $process.ExitCode; Log = $log; Text = $text }
}

function Invoke-TestPlatform {
    param([string]$Platform, [string]$GateName)
    $results = Join-Path $EvidencePath "$Platform-results.xml"
    # A stale result file from an earlier run must never be mistaken for this run's evidence.
    if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results -Force }

    $run = Invoke-Unity -Label "tests-$Platform" -Arguments @(
        '-runTests', '-testPlatform', $Platform, '-testResults', "`"$results`"", '-quit')

    $parsed = Read-NUnitResults -Path $results
    $gate = Test-NUnitGate -Results $parsed -ExitCode $run.ExitCode -Platform $Platform
    Add-Row -Name $GateName -State $gate.State -Detail $gate.Detail
    return $parsed
}

# --- preconditions ---------------------------------------------------------------------
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
$pinned = if (Test-Path -LiteralPath $versionFile) {
    (Select-String -LiteralPath $versionFile -Pattern 'm_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value.Trim()
} else { 'unknown' }

if (-not (Test-Path -LiteralPath $UnityPath)) {
    Add-Row -Name 'editor' -State 'BLOCKED' -Detail "Unity not found at $UnityPath; expected $pinned. Install via Unity Hub with Android Build Support."
    foreach ($gate in 'unity-import-and-compile', 'generate-veil-arena', 'editmode-tests',
                      'playmode-tests', 'encounter-coverage', 'android-apk-build') {
        Add-Row -Name $gate -State 'BLOCKED' -Detail 'No Editor'
    }
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    Write-Warning "Editor missing: nothing was verified. Evidence stub written to $EvidencePath"
    exit 2
}

$actual = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
$versionOk = $actual -like "$pinned*"
Add-Row -Name 'editor-version' -State $(if ($versionOk) { 'PASS' } else { 'FAIL' }) `
    -Detail "pinned $pinned, launched $actual"
if (-not $versionOk -and -not $KeepGoing) {
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    exit 1
}

# --- 1. import + compile ---------------------------------------------------------------
$import = Invoke-Unity -Label 'import' -Arguments @('-quit')
$compileErrors = ([regex]::Matches($import.Text, 'error CS\d{4}')).Count
$importOk = $import.ExitCode -eq 0 -and $compileErrors -eq 0 -and $import.Text.Length -gt 0
Add-Row -Name 'unity-import-and-compile' -State $(if ($importOk) { 'PASS' } else { 'FAIL' }) `
    -Detail "exit $($import.ExitCode), $compileErrors CS error(s), $(if ($import.Text.Length -gt 0) { 'log captured' } else { 'LOG MISSING' }), $($import.Log)"
if (-not $importOk -and -not $KeepGoing) {
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    exit 1
}

# --- 2. generate the arena twice -------------------------------------------------------
$scene = Join-Path $ProjectPath 'Assets\VEILBREAK\Scenes\VeilArena.unity'
$hashes = @()
$generateOk = $true
foreach ($pass in 1..2) {
    $generated = Invoke-Unity -Label "generate-pass$pass" -Arguments @(
        '-quit', '-executeMethod', 'Veilbreak.Editor.VeilArenaGenerator.GenerateBatch')
    if ($generated.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $scene)) {
        Add-Row -Name 'generate-veil-arena' -State 'FAIL' `
            -Detail "pass $pass: exit $($generated.ExitCode), scene present: $(Test-Path -LiteralPath $scene)"
        $generateOk = $false
        break
    }
    $hashes += (Get-FileHash -LiteralPath $scene -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $scene -Destination (Join-Path $EvidencePath "VeilArena-pass$pass.unity") -Force
}
if ($generateOk -and $hashes.Count -eq 2) {
    $identical = $hashes[0] -eq $hashes[1]
    Add-Row -Name 'generate-veil-arena' -State $(if ($identical) { 'PASS' } else { 'FAIL' }) `
        -Detail "two passes, identical=$identical ($($hashes[0].Substring(0,12)) / $($hashes[1].Substring(0,12)))"
    $generateOk = $identical
}
if (-not $generateOk -and -not $KeepGoing) {
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    exit 1
}

# --- 3/4. test runners -----------------------------------------------------------------
$null = Invoke-TestPlatform -Platform 'EditMode' -GateName 'editmode-tests'
if ($rows[-1].State -ne 'PASS' -and -not $KeepGoing) {
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    exit 1
}
$playResults = Invoke-TestPlatform -Platform 'PlayMode' -GateName 'playmode-tests'
if ($rows[-1].State -ne 'PASS' -and -not $KeepGoing) {
    Save-Summary -Path $EvidencePath
    Write-Host (Format-GateReport -Rows $rows -IncludeManualChecklist)
    exit 1
}

# --- 5. encounter coverage -------------------------------------------------------------
$coverage = Test-RequiredCases -Results $playResults -Required $requiredEncounterCases
Add-Row -Name 'encounter-coverage' -State $coverage.State `
    -Detail ('automated PlayMode coverage: ' + $coverage.Detail)

# --- 6. Android development APK --------------------------------------------------------
$apk = Join-Path $ProjectPath 'Builds\Android\VEILBREAK-development.apk'
# Delete any previous APK first: its presence afterwards is the build evidence.
if (Test-Path -LiteralPath $apk) { Remove-Item -LiteralPath $apk -Force }
$built = $false
$buildDetail = 'not attempted' 
try {
    $build = Invoke-Unity -Label 'android-build' -Arguments @(
        '-quit', '-buildTarget', 'Android',
        '-executeMethod', 'Veilbreak.Editor.AndroidBuild.BuildDevelopment')
    $built = (Test-Path -LiteralPath $apk) -and $build.ExitCode -eq 0
    $size = if (Test-Path -LiteralPath $apk) { (Get-Item -LiteralPath $apk).Length } else { 0 }
    # The Editor logs the BuildReport summary; keep the line as the build evidence.
    $reportLine = ($build.Text -split "`r?`n" |
        Where-Object { $_ -match 'Android development APK built|Build succeeded|BuildReport|totalErrors' } |
        Select-Object -Last 3) -join ' ; '
    if (Test-Path -LiteralPath $apk) {
        Copy-Item -LiteralPath $apk -Destination (Join-Path $EvidencePath 'VEILBREAK-development.apk') -Force
    }
    $buildDetail = "exit $($build.ExitCode), apk=$built, $size bytes; report: $reportLine"
} catch {
    $buildDetail = "build threw: $($_.Exception.Message)"
}
Add-Row -Name 'android-apk-build' -State $(if ($built) { 'PASS' } else { 'FAIL' }) -Detail $buildDetail

# --- report ----------------------------------------------------------------------------
Save-Summary -Path $EvidencePath
$report = Format-GateReport -Rows $rows -IncludeManualChecklist
Write-Host ''
Write-Host $report
Set-Content -LiteralPath (Join-Path $EvidencePath 'report.txt') -Value $report -Encoding UTF8
Write-Host "`nEvidence: $EvidencePath"
exit (Get-ExitCode -Rows $rows)

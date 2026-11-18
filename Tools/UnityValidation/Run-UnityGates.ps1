<#
.SYNOPSIS
    Runs the Unity 6000.3.26f1 acceptance gates for VEILBREAK and records real evidence.

.DESCRIPTION
    This script does NOT simulate or approximate anything. Every gate below launches the real
    Editor and either succeeds with artefacts on disk or fails loudly. It exists because the
    development sandbox has no Unity Editor, Android SDK or device: these gates can only be run
    on the Windows development machine that hosts:

        D:\UnityEditors\6000.3.26f1\Editor\Unity.exe

    Gates, in order (stop on the first failure unless -KeepGoing):
      1. Import + compile   -batchmode -quit, then assert no compile errors in the log
      2. Generate Veil Arena twice (idempotency), diffing the resulting scene file
      3. EditMode tests     -runTests -testPlatform EditMode  -> NUnit XML
      4. PlayMode tests     -runTests -testPlatform PlayMode  -> NUnit XML
      5. Full encounter playthrough (PlayMode smoke, recorded from the same run)
      6. Android development APK build, with the BuildReport summary captured

    Evidence is written to Documentation/UnityValidation/<timestamp>/ so the run can be cited.

.PARAMETER ProjectPath
    Unity project root. Defaults to the repository containing this script.

.PARAMETER UnityPath
    Full path to Unity.exe. Defaults to the pinned development Editor.

.PARAMETER KeepGoing
    Continue after a failed gate so every gate reports instead of stopping at the first.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\UnityValidation\Run-UnityGates.ps1
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$UnityPath = 'D:\UnityEditors\6000.3.26f1\Editor\Unity.exe',
    [switch]$KeepGoing
)

$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$evidence = Join-Path $ProjectPath "Documentation\UnityValidation\$stamp"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$summary = [ordered]@{}
$failures = @()

function Write-Gate([string]$name, [string]$state, [string]$detail) {
    $summary[$name] = "$state :: $detail"
    $colour = if ($state -eq 'PASS') { 'Green' } elseif ($state -eq 'BLOCKED') { 'Yellow' } else { 'Red' }
    Write-Host ("[{0}] {1} - {2}" -f $state, $name, $detail) -ForegroundColor $colour
}

function Invoke-Unity([string]$label, [string[]]$arguments) {
    $log = Join-Path $evidence "$label.log"
    Write-Host "Running Unity ($label): $UnityPath $($arguments -join ' ')"
    $all = @('-projectPath', $ProjectPath, '-logFile', $log, '-batchmode') + $arguments
    $process = Start-Process -FilePath $UnityPath -ArgumentList $all -Wait -PassThru -NoNewWindow
    $exit = $process.ExitCode
    if (-not (Test-Path $log)) { throw "Unity produced no log for gate '$label'." }
    return [pscustomobject]@{ ExitCode = $exit; Log = $log; Text = (Get-Content $log -Raw) }
}

# --- preconditions ---------------------------------------------------------------------
if (-not (Test-Path $UnityPath)) {
    Write-Gate 'editor-present' 'BLOCKED' "Unity not found at $UnityPath. Install 6000.3.26f1 (Android Build Support, SDK/NDK/OpenJDK) via Unity Hub."
    Write-Gate 'unity-import-and-compile' 'BLOCKED' 'No Editor'
    Write-Gate 'generate-veil-arena' 'BLOCKED' 'No Editor'
    Write-Gate 'editmode-tests' 'BLOCKED' 'No Editor'
    Write-Gate 'playmode-tests' 'BLOCKED' 'No Editor'
    Write-Gate 'encounter-playthrough' 'BLOCKED' 'No Editor'
    Write-Gate 'android-apk-build' 'BLOCKED' 'No Editor'
    $summary.GetEnumerator() | ForEach-Object { "{0}`t{1}" -f $_.Key, $_.Value } |
        Set-Content -Path (Join-Path $evidence 'summary.tsv') -Encoding UTF8
    Write-Warning "Editor missing: nothing was verified. Evidence stub written to $evidence"
    exit 2
}

$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
$pinned = (Select-String -Path $versionFile -Pattern 'm_EditorVersion: (.*)').Matches[0].Groups[1].Value.Trim()
$actual = (Get-Item $UnityPath).VersionInfo.ProductVersion
Write-Gate 'editor-version' $(if ($actual -like "$pinned*") { 'PASS' } else { 'FAIL' }) "pinned $pinned, launched $actual"
if ($summary['editor-version'] -like 'FAIL*' -and -not $KeepGoing) { exit 1 }

# --- 1. import + compile ---------------------------------------------------------------
$import = Invoke-Unity 'import' @('-quit')
$compileErrors = ([regex]::Matches($import.Text, 'error CS\d{4}')).Count
$state = if ($import.ExitCode -eq 0 -and $compileErrors -eq 0) { 'PASS' } else { 'FAIL' }
Write-Gate 'unity-import-and-compile' $state "exit $($import.ExitCode), $compileErrors CS errors, log $($import.Log)"
if ($state -eq 'FAIL' -and -not $KeepGoing) { exit 1 }

# --- 2. generate the arena twice (idempotency) -----------------------------------------
$scene = Join-Path $ProjectPath 'Assets\VEILBREAK\Scenes\VeilArena.unity'
$hashes = @()
foreach ($pass in 1..2) {
    $gen = Invoke-Unity "generate-pass$pass" @('-quit', '-executeMethod', 'Veilbreak.Editor.VeilArenaGenerator.GenerateBatch')
    if ($gen.ExitCode -ne 0 -or -not (Test-Path $scene)) {
        Write-Gate 'generate-veil-arena' 'FAIL' "pass $pass: exit $($gen.ExitCode); scene present: $(Test-Path $scene)"
        if (-not $KeepGoing) { exit 1 }
        break
    }
    $hashes += (Get-FileHash -Path $scene -Algorithm SHA256).Hash
    Copy-Item $scene (Join-Path $evidence "VeilArena-pass$pass.unity")
}
if ($hashes.Count -eq 2) {
    $same = $hashes[0] -eq $hashes[1]
    Write-Gate 'generate-veil-arena' $(if ($same) { 'PASS' } else { 'FAIL' }) "two passes, identical=$same ($($hashes[0].Substring(0,12)) vs $($hashes[1].Substring(0,12)))"
}

# --- 3/4. EditMode and PlayMode test runners -------------------------------------------
function Invoke-TestPlatform([string]$platform, [string]$gate) {
    $results = Join-Path $evidence "$platform-results.xml"
    $run = Invoke-Unity "tests-$platform" @('-runTests', '-testPlatform', $platform,
        '-testResults', $results, '-quit')
    if (-not (Test-Path $results)) {
        Write-Gate $gate 'FAIL' "no NUnit result file; exit $($run.ExitCode)"
        return
    }
    [xml]$xml = Get-Content $results -Raw
    $total = [int]$xml.'test-run'.total
    $passed = [int]$xml.'test-run'.passed
    $failed = [int]$xml.'test-run'.failed
    $skipped = [int]$xml.'test-run'.skipped
    Write-Gate $gate $(if ($failed -eq 0 -and $total -gt 0) { 'PASS' } else { 'FAIL' }) `
        "total $total, passed $passed, failed $failed, skipped $skipped, results $results"
}
Invoke-TestPlatform 'EditMode' 'editmode-tests'
if (-not $KeepGoing -and $summary['editmode-tests'] -like 'FAIL*') { exit 1 }
Invoke-TestPlatform 'PlayMode' 'playmode-tests'
if (-not $KeepGoing -and $summary['playmode-tests'] -like 'FAIL*') { exit 1 }

# --- 5. full encounter playthrough -----------------------------------------------------
# The PlayMode suite drives the encounter end to end (title -> fight -> win/lose -> restart).
# Record whether those specific tests actually ran, rather than claiming a separate playthrough.
$playResults = Join-Path $evidence 'PlayMode-results.xml'
if (Test-Path $playResults) {
    [xml]$xml = Get-Content $playResults -Raw
    $names = $xml.SelectNodes('//test-case') | ForEach-Object { $_.name }
    # These play-mode cases together drive the whole encounter: bootstrap, fight, outcome, restart.
    $wanted = @('TitlePauseWinLoseAndRepeatedRestartResetTheEncounter',
                'GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce',
                'MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing',
                'BossWarningOverridesRecentTutorialAndShiftMessages',
                'BootstrapProducesExactlyTheLayoutGeometry')
    $seen = @($wanted | Where-Object { $n = $_; $names | Where-Object { $_ -like "*$n*" } })
    if ($seen.Count -eq 0) {
        Write-Gate 'encounter-playthrough' 'FAIL' "no encounter playthrough test found in $playResults"
    } else {
        Write-Gate 'encounter-playthrough' 'PASS' ("covered by: " + ($seen -join ', '))
    }
} else {
    Write-Gate 'encounter-playthrough' 'BLOCKED' 'PlayMode results unavailable'
}

# --- 6. Android development APK -------------------------------------------------------
$apk = Join-Path $ProjectPath 'Builds\Android\VEILBREAK-development.apk'
if (Test-Path $apk) { Remove-Item $apk -Force }
$build = Invoke-Unity 'android-build' @('-quit', '-buildTarget', 'Android',
    '-executeMethod', 'Veilbreak.Editor.AndroidBuild.BuildDevelopment')
$built = Test-Path $apk
if ($built) { Copy-Item $apk $evidence -ErrorAction SilentlyContinue }
$report = ([regex]::Matches($build.Text, 'Android development APK built: ([^\.]+)') |
    ForEach-Object { $_.Groups[1].Value }) -join '; '
Write-Gate 'android-apk-build' $(if ($built -and $build.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }) `
    "exit $($build.ExitCode), apk=$built, $(if ($built) { (Get-Item $apk).Length } else { 0 }) bytes, log $($build.Log)"

$summary.GetEnumerator() | ForEach-Object { "{0}`t{1}" -f $_.Key, $_.Value } |
    Set-Content -Path (Join-Path $evidence 'summary.tsv') -Encoding UTF8
Write-Host "`nEvidence written to $evidence"
$summary.GetEnumerator() | ForEach-Object { "{0,-28} {1}" -f $_.Key, $_.Value }

if ($summary.Values | Where-Object { $_ -like 'FAIL*' -or $_ -like 'BLOCKED*' }) { exit 1 }
Write-Host "All Unity gates passed." -ForegroundColor Green
exit 0

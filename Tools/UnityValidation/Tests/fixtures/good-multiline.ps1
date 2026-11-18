param(
    [string]$ProjectPath,
    [switch]$KeepGoing
)

function Get-State {
    param([string]$State)
    # Multi-line switch: the form the acceptance scripts actually use.
    $colour = switch ($State) {
        'PASS' { 'Green' }
        'BLOCKED' { 'Yellow' }
        default { 'Red' }
    }
    return $colour
}

foreach ($row in @('PASS', 'FAIL')) {
    Write-Host ("{0} -> {1}" -f $row, (Get-State -State $row))
}

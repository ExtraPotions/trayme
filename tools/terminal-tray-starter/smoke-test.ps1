param([string]$Fixture = 'mintty.exe', [switch]$Minimized, [switch]$StopHelper)
$ErrorActionPreference = 'Stop'
$folder = Join-Path $PSScriptRoot ('smoke/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$fixtureProcess = Start-Process -FilePath (Join-Path $PSScriptRoot ('bin/' + $Fixture)) -ArgumentList ('"' + $folder + '"') -WindowStyle $(if ($Minimized) { 'Minimized' } else { 'Normal' }) -PassThru
function Read-State {
    $path = Join-Path $folder 'state.txt'
    try { if (Test-Path $path) { return [IO.File]::ReadAllText($path) } } catch [IO.IOException] { }
    return ''
}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    do { Start-Sleep -Milliseconds 100; $state = Read-State } until ($state.StartsWith('False,') -or [DateTime]::UtcNow -gt $deadline)
    if (-not $state.StartsWith('False,')) { throw "New fixture was not hidden in Trayme: $state" }
    if ($StopHelper) { & (Join-Path $PSScriptRoot 'bin/TerminalTrayStarter.exe') --stop }
    else { [IO.File]::WriteAllText((Join-Path $folder 'restore'), '') }
    $deadline = [DateTime]::UtcNow.AddSeconds(4)
    do { Start-Sleep -Milliseconds 100; $state = Read-State } until ($state -eq 'True,False' -or [DateTime]::UtcNow -gt $deadline)
    if ($state -ne 'True,False') { throw "Fixture failed to restore: $state" }
    $deadline = [DateTime]::UtcNow.AddSeconds(2)
    do {
        Start-Sleep -Milliseconds 100
        if ((Read-State) -ne 'True,False') { throw 'Restored fixture was hidden again' }
    } until ([DateTime]::UtcNow -gt $deadline)
    'PASS: new window hidden in tray; restored window stayed visible.'
} finally {
    [IO.File]::WriteAllText((Join-Path $folder 'stop'), '')
    if (-not $fixtureProcess.WaitForExit(5000)) { $fixtureProcess.Kill(); $fixtureProcess.WaitForExit(); throw 'Fixture required forced cleanup' }
    $fixtureProcess.Dispose()
}

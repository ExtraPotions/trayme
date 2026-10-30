$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'bin/TerminalTrayStarter.exe'
if (-not (Test-Path $source)) { throw 'Build the helper first.' }
$destination = Join-Path $env:LOCALAPPDATA 'Programs/TerminalTrayStarter'
$exe = Join-Path $destination 'TerminalTrayStarter.exe'
$running = @(Get-Process TerminalTrayStarter -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    & $source --stop
    foreach ($process in $running) {
        if (-not $process.WaitForExit(5000)) { throw 'Existing helper did not exit; installation stopped.' }
    }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $exe -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $destination 'README.md') -Force
if ((Get-FileHash $source).Hash -ne (Get-FileHash $exe).Hash) { throw 'Installed executable differs from tested build.' }
$runKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
New-ItemProperty -Path $runKey -Name 'TerminalTrayStarter' -Value ('"' + $exe + '"') -PropertyType String -Force | Out-Null
Start-Process -FilePath $exe -WindowStyle Hidden
Get-FileHash $exe -Algorithm SHA256 | Format-List Path,Hash

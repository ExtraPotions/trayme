$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$source = Join-Path $PSScriptRoot 'TerminalTrayStarter.cs'
$references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll')
& $compiler /nologo /target:winexe /optimize+ @references "/out:$out/TerminalTrayStarter.exe" $source
if ($LASTEXITCODE -ne 0) { throw 'Helper build failed' }
& $compiler /nologo /target:exe /main:Tests @references "/out:$out/Tests.exe" $source (Join-Path $PSScriptRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& "$out/Tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
& $compiler /nologo /target:winexe @references "/out:$out/mintty.exe" (Join-Path $PSScriptRoot 'TrayFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed' }
& $compiler /nologo /target:winexe "/out:$out/ConsoleFixture.exe" (Join-Path $PSScriptRoot 'ConsoleFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Console fixture build failed' }

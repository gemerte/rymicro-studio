param(
    [switch]$DebugConsole,
    [switch]$Run,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fw   = Split-Path $csc

if (-not (Test-Path $csc)) { throw "C# compiler not found: $csc" }

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Management.dll',
    'System.Web.Extensions.dll'
) | ForEach-Object { '/r:"' + (Join-Path $fw $_) + '"' }

$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
$out = Join-Path $root 'RYMICRO Studio.exe'
$target = 'winexe'
if ($DebugConsole) { $target = 'exe' }

Write-Host "Building RYMICRO Studio..." -ForegroundColor Cyan
$compilerArgs = @('/nologo', "/target:$target", '/platform:anycpu', '/optimize+', '/codepage:65001', '/warn:4', "/out:`"$out`"") + $refs + $src
& $csc $compilerArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}

Write-Host "OK: $out" -ForegroundColor Green

if ($Run) {
    $exeArgs = @()
    if ($NoOpen) { $exeArgs += '--silent' }
    Start-Process -FilePath $out -ArgumentList $exeArgs -WorkingDirectory $root
}

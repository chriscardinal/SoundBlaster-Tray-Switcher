#requires -Version 5
[CmdletBinding()]
param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $here 'bin'
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

# Prefer the Roslyn csc.exe from VS BuildTools (modern C# syntax). Fall back to the
# framework's csc.exe (C# 5 max) if Roslyn isn't installed.
$cscCandidates = @(
    'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "No csc.exe found. Tried: $($cscCandidates -join '; ')" }

$sources = Get-ChildItem -Path $here -Filter *.cs | Select-Object -ExpandProperty FullName

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll'
)

function Compile($targetKind, $outPath, $defineExtra) {
    $cscArgs = @(
        '/nologo',
        "/target:$targetKind",
        '/platform:x86',
        '/optimize+',
        "/out:$outPath"
    )
    if ($defineExtra) { $cscArgs += "/define:$defineExtra" }
    foreach ($r in $refs) { $cscArgs += "/reference:$r" }
    $cscArgs += $sources
    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc.exe exited with $LASTEXITCODE for $outPath" }
}

$exeGui = Join-Path $out 'SBQuickSwitch.exe'
$exeCli = Join-Path $out 'SBQuickSwitchCli.exe'

Write-Host "Compiling SBQuickSwitch.exe (winexe, x86)..." -ForegroundColor Cyan
Compile 'winexe' $exeGui $null
Write-Host "Built: $exeGui" -ForegroundColor Green

Write-Host "Compiling SBQuickSwitchCli.exe (console, x86)..." -ForegroundColor Cyan
Compile 'exe' $exeCli 'CONSOLE_SUBSYSTEM'
Write-Host "Built: $exeCli" -ForegroundColor Green

if ($Run) {
    Write-Host "Launching tray..." -ForegroundColor Cyan
    Start-Process -FilePath $exeGui
}

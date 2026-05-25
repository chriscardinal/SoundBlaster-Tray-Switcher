# Quick smoke test that runs our app's COM code without the tray UI.
# Must launch as a 32-bit PowerShell because the COM server is x86-only.
[CmdletBinding()]
param([switch]$Toggle)

# Re-launch in 32-bit PowerShell if we're not already there
if ([Environment]::Is64BitProcess) {
    $ps32 = "$env:SystemRoot\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($Toggle) { $argList += '-Toggle' }
    & $ps32 @argList
    exit $LASTEXITCODE
}

$bits = if ([Environment]::Is64BitProcess) { '64-bit' } else { '32-bit' }
"Running in: $bits PowerShell"

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe  = Join-Path $here 'bin\SBQuickSwitch.exe'
if (-not (Test-Path $exe)) { throw "Build first: $exe missing" }

# Load our compiled assembly so we can reuse its types
[void][Reflection.Assembly]::LoadFrom($exe)
Add-Type -AssemblyName System.Windows.Forms  # ensure WinForms is loaded for the deps

$mode = [Enum]::Parse([SBQuickSwitch.OutputMode], 'Unknown')

$ctrl = New-Object SBQuickSwitch.AE7Controller
try {
    Write-Host "Enumerating audio endpoints..." -ForegroundColor Cyan
    $endpoints = [SBQuickSwitch.Native]::EnumerateRenderEndpoints()
    foreach ($e in $endpoints) {
        "  [$($e.Id)]  $($e.FriendlyName)"
    }
    ""
    Write-Host "Binding to AE-7..." -ForegroundColor Cyan
    $ctrl.BindToAE7()
    "  Endpoint: $($ctrl.EndpointName)"
    "  ID: $($ctrl.EndpointId)"

    Write-Host "`nReading current MultiplexOutput..." -ForegroundColor Cyan
    $cur = $ctrl.GetMultiplexOutput()
    $mode = $ctrl.GetMode()
    "  Raw value: $cur"
    "  Mapped: $mode"

    if ($Toggle) {
        Write-Host "`nToggling..." -ForegroundColor Yellow
        $newMode = $ctrl.Toggle()
        "  New mode: $newMode  (raw $($ctrl.GetMultiplexOutput()))"
    }
}
finally {
    $ctrl.Dispose()
}

# Generates lib\Interop.BMDSwitcherAPI.dll from the ATEM Switchers SDK DLL
# installed with ATEM Software Control. Requires TlbImp (Windows SDK / .NET Framework 4.8 Developer Pack).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$api = Get-ChildItem "${env:ProgramFiles(x86)}\Blackmagic Design", "$env:ProgramFiles\Blackmagic Design" `
    -Recurse -Filter BMDSwitcherAPI64.dll -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $api) { throw "BMDSwitcherAPI64.dll not found. Install ATEM Software Control (ATEM Switchers)." }

$tlbimp = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows" -Recurse -Filter TlbImp.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $tlbimp) { throw "TlbImp.exe not found. Install the .NET Framework 4.8 Developer Pack or Visual Studio." }

New-Item -ItemType Directory -Force "$root\lib" | Out-Null
& $tlbimp.FullName $api.FullName /out:"$root\lib\Interop.BMDSwitcherAPI.dll" /namespace:BMDSwitcherAPI /machine:X64 /nologo

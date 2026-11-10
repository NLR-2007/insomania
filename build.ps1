<#
    Insominia build script.

    Needs nothing installed - it uses the C# compiler that ships with Windows
    (.NET Framework 4.x) and produces a single self-contained .exe that runs on
    any Windows 10/11 machine with no runtime download.

    Usage:
        .\build.ps1            # build the app
        .\build.ps1 -Diag      # also build the console diagnostics tool
        .\build.ps1 -Run       # build, then launch the app
#>
[CmdletBinding()]
param(
    [switch]$Diag,
    [switch]$Run,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$Root     = $PSScriptRoot
$SrcDir   = Join-Path $Root 'src'
$BuildDir = Join-Path $Root 'build'
$Fw       = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$Csc      = Join-Path $Fw 'csc.exe'

if (-not (Test-Path $Csc)) {
    throw "C# compiler not found at $Csc. This machine is missing .NET Framework 4.x."
}

if ($Clean) {
    if (Test-Path $BuildDir) { Remove-Item $BuildDir -Recurse -Force }
    Write-Host "Cleaned." -ForegroundColor Yellow
    return
}

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

$Sources = @(
    'Program.cs'
    'TrayApp.cs'
    'TrayIcons.cs'
    'KeepAwake.cs'
    'LidPolicy.cs'
    'ElevatedTask.cs'
    'SafetyMonitor.cs'
    'AppSettings.cs'
    'StartupManager.cs'
    'ProcessThrottler.cs'
    'AppPickerForm.cs'
) | ForEach-Object { Join-Path $SrcDir $_ }

$missing = $Sources | Where-Object { -not (Test-Path $_) }
if ($missing) { throw "Missing source files:`n$($missing -join "`n")" }

$Refs = @(
    'System.dll'
    'System.Core.dll'
    'System.Drawing.dll'
    'System.Windows.Forms.dll'
    'System.Management.dll'
) | ForEach-Object { "-reference:$(Join-Path $Fw $_)" }

function Invoke-Csc {
    # NOTE: do not call the parameter $Args - that name collides with
    # PowerShell's automatic $args variable and silently arrives empty.
    param([string]$Name, [string[]]$CscArgs)

    $rsp = Join-Path $BuildDir "$Name.rsp"
    # csc.exe cannot read a response file that starts with a BOM, so write it without one.
    [System.IO.File]::WriteAllLines($rsp, $CscArgs, (New-Object System.Text.UTF8Encoding($false)))
    & $Csc "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw "$Name build failed (exit $LASTEXITCODE)." }
}

# ---- main app -----------------------------------------------------------
$exe = Join-Path $BuildDir 'Insominia.exe'

# A running instance holds a lock on the .exe, so close it first.
$live = Get-Process -Name 'Insominia' -ErrorAction SilentlyContinue
if ($live) {
    Write-Host "Closing the running instance ..." -ForegroundColor Yellow
    $live | Stop-Process -Force
    Start-Sleep -Milliseconds 700
}

Write-Host "Building Insominia.exe ..." -ForegroundColor Cyan

Invoke-Csc -Name 'insominia' -CscArgs (@(
    '-nologo'
    '-optimize+'
    '-warn:4'
    '-target:winexe'
    "-out:$exe"
    "-win32manifest:$(Join-Path $SrcDir 'app.manifest')"
) + $Refs + $Sources)

$size = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host "  OK  $exe  ($size KB)" -ForegroundColor Green

# ---- diagnostics tool ---------------------------------------------------
if ($Diag) {
    $diagExe = Join-Path $BuildDir 'Insominia.Diag.exe'
    $diagSrc = Join-Path $Root 'tools\diag\Diag.cs'
    Write-Host "Building Insominia.Diag.exe ..." -ForegroundColor Cyan

    # The diagnostics tool is a console app, so leave out the UI pieces.
    $libs = $Sources | Where-Object { $_ -notmatch '(Program|TrayApp|TrayIcons|AppPickerForm)\.cs$' }

    Invoke-Csc -Name 'diag' -CscArgs (@(
        '-nologo'
        '-target:exe'
        "-out:$diagExe"
    ) + $Refs + @($diagSrc) + $libs)

    Write-Host "  OK  $diagExe" -ForegroundColor Green
}

if ($Run) {
    Write-Host "Launching ..." -ForegroundColor Cyan
    Start-Process $exe
    Write-Host "Look for the Insominia icon near the clock." -ForegroundColor Green
}

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host '=== 1. git pull ==='
git pull
if ($LASTEXITCODE -ne 0) {
    Write-Host 'git pull gagal.' -ForegroundColor Red
    Read-Host 'Tekan Enter untuk keluar'
    exit 1
}

Write-Host ''
Write-Host '=== 2. Python TV service (tab baru) ==='
$pythonDir = Join-Path $PSScriptRoot 'python'
$pyCommand = '.\venv\Scripts\Activate.ps1; python tv_service.py'

if (Get-Command wt -ErrorAction SilentlyContinue) {
    # Tab baru di Windows Terminal yang sama (sebelah tab aktif)
    wt -w 0 new-tab --title 'TV Service' -d $pythonDir powershell -NoExit -Command $pyCommand
} else {
    Write-Host 'Windows Terminal (wt) tidak ditemukan; membuka jendela PowerShell baru.' -ForegroundColor Yellow
    Start-Process powershell -WorkingDirectory $pythonDir -ArgumentList @(
        '-NoExit',
        '-Command',
        $pyCommand
    )
}

Write-Host ''
Write-Host '=== 3. Aplikasi desktop (.NET) ==='
Set-Location (Join-Path $PSScriptRoot 'app')
dotnet run -- --verbose

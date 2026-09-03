# Build script for Orbit v0.1.1 Setup Installer
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building Orbit v0.1.1 Release & Setup " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Publish self-contained single-file executable
Write-Host "`n[1/2] Publishing self-contained win-x64 executable..." -ForegroundColor Yellow
dotnet publish src\Orbit\Orbit.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[ERROR] dotnet publish failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n[SUCCESS] Binary published to: publish\win-x64\Orbit.exe" -ForegroundColor Green

# 2. Check for Inno Setup compiler
Write-Host "`n[2/2] Compiling Inno Setup installer..." -ForegroundColor Yellow
$iscc = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
        "${env:ProgramFiles}\Inno Setup 6\iscc.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\iscc.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}

if ($iscc) {
    & $iscc installer\Orbit.iss
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n[SUCCESS] Setup installer generated at: installer\output\OrbitSetup-v0.1.1.exe" -ForegroundColor Green
    } else {
        Write-Host "`n[ERROR] Inno Setup compilation failed." -ForegroundColor Red
    }
} else {
    Write-Host "`n[NOTE] Inno Setup compiler ('iscc') is not installed yet." -ForegroundColor Yellow
    Write-Host "To compile the setup .exe, install Inno Setup once via winget:" -ForegroundColor White
    Write-Host "  winget install JRSoftware.InnoSetup" -ForegroundColor Cyan
    Write-Host "After installing, run:" -ForegroundColor White
    Write-Host "  iscc installer\Orbit.iss" -ForegroundColor Cyan
}

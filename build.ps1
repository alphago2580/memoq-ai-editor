# MemoQ AI Sidecar Build Script
# PowerShell script to build all components

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MemoQ AI Sidecar - Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Build Frontend
Write-Host "[1/3] Building Frontend (CodeMirror 6)..." -ForegroundColor Yellow
Push-Location "Frontend"

if (-not (Test-Path "node_modules")) {
    Write-Host "Installing npm packages..." -ForegroundColor Gray
    npm install
}

Write-Host "Bundling JavaScript..." -ForegroundColor Gray
npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Frontend build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location
Write-Host "Frontend build completed." -ForegroundColor Green
Write-Host ""

# 2. Build Sidecar App
Write-Host "[2/3] Building Sidecar WPF Application..." -ForegroundColor Yellow
Push-Location "SidecarApp"

# Create Frontend output directory
$frontendDist = "bin\Debug\net6.0-windows\Frontend"
if (-not (Test-Path $frontendDist)) {
    New-Item -ItemType Directory -Path $frontendDist -Force | Out-Null
}

# Copy frontend files
Write-Host "Copying frontend files..." -ForegroundColor Gray
Copy-Item "..\Frontend\index.html" $frontendDist -Force
Copy-Item "..\Frontend\style.css" $frontendDist -Force
Copy-Item "..\Frontend\editor.bundle.js" $frontendDist -Force -ErrorAction SilentlyContinue

# Build WPF app
Write-Host "Building WPF application..." -ForegroundColor Gray
dotnet build -c Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "Sidecar build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location
Write-Host "Sidecar build completed." -ForegroundColor Green
Write-Host ""

# 3. Build MemoQ Plugin
Write-Host "[3/3] Building MemoQ Plugin..." -ForegroundColor Yellow
Push-Location "MemoQPlugin"

# Check if SDK DLLs exist
if (-not (Test-Path "lib\MemoQ.PreviewInterfaces.dll")) {
    Write-Host "WARNING: MemoQ SDK DLLs not found in 'lib' folder!" -ForegroundColor Red
    Write-Host "Please copy the following DLLs from MemoQ installation:" -ForegroundColor Yellow
    Write-Host "  - MemoQ.PreviewInterfaces.dll" -ForegroundColor Yellow
    Write-Host "  - MemoQ.Addins.Common.dll" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Skipping plugin build..." -ForegroundColor Gray
} else {
    Write-Host "Building plugin DLL..." -ForegroundColor Gray
    dotnet build -c Debug

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Plugin build failed!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Write-Host "Plugin build completed." -ForegroundColor Green
}

Pop-Location
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Frontend: " -NoNewline
Write-Host "READY" -ForegroundColor Green
Write-Host "Sidecar:  " -NoNewline
Write-Host "READY" -ForegroundColor Green
Write-Host "Plugin:   " -NoNewline
if (Test-Path "MemoQPlugin\lib\MemoQ.PreviewInterfaces.dll") {
    Write-Host "READY" -ForegroundColor Green
} else {
    Write-Host "NEEDS SDK" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run Sidecar: .\SidecarApp\bin\Debug\net6.0-windows\SidecarApp.exe" -ForegroundColor Gray
Write-Host "2. Install Plugin DLL in MemoQ (if built)" -ForegroundColor Gray
Write-Host "3. Start Ollama: ollama serve" -ForegroundColor Gray
Write-Host ""

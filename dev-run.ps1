# dev-run.ps1 — Kill previous dev instance, build Debug, launch with --dev
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
$sln     = "$PSScriptRoot\DisplayProfileManager.sln"
$exe     = "$PSScriptRoot\bin\Debug\DisplayProfileManager.exe"

# Kill any dev instance (--dev arg) without touching the installed version
Get-Process -Name "DisplayProfileManager" -ErrorAction SilentlyContinue | ForEach-Object {
    $cmdline = (Get-WmiObject Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine
    if ($cmdline -like "*--dev*") {
        Write-Host "Killing previous dev instance (PID $($_.Id))..." -ForegroundColor Yellow
        $_ | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }
}

Write-Host "Building Debug..." -ForegroundColor Cyan
& $msbuild $sln /p:Configuration=Debug /v:minimal
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }

Write-Host "Launching dev instance..." -ForegroundColor Cyan
Start-Process -FilePath $exe -ArgumentList "--dev"

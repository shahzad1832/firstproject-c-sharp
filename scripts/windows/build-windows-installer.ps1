param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$root = Resolve-Path "$PSScriptRoot\..\.."
$installerScript = Join-Path $root "installer\firstProject.iss"

if (-not (Get-Command iscc.exe -ErrorAction SilentlyContinue)) {
    Write-Error "Inno Setup compiler (iscc.exe) not found. Install Inno Setup and ensure iscc.exe is in PATH."
    exit 1
}

Write-Host "Publishing Windows build..."
dotnet publish -c $Configuration -r $Runtime --self-contained true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building installer..."
& iscc.exe $installerScript
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Installer created in: $root\bin\Release\installer"

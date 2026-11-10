$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'KsefPrinter\KsefPrinter.csproj'
$publishDir = Join-Path $root 'publish\KsefPrinter'
$installerScript = Join-Path $root 'installer\KsefPrinter.iss'

[xml]$csproj = Get-Content -LiteralPath $project
$version = [string]$csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Nieprawidlowa wersja w KsefPrinter.csproj: '$version'"
}

Write-Host "Publikowanie KsefPrinter $version (win-x64, bez .NET Runtime)..."
dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish nie powiodl sie.' }

# Find Inno Setup installed for all users or only for the current user.
$programRoots = @(
    $env:ProgramFiles,
    [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'),
    $env:LOCALAPPDATA
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

$isccCandidates = @()
foreach ($base in $programRoots) {
    foreach ($name in @('Inno Setup 7', 'Inno Setup 6', 'Programs\Inno Setup 7', 'Programs\Inno Setup 6')) {
        $isccCandidates += Join-Path $base "$name\ISCC.exe"
    }
}

# The installer may register a custom destination.
foreach ($regPath in @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
)) {
    $install = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
    if ($install -and $install.InstallLocation) {
        $isccCandidates += Join-Path $install.InstallLocation 'ISCC.exe'
    }
}

$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    throw 'Nie znaleziono ISCC.exe. Podaj lokalizacje pliku ISCC.exe z katalogu Inno Setup.'
}

Write-Host "Tworzenie instalatora Inno Setup..."
& $iscc "/DMyAppVersion=$version" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja instalatora nie powiodla sie.' }

$output = Join-Path $root "dist\KsefPrinter-$version-Setup.exe"
if (-not (Test-Path -LiteralPath $output)) { throw "Nie znaleziono instalatora: $output" }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Host "Gotowe: $output"
Write-Host "SHA-256: $hash"
Write-Host 'Przed publikacja przetestuj instalacje i aktualizacje na Windows.'

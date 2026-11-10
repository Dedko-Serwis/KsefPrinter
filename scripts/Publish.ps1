$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'KsefPrinter\KsefPrinter.csproj'
$publishDir = Join-Path $root 'publish\KsefPrinter'
$installerScript = Join-Path $root 'installer\KsefPrinter.iss'

[xml]$csproj = Get-Content -LiteralPath $project
$version = [string]$csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Nieprawidłowa wersja w KsefPrinter.csproj: '$version'"
}

Write-Host "Publikowanie KsefPrinter $version (win-x64, bez .NET Runtime)..."
dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish nie powiódł się.' }

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    throw 'Nie znaleziono ISCC.exe. Zainstaluj Inno Setup lub dodaj katalog Inno Setup do PATH.'
}

Write-Host "Tworzenie instalatora Inno Setup..."
& $iscc "/DMyAppVersion=$version" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja instalatora nie powiodła się.' }

$output = Join-Path $root "dist\KsefPrinter-$version-Setup.exe"
if (-not (Test-Path -LiteralPath $output)) { throw "Nie znaleziono instalatora: $output" }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Host "Gotowe: $output"
Write-Host "SHA-256: $hash"
Write-Host 'Przed publikacją przetestuj instalację i aktualizację na Windows.'

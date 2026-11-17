# Jednolity punkt wejscia do budowania wydan KsefPrinter.
# Wersja jest odczytywana z KsefPrinter.csproj.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'Publish.ps1')
if (-not $?) { throw 'Budowanie wydania nie powiodlo sie.' }

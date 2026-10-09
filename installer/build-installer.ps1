# build-installer.ps1 — Fabrique l'installateur PlumeSetup-X.Y.Z.exe
#
# Prérequis poste de build : Inno Setup 6 (https://jrsoftware.org/isinfo.php) — `iscc` dans le
# PATH, ou installé au chemin standard "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (install admin),
# ou au chemin par utilisateur "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (install sans admin).
#
# Étapes : lit <Version> du csproj (source unique de vérité) → dotnet publish (Release,
# single-file, framework-dependent : la même commande que deploy.ps1) → iscc plume.iss →
# installer\output\PlumeSetup-X.Y.Z.exe. deploy.ps1 reste la boucle courte de dev.
#
# Usage :  pwsh ./installer/build-installer.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# 1) Version depuis le csproj.
$csproj = Join-Path $root 'src\Plume\Plume.csproj'
$match = Select-String -Path $csproj -Pattern '<Version>([0-9.]+)</Version>'
if (-not $match) { throw "Version introuvable dans $csproj" }
$version = $match.Matches[0].Groups[1].Value
Write-Host "Version : $version" -ForegroundColor Cyan

# 2) Publier (Release, single-file, framework-dependent — l'exe reste ~1 Mo).
# Dossier de publication supprimé avant coup : une publication incrémentale par-dessus un dossier
# existant ne recopie pas toujours Resources\ (constaté en pratique), laissant un dossier vide que
# l'installateur refuserait de compiler (aucun fichier ne correspond au motif Resources\*).
$publishDir = Join-Path $root 'src\Plume\bin\Release\net10.0-windows\win-x64\publish'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Write-Host 'Publication…' -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\Plume') -c Release -r win-x64 `
    -p:PublishSingleFile=true --self-contained false --nologo
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication (code $LASTEXITCODE)." }

# 3) Compiler l'installateur.
$iscc = $null
$cmd = Get-Command iscc -ErrorAction SilentlyContinue
if ($cmd) { $iscc = $cmd.Source }
elseif (Test-Path "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") {
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}
elseif (Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") {
    $iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
}
if (-not $iscc) { throw 'Inno Setup 6 introuvable : installe-le depuis https://jrsoftware.org/isinfo.php' }

& $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'plume.iss')
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation Inno Setup (code $LASTEXITCODE)." }

Write-Host "OK : installer\output\PlumeSetup-$version.exe" -ForegroundColor Green

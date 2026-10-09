# deploy.ps1 — Publie Plume, l'installe hors OneDrive et le relance.
#
# Étapes : publish (Release, single-file) → arrêt du Plume en cours →
# copie vers %LOCALAPPDATA%\Plume → relance depuis ce dossier.
#
# Pourquoi %LOCALAPPDATA% : l'exe runtime ne doit jamais vivre dans OneDrive
# (sync = « Démarrer avec Windows » instable). Voir docs/roadmap.md.
#
# Usage :  pwsh ./deploy.ps1        (ou clic droit → Exécuter avec PowerShell)
#          ./deploy.ps1 -NoLaunch   pour ne pas relancer l'app à la fin

param([switch]$NoLaunch)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'src\Plume\bin\Release\net10.0-windows\win-x64\publish'
$publishExe = Join-Path $publishDir 'Plume.exe'
$destDir = Join-Path $env:LOCALAPPDATA 'Plume'
$destExe = Join-Path $destDir 'Plume.exe'

function Stop-Plume {
    $procs = Get-Process Plume -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "Arrêt de Plume en cours…" -ForegroundColor Yellow
        $procs | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }
}

# 1) Arrêter l'app avant de publier (sinon l'exe du dossier publish est verrouillé).
Stop-Plume

# 2) Publier.
Write-Host "Publication (Release, single-file)…" -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\Plume') -c Release -r win-x64 `
    -p:PublishSingleFile=true --self-contained false --nologo
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication (code $LASTEXITCODE)." }
if (-not (Test-Path $publishExe)) { throw "Exe introuvable : $publishExe" }

# 3) Copier vers %LOCALAPPDATA%\Plume (re-stop par sécurité au cas où l'app aurait redémarré).
Stop-Plume
if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }
Copy-Item $publishExe $destExe -Force

# Ressources (logo pastille/fenêtres + icône) : requises à côté de l'exe (cf. AppLogo.cs).
# Copier le CONTENU (pas le dossier) : sinon Copy-Item imbrique Resources\Resources quand la cible existe.
$publishRes = Join-Path $publishDir 'Resources'
if (Test-Path $publishRes) {
    $destRes = Join-Path $destDir 'Resources'
    if (-not (Test-Path $destRes)) { New-Item -ItemType Directory -Path $destRes | Out-Null }
    Copy-Item (Join-Path $publishRes '*') $destRes -Recurse -Force
}

$version = (Get-Item $destExe).VersionInfo.ProductVersion
Write-Host "Installé : $destExe (version $version)" -ForegroundColor Green

# 4) Relancer (sauf -NoLaunch).
if (-not $NoLaunch) {
    Start-Process $destExe
    Write-Host "Plume relancé." -ForegroundColor Green
} else {
    Write-Host "Relance ignorée (-NoLaunch)." -ForegroundColor DarkGray
}

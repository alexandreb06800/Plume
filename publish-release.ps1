# publish-release.ps1 — Publie la version courante sur GitHub (Release + archive + empreinte).
#
# C'est ce que lit le bouton « Vérifier les mises à jour » de l'onglet À propos (Services/UpdateService.cs) :
# la dernière Release du dépôt, son archive « Plume-X.Y.Z.zip » et la ligne « sha256: … » de son texte.
#
# Étapes : lit <Version> du csproj → vérifie que le dépôt est propre et poussé → publish (Release,
# single-file) → archive (Plume.exe + Resources) → empreinte SHA-256 → Release « vX.Y.Z » + envoi de l'archive.
#
# Jeton : la connexion Git déjà enregistrée sur le poste (git credential fill) — rien à configurer.
#
# Usage :  ./publish-release.ps1            (à lancer après le git push de la version)
#          ./publish-release.ps1 -Notes "…" texte affiché dans la Release (défaut : titre du dernier commit)

param([string]$Notes)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'alexandreb06800/Plume'   # doit rester identique à UpdateService.Repository

[xml]$csproj = Get-Content (Join-Path $root 'src\Plume\Plume.csproj') -Encoding UTF8
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw "Version introuvable dans Plume.csproj." }
$tag = "v$version"

# 1) Le dépôt doit être propre et poussé : la Release pointe sur un commit que GitHub connaît.
Push-Location $root
try {
    if (git status --porcelain) { throw "Le dépôt contient des modifications non commitées." }
    $branch = git rev-parse --abbrev-ref HEAD
    git fetch -q origin
    $head = git rev-parse HEAD
    if ($head -ne (git rev-parse "origin/$branch")) { throw "La branche $branch n'est pas poussée : lance git push d'abord." }
    if (-not $Notes) { $Notes = git log -1 --format=%s }
} finally { Pop-Location }

# 2) Publier l'exe.
$publishDir = Join-Path $root 'src\Plume\bin\Release\net10.0-windows\win-x64\publish'
Write-Host "Publication de Plume $version…" -ForegroundColor Cyan
# Dossier vidé d'abord : un second publish consécutif laisse « Resources » vide (constaté en 5.4.12),
# et l'archive partirait alors sans le logo ni l'icône.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root 'src\Plume') -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Échec de la publication (code $LASTEXITCODE)." }

# 3) Archive : Plume.exe + Resources (logo, icône), comme deploy.ps1 les installe.
$stage = Join-Path $env:TEMP "plume-release-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $publishDir 'Plume.exe') $stage
Copy-Item (Join-Path $publishDir 'Resources') (Join-Path $stage 'Resources') -Recurse
$zip = Join-Path $env:TEMP "Plume-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ("Archive : {0} ({1:N0} Ko)  sha256 {2}" -f $zip, ((Get-Item $zip).Length / 1KB), $sha)

# 4) Jeton : connexion Git du poste (jamais affiché).
$cred = "protocol=https`nhost=github.com`n`n" | git credential fill
$token = ($cred | Where-Object { $_ -like 'password=*' } | Select-Object -First 1) -replace '^password=', ''
if (-not $token) { throw "Aucune connexion Git enregistrée pour github.com." }
$headers = @{ Authorization = "Bearer $token"; 'User-Agent' = 'Plume-publish'; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }

# 5) Release (refus si elle existe déjà : une version publiée ne se réécrit pas).
$existing = $null
try { $existing = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers } catch { }
if ($existing) { throw "La Release $tag existe déjà. Incrémente <Version> avant de republier." }

$body = @{ tag_name = $tag; target_commitish = $head; name = "Plume $version"; body = "$Notes`n`nsha256: $sha" } | ConvertTo-Json
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers `
    -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))

$upload = "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=Plume-$version.zip"
$asset = Invoke-RestMethod $upload -Method Post -Headers $headers -ContentType 'application/zip' -InFile $zip

Remove-Item $stage -Recurse -Force
Write-Host "Release $tag publiée : $($release.html_url)" -ForegroundColor Green
Write-Host "Archive jointe : $($asset.name) ($($asset.size) octets)" -ForegroundColor Green

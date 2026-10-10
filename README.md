# Plume

Utilitaire **Windows** de **dictée vocale**. Appuie sur ton **raccourci** (défaut **Alt + Espace**), parle en français, et le texte **nettoyé** s'insère là où est ton curseur — dans n'importe quelle application (Word, Outlook, navigateur, Slack, Jira…).

Léger, rapide, design minimal : une pastille flottante, un menu dans la barre des tâches, une fenêtre de réglages. Rien d'autre.

---

## Installation

**Ce qu'il te faut**

- Windows 10 ou 11 (64 bits) et un micro.
- Le **runtime .NET 10 Desktop** (gratuit, Microsoft) : https://dotnet.microsoft.com/download/dotnet/10.0 → colonne « Run desktop apps », **x64**. Sans lui, Plume ne démarre pas.
- Une **clé Groq** (gratuite) : https://console.groq.com/keys. Elle sert à la transcription et au nettoyage du texte.

**Étapes**

1. Ouvre la page **[Releases](../../releases/latest)** du dépôt et télécharge `Plume-X.Y.Z.zip`. Le dépôt est privé : il faut être connecté à GitHub avec un compte qui y a accès.
2. Dézippe l'archive dans **`%LOCALAPPDATA%\Plume`** (colle ce chemin dans la barre d'adresse de l'Explorateur). `Plume.exe` et le dossier `Resources` doivent rester côte à côte. N'installe pas Plume dans OneDrive : le démarrage avec Windows y devient instable.
3. Lance **`Plume.exe`**. Windows affiche « Windows a protégé votre ordinateur », parce que l'exe n'est pas signé : clique sur **Informations complémentaires → Exécuter quand même**.
4. Un **assistant de prise en main en 3 étapes** s'ouvre : colle ta clé Groq, teste ton micro, fais ta première dictée.

Plume s'inscrit d'elle-même au démarrage de Windows et vit dans la zone de notification (près de l'horloge).

> **Vérifier le téléchargement (facultatif).** Chaque Release publie l'empreinte SHA-256 de son archive (ligne `sha256: …`). Dans PowerShell : `Get-FileHash .\Plume-X.Y.Z.zip` doit afficher la même valeur.

> Une clé **OpenRouter** (https://openrouter.ai/keys) est facultative : elle ne sert que si tu choisis un modèle de nettoyage qui n'est pas hébergé par Groq (ex. `openai/gpt-4o-mini`).

## Mise à jour

**Réglages → À propos → « Vérifier les mises à jour »**. Si une version plus récente existe, le bouton devient « Installer la version X » : un clic, et Plume la télécharge, vérifie son empreinte, s'installe et redémarre toute seule. Rien n'est vérifié sans clic, ni au démarrage.

Le dépôt étant privé, Plume a besoin d'un accès à GitHub :

- si Git est installé et déjà connecté à GitHub sur le poste, il n'y a **rien à faire** ;
- sinon, crée un **jeton GitHub en lecture seule** (GitHub → Settings → Developer settings → Fine-grained tokens → accès au dépôt Plume, permission « Contents : Read-only ») et colle-le dans le champ « Jeton GitHub (facultatif) », sous le bouton.

## Sécurité et données

- **Aucune clé n'est fournie avec Plume** : ni dans ce dépôt, ni dans l'exe. Chacun saisit sa propre clé, qui reste sur son poste, **chiffrée** (DPAPI de Windows, liée à la session) dans `%APPDATA%\Plumenfig.json`.
- **Ce qui quitte ton poste** : l'audio dicté part chez Groq pour être transcrit, puis le texte part chez Groq (ou OpenRouter, selon le modèle choisi) pour être nettoyé. Rien d'autre : pas de télémétrie, pas de compte Plume. Détail des flux et des engagements des fournisseurs : [`docs/confidentialite-flux-donnees.md`](docs/confidentialite-flux-donnees.md).
- **Rien n'est conservé** : ni l'audio, ni le texte dicté ne sont écrits sur disque. Le texte dicté est tenu hors de l'historique du presse-papiers Windows (Win+V).
- **L'exe n'est pas signé** (d'où l'avertissement de Windows à la première exécution). L'intégrité de chaque version se vérifie par son empreinte SHA-256, que le bouton de mise à jour contrôle systématiquement.

## Désinstallation

1. Réglages → décoche « Démarrer avec Windows », puis quitte Plume (clic droit sur l'icône → Quitter).
2. Supprime le dossier `%LOCALAPPDATA%\Plume` (le programme).
3. Supprime `%APPDATA%\Plume` si tu veux aussi effacer tes réglages et ta clé.

## Développement

Prérequis : Windows 10/11, [SDK .NET 10](https://dotnet.microsoft.com/download), un micro.

```bash
dotnet run --project src/Plume     # lancer en dév
dotnet test                        # tests (logique pure, xUnit)
./deploy.ps1                       # publier + installer dans %LOCALAPPDATA%\Plume + relancer
./publish-release.ps1              # après le git push : publier la version (Release GitHub + empreinte)
```

Un installateur classique peut aussi être fabriqué avec `installer/build-installer.ps1` (Inno Setup 6 requis) ; il n'est pas publié dans les Releases.

---

## Usage

- **Raccourci** (défaut Alt+Espace) : 1er appui = démarre l'écoute, 2e appui = arrête et insère le texte. **Échap** annule à tout moment.
- **Mode de dictée** (réglages) : *Toggle* (appui/ré-appui) ou *Maintien enfoncé* (push-to-talk).
- **Pastille flottante** : **double-clic** = démarrer/arrêter la dictée sans clavier ; **clic** = menu des **styles de réécriture** sur le texte sélectionné (Corriger, Email pro… — éditables, favoris, modèle IA par style, styles « avec voix » pour dicter une consigne) ; ce menu propose aussi **« Copier le dernier message »**.
- **Assistant vocal** (Ctrl+Alt+A) : pose une question à la voix, réponse dans une fenêtre (multi-tours, Copier / Insérer).
- **Dictionnaire** (réglages) : ton **vocabulaire** (noms propres, jargon — mieux reconnus et préservés) et tes **règles de remplacement** (`raccourci → texte`, multi-ligne pour les signatures). Les commandes dictées « à la ligne » / « nouveau paragraphe » insèrent de vrais sauts de ligne.

## Configuration

Tout est local : `%APPDATA%\Plume\config.json` (**jamais committé**). Les clés API y sont **chiffrées** (DPAPI). Journal de diagnostic opt-in : `%LOCALAPPDATA%\Plume\diag.log` (jamais le texte dicté, jamais les clés).

## Dépannage

| Problème | Solution |
|---|---|
| Alt+Espace perturbe la fenêtre active | Change le raccourci dans les réglages (ex. `Ctrl + Alt + Espace`). |
| « Ce raccourci est déjà utilisé » | Choisis-en un autre ; l'ancien reste actif tant que le nouveau échoue. |
| Le texte ne s'insère pas | Vérifie qu'un champ texte a le focus dans l'app cible. Le dernier texte reste récupérable via le menu Plume → « Copier le dernier texte ». |
| Erreur 401 | Clé API invalide → corrige-la dans les réglages (rouvre la prise en main via À propos si besoin). |
| « Rien entendu » | Aucun son capté ; vérifie le micro choisi dans les réglages (ou le micro par défaut Windows). |

## Documentation

- **`CLAUDE.md`** — cerveau du projet (carte du code, règles, priorités) — lu par Claude Code à chaque session.
- **`docs/roadmap.md`** — versions à venir (point d'entrée unique) ; historique livré dans `docs/roadmap-archive.md`.

> Périmètre : **français uniquement, aucune traduction**, à aucune version.

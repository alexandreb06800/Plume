# Plume

Utilitaire **Windows** de **dictée vocale**. Appuie sur ton **raccourci** (défaut **Alt + Espace**), parle en français, et le texte **nettoyé** s'insère là où est ton curseur — dans n'importe quelle application (Word, Outlook, navigateur, Slack, Jira…).

Léger, rapide, design minimal : une pastille flottante, un menu dans la barre des tâches, une fenêtre de réglages. Rien d'autre.

---

## Installation (collègues)

Lance **`PlumeSetup-X.Y.Z.exe`** : installation par utilisateur, sans droits admin, avec le composant .NET 8 installé automatiquement s'il manque.

Au premier lancement, un **assistant de prise en main en 3 étapes** te guide : coller ta clé **Groq** (gratuite → https://console.groq.com/keys), tester ton micro, réussir ta première dictée. C'est tout.

> Une clé **OpenRouter** (https://openrouter.ai/keys) est optionnelle : elle sert si tu choisis un modèle de nettoyage non-Groq (ex. `openai/gpt-4o-mini`).

## Installation (développeur)

Prérequis : Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download), un micro.

```bash
dotnet run --project src/Plume     # lancer en dév
dotnet test                        # tests (logique pure, xUnit)
./deploy.ps1                       # publier + installer dans %LOCALAPPDATA%\Plume + relancer
```

L'exe runtime vit dans `%LOCALAPPDATA%\Plume` (jamais dans OneDrive — sinon « Démarrer avec Windows » devient instable). L'installateur collègues se fabrique avec `installer/build-installer.ps1` (Inno Setup 6 requis).

---

## Usage

- **Raccourci** (défaut Alt+Espace) : 1er appui = démarre l'écoute, 2e appui = arrête et insère le texte. **Échap** annule à tout moment.
- **Mode de dictée** (réglages) : *Toggle* (appui/ré-appui) ou *Maintien enfoncé* (push-to-talk).
- **Pastille flottante** : **double-clic** = démarrer/arrêter la dictée sans clavier ; **clic** = menu des **styles de réécriture** sur le texte sélectionné (Corriger, Email pro… — éditables, favoris, modèle IA par style, styles « avec voix » pour dicter une consigne).
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

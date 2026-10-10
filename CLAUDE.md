# CLAUDE.md — Plume

> Fichier lu automatiquement par Claude Code à chaque session. Le garder **court**.
> Le détail fonctionnel et technique vit dans `docs/`.

## C'est quoi
**Plume** est un utilitaire **Windows** de **dictée vocale**. L'utilisateur appuie sur un **raccourci global** (personnalisable), parle en **français**, et le texte **nettoyé** est inséré là où se trouve le curseur, dans **n'importe quelle application** (Word, navigateur, Slack, Jira…). S'y ajoutent : styles de réécriture sur sélection (menu de la pastille flottante) et assistant vocal (Ctrl+Alt+A).

## ⭐ RÈGLE PRIMORDIALE
**La rapidité et la vitesse d'utilisation priment sur tout.** Ouverture quasi instantanée, faible empreinte mémoire, zéro friction à l'usage. **Avant toute mise à jour qui ralentirait l'app** (démarrage, latence ressentie, mémoire, taille de l'exe), **PRÉVENIR l'utilisateur** et proposer une alternative légère ou un mode optionnel désactivé par défaut. Aucune fonctionnalité ne justifie de casser la légèreté.

## Priorités produit — NON négociables
1. **Léger et instantané** : ouverture < 1 s, faible empreinte mémoire, vit dans la barre des tâches (tray).
2. **Design ultra simple** : une pastille flottante + un menu tray + une fenêtre de réglages. Rien d'autre.
3. **Réactif** : retour visuel < 150 ms après l'appui sur le raccourci.
4. **Français uniquement.** **Aucune traduction, jamais** (ni dans le code, ni dans la roadmap).

## Stack imposée
- **.NET 10 · C# · WPF** (Windows uniquement) — 100 % XAML natif, aucune lib UI tierce.
- **NAudio** — capture micro (seule dépendance NuGet autorisée).
- **Win32 `RegisterHotKey`** — raccourcis globaux.
- **HttpClient** → **Groq** (STT si clé Groq ; nettoyage si modèle « groq/… ») ou **OpenRouter** sinon. Routage statique décidé avant l'appel — PAS de bascule automatique sur erreur.
- Insertion au curseur via **presse-papiers + Ctrl+V** (presse-papiers restauré).
- ❌ Interdits : Electron, dépendance lourde non justifiée, framework cross-platform.

## Flux (en une ligne)
`Raccourci` → capture audio 16 kHz → STT Groq `/audio/transcriptions` (Whisper) → nettoyage LLM `/chat/completions` (prompt du style) → règles de remplacement → insertion au curseur.

## Carte du code (lire ceci AVANT d'explorer — ouvrir directement le bon fichier)
`src/Plume/` :
- `App.xaml.cs` — racine : démarrage, tray, enregistrement des raccourcis, pastille (`_badge`), thème.
- `DictationController.cs` — orchestre tous les flux (dictée, style sur sélection, style avec voix, assistant) : machine à états, démarrage/arrêt de l'écoute, warm-up réseau. Découpé en classes partielles par flux : `.Dictation.cs` (arrêt → STT → nettoyage → remplacements → insertion, dictée classique), `.Styles.cs` (style écrit/avec voix, Commande vocale), `.Agent.cs` (assistant vocal, fenêtre de réponse).
- `TrayIcon.cs` — icône + menu tray (Shell_NotifyIcon en P/Invoke, sans WinForms).
- `Models/AppConfig.cs` — config persistée (`%APPDATA%\Plume\config.json`) + `HotkeyConfig`/`UsageStats`. `Models/SelectionStyle.cs` — styles du menu (+ `BadgePosition`). `Models/AgentSession.cs` — conversation assistant. `Models/ClipboardSnapshot.cs` — instantané du presse-papiers (capture/restauration + retentatives). `Models/ModelInfo.cs` — entrée du catalogue de modèles. `Models/StyleProfile.cs` — legacy (migration).
- `Services/` — un rôle par fichier : `AudioCaptureService` (micro NAudio 16 kHz), `TranscriptionService` (STT Groq/OpenRouter), `CleanupService` (LLM), `InsertionService` (collage + restauration presse-papiers), `SelectionService` (capture de la sélection), `ReplacementService` (règles De→Vers), `StyleService`+`StyleCatalog` (styles, intégrés, migration), `HotkeyService`+`HotkeyParser`, `Win32Screens` (écrans en Win32 pur, remplace Screen WinForms), `AgentChatService` (assistant), `ConfigService` (config + chiffrement DPAPI des clés API), `DiagLogService` (journal de diagnostic opt-in, `%LOCALAPPDATA%\Plume\diag.log`), `ThemeService` (clair/sombre à chaud), `StartupService` (démarrage Windows), `ModelCatalog`/`ModelUpdateService`/`GroqModelsClient` (catalogue + vérif modèles), `WarmUpPlanner` (hôtes à préchauffer selon le routage, pur), `UpdateService` (mise à jour à la demande via les Releases GitHub : vérif, téléchargement, empreinte, remplacement de l'exe), `UsageStatsService`/`DailyUsageService` (stats cumulées + agrégats journaliers), `LightMarkdown`, `MarkdownHtml` (copie riche HTML/CF_HTML), `TickSoundService`+`SoundCatalog` (tops sonores), `BadgePlacementService` (position pastille, pur), `ScreenMetrics` (conversions DPI physique↔DIP), `CommandPromptBuilder` (prompt Commande vocale), `DarkTitleBar` (barre de titre sombre), `OpenRouterException`, `WavUtils`, `AppLogo`, `StarRating`.
- `Views/` — `SettingsWindow` (réglages : chrome commun — barre latérale, agrégation de la config — découpé en classes partielles par onglet : `.General.cs`, `.Dictionnaire.cs`, `.Styles.cs`, `.APropos.cs`, chacune référençant son panneau via le champ généré, ex. `PanelGeneral.SttCombo`), `BadgeWindow` (pastille flottante), `StyleMenuWindow` (menu des styles), `AnswerWindow` (assistant), `OnboardingWindow` (assistant de prise en main, 3 étapes, premier lancement sans clé). `Views/SettingsPanels/` — un `UserControl` par onglet des Réglages : `GeneralPanel` (markup + 2 gestionnaires liés en XAML), `DictionnairePanel` (autonome : vocabulaire, remplacements, événement `Changed`), `StylePanel` (markup + 2 gestionnaires liés en XAML ; logique dans `SettingsWindow.Styles.cs`), `AProposPanel` (markup pur). `Converters/StarRatingConverter.cs` — liaison étoiles.
- `Resources/` — `Theme.xaml` (styles), `Colors.Light/Dark.xaml` (tokens échangés à chaud ; toujours `DynamicResource`).

Tests : `tests/Plume.Tests` (xUnit, logique pure uniquement). Déploiement : `deploy.ps1` (boucle courte de dev ; l'exe runtime vit dans `%LOCALAPPDATA%\Plume`, jamais dans OneDrive). Installateur collègues : `installer/build-installer.ps1` → `PlumeSetup-X.Y.Z.exe` (Inno Setup 6 requis sur le poste de build).

## Construire / lancer
- Dév : `dotnet run --project src/Plume` · Tests : `dotnet test`
- Analyzers .NET actifs (`Directory.Build.props` + politique de sévérité dans `.editorconfig` à la racine) : le build doit rester à **0 warning** ; ne pas assouplir une règle sans la commenter dans `.editorconfig`.
- Exe : `dotnet publish src/Plume -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false`
- Config + clés API : `%APPDATA%\Plume\config.json` (créé au 1er lancement, **jamais committé**).

## Périmètre de travail
- Version courante : voir `<Version>` dans `src/Plume/Plume.csproj`. **À-venir et plans prêts : `docs/roadmap.md` (point d'entrée unique).** Historique livré : `docs/roadmap-archive.md` (ne lire que pour une décision passée).
- **NE PAS** implémenter : traduction (jamais), snippets (couverts par les règles de remplacement), ni les idées non validées (`docs/superpowers/specs/2026-07-03-plume-idees-ulterieures-cadrees.md`).

## Règles pour Claude Code
- Toutes les chaînes d'interface **en français** ; commentaires en français, concis.
- **Ne jamais committer de clé API** ; lire depuis `config.json` ou variable d'env.
- Gérer toutes les erreurs **sans crash** (pas de micro, pas de réseau, clé invalide, silence, texte vide, presse-papiers verrouillé).
- Couleurs uniquement via tokens `Colors.Light/Dark.xaml` + `DynamicResource` (jamais en dur dans les vues).
- Avant d'ajouter une dépendance NuGet : vérifier qu'elle est légère et justifiée (défaut : refuser).
- Un fichier ajouté/renommé → mettre à jour la « Carte du code » ci-dessus.
- **À chaque version livrée** : bump `<Version>` dans `Plume.csproj` **+** ligne de changelog en tête de l'onglet « À propos » **+** mise à jour `docs/roadmap.md` (retirer de l'à-venir) et `docs/roadmap-archive.md` (ajouter au livré) **+** après le `git push`, `./publish-release.ps1` (Release GitHub lue par le bouton « Vérifier les mises à jour » ; le dépôt nommé dans le script doit rester celui de `UpdateService.Repository`). Aucune livraison sans ces 5 étapes.
- Git : commits directs sur `master` (pas de branches ni PR), puis **`git push` en fin de livraison** — le dépôt est sauvegardé sur GitHub (privé : `alexandreb06800/Plume`, depuis le 2026-07-23).

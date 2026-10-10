# Roadmap — Archive des versions livrées

> Historique détaillé des versions **déjà livrées** (ou abandonnées), déplacé ici pour garder `roadmap.md` court (et économe en tokens pour les sessions IA). **Ne pas lire ce fichier pour planifier du travail** — seul `docs/roadmap.md` fait foi pour l'à-venir.

---

## 5.4.12 — Copier le dernier message depuis la pastille ✅ (livrée le 2026-10-10)
Demande : récupérer le dernier texte sans passer par le menu tray.
- Entrée « Copier le dernier message » dans le menu de la pastille (`StyleMenuWindow`), sous « Assistant vocal » ; estompée et inactive tant qu'aucun texte n'a été produit.
- Même code que le menu tray (`TrayIcon.CopyLast`, rendue publique) : copie + notification « Copié ✓ », presse-papiers verrouillé sans crash.
- Pas de clic droit sur la pastille (écarté par l'utilisateur).
- `publish-release.ps1` vide désormais le dossier de publication avant `dotnet publish` : relancé deux fois de suite, le publish laissait `Resources` vide et l'archive partait sans logo ni icône. L'archive de la Release 5.4.12 a été remplacée une fois pour cette raison (0 téléchargement), empreinte mise à jour.

## 5.4.11 — Prompts d'email sans nom d'auteur ✅ (livrée le 2026-10-10)
Suite de l'audit 5.4.10, sur décision utilisateur : les prompts intégrés « Email pro » et « Finaliser l'email » citaient le prénom et le nom de l'auteur (« l'assistant de rédaction… de [prénom] », « n'écris jamais mon nom ([prénom], [prénom nom]) »). Distribués à quelqu'un d'autre, ils auraient rédigé en son nom.
- Formulations génériques : « mon assistant de rédaction d'emails professionnels », « N'écris JAMAIS mon nom ni mon prénom ». Vérifié sur Qwen 3.8 (6 cas, dont deux brouillons contenant une signature complète) : aucune signature recopiée, comportement inchangé.
- **Migration par empreinte** : pour reconnaître un ancien défaut, le code en gardait le texte verbatim (`Legacy3BuiltInPrompts`) — donc le nom restait dans l'exe. Les anciens défauts de ces deux styles sont désormais reconnus par leur empreinte SHA-256 (`StyleService.LegacyPromptHashes`, `PromptHash`), sans texte conservé. Vérifié avec les quatre anciens textes réels : tous migrés ; un prompt personnalisé reste intact.
- Vérifié sur l'exe publié : plus aucune occurrence du nom (recherche ASCII/UTF-8 et UTF-16).
- Releases 5.4.8 et 5.4.9 retirées de GitHub (leur exe contenait le chemin du poste de compilation).
- Reste hors de l'exe, non modifié : `AppPublisher` de `installer/plume.iss` (installateur non publié) et les notes historiques de `docs/`.

## 5.4.10 — Audit de ce qui est distribué + guide d'installation ✅ (livrée le 2026-10-10)
Demande : expliquer l'installation sur la page GitHub et vérifier qu'aucune clé ni donnée personnelle n'est distribuée. Audit du dépôt (arbre et historique complet), de l'archive de la Release et de l'exe (recherche en ASCII/UTF-8 et UTF-16).
- **Clés et secrets : rien.** Aucune clé GitHub, Groq, OpenRouter ou privée, aucun fragment de `config.json` — ni dans le dépôt, ni dans l'historique git, ni dans l'exe. `config.json` et `.env` sont ignorés par git ; les commits utilisent l'adresse anonyme de GitHub. L'archive ne contient que `Plume.exe` et `Resources`.
- **Corrigé — chemin du poste de compilation dans l'exe** : l'exe embarquait le chemin complet de son fichier de symboles (`C:\Users\<nom>\…\Plume.pdb`), donc le nom d'utilisateur Windows de celui qui publie. `DebugType=none` en configuration Release (`Plume.csproj`). Vérifié sur l'exe publié : il ne reste que les chemins de l'auteur de la bibliothèque NAudio.
- **Corrigé — mentions de l'employeur** introduites dans cette archive lors des versions précédentes (noms de produits utilisés dans le banc d'essai) : neutralisées. Elles restent lisibles dans l'historique git du dépôt (privé) ; pas de réécriture d'historique.
- **Signalé, non modifié** : les prompts intégrés « Email pro » et « Finaliser l'email » citent le prénom et le nom de l'auteur (interdiction de signer à sa place). Sans importance pour un usage personnel ; à rendre génériques si Plume est distribuée à d'autres personnes.
- **README** : sections Installation (runtime .NET 10 Desktop, archive de la Release, avertissement SmartScreen, vérification d'empreinte), Mise à jour (bouton, jeton), Sécurité et données, Désinstallation, Développement. L'ancienne section renvoyait à un installateur `PlumeSetup` qui n'est pas publié.
- Limites connues du mécanisme de mise à jour, assumées : l'empreinte SHA-256 est publiée au même endroit que l'archive (elle garantit l'intégrité du téléchargement, pas l'identité de l'auteur — un compte GitHub compromis pourrait publier les deux) ; l'exe n'est pas signé.

## 5.4.9 — Bouton de mise à jour : libellé sans ambiguïté ✅ (livrée le 2026-10-10)
Premier essai réel du bouton 5.4.8 par l'utilisateur : la version était bien trouvée, mais le message « Version 5.4.8 disponible… Plume se fermera puis se relancera » a été compris comme « redémarre Plume toi-même ». Plume a été relancée à la main et rien n'a été installé (constat sur disque : dossier de téléchargement jamais créé). Le second clic attendu (« Installer et redémarrer ») n'était pas assez visible.
- Le bouton devient **« Installer la version X »**, en style principal (`PrimaryButton`), et le message dit explicitement de cliquer dessus (« Plume la télécharge et redémarre toute seule »).
- Leçon : un message d'état ne doit jamais décrire une action que l'application fera comme si l'utilisateur devait la faire.

## 5.4.8 — Bouton « Vérifier les mises à jour » ✅ (livrée le 2026-10-10)
Réouverture de V8-F point 2 (abandonné le 2026-07-16) sur décision utilisateur — voir `docs/roadmap.md`. Choix : mise à jour **à la demande**, via les Releases du dépôt GitHub privé.
- **`Services/UpdateService.cs`** : lit `releases/latest` du dépôt (`UpdateService.Repository`), compare les versions, télécharge l'archive `Plume-X.Y.Z.zip`, **vérifie son empreinte SHA-256** (ligne `sha256: …` du texte de la Release — installation refusée si absente ou différente : l'exe n'est pas signé), puis installe. Installation : extraction dans un dossier temporaire, l'exe en cours est **renommé** en `Plume.old.exe` (Windows l'autorise, pas l'écrasement — vérifié), les nouveaux fichiers prennent sa place ; retour arrière si la copie échoue ; `Plume.old.exe` supprimé au démarrage suivant, en tâche de fond.
- **Redémarrage** (`App.RestartAfterUpdate`) : le verrou d'instance unique est rendu avant de lancer le nouvel exe, sinon celui-ci se croirait en double.
- **Accès au dépôt privé** : jeton GitHub facultatif dans À propos (`AppConfig.UpdateToken`, chiffré DPAPI comme les clés API) ; sans lui, la connexion Git déjà enregistrée sur le poste (`git credential fill`, sans jamais ouvrir de fenêtre). Sur un poste où `git push` fonctionne, rien à configurer.
- **Interface** : bouton en deux temps dans À propos (« Vérifier les mises à jour » → « Installer et redémarrer »), état affiché dessous, erreurs en français (pas de réseau, jeton refusé, aucune version publiée, empreinte incorrecte).
- **`publish-release.ps1`** : publie la version courante (dépôt propre et poussé exigé) — publish, archive (Plume.exe + Resources), empreinte, Release `vX.Y.Z` + envoi. Une Release existante n'est jamais réécrite.
- **RÈGLE PRIMORDIALE** : aucun appel réseau ni travail au démarrage (seul ajout : la suppression éventuelle de `Plume.old.exe`, en tâche de fond). Aucune dépendance ajoutée (`System.IO.Compression` fait partie de .NET). Confidentialité : un appel à `api.github.com` au clic uniquement, documenté dans `docs/confidentialite-flux-donnees.md`.
- **Écarté** : mise à jour automatique en arrière-plan (exe non signé : télécharger et exécuter du code sans action de l'utilisateur serait un risque), vérification au démarrage, installateur Inno Setup dans la boucle (non requis : l'exe fait 1,2 Mo).
- **Limites** : les styles créés par l'utilisateur vivent dans la config de chaque poste et ne sont pas mis à jour par ce bouton ; le poste doit avoir le runtime .NET 10 Desktop. Non vérifié par test automatique : le clic et le redémarrage réel (logique testée, 23 tests ; chaîne réseau vérifiée contre la vraie Release).

## 5.4.7 — Style « Email pro » révisé ✅ (livrée le 2026-10-10)
Dernier style à passer au banc d'essai (3 cas : réponse interne avec fil cité, réponse client avec mots à éviter et date inconnue, brouillon très court ; Qwen 3.8 et GPT-4o mini). Bilan de départ : bon — fil jamais recopié, pas de signature, « dès que possible » retiré, un brouillon court reste court.
- **« erreur » / « bug » du brouillon** : gardés tels quels dans 3 réponses client sur 4 (« il s'agissait d'une **erreur de mapping** »), la règle disant « sauf nécessité absolue ». Précision : leur présence dans le brouillon n'en est pas une ; reformuler sans supprimer l'information (« un ajustement de mapping de notre côté »). Après : 12 passes sur 12.
- **Forme d'adresse** : une seule par email, celle du brouillon prime sur les défauts interne/externe.
- **Défaut propre à cette variante** : l'exemple « client » du prompt s'adressait à une « Julie » vouvoyée ; GPT-4o mini vouvoyait ensuite toute Julie, même tutoyée dans le brouillon (« Salut Julie, … vous pouvez valider », 3 fois sur 3 — y compris avec Julie déclarée « interne » dans le référentiel). L'exemple utilise désormais « Monsieur Bernard » ; formule de registre alignée (« Tutoiement dans la source → tutoiement »), le jeu de pronoms « Je tutoie → tu tutoies » étant ambigu. Après : 6 passes sur 6 (Julie et Sophie).
- « Finaliser l'email » vérifié sur le même risque (exemple à prénom) : pas de défaut, inchangé.
- Migration douce : `email-pro` ajouté à `Legacy3BuiltInPrompts` (un Email pro personnalisé — référentiel rempli — n'est jamais touché). Fiche `docs/prompts/style-email-pro.md` à jour.

## 5.4.6 — Prompts des styles révisés ✅ (livrée le 2026-10-10)
Demande : même analyse que pour le prompt de dictée, sur les styles. Méthode : banc d'essai reproduisant l'assemblage exact de Plume (`CleanupService` + `CommandPromptBuilder`), 20 cas pièges sur les 9 styles intégrés et 5 des styles créés (Ticket Jira, Page Confluence, Commentaire de résolution, Réponse client, Finaliser l'email), sur Qwen 3.8 (Groq) et GPT-4o mini (OpenRouter). On ne corrige que ce que la mesure montre défaillant.
- **Tu/vous inversé par GPT-4o mini (défaut majeur)** : avec l'ancienne consigne (« ne remplace jamais "tu" par "vous" ni l'inverse »), Corriger, Ton plus décontracté et Ton plus empathique passaient du « vous » au « tu », Ton plus professionnel du « tu » au « vous » — **12 fois sur 12**. Nouvelle consigne positive (« repère si le texte tutoie ou vouvoie… garde EXACTEMENT la même forme ») ; Ton pro et Ton décontracté précisent en plus que le registre n'impose ni le vouvoiement ni le tutoiement, avec exemple. Après : conservé sur les deux modèles, 2 passes sur 2.
- **Rallonger** rendait le texte inchangé sur Qwen → « au moins deux fois plus de mots », aucun fait nouveau. **Raccourcir** : « au plus la moitié », rappels de contexte et précautions oratoires nommés. Limite : sur GPT-4o mini ces deux styles restent timides.
- **Basculer** : « Bonjour Madame Durand » devenait « Bonjour Durand » (Qwen) ou « Monsieur Durand » (GPT-4o mini) → noms et civilités conservés tels quels.
- **Commande vocale** : salutation et formule de politesse hors de toute liste.
- **Assemblage des styles avec voix sans sélection** : le message utilisateur était « Texte à reformuler… » suivi d'un bloc vide ; GPT-4o mini répondait « il n'y a pas de texte à reformuler ». Nouveau `CleanupKind.NoSelection` (message explicite, sans bloc). Variante écartée après mesure : passer la dictée en message utilisateur (réparait la Commande vocale mais ramenait le jargon dans Réponse client).
- **Styles créés** (fiches `docs/prompts/` mises à jour ; à recoller dans Réglages → Styles là où ils vivent) : Ticket Jira (cas « demande d'évolution »), Réponse client (« Bonjour, », accord, table de traduction du jargon — 6 passes sur 6 sans jargon), Finaliser l'email (aveu reformulé, plus supprimé ; aussi dans `StyleCatalog`). Page Confluence et Commentaire de résolution : corrects, inchangés. Email pro : non testé (prompt de 8 Ko à personnaliser).
- **Migration douce** : génération précédente → `Legacy3BuiltInPrompts`. Changement assumé : les styles retirés du jeu par défaut en 5.1.7 (Corriger, Raccourcir, Rallonger, Ton décontracté, Ton empathique) reçoivent désormais leur prompt révisé s'ils n'ont jamais été personnalisés (`DefaultPromptFor`) — ils n'étaient plus migrés. Jamais réinjectés ni supprimés ; un prompt personnalisé n'est jamais touché.

## 5.4.5 — Prompt de dictée par défaut optimisé ✅ (livrée le 2026-10-10)
Demande : « analyse mon prompt et optimise-le si besoin ». Analyse du défaut (`StyleCatalog.DictationPrompt`), réécriture, puis banc d'essai sur le prompt réel — 15 phrases pièges, Qwen 3.8 (Groq) et GPT-4o mini (OpenRouter). Philosophie inchangée : « intervenir le MOINS possible ».
- **Défauts relevés** : contradiction « intervenir le moins possible » / « aère, jamais un seul bloc » (dictée courte découpée) ; aucune règle sur les nombres (Qwen « 15 novembre », GPT-4o mini « quinze novembre »), les auto-corrections, l'adresse en tête ; « Sujet différent : » recopié dans le texte ; pas d'exemple anti-détournement ; « à la ligne » supprimé par le modèle (commande perdue).
- **Nouveau prompt** (structure : règle absolue + 3 exemples dont l'anti-détournement · tâche · ce qu'on conserve · nombres · mise en forme) : nombres en chiffres pour dates/heures/montants/pourcentages/quantités ; auto-corrections (« mardi non pardon mercredi » → « mercredi ») ; adresse en tête isolée + ligne vide, avec exemple (repris de la v4 du 2026-07-09, demande explicite d'alors) ; dictée courte = un seul paragraphe ; marqueur de changement de sujet non recopié ; « à la ligne » recopié tel quel pour l'application ; exemple de liste.
- **Trois invariants V8-I-0 conservés mot pour mot** (test `DictationPrompt_Contient_Les_Trois_Invariants_Durcis_V8I0`) : tutoiement/vouvoiement, vocabulaire du locuteur, correction phonétique. La correction phonétique, d'abord retirée au profit de « mot inconnu = voulu », a été **rétablie et bornée** : mot COURANT incohérent → mot phonétiquement proche (« maille » → « mail », « devine » → « devis », vérifié sur phrase absente du prompt) ; mot inconnu (nom propre, sigle, métier) → recopié (noms de produit et sigles fictifs du banc d'essai restés intacts).
- **Résultats** : Qwen 3.8 15/15. Avant : montants en lettres, « Sujet différent : » recopié, prénom non isolé, « à la ligne » perdu. GPT-4o mini : mêmes gains ; résidus connus — « dix minutes » et « 3 heures » en lettres/mi-chiffres, a reformulé une fois « Tu peux me dire » en « Peux-tu me dire ».
- **Migration douce** : le défaut 5.4.4 devient `Legacy3DictationPrompt` ; test paramétré sur Legacy2 et Legacy3 (par réflexion). Un prompt personnalisé n'est jamais touché.
- **`ReplacementService.ApplyLineBreaks`** : la commande placée par le modèle en tête de ligne après une phrase close donnait « retour.⏎.⏎Je » (1 fois sur 3 mesuré). Le motif absorbe désormais les blancs et retours à la ligne précédents, et la clôture n'ajoute un point que si la phrase n'en a pas déjà (rien après « ? », « ! », « : », ni en tout début de texte). Les 12 tests existants passent inchangés, 3 ajoutés.
- Longueur : 3,3 → 4,8 Ko (~+350 tokens par dictée). Sans coût sur l'offre Groq gratuite, mais la limite de ~8 000 tokens/min passe d'environ 8-10 à 6-7 dictées par minute.

## 5.4.4 — Listes automatiques dans la dictée ✅ (livrée le 2026-10-10)
Idée issue d'un tour de la concurrence (« smart formatting » de Wispr Flow), retenue par l'utilisateur parmi 4 propositions. Prompt seul + un nettoyage de fins de ligne, aucun coût de latence.
- `StyleCatalog.DictationPrompt` : la ligne vague « tu peux les mettre en liste à puces » devient une règle « Listes » : liste quand le locuteur énumère clairement des éléments à part entière (≈ 3 ou plus) ou annonce une liste ; **numérotée** si ordre/numérotation (premièrement, d'abord, ensuite, enfin…), sinon **puces « - »** ; majuscule, pas de virgule en fin de ligne ; une énumération courte dans un récit reste une phrase ; aucune autre mise en forme.
- Migration douce : l'ancien défaut devient `Legacy2DictationPrompt` (`StyleService.MigrateHardenedPrompts`) — un prompt jamais personnalisé passe au nouveau au lancement, un prompt personnalisé n'est jamais touché (tests).
- `ReplacementService.TrimLineEnds` après le nettoyage : GPT-4o mini termine les lignes de liste par des espaces (sauts de ligne markdown).
- Banc d'essai (7 phrases dont 3 pièges qui ne doivent PAS devenir des listes, prompt réel) : **Qwen 3.8 : 7/7** (numérotée × 2, puces × 2, 3 phrases conservées) ; ancien prompt : 2 listes ratées, puces finies par « ; ». GPT-4o mini : listes correctes et pièges conservés, mais transforme un « d'abord… ensuite… enfin » en puces au lieu de numéros et a remplacé « doc » par « documentation » une fois.

## 5.4.3 — Plantage à l'arrêt du micro corrigé ✅ (livrée le 2026-10-10)
Constat (journal d'événements Windows) : plantage de Plume le 2026-09-15 (5.3.9) — `AccessViolationException` dans `NAudio.Wave.WaveInBuffer.Reuse` → `waveInPrepareHeader`, sur le thread d'enregistrement. Cause : `AudioCaptureService.Stop()` appelait `StopRecording()` (qui ne fait que SIGNALER l'arrêt) puis `Dispose()` aussitôt ; le thread NAudio pouvait encore remettre en service un tampon déjà libéré. **Reproduit** : 300 démarrages/arrêts rapides du micro → 4 plantages sur 5 passes.
- `AudioCaptureService.StopAndWait` : aucune libération avant que le thread d'enregistrement soit sorti de sa boucle (`RecordingStopped`, suivi dès le démarrage par `_threadDone` — déjà posé si le micro a été perdu, donc sans attente). Attente bornée à 500 ms.
- Seconde course NAudio : un arrêt demandé avant le démarrage effectif du thread (2e appui pendant l'ouverture du micro) était ignoré (NAudio repasse en capture) → attente du premier bloc audio (`_threadStarted`, borné à 300 ms) avant d'arrêter.
- `CaptureStarted` passé en `BeginInvoke` (garde : écoute toujours en cours) : l'`Invoke` bloquant immobilisait le thread NAudio pendant qu'un arrêt simultané attendait sa fin.
- Vérifié : 5 × 300 cycles → 0 plantage, 0 attente expirée. Coût : arrêt du micro ~35 ms médian (contre ~10 ms), une fois par dictée — imperceptible face au réseau (RÈGLE PRIMORDIALE : impact négligeable, assumé contre un plantage).

## 5.4.2 — Presse-papiers restauré hors historique ✅ (livrée le 2026-10-09)
Repris de la version professionnelle (5.4.0 du 2026-07-28, constat de confidentialité « gravité haute »), en le fusionnant avec la restauration multi-formats de la 5.4.0 perso : un gestionnaire de mots de passe marque le texte copié hors historique (`ExcludeClipboardContentFromMonitorProcessing`…). La restauration multi-formats ne recopiait pas ces marqueurs (WPF les relit souvent vides) → un mot de passe copié juste avant une dictée entrait dans l'historique Win+V et le presse-papiers infonuagique après restauration.
- `ClipboardSnapshot` : marqueurs exclus de la capture (`PlanFormats`) et **toujours reposés** à la restauration (`AddExclusionMarkers`, multi-formats comme texte seul via `WriteTextExcluded`, repli `SetText` si le marquage échoue). `SetTextExcludedFromHistory` factorisé sans reprise imbriquée.
- Vérifié sur le vrai presse-papiers : mot de passe marqué → restauré marqué (historique + cloud) ; texte simple, cellules Excel et gras Word toujours restitués.

## 5.4.1 — Catalogue des modèles de nettoyage révisé ✅ (livrée le 2026-10-09)
Constat : **`llama-3.3-70b-versatile` et `llama-3.1-8b-instant` arrêtés par Groq le 16/08/2026** (404 ; page officielle des retraits Groq) — une config « nettoyage Llama » insérait depuis le texte brut à chaque dictée (bulle « Nettoyage indisponible »), et l'assistant vocal était en panne. **`google/gemini-2.0-flash-001`** : 404 sur OpenRouter (« No endpoints found »). Les Whisper restent servis par OpenRouter `/audio/transcriptions` (testé 200) bien qu'absents de `/models`.
- `ModelCatalog.Cleanup` : **Qwen 3.8 27B (Groq)** en tête (= modèle retenu par la prise en main sans clé OpenRouter, `CleanupModelUtilisable`), **GPT-OSS 120B (Groq)**, **GPT-4o mini (OpenRouter)**. Retirés : les deux Llama, Gemini 2.0 Flash.
- Comparatif sur le prompt de dictée réel (5 phrases : hésitations, auto-correction « non pardon », dates/nombres, noms) : Qwen 3.8 meilleur (auto-correction OK, dates en chiffres) et ~250 ms médian ; GPT-4o mini bon, ~840 ms ; GPT-OSS 120B ~900 ms et rate l'auto-correction. Coût négligeable dans tous les cas (offre Groq gratuite : 0 € ; GPT-4o mini ≈ 0,15 $/1 000 dictées). Limite de l'offre Groq gratuite : ~8K tokens/min ≈ 8-10 dictées/min (429 au-delà → texte brut).
- Ids Groq à éditeur (`groq/qwen/…`, `groq/openai/…`) : seul le préfixe `groq/` est retiré (test ajouté). « Vérifier les modèles » ne propose pas de successeur pour ces ids (pas de token de version au format attendu) — limite connue de `ModelUpdateService`, sans erreur.
- Vigilance : Groq renouvelle souvent ses Qwen (qwen3.6-27b arrêté le 14/09/2026) ; en cas de retrait, Plume insère le texte brut avec le message « modèle retiré » qui renvoie aux Réglages.

## 5.4.0 — .NET 10 + correctifs de l'audit d'optimisation ✅ (livrée le 2026-10-09)
Audit d'optimisation du 2026-10-09 (code lu en entier, mesures réelles) ; tout appliqué sauf ReadyToRun (mesuré, écarté).
- **.NET 10 LTS** (fin de support .NET 8 le 10/11/2026) : `net10.0-windows` (app + tests), installateur (`DotNet10DesktopInstalled`, lien `aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe`, vérifié), `deploy.ps1`, `build-installer.ps1`. Build 0 warning sans retouche. Démarrage mesuré (2e instance, médiane) : 442 ms → 376 ms.
- **Presse-papiers restauré à l'identique** (`ClipboardSnapshot`) : l'ancien instantané ne gardait qu'UN format (fichiers > image > texte). Bug **reproduit sur Excel** : copier des cellules, dicter, recoller → une **image** au lieu des cellules (Excel annonce une image d'aperçu). Désormais tous les formats utiles sont copiés (`PlanFormats`, pur et testé) : exclusion des formats GDI/OLE, de l'aperçu image en présence d'un document Office, et de RTF/XML Spreadsheet/Biff5 en présence de Biff12 (RTF ≈ 200 ms de rendu à froid côté Excel). Plafond 64 Mo, repli sur l'ancien instantané (texte désormais AVANT image). Mesuré : Excel 50 lignes → cellules + gras restitués, capture 18-57 ms à froid (ancien : 39 ms) ; Word → gras restitué, 23-45 ms ; HTML, fichiers + effet « couper », image seule : conservés.
- **Warm-up allégé** (`WarmUpPlanner`, pur et testé) : ne préchauffe que les hôtes réellement routés (config 100 % Groq → plus d'appel OpenRouter) ; OpenRouter via `/api/v1/key` au lieu de `/models` (**765 Ko non compressés** téléchargés puis jetés à chaque début d'écoute).
- **HttpClient** : `SocketsHttpHandler` avec `PooledConnectionIdleTimeout` 10 min (défaut 1 min → reconnexion ~500 ms après une courte pause) + décompression automatique.
- **Pastille** : halo porté par une ellipse dédiée (`Halo`) derrière le contenu, au lieu de `Card` : le flou n'est plus recalculé à chaque mouvement des barres de niveau (25×/s en écoute).
- **Stats** : écriture config + `usage-daily.json` en tâche de fond (`DailyUsageService.RecordDictation`, sous verrou) au lieu du thread UI après chaque dictée.
- **Écarté après mesure** : `PublishReadyToRun` — exe 1,2 → 2,1 Mo pour -16 ms au démarrage (commentaire dans le csproj).
Impact RÈGLE PRIMORDIALE : positif (démarrage plus rapide, moins de réseau et de rendu pendant la dictée) ; taille de l'exe inchangée (1,2 Mo).

## 5.3.9 — Choix du nombre d'écrans pour la pastille ✅ (livrée le 2026-07-23)
Demande directe de l'auteur : pouvoir choisir entre une pastille sur chaque écran connecté (comportement
historique depuis V8-J) et une seule pastille, sur l'écran principal uniquement — utile quand la seconde
pastille n'a pas d'usage sur un setup multi-écrans donné.
- **`AppConfig.BadgeOnAllScreens`** (bool, défaut `true` = comportement historique inchangé pour les
  configs existantes).
- **Réglages → Général → Apparence & système** : nouveau menu déroulant « Écrans » (« Sur tous les
  écrans » / « Écran principal uniquement »), sous la bascule « Pastille flottante ».
- **`App.ReconcileBadges`** filtre la liste des écrans (`Win32Screens.All()`) sur `IsPrimary` quand le
  réglage est sur écran principal ; la boucle de réconciliation existante (V8-J) ferme alors les pastilles
  des écrans non retenus, sans changement d'architecture. `ApplyBadgeVisibility` réconcilie désormais à
  chaque sauvegarde (plus seulement montrer/cacher), pour que le changement s'applique à chaud.
Impact RÈGLE PRIMORDIALE : nul (un bool de config + un filtre sur une liste déjà énumérée ; aucune pastille
supplémentaire créée, aucun coût au démarrage).

## 5.3.8 — Correctifs de l'audit complet ✅ (livrée le 2026-07-16)
Plan exécuté : `docs/superpowers/plans/2026-07-16-correctifs-audit.md` (audit source :
`docs/audits/2026-07-16-audit-complet.md`). Quatre correctifs ciblés, zéro nouvelle dépendance :
- **Onboarding 100 % Groq (bug, gravité haute)** : à la validation de la clé Groq (étape 1), si
  aucune clé OpenRouter n'existe, le modèle de nettoyage bascule sur le premier modèle Groq du
  catalogue (`ConfigService.CleanupModelUtilisable`, pur + testé). Avant : le défaut
  `openai/gpt-4o-mini` (OpenRouter) bloquait la première dictée de l'étape 3 avec un message parlant
  d'un service que le guide n'avait jamais mentionné.
- **Modèle disparu** : les erreurs 400/404/410 de transcription/nettoyage/assistant ajoutent
  « Le modèle configuré n'existe peut-être plus : ouvre les Réglages → Général et vérifie les
  modèles. » (`OpenRouterException.ModelHint`) — l'app reste réparable sans recompilation le jour où
  Groq/OpenRouter retire un modèle.
- **Dictée longue sur lien lent** : délai STT proportionnel à la taille de l'audio
  (`TranscriptionService.TimeoutFor` : 45 s + 10 s/Mio, plafond 180 s) au lieu des 45 s fixes qui
  perdaient une dictée de 5 min sur VPN/4G.
- **Multi-écrans** : le niveau vocal est marshalé sur le thread UI avant d'énumérer les pastilles
  (`App.xaml.cs`) — supprime une course rarissime (écran branché/débranché pendant une dictée) qui
  annulait la dictée avec « Micro déconnecté ».

---

## Vocabulaire métier ✅ (livré le 2026-07-16, config uniquement — pas de bump de version)
Ticket exécuté tel quel (plan personnel, non versionné). Aucun changement de code — 26 termes ajoutés au
`vocabulary` de l'auteur dans `%APPDATA%\Plume\config.json` (noms de clients et jargon métier propres à
son activité professionnelle, non détaillés ici). Les termes déjà présents n'ont pas été dupliqués.

---

## Pack de styles « Jira & Confluence » ✅ (livré le 2026-07-16, config uniquement — pas de bump de version)
Ticket exécuté tel quel : `docs/superpowers/plans/2026-07-16-styles-jira-confluence.md`. Aucun changement
de code — 4 styles avec voix ajoutés au perso de l'auteur dans `%APPDATA%\Plume\config.json`
(`selectionStyles`), résultat en fenêtre : **Ticket Jira**, **Commentaire de résolution**,
**Réponse client**, **Page Confluence**. Ne figurent pas dans les intégrés (`StyleCatalog`) — propres
à l'auteur, pas des défauts pour les nouvelles installs. Prompts de référence :
`docs/prompts/style-ticket-jira.md`, `style-commentaire-resolution.md`, `style-reponse-client.md`,
`style-page-confluence.md`.

---

## 5.3.7 — Liens de création des clés API cliquables ✅ (livrée le 2026-07-16)
Demande utilisateur : guider la création des clés au moment de la configuration.
- **Réglages → Général** : les sous-titres des lignes « Clé Groq » et « Clé OpenRouter » contiennent
  désormais un lien cliquable (`console.groq.com/keys`, `openrouter.ai/keys`) — gestionnaires dans
  `GeneralPanel.xaml.cs` (les handlers liés en XAML doivent vivre dans la classe du fichier), `OpenUrl`
  sans crash si aucun navigateur.
- **Prise en main (onboarding)** : l'étape 1 avait déjà le lien Groq ; le panneau alternatif
  « J'ai plutôt une clé OpenRouter » gagne le lien `openrouter.ai/keys`.

---

## 5.3.6 — Invitation d'un collègue : email refait + installateur en pièce jointe ✅ (livrée le 2026-07-16)
Demande utilisateur. Le bouton « Inviter un collègue » (pied des Réglages) envoyait un mailto au texte
minimal pointant vers un lien de téléchargement fictif (jamais mis en service).
- **Nouveau texte** (`UsageStatsService.InviteSubject`/`BuildInviteBody`) : présentation concrète
  (gains, styles, assistant, prise en main guidée) + avertissement SmartScreen expliqué (installateur
  non signé, décision du 2026-07-16). Deux variantes testées : avec PJ / sans PJ (« demande-moi »).
- **Pièce jointe** : brouillon **Outlook** composé en COM liaison tardive (`Outlook.Application`,
  aucune dépendance, jamais d'envoi automatique — `Display` seulement) avec `PlumeSetup-*.exe` joint.
  Repli : mailto sans PJ (protocole incapable de joindre) si Outlook est absent ou échoue.
- **Source de la PJ** : l'installateur **se recopie** dans `%LOCALAPPDATA%\Plume` à l'installation
  (`plume.iss`, `Source: "{srcexe}"`, `DestName` figé) — chaque poste installé peut essaimer sans
  serveur de distribution. Limite assumée : la PJ reste à la version de la dernière **installation**
  (un `deploy.ps1` de dév ne la rafraîchit pas).

---

## 5.3.5 — Clôture du backlog : section « À venir » retirée de l'onglet À propos ✅ (livrée le 2026-07-16)
Décision utilisateur : le backlog est intégralement clôturé (tout livré ou écarté), Plume passe en
mode maintenance. La section « À venir » de l'onglet À propos (`AProposPanel.xaml`) était de plus
totalement périmée — chaque entrée était soit livrée depuis longtemps (question à l'IA sur sélection,
modification à la voix → V4), soit officiellement écartée (profil par application → retiré en V6,
snippets vocaux → couverts par les remplacements, transcription locale → V5 abandonnée) : supprimée.
L'onglet À propos ne contient plus que la version, la prise en main et les Nouveautés.
`docs/roadmap.md` : les « Idées ultérieures » (historique des dictées, mode chuchoté) sont clôturées
avec le reste — la spec de cadrage reste consultable si un besoin réel apparaît un jour.

---

## 5.3.4 — Dette technique : CA1305/CA1310 sur les données persistées ✅ (livrée le 2026-07-16)
Aucun changement de comportement (vérifié : build 0 warning, 260 tests unitaires verts).
- **`DailyUsageService`** : la clé de date `yyyy-MM-dd` du fichier `usage-daily.json` est désormais
  produite et relue via `CultureInfo.InvariantCulture` (`Record` et `TryParse`) — jamais affectée par
  la locale Windows de la machine, même si elle change un jour.
- **Reste volontairement en l'état** : les `ToString("0.0")` de température dans `SettingsWindow`
  (affichage UI, fr-FR assumé partout) — corriger uniquement les données **persistées**, pas l'affichage.
- **`.editorconfig`** : `CA1310` passé en `warning` (tous les usages existants précisaient déjà
  `StringComparison` — plus aucune régression possible) ; `CA1305` reste en `suggestion` car les
  `ToString` de température UI restent volontairement non corrigés (le passer en `warning` casserait
  le build à 0 warning).

---

## 5.3.3 — Dette technique : découpage des gros fichiers ✅ (livrée le 2026-07-16)
Aucun changement de comportement (vérifié : build 0 warning, 260 tests unitaires verts, smoke test UI
réel — construction de `SettingsWindow`, parcours des 4 onglets, ajout d'un mot de vocabulaire et
d'une règle de remplacement, édition + sauvegarde d'un style — le tout persisté correctement).
- **`DictationController.cs`** (~950 lignes) → classes partielles par flux : `DictationController.cs`
  (état, démarrage/arrêt de l'écoute, warm-up réseau), `.Dictation.cs` (dictée classique — extraction
  de `ProcessDictation`, symétrique à `ProcessAgent`/`ProcessCommand`), `.Styles.cs` (style écrit/avec
  voix, Commande vocale), `.Agent.cs` (assistant vocal, fenêtre de réponse).
- **`SettingsWindow.xaml`** (~1 200 lignes, le plus gros fichier du dépôt) → un `UserControl` par onglet
  sous `Views/SettingsPanels/` (`GeneralPanel`, `DictionnairePanel`, `StylePanel`, `AProposPanel`).
  Les éléments nommés restent accessibles depuis `SettingsWindow` via le champ généré par WPF
  (modificateur par défaut `internal`, même assembly — ex. `PanelGeneral.SttCombo`), sans changer un
  seul binding ni style.
- **`SettingsWindow.xaml.cs`** (~930 lignes) → classes partielles par onglet (`.General.cs`,
  `.Dictionnaire.cs`, `.Styles.cs`, `.APropos.cs`) référençant leur panneau. `DictionnairePanel` est
  entièrement autonome (vocabulaire, remplacements, événement `Changed`) ; les deux gestionnaires liés
  en XAML dans un `DataTemplate` (favori et « visible dans le menu » de la liste des styles) vivent
  directement sur `StylePanel` — contrainte WPF, ils sont résolus contre la classe du fichier XAML qui
  les déclare — et relaient vers `SettingsWindow.Styles.cs` via ce même événement.
- Carte du code de `CLAUDE.md` mise à jour en conséquence.

---

## 5.3.2 — Corrections de l'audit du 2026-07-16 + outillage analyzers ✅ (livrée le 2026-07-16)
Deux correctifs issus d'une revue de robustesse complète (agent de revue + contre-vérification), plus
de l'hygiène de dépôt.
- **Échap sur les styles écrits** : `ApplyWrittenStyleAsync` n'enregistrait pas Échap et appelait
  `CleanAsync` avec `CancellationToken.None` — spinner « Application… » inarrêtable jusqu'à 30 s si
  le réseau rame. Aligné sur le motif des autres flux (jeton de session `++_sessionId`, `_cts` local,
  garde `session == _sessionId` dans le `finally`, restauration du presse-papiers sur annulation).
- **Restauration du presse-papiers sérialisée** (`InsertionService`) : deux insertions en < 400 ms
  faisaient se chevaucher les timers de restauration (le presse-papiers final pouvait garder un texte
  dicté intermédiaire, ou écraser le collage en cours). Désormais : une nouvelle insertion annule le
  timer en attente et hérite de son instantané (le VRAI presse-papiers de l'utilisateur) ; si la mise
  au presse-papiers échoue, la restauration héritée est ré-armée (jamais perdue).
- **Analyzers .NET activés** (`Directory.Build.props` : `AnalysisLevel=latest-recommended` +
  `EnforceCodeStyleInBuild`) + `.editorconfig` (politique : CA1707 assumé → none ; existant toléré →
  suggestion IDE, listé à résorber au fil de l'eau ; toute nouvelle règle reste bloquante). Build 0 warning.
- **Hygiène docs** : « État courant » de `roadmap.md` compacté (l'historique par version vit ici,
  ~2 300 tokens gagnés par session IA), README réécrit (installateur + onboarding Groq, périmètre réel),
  dette technique complétée (`SettingsWindow.xaml` ~1 150 lignes à découper aussi).

---

## 5.3.1 — Warm-up réseau étendu à OpenRouter ✅ (livrée le 2026-07-16)
Ticket : `docs/superpowers/plans/2026-07-11-warmup-openrouter.md`, issu de l'audit de latence du
2026-07-11 (`docs/audits/2026-07-11-audit-latence.md`) : le nettoyage payait ~+550 ms de connexion
à froid vers `openrouter.ai` sur la 1re dictée après une pause (1 177 ms mesurés contre ~630 ms à chaud),
seul `api.groq.com` étant préchauffé.
- `WarmUpGroqAsync()` → `WarmUpAsync()` dans `DictationController` : préchauffe **Groq et OpenRouter
  en parallèle** (via `WarmUpHostAsync`, requête `GET /models` légère, timeout propre de 10 s chacun,
  early-return silencieux sans clé, `HttpClient` partagé `_http` — c'est son pool qu'on chauffe).
- Points d'appel inchangés (constructeur + début d'écoute), toujours fire-and-forget, ne lève jamais.
- Aucune attente ajoutée sur le chemin de latence ni au démarrage (règle primordiale respectée).

---

## 5.3.0 — Double-clic sur la pastille = dictée ✅ (livrée le 2026-07-15)
Spec : `docs/superpowers/specs/2026-07-15-double-clic-pastille-dictee-design.md`. Rouvre, sur demande
explicite de l'utilisateur, l'idée mise en réserve le 2026-07-03 (crainte de perdre la sélection/le
focus — infondée : la pastille est `WS_EX_NOACTIVATE` et ne vole jamais le focus).
- **Double-clic sur la pastille (repos)** → démarre la dictée en bascule (`ToggleDictation(0, target)`,
  fenêtre cible capturée au clic via `GetForegroundWindow`) ; fonctionne aussi en mode « maintien »
  (vk = 0 → la surveillance de relâchement se coupe d'elle-même, comportement bascule).
- **Re-clic pendant l'écoute** → arrêt immédiat (comportement préexistant, inchangé, sans délai).
- **Compromis assumé (règle primordiale respectée : utilisateur prévenu)** : le menu des styles
  s'ouvre désormais après le délai système de double-clic (`GetDoubleClickTime`, ~500 ms) au lieu
  d'instantanément, pour laisser sa chance au 2e clic. Alternatives écartées : menu instantané
  refermé au 2e clic (flash), simple clic = dictée (cassait l'habitude du menu).
- Implémentation : `BadgeWindow` émet `DoubleClicked` (jamais sur un glisser) ; `App.xaml.cs` porte
  le minuteur unique simple/double clic. Sous-titre du réglage « Pastille flottante » mis à jour.

---

## 5.2.4 — Corrections de l'audit complet du 2026-07-10 + refonte de l'onglet Dictionnaire (2026-07-15)
Plan : `docs/superpowers/plans/2026-07-10-corrections-audit.md`. 11 correctifs livrés (Tasks 1, 3 à 12 —
la Task 2 n'a pas eu besoin d'être relivrée, déjà couverte par la 5.2.1) issus d'un audit complet
(fiabilité dictée/presse-papiers, anti-crash, DPI multi-écrans, clés API, hygiène docs), plus une
refonte de l'onglet Dictionnaire.
- **AudioCaptureService ne lève plus jamais + filet anti-crash global** (Task 1) : `Stop()`/`Cleanup()`
  avalent toute exception matérielle (micro débranché en pleine dictée) ; nouveau
  `DispatcherUnhandledException` global (`App.OnStartup`) journalise et empêche tout crash du process.
- **Retentatives presse-papiers sur le chemin critique** (Task 3) : `ClipboardSnapshot.WithRetry`
  (2 reprises espacées de 60 ms) appliqué à `Capture`/`Restore`/`SetTextExcludedFromHistory` — un
  verrou transitoire (OneDrive, Office) ne fait plus échouer la dictée ni perdre le presse-papiers
  original de l'utilisateur.
- **Durée max atteinte sans disposer le micro depuis son propre callback** (Task 4) : `BeginInvoke`
  remplace `Invoke` sur `MaxDurationReached` (le `WaveInEvent` n'est plus disposé pendant que son
  `DataAvailable` s'exécute encore — pattern déconseillé par NAudio).
- **Jeton de session anti-course** (Task 5) : nouveau `_sessionId` dans `DictationController` — un
  `Cancel()` suivi d'une relance rapide ne laisse plus le `finally` tardif d'un flux périmé écraser
  l'état de la nouvelle session (pastille, Échap, `CancellationTokenSource` capturé en variable locale
  et disposé proprement).
- **Écritures atomiques** (Task 6) : `config.json` et `usage-daily.json` passent désormais par un
  fichier `.tmp` puis `File.Move` — un arrêt forcé en pleine écriture ne corrompt plus le fichier
  (plus de perte silencieuse des clés API ou de l'historique des stats).
- **Clés API exigées selon le besoin réel** (Task 7) : nouvelle méthode pure
  `ConfigService.MissingKeyMessage` — une configuration 100 % Groq (STT + modèle de nettoyage
  `groq/…`) ne réclame plus de clé OpenRouter ; message ciblé (Groq ou OpenRouter) selon le modèle
  réellement utilisé par le flux. `CLAUDE.md` corrigé en conséquence (routage statique, pas de repli
  automatique sur erreur).
- **Multi-écrans à DPI différents** (Task 8) : nouveau `Services/ScreenMetrics.cs` (conversion pixels
  physiques ↔ DIP par moniteur, PerMonitorV2) utilisé dans `App.ReconcileBadges`/`SaveBadgePosition` et
  `StyleMenuWindow` — pastille et menu des styles ne sortent plus de la zone visible sur un setup à
  échelles mixtes (portable 150 % + écran externe 100 %, cas courant).
- **Éditeur de styles : plus de perte de modification en cours** (Task 9) : `CommitStyleEdits` reporte
  désormais silencieusement les champs valides vers le style édité au changement de sélection et à la
  fermeture des Réglages (un brouillon de nouveau style reste soumis au bouton explicite).
- **Petites malfaçons** (Task 10) : couleurs de la pastille (fond des glyphes, halo bleu) passées en
  tokens `Colors.Light/Dark.xaml` + `DynamicResource` ; `HttpResponseMessage` désormais disposés dans
  `TranscriptionService`/`CleanupService`/`AgentChatService` ; trace de diagnostic sur un échec silencieux
  de `RegisterEscape` (Échap déjà pris par une autre app).
- **Tests complémentaires + hygiène documentaire** (Task 11) : cas limites `HotkeyParser` (modificateur
  inconnu, touche nulle/« None », synonymes/casse) et `ReplacementService` (remplacement vers vide,
  limite `\b` connue, « Vers » multiligne composé avec les sauts de ligne auto) ; Carte du code de
  `CLAUDE.md` complétée (fichiers manquants : `ClipboardSnapshot`, `ModelInfo`, `ScreenMetrics`,
  `TickSoundService`, `BadgePlacementService`, etc.) ; item V8-C de la roadmap tranché comme abandonné
  avec le lot I (même principe de récupération après coup).
- **Refonte de l'onglet Dictionnaire** (Task 12) : vocabulaire en **nuage de jetons** (ajout par bouton
  ou touche Entrée, × pour retirer) ; règles de remplacement en **lignes-cartes** cohérentes avec
  l'onglet Styles (raccourci en semibold, aperçu du texte inséré, `↵` pour le multiligne, × au survol +
  touche Suppr) ; ajout d'une règle au clavier via Ctrl+Entrée dans « Texte inséré » ; états vides
  quand les listes sont vides. Zéro nouveau token de couleur, zéro animation ajoutée.
- Tests : 260/260 au vert (croissance progressive au fil des tâches : retentatives presse-papiers,
  écriture atomique, clés API manquantes, cas limites `HotkeyParser`/`ReplacementService`).
- Impact RÈGLE PRIMORDIALE : nul — corrections chirurgicales sur des chemins d'erreur/cas limites et
  une refonte visuelle d'un onglet existant ; aucun ajout au démarrage ni au chemin nominal de la dictée.

---

## V1 — Dictée classique ✅
Capture (Alt+Espace) → transcription → nettoyage FR → insertion au curseur. Tray + réglages. Léger, rapide, simple.
→ détail dans `cahier-des-charges-v1.md`.

---

## V2 — Dictionnaire personnel ✅ (livrée le 2026-06-19)
**Le besoin n°1 après la V1.** Ajouter ses propres mots pour que la dictée ne les massacre plus (noms propres, jargon métier, acronymes : « SFTP », « PGP »…).

- UI dans les réglages (onglet **Dictionnaire**) : liste de mots + règles, ajout / suppression. ✅
- Le dictionnaire est injecté à **deux endroits** :
  1. **STT** : passé en *passthrough provider* (best-effort ; OpenRouter n'expose pas de `prompt` direct). ✅
  2. **Nettoyage LLM** : fourni au system prompt pour qu'il n'altère pas ces termes (voie fiable). ✅
- **Règles de remplacement** : `raccourci → texte` (ex. « emevem » → « exemple.fr »), substitution exacte. ✅

*Détail : `docs/superpowers/specs/2026-06-19-plume-v2-design.md`.*

Également livré avec la V2 :
- **Maintien enfoncé (push-to-talk)** en plus du mode toggle (réglage). ✅
- **Sélection du micro** dans les réglages. ✅

---

## V2.5 — Refonte visuelle & finitions ✅ (livrée le 2026-06-20)
*Direction « Apple épuré », clair. Détail : `docs/superpowers/specs/2026-06-20-plume-v2.5-design.md`.*

Rafraîchir l'esthétique (pastille d'écoute, fenêtre Réglages, icône tray) : typographie, couleurs, coins arrondis, espacements, finitions.

Inclut aussi :
- **Onglet « À propos »** : numéro de version + changelog résumé (version d'assembly propre).
- **Correctif bug — fenêtres Réglages multiples** : réactiver l'instance ouverte au lieu d'en créer une autre.
- **Sélecteur de modèles (STT + nettoyage)** : liste déroulante curatée (prix par 1M tokens, coût estimé par dictée, note de fiabilité en étoiles), saisie custom possible. Prix/notes statiques (zéro réseau).

Contraintes tenues : zéro dépendance UI tierce, zéro chargement au démarrage, retour visuel < 150 ms préservé.

---

## V3 — Nettoyage personnalisable

### A — Nettoyage personnalisable ✅ (livrée le 2026-06-20)
*Détail : `docs/superpowers/specs/2026-06-20-plume-v3-nettoyage-personnalisable-design.md`.*
- **Profils de style** : chaque profil = nom + prompt de nettoyage éditable ; bascule rapide via le sous-menu tray. ✅
- **Prompt de nettoyage éditable** + « Réinitialiser au défaut ». ✅
- Profils livrés : **Brut** (sans nettoyage) et **Standard**. L'ancienne case « Activer le nettoyage » remplacée par le profil Brut (migration auto). ✅

### B — Snippets vocaux ❌ (abandonnée le 2026-06-29 — déjà couvert par l'existant)
Le besoin réel (« insérer une URL / un texte fixe sur un déclencheur ») est **déjà couvert par les Règles de remplacement** de la V2. Une fonctionnalité dédiée ferait doublon.
- **Limites connues** : le déclencheur doit survivre à la transcription + au nettoyage (préférer une phrase naturelle) ; les **blocs multi-lignes** dans le champ « Vers » sont le seul cas qui pourrait un jour rouvrir le sujet (→ repris par V8-E).

---

## V3.5 — Finitions & confort

### A — Réglages Général ✅ (livrée le 2026-06-20)
Toggle « Démarrer avec Windows » (défaut ON) · clé API allégée (« Clé configurée ✓ » + Modifier) · menus de modèles au clic sur toute la ligne · catalogue de nettoyage élargi (Gemini, DeepSeek, Kimi, Qwen) · bouton « Copier le dernier texte » (mémoire seule).

### B — Raccourci par profil de style ✅ (livrée le 2026-06-20)
*Détail : `docs/superpowers/specs/2026-06-20-plume-v3.5-B-raccourci-par-profil-design.md`.* Raccourci dédié optionnel par profil, conflits détectés. (Supprimé depuis par la refonte V6.)

### C — Stats d'usage & bouton Partager/Inviter ✅ (livrée le 2026-06-21)
Stats (dictées, mots, temps gagné ~40 mots/min) dans l'onglet Statistiques + bouton « Inviter un collègue » (mailto pré-rempli). (Onglet remplacé par le pied de sidebar via le plan V7.)

### D — Auto-enregistrement ✅ (livrée le 2026-06-21)
Suppression des boutons « Enregistrer » ; chaque changement persiste à la volée (perte de focus / changement de valeur, pas à chaque frappe).

### E — Overlay simplifié ✅ (livrée le 2026-06-21)
Cycle réduit à 2 états : Écoute (barres statiques, spinner pendant la préparation du micro) → Traitement (spinner). Vu-mètre animé écarté (trop lourd pour le gain). (Overlay supprimé ensuite par le plan V7 : la pastille intègre les états.)

### F — Mode sombre ✅ (livrée le 2026-06-21)
Thème sombre complet suivant automatiquement Windows, à chaud. Tokenisation des couleurs (`Colors.Light/Dark.xaml` + brushes `DynamicResource`).

### G — Profil restreint à une application ✅ (livrée le 2026-06-29, 4.3.0 — **supprimée par la refonte V6**)
Un profil pouvait porter une « Application associée » (Outlook / Teams / Navigateur) : son raccourci ne se déclenchait que si cette app était au premier plan. Détection P/Invoke `QueryFullProcessImageName` + mapping pur testable (`ForegroundAppService`). **Retiré en 5.0.0** avec la suppression des raccourcis par style. Ne pas rouvrir.

### Lot 1 — Finitions / dette ✅ (livré le 2026-06-29, 4.3.0)
1. Nettoyage du code de rebind mort (`HotkeyService.TryRegister`, `DictationController.TryRebindHotkey`…).
2. Fin des écritures redondantes à l'ouverture de l'onglet Style (comparaison au dernier état persisté).
3. **Température par profil** : champ `Temperature` (défaut 0.2) + curseur « Créativité du nettoyage », transmis à `CleanupService.CleanAsync` (borné [0,1]).
4. **Durée d'écoute configurable** (`MaxListeningSeconds` 30/60/120/300, défaut 60) + son discret à la coupure auto.

### H — « Copier le dernier texte » dans le menu tray ✅ (livrée le 2026-06-26)
Entrée tray « Copier le dernier message » (`DictationController.LastText`, mémoire seule), grisée tant qu'aucune dictée, notification « Copié ✓ ».

### I — « Vérifier s'il existe un modèle plus récent » ✅ (livrée le 2026-06-26)
Bouton (Général → Modèle) : `GET https://api.groq.com/openai/v1/models` (`GroqModelsClient`) + heuristique famille/version (`ModelUpdateService.FindNewer`) + « Mettre à jour » en un clic. Limite assumée : détection par nom de modèle, Groq uniquement. Réseau au clic seulement. Tests : `ModelUpdateServiceTests` (18 cas), `GroqModelsClientTests`.

---

## V4 — Reformuler / commander un texte existant

### A — Reformuler la sélection avec un style ✅ (livrée le 2026-06-26)
*Détail : `docs/superpowers/specs/2026-06-26-plume-v4-A-reformuler-la-selection-design.md` ; révisée par V4-A bis (`…-v4-A-bis-mode-par-profil-design.md`) puis par la refonte « profils indépendants » (`2026-06-29-plume-profils-independants-activables-design.md`).*
Sélectionner un texte, presser un raccourci lié à un style → Plume réécrit la sélection selon le prompt du style et la remplace. Déroulé : mémoriser le presse-papiers → Ctrl+C → LLM → collage → restauration. Nouveau `SelectionService`.

### B — Mode commande vocale ✅ (livrée le 2026-06-30, 4.6.0)
*Détail : `docs/superpowers/specs/2026-06-30-plume-v4-B-commande-vocale-design.md`.*
Sélectionner un texte, **dicter une instruction** (« mets en liste »…) → appliquée à la sélection puis remplacée. « Agent IA » renommé « Commande écrite ». Prompt-cadre éditable.

### C — Assistant vocal général ✅ (livrée le 2026-06-29, 4.4.0)
*Détail : `docs/superpowers/specs/2026-06-29-plume-v4-C-assistant-vocal-design.md`.*
Raccourci global dédié (défaut Ctrl+Alt+A, personnalisable, activable) → question à la voix → réponse dans une fenêtre (`AnswerWindow`), dialogue multi-tours, date/heure injectée au prompt. Nouveaux : `AgentChatService`, `AgentSession`. Aucun lien avec la sélection. (Idée initiale « question sur une sélection » abandonnée le 2026-06-29.)

### D — Finitions assistant vocal ✅ (livrée le 2026-06-30, 4.5.0)
*Détail : `docs/superpowers/specs/2026-06-30-plume-v4-Cplus-finitions-assistant-design.md`.*
(1) Rendu markdown léger des réponses (`LightMarkdown`, parseur pur testé) ; (2) bouton « Insérer au curseur » (fenêtre d'origine figée au début de conversation, insertion non destructive).

### E — Stats en bas de la sidebar + filtre par période
**Décision 2026-06-30** ; intégrée au **plan V7** (tâches 8-9) : suppression de l'onglet Statistiques, bloc compact en pied de sidebar (temps gagné + dictées/mots + segments Sem·Mois·Tot), « Inviter » déplacé, agrégats journaliers `usage-daily.json` (jamais lu au démarrage, élagage > 13 mois). « Entre deux dates » abandonné (besoin de niche).

---

## V6 — Refonte « une dictée, un menu » ✅ (livrée le 2026-07-02, version 5.0.0)
*Spec : `docs/superpowers/specs/2026-07-02-plume-v6-refonte-styles-design.md` · Plan : `docs/superpowers/plans/2026-07-02-plume-v6-refonte-styles.md`.*

Un seul raccourci de dictée global (prompt de base + créativité dans Général) ; tous les autres styles deviennent des entrées du **menu de sélection** ouvert par la **pastille flottante** (`BadgeWindow`, créée en différé → coût nul au démarrage ; bouton Stop pendant l'écoute) → `StyleMenuWindow` (Démarrer la dictée + favoris + autres). Styles **écrits** ou **« avec voix »** (instruction dictée ; sélection optionnelle → insertion au curseur si rien n'est sélectionné). 9 styles intégrés éditables (dont **Affiner** et **Basculer tutoiement/vouvoiement**). Migration automatique des anciens profils. Suppressions : `ProfileMode`, raccourcis par style, `TargetApp`, `ProfileService`/`ProfileCatalog`/`ForegroundAppService`. Tests : 164/164 verts.

- **V6-A (pastille + menu)** ✅ incluse dans la refonte. Écarté définitivement : hook souris global / clic droit universel (charge permanente + conflits).
- **V6-B (modèle IA par style)** ✅ incluse : `SelectionStyle.Model` (null = modèle global), menu « Modèle » dans l'éditeur, résolution « modèle du style, sinon global » dans `DictationController`.
- **V6-C (maintien/tap par style)** ❌ caduque (2026-07-03) : plus de raccourci par style ; le mode global (Général) et le déclenchement à la souris couvrent le besoin.

---

## Correctifs immédiats avant V7 ✅ (livrés le 2026-07-03, version 5.0.1)
Quatre correctifs promus en priorité absolue devant la refonte V7, chacun livrable seul. Tests : 169/169 verts.

### V8-G — Réactivité à l'appui du raccourci ✅
`StartListening` ouvrait le micro (`AudioCaptureService.TryStart`, synchrone, 100-500 ms avec un casque Bluetooth) **avant** d'afficher le moindre retour visuel. Inversion : le spinner (`_overlay.ShowPreparing()`) s'affiche **immédiatement** à l'appui, l'ouverture du micro se fait ensuite sur un thread d'arrière-plan (`Task.Run` + `OnMicOpened` sur le thread UI). Courses gérées explicitement : annulation (Échap) ou 2e appui (Stop) pendant l'ouverture asynchrone sont mémorisés (`_cancelMicOnOpen`/`_stopRequestedDuringOpen`) et rejoués une fois le micro prêt, sans jamais toucher `AudioCaptureService` depuis deux threads à la fois. `WaveInEvent.BufferMilliseconds` réduit à 40 ms (défaut NAudio 100 ms) → 1er buffer audio (`CaptureStarted`, barres vertes) ~2× plus tôt. Mesure : `Stopwatch` en debug (appui → micro ouvert → premier buffer), loggué dans `%TEMP%\plume-timing.log`.

### V8-I-0 — Durcir les prompts par défaut ✅
Trois invariants ajoutés au prompt de dictée de base (`StyleCatalog.DictationPrompt`) et au gabarit commun des styles écrits (`EcritSuffixe`, `CommandPrompt`) : tutoiement/vouvoiement conservé strictement (sauf **Basculer**, dont c'est la fonction), vocabulaire du locuteur jamais remplacé (dictée de base uniquement — incompatible avec des styles comme Affiner/Raccourcir qui reformulent volontairement), correction phonétique des mots incohérents (probable erreur de transcription). **Migration douce** dans `StyleService.Normalize` (`MigrateHardenedPrompts`) : un prompt persisté dans `config.json` encore identique mot pour mot à l'ancien défaut (constantes `Legacy*`) est remplacé par le nouveau ; un prompt personnalisé par l'utilisateur, même légèrement, n'est jamais touché.

### V8-H (point 0) — Presse-papiers : ne plus effacer images/fichiers ✅
`InsertionService.Paste`/`PasteAndRestore` et `SelectionService` ne sauvegardaient que le texte du presse-papiers (`Clipboard.GetText()`) avant de coller/capturer, puis l'effaçaient à la restauration — une capture d'écran ou des fichiers copiés juste avant une dictée étaient perdus. Nouveau type partagé `Models/ClipboardSnapshot.cs` (texte | image | fichiers | vide), `Capture()`/`Restore()` ne levant jamais, utilisé par `InsertionService` **et** `SelectionService` (même logique aux deux endroits, `CapturedSelection.Original` remplace `OriginalClipboard`/`OriginalHadText`). Formats exotiques (objets Office, rendu différé) restent hors périmètre, assumé.

### V8-H (point 1) — Presse-papiers : dictée exclue de l'historique Windows ✅ *(ajouté le 2026-07-03, demande explicite de l'utilisateur)*
Le texte dicté transitait par le presse-papiers réel (texte, format standard) le temps du Ctrl+V : Windows le capturait donc dans l'historique (Win+V) et le synchronisait potentiellement au presse-papiers infonuagique, malgré la restauration immédiate après coup. Correctif : `ClipboardSnapshot.SetTextExcludedFromHistory(text)` construit un `DataObject` portant, en plus du texte, les formats `ExcludeClipboardContentFromMonitorProcessing` (exclusion complète, historique + synchro), `CanIncludeInClipboardHistory=0` et `CanUploadToCloudClipboard=0` (mêmes formats que ceux utilisés par Chrome en navigation privée) — repli silencieux sur `Clipboard.SetText` classique si l'écriture échoue (le collage prime toujours sur la confidentialité de l'historique). Utilisé pour le texte inséré (`InsertionService.PasteAndRestore`) et pour le sentinel de capture de sélection (`SelectionService.CaptureSelectionAsync`). Zéro dépendance, alias `DataObject` ajouté à `GlobalUsings.cs` (ambiguïté WPF/WinForms, même motif que `Clipboard`/`Application`/`MessageBox`).

> **Reste : smoke test manuel (UI)** — checklist dans le plan V6 (comportements souris/focus non couverts par les tests automatiques), surtout le mode maintien (cf. mémoire V4-B §9).

---

## V7 — Refonte design « Granola » ✅ (livrée le 2026-07-04, version 5.1.0)
*Plan : `docs/superpowers/plans/2026-07-03-plume-v7-refonte-design-granola.md`.*

Refonte visuelle et d'ergonomie de l'ensemble de l'application, plus suppression de l'overlay séparé au profit d'une pastille qui porte tous les états. Inclut **V4-E (stats sidebar)** ✅.

- **Thème sombre complet** : barre de titre (Réglages, `AnswerWindow`) sombre en thème sombre / claire en thème clair, bascule à chaud avec le reste de l'app ; ascenseur fin et thémé partout (Réglages, menus déroulants, réponse assistant), palette `Colors.Light/Dark.xaml` étendue en conséquence.
- **Pastille = seul retour visuel** : `OverlayWindow` supprimée, tous ses états (spinner préparation → écoute → traitement → repos) migrés sur `BadgeWindow` ; plus aucun popup pendant la dictée. Halos de couleur autour de la pastille (vert = écoute, bleu = traitement), clic pendant l'écoute = stop, curseur ■ au survol pendant l'écoute. Pastille toujours au premier plan, y compris après passage d'une app topmost ; réapparaît pendant la dictée même si masquée par réglage, puis se recache.
- **Réglages réorganisés** : 4 onglets avec icônes et sous-titres, ordre logique ; bloc stats (Sem/Mois/Tot) + bouton « Inviter » déplacés en pied de sidebar (V4-E, remplace l'ancien onglet Statistiques, agrégats journaliers `usage-daily.json`, élagage > 13 mois).
- **Styles réordonnables** : glisser-déposer dans l'éditeur de styles, ordre persisté dans `config.json` et appliqué au menu de la pastille (favoris d'abord, puis les autres, chacun dans l'ordre manuel).
- **Éditeur de style** : case « instruction dictée » réservée à la Commande vocale (absente pour les autres styles et à la création), sans changement de comportement pour les styles existants.
- Cohérence visuelle étendue à `StyleMenuWindow` et `AnswerWindow` (mêmes tokens, mêmes ascenseurs).

Tests automatiques verts sur toute la refonte (logique pure : ordre des styles, migration, catalogues). **Reste, à la charge de l'utilisateur** : smoke test manuel UI complet (checklist du plan V7 — démarrage instantané, rendu de la barre de titre sombre/claire, halos, drag-and-drop des styles, retours visuels des styles « avec voix »/commande écrite/assistant), non exécutable en environnement headless.

---

## Correctifs post-V7 ✅ (livrés le 2026-07-04, version 5.1.1)

### Pastille hors écran après changement de configuration d'écran
Signalé lors du smoke test manuel de la V7 : la pastille restait invisible, sa position mémorisée (`config.BadgePosition`) ne correspondant plus à l'écran courant (résolution différente de celle où la position avait été enregistrée). Corrigé ponctuellement en réinitialisant la position enregistrée (repli automatique vers un coin visible de l'écran actuel, logique déjà présente dans `BadgeWindow`). Le vrai correctif structurel (recadrage automatique à chaque démarrage/changement d'écran) reste à faire : voir V8-J point 2 (« jamais hors écran »), non traité par cet incident.

### Barres vertes animées selon le niveau vocal ✅ *(décision utilisateur 2026-07-04, réouvre un point écarté en V7/V8-K)*
Pendant l'écoute, les 3 barres vertes de la pastille bougent désormais selon le niveau de la voix captée, au lieu d'une hauteur figée. Réutilise le niveau déjà calculé par `AudioCaptureService.LevelChanged` (pic brut par bloc audio, aucun calcul supplémentaire) : `DictationController` relaie l'évènement (`LevelChanged`), `App.xaml.cs` le transmet à `BadgeWindow.SetLevel(float)`, qui lisse la valeur (moyenne exponentielle légère, évite le tremblement) et ajuste la hauteur des 3 barres (facteurs différents par barre pour un rendu naturel, barre du milieu plus réactive). Actif uniquement pendant l'état Écoute ; aucun coût hors dictée.

---

## V8-D — Pré-chauffe réseau ✅ (livrée le 2026-07-04, version 5.1.2)
*Spec : `docs/superpowers/specs/2026-07-04-plume-v8-D-prechauffe-reseau-design.md` · Plan : `docs/superpowers/plans/2026-07-04-plume-v8-D-prechauffe-reseau.md`.*

Un préchauffage de la connexion Groq existait déjà (`WarmUpGroqAsync`, commit `656c987` du 2026-06-21), mais uniquement **au démarrage de l'app** — il ne profitait donc qu'à la toute première dictée après le lancement, le pool de connexions .NET évinçant une connexion inactive après quelques minutes. Plume tournant en tâche de fond toute la journée, la quasi-totalité des dictées repayaient le coût plein de la reconnexion.

Correctif : un second appel de la **même méthode existante, inchangée**, ajouté en tête de `StartListening` (`DictationController.cs`), pour tous les modes (Dictée, Commande vocale, Assistant). Fire-and-forget, aucun impact sur la réactivité (< 150 ms) ni sur le démarrage, aucun nouveau réglage. Découvert en cadrage que la roadmap décrivait initialement un nouveau mécanisme (requête `HEAD` dédiée) — révisé pour réutiliser l'existant plutôt que dupliquer la logique réseau.

Lot **V8-A** (arrêt automatique sur silence), initialement cadré avant D, a été abandonné le même jour : le besoin de stop rapide est déjà couvert par le clic sur la pastille en écoute (V7) ; le risque de couper une pause de réflexion pendant la dictée a été jugé trop élevé pour la valeur apportée.

Tests : 173/173 verts (aucun nouveau test requis — second site d'appel d'une méthode déjà testée en production, sans nouvelle logique). Revue de code (tâche + finale) : Approved, 0 Critical/Important. Smoke test manuel réseau effectué par l'utilisateur (dictées après pause de 3-4 min) : aucune régression observée ; le gain (une centaine de ms théoriques) n'est pas mesurable de façon fiable manuellement, noyé dans la variance naturelle des temps de réponse Groq (plusieurs centaines de ms d'un appel à l'autre, indépendamment du réseau local).

---

## V8-K — Sons discrets de début et fin d'écoute ✅ (livrée le 2026-07-04, version 5.1.3)
*Spec : `docs/superpowers/specs/2026-07-04-plume-v8-K-sons-discrets-design.md` · Plan : `docs/superpowers/plans/2026-07-04-plume-v8-K-sons-discrets.md`.*

Deux tops sonores très courts, désactivés par défaut, réglables dans Général → Sons (interrupteur + choix du son parmi Aucun/Discret/Clair, indépendant pour le début et la fin) : un au moment où le micro devient prêt (`AudioCaptureService.CaptureStarted`, tous modes d'écoute — Dictée/Commande vocale/Assistant), un à l'insertion réussie d'une dictée classique uniquement (pas Commande vocale ni Assistant, scope décidé en cadrage). Aucun fichier audio committé : `TickSoundService.GenerateWav` synthétise une sinusoïde courte avec enveloppe fade-in/fade-out, encapsulée en WAV via `WavUtils.WriteWav` déjà existant. Nouveaux `SoundCatalog` (métadonnées) et `TickSoundService` (synthèse + lecture), `InsertionService.Paste` change de `void` à `bool` pour exposer le succès du collage au flux de dictée.

**Bug découvert en vérification manuelle, corrigé le jour même (hors plan, via `superpowers:systematic-debugging`)** : la première implémentation jouait les sons via `System.Media.SoundPlayer`, qui s'est révélé échouer **silencieusement** (sans exception) à jouer des sons courts répétés — comportement documenté de cette API .NET historique (l'objet peut être ramassé par le GC ou son état interne se dégrader sans que .NET le signale). Une première tentative de correctif (retenir une référence contre le GC) n'a rien changé, confirmant que ce n'était pas la vraie cause. Root cause confirmée en éliminant les explications externes (son Windows général fonctionnel sur la machine de test, donc problème spécifique à Plume) : remplacement par **NAudio** (déjà dépendance du projet pour le micro, aucune nouvelle dépendance) — `WaveOutEvent`/`WaveFileReader`, instances retenues le temps de la lecture puis libérées proprement via l'évènement `PlaybackStopped` (pas de fuite). Un second problème est apparu après ce correctif : l'ouverture du périphérique audio (`WaveOutEvent.Init`) est synchrone et plus coûteuse que l'ancien `SoundPlayer`, ajoutant une latence perceptible signalée par l'utilisateur — déplacée dans un `Task.Run` pour rester hors du chemin critique (RÈGLE PRIMORDIALE).

Tests : 182/182 verts (9 nouveaux : `SoundCatalogTests` + `TickSoundServiceTests`, aucun sur le branchement `DictationController`/`InsertionService`/UI — non testable unitairement dans ce projet, cohérent avec l'existant). Revue de code (4 tâches + finale) : Approved, 0 Critical/Important. Vérifié manuellement par l'utilisateur après le correctif NAudio : les deux tops jouent de façon fiable à chaque dictée, réactivité retrouvée.

---

## Option par style « Résultat dans une fenêtre » ✅ (livrée le 2026-07-06, version 5.1.4)
*Spec : `docs/superpowers/specs/2026-07-06-plume-style-resultat-fenetre-design.md` (demande utilisateur du jour, hors lots V8).*

Cas déclencheur : le style perso « Email pro » — l'utilisateur sélectionne tout son email (brouillon + historique de conversation) pour donner le contexte au LLM, et le résultat **remplaçait** la sélection, écrasant tout l'email. Solution : booléen `SelectionStyle.ResultInWindow` (défaut `false`, rétro-compatible) + case « Résultat dans une fenêtre » dans l'éditeur de style. Coché : le résultat s'ouvre dans la fenêtre de réponse existante (`AnswerWindow` : Copier / Insérer au curseur / Fermer, non destructive), le presse-papiers capturé est restauré, le document n'est jamais touché. Honoré pour les styles écrits (`ApplyWrittenStyleAsync`) et avec voix (`ProcessCommand`) ; suffixe « · fenêtre » dans la liste des styles. Écartés : fenêtre dédiée (duplication), réglage global (trop grossier), bascule automatique selon la taille de sélection (comportement magique).

Tests : 182/182 verts (aucun nouveau — branchement contrôleur/UI, non testable unitairement, cohérent avec l'existant). Impact RÈGLE PRIMORDIALE : nul (un booléen, fenêtre lazy existante).

---

## Copie riche + prompt « Email pro » pour Plume ✅ (livrée le 2026-07-06, version 5.1.5)
*Prompt : `docs/prompts/style-email-pro.md` (à coller dans le style ; réglages recommandés inclus).*

Suite de la 5.1.4 : l'utilisateur voulait retrouver dans Plume le résultat de son prompt « Email pro » utilisé dans Claude. Trois écarts identifiés et traités :
1. **Cadrage** : `CleanupService` présente la sélection comme un « texte à reformuler » — le nouveau prompt redéfinit explicitement la tâche (entrée = brouillon + historique du fil en contexte ; sortie = uniquement l'email) et est adapté au one-shot (jamais de question → placeholders `[À COMPLÉTER]`, pas de bloc Remarques, exemples condensés intégrés au lieu du fichier joint Claude).
2. **Modèle** : recommandation Llama 3.3 70B (Groq) par défaut, ou id OpenRouter d'un modèle Claude saisi dans le champ modèle du style pour une parité totale.
3. **Mise en forme** : le style repose sur le gras/les puces, mais « Copier » copiait le markdown brut (`**`). Nouveau service pur `MarkdownHtml` (mêmes règles que `LightMarkdown`) : HTML groupé (`ul`/`ol`, `b`/`i`/code), enveloppe CF_HTML (offsets en octets UTF-8), texte brut sans marqueurs. Le bouton Copier de `AnswerWindow` pose désormais les deux formats dans le presse-papiers → collage dans Outlook/Word avec gras et listes, texte propre ailleurs.

Tests : 194/194 verts (12 nouveaux : `MarkdownHtmlTests` — HTML, échappement, groupage des listes, offsets CF_HTML avec accents). Impact RÈGLE PRIMORDIALE : nul (conversion à la copie uniquement).

---

## Styles intégrés adaptés à Llama 3.3 (anti-markdown) ✅ (livrée le 2026-07-06, version 5.1.6)

Revue de tous les prompts de styles de l'utilisateur (modèle de nettoyage : `groq/llama-3.3-70b-versatile`). Constat : les styles intégrés remplacent la sélection en **texte brut**, mais rien n'interdisait le markdown — or Llama 3.3 ajoute volontiers `**gras**` ou `###`, qui se collent alors littéralement dans le document. Correctif : règle « texte brut, jamais de markdown » (`TexteBrut` dans `StyleCatalog`) injectée dans `EcritSuffixe`, `BasculerPrompt` et `CommandPrompt`, avec **migration douce génération 2** (`StyleService.Legacy2BuiltInPrompts`, même mécanisme que V8-I-0) : un prompt encore identique au défaut V8-I-0 reçoit le nouveau, un prompt personnalisé n'est jamais touché. Le style perso « Email pro » (non migrable par code) a été mis à jour directement dans la config de l'utilisateur (app arrêtée, backup `config.json.bak-*`) : version avec exemples de `docs/prompts/style-email-pro.md`, « Résultat dans une fenêtre » coché, créativité 0,3.

Tests : 197/197 verts (3 nouveaux : migration gen2, prompt personnalisé jamais écrasé, présence de la règle dans les 9 intégrés). Impact RÈGLE PRIMORDIALE : nul.

---

## Nouveaux styles par défaut à l'installation ✅ (livrée le 2026-07-06, version 5.1.7)

Décision utilisateur : le jeu de styles réel de l'auteur devient le jeu **par défaut** d'une nouvelle installation (`StyleCatalog.BuiltIns()`), dans son ordre : **Commande vocale** (favori), **Affiner** (favori), **Email pro** (fenêtre + créativité 0,3, prompt complet avec référentiel/exemples — `docs/prompts/style-email-pro.md`), **Ton plus professionnel**, **Basculer tutoiement/vouvoiement**, **Finaliser l'email** (favori, créativité 0,3, remplacement en place — `docs/prompts/style-finaliser-email.md`). Retirés des défauts : Corriger, Raccourcir, Rallonger, Ton décontracté, Ton empathique — leurs ids et constantes de migration sont conservés ; chez une install existante ils ne sont **ni supprimés ni réinjectés** (seed unique), et un prompt retiré du catalogue n'est plus migré (DefaultPromptFor → null, prompt conservé). `Make()` étendu (température et fenêtre par style).

Note : le prompt Email pro par défaut a depuis été généralisé (référentiel personnel remplacé par des placeholders à compléter par l'utilisateur), pour une version personnelle de l'app découplée de tout employeur.

Tests : 198/198 verts (catalogue 6 styles, favoris/réglages par défaut, intégré retiré du catalogue jamais migré ni écrasé). Impact RÈGLE PRIMORDIALE : nul.

---

## « Démarrer avec Windows » fiabilisé ✅ (livrée le 2026-07-06, version 5.1.8)

Bug utilisateur : la case « Démarrer avec Windows » ne restait pas cochée entre les redémarrages. Débogage systématique — deux causes racines :
1. **`StartupServiceTests.SetEnabled_ActivePuisDesactive` écrivait dans le VRAI registre** et se terminait par `SetEnabled(false)` : chaque `dotnet test` sur le poste de dev **supprimait l'entrée Run réelle** (cause principale, reproduite). Corrigé : le test sauvegarde puis restaure l'entrée d'origine (try/finally).
2. **Chemin fragile** : l'entrée était écrite avec `Environment.ProcessPath` — une sauvegarde des réglages depuis une instance de dev (dépôt sous OneDrive) inscrivait un chemin OneDrive, que la synchro/sécurité peut retirer (comportement documenté dans `StartupService`).

Correctifs : `StartupService.ResolveRunPath` (l'exe installé `%LOCALAPPDATA%\Plume\Plume.exe` est préféré quand l'instance tourne depuis OneDrive ; appliqué dans `SetEnabled` pour tous les appelants) + **auto-réparation** dans `App.OnStartup` (si `StartWithWindows`, l'entrée Run est réinscrite à chaque lancement — une suppression externe ne survit pas au lancement suivant) + synchro de l'élément du menu tray après sauvegarde des réglages (`RefreshStartup`).

Tests : 202/202 verts (4 nouveaux sur `ResolveRunPath` ; vérifié après run que l'entrée Run réelle survit aux tests). Impact RÈGLE PRIMORDIALE : nul (une écriture registre au lancement).

---

## Styles email : signature Outlook plus jamais recopiée ✅ (livrée le 2026-07-06, version 5.1.9)

Retour utilisateur : « Email pro » terminait la réponse par la signature complète (nom, fonction, téléphone…) — Llama 3.3 la recopiait depuis le fil sélectionné, la consigne « pas de signature » seule ne suffisant pas. Règle « SIGNATURE INTERDITE » explicite (liste de ce qui ne doit jamais suivre la formule de politesse, mention que la signature vue dans l'entrée est celle d'Outlook et serait dupliquée), placée en fin de section SORTIE (recency). Appliquée aux prompts « Email pro » et « Finaliser l'email » à trois endroits : défauts `StyleCatalog`, docs de référence (`docs/prompts/`), et config utilisateur (re-patch, app arrêtée, lecture UTF-8 explicite).

Tests : 202/202 verts (prompts seulement). Impact RÈGLE PRIMORDIALE : nul.

---

## Bouton « Copier » de la fenêtre de réponse fiabilisé ✅ (livrée le 2026-07-07, version 5.1.10)

Retour utilisateur : avec « Email pro » (résultat en fenêtre), le bouton « Copier » « ne fonctionne pas ». Investigation : le mécanisme de copie riche (texte brut + CF_HTML, livré en 5.1.5) est correct — vérifié hors app par pose puis relecture Win32 inter-processus et collage Word (gras + puces restitués). Le seul point de défaillance restant est `Clipboard.SetDataObject(…, flush)` qui lève `CLIPBRD_E_CANT_OPEN` quand un autre processus (OneDrive, Office, Teams…) tient brièvement le presse-papiers — WPF ne réessaie pas, et le `catch {}` du bouton avalait l'échec **en silence** : bouton « mort » en apparence, aucun indice.

Correctif (`AnswerWindow.CopyLastAnswer`) : 3 tentatives espacées de 100 ms, puis retour visuel sur le bouton — « Copié ✓ » en succès, « Échec de la copie » sinon (libellé restauré après 1,5 s, même pattern que le bouton « Copier le dernier texte » des réglages). Plus jamais d'échec silencieux : si « Copié ✓ » s'affiche et que le collage reste incorrect, la piste presse-papiers sera définitivement écartée.

Impact RÈGLE PRIMORDIALE : nul (les tentatives n'ont lieu qu'en cas d'échec, 200 ms max, asynchrones).

---

## V8-E — Commandes vocales de saut de ligne ✅ (livrée le 2026-07-08, version 5.1.11)

Objectif : officialiser les retours à la ligne dans les dictées, pour des emails structurés sans repasser par le clavier. Spec : `docs/superpowers/specs/2026-07-06-plume-v8-lots-e-h-i-j.md` (section Lot E).

- Déclencheurs (insensibles à la casse, plus long au plus court) : « nouveau paragraphe » → double saut (`\n\n`) ; « retour à la ligne », « à la ligne », « nouvelle ligne » → simple saut (`\n`). Ponctuation adjacente (virgule ou point, espaces) absorbée ; la lettre qui suit le saut est mise en majuscule pour une phrase proprement close.
- Nouvelle méthode pure `ReplacementService.ApplyLineBreaks(string text)` (un seul `Regex.Replace`), appelée dans `DictationController` juste avant `ReplacementService.Apply`, gatée par le nouveau booléen `AppConfig.LineBreakCommandsEnabled` (ON par défaut) — case à cocher dans Réglages → Dictionnaire, à côté des règles de remplacement.
- Faux positif assumé : pas de mécanisme d'échappement, décocher le réglage si besoin de dicter littéralement « à la ligne ».
- Champ « Vers » des règles de remplacement perso : multi-ligne (`AcceptsReturn`), affiché avec « ↵ » dans la liste — lève la limite notée à l'abandon des snippets, ouvre les signatures multi-lignes.

Tests : 11 nouveaux sur `ApplyLineBreaks` (déclencheurs, ponctuation, casse, absence de déclencheur, fin de texte, déclencheurs multiples) + 4 sur l'affichage `ReplacementRule.ToString()` (dont un correctif de robustesse : `ToString()` plantait avec une `NullReferenceException` si le champ « Vers » d'une règle était `null`, cas possible sur une config corrompue ou éditée à la main — contraire à la règle « jamais de crash ») + 2 sur la persistance de `LineBreakCommandsEnabled`. Impact RÈGLE PRIMORDIALE : nul (traitement pendant une dictée déjà en cours, aucun coût au démarrage).

---

## V8-J — Pastille multi-écrans, jamais hors écran ✅ (livrée le 2026-07-09, version 5.1.12)

Objectif : une pastille visible sur chaque écran connecté (états synchronisés), plus jamais de pastille perdue hors de la zone visible après un changement d'écrans. Spec : `docs/superpowers/specs/2026-07-06-plume-v8-lots-e-h-i-j.md` (section Lot J).

- Nouvelle méthode pure `Services/BadgePlacementService` (position par défaut + « jamais hors écran »), testée en isolation (aucune dépendance WPF/Win32).
- `AppConfig.BadgePositions` (dictionnaire écran → position, clé `Screen.DeviceName`) remplace le champ unique `BadgePosition` (conservé pour la désérialisation et migré automatiquement vers l'écran principal au premier chargement).
- `App.xaml.cs` : `_badge` unique devenu `_badges` (une par écran, `Screen.AllScreens`), tous les appels d'état (écoute/traitement/niveaux/idle) diffusés à toutes les pastilles ; clic et menu des styles fonctionnent depuis n'importe quelle pastille, le menu s'ouvrant près de celle cliquée.
- Abonnement à `SystemEvents.DisplaySettingsChanged` (créé/désabonné en différé, comme le thème) : réconcilie les pastilles à chaud (écran ajouté/retiré) et re-clampe toutes les positions dans leur zone de travail visible.
- Pas de masquage par écran (décision du cadrage) : « Afficher la pastille » reste un réglage tout ou rien.
- Approximation assumée : les coordonnées `Screen.AllScreens`/`Screen.WorkingArea` (WinForms, pixels physiques) sont traitées comme des unités WPF directement, sans conversion DPI — correct sur écrans de même résolution/échelle ; la dette technique « suppression WinForms » migrera vers `EnumDisplayMonitors` plus tard.

Tests : 7 nouveaux sur `BadgePlacementService` (position par défaut, clamp dans/hors zone, zone décalée, cas limite zone plus petite que la pastille) + 3 sur la persistance/migration de `BadgePositions` (dont la mise à jour d'un test existant qui vérifiait l'ancien champ unique). Câblage `App.xaml.cs`/`BadgeWindow` non testable unitairement (orchestration UI, cohérent avec le reste du projet) — vérifié par build + test manuel multi-écrans. Impact RÈGLE PRIMORDIALE : nul (création des pastilles toujours en différé, jamais au boot ; l'abonnement aux évènements d'écran ne coûte rien tant qu'aucune pastille n'a jamais été créée).

---

## V8-H — Confidentialité & défauts sains ✅ (livrée le 2026-07-09, version 5.1.13)

Objectif : les clés API ne doivent plus jamais apparaître en clair sur disque, et un collègue qui rencontre un problème doit pouvoir être diagnostiqué à distance sans jamais exposer son texte dicté. Spec : `docs/superpowers/specs/2026-07-06-plume-v8-lots-e-h-i-j.md` (section Lot H).

**H-1 — Chiffrement DPAPI des clés API** :
- `Services/ConfigService.Save` chiffre toujours `OpenRouterApiKey`/`GroqApiKey` (préfixe `enc:` + Base64, `ProtectedData.Protect`, portée `CurrentUser`) sur une copie JSON (`JsonNode`) — l'objet `AppConfig` passé par l'appelant n'est jamais muté, les services en mémoire gardent le clair.
- `Load` déchiffre les valeurs `enc:` ; une valeur sans préfixe (ancienne config en clair) est utilisée telle quelle et re-chiffrée automatiquement au prochain `Save` (migration transparente, aucune intervention).
- Blob illisible (autre machine/profil Windows, ou corrompu) → clé considérée absente (`null`), jamais de crash ; l'utilisateur la re-saisit dans Réglages.
- Aucune nouvelle dépendance : `System.Security.Cryptography.ProtectedData` est déjà disponible sur `net8.0-windows`.

**H-2 — Journal de diagnostic opt-in** :
- Nouveau `Services/DiagLogService` : `%LOCALAPPDATA%\Plume\diag.log`, une ligne par étape (horodatage, étape, durée ou erreur) — jamais le texte dicté, jamais les clés. Borné à ~500 Ko (rotation vers `diag.old.log`, écrasé). Écriture fire-and-forget (`Task.Run`), aucun coût quand désactivé (nouveau réglage `AppConfig.DiagnosticsLogEnabled`, **OFF par défaut**).
- Branché dans `DictationController` aux points de mesure existants (micro prêt, transcription, nettoyage) + erreurs de la dictée classique (type + message).
- UI : Réglages → Général → nouveau groupe « Diagnostic » (toggle + bouton « Ouvrir le journal », grisé si le fichier n'existe pas encore).

Tests : 4 nouveaux sur `ConfigServiceTests` (clés chiffrées sur disque, aller-retour Save/Load, migration clair→chiffré, blob corrompu → absent sans crash) + 4 sur `DiagLogServiceTests` (toggle désactivé = zéro écriture, contenu d'une ligne, rotation au-delà du plafond, erreur d'écriture sans crash). Impact RÈGLE PRIMORDIALE : nul (chiffrement au moment du `Save` existant, journal désactivé par défaut et fire-and-forget).

---

## V8-I — Boucle d'amélioration des prompts ❌ écarté (livré en 5.1.14/5.1.15, retiré en 5.1.16, 2026-07-10)

I-1 avait déjà été écarté le 2026-07-09 (le prompt de dictée perso couvrait déjà ce besoin). I-2 (fenêtre « Réviser la dernière dictée », audio/brut/inséré/« ce que j'aurais voulu », version 5.1.14) et I-3 (bouton « En tirer une leçon » — suggestions de règle par diff mot à mot ou par IA, liste « Règles apprises » plafonnée/dédupliquée, version 5.1.15) ont été implémentées, testées (0 finding Critique/Important sur toutes les revues de tâches et les deux revues finales whole-branch) puis livrées telles quelles.

**Retiré intégralement le 2026-07-10 (version 5.1.16)**, à la demande de l'utilisateur après un premier usage réel : le principe même (réviser une dictée après coup pour en tirer une correction) ne correspond à aucun besoin réel — en pratique, l'utilisateur ne revient jamais corriger une dictée après le fait, quel que soit le soin apporté à l'implémentation. Retrait par `git revert` de la plage complète (21 commits, tag conceptuel `49bf110..f91e645`), retour exact à l'état V8-H (5.1.13) confirmé par build + suite de tests (236/236). Voir `docs/roadmap.md` (section « Abandonné / caduc ») pour le rappel court, et l'historique Git (messages `feat(revision):`/`feat(lecons):`) pour le détail technique complet si ce principe devait être reconsidéré un jour — **ne pas rouvrir sans un besoin concret vérifié en amont**, pas seulement validé en brainstorming.

---

## 5.2.0 — Allègement mémoire (2026-07-11)
Audit préalable (dump .NET + gcroot + inspection code) : aucun des suspects de la roadmap ne fuyait
(fenêtres, événements, buffers audio, threads) ; tas managé 14 Mo, objets Plume 22 Ko. Le poids venait
du socle : WinForms chargé pour le seul NotifyIcon.
- Tray réécrit en Shell_NotifyIcon P/Invoke (même API), Screen → Win32Screens (EnumDisplayMonitors) :
  `UseWindowsForms` retiré du csproj.
- Logo décodé à 128 px (DecodePixelWidth) au lieu de 512.
- `System.GC.ConserveMemory=5` (runtimeconfig.template.json).
- Historique assistant borné à `AgentSession.MaxTurns=20`.
- Mesures : avant = 145 Mo WS / 160 Mo privés / 30 threads au repos ; 208 Mo WS / 53 threads en usage.
  Après = 188 Mo WS / 116 Mo privés / 38 threads au repos (2 min post-lancement) ; 186 Mo WS / 116,5 Mo
  privés / 28 threads après usage (2-3 dictées + 10 min de repos). Mémoire privée -27 %, threads en usage
  quasi divisés par deux ; le WorkingSet ne baisse pas autant qu'espéré — probablement les pages du binaire
  single-file (runtime embarqué) mappées en mémoire partagée, un indicateur moins fiable que le privé ici.
  Smoke test (dictées, tray, insertion, pastilles) : aucun souci constaté.
- Non retenu : trim du WorkingSet à l'inactivité (fautes de page au réveil, contraire à « vitesse d'abord »).

---

## 5.2.1 — Robustesse : correctifs de l'audit d'erreurs (2026-07-11)
Audit préalable en deux temps (revue statique de tous les chemins d'erreur du contrôleur/services +
tests d'injection réels sur machine — clé invalide, réseau coupé, silence, presse-papiers verrouillé
inter-processus) : `docs/superpowers/plans/2026-07-11-audit-robustesse.md`. Deux non-conformités
confirmées à l'exigence CLAUDE.md (« gérer toutes les erreurs sans crash, avec retour utilisateur propre »),
plus des garde-fous préventifs contre des blocages théoriques repérés en revue de code.
- **Presse-papiers verrouillé pendant une dictée classique** (`DictationController.StopAndProcess`) :
  l'échec de `_insertion.Paste` était jusqu'ici totalement silencieux (la dictée semblait avoir marché,
  rien n'était collé). Balloon « Presse-papiers indisponible : texte non inséré. Récupère-le via
  « Copier le dernier texte » » ; la dictée n'est plus comptée dans les statistiques si elle n'a pas
  été insérée.
- **Micro déconnecté en cours d'écoute** (casque Bluetooth éteint, périphérique débranché) : nouvel
  évènement `AudioCaptureService.DeviceLost` (abonné à `WaveInEvent.RecordingStopped`, ne se déclenche
  que si l'arrêt n'était pas volontaire) → arrêt propre + balloon « Micro déconnecté. » au lieu d'une
  pastille figée en écoute sans explication.
- **Micro orphelin sur double ouverture concurrente** (Échap pendant l'ouverture lente d'un micro
  Bluetooth, suivi d'un ré-appui immédiat avant la fin de l'ouverture précédente) : `StartListening`
  ignore désormais tout appel pendant qu'une ouverture est en cours ; `AudioCaptureService.TryStart`
  ferme aussi défensivement toute capture déjà ouverte avant d'en démarrer une nouvelle.
- **Transcription vide en dictée classique** : garde `IsNullOrWhiteSpace` ajoutée avant le nettoyage
  (déjà présente en mode Agent et Commande vocale, absente en dictée) — évite qu'un LLM invente du
  contenu à partir d'une transcription vide.
- **Erreurs imprévues rendues visibles** : les trois `catch` fourre-tout (dictée, agent, commande
  vocale) affichent désormais un balloon générique au lieu de retomber en silence à l'état Idle.
- **Filet de sécurité anti-blocage** : `ApplyWrittenStyleAsync` et `StartVoiceStyleAsync` enveloppent
  désormais tout leur corps dans un try/(finally), pas seulement la partie réseau — une exception
  imprévue avant le nettoyage ne peut plus laisser l'état bloqué sur `Working` indéfiniment (spinner
  permanent, raccourcis morts, seul recours étant de redémarrer Plume).
- Tests : 4 nouveaux (réponse LLM à contenu vide → `CleanupService`/`AgentChatService`, réponse STT
  sans champ `text`, 401 sur `CleanupService` non couvert jusqu'ici) — 242/242 au vert.
- Impact RÈGLE PRIMORDIALE : nul (aucun chemin nominal modifié, uniquement des branches d'erreur).

---

## 5.2.2 — V8-F-1 : installateur Inno Setup pour les collègues (2026-07-15)
Nouveau dossier `installer/` : `plume.iss` (script Inno Setup 6) + `build-installer.ps1` (chaîne de
fabrication) → `installer/output/PlumeSetup-X.Y.Z.exe`, jamais versionné dans git.
- **Installation par utilisateur, sans droits admin** (`PrivilegesRequired=lowest`) dans
  `%LOCALAPPDATA%\Plume` (`DisableDirPage=yes` : jamais de choix d'un autre dossier, jamais OneDrive) ;
  interface entièrement en français (`Languages: french`).
- **Arrêt de Plume avant copie** : `taskkill /f /im Plume.exe` dans `PrepareToInstall` (ceinture et
  bretelles en plus de `CloseApplications=yes`), relancé à la fin si la case « Lancer Plume » reste
  cochée.
- **Runtime .NET 8 Desktop détecté et installé au besoin** : détection par clé registre
  `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App` (préfixe
  `8.`) ; si absent, téléchargement de `https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe`
  (page de téléchargement Inno Setup native) puis installation silencieuse
  `/install /quiet /norestart` (codes 0/3010/1638 acceptés) — tout échec annule proprement
  l'installation de Plume (rien n'est copié à moitié).
- **Désinstallation propre qui conserve la config** : `[UninstallDelete]` supprime `{app}` (exe +
  `Resources\`) et `CurUninstallStepChanged` retire l'entrée `HKCU\...\CurrentVersion\Run\Plume`, mais
  `%APPDATA%\Plume\config.json` (clés API chiffrées, styles, dictionnaire, stats) n'est **jamais**
  touché — une réinstallation retrouve tout, aucun assistant de prise en main à revoir.
- **Chaîne de fabrication** `build-installer.ps1` : lit `<Version>` du csproj (source unique de
  vérité) → `dotnet publish` Release/win-x64/single-file/framework-dependent → `iscc
  /DAppVersion=<version> plume.iss`. Détecte Inno Setup en PATH puis aux chemins standards (Program
  Files (x86) et per-user `%LOCALAPPDATA%\Programs\Inno Setup 6`) ; échec explicite si absent (jamais
  d'installation silencieuse d'un outil tiers).
- **Limite assumée** : sans certificat de signature de code, l'avertissement SmartScreen « éditeur
  inconnu » subsiste — acceptable en diffusion interne (consigne dans le mail d'invitation aux
  collègues) ; à revoir seulement en cas de diffusion publique.
- **Test manuel réel effectué** (scénario « réinstallation par-dessus », poste de dev avec .NET 8 déjà
  présent) : installation < 10 s sans étape runtime, aucune demande d'admin (hors SmartScreen), exe +
  `Resources\` + raccourci menu Démarrer présents, config non touchée (dictée immédiate) ;
  désinstallation complète (exe, dossier, raccourci, entrée Run) avec config conservée ; réinstallation
  qui retrouve tout. **Reste à valider** : scénario « machine vierge sans .NET 8 » (téléchargement +
  installation réelle du runtime), non testable sur le poste de dev — à faire sur une VM Windows propre
  ou au premier poste de collègue invité.
- Impact RÈGLE PRIMORDIALE : nul (coût unique à l'installation, rien au démarrage de l'app).

---

## 5.2.3 — V8-B : assistant de prise en main au premier lancement (2026-07-15)
Spec : `docs/superpowers/specs/2026-07-06-plume-installateur-et-premier-lancement.md`. Nouvelle fenêtre
`Views/OnboardingWindow` (3 étapes), pour que le premier contact d'un collègue invité ne soit plus une
icône tray muette.
- **Déclenchement seulement si aucune clé n'est configurée** (ni Groq ni OpenRouter) : `App.xaml.cs`
  l'instancie au démarrage à la place du silence habituel ; coût nul dans tous les autres cas (fenêtre
  jamais créée si une clé existe). Accessible ensuite à la demande via À propos → « Revoir la prise en
  main » (l'étape clé est alors déjà marquée acquise si une clé existe).
- **Étape 1 — clé API** : coller la clé Groq, « Tester la clé » l'appelle via `GroqModelsClient` (liste
  des modèles, aucun token consommé) ; succès → clé chiffrée enregistrée (`ConfigService`) et « Clé
  valide ✓ » ; échec → message clair, pas de crash, on peut réessayer. Lien direct vers
  `console.groq.com/keys` ; repli OpenRouter (champ replié par défaut, révélé au clic) enregistré sans
  test équivalent (validé à l'usage).
- **Étape 2 — micro** : capture dédiée via une instance `AudioCaptureService` séparée de celle de la
  dictée (jamais de micro partagé ni laissé ouvert en arrière-plan), vu-mètre lissé au même rendu que les
  barres de la pastille. Sélecteur de périphérique affiché seulement si plusieurs micros détectés ;
  changer de micro relance le test. Aucun micro détecté → message + « Continuer quand même ».
- **Étape 3 — première dictée guidée** : instruction avec le raccourci réel (`HotkeyParser.Describe`),
  zone de texte focalée pour recevoir l'insertion au curseur ; dès qu'un texte y apparaît, confirmation
  visuelle et « Terminer » s'active.
- Thème clair/sombre à chaud (même mécanisme que `AnswerWindow`, barre de titre incluse).
- **Smoke test manuel réel effectué** (parcours complet : clé bidon puis vraie clé Groq, vu-mètre au
  micro réel, dictée avec le raccourci réel, relance sans réaffichage, « Revoir la prise en main »,
  bascule thème sombre) : conforme, aucun écart constaté.
- Impact RÈGLE PRIMORDIALE : nul (fenêtre à la demande, aucun réseau ni chargement au démarrage normal
  une fois une clé configurée).

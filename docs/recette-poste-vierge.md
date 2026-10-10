# Recette poste vierge — chaîne d'essaimage complète

> Validation manuelle, en une seule passe (~45-60 min), du parcours exact que vivra chaque collègue :
> installation → prise en main → usage quotidien. À dérouler **une fois** sur une machine qui n'a
> jamais vu Plume : VM Windows 10/11 propre, ou PC d'un collègue pas encore équipé.
> Version minimale à tester : **5.3.8** (les PlumeSetup ≤ 5.3.7 contiennent le bug d'onboarding 100 % Groq).
>
> Cocher au fil de l'eau. En cas d'échec : noter le comportement observé, activer le journal de
> diagnostic (Réglages → Général → Diagnostic) et rejouer l'étape avant de conclure.

## 0. Préparation (sur ton poste, avant d'aller sur la machine de test)

- [ ] Récupérer `installer/output/PlumeSetup-5.3.8.exe` (ou plus récent) sur une clé USB ou un partage.
- [ ] Vérifier la machine cible : **pas** de dossier `%LOCALAPPDATA%\Plume` ni `%APPDATA%\Plume` (sinon supprimer les deux pour une vraie première installation).
- [ ] Idéalement : machine **sans** runtime .NET 10 Desktop (PC neuf ou VM propre) pour tester le téléchargement automatique. Vérifier : `dotnet --list-runtimes` en console → aucun `Microsoft.WindowsDesktop.App 10.x` = parfait.
- [ ] Prévoir un compte Groq de test (ou créer le compte pendant la recette, comme un vrai collègue : c'est le parcours à valider). **Ne pas** préparer de clé OpenRouter — le scénario nominal est 100 % Groq.

## 1. Installation

- [ ] Double-clic sur `PlumeSetup-5.3.8.exe`. **Attendu** : avertissement SmartScreen « Windows a protégé votre ordinateur » → « Informations complémentaires » → « Exécuter quand même » (assumé, installateur non signé — c'est le message expliqué dans l'email d'invitation).
- [ ] **Aucune demande de droits administrateur** pour Plume lui-même (installation par utilisateur).
- [ ] Si le runtime .NET 10 manque : message « Plume a besoin d'un composant Microsoft gratuit… » → téléchargement → UAC **pour l'installeur Microsoft uniquement** → installation silencieuse. **Attendu** : aucune erreur, l'installation de Plume enchaîne.
- [ ] Fin d'installation : case « Lancer Plume » cochée → Plume démarre, icône dans la zone de notification (barre des tâches).
- [ ] Vérifier : raccourci « Plume » dans le menu Démarrer ; fichiers dans `%LOCALAPPDATA%\Plume`.

## 2. Prise en main (onboarding) — LE test critique

- [ ] La fenêtre « Bienvenue dans Plume » s'ouvre seule (aucune clé configurée).
- [ ] **Étape 1** : cliquer le lien `console.groq.com/keys` → le navigateur s'ouvre. Créer le compte + la clé comme un vrai collègue. Coller la clé → « Tester la clé » → **Attendu** : « Clé valide ✓ — enregistrée. » **Ne pas toucher** au panneau « J'ai plutôt une clé OpenRouter ».
- [ ] **Étape 2** : parler → la barre verte bouge → « Ton micro fonctionne ✓ ».
- [ ] **Étape 3** : suivre l'instruction (raccourci → dicter « Bonjour, ceci est ma première dictée » → raccourci). **Attendu** : le texte apparaît **nettoyé et ponctué** dans la zone, « 🎉 Bravo, Plume est prêt ! » s'affiche, « Terminer » s'active. ⚠️ C'est le scénario corrigé en 5.3.8 : s'il apparaît une bulle « renseigne ta clé OpenRouter », la recette échoue.
- [ ] Vérifier dans `%APPDATA%\Plume\config.json` : `"cleanupModel"` commence par `"groq/"` et les deux clés sont stockées chiffrées (valeurs préfixées `enc:`, jamais en clair).

## 3. Usage quotidien (dans le Bloc-notes + Outlook/Teams si disponibles)

- [ ] **Dictée bascule** : raccourci (Alt+Espace par défaut) → pastille en écoute (barres vertes qui suivent la voix) → parler 2-3 phrases avec « euh » et hésitations → raccourci → texte inséré au curseur, propre, sans tics de langage.
- [ ] **Sauts de ligne dictés** : dicter « premier point à la ligne deuxième point nouveau paragraphe conclusion » → vrais retours à la ligne.
- [ ] **Échap** : lancer une dictée, appuyer sur Échap pendant l'écoute → tout s'arrête, rien n'est inséré, la pastille revient au repos. Relancer une dictée juste après → elle fonctionne.
- [ ] **Pastille** : clic pendant l'écoute = stop + traitement ; au repos, double-clic = dictée sans clavier ; clic simple = menu des styles (s'ouvre après ~0,5 s).
- [ ] **Style écrit** : taper une phrase avec des fautes dans le Bloc-notes, la sélectionner, pastille → « Affiner » → la sélection est remplacée par la version corrigée ; le presse-papiers d'avant est intact (copier un mot avant le test, coller après : le mot revient).
- [ ] **Commande vocale** : sélectionner un texte, menu → « Commande vocale », dicter « mets ce texte en liste à puces » → la sélection est transformée.
- [ ] **Assistant vocal** : Ctrl+Alt+A → poser une question → la fenêtre de réponse s'ouvre ; poser une question de suivi (Ctrl+Alt+A à nouveau) → la conversation continue ; « Insérer au curseur » → la réponse se colle dans le document et la fenêtre se ferme.
- [ ] **Copier le dernier texte** : menu tray (clic droit sur l'icône) → « Copier le dernier message » → colle bien la dernière dictée.
- [ ] **Confidentialité presse-papiers** : après une dictée, ouvrir Win+V → le texte dicté **n'apparaît pas** dans l'historique.

## 4. Cas limites (5 minutes)

- [ ] **Sans réseau** : couper le Wi-Fi → dicter → bulle claire (« Pas de réseau ou délai dépassé ») , la pastille revient au repos, l'app reste utilisable. Rallumer le Wi-Fi → la dictée suivante marche sans redémarrer.
- [ ] **Silence** : lancer une dictée, ne rien dire, arrêter → rien n'est inséré, pas d'erreur.
- [ ] **Image dans le presse-papiers** : faire une capture d'écran (elle va dans le presse-papiers), dicter, puis Ctrl+V → **l'image** se colle (le presse-papiers a été restauré).
- [ ] **Redémarrage d'explorer.exe** (Gestionnaire des tâches → Explorateur Windows → Redémarrer) → l'icône tray de Plume **revient toute seule**.
- [ ] **Thème** : basculer Windows clair ↔ sombre (Paramètres → Personnalisation → Couleurs) → réglages et fenêtres de Plume suivent à chaud.
- [ ] **Multi-écrans** (si la machine en a) : brancher/débrancher un écran → une pastille par écran, aucune pastille perdue hors écran.
- [ ] **Verrouillage** : Win+L, déverrouiller → raccourci → la dictée fonctionne.

## 5. Redémarrage et cycle de vie

- [ ] Redémarrer Windows → Plume **démarre seul** (icône tray présente sans rien lancer).
- [ ] Réglages → À propos : « Version 5.3.8 » (ou plus récente) et le changelog s'affiche.
- [ ] *(Optionnel, si la machine de test est jetable)* Désinstaller (Paramètres → Applications) → `%LOCALAPPDATA%\Plume` supprimé, plus de démarrage auto, **mais** `%APPDATA%\Plume` (config + clés) conservé. Réinstaller → Plume retrouve la clé et les réglages, l'onboarding ne réapparaît pas.

## Verdict

- [ ] **Toutes les cases cochées → la chaîne d'essaimage est validée de bout en bout.** Plume peut être distribué et vivre en autonomie ; aucune recette supplémentaire n'est prévue.
- En cas d'échec sur une étape : noter la version, l'étape, le message exact (et `%LOCALAPPDATA%\Plume\diag.log` si le diagnostic était actif) — c'est le dossier d'entrée d'une future session de correction.

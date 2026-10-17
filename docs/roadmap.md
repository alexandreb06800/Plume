# Roadmap — Plume

Plan de charge des versions **à venir**. Chaque version reste **petite et livrable**.
Règle permanente : **aucune traduction, à aucune version.**

> **Historique livré** (V1 → V7, détail complet) : `docs/roadmap-archive.md`. Ne lire l'archive que si une question porte sur une décision passée.

## 📋 Prochaines mises à jour — dans cet ordre

**Backlog de développement clôturé le 2026-07-16 (décision utilisateur).** Tout est livré (V1 → V8, dette technique comprise) ou explicitement écarté (V5, F-2, lot I, idées ultérieures… — voir « Abandonné / caduc »). Plume est en **mode maintenance** : toute évolution future part d'une nouvelle demande de l'utilisateur, jamais de ce fichier.

---

## État courant (résumé — détail dans l'archive)

- **Version courante : 5.4.19 (2026-10-17).** Le détail de CHAQUE version livrée vit dans `docs/roadmap-archive.md` — ne le lire que pour une décision passée. À chaque livraison : mettre à jour la version courante ici et ajouter la section détaillée dans l'archive.
- **En bref, ce que Plume fait aujourd'hui** : dictée globale (raccourci personnalisable, toggle ou maintien), pastille flottante multi-écrans (double-clic = dictée, clic = menu des styles), styles de réécriture sur sélection (écrits / avec voix, résultat en fenêtre optionnel, copie riche), dictionnaire + règles de remplacement + sauts de ligne dictés, assistant vocal (Ctrl+Alt+A), thème clair/sombre, stats, sons discrets optionnels, clés API chiffrées (DPAPI), journal de diagnostic opt-in, warm-up réseau Groq + OpenRouter, onboarding 3 étapes au premier lancement, installateur PlumeSetup, démarrage Windows fiabilisé, allègement mémoire (sans WinForms).
- **Abandonné / caduc (ne pas rouvrir sans décision explicite)** : snippets vocaux (couverts par les règles de remplacement), profil restreint à une application (`TargetApp`, retiré en 5.0.0), maintien/tap par style (plus de raccourci par style), hook souris global, **arrêt auto sur silence** (V8-A, écarté le 2026-07-04 — le besoin de stop rapide est déjà couvert par le clic sur la pastille en écoute (livré en V7) ; risque de couper une pause de réflexion pendant la dictée jugé trop élevé pour la valeur apportée), **lot I au complet — « Réviser la dernière dictée » / « En tirer une leçon »** (I-1/I-2/I-3, écarté le 2026-07-10 après livraison en 5.1.14/5.1.15 — retiré en 5.1.16 : l'utilisateur ne revient jamais corriger une dictée après coup en usage réel ; ne pas rouvrir ce principe sans un besoin concret et vérifié avant tout plan), **« Copier la version brute » (V8-C, écartée le 2026-07-10 avec le lot I** — même principe de récupération après coup ; ne rouvrir que sur besoin concret**)**, **signature de code de l'installateur** (refusée le 2026-07-16 — coût du certificat non justifié pour une diffusion interne ; l'avertissement SmartScreen est assumé), **mise à jour en un clic** (V8-F-2, abandonnée le 2026-07-16 — la distribution manuelle de PlumeSetup suffit ; ne rouvrir que si le parc grossit), **V5 — transcription locale whisper.cpp** (abandonnée le 2026-07-16 — l'utilisateur ne souhaite pas la développer ; n'apportait que de la confidentialité, aucun gain de vitesse. Le plan complet reste disponible si un besoin réel de données sensibles apparaît : `docs/superpowers/plans/2026-07-03-plume-v5-transcription-locale.md`).

---

## V8 — Confort & adoption (validée le 2026-07-03, à cadrer en brainstorming avant plan)
*Améliorations proposées par Claude et retenues par l'utilisateur, classées par rapport valeur/effort. Priorisée devant la V5 le 2026-07-04. Chacune reste petite et livrable ; en cadrage, découper en lots si besoin.*

### B — Assistant de premier lancement ✅ *(livrée le 2026-07-15, 5.2.3 — détail dans l'archive)*

### C — Filet de sécurité « Copier la version brute » ❌ *(abandonnée avec le lot I le 2026-07-10)*
Était absorbée par le lot I (« à livrer ensemble ») ; le lot I a été intégralement écarté après usage réel, et C relève du même principe de récupération après coup — même verdict. Classée dans « Abandonné / caduc » ; ne rouvrir que sur besoin concret constaté (sur-correction du LLM réellement subie).

### D — Pré-chauffe réseau (latence perçue) ✅ *(livrée le 2026-07-04, 5.1.2 — détail dans l'archive)*

### E — « À la ligne » dicté (retours à la ligne) ✅ *(livrée le 2026-07-08, 5.1.11 — détail dans l'archive)*

### F — Distribution & mises à jour — point 1 (installateur) ✅ livré le 2026-07-15 (5.2.2) · point 2 (auto-update) ❌ abandonné le 2026-07-16, puis ✅ **rouvert et livré le 2026-10-10 (5.4.8)** sous forme de bouton à la demande
Le point 2 (« Vérifier une mise à jour » dans À propos + canal de distribution interne) est **abandonné sur décision utilisateur** : la distribution manuelle de `PlumeSetup-X.Y.Z.exe` suffit au besoin réel. Ne rouvrir que si le parc de collègues grossit au point de rendre la distribution manuelle pénible.
**Rouvert le 2026-10-10 sur décision utilisateur** : Plume évolue de nouveau (8 versions en deux jours, deux postes), la mise à jour du second poste imposait de transporter le dépôt et de recompiler, et l'arrêt d'un modèle par un fournisseur (Llama, 16/08/2026) a montré qu'une version corrigée doit pouvoir être récupérée simplement. Livré : bouton « Vérifier les mises à jour » (À propos), à la demande uniquement — détail dans l'archive (5.4.8).

### G — Réactivité à l'appui du raccourci (feedback instantané) ✅ *(livrée le 2026-07-03, 5.0.1 — détail dans l'archive)*

### H — Confidentialité & défauts sains ✅ *(livrée le 2026-07-09, 5.1.13 — détail dans l'archive)*
- **Écarté de ce lot (décision utilisateur 2026-07-03)** : changer le raccourci par défaut Alt+Espace → Ctrl+Espace pour les nouvelles installs (conflit connu avec le menu de fenêtre Windows/PowerToys Run, assumé ; chacun personnalise son raccourci).

### I — Boucle d'amélioration des prompts (« Réviser la dernière dictée ») ❌ *écarté le 2026-07-10 après livraison et usage réel (I-1 déjà écarté le 2026-07-09 ; I-2 et I-3 livrées en 5.1.14/5.1.15 puis intégralement retirées en 5.1.16 — l'utilisateur ne revient jamais corriger une dictée après coup en pratique, le principe ne correspondait à aucun besoin réel malgré la validation initiale du 2026-07-03) — voir « Abandonné / caduc » ci-dessous. Le premier lot (I-0, durcissement des prompts par défaut) reste livré séparément, le 2026-07-03 en 5.0.1 — détail dans l'archive.*

### J — Pastille multi-écrans, jamais hors écran ✅ *(livrée le 2026-07-09, 5.1.12 — détail dans l'archive)*

### K — Sons discrets de début et fin d'écoute ✅ *(livrée le 2026-07-04, 5.1.3 — détail dans l'archive)*

**Écartés en connaissance de cause (décision 2026-07-03)** : insertion par frappe simulée (lent sur les longs textes), détection d'app active (supprimée en V6, ne pas rouvrir), **raccourci clavier pour ouvrir le menu des styles** (refusé), **flash de confirmation d'insertion sur la pastille** (jugé inutile). ~~Double-clic sur la pastille pour dicter directement~~ → **finalement livré le 2026-07-15 (5.3.0)** sur demande explicite de l'utilisateur — la crainte de perdre la sélection/le focus était infondée (`WS_EX_NOACTIVATE` confirmé).

---

## Dette technique
- ~~CA1305/CA1310 (culture explicite) sur les données persistées~~ — **livré en 5.3.4**. Détail : `docs/roadmap-archive.md`.
- ~~Découpage des gros fichiers~~ (`DictationController`, `SettingsWindow`) — **livré en 5.3.3**. Détail : `docs/roadmap-archive.md`.
- ~~Allègement mémoire~~ — **livré en 5.2.0** (audit 2026-07-10/11 : tas managé sain à 14 Mo, gain réel = retrait WinForms). Détail : `docs/roadmap-archive.md`.

---

## Idées ultérieures — ❌ clôturées le 2026-07-16
*Les deux idées en réserve (historique/scratchpad des dernières dictées, mode chuchoté) sont clôturées avec le reste du backlog : jamais validées, aucun besoin constaté en usage réel. La spec de cadrage reste consultable si un besoin apparaît un jour : `docs/superpowers/specs/2026-07-03-plume-idees-ulterieures-cadrees.md`.*

---

## Jamais
- **Traduction** dans une autre langue.
- Multi-langue de dictée (le produit est et reste **français**) — sauf décision explicite contraire.

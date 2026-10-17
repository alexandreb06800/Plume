# Confidentialité — audit des flux de données de Plume

> Audit réalisé les 2026-07-16/17 (version 5.3.8). Volet code : lecture intégrale des services réseau
> (`docs/audits/2026-07-16-audit-complet.md`). Volet fournisseurs : chaque affirmation ci-dessous a été
> sourcée sur les pages officielles puis **contre-vérifiée par une relecture indépendante de la source**
> (21 affirmations, 21 vérifiées ; 3 corrigées au passage). Les politiques des fournisseurs évoluent :
> les constats valent **à la date de consultation** et sont à revérifier ~une fois par an.
>
> ⚠️ Ceci est une analyse technique documentée, **pas un avis juridique**. Pour un enjeu de conformité
> formel (client, CNIL), faire valider par le référent RGPD/DPO de ton entreprise.

---

## 1. Ce qui sort de la machine (inventaire exhaustif, issu du code)

| Donnée | Quand | Destinataire |
|---|---|---|
| **Audio dicté** (WAV 16 kHz) | à chaque dictée / commande vocale / question à l'assistant | API Groq `/audio/transcriptions` (si clé Groq) — sinon OpenRouter |
| **Texte transcrit** | à chaque nettoyage | Groq `/chat/completions` (modèle « groq/… ») ou OpenRouter (autres modèles, ex. `openai/gpt-4o-mini` → relayé à OpenAI) |
| **Texte sélectionné** | à chaque style appliqué sur une sélection (y c. le fil d'emails complet pour « Email pro ») | idem nettoyage |
| **Questions + historique de conversation** (20 derniers tours) | à chaque tour de l'assistant vocal | idem nettoyage |
| **Vocabulaire personnalisé** (noms de clients, jargon) | joint à chaque requête STT et nettoyage | Groq et/ou OpenRouter |
| **Vérification de mise à jour** (5.4.8) : aucun contenu dicté ; aucun jeton ni identifiant (dépôt public, lecture anonyme) ; seul l'appel à l'API GitHub, avec l'adresse IP du poste | **uniquement au clic** sur « Vérifier les mises à jour » (À propos) — jamais au démarrage | API GitHub (`api.github.com`, puis téléchargement de l'archive) |
| **Prompts des styles** (dont le référentiel du style « Email pro » : noms internes, clients, infra) | à chaque usage du style | idem nettoyage |
| Rien d'autre | — | aucune télémétrie, aucun serveur tiers propriétaire, aucun autre tiers |

**Ce qui ne sort jamais** : les clés API (chiffrées DPAPI dans `config.json`, jamais journalisées), le
texte final inséré (mémoire seule), les statistiques d'usage (locales), le journal de diagnostic
(local, opt-in, **jamais** le texte dicté ni les clés). Le texte dicté est de plus **exclu de
l'historique du presse-papiers Windows** (Win+V) et du presse-papiers cloud.

**Stocké localement** : `%APPDATA%\Plume\config.json` (clés chiffrées, styles, vocabulaire),
`usage-daily.json` (compteurs anonymes), `%LOCALAPPDATA%\Plume\diag.log` (opt-in, sans contenu dicté).
Aucun audio n'est écrit sur disque : la capture vit en mémoire et est jetée après transcription.

## 2. Ce que font les fournisseurs des données (faits vérifiés)

### Groq (STT Whisper + nettoyage « groq/… ») — consulté les 2026-07-16/17
- **Pas d'entraînement** sur les données API : interdit contractuellement sauf permission/instruction du client (Services Agreement §4.2 : « *Groq is not permitted to use Inputs or Outputs for training or fine-tuning any AI Model Services or other models* »).
- **Pas de conservation par défaut** des requêtes d'inférence (« *By default, Groq does not retain customer data for inference requests* », doc « Your Data in GroqCloud »). Exception : journaux de dépannage/anti-abus, **30 jours max**.
- **Zero Data Retention (ZDR)** activable par **tous** les clients (console → Data Controls) : supprime aussi ces journaux d'exception.
- Ce qui est conservé l'est **aux États-Unis** (buckets GCP) ; aucune option de résidence UE.
- **DPA RGPD intégré automatiquement** au contrat (tout client API, sans signature séparée), incorporant les clauses contractuelles types UE (EU SCCs, module 2 responsable → sous-traitant).

### OpenRouter (relais du nettoyage `openai/gpt-4o-mini`) — consulté les 2026-07-16/17
- **Ne stocke pas** les prompts/réponses par défaut (métadonnées seules : tokens, latence). Deux opt-ins distincts existent (logs privés de débogage ; autorisation d'usage produit contre remise de 1 %) — **ne pas les activer**. Échantillonnage anonyme de catégorisation, sans association au compte.
- **N'entraîne pas** ses modèles sur les données, mais **relaie au provider final**, qui applique SA politique. Le réglage de routage par défaut (`data_collection: allow`) tolère, pour d'autres modèles, des providers qui stockent/entraînent → à restreindre dans les réglages de confidentialité du compte.
- **Point faible RGPD** : le DPA signé et opposable (contrat de sous-traitance art. 28) est **réservé aux comptes Enterprise** (support OpenRouter, corroboré). Un compte self-serve — le cas de Plume — n'a **pas de DPA exécuté** avec OpenRouter.

### OpenAI (provider final du modèle par défaut `openai/gpt-4o-mini`) — consulté les 2026-07-16/17
- **Pas d'entraînement** sur les données API (« *data sent to the OpenAI API is not used to train or improve OpenAI models* », politique effective depuis mars 2023 ; tableau par endpoint : « No » pour `/v1/chat/completions`).
- **Rétention anti-abus 30 jours max** par défaut, pas de stockage applicatif pour `/v1/chat/completions` ; ZDR possible sur approbation.
- **DPA** (màj 2025-12-01, effectif 2026-01-01, vérifié directement le 2026-07-17) : clients EEE traités par **OpenAI Ireland Limited**, transferts hors EEE fondés sur les **SCCs** ou une décision d'adéquation ; aucune mention du Data Privacy Framework.

### Cadre des transferts UE → États-Unis — vérifié le 2026-07-17
- **Aucun des trois** (Groq, OpenAI, OpenRouter) ne figure au registre officiel du EU-US **Data Privacy Framework** (interrogation directe du registre du Département du Commerce, 0 résultat sur 3 655 participants actifs, contrôle positif Microsoft OK). La décision d'adéquation UE-US ne fonde donc **aucun** des transferts de Plume.
- Les transferts reposent sur les **clauses contractuelles types** (art. 46 RGPD) : couvertes automatiquement par les DPA de **Groq** et d'**OpenAI** pour tout client API ; **non couvertes côté OpenRouter en self-serve** (DPA Enterprise uniquement).
- Rappel CNIL : vers une entité non certifiée DPF, l'exportateur (l'entreprise utilisatrice) doit s'appuyer sur les garanties de l'art. 46 et vérifier lui-même le registre.

### Données de santé (article 9 RGPD) — nuance importante
L'art. 9 interdit par principe le traitement des « données concernant la santé ». Selon la définition
CNIL, une information n'est une donnée de santé **que si elle révèle quelque chose sur l'état de santé**
d'une personne : mentionner qu'un salarié nommé a une visite périodique banale n'en révèle rien en soi ;
en revanche un **suivi individuel renforcé, une visite de reprise, un motif médical, une inaptitude ou
une restriction** en révèlent. C'est cette ligne qui guide les règles d'usage ci-dessous.

## 3. Analyse : où sont les vrais risques

1. **Le canal OpenRouter self-serve est le maillon juridiquement le plus faible** : pas de DPA signé
   (art. 28), alors que Groq et OpenAI couvrent tout client API automatiquement. Techniquement le
   contenu n'y est ni stocké ni entraîné par défaut — le risque est contractuel, pas opérationnel.
2. **Tout transfert passe par les États-Unis** (SCCs, pas de DPF) : acceptable pour du contenu
   professionnel courant, à éviter pour des données de santé nominatives.
3. **Le vocabulaire et les prompts de styles embarquent des noms de clients/collègues** envoyés à
   chaque requête — même régime de protection que le contenu dicté, mais bon à savoir.
4. La **rétention technique 30 jours** (anti-abus chez OpenAI ; dépannage chez Groq hors ZDR) est la
   seule copie durable possible du contenu dicté chez les fournisseurs.

## 4. Actions recommandées (par ordre d'impact)

1. **Activer Zero Data Retention chez Groq** (console.groq.com → Data Controls) — 2 minutes, gratuit,
   ouvert à tous : plus **aucune** rétention côté Groq, même de dépannage. À faire sur ton compte et à
   recommander dans l'email d'invitation aux collègues.
2. **Durcir le compte OpenRouter** (openrouter.ai/settings/privacy) : interdire les providers qui
   stockent/entraînent (équivalent compte du `data_collection: deny`) et ne jamais activer les deux
   opt-ins de logging.
3. **Arbitrage tranché le 2026-07-17 (décision utilisateur)** : le nettoyage **reste sur
   `openai/gpt-4o-mini` via OpenRouter**, pour la qualité. L'absence de DPA signé côté OpenRouter
   self-serve est acceptée en connaissance de cause, les mesures 1, 2 et 4 s'appliquant en
   compensation ; la bascule vers un modèle `groq/…` (Réglages → Général, deux clics, sans
   réinstallation) reste la porte de sortie si la conformité devait primer un jour.
4. **Diffuser la page ci-dessous** aux collègues équipés (email d'invitation, Confluence).
5. **Revérification annuelle** : registre DPF (dataprivacyframework.gov) et les pages sources listées
   en fin de document.

---

## 5. Page à diffuser aux collègues — « Plume et vos données, les règles simples »

> **Comment ça marche.** Quand tu dictes, ta voix est envoyée à un service de transcription (Groq,
> serveurs aux États-Unis), puis le texte à un service de mise en propre. Ces services **ne conservent
> pas** ton contenu par défaut et **ne s'en servent pas pour entraîner leurs modèles**. Plume ne garde
> rien non plus : pas d'historique de tes dictées, ni sur ton PC, ni ailleurs.
>
> **✅ Tu peux dicter sans te poser de question** : emails professionnels courants, comptes rendus,
> messages Teams, tickets, textes internes — y compris avec des noms de clients ou de collègues.
>
> **❌ À ne pas dicter (comme dans tout outil cloud non validé pour ça)** :
> - tout ce qui révèle **l'état de santé d'une personne nommée** : motif médical, inaptitude,
>   restriction de poste, diagnostic, suivi individuel renforcé, contenu d'un échange médical
>   (dire qu'un salarié a une visite périodique planifiée, sans plus, n'est pas concerné) ;
> - **mots de passe, clés d'accès, données bancaires** ;
> - documents classés confidentiels par un client.
>
> En cas de doute : tape-le au clavier au lieu de le dicter — Plume n'intervient alors pas du tout.

---

## Sources (consultées et contre-vérifiées les 2026-07-16/17)

- Groq — Your Data in GroqCloud : `https://console.groq.com/docs/your-data`
- Groq — Services Agreement (§4.2) : `https://console.groq.com/docs/legal/services-agreement`
- Groq — DPA (§8, EU SCCs) : `https://console.groq.com/docs/legal/customer-data-processing-addendum`
- OpenRouter — Data collection : `https://openrouter.ai/docs/guides/privacy/data-collection`
- OpenRouter — Provider routing (`data_collection`, `zdr`) : `https://openrouter.ai/docs/features/provider-routing`
- OpenRouter — Privacy Policy (2026-07-06) : `https://openrouter.ai/privacy`
- OpenRouter — Support DPA (Enterprise uniquement) : article Zendesk 47828437697051
- OpenAI — Your data (API) : `https://developers.openai.com/api/docs/guides/your-data`
- OpenAI — DPA (effectif 2026-01-01) : `https://openai.com/policies/data-processing-addendum/`
- Registre EU-US Data Privacy Framework : `https://www.dataprivacyframework.gov/s/participant-search`
- CNIL — Adéquation États-Unis, questions-réponses : `https://www.cnil.fr/fr/adequation-des-etats-unis-les-premieres-questions-reponses`
- CNIL — RGPD chapitre 2 (art. 9) et « Qu'est-ce qu'une donnée de santé ? »

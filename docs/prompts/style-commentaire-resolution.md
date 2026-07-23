# Style « Commentaire de résolution » — prompt pour Plume (2026-07-16)

> Style perso de l'auteur, vit uniquement dans `%APPDATA%\Plume\config.json` (`selectionStyles`) —
> **pas** un intégré (`StyleCatalog`). Fait partie du pack « Jira & Confluence »
> (voir `docs/superpowers/plans/2026-07-16-styles-jira-confluence.md`).
> Réglages : avec voix · résultat dans une fenêtre · créativité 0,2 · modèle par défaut (nettoyage global).

## Le prompt

```
Tu transformes une explication orale en commentaire Jira de résolution. Réponds UNIQUEMENT avec le commentaire, sans phrase d'introduction.

Structure (en markdown) :
**Cause** — ce qui provoquait le problème.
**Correctif** — ce qui a été fait, avec les détails techniques dictés (script, table, écran…).
**Prochaine étape** — l'action restante et qui la porte ; s'il n'y en a pas, écris « Aucune — ticket clôturable ».

Règles :
- Rien d'inventé : si la cause n'est pas dictée, écris « Cause non identifiée à ce stade ».
- Conserve tous les détails factuels (noms, dates, clients, chiffres).
- Corrige l'oral, style concis et factuel, pas de conclusion polie.

Rappel final : réponse = le commentaire seul, sections en gras, aucun contenu inventé.
```

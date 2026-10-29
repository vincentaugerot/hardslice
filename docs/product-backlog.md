# HardSlice — backlog produit

Statut : backlog actif, 29/10/2026.

Source de vérité produit : [specifications.md](specifications.md).

Roadmap : [plan.md](plan.md).

Ce document décrit les incréments qui construisent HardSlice. Aucun autre document ne peut réduire le périmètre produit ni ajouter une porte au premier incrément sans décision explicite du mainteneur.

## 1. Règles du backlog

- Chaque ticket produit un artefact utilisé par le produit : code, configuration, test ou documentation livrée avec ce code.
- Chaque ticket technique possède une commande ou un test d'acceptation exécutable.
- Le premier objectif est une boucle verticale utilisable, pas la complétude du catalogue.
- Un livrable peut être différé, mais il n'est retiré de la roadmap que par une décision explicite du mainteneur.
- Le code produit n'est pas du code jetable.
- HardSlice n'ajoute aucune dépendance au runtime de l'application générée.
- Les identifiants actifs sont `HardSlice`, `HSxxx` et `dotnet hardslice`.

## 2. Incrément actif — première boucle verticale, pré-v0.1

### Résultat attendu

> Depuis un checkout neuf, `dotnet new hardslice-api` génère une application Bookings minimale qui compile et dont les tests passent. Une référence interdite entre deux slices produit un diagnostic HS016 exploitable par un agent. Un agent corrige la violation sans contourner la règle, puis le build et les tests repassent au vert.

Le travail commence par PB-001. PB-002 ne débute qu'une fois les commandes d'acceptation de PB-001 vertes ; les tickets suivants sont abordés dans l'ordre, sauf dépendance technique explicitement constatée.

### PB-001 — Créer la solution HardSlice compilable

**Livrables**

- solution et configuration .NET 10 ;
- `HardSlice.Analyzers` cible `netstandard2.0` ; les tests et l'outillage ciblent `net10.0` lorsque pertinent ;
- projets `HardSlice.Analyzers`, `HardSlice.Analyzers.Tests` et `HardSlice.Templates` ;
- propriétés et versions centralisées ;
- CI minimale exécutant restore, build et tests.

**Acceptation**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Les trois commandes réussissent depuis un checkout propre.

### PB-002 — Créer l'application Bookings minimale

**Livrables**

- projet Domain séparé, sans dépendance runtime vers ASP.NET Core, EF Core ou HardSlice ; `HardSlice.Analyzers` peut y être consommé uniquement comme asset d'analyse au build ;
- application ASP.NET Core organisée en vertical slices ;
- au moins deux slices du module Bookings ;
- agrégat `Booking` avec un invariant minimal réel — par exemple une période dont la fin doit être postérieure au début ; l'invariant complet de chevauchement arrive en v0.1 ;
- tests unitaires et projet de tests d'architecture ;
- aucun package HardSlice chargé au runtime.

**Acceptation**

`dotnet build` et `dotnet test` réussissent. Les tests vérifient au minimum l'invariant du domaine et l'indépendance du projet Domain.

### PB-003 — Implémenter HS016

**Livrables**

- analyseur Roslyn faisant partie du produit ;
- détection entre slices d'un même assembly, forme nécessaire au premier scénario ;
- identification déterministe des modules et des slices selon la convention du template ;
- tests positifs, négatifs et cas limites couvrant au minimum : déclarations et signatures, génériques, héritage, attributs, construction, membres statiques, `typeof`, `nameof`, casts, méthodes d'extension et types imbriqués ;
- alias, global usings et classes partielles sans diagnostic en double ;
- cas autorisés : même slice, Domain et contrat public du module ;
- aucun faux positif connu dans la référence Bookings.

**Acceptation**

Une référence d'un type de `Bookings/CancelBooking` vers un type de `Bookings/ConfirmBooking` produit HS016. Les références autorisées vers le domaine ou le contrat public du module restent valides.

### PB-004 — Rendre le diagnostic HS016 réparable

**Livrables**

- message sur une ligne ;
- symbole fautif, slice source et slice cible ;
- règle en une phrase et correction attendue ;
- référence vers une fiche HS016 locale ou embarquée ;
- fiche indiquant également les contournements interdits.

**Acceptation**

Le diagnostic contient assez d'informations pour corriger la fixture sans consulter le code de l'analyseur. Forme attendue :

```text
HS016: 'CancelBookingHandler' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'. Slices never reference each other. Fix: move the shared rule into the Booking aggregate, raise a domain event, or use the module public contract. Doc: ...
```

Dans le premier incrément, le chemin documentaire local est garanti par le template `hardslice-api`. L'installation directe du seul paquet `HardSlice.Analyzers`, hors d'une solution générée, n'est pas encore un parcours supporté ; elle recevra une référence documentaire publique stable avec le kit d'adoption.

### PB-005 — Packager le template local

**Livrables**

- paquet local `HardSlice.Templates` ;
- commande `dotnet new hardslice-api` ;
- application Bookings minimale incluse ;
- analyseur HS016 installé et actif dans le projet généré ;
- fiche `docs/rules/HS016.md` présente au chemin annoncé par le diagnostic ;
- `scripts/test-template.sh`, qui construit et installe le paquet, génère une application dans un répertoire temporaire puis lance ses tests.

**Acceptation**

```text
scripts/test-template.sh
```

La solution générée compile et ses tests passent sans modification manuelle. Le script vérifie également que le chemin `Doc:` annoncé par HS016 existe depuis la racine de la solution générée.

### PB-006 — Ajouter la vérification et le guidage agent

**Livrables**

- `AGENTS.md` court dans le template ;
- `scripts/check.sh` et `scripts/check.ps1` ;
- consigne qu'un agent n'a pas terminé tant que la vérification échoue ;
- instructions pour corriger HS016 sans suppression, baisse de sévérité ni modification de l'analyseur.

**Acceptation**

Les deux scripts appliquent le même contrat logique — restore si nécessaire, build, tests unitaires et tests d'architecture — et retournent un code non nul en cas d'échec.

### PB-007 — Vérifier l'installation depuis un checkout propre

**Livrables**

- exécution de `scripts/test-template.sh` dans un environnement propre de CI ;
- checkout neuf, caches non requis et aucun fichier non versionné ;
- conservation du même scénario que PB-005, sans second harness concurrent.

**Acceptation**

Le job de CI exécute `scripts/test-template.sh` depuis un checkout propre et réussit sans état implicite de la machine du développeur.

### PB-008 — Démontrer la correction par un agent

**Livrables**

- fixture contenant une violation HS016 volontaire ;
- prompt de développement simple ;
- trace concise de la correction ;
- vérification qu'aucune règle, configuration ou test n'a été contourné.

**Acceptation**

Au moins un run de développement suit la boucle :

```text
violation HS016
→ diagnostic lu par l'agent
→ correction architecturale
→ scripts/check.sh vert
```

Cette démonstration n'est pas un benchmark statistique et ne prétend pas prouver une amélioration générale.

### Définition de fini

L'incrément est terminé lorsque PB-001 à PB-008 sont satisfaits depuis un checkout propre. Aucune porte documentaire supplémentaire ne s'applique.

## 3. Incréments suivants

| Incrément | Résultat produit | Contenu principal | Acceptation globale |
|---|---|---|---|
| **2 — Fondation v0.1** | Le template génère une application Bookings représentative | Invariant complet de chevauchement, PostgreSQL, EF Core, Testcontainers, premières règles et tests d'architecture | Le projet généré démontre domaine riche, commandes, projections de requêtes et frontières architecturales ; build et tests verts |
| **3 — Analyseurs v0.2** | Les dérives principales sont détectées par des diagnostics réparables | HS003 à HS011 et HS015, détection inter-assemblies, diagnostic de configuration pour les types applicatifs non classifiables, API bannies, fiches, `check`, `explain`, niveaux d'analyse, faux positifs et performance | Chaque règle possède fixtures conforme/fautive, tests et fiche ; aucun faux positif connu sur la référence |
| **4 — Boucle agent v0.3** | Un agent génère puis corrige par les chemins officiels | Templates d'éléments, six skills, fichiers de guidage des quatre agents, hooks après édition et de fin de tâche, scripts uniques pour hooks/pre-commit/CI, scénarios E2E | Une slice est générée, complétée et vérifiée sans recréer manuellement sa structure ; les quatre agents ciblés disposent d'un adaptateur natif ou du fallback documenté |
| **5 — Benchmark v0.4** | L'effet des garde-fous est mesuré et publié | Configurations A/B/C, tâches Bookings, runner, répétitions, coûts et qualité | Protocole, prompts, versions, résultats bruts et analyse sont publiables et reproductibles |
| **6 — Adoption v0.5** | Une équipe ajoute HardSlice sans corriger toute sa dette | `init`, `map`, conventions, baseline, cliquet, `report`, `verify`, mode couches applicable | Les violations existantes sont baselinées, une nouvelle violation est bloquée et une correction réduit la baseline |
| **7 — Comparaison v0.6** | Les architectures couches et slices sont comparées | Applications équivalentes, mêmes tâches, agents, modèles et budgets | Les conclusions distinguent l'effet de l'architecture de celui des garde-fous |
| **8 — Distribution v1.0** | HardSlice devient un produit open source installable et maintenable | Paquets stables, documentation, site, CI de publication, compatibilité, sécurité et contribution | Un utilisateur externe réussit les parcours greenfield et adoption depuis la documentation publique |

## 4. Différé, mais non abandonné

| Élément | Incrément prévu |
|---|---:|
| Bookings complet avec PostgreSQL | 2 |
| Catalogue initial complet | 3 |
| Templates d'éléments, six skills et hooks | 4 |
| Claude Code, Codex, GitHub Copilot et Gemini CLI | 4, avec adaptateur natif si disponible et fallback `AGENTS.md` + vérification sinon |
| Benchmark statistique A/B/C | 5 |
| `init`, `map`, baseline, cliquet, `report` et `verify` | 6 |
| Mode couches complet | 6–7 |
| Benchmark couches contre slices | 7 |
| Paquets stables et site public | 8 |

La recherche utilisateur et la mécanique du benchmark ne sont pas des prérequis au premier incrément.

# HardSlice — plan de livraison

Statut : roadmap produit active, 30/10/2026.

**Source de vérité produit** : [specifications.md](specifications.md).

**Backlog actif** : [product-backlog.md](product-backlog.md).

Ce plan organise la livraison de la spécification. Il peut phaser un livrable, mais ne peut ni le supprimer ni le rendre définitivement conditionnel sans décision explicite du mainteneur.

---

## 1. Produit à construire

> HardSlice est un kit open source .NET qui fait respecter, par le build, une architecture en vertical slices avec un domaine riche aux agents de code, pour les projets neufs comme pour les solutions existantes, et qui mesure publiquement son efficacité.

HardSlice n'est pas un framework d'exécution pour construire des agents. C'est une chaîne d'outils d'architecture destinée aux dépôts qu'ils modifient.

Les cinq piliers engagés sont :

1. **Générer** : templates de solution et d'éléments.
2. **Contraindre** : analyseurs Roslyn, dont les interdictions d'API, et tests d'architecture appliqués par le build.
3. **Faire corriger** : diagnostics, fiches, instructions, skills et hooks qui ramènent l'agent au vert.
4. **Adopter progressivement** : cartographie, conventions, baseline, cliquet et rapport sur une solution existante.
5. **Mesurer** : benchmarks publics, reproductibles et transparents.

Aucun composant HardSlice n'est requis au runtime de l'application générée.

## 2. Décisions de livraison

| Sujet | Décision |
|---|---|
| Nom | **HardSlice**, préfixe `HS`, paquets `HardSlice.*` |
| Première entrée produit | **Greenfield** : template et application Bookings minimale |
| Adoption de l'existant | Engagement de roadmap après la première boucle produit |
| Premier garde-fou | **HS016** : aucune dépendance entre slices |
| Architecture de référence | .NET 10, ASP.NET Core Minimal APIs, vertical slices, domaine séparé |
| Distribution | Paquets NuGet et outil local épinglé ; aucune copie d'analyseurs dans les projets utilisateurs |
| CLI canonique | `dotnet hardslice <commande>` |
| Vérification | Build et tests comme autorité locale ; CI protégée comme autorité finale |
| Langue publique | Code, diagnostics et documentation publique en anglais |
| Classification des types | Contrats applicatifs `IEntity`, `IAggregateRoot`, `IDomainEvent` et contrats de messagerie, déclarés dans `hardslice.json` et reconnus par symbole ; aucune heuristique de forme (30/10/2026) |
| HS010 et HS011 | Analyseurs HardSlice ciblés avec leurs propres identifiants, au lieu de `BannedApiAnalyzers` qui les confondrait sous `RS0030` (30/10/2026) |
| Vérification outillée | `dotnet hardslice check` dès v0.2 ; `scripts/check.sh` et `check.ps1` le lancent ; `dotnet build` seul produit toujours les diagnostics |
| Benchmark | Après une première boucle produit utilisable, jamais comme prérequis au premier code |
| Templates d'éléments | Ils créent les fichiers de leur élément et affichent les enregistrements à ajouter, que la skill ajoute ; le code généré compile sans diagnostic HS, ses tests et la vérification échouent tant qu'un marqueur `TODO(hardslice):` reste (31/10/2026) |
| Événements de domaine | Publiés à leurs gestionnaires dans la transaction de la commande ; un gestionnaire vit dans une slice de réaction, agit par `AppDbContext`, sans `IMediator` ni effet externe ; pas d'outbox (31/10/2026) |
| Vérification des hooks | `check` complet reste le seul verdict ; `--build` après édition ; le hook de fin lance `check` s'il tient en 60 s, sinon un mode réduit aux slices touchées, selon la mesure de PB-045 (31/10/2026) |
| HS000 | Suppressions écrites dans le code source seulement ; les réglages globaux sont protégés par les hooks et la CI, puis repris en v0.5 avec la baseline (31/10/2026) |

Les choix fonctionnels de la spécification restent engagés. Contraintes transverses retenues : niveaux d'analyse, baseline par occurrence, contrôle des faux positifs, budgets de performance et versions CLI/analyseurs identiques.

## 3. Mode de livraison

HardSlice est construit par tranches verticales utilisables. Chaque incrément doit :

- produire un artefact compilable, installable ou générable ;
- posséder une commande d'acceptation reproductible ;
- démontrer une capacité visible pour l'utilisateur ou l'agent ;
- inclure les tests de cette capacité ;
- ne pas dépendre d'un protocole futur qui n'est pas nécessaire à son fonctionnement.

Un spike est autorisé uniquement lorsqu'une inconnue bloque le code en cours. Il reste borné et ne devient pas une phase de gouvernance autonome.

La recherche sur l'adoption est planifiée pour v0.5. Le mainteneur peut la reprendre plus tôt en parallèle avec un protocole actif et validé, mais elle ne bloque pas la première boucle codée.

## 4. Premier jalon — incrément 1, pré-v0.1

### Résultat attendu

> Depuis un checkout neuf, `dotnet new hardslice-api` génère une application Bookings minimale qui compile et dont les tests passent. Une dépendance interdite entre deux slices produit un diagnostic HS016 orienté réparation. Un agent corrige la violation sans contourner la règle, puis le build et les tests repassent au vert.

### Livrables

- solution .NET compilable ;
- `HardSlice.Analyzers` et ses tests ;
- HS016 pour les références entre slices d'un même assembly, forme nécessaire au premier scénario ;
- diagnostic indiquant le symbole fautif, les deux slices, la correction attendue et la documentation ;
- application Bookings minimale avec domaine séparé et deux slices ;
- package local du template `hardslice-api` ;
- `AGENTS.md`, `scripts/check.sh` et `scripts/check.ps1` ;
- fiche de règle HS016 ;
- scénario de développement E2E : violation semée, correction par agent, retour au vert ;
- CI exécutant le build et les tests.

### Acceptation

Depuis un checkout propre, la solution du dépôt passe :

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Puis `scripts/test-template.sh` construit et installe le paquet local, génère `Demo` dans un répertoire temporaire et y exécute les tests. Sur une fixture fautive, `dotnet build` échoue avec HS016 et son instruction de réparation. Après correction par l'agent, `scripts/check.sh` retourne `0`.

Le benchmark statistique, la baseline complète, le runner isolé, le catalogue complet et le support de plusieurs agents ne font pas partie de ce jalon. Ils restent engagés dans les versions suivantes.

## 5. Roadmap

### v0.1 — Template utilisable et architecture de référence

- compléter l'application Bookings avec l'invariant de chevauchement représentatif et son domaine complet ;
- livrer commandes, requêtes, endpoints et tests ;
- ajouter les tests d'architecture HS001, HS012 à HS014 et HS016 à HS018 ;
- fiabiliser le template de solution et documenter son utilisation.

**Résultat visible** : un utilisateur génère une application cohérente et peut la modifier avec un agent.

### v0.2 — Analyseurs et diagnostics réparables

- classer entités, agrégats, événements, commandes, requêtes et handlers par des contrats applicatifs (`IEntity`, `IAggregateRoot`), y compris depuis un projet référencé, avec les diagnostics de configuration HS900 à HS902 ;
- livrer progressivement HS003 à HS011, HS015 et HS019 ; HS010 et HS011 sont des analyseurs HardSlice avec leurs propres identifiants, et non des listes `BannedApiAnalyzers` ;
- fournir les fiches des règles, `dotnet hardslice check` et `explain` dans un outil local épinglé à la version des analyseurs ;
- ajouter les niveaux d'analyse `Minimum`, `Recommended` et `All`, les tests de faux positifs, les cas inter-assemblies et la mesure de performance sur un build incrémental réel ;
- publier les premiers paquets NuGet en préversion (`0.2.0-preview.1`).

**Résultat visible** : les dérives principales échouent avec des messages que l'agent peut corriger.

### v0.3 — Génération et boucle agent

- livrer les templates `hardslice-command`, `hardslice-query`, `hardslice-module`, `hardslice-aggregate` et `hardslice-event` ;
- publier les événements de domaine à leurs gestionnaires, prérequis de `hardslice-event` et de la skill `add-domain-event` ;
- livrer les six skills de la spécification et HS000 ;
- livrer `AGENTS.md`, `CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md` ;
- pour Claude Code, ajouter le build incrémental après édition et le hook de fin de tâche prévus par la spécification ;
- cibler Claude Code, Codex, GitHub Copilot et Gemini CLI : adaptateur natif lorsqu'il existe, sinon fallback explicite par `AGENTS.md` et le script de vérification ;
- faire des scripts de vérification le point unique appelé par les hooks, le pre-commit et la CI ;
- tester la boucle E2E de chaque règle bloquante.

**Résultat visible** : l'agent génère, complète, vérifie et corrige une fonctionnalité par les chemins officiels.

### v0.4 — Benchmark avec et sans HardSlice

- exécuter le protocole A/B/C sur Bookings ;
- utiliser un runner isolé et des tâches figées ;
- mesurer réussite, violations, coût, durée, itérations, fichiers lus et tokens lorsque disponibles ;
- publier les prompts, les résultats bruts et les résultats défavorables.

**Résultat visible** : une mesure publique et reproductible de l'effet de HardSlice.

### v0.5 — Adoption sur une solution existante

- livrer `init`, `map`, `baseline`, `report` et `verify` ;
- rendre les conventions configurables ;
- identifier les occurrences de baseline et appliquer le cliquet localement et en CI ;
- adopter progressivement sans déplacer automatiquement les fichiers ;
- valider le parcours sur au moins une solution externe ;
- couvrir les règles communes applicables aux architectures en couches.

**Résultat visible** : une équipe bloque les nouvelles dérives sans corriger immédiatement toute sa dette.

### v0.6 — Benchmark couches contre slices

- construire deux applications et ensembles de règles comparables ;
- exécuter les mêmes tâches avec les mêmes agents, modèles et budgets ;
- mesurer le contexte lu, les tokens, les violations, le coût et la durée ;
- publier les limites et les cas défavorables.

**Résultat visible** : une comparaison publique et chiffrée des deux architectures pour les agents.

### v1.0 — Distribution stable

- publier les packages NuGet stables des templates, analyseurs et outils ;
- fournir la documentation complète et versionnée ;
- tester les parcours greenfield et adoption de bout en bout ;
- fournir CI GitHub Actions et Azure Pipelines, politique de compatibilité, sécurité et contribution.

## 6. Traçabilité avec la spécification

| Livrable de la spécification | Première livraison | Complétude visée |
|---|---|---|
| Template de solution | Premier jalon | v0.1 |
| Templates d'éléments | v0.3 | v1.0 |
| Analyseurs Roslyn | Premier jalon avec HS016 | v0.2 |
| Tests d'architecture | v0.1 | v0.2 |
| Kit agents | Premier jalon minimal | v0.3 |
| Scripts de vérification | Premier jalon | v0.3 |
| Outil `dotnet hardslice` | v0.2 (`check`, `explain`) | v1.0 |
| Outil d'adoption (`init`, `baseline`…) | v0.5 | v1.0 |
| Benchmark | v0.4 | v0.6 |
| Documentation des règles | Premier jalon avec HS016 | v0.2 et v1.0 |

Aucun de ces livrables n'est conditionnel. Leur conception ou leur priorité peut évoluer, mais tout abandon exige une décision produit explicite.

## 7. Contrats techniques transverses

### Diagnostics

Chaque diagnostic fournit le symbole et l'emplacement fautifs, la règle en une phrase, une correction architecturale concrète, les contournements à éviter et une référence accessible par `dotnet hardslice explain`.

### Qualité des règles

Avant sa publication comme règle bloquante, une règle possède une sémantique déterministe, des tests positifs et négatifs, les cas inter-assemblies pertinents, aucun faux positif connu accepté silencieusement, un coût mesuré et une fiche de correction. Le premier incrément local peut démontrer HS016 avant sa mesure de performance, prévue en v0.2.

### Distribution

Les analyseurs et l'outil utilisent la même version. L'outil est épinglé localement dans `.config/dotnet-tools.json`. Le template référence des packages, pas des copies susceptibles de dériver.

### Adoption et autorité

Les garde-fous locaux visent les dérives accidentelles ; ils ne constituent pas une frontière de sécurité. La branche protégée et les contrôles CI restent l'autorité finale sur les fichiers sensibles et la croissance de la baseline.

## 8. Validation future

Le protocole détaillé du benchmark, son isolation et ses règles d'intégrité seront conçus à l'approche de v0.4. La recherche d'adoption sera préparée pour v0.5, avec validation avant tout contact humain. Ni l'une ni l'autre ne constitue un prérequis aux incréments v0.1 à v0.3.

## 9. Prochaine action

Les incréments 1 « boucle verticale HardSlice » et 2 « Fondation v0.1 » sont terminés. Exécuter l'incrément actif « Analyseurs v0.2 » (PB-018 à PB-033) décrit dans [product-backlog.md](product-backlog.md).

Commencer par PB-018 (classification), qui débloque les règles du domaine. PB-019 (performance), PB-020 (niveaux d'analyse), PB-021 (outil) et les règles HS003, HS011 et HS015 avancent en parallèle. Les prérequis de la préversion NuGet sont à réunir par le mainteneur pendant l'incrément ; seule la publication finale (PB-033) les attend.

L'incrément suivant, « Boucle agent v0.3 », est découpé en PB-034 à PB-051 dans le [backlog](product-backlog.md) ; ses arbitrages ont été validés le 31/10/2026 et il peut démarrer en parallèle de la publication de PB-033, que seul PB-051 attend.

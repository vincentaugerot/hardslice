# Coût des analyseurs HardSlice

Ce rapport mesure ce que les analyseurs de `HardSlice.Analyzers` ajoutent à un build réel, et fixe les seuils qu'ils doivent respecter (PB-019). Il mesure aussi les vérifications que lancent les hooks des agents, contre leurs budgets (PB-045). Chaque ticket de règle y enregistre sa mesure.

```bash
scripts/measure-analyzers.sh
```

Le script remplace la dernière mesure ci-dessous et ajoute une ligne à l'historique ; le reste du rapport n'est jamais réécrit. `--report <fichier>` écrit dans un autre fichier, `--work-dir <dossier vide>` garde la fixture pour l'examiner. Le scénario E lance les tests d'intégration : le script exige un runtime de conteneurs compatible Docker. Compter quelques minutes (environ huit sur une machine de 4 cœurs).

## Fixture

`PerfApp`, générée à chaque mesure depuis les paquets locaux, comme `scripts/test-template.sh` : `scripts/generate-app.sh` empaquette `HardSlice.Analyzers` et `HardSlice.Templates`, installe le template et génère la solution avec son exemple Bookings. `scripts/generate-perf-modules.sh` y ajoute ensuite 20 modules synthétiques, `Module01` à `Module20`, écrits comme Bookings et identiques d'une exécution à l'autre :

- dans le Domain, un agrégat `Item` (`IAggregateRoot`), son identifiant, son statut et un événement `ItemCreated` ;
- dans l'application, son mapping EF Core au niveau du module, la classe du module (enregistrements et routes) et 10 slices : 5 commandes (`CreateItem`, `RenameItem`, `AdjustQuantity`, `ArchiveItem`, `RestoreItem`), chacune avec son validateur, son handler et son endpoint, et 5 requêtes (`GetItem`, `ListItems`, `CountItems`, `FindItemsByName`, `ListArchivedItems`) ;
- `Program.cs` enregistre et expose chaque module après Bookings.

Les modules synthétiques n'ont ni tests ni migration : la fixture sert au build, pas à la vérification complète. Le build porte sur toute la solution, projets de tests compris, comme celui d'un développeur.

Le scénario E porte sur une copie de `PerfApp` faite avant l'ajout des modules synthétiques : l'application générée avec Bookings seul, ses tests et ses migrations, sur laquelle `scripts/check.sh` passe.

La variante sans analyseurs est une copie de la fixture dont le `Directory.Build.targets` retire les éléments `Analyzer` du paquet `HardSlice.Analyzers` après leur résolution. Le paquet reste référencé et ses fichiers MSBuild importés : seul le chargement des analyseurs disparaît. Aucune propriété de désactivation n'est ajoutée au paquet. Le script vérifie, avant de mesurer, que la variante avec analyseurs charge `HardSlice.Analyzers` et que l'autre ne le charge pas.

## Scénarios

Chacun est mesuré avec et sans analyseurs :

| Scénario | Build | Modification avant chaque build |
|---|---|---|
| A | `dotnet build --no-restore` | corps de la méthode `Handle` de la slice `Module10/ListItems` (constante locale) ; l'assembly de référence ne change pas, seul `PerfApp` recompile |
| B | `dotnet build --no-restore` | API publique de l'agrégat `Module10/Item` (méthode publique renommée) ; le Domain recompile, puis `PerfApp` et les projets de tests qui en dépendent |
| C | `dotnet build --no-restore --no-incremental` | aucune ; build complet de référence |

Un build sans changement n'est pas mesuré : il peut ne rien analyser. Chaque build de A et B compile une valeur jamais écrite auparavant dans sa variante.

Les vérifications des hooks des agents sont mesurées par les scripts qu'appellent les hooks, `dotnet tool restore` compris, avec analyseurs seulement :

| Scénario | Hook | Commande | Application | Modification avant chaque exécution |
|---|---|---|---|---|
| D | après chaque édition | `scripts/check.sh --build` | fixture avec analyseurs | celle de A |
| E | avant que l'agent rende la main | `scripts/check.sh` | Bookings seul | commentaire ajouté à la fin du fichier de la slice `Bookings/GetBooking` ; `PerfApp` recompile, puis toute la vérification tourne, conteneur PostgreSQL des tests d'intégration compris |

Chaque exécution doit réussir : une vérification en échec arrête la mesure.

## Méthode

- restauration et premier build de chaque variante avant toute mesure ; la restauration ne dépend pas des analyseurs et n'est pas mesurée (`--no-restore`) ;
- serveur de compilation actif (comportement par défaut de `dotnet build`), partagé par les deux variantes ;
- par scénario, un build de chauffe par variante, puis cinq builds mesurés par variante, en alternance avec puis sans analyseurs, pour que la dérive du runner touche les deux séries ; pour D et E, une exécution de chauffe puis cinq exécutions mesurées ;
- durée totale de la commande `dotnet build`, ou du script pour D et E, horloge murale ; médiane des cinq mesures retenue, minimum et maximum publiés ;
- un dernier build complet avec `-p:ReportAnalyzer=true` donne le temps cumulé de chaque analyseur HardSlice par projet. Il explique un surcoût ; seuls les seuils sur la durée totale font foi.

## Seuils

Sur la médiane, pour la fixture, le SDK épinglé par `global.json` et le runner `ubuntu-latest` documentés :

| Scénario | Durée totale avec analyseurs | Surcoût des analyseurs HardSlice |
|---|---:|---:|
| A | ≤ 10 s, budget du build après édition de la spécification | ≤ 1 s |
| B | ≤ 20 s | ≤ 2 s |
| C | mesuré, sans seuil | ≤ 15 % |

Le surcoût est la médiane avec analyseurs moins la médiane sans analyseurs ; pour C, rapporté à la médiane sans analyseurs. Un seuil ne change que par une décision du mainteneur consignée dans ce rapport, jamais automatiquement.

Les vérifications des hooks ont les budgets de la section 9 de la spécification, sur la médiane :

| Scénario | Durée totale |
|---|---:|
| D, après édition | ≤ 10 s |
| E, avant de rendre la main | ≤ 60 s |

## Dépassement d'un seuil

Un dépassement est d'abord qualifié, dans le rapport et par le code de sortie du script :

| Qualification | Condition | Code de sortie | Suite |
|---|---|---:|---|
| Variabilité de l'environnement | la dispersion d'une des deux séries du scénario (maximum moins minimum, rapporté à la médiane) dépasse 20 % | 3 | série instable : mesure relancée sur un nouveau runner, sans conclusion |
| Coût du build sans HardSlice | la variante sans analyseurs dépasse déjà le budget de durée totale | 0, avec un avertissement | le dépassement ne vient pas des analyseurs : arbitrage sur la fixture, le runner ou le budget |
| Surcoût des analyseurs | séries stables, et le surcoût dépasse son seuil, ou la variante sans analyseurs respecte le budget que la variante avec analyseurs dépasse | 4 | `ReportAnalyzer` désigne l'analyseur en cause |
| Budget d'un hook | série D ou E stable, au-dessus de son budget | 0, avec un avertissement | arbitrage sur la vérification du hook : pour E, la décision ci-dessous prévoit alors `--affected` |

Une série D ou E instable au-dessus de son budget est qualifiée comme une variabilité de l'environnement (code 3).

Un surcoût n'est une régression confirmée qu'après deux exécutions stables consécutives au-dessus du seuil. Il bloque alors l'intégration du ticket qui l'introduit, et seulement celui-là. Les autres qualifications ne font pas échouer les pull requests : elles ouvrent un arbitrage.

## Intégration continue

Le workflow `.github/workflows/performance.yml` tourne sur `main` et sur les pull requests qui touchent les analyseurs, le template, les scripts de mesure ou le SDK épinglé :

- le job `performance` mesure sur un runner `ubuntu-latest` neuf. Il échoue seulement si la mesure n'aboutit pas (build en échec, fixture invalide) ; une série instable ou un surcoût passe la main au job suivant ;
- le job `performance-rerun` ne tourne qu'après une série instable ou un surcoût, sur un autre runner neuf. Il réussit si la seconde mesure respecte les seuils (ou ne relève que le coût du build sans HardSlice). Il échoue si deux surcoûts stables se suivent, régression confirmée, et aussi si la seconde mesure reste instable ou ne donne qu'un surcoût non confirmé : relancer alors le workflow, aucune conclusion n'est possible sur ces exécutions.

Chaque job publie la mesure dans son résumé GitHub Actions. Le rapport n'est pas commité par la CI : un ticket de règle enregistre sa mesure en lançant le script et en commitant ce fichier.

## Décisions du mainteneur

Les seuils des analyseurs sont ceux de PB-019.

### Vérification des hooks, 31/10/2026 (PB-045)

Arbitrage de l'incrément v0.3 : la vérification complète, `dotnet hardslice check`, reste le seul verdict ; le hook après édition lance `scripts/check.sh --build` ; le hook avant de rendre la main lance `scripts/check.sh` tel quel si la mesure de `check` complet sur une application générée avec Bookings tient en 60 s, et sinon une vérification réduite aux slices touchées, `--affected`.

Mesure du 31/10/2026 sur la machine locale (4 cœurs, voir la dernière mesure ci-dessous) : `scripts/check.sh` complet en 18,61 s de médiane (scénario E), `scripts/check.sh --build` en 4,86 s sur la fixture (scénario D), loin des budgets. Décision : le hook de fin lance `scripts/check.sh` ; `--affected` n'est pas livré, et `check.sh` refuse tout argument autre que `--build`. Le job `performance` mesure les deux sur `ubuntu-latest` à chaque exécution : un dépassement stable du budget de E rouvre la décision et le ticket qui livrerait `--affected`.

## Dernière mesure

<!-- measure-analyzers:latest:begin -->

### Mesure du 2026-11-03

- Commit : `804ca5e, avec des modifications non commitées`
- Règles HardSlice : HS000, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902
- SDK effectif : 10.0.401
- Runner : machine locale, Linux 6.8.0-147-generic
- Processeur : AMD EPYC 9354P 32-Core Processor, 4 cœurs
- Mémoire : 15.6 Gio
- Fixture : PerfApp, Bookings et 20 modules synthétiques de 10 slices, 360 fichiers C# et 11433 lignes sous `src/`
- Verdict : seuils respectés

Durées en secondes, une chauffe puis 5 builds mesurés par variante, en alternance :

| Scénario | Analyseurs | Minimum | Médiane | Maximum | Dispersion |
|---|---|---:|---:|---:|---:|
| A | avec | 4.11 | 4.24 | 7.94 | 90.2 % |
| A | sans | 3.78 | 4.07 | 4.45 | 16.4 % |
| B | avec | 5.26 | 5.37 | 5.56 | 5.6 % |
| B | sans | 4.91 | 5.17 | 7.54 | 50.9 % |
| C | avec | 6.20 | 6.98 | 8.81 | 37.4 % |
| C | sans | 5.63 | 6.29 | 7.52 | 30.1 % |

Seuils, sur la médiane :

| Scénario | Mesure | Valeur | Seuil | Résultat |
|---|---|---:|---:|---|
| A | Durée totale avec analyseurs | 4.24 s | ≤ 10.00 s | respecté |
| A | Surcoût des analyseurs | 0.17 s | ≤ 1.00 s | respecté |
| B | Durée totale avec analyseurs | 5.37 s | ≤ 20.00 s | respecté |
| B | Surcoût des analyseurs | 0.20 s | ≤ 2.00 s | respecté |
| C | Surcoût des analyseurs | 10.9 % | ≤ 15 % | respecté |

Vérifications des hooks des agents, une exécution de chauffe puis 5 exécutions mesurées, en secondes. D sur la fixture avec analyseurs ; E sur PerfApp généré avec Bookings seul (80 fichiers C# sous `src/` et `tests/`), conteneur PostgreSQL compris :

| Scénario | Hook | Commande | Minimum | Médiane | Maximum | Dispersion | Budget | Résultat |
|---|---|---|---:|---:|---:|---:|---:|---|
| D | après édition | `scripts/check.sh --build` | 4.91 | 5.26 | 5.38 | 8.9 % | ≤ 10.00 s | respecté |
| E | avant de rendre la main | `scripts/check.sh` | 19.07 | 20.40 | 21.80 | 13.4 % | ≤ 60.00 s | respecté |

`-p:ReportAnalyzer=true`, build complet avec analyseurs : temps cumulé de chaque analyseur HardSlice, puis de tous les analyseurs du projet.
Ces temps se chevauchent (exécution concurrente) : ils expliquent un surcoût, seuls les seuils sur la durée totale font foi.

| Projet | Analyseur HardSlice | Temps | Tous les analyseurs |
|---|---|---:|---:|
| PerfApp.Domain | SuppressionJustificationAnalyzer (HS000) | 0.002 s | 0.025 s |
| PerfApp.Domain | SliceStructureAnalyzer (HS004) | 0.002 s | 0.025 s |
| PerfApp.Domain | SliceReferenceAnalyzer (HS016) | 0.002 s | 0.025 s |
| PerfApp.Domain | AggregateReferenceAnalyzer (HS008) | 0.001 s | 0.025 s |
| PerfApp.Domain | AsyncUsageAnalyzer (HS011) | <0.001 s | 0.025 s |
| PerfApp.Domain | SystemClockAnalyzer (HS010) | <0.001 s | 0.025 s |
| PerfApp.Domain | EntityStateAnalyzer (HS007) | <0.001 s | 0.025 s |
| PerfApp.Domain | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.025 s |
| PerfApp.Domain | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.025 s |
| PerfApp.Domain | DomainEventRecordAnalyzer (HS009) | <0.001 s | 0.025 s |
| PerfApp.Domain | CommandResponseAnalyzer (HS005) | <0.001 s | 0.025 s |
| PerfApp.Domain | QueryHandlerTrackingAnalyzer (HS006) | <0.001 s | 0.025 s |
| PerfApp.Domain | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.025 s |
| PerfApp.Domain | DomainEventNameAnalyzer (HS019) | <0.001 s | 0.025 s |
| PerfApp | SliceReferenceAnalyzer (HS016) | 0.971 s | 1.433 s |
| PerfApp | SliceStructureAnalyzer (HS004) | 0.102 s | 1.433 s |
| PerfApp | EndpointDatabaseAccessAnalyzer (HS003) | 0.012 s | 1.433 s |
| PerfApp | SliceEndpointAnalyzer (HS015) | 0.009 s | 1.433 s |
| PerfApp | ClassificationAnalyzer (HS900, HS901, HS902) | 0.007 s | 1.433 s |
| PerfApp | QueryHandlerTrackingAnalyzer (HS006) | 0.005 s | 1.433 s |
| PerfApp | CommandResponseAnalyzer (HS005) | 0.003 s | 1.433 s |
| PerfApp | SuppressionJustificationAnalyzer (HS000) | 0.003 s | 1.433 s |
| PerfApp | AsyncUsageAnalyzer (HS011) | 0.002 s | 1.433 s |
| PerfApp | AggregateReferenceAnalyzer (HS008) | 0.001 s | 1.433 s |
| PerfApp | SystemClockAnalyzer (HS010) | <0.001 s | 1.433 s |
| PerfApp | EntityStateAnalyzer (HS007) | <0.001 s | 1.433 s |
| PerfApp | DomainEventRecordAnalyzer (HS009) | <0.001 s | 1.433 s |
| PerfApp | DomainEventNameAnalyzer (HS019) | <0.001 s | 1.433 s |
| PerfApp.ArchitectureTests | SuppressionJustificationAnalyzer (HS000) | 0.009 s | 0.262 s |
| PerfApp.ArchitectureTests | SliceReferenceAnalyzer (HS016) | 0.003 s | 0.262 s |
| PerfApp.ArchitectureTests | AsyncUsageAnalyzer (HS011) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | AggregateReferenceAnalyzer (HS008) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | QueryHandlerTrackingAnalyzer (HS006) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | CommandResponseAnalyzer (HS005) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | SystemClockAnalyzer (HS010) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | SliceStructureAnalyzer (HS004) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | EntityStateAnalyzer (HS007) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | DomainEventRecordAnalyzer (HS009) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.262 s |
| PerfApp.ArchitectureTests | DomainEventNameAnalyzer (HS019) | <0.001 s | 0.262 s |
| PerfApp.IntegrationTests | SliceReferenceAnalyzer (HS016) | 0.042 s | 0.113 s |
| PerfApp.IntegrationTests | SuppressionJustificationAnalyzer (HS000) | 0.002 s | 0.113 s |
| PerfApp.IntegrationTests | AggregateReferenceAnalyzer (HS008) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | AsyncUsageAnalyzer (HS011) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | SystemClockAnalyzer (HS010) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | EntityStateAnalyzer (HS007) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | QueryHandlerTrackingAnalyzer (HS006) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | CommandResponseAnalyzer (HS005) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | SliceStructureAnalyzer (HS004) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | DomainEventRecordAnalyzer (HS009) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.113 s |
| PerfApp.IntegrationTests | DomainEventNameAnalyzer (HS019) | <0.001 s | 0.113 s |
| PerfApp.Tests | SliceReferenceAnalyzer (HS016) | 0.004 s | 0.056 s |
| PerfApp.Tests | SuppressionJustificationAnalyzer (HS000) | 0.001 s | 0.056 s |
| PerfApp.Tests | AggregateReferenceAnalyzer (HS008) | <0.001 s | 0.056 s |
| PerfApp.Tests | AsyncUsageAnalyzer (HS011) | <0.001 s | 0.056 s |
| PerfApp.Tests | EntityStateAnalyzer (HS007) | <0.001 s | 0.056 s |
| PerfApp.Tests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.056 s |
| PerfApp.Tests | DomainEventRecordAnalyzer (HS009) | <0.001 s | 0.056 s |
| PerfApp.Tests | CommandResponseAnalyzer (HS005) | <0.001 s | 0.056 s |
| PerfApp.Tests | SystemClockAnalyzer (HS010) | <0.001 s | 0.056 s |
| PerfApp.Tests | SliceStructureAnalyzer (HS004) | <0.001 s | 0.056 s |
| PerfApp.Tests | QueryHandlerTrackingAnalyzer (HS006) | <0.001 s | 0.056 s |
| PerfApp.Tests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.056 s |
| PerfApp.Tests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.056 s |
| PerfApp.Tests | DomainEventNameAnalyzer (HS019) | <0.001 s | 0.056 s |

<!-- measure-analyzers:latest:end -->

## Historique

Médianes en secondes, avec / sans analyseurs pour A, B et C ; avec analyseurs pour les hooks D et E, mesurés depuis PB-045 (« – » avant).

| Date | Commit | Règles HardSlice | Runner | A | B | C | D | E | Verdict |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| 2026-10-30 | `7c2b785` | HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.76 / 3.72 | 4.62 / 4.36 | 4.76 / 4.84 | – | – | pass |
| 2026-10-30 | `1dcb109` | HS003, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.38 / 3.15 | 4.07 / 3.68 | 4.50 / 4.22 | – | – | pass |
| 2026-10-30 | `460b963` | HS003, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.27 / 3.04 | 4.07 / 3.73 | 4.39 / 3.96 | – | – | pass |
| 2026-10-30 | `027b032` | HS003, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.57 / 3.37 | 3.86 / 3.34 | 4.12 / 3.76 | – | – | pass |
| 2026-10-30 | `ce292d4` | HS003, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.09 / 2.95 | 3.88 / 3.63 | 4.07 / 3.66 | – | – | pass |
| 2026-10-30 | `ba18a28` | HS003, HS004, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.46 / 3.14 | 4.02 / 4.01 | 4.47 / 4.21 | – | – | pass |
| 2026-10-30 | `2c4af08` | HS003, HS004, HS005, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.65 / 3.29 | 4.23 / 3.91 | 4.31 / 4.15 | – | – | pass |
| 2026-10-30 | `7436ad4` | HS003, HS004, HS005, HS006, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.21 / 2.85 | 3.78 / 3.47 | 4.06 / 3.70 | – | – | pass |
| 2026-10-30 | `77c1e44` | HS003, HS004, HS005, HS006, HS007, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.06 / 2.73 | 3.44 / 3.21 | 3.94 / 3.72 | – | – | pass |
| 2026-10-30 | `d4df85b` | HS003, HS004, HS005, HS006, HS007, HS008, HS010, HS011, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.05 / 2.72 | 3.61 / 3.46 | 4.08 / 3.70 | – | – | pass |
| 2026-10-30 | `bb57729` | HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.05 / 2.84 | 3.76 / 3.40 | 4.13 / 3.77 | – | – | pass |
| 2026-10-31 | `b7141a1` | HS000, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.21 / 2.85 | 4.10 / 3.86 | 4.58 / 4.13 | – | – | pass |
| 2026-10-31 | `5eb9efc` | HS000, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 4.20 / 4.02 | 5.15 / 4.99 | 5.80 / 5.47 | 4.86 | 18.61 | pass |
| 2026-11-02 | `61c727e` | HS000, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.82 / 3.43 | 4.17 / 3.78 | 4.77 / 4.15 | 4.19 | 22.79 | pass |
| 2026-11-03 | `804ca5e` | HS000, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS015, HS016, HS019, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 4.24 / 4.07 | 5.37 / 5.17 | 6.98 / 6.29 | 5.26 | 20.40 | pass |
<!-- measure-analyzers:history -->

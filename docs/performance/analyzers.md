# Coût des analyseurs HardSlice

Ce rapport mesure ce que les analyseurs de `HardSlice.Analyzers` ajoutent à un build réel, et fixe les seuils qu'ils doivent respecter (PB-019). Chaque ticket de règle y enregistre sa mesure.

```bash
scripts/measure-analyzers.sh
```

Le script remplace la dernière mesure ci-dessous et ajoute une ligne à l'historique ; le reste du rapport n'est jamais réécrit. `--report <fichier>` écrit dans un autre fichier, `--work-dir <dossier vide>` garde la fixture pour l'examiner. Compter quelques minutes (environ cinq sur une machine de 4 cœurs).

## Fixture

`PerfApp`, générée à chaque mesure depuis les paquets locaux, comme `scripts/test-template.sh` : `scripts/generate-app.sh` empaquette `HardSlice.Analyzers` et `HardSlice.Templates`, installe le template et génère la solution avec son exemple Bookings. `scripts/generate-perf-modules.sh` y ajoute ensuite 20 modules synthétiques, `Module01` à `Module20`, écrits comme Bookings et identiques d'une exécution à l'autre :

- dans le Domain, un agrégat `Item` (`IAggregateRoot`), son identifiant, son statut et un événement `ItemCreated` ;
- dans l'application, son mapping EF Core au niveau du module, la classe du module (enregistrements et routes) et 10 slices : 5 commandes (`CreateItem`, `RenameItem`, `AdjustQuantity`, `ArchiveItem`, `RestoreItem`), chacune avec son validateur, son handler et son endpoint, et 5 requêtes (`GetItem`, `ListItems`, `CountItems`, `FindItemsByName`, `ListArchivedItems`) ;
- `Program.cs` enregistre et expose chaque module après Bookings.

Les modules synthétiques n'ont ni tests ni migration : la fixture sert au build, pas à `scripts/check.sh`. Le build porte sur toute la solution, projets de tests compris, comme celui d'un développeur.

La variante sans analyseurs est une copie de la fixture dont le `Directory.Build.targets` retire les éléments `Analyzer` du paquet `HardSlice.Analyzers` après leur résolution. Le paquet reste référencé et ses fichiers MSBuild importés : seul le chargement des analyseurs disparaît. Aucune propriété de désactivation n'est ajoutée au paquet. Le script vérifie, avant de mesurer, que la variante avec analyseurs charge `HardSlice.Analyzers` et que l'autre ne le charge pas.

## Scénarios

Chacun est mesuré avec et sans analyseurs :

| Scénario | Build | Modification avant chaque build |
|---|---|---|
| A | `dotnet build --no-restore` | corps de la méthode `Handle` de la slice `Module10/ListItems` (constante locale) ; l'assembly de référence ne change pas, seul `PerfApp` recompile |
| B | `dotnet build --no-restore` | API publique de l'agrégat `Module10/Item` (méthode publique renommée) ; le Domain recompile, puis `PerfApp` et les projets de tests qui en dépendent |
| C | `dotnet build --no-restore --no-incremental` | aucune ; build complet de référence |

Un build sans changement n'est pas mesuré : il peut ne rien analyser. Chaque build de A et B compile une valeur jamais écrite auparavant dans sa variante.

## Méthode

- restauration et premier build de chaque variante avant toute mesure ; la restauration ne dépend pas des analyseurs et n'est pas mesurée (`--no-restore`) ;
- serveur de compilation actif (comportement par défaut de `dotnet build`), partagé par les deux variantes ;
- par scénario, un build de chauffe par variante, puis cinq builds mesurés par variante, en alternance avec puis sans analyseurs, pour que la dérive du runner touche les deux séries ;
- durée totale de la commande `dotnet build`, horloge murale ; médiane des cinq mesures retenue, minimum et maximum publiés ;
- un dernier build complet avec `-p:ReportAnalyzer=true` donne le temps cumulé de chaque analyseur HardSlice par projet. Il explique un surcoût ; seuls les seuils sur la durée totale font foi.

## Seuils

Sur la médiane, pour la fixture, le SDK épinglé par `global.json` et le runner `ubuntu-latest` documentés :

| Scénario | Durée totale avec analyseurs | Surcoût des analyseurs HardSlice |
|---|---:|---:|
| A | ≤ 10 s, budget du build après édition de la spécification | ≤ 1 s |
| B | ≤ 20 s | ≤ 2 s |
| C | mesuré, sans seuil | ≤ 15 % |

Le surcoût est la médiane avec analyseurs moins la médiane sans analyseurs ; pour C, rapporté à la médiane sans analyseurs. Un seuil ne change que par une décision du mainteneur consignée dans ce rapport, jamais automatiquement.

## Dépassement d'un seuil

Un dépassement est d'abord qualifié, dans le rapport et par le code de sortie du script :

| Qualification | Condition | Code de sortie | Suite |
|---|---|---:|---|
| Variabilité de l'environnement | la dispersion d'une des deux séries du scénario (maximum moins minimum, rapporté à la médiane) dépasse 20 % | 3 | série instable : mesure relancée sur un nouveau runner, sans conclusion |
| Coût du build sans HardSlice | la variante sans analyseurs dépasse déjà le budget de durée totale | 0, avec un avertissement | le dépassement ne vient pas des analyseurs : arbitrage sur la fixture, le runner ou le budget |
| Surcoût des analyseurs | séries stables, et le surcoût dépasse son seuil, ou la variante sans analyseurs respecte le budget que la variante avec analyseurs dépasse | 4 | `ReportAnalyzer` désigne l'analyseur en cause |

Un surcoût n'est une régression confirmée qu'après deux exécutions stables consécutives au-dessus du seuil. Il bloque alors l'intégration du ticket qui l'introduit, et seulement celui-là. Les autres qualifications ne font pas échouer les pull requests : elles ouvrent un arbitrage.

## Intégration continue

Le workflow `.github/workflows/performance.yml` tourne sur `main` et sur les pull requests qui touchent les analyseurs, le template, les scripts de mesure ou le SDK épinglé :

- le job `performance` mesure sur un runner `ubuntu-latest` neuf. Il échoue seulement si la mesure n'aboutit pas (build en échec, fixture invalide) ; une série instable ou un surcoût passe la main au job suivant ;
- le job `performance-rerun` ne tourne qu'après une série instable ou un surcoût, sur un autre runner neuf. Il réussit si la seconde mesure respecte les seuils (ou ne relève que le coût du build sans HardSlice). Il échoue si deux surcoûts stables se suivent, régression confirmée, et aussi si la seconde mesure reste instable ou ne donne qu'un surcoût non confirmé : relancer alors le workflow, aucune conclusion n'est possible sur ces exécutions.

Chaque job publie la mesure dans son résumé GitHub Actions. Le rapport n'est pas commité par la CI : un ticket de règle enregistre sa mesure en lançant le script et en commitant ce fichier.

## Décisions du mainteneur

Aucune à ce jour : les seuils sont ceux de PB-019.

## Dernière mesure

<!-- measure-analyzers:latest:begin -->

### Mesure du 2026-10-30

- Commit : `460b963, avec des modifications non commitées`
- Règles HardSlice : HS003, HS015, HS016, HS900, HS901, HS902
- SDK effectif : 10.0.401
- Runner : machine locale, Linux 6.8.0-147-generic
- Processeur : AMD EPYC 9354P 32-Core Processor, 4 cœurs
- Mémoire : 15.6 Gio
- Fixture : PerfApp, Bookings et 20 modules synthétiques de 10 slices, 355 fichiers C# et 11045 lignes sous `src/`
- Verdict : seuils respectés

Durées en secondes, une chauffe puis 5 builds mesurés par variante, en alternance :

| Scénario | Analyseurs | Minimum | Médiane | Maximum | Dispersion |
|---|---|---:|---:|---:|---:|
| A | avec | 3.15 | 3.27 | 5.88 | 83.5 % |
| A | sans | 2.89 | 3.04 | 3.53 | 20.9 % |
| B | avec | 3.82 | 4.07 | 4.26 | 10.7 % |
| B | sans | 3.57 | 3.73 | 5.92 | 62.9 % |
| C | avec | 4.24 | 4.39 | 4.53 | 6.7 % |
| C | sans | 3.69 | 3.96 | 4.48 | 19.9 % |

Seuils, sur la médiane :

| Scénario | Mesure | Valeur | Seuil | Résultat |
|---|---|---:|---:|---|
| A | Durée totale avec analyseurs | 3.27 s | ≤ 10.00 s | respecté |
| A | Surcoût des analyseurs | 0.23 s | ≤ 1.00 s | respecté |
| B | Durée totale avec analyseurs | 4.07 s | ≤ 20.00 s | respecté |
| B | Surcoût des analyseurs | 0.34 s | ≤ 2.00 s | respecté |
| C | Surcoût des analyseurs | 10.6 % | ≤ 15 % | respecté |

`-p:ReportAnalyzer=true`, build complet avec analyseurs : temps cumulé de chaque analyseur HardSlice, puis de tous les analyseurs du projet.
Ces temps se chevauchent (exécution concurrente) : ils expliquent un surcoût, seuls les seuils sur la durée totale font foi.

| Projet | Analyseur HardSlice | Temps | Tous les analyseurs |
|---|---|---:|---:|
| PerfApp.Domain | SliceReferenceAnalyzer (HS016) | 0.002 s | 0.020 s |
| PerfApp.Domain | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.020 s |
| PerfApp.Domain | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.020 s |
| PerfApp.Domain | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.020 s |
| PerfApp | SliceReferenceAnalyzer (HS016) | 1.017 s | 1.290 s |
| PerfApp | ClassificationAnalyzer (HS900, HS901, HS902) | 0.012 s | 1.290 s |
| PerfApp | EndpointDatabaseAccessAnalyzer (HS003) | 0.008 s | 1.290 s |
| PerfApp | SliceEndpointAnalyzer (HS015) | 0.007 s | 1.290 s |
| PerfApp.ArchitectureTests | SliceReferenceAnalyzer (HS016) | 0.003 s | 0.099 s |
| PerfApp.ArchitectureTests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.099 s |
| PerfApp.ArchitectureTests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.099 s |
| PerfApp.ArchitectureTests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.099 s |
| PerfApp.IntegrationTests | SliceReferenceAnalyzer (HS016) | 0.052 s | 0.134 s |
| PerfApp.IntegrationTests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.134 s |
| PerfApp.IntegrationTests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.134 s |
| PerfApp.IntegrationTests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.134 s |
| PerfApp.Tests | SliceReferenceAnalyzer (HS016) | 0.053 s | 0.631 s |
| PerfApp.Tests | ClassificationAnalyzer (HS900, HS901, HS902) | <0.001 s | 0.631 s |
| PerfApp.Tests | EndpointDatabaseAccessAnalyzer (HS003) | <0.001 s | 0.631 s |
| PerfApp.Tests | SliceEndpointAnalyzer (HS015) | <0.001 s | 0.631 s |

<!-- measure-analyzers:latest:end -->

## Historique

Médianes en secondes, avec / sans analyseurs.

| Date | Commit | Règles HardSlice | Runner | A | B | C | Verdict |
|---|---|---|---|---:|---:|---:|---|
| 2026-10-30 | `7c2b785` | HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.76 / 3.72 | 4.62 / 4.36 | 4.76 / 4.84 | pass |
| 2026-10-30 | `1dcb109` | HS003, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.38 / 3.15 | 4.07 / 3.68 | 4.50 / 4.22 | pass |
| 2026-10-30 | `460b963` | HS003, HS015, HS016, HS900, HS901, HS902 | machine locale, Linux 6.8.0-147-generic | 3.27 / 3.04 | 4.07 / 3.73 | 4.39 / 3.96 | pass |
<!-- measure-analyzers:history -->

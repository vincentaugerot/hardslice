# S2a — Conventions minimales pour HS016

Statut : **inventaire provisoire (S2A-01), non figé**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02). Le format (S2A-02), la version (S2A-03), la validation (S2A-04) et le gel après D-S1 (S2A-05) relèvent du sprint 2. Les noms de clés sont des libellés de travail, indépendants du format qui sera choisi.
Sources : [plan.md](../plan.md) §6.5 (lignes 1A), §7.2, §7.3 ; backlog S2A-01.

## Principes

- **Classification fermée** : dans le périmètre applicatif, tout type est rattaché à une slice ou à une frontière explicitement autorisée ; « non classifiable » n'équivaut jamais à « autorisé » et produit un diagnostic de configuration.
- **Aucune ambiguïté** : un namespace donné reçoit au plus une classification ; une configuration qui permettrait deux lectures est rejetée par un diagnostic de configuration.
- **Comparaisons par segments** : un namespace est une suite de segments séparés par `.` ; toute comparaison se fait segment par segment, en ordinal et sensible à la casse. `Fx.Domain` ne couvre donc jamais `Fx.DomainEvents`.
- **Aucun marqueur contrôlé par un fichier applicatif ne suffit** à changer une classification ou à exclure du code (§7.3).

## Clés

| # | Clé (libellé de travail) | Besoin HS016 couvert | Source | Type et sémantique | Défaut |
|---|---|---|---|---|---|
| K1 | `schema_version` | Lire la configuration sans ambiguïté ; rejeter une version inconnue | §7.2 « version de schéma dès le départ » ; §6.5 « Configuration invalide » | entier | **aucun** : absente → diagnostic de configuration (à confirmer par S2A-03) |
| K2 | `application_assemblies` | Délimiter le périmètre applicatif où la classification fermée s'applique | §7.2 « projets du périmètre applicatif » ; §6.5 « Classification fermée » | liste d'**`AssemblyName`**, comparés en ordinal à `Compilation.AssemblyName` (compilation courante) et à `IAssemblySymbol.Name` (type cible venant d'un autre assembly) | **aucun** : liste vide → diagnostic de configuration |
| K3 | `slice_roots` | Identifier les slices à partir du namespace, y compris avec plusieurs racines et des profondeurs différentes (cas FSH) ; HS016 déduit la slice du namespace, pas du chemin (§6.3) | §7.2 « racine et motif de namespace des slices » ; §6.2 (HS016 : « classification fermée par namespace ») | liste de couples `{ prefix, depth }` : `prefix` = namespace ; `depth` = nombre de segments, après `prefix`, qui forment l'identité de la slice (≥ 1) | aucun |
| K4 | — | Fusionnée dans K3 : le motif `<Root>.Features.<Module>.<Slice>` s'exprime par `prefix = <Root>.Features.<Module>`, `depth = 1`, ou `prefix = <Root>.Features`, `depth = 2` | — | — | — |
| K5 | `allowed_boundaries` | Lister les namespaces référençables depuis n'importe quelle slice : domaine, transverse, contrat de module, racine de composition | §7.2 « frontières de namespace explicitement autorisées » | liste de couples `{ namespace, mode }` ; `mode = exact` : le namespace exactement ; `mode = subtree` : le namespace et tous ses descendants par segments ; `<global>` désigne le namespace global, en mode `exact` seulement | **vide** (rien n'est autorisé implicitement) |
| K6 | `excluded_assemblies` | Retirer du périmètre des assemblies listées dans K2 (outils, benchmarks, tests en 1A), par exemple quand K2 reprend toutes les assemblies d'une solution | §7.2 « projets exclus » | liste d'**`AssemblyName`**, **sous-ensemble de K2** ; périmètre effectif = **K2 moins K6** | vide |
| K7 | `generated_code_allowlist` | Distinguer le vrai code généré (sortie de générateur, migrations) d'un faux marquage | §7.2 « allowlist protégée » ; §6.5 « Code généré » ; §7.3 | liste d'entrées typées : `path` (chemin canonique, voir ci-dessous) ou `generator` (identité de générateur) ; un filtre `namespace` facultatif peut **restreindre** une entrée, jamais en constituer une seule | vide |

### Règle de classification d'un type

Le **périmètre effectif** est K2 moins K6. Pour un type déclaré dans une compilation dont l'`AssemblyName` est dans le périmètre effectif, ou pour un type cible dont l'assembly est dans le périmètre effectif :

1. Si son namespace est couvert par une entrée de K5 → **frontière autorisée**. K5 est évaluée avant K3 : une frontière peut découper un sous-arbre d'une racine de slices (exemple FSH : `…Features.v1.Internal`). `check` liste chaque recouvrement K5/K3 pour la revue.
2. Sinon, si une entrée de K3 a un `prefix` égal au namespace ou qui en est un préfixe par segments, et que le namespace compte au moins `depth` segments après ce `prefix` → **slice** dont l'identité est `prefix` + les `depth` segments suivants. Les segments plus profonds appartiennent à cette slice.
3. Sinon → **non classifiable** → diagnostic de configuration.

Contraintes de validité (diagnostic de configuration sinon) : deux entrées de K3 n'ont jamais le même `prefix`, et aucun `prefix` n'est préfixe par segments d'un autre, de sorte que l'étape 2 ne trouve jamais deux entrées ; deux entrées de K5 identiques sont rejetées.

Entrée de K6 absente de K2 : **erreur de configuration** (proposition). Une telle entrée n'a aucun effet et trahit presque toujours une faute de frappe ou une assembly renommée ; un simple avertissement laisserait le périmètre effectif différent de l'intention sans bloquer. **À décider explicitement** : la proposition est soumise à S2A-04, qui fixe le traitement des configurations invalides, et à la décision D-S2a. Le spike S1 n'en dépend pas : K6 y est vide (fixtures, nopCommerce, FSH).

Correspondance projet → `AssemblyName` : l'analyseur ne voit que les `AssemblyName`. `dotnet hardslice check` établit la correspondance depuis la propriété MSBuild `AssemblyName` évaluée de chaque `*.csproj` suivi par git, et signale toute entrée de K2 ou de K6 qui ne correspond à aucun projet. Cette répartition est à confirmer par S1-08.

### Racine de composition et namespace global

`Program`, généré par les instructions de niveau supérieur, vit dans le namespace global. C'est une **racine de composition**, pas du code généré : il est classé par une entrée K5 `{ namespace: <global>, mode: exact }`, **jamais par K7**. Il peut référencer toutes les slices ; HS016 ne s'applique qu'aux références émises depuis une slice. Une catégorie dédiée « racine de composition », distincte d'une frontière, pourra être introduite avec S3 (HS019) ; S2A-02 tranche si elle est nécessaire dès 1A.

### K7 : ce qui rend un code « généré »

Un fichier ou un arbre n'est traité comme généré que s'il correspond à une entrée de K7 :
- `path` : chemin relatif à la racine du dépôt, séparateurs `/`, sans `.` ni `..`, sensible à la casse, désignant un fichier ou un dossier (préfixe par segments de chemin). Il est comparé au chemin normalisé de l'arbre. Usage : migrations, dossiers de sortie de générateurs externes.
- `generator` : identité `<AssemblyName du générateur>/<nom complet du type générateur>`, comparée aux arbres produits par un générateur source dans la compilation (le chemin d'un arbre généré par Roslyn commence par cette identité). Usage : générateurs source du SDK ou du projet.
- `namespace` (filtre facultatif) : restreint une entrée `path` ou `generator` aux types de ce namespace. Il n'est jamais suffisant seul, parce qu'un fichier applicatif choisit librement son namespace.

Ne suffisent **jamais**, seuls ou combinés : `[GeneratedCode]`, `[CompilerGenerated]`, un suffixe `*.g.cs` ou `*.designer.cs`, un en-tête `// <auto-generated/>`, `generated_code = true` dans un `.editorconfig` ou un `.globalconfig`. Un fichier suivi par git qui porte l'un de ces marqueurs sans correspondre à K7 reste analysé (F25 du protocole S1).

Note : le mainteneur avait cité les namespaces parmi les formes possibles d'une entrée K7. Ils sont ici réduits à un filtre, pour respecter la règle « aucun marqueur contrôlé par un fichier applicatif ne suffit ». Ce point est à confirmer par le mainteneur.

### Type applicatif non classifiable mais jamais référencé

Proposition, testée par S1 avant toute décision (F26, F20 et X4 du protocole S1) : **diagnostic de l'analyseur**, sans analyse de fin de compilation.
- Un type **déclaré** dans une compilation du périmètre et non classifiable reçoit un diagnostic de configuration sur sa déclaration (action par symbole sur chaque type nommé), qu'il soit référencé ou non. Les références vers lui dans la même compilation ne produisent pas de diagnostic supplémentaire.
- Un type **de métadonnées** d'un assembly du périmètre, non classifiable, reçoit le diagnostic sur chaque site de référence, puisque sa déclaration n'est pas dans la compilation courante.
- `check` ne porte que les contrôles qui exigent une vue de la solution (correspondance projet → `AssemblyName`, cohérence entre projets). L'affectation définitive relève de S1-08.

## Clés candidates écartées

| Clé candidate | Raison de l'écart |
|---|---|
| Interfaces marquant un handler, wrappers de dépendance, racines de composition au sens de HS019 | HS019, livrées par S3 en phase 1B (R-15) |
| Identification des projets de tests | S3 (phase 1B) ; en 1A, un projet de tests n'est simplement pas listé dans K2, ou en est retiré par K6 |
| Chemin de la baseline, compteurs, raisons | S2b, avant la phase 4 |
| Nommage et emplacement des fichiers d'une slice | HS004, convention de catégorie 2 (E2) |
| Sévérité ou niveau d'analyse | Le niveau est une propriété MSBuild racine (`HardSliceAnalysisLevel`, §6.3) ; toute sévérité HS hors du `.globalconfig` du paquet est interdite (§7.3) |
| Correspondance dossier ↔ namespace | IDE0130 n'est pas imposée ; HS016 lit le namespace (§6.3) |
| Frontières propres à un module (référençables seulement depuis les slices de ce module) | Hors minimum du §7.2 ; dans K5, une frontière vaut pour toutes les slices. Limite à consigner si la calibration FSH la rend nécessaire |

## Questions ouvertes pour S1 et S2A-02

1. **Types au niveau du module** (namespace `<Root>.Features.<Module>` lui-même, par exemple `BookingsModule`) : faut-il une entrée K5 `exact` explicite (choix de la fixture S1, F19), ou une règle par défaut ? Sans entrée, ils sont non classifiables (étape 3).
2. **Slices réparties sur deux projets** (FSH : commande dans `Modules.<M>.Contracts`, handler dans `Modules.<M>`) : la configuration S1 traite `Contracts` comme frontière K5. Il reste à voir si cela suffit ou s'il faut rattacher les deux namespaces à la même slice.
3. **Types générés par des générateurs du SDK** (regex, interop, `System.Text.Json`) : couverts par des entrées `generator`, à inventorier sur FSH et nopCommerce.
4. **Racine de composition** : entrée K5 `<global>` ou catégorie distincte (voir ci-dessus).

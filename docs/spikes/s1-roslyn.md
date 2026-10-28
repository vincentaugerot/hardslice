# S1 — Spike Roslyn (HS016)

Statut : **protocole DRAFT**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02, A-03), **non validé** (V-S1-01 `OPEN` dans [phase-0-decisions.md](../execution/phase-0-decisions.md)). **Aucune mesure n'a été exécutée.**
Sources : [plan.md](../plan.md) §6.1, §6.2, §6.3, §6.4, §6.5, §7.2, §10, annexe A.3 ; backlog S1-01 à S1-09 ; [s2a-conventions.md](s2a-conventions.md).

## Protocole

### 1. Objet et périmètre

Déterminer, mesures à l'appui, si HS016 (« une slice n'utilise aucun type d'une autre slice ») est détectable par opération ou par symbole, à travers les assemblies, en IDE et en CLI, dans les budgets du §6.4, avec un `.globalconfig` par niveau livré par paquet ; fixer la frontière analyseur / `dotnet hardslice check` et une première calibration de l'exposition HS016.

- Le code du spike est **jetable** : il n'est pas l'implémentation de HS016 de la phase 1A et n'en préjuge pas la forme.
- Pas d'analyseur de fin de compilation (`RegisterCompilationEndAction`) dans le prototype (§6.1) ; s'il s'avérait nécessaire, c'est un résultat à consigner comme écart, pas une solution adoptée.
- Identifiants jetables du prototype : `HS016` pour la règle (nécessaire pour tester la précédence de sa sévérité) ; `SPKCFG` pour un type applicatif non classifiable ; `SPKOPP` (sévérité Info, mode statistique seulement) pour compter les opportunités. Aucun identifiant n'est alloué (D-09).
- La classification suit l'inventaire provisoire de [s2a-conventions.md](s2a-conventions.md) (K1–K7), lu par le prototype depuis un `.globalconfig` de spike ; ce format de lecture n'anticipe pas S2A-02.
- Hors périmètre : message exemplaire, fiches, `explain`, tests complets de la matrice §6.5, `check`, budget `check` (≤ 30 s / ≤ 90 s, **reporté en 1A** : la mini-application n'existe pas).

### 2. Préalables

| Entrée | Objet | État |
|---|---|---|
| D-05 | Capacité | `DECIDED` |
| D-06 | nopCommerce @ `221305873bc81c47981b2e510b2e9bed7e5b2076` ; FSH @ `a967537526f3b769c7d268da3ed2a6920af65ce9` obligatoire ; fdmm exploratoire | `DECIDED` |
| D-07 | Choix : VS Code stable + C# Dev Kit stable | `DECIDED` |
| PRE-S1-04 | Prérequis opérationnel IDE : installation et versions exactes attestées par le mainteneur | `OPEN` — S1-04 fermé |
| D-10, D-10-A1 | Toute sortie brute exclusivement sous `$HS_ART/<RUN_ID>/` ; fiches et extraits normalisés seulement dans le dépôt ; variables symboliques ; manifeste privé | `DECIDED` |
| D-11 | Notes en français | `DECIDED` |
| D-12 | Une extension de 0,5 j-p au plus ; seul le temps focalisé du mainteneur compte | `DECIDED` |
| M-08, M-08-A1 | Poste local ; builds OSS dans un compte macOS dédié et non privilégié | `DECIDED` |
| OP-01 | Attestation d'isolation avant le premier restore de nopCommerce ou de FSH | `OPEN` — S1-06 et S1-07 fermés |
| M-10 | Second relecteur indépendant pour les cas de triage ambigus ou contestés | `OPEN` |
| **V-S1-01** | Validation de ce protocole, y compris les oracles, les classes de commandes, la répartition du §9 et le wrapper | **`OPEN`** |
| AUT-01 | Commits locaux sans push ; commit de baseline **après** V-S1-01, avant le premier run mesuré | `DECIDED` ; commit non créé |

Dépendance entre questions : **Q6 (S1-07) dépend de Q2 (S1-03)**. Si X1 ou X2 n'atteint pas son oracle, Q6 est `non mesuré`.

D-01, D-13 et les validations UXR ne bloquent pas S1.

#### 2.1 Checklist opérationnelle avant le premier run

Chaque case est cochée dans la fiche de la première tentative de la série concernée, avec sa preuve.

| # | Point | Qui | Avant |
|---|---|---|---|
| 1 | V-S1-01 `DECIDED` | Mainteneur | Tout run |
| 2 | Échafaudage du spike (prototype, fixtures, états Q3, scripts de série) commité localement en commits atomiques, sans push ; commit de baseline créé ; le run cite un SHA dont le worktree est propre (`git status --porcelain` vide) | Agent, après V-S1-01 | Tout run |
| 3 | `tools/test-run-logged.sh` exécuté au commit du run : `passed=N failed=0`, code 0 | Celui qui lance | Tout run |
| 4 | `<RUN_ID>` neuf (§3.2) ; `$HS_ART/<RUN_ID>/` créé par `mkdir -m 0700` sans `-p` (échec si le dossier existe) ; manifeste privé initialisé | Script de série | Toute tentative |
| 5 | Environnement en liste blanche (§3.4) appliqué par le script de série ; inventaire de l'environnement archivé dans `$HS_RUN` | Script de série | Toute tentative |
| 6 | `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` = `10.0.102` (étape `must_succeed` dont la sortie est comparée) | Script de série | Toute tentative |
| 7 | Journal humain : heure de début déclarée par le mainteneur | Mainteneur | Tout ticket S1 |
| 8 | OP-01 attestée (compte dédié, `HOME` vide, aucun secret ni donnée UXR, environnement en liste blanche, cache NuGet dédié, NuGet public, réseau limité, accès limité, conditions M-08) | Mainteneur | Premier restore de nopCommerce ou de FSH |
| 9 | Bootstrap Q5/Q6 (§6.5.1) terminé, empreintes des binaires enregistrées, **avant** la coupure réseau | Script de série, lancé par le mainteneur | Q5, Q6 |
| 10 | Clones vérifiés : URL, SHA, `git status --porcelain` vide, avant et après la série | Script de série | Q5, Q6 |
| 11 | Configuration de classification (`config/nopcommerce.globalconfig`, `config/fsh.globalconfig`) commitée et hachée avant le run | Agent | Q5, Q6 |
| 12 | Q2 conforme pour X1 et X2 au même code d'analyseur | Agent | Q6 |
| 13 | PRE-S1-04 attestée (versions exactes de VS Code stable, C# Dev Kit, extension C#, runtime) | Mainteneur | S1-04 |
| 14 | M-10 identifié, ou risque accepté que tout cas ambigu laisse Q6 `non mesuré` | Mainteneur | Triage S1-07 |

### 3. Environnement épinglé

Constaté le 28/10/2026 (commandes : `dotnet --list-sdks`, `dotnet /usr/local/share/dotnet/sdk/<sdk>/Roslyn/bincore/csc.dll -version`, `sysctl`, `sw_vers`) :

| Élément | Valeur |
|---|---|
| Machine (M-08) | Apple M4 Max, 16 cœurs, 64 Go, arm64, sur secteur, applications lourdes fermées |
| OS | macOS 26.6.2 (25G83) |
| Hôte .NET | `/usr/local/share/dotnet/dotnet` |
| SDK du spike | **10.0.102** (`/usr/local/share/dotnet/sdk/10.0.102/dotnet.dll`) |
| Compilateur de ce SDK | Roslyn 5.0.0-2.25612.105 (44525024) |
| Référence de l'analyseur | `Microsoft.CodeAnalysis.CSharp` **5.0.0**, cible `netstandard2.0` (annexe A.3) ; `Microsoft.CodeAnalysis.Analyzers` aligné |
| Hôte de benchmark | `net10.0` ; `Microsoft.CodeAnalysis.CSharp.Workspaces` et `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.0.0 ; `Microsoft.Build.Locator` ; le runtime effectivement chargé (`RuntimeInformation.FrameworkDescription`) est écrit dans sa sortie |
| SDK installé non utilisé | 10.0.201 (Roslyn 5.3.0-2.26153.122) |
| Outils | git 2.51.0, bash, `shasum`, Python 3 (extraction et normalisation) |
| IDE (D-07) | VS Code stable + C# Dev Kit stable, à installer par le mainteneur (PRE-S1-04) |

#### 3.1 Règle d'invocation du SDK

Toute commande `restore`, `build`, `pack`, `msbuild` ou `nuget` du spike, sur une fixture comme sur une solution externe, s'écrit avec l'hôte et le SDK absolus :

```
/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll <verbe> …
```

L'hôte de benchmark construit est lancé par `/usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" …`, sans `dotnet run`. Aucune commande `dotnet <verbe>` nue, aucun hôte trouvé par le `PATH`. Chaque série vérifie, par une étape `must_succeed` à sortie comparée, `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` et, pour une solution externe, `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <un .csproj de la solution> -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath`. Une valeur qui ne désigne pas 10.0.102 arrête la série.

Vérification préalable du 28/10/2026, hors mesure : sur le clone nopCommerce, dont le `global.json` épingle 10.0.401, l'invocation du SDK 10.0.102 par son `dotnet.dll` évalue `NETCoreSdkVersion = 10.0.102` et `RoslynTargetsPath` sous `sdk/10.0.102`, sans modification du clone. Le spike pose aussi `spikes/s1-roslyn/global.json` (`"version": "10.0.102"`, `"rollForward": "disable"`), ceinture en plus de la règle d'invocation.

#### 3.2 Identifiant de tentative et variables symboliques (D-10-A1)

**`<RUN_ID>`** identifie une tentative d'une série, de façon immuable : `<AAAAMMJJ>T<HHMMSS>Z-<Qn>-<SHA court de 12 caractères du commit HardSlice>-a<NN>`, par exemple `20261030T091500Z-Q1-0123456789ab-a01`. `a<NN>` numérote les tentatives d'une même question au même commit. Un `<RUN_ID>` n'est jamais réutilisé : le script de série crée `$HS_ART/<RUN_ID>/` par `mkdir -m 0700` sans `-p`, ce qui échoue si le dossier existe, et le dossier `results/<RUN_ID>/` du dépôt est créé de même. Une tentative interrompue garde son `<RUN_ID>`, et la suivante en reçoit un nouveau.

Les commandes versionnées n'emploient que ces variables ; leur expansion exacte est écrite dans le manifeste privé `$HS_RUN/manifest.private.md`, hors git.

| Variable | Contenu |
|---|---|
| `$HS_REPO` | Copie de travail du dépôt HardSlice au commit mesuré (dans le compte dédié pour Q5 et Q6) |
| `$HS_ART` | Racine des artefacts du compte qui exécute la série, hors git et hors synchronisation cloud |
| `$HS_RUN` | `$HS_ART/<RUN_ID>`, dossier de la tentative : **seul emplacement des sorties brutes** |
| `$HS_CLONES` | `$HS_RUN/clones`, clones OSS de la tentative |

Permissions : dossiers `0700`, fichiers `0600` (le wrapper impose `umask 077`, le script de série aussi). Les sorties sont ensuite rangées par le mainteneur sous l'emplacement D-10 (`~/Documents/hardslice-artifacts/s1-roslyn/<RUN_ID>/`), avec vérification des SHA-256 du manifeste.

#### 3.3 Wrapper de commande

Toute commande passe par le wrapper versionné [`spikes/s1-roslyn/tools/run-logged.sh`](../../spikes/s1-roslyn/tools/run-logged.sh) :

```
"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/<étape>.log" -- <commande> [arguments…]
```

Comportement :
- **Création** : `umask 077`. Le log et son sidecar `<log>.exit` sont créés exclusivement (`noclobber`, donc `O_EXCL`), en mode `0600`.
- **Préflight** : refus avant lancement si le parent est absent ou est un lien symbolique, si le log ou le `.exit` existe déjà, ou si l'un d'eux est un lien symbolique (même cassé). Aucun écrasement possible, aucun `.exit` périmé réutilisable.
- **En-tête** : horodatage UTC et commande exacte, relus (`cmp`) avant le lancement. Un échec arrête tout sans lancer la commande.
- **Exécution** : sortie standard et d'erreur complètes dans le log.
- **Clôture** : pied (horodatage, `# exit: <code>`) écrit puis vérifié après fermeture ; `.exit` créé exclusivement puis relu.
- **Codes de sortie** :
  - le **code réel** de la commande ;
  - 64 sur erreur d'usage ;
  - 70 sur erreur de préflight, et la commande n'est pas lancée ;
  - après lancement, un échec d'enregistrement conserve le code de la commande, ou donne 70 si ce code était 0.
- **Rôle** : le wrapper ne juge jamais le code ; le script de série le compare à l'oracle (§3.5).

Test automatisé versionné : [`spikes/s1-roslyn/tools/test-run-logged.sh`](../../spikes/s1-roslyn/tools/test-run-logged.sh). Il travaille dans un dossier temporaire privé supprimé à la fin et lance le wrapper sous `umask 022`, pour prouver que le wrapper impose lui-même `0600`. Cas couverts :
- succès, échec, stdout et stderr, sortie sans fin de ligne, code 127 ;
- trois formes de mauvais usage ;
- parent absent ;
- log existant, `.exit` existant ;
- lien symbolique cassé ou valide pour le log, lien symbolique pour le `.exit`, parent lien symbolique ;
- permissions `0600` du log et du `.exit` ;
- pour chaque erreur de préflight, preuve que la commande (création d'un fichier marqueur) n'a pas été exécutée.

Résultat du 28/10/2026, hors mesure : `passed=34 failed=0`.

#### 3.4 Isolation et environnement d'exécution (M-08-A1, OP-01)

Tout restore, build ou benchmark de **nopCommerce et de FSH** s'exécute sur le M4 Max, sous un compte macOS local **dédié et non privilégié**, préparé par le mainteneur et attesté par **OP-01** avant le premier restore :
- `HOME` dédié et vide ;
- aucun secret, trousseau, agent SSH, credential cloud ou donnée UXR accessible ;
- configuration NuGet limitée aux sources publiques requises ;
- cache NuGet dédié à la tentative ;
- accès uniquement à une copie du commit HardSlice mesuré (`$HS_REPO`) et au dossier d'artefacts (`$HS_ART`) ;
- réseau limité pendant le restore et le clonage : LAN bloqué, sortie limitée aux flux nécessaires (`api.nuget.org` et ses CDN, `github.com` pour les clones), puis coupure complète après le bootstrap. Si ce filtrage n'est pas réalisable, le **risque résiduel** est décrit et accepté explicitement par le mainteneur dans OP-01. Un échec dû à l'absence de réseau est publié ; toute réactivation est consignée avec sa raison.

L'agent ne crée aucun compte ni configuration système. Comme l'agent s'exécute sous le compte du mainteneur, **le mainteneur lance les scripts de série de Q5 et Q6** dans le compte dédié. L'agent écrit et commite ces scripts, puis analyse les sorties rangées. Les fixtures du spike (Q1 à Q4) ne contiennent pas de code tiers et s'exécutent dans le compte du mainteneur, avec le même environnement en liste blanche.

**Environnement en liste blanche**, appliqué par chaque script de série (`env -i`). Toute autre variable est absente et l'inventaire effectif est archivé dans `$HS_RUN/environment.txt`.

| Variable | Valeur |
|---|---|
| `HOME` | `HOME` du compte qui exécute |
| `PATH` | `/usr/bin:/bin:/usr/sbin:/sbin` |
| `TMPDIR` | `$HS_RUN/tmp` |
| `LANG`, `LC_ALL` | `en_US.UTF-8` |
| `DOTNET_CLI_UI_LANGUAGE` | `en-US` (sorties MSBuild en anglais, stables pour l'extraction) |
| `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_NOLOGO` | `1` |
| `DOTNET_CLI_HOME` | `$HS_RUN/dotnet-home` |
| `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH`, `NUGET_PLUGINS_CACHE_PATH` | sous `$HS_RUN/nuget/` (cache dédié à la tentative) |
| `MSBuildSDKsPath` | `/usr/local/share/dotnet/sdk/10.0.102/Sdks` |
| `GIT_CONFIG_NOSYSTEM`, `GIT_CONFIG_GLOBAL`, `GIT_TERMINAL_PROMPT` | `1`, `/dev/null`, `0` |
| `HS_REPO`, `HS_ART`, `HS_RUN`, `HS_CLONES` | §3.2 |

#### 3.5 Classes de commandes et oracles de code de sortie

Chaque étape d'un script de série porte une classe pré-enregistrée. Le wrapper conserve toujours le code réel ; **le script de série** compare ce code et, si la classe l'exige, les diagnostics observés à l'oracle :

| Classe | Oracle | Série si l'oracle n'est pas atteint |
|---|---|---|
| `must_succeed` | Code 0 ; sortie comparée quand c'est précisé | Arrêt ; tentative invalide |
| `must_fail(<code>, <diagnostics>)` | Code exact et ensemble de diagnostics attendus (ID, nombre, emplacement si précisé) ; 0 AD0001 | Arrêt ; tentative invalide |
| `informative` | Aucun sur le code ; code et sortie publiés | Continue |

Un échec attendu conforme à son oracle (`must_fail` atteint) **ne stoppe pas** la série. Les diagnostics sont lus dans le SARIF (`-p:ErrorLog`) ou dans la sortie de l'hôte de benchmark, jamais dans le texte localisé. Une tentative invalide reste publiée.

### 4. Organisation du spike (D-10, D-10-A1)

```
spikes/.gitignore                   garde-fous : *.log, *.exit, *.binlog, *.sarif, *.csv, raw/
spikes/s1-roslyn/
  global.json                       SDK 10.0.102
  README.md                         reproduction pas à pas
  src/HardSlice.Spike.Analyzer/     prototype HS016 (netstandard2.0)
  src/HardSlice.Spike.Package/      paquet local + level-N.globalconfig (Q4)
  bench/HardSlice.Spike.Bench/      hôte CompilationWithAnalyzers (Q1, Q5, Q6)
  fixtures/forms/                   Q1 (compilation saine)
  fixtures/forms-broken/            Q1, forme F23 (compilation cassée)
  fixtures/cross-assembly/          Q2
  fixtures/ide/states/I1 … I7/      Q3, états commités
  fixtures/precedence/case-a … h/   Q4
  inject/                           targets d'injection dans une solution externe sans la modifier (Q5, Q6)
  config/                           .globalconfig de classification (fixtures, nopCommerce, FSH)
  tools/                            run-logged.sh, test-run-logged.sh, scripts de série, extraction, normalisation
  results/<RUN_ID>/                 fiches et extraits normalisés seulement
```

**Toute sortie brute reste exclusivement sous `$HS_RUN`**, quelle que soit sa taille : binlogs, logs complets, `.exit`, SARIF, CSV, sorties MSBuild, captures brutes, clones OSS. `results/<RUN_ID>/` ne reçoit que des fiches et des extraits **normalisés** : nom d'utilisateur, `$HOME` et chemins locaux remplacés par `<USER>`, `<HOME>`, `<HS_ART>`, `<HS_RUN>`, `<HS_CLONES>`, `<HS_REPO>`, puis contrôle de secrets ; un extrait qui échoue au contrôle n'est pas versionné. `spikes/.gitignore` ignore les extensions brutes et les dossiers `raw/` comme garde-fou.

### 5. Fiche de run (obligatoire pour chaque tentative)

Chaque tentative produit une fiche normalisée `results/<RUN_ID>/README.md` avec :
- `<RUN_ID>`, question (Q1–Q7), date et heure ;
- machine, compte (« mainteneur » ou « dédié »), conditions M-08 ;
- versions : sortie `--version` de la règle d'invocation, compilateur, runtime de l'hôte de benchmark, paquets Roslyn, IDE le cas échéant ;
- SHA du commit HardSlice, `git status --porcelain`, résultat de `test-run-logged.sh` ;
- pour chaque dépôt externe : **URL, SHA de `HEAD` et `git status --porcelain` vide**, avant et après. Un clone est identifié ainsi, pas par le SHA-256 de ses fichiers ;
- pour chaque étape : commande en variables symboliques, classe, code de sortie (lu dans `.exit`), oracle atteint ou non ;
- configuration de classification et son SHA-256 ; empreintes SHA-256 des binaires construits (Q5, Q6) ;
- références des sorties brutes : nom, emplacement symbolique et SHA-256, le chemin réel restant dans le manifeste privé ;
- résultats bruts résumés ; **interprétation séparée** ; limites et anomalies.

Règles : une valeur n'est « mesurée » que si la commande a été exécutée ; une tentative ratée, invalide ou rejouée est conservée et publiée avec les autres ; une configuration ou un prototype modifié après une tentative donne une nouvelle tentative avec un nouveau `<RUN_ID>`, l'ancienne reste publiée.

### 6. Questions

| Q | Ticket | Question | Mesure | Critère | Artefact versionné |
|---|---|---|---|---|---|
| Q1 | S1-02 | HS016 est-elle détectable par opération ou par symbole dans une compilation, sur les formes 1A ? | Opportunités et diagnostics observés par forme, comparés aux oracles pré-enregistrés (§6.1) | Oracles atteints ; 0 faux positif ; 0 doublon ; 0 AD0001 ; formes manquées nommées avec l'API en cause | `results/<RUN_ID>/forms.md` |
| Q2 | S1-03 | La détection fonctionne-t-elle quand le type cible vient d'un autre assembly ? | Diagnostics par configuration X1–X5 | Attendu atteint ; informations perdues en métadonnées nommées | `results/<RUN_ID>/cross-assembly.md` |
| Q3 | S1-04 | Comportement dans l'IDE retenu et en CLI, depuis un paquet local ? | Observation humaine des états I1–I7 et CLI côte à côte | Divergences listées ; captures datées | `results/<RUN_ID>/ide-cli.md` |
| Q4 | S1-05 | `.globalconfig` par niveau livré par paquet et précédence effective ? | Cas (a)–(h), attendu du plan contre observé | Tout écart au §6.3 signalé comme écart | `results/<RUN_ID>/globalconfig-precedence.md` |
| Q5 | S1-06 | Coût sur nopCommerce ? | (1) part de HS016 dans le temps de compilation ; (2) temps d'exécution de HS016 par document, amorti | (1) ≤ 5 % ; (2) médiane ≤ 20 ms | `results/<RUN_ID>/perf.md` |
| Q6 | S1-07 | Première calibration des opportunités HS016 sur FSH ? | Opportunités et diagnostics ; triage complet HS016 et SPKCFG par le mainteneur | Q2 conforme ; nombres bruts publiés ; triage complet ; 0 SPKCFG non résolu ; seuil proposé avec les nombres | `results/<RUN_ID>/exposure.md`, tables de triage |
| Q7 | S1-08 | Frontière analyseur / `check` ? | Table contrôle → analyseur / `check` / non retenu | Chaque affectation renvoie à un artefact Q1–Q5 | section « Frontière » |

#### 6.1 Q1 — Formes (fixtures `fixtures/forms` et `fixtures/forms-broken`)

Classification de `fixtures/forms` (K1–K7 de [s2a-conventions.md](s2a-conventions.md)) :
- K2 = `Fx` ; K6 vide ;
- K3 = `{ Fx.Features.Bookings, 1 }`, `{ Fx.Features.Rooms, 1 }`, d'où les slices `Fx.Features.Bookings.BookRoom`, `Fx.Features.Bookings.CancelBooking` et `Fx.Features.Rooms.GetRoom` ;
- K5 = `{ Fx.Domain, subtree }`, `{ Fx.Shared, subtree }`, `{ Fx.Features.Bookings, exact }` (type de module) et `{ <global>, exact }` (racine de composition `Program`) ;
- `Fx.Misc` n'est couvert par aucune entrée ;
- K7 = `{ generator: Fx.Generators/Fx.Generators.SliceGenerator }`.

`fixtures/forms-broken` contient seulement F23 ; sa classification est identique, avec K2 = `Fx.Broken`. F23 est isolée pour que l'échec attendu de sa compilation ne masque pas les autres formes, puisque le compilateur en ligne de commande peut ne pas exécuter les analyseurs sur une compilation en erreur.

Étapes de la série Q1 (compte du mainteneur) :

| Étape | Commande | Classe |
|---|---|---|
| Q1-1 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/forms-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/forms/Forms.slnx" -nologo --no-incremental "-p:ErrorLog=$HS_RUN/forms.sarif"` | `must_succeed` ; oracle diagnostics (SARIF) : colonne HS016 et `SPKCFG` du tableau, sans F23 |
| Q1-2 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/forms-broken-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/forms-broken/FormsBroken.slnx" -nologo --no-incremental "-p:ErrorLog=$HS_RUN/forms-broken.sarif"` | `must_fail(1, { CS0246 × 1 sur Missing } ; 0 AD0001)` ; HS016 en CLI : **observé et publié, sans oracle** (entrée de Q7) |
| Q1-3 | Construction de l'hôte : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/bench-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release -o "$HS_RUN/bin/bench"` | `must_succeed` |
| Q1-4 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/forms-exposure.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_REPO/spikes/s1-roslyn/fixtures/forms/Forms.slnx" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_REPO/spikes/s1-roslyn/config/forms.globalconfig" --out "$HS_RUN/forms-exposure.csv"` | `must_succeed` ; oracle : colonnes `SPKOPP`, HS016 et `SPKCFG` du tableau, sans F23 |
| Q1-5 | Même commande sur `fixtures/forms-broken/FormsBroken.slnx` avec `config/forms-broken.globalconfig`, sortie `forms-broken-exposure.csv`. L'hôte accepte une compilation en erreur pour ce cas | `must_succeed` ; oracle F23 : 1 `SPKOPP`, 1 HS016, 0 exception d'analyseur |

**Règle des opportunités (sémantique, par site de référence).** Une opportunité `SPKOPP` est un site de référence, **situé dans une slice**, vers un type nommé du périmètre effectif (K2 moins K6). Un site est :
- (a) un nœud de syntaxe de type qui se lie à ce type : identifiant, nom générique ou nom d'alias ; un nom qualifié compte une seule fois, à son niveau le plus externe. Les commentaires de documentation sont exclus ;
- (b) ou une référence implicite par opération à un membre de ce type, quand aucun nœud (a) ne nomme ce type dans la même expression (exemple : appel d'une méthode d'extension sous forme réduite).

Appartenance : un site appartient à la slice du namespace englobant le plus proche. Une directive `using` placée hors de toute déclaration de namespace, ou une directive `global using`, n'appartient à aucune slice : sa cible n'est pas une opportunité, mais chaque usage depuis une slice l'est. Un import de namespace n'est jamais une opportunité. Une référence depuis un code hors slice (frontière, racine de composition, `Fx.Misc`) n'est pas une opportunité. Le code généré couvert par K7 n'est pas analysé.

**Règle des diagnostics (par site canonique, après dédoublonnage explicite des alias).** HS016 est émis une fois par **site canonique** d'opportunité vers une autre slice. Le site canonique d'une opportunité est le site lui-même, sauf pour un alias déclaré dans une slice (directive placée dans le namespace de la slice) : la directive et tous les usages de cet alias ont pour site canonique la **cible de la directive**, qui reçoit un seul diagnostic, même si l'alias est utilisé zéro, une ou plusieurs fois. `SPKCFG` se place sur l'identifiant de la déclaration du type non classifiable s'il est déclaré dans la compilation, sinon sur le site de référence. Les oracles ci-dessous sont **pré-enregistrés** : ils font partie de V-S1-01 et ne changent plus après la première tentative.

**Discipline des fixtures.** Les fichiers support (types cibles de `BookRoom`, `GetRoom`, `Fx.Domain`, `Fx.Shared`, type de module, `Fx.Misc`, support de `CancelBooking`) ne contiennent **aucune** référence à un type du périmètre. Chaque fichier de forme ne contient que les références décrites dans sa ligne. Le respect de cette discipline est vérifié à la relecture des fixtures, puis par les totaux.

| # | Forme (fichier de la slice `CancelBooking`, sauf mention) | Opportunités `SPKOPP` attendues | HS016 attendus (site canonique) | `SPKCFG` attendus |
|---|---|---:|---|---:|
| F01 | `BookRoomDto d = default;` | 1 (autre slice) | 1, sur `BookRoomDto` | 0 |
| F02 | `var d = new BookRoomDto();` | 1 (autre slice) | 1, sur `BookRoomDto` | 0 |
| F03 | `BookRoomHandler.Validate();` (membre statique) | 1 (autre slice) | 1, sur `BookRoomHandler` | 0 |
| F04 | Champ `BookRoomDto _f;`, propriété `BookRoomDto P { get; }`, méthode `BookRoomDto M(BookRoomDto p)` | 4 (autre slice) | 4, un par nœud de type | 0 |
| F05 | `class F05 : BookRoomBase, IBookRoomPort { }` | 2 (autre slice) | 2 | 0 |
| F06a | Alias **utilisé**, directive dans le namespace de la slice (extrait ci-dessous) | **3** : la directive et chacun des 2 usages (autre slice) | **1**, sur la cible de la directive ; **0** sur les 2 usages | 0 |
| F06b | Alias **inutilisé**, directive dans le namespace de la slice : `using UnusedAlias = Fx.Features.Bookings.BookRoom.BookRoomHandler;` | 1 : la directive (autre slice) | 1, sur la cible de la directive | 0 |
| F07a | `global using Fx.Features.Bookings.BookRoom;` dans `GlobalUsings.cs` (sans namespace), puis `BookRoomHandler h = default;` dans `CancelBooking` | 1 : l'usage ; 0 pour l'import de namespace | 1, sur l'usage | 0 |
| F07b | `global using GlobalAlias = Fx.Features.Bookings.BookRoom.BookRoomHandler;` dans `GlobalUsings.cs`, puis `GlobalAlias g = default;` dans `CancelBooking` | 1 : l'usage ; 0 pour la directive, qui n'appartient à aucune slice | 1, sur l'usage `GlobalAlias` | 0 |
| F08 | `List<BookRoomDto> l = new();` | 1 (autre slice) | 1, sur `BookRoomDto` | 0 |
| F09 | `var t = typeof(BookRoomHandler);` | 1 (autre slice) | 1 | 0 |
| F10 | `var n = nameof(BookRoomHandler);` | 1 (autre slice) | 1 | 0 |
| F11 | `[BookRoomMarker] class F11 { }` (attribut défini dans `BookRoom`) | 1 (autre slice) | 1, sur `BookRoomMarker` | 0 |
| F12 | `partial class F12` sur deux fichiers : partie A, champ `BookRoomDto _d;` ; partie B, paramètre `CancelBookingCommand c` | 2 : 1 autre slice, 1 même slice | 1, sur `BookRoomDto` ; aucun doublon par partie | 0 |
| F13 | `o is BookRoomDto`, `o as BookRoomDto`, `(BookRoomDto)o` | 3 (autre slice) | 3 | 0 |
| F14 | `"x".ToBookRoomCode()` (extension `string` définie dans `BookRoom`) | 1 (autre slice, site (b)) | 1, sur `ToBookRoomCode` | 0 |
| F15 | Accès à une constante, extrait exact ci-dessous | 1 : `BookRoomConstants` ; 0 pour l'import | 1, sur le token `BookRoomConstants`, ligne 7 de `F15_Constant.cs` | 0 |
| F16 | `BookRoomHandler.Result r = default;` (type imbriqué) | 1 (nom qualifié compté une fois) | 1, sur `BookRoomHandler.Result` | 0 |
| F17 | `Fx.Domain.Booking b = default; Fx.Shared.Clock c = default;` | 2 (frontière) | 0 | 0 |
| F18 | `CancelBookingCommand x = default; Fx.Features.Bookings.CancelBooking.Internal.Policy p = default;` | 2 (même slice) | 0 | 0 |
| F19 | `BookingsModule m = default;` | 1 (frontière) | 0 | 0 |
| F20 | `Fx.Misc.Helper h = default;` ; `Helper` déclaré dans `Fx` | 1 (non classifiable) | 0 | 1, sur la déclaration de `Helper` |
| F21 | Dans la slice `GetRoom` : `BookRoomDto d = default;` | 1 (autre slice) | 1 | 0 |
| F22 | `/// <see cref="BookRoomHandler"/>` sur un membre sans autre référence | 0 | 0 | 0 |
| F24 | Sortie du générateur K7 qui référence `BookRoomDto` | 0 (non analysé) | 0 | 0 |
| F25 | Quatre fichiers hors K7, chacun avec `BookRoomDto d = default;` et un faux marquage : `[GeneratedCode]` ; nom `*.g.cs` ; en-tête `// <auto-generated/>` ; `generated_code = true` dans `.editorconfig` | 4 (autre slice) | 4, un par fichier | 0 |
| F26 | `Fx.Misc.Orphan`, déclaré dans `Fx`, jamais référencé | 0 | 0 | 1, sur la déclaration de `Orphan` |
| F27 | `Program` (instructions de niveau supérieur, namespace global) qui référence les trois slices | 0 (hors slice) | 0 | 0 |
| **Total `fixtures/forms`** | | **38** : 31 autre slice, 3 même slice, 3 frontière, 1 non classifiable | **29** | **2** |
| F23 (`fixtures/forms-broken`) | Compilation cassée : `BookRoomDto d = default; Missing m = default; m.Foo();` dans la slice `CancelBooking`, `BookRoomDto` déclaré dans la slice `BookRoom` du même projet | 1 (autre slice) ; 0 sur les symboles en erreur | Hôte : 1, sur `BookRoomDto`. CLI : `must_fail(1, CS0246 × 1)`, 0 AD0001 ; présence de HS016 observée sans oracle | 0 |

Extrait exact de F06a (`fixtures/forms/src/Fx/Features/Bookings/CancelBooking/F06a_UsedAlias.cs`) :

```csharp
1  namespace Fx.Features.Bookings.CancelBooking;
2
3  using BookRoomAlias = Fx.Features.Bookings.BookRoom.BookRoomHandler;
4
5  internal static class F06aUsedAlias
6  {
7      public static void Use()
8      {
9          BookRoomAlias first = default;
10         BookRoomAlias second = default;
11     }
12 }
```

Oracle : 3 opportunités (ligne 3 : cible ; lignes 9 et 10 : usages) ; 1 HS016 sur `Fx.Features.Bookings.BookRoom.BookRoomHandler`, ligne 3 ; 0 HS016 lignes 9 et 10. F06b suit la même forme, sans usage.

Extrait exact de F15 (`fixtures/forms/src/Fx/Features/Bookings/CancelBooking/F15_Constant.cs`) :

```csharp
1  using Fx.Features.Bookings.BookRoom;
2
3  namespace Fx.Features.Bookings.CancelBooking;
4
5  internal static class F15Constant
6  {
7      public static int Max() => BookRoomConstants.MaxRooms;
8  }
```

avec, dans `fixtures/forms/src/Fx/Features/Bookings/BookRoom/BookRoomConstants.cs` : `namespace Fx.Features.Bookings.BookRoom; public static class BookRoomConstants { public const int MaxRooms = 10; }`.

Faux positif : diagnostic là où l'oracle en attend 0, ou doublon. Forme manquée : diagnostic attendu absent. Écart d'opportunités : nombre ou catégorie différents de l'oracle, consigné comme anomalie. Une correction du prototype donne une nouvelle tentative ; toutes sont publiées.

#### 6.2 Q2 — Inter-assemblies (fixture `fixtures/cross-assembly`)

Le périmètre applicatif (K2) est défini **par `AssemblyName`**, comparé en ordinal à `IAssemblySymbol.Name` du type cible et à `Compilation.AssemblyName` du consommateur.

| # | Configuration | K2 | Attendu |
|---|---|---|---|
| X1 | Consommateur `AssemblyName = Fx.Bookings.CancelBooking` (slice `Fx.Features.Bookings.CancelBooking`) → `ProjectReference` vers `AssemblyName = Fx.Bookings.BookRoom` (slice `Fx.Features.Bookings.BookRoom`) | `Fx.Bookings.CancelBooking`, `Fx.Bookings.BookRoom` | 1 × HS016 dans le consommateur |
| X2 | Même consommateur → `PackageReference` vers un paquet local dont l'assembly garde `AssemblyName = Fx.Bookings.BookRoom` | idem | 1 × HS016 |
| X3 | Consommateur → assembly leurre `AssemblyName = Decoy.Lib`, namespace `Fx.Features.Bookings.Decoy` | idem (sans `Decoy.Lib`) | 0 : hors périmètre malgré le namespace |
| X4 | Consommateur → assembly `AssemblyName = Fx.Bookings.Misc` (dans K2), type `Fx.Misc.Helper` (namespace non classifiable) | idem + `Fx.Bookings.Misc` | 1 × `SPKCFG` au site de référence dans le consommateur (la déclaration n'est pas dans sa compilation), jamais silencieux |
| X5 | Types partiels, attributs et marquage généré vus depuis les métadonnées | — | Informations perdues nommées (chemin de fichier, marquage par nom de fichier, parties) |

| Étape | Commande | Classe |
|---|---|---|
| Q2-1 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/cross-assembly-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental "-p:ErrorLog=$HS_RUN/cross-assembly.sarif"` | `must_succeed` ; oracle diagnostics X1, X3, X4 |
| Q2-2 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/bookroom-pack.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_REPO/spikes/s1-roslyn/fixtures/cross-assembly/src/Fx.Bookings.BookRoom" -c Release -o "$HS_RUN/feed"` | `must_succeed` ; contrôle : `lib/*/Fx.Bookings.BookRoom.dll` présent dans le `.nupkg` |
| Q2-3 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/cross-assembly-package-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental -p:UsePackage=true "-p:LocalFeed=$HS_RUN/feed" "-p:ErrorLog=$HS_RUN/cross-assembly-package.sarif"` | `must_succeed` ; oracle diagnostics X2 |

**Si X1 ou X2 n'atteint pas son oracle, Q6 est `non mesuré`** (§2).

#### 6.3 Q3 — IDE et CLI (états commités `fixtures/ide/states/I1 … I7`)

L'agent **ne manipule pas l'IDE**, n'installe rien et ne déduit jamais le comportement IDE d'un succès CLI. S1-04 ne s'ouvre qu'après **PRE-S1-04** : installation par le mainteneur de VS Code stable et de C# Dev Kit stable, et relevé des versions exactes (VS Code, C# Dev Kit, extension C#, runtime .NET de l'extension) **avant le test**. Sans retour du mainteneur, S1-04 est `non mesuré`.

**États commités.** Chaque état est un dossier complet et commité, dont l'empreinte (SHA-256 de l'arborescence) figure dans la fiche. Le mainteneur ouvre dans VS Code un dossier de travail `$HS_RUN/ide-work/`, hors dépôt. Les transitions sont appliquées, IDE ouvert, par le script versionné `tools/q3-apply-state.sh <état>`, qui copie l'état dans `$HS_RUN/ide-work/`, journalise l'heure UTC de la copie et vérifie l'empreinte. Le mainteneur chronomètre l'apparition ou la disparition du diagnostic à partir de cette heure. Les paquets locaux (version 1 et version 2 de l'analyseur, pour I7) sont produits dans `$HS_RUN/feed` par la série.

| État | Contenu | Observé dans l'IDE | CLI (build sans puis avec `-warnaserror`) et classe |
|---|---|---|---|
| I1 | Fixture sans violation, paquet v1 | Analyseur chargé depuis le paquet (nœud Analyzers ou journal du serveur de langage) ; aucun HS016 | sans : `must_succeed`, 0 HS016 ; avec : `must_succeed` |
| I2 | I1 + violation F01 | Présence, ID, emplacement, sévérité, délai d'apparition | sans : `must_succeed`, 1 HS016 warning ; avec : `must_fail(1, HS016 × 1 en erreur)` |
| I3 | Retour à I1 | Disparition, délai | comme I1 |
| I4 | I2 + `<HardSliceAnalysisLevel>1</HardSliceAnalysisLevel>` à la racine | Sévérité après rechargement ; rechargement nécessaire ou non | sans : `must_fail(1, HS016 × 1 en erreur)` ; avec : idem |
| I5 | I4 + `.editorconfig` utilisateur `dotnet_diagnostic.HS016.severity = suggestion` | Prise en compte sans redémarrage ? | sans : `must_succeed`, HS016 en `note` dans le SARIF ; avec : `must_succeed` |
| I6 | I4 + `.globalconfig` utilisateur, même réglage | idem | comme I5 |
| I7 | I2 avec le paquet v2 | Nouvelle version prise en compte (cache d'analyseurs) ? | sans : `must_succeed`, 1 HS016 ; la version de l'assembly v2 apparaît dans `ReportAnalyzer` |

Commande CLI par état : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/cli-<état>-<sans|warnaserror>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/ide/states/<état>" -nologo --no-incremental -p:ReportAnalyzer=true "-p:LocalFeed=$HS_RUN/feed" "-p:ErrorLog=$HS_RUN/cli-<état>-<sans|warnaserror>.sarif" [-warnaserror]`.

#### 6.4 Q4 — `.globalconfig` par niveau et précédence (états commités `fixtures/precedence/case-a … case-h`)

Paquet local : `build/` et `buildTransitive/` contiennent un `.props`/`.targets` qui ajoute `HardSlice.Analyzers.level-1.globalconfig` à `GlobalAnalyzerConfigFiles` quand `HardSliceAnalysisLevel` vaut 1. Chaque cas contient exactement une violation F01.

Pack (`must_succeed`) : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/pack.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_REPO/spikes/s1-roslyn/src/HardSlice.Spike.Package" -c Release -o "$HS_RUN/feed"` ; empreinte `shasum -a 256` du `.nupkg` dans la fiche.

| Cas | Configuration | Options du cas | Attendu par le plan (§6.3, §6.5, annexe A.3) | Classe |
|---|---|---|---|---|
| (a) | Aucune propriété | — | HS016 en warning | `must_succeed` ; 1 HS016 `warning` |
| (b) | `HardSliceAnalysisLevel` = 1 à la racine | — | HS016 en erreur | `must_fail(1, HS016 × 1 error)` |
| (c) | (b) + `.globalconfig` utilisateur `dotnet_diagnostic.HS016.severity = suggestion` | — | rétrogradée (le `global_level` 100 utilisateur l'emporte sur le 0 du paquet) | `must_succeed` ; HS016 `note`, 0 warning ni erreur HS016 |
| (d) | (b) + `.editorconfig` utilisateur, même réglage | — | rétrogradée (`.editorconfig` > `.globalconfig`) | `must_succeed` ; comme (c) |
| (e) | (a) | `-warnaserror` | erreur | `must_fail(1, HS016 × 1 error)` |
| (f) | (b) | **`-p:NoWarn=HS016`** | aucun diagnostic | `must_succeed` ; 0 HS016 warning ou erreur |
| (g) | `HardSliceAnalysisLevel` = 99 (inconnu) | — | diagnostic de configuration clair, jamais de désactivation silencieuse | `informative` sur le code ; oracle diagnostics : au moins un diagnostic de configuration explicite, et jamais 0 diagnostic au total |
| (h) | Propriété définie dans un `.csproj` au lieu de la racine | — | comportement MSBuild consigné tel qu'observé ; la détection relève de `verify` (phase 4), aucun rejet exigé en phase 0 | `informative` |

Cas (f) : `-p:NoWarn=HS016` est la forme MSBuild de l'option `/nowarn` du compilateur ; la cible `CoreCompile` transmet la propriété `NoWarn` à `csc` comme `/nowarn:`. Passée en ligne de commande, elle remplace toute valeur `NoWarn` du projet ; la fixture n'en définit aucune.

Commande par cas : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/precedence-<x>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/fixtures/precedence/case-<x>" -nologo --no-incremental "-p:LocalFeed=$HS_RUN/feed" "-p:ErrorLog=$HS_RUN/precedence-<x>.sarif" [options du cas]`.

#### 6.5 Q5 — Performance sur nopCommerce (D-06, M-08, M-08-A1)

Exécution dans le **compte dédié** (§3.4), lancée par le mainteneur après OP-01, par le script de série versionné qui enchaîne exactement les étapes ci-dessous. Conditions M-08 consignées par le mainteneur. Serveur de compilation laissé actif (défaut, consigné).

##### 6.5.1 Bootstrap (réseau limité, avant la coupure) — commun à Q5 et Q6

La même séquence ouvre la série Q5 et la série Q6, chacune dans son propre `$HS_RUN`. **Q6 est donc autonome**, que Q5 ait été exécutée ou non.

| Étape | Commande | Classe |
|---|---|---|
| B0 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/sdk-version.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` | `must_succeed` ; sortie = `10.0.102` |
| B1 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/wrapper-tests.log" -- "$HS_REPO/spikes/s1-roslyn/tools/test-run-logged.sh"` | `must_succeed` |
| B2 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/analyzer-restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_REPO/spikes/s1-roslyn/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj"` | `must_succeed` |
| B3 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/analyzer-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj" -c Release --no-restore -o "$HS_RUN/bin/analyzer"` | `must_succeed` |
| B4 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/bench-restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_REPO/spikes/s1-roslyn/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj"` | `must_succeed` |
| B5 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/bench-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_REPO/spikes/s1-roslyn/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release --no-restore -o "$HS_RUN/bin/bench"` | `must_succeed` |
| B6 | `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/binaries.sha256.log" -- shasum -a 256 "$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll" "$HS_RUN"/bin/bench/*.dll "$HS_RUN/bin/bench/HardSlice.Spike.Bench.runtimeconfig.json" "$HS_RUN/bin/bench/HardSlice.Spike.Bench.deps.json"` | `must_succeed` ; empreintes reportées dans le manifeste et la fiche |
| B7 | Clone du dépôt externe à son SHA exact : `git init`, `git remote add origin <URL>`, `git fetch --depth 1 origin <SHA>`, `git checkout --detach FETCH_HEAD`, chacune par le wrapper dans `$HS_CLONES/<dépôt>` | `must_succeed` |
| B8 | Vérification du clone : `git -C "$HS_CLONES/<dépôt>" rev-parse HEAD`, `git -C "$HS_CLONES/<dépôt>" remote get-url origin`, `git -C "$HS_CLONES/<dépôt>" status --porcelain`, par le wrapper | `must_succeed` ; sorties = SHA, URL, vide |
| B9 | Contrôle SDK de la solution externe : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/external-sdk.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <un .csproj de la solution> -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath` | `must_succeed` ; `10.0.102` et chemin sous `sdk/10.0.102` |
| B10 | Restore de la solution externe (§6.5.2 pour nopCommerce, §6.6 pour FSH) | voir la section |
| B11 | **Coupure réseau** par le mainteneur selon le plan OP-01, heure consignée | — |

Après B11, aucune commande ne restaure ni ne compile le prototype : les builds externes utilisent `--no-restore`, et l'hôte est lancé depuis `$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll`. Les targets d'injection reçoivent `-p:HardSliceSpikeAnalyzerPath=$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll`.

##### 6.5.2 Restore et injection pour nopCommerce

Clone : URL `https://github.com/nopSolutions/nopCommerce`, SHA `221305873bc81c47981b2e510b2e9bed7e5b2076`, dans `$HS_CLONES/nopCommerce`.

**Injection sans modifier la solution.** `-p:CustomAfterMicrosoftCommonTargets=$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets` :
- le fichier ajoute toujours le `.globalconfig` de classification (`config/nopcommerce.globalconfig`) à `GlobalAnalyzerConfigFiles`, ainsi que `<CompilerVisibleProperty Include="NETCoreSdkVersion" />` ;
- il n'ajoute l'analyseur `$(HardSliceSpikeAnalyzerPath)` que si `HardSliceSpikeInjectAnalyzer=true` ;
- les builds de référence passent par la même injection avec `HardSliceSpikeInjectAnalyzer=false`, de sorte que seul l'analyseur diffère ;
- contrôle : l'analyseur figure dans la sortie `ReportAnalyzer` de chaque projet C# de chaque build `H`, et d'aucun build `R`. Sinon la tentative est invalide.

**Classification « pire cas »**, fixée avant mesure :
- K2 = `AssemblyName` de tous les projets C# de `NopCommerce.sln` (liste archivée) ; K6 vide ;
- K3 = `{ Nop, 2 }` : chaque namespace `Nop.<A>.<B>` et ses descendants forment une slice ;
- K5 vide.

Elle maximise les opportunités et les diagnostics créés. Les diagnostics émis sur nopCommerce ne sont **pas** triés (la solution n'est pas organisée en slices) ; ils sont comptés.

B10 (`must_succeed`) : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/nopCommerce/src/NopCommerce.sln" "-p:CustomAfterMicrosoftCommonTargets=$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets" "-p:HardSliceSpikeAnalyzerPath=$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll"`.

##### 6.5.3 (1) Part de HS016 dans la compilation — budget ≤ 5 %

1. Build `<k>-<X>` (`must_succeed`), avec `<X>` = `R` (`<inj>` = `false`) ou `H` (`<inj>` = `true`) :
   `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/build-<k>-<X>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/nopCommerce/src/NopCommerce.sln" -c Release --no-restore --no-incremental -nologo -p:ReportAnalyzer=true "-p:CustomAfterMicrosoftCommonTargets=$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets" "-p:HardSliceSpikeAnalyzerPath=$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll" -p:HardSliceSpikeInjectAnalyzer=<inj> "-bl:$HS_RUN/build-<k>-<X>.binlog" "-flp:logfile=$HS_RUN/build-<k>-<X>.msbuild.log;verbosity=detailed;performancesummary"`
2. **Warm-up non mesuré** : `W1 = R`, puis `W2 = H`. Sorties conservées, exclues du calcul.
3. **Ordre mesuré, fixé et publié avant le run** (contrebalancé ABBA) : `1 R, 2 H, 3 H, 4 R, 5 R, 6 H, 7 H, 8 R, 9 R, 10 H`, soit 5 `R` et 5 `H`. Un build en échec invalide toute la série, qui est rejouée en entier dans le même ordre sous un nouveau `<RUN_ID>` ; la tentative invalide reste publiée.
4. Ratio par build `H` = Σ sur les projets du temps d'exécution de l'analyseur HS016 (lignes `ReportAnalyzer`) / Σ de la durée de la tâche `Csc` (résumé de performance). Verdict sur la **médiane des 5 ratios**. Dispersion publiée selon les conventions du §6.5.5. Temps murs `R` et `H` publiés de même, à titre informatif.

##### 6.5.4 (2) Temps d'exécution de HS016 par document — budget médiane ≤ 20 ms

Commande (réseau coupé, `must_succeed`) : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/perf-docs.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" perf --solution "$HS_CLONES/nopCommerce/src/NopCommerce.sln" --inject "$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --warmup 2 --reps 10 --equivalence-every 10 --out "$HS_RUN/perf-docs.csv"`.

Chargement :
1. `MSBuildLocator.RegisterMSBuildPath("/usr/local/share/dotnet/sdk/10.0.102")` avant tout chargement MSBuild ; `MSBuildSDKsPath` vient de l'environnement en liste blanche.
2. `MSBuildWorkspace.Create(properties)` avec `Configuration=Release`, `CustomAfterMicrosoftCommonTargets=$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets`, `HardSliceSpikeInjectAnalyzer=false` ; `await workspace.OpenSolutionAsync(sln)`.
3. **Échec immédiat (code de sortie ≠ 0, aucune mesure)** si `workspace.Diagnostics` contient au moins une entrée, quel que soit son `WorkspaceDiagnosticKind` ; toutes les entrées sont écrites dans la sortie. Un assouplissement exigerait une nouvelle validation explicite.
4. **Contrôle du SDK effectif** : pour chaque projet, `project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.NETCoreSdkVersion", out v)` doit donner `10.0.102` ; sinon échec.
5. **Contrôle des documents ignorés** : pour chaque projet, la liste `Compile` évaluée par `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <csproj> -getItem:Compile -p:Configuration=Release "-p:CustomAfterMicrosoftCommonTargets=$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets"`, lancée par le wrapper (log `compile-items-<projet>.log`, `must_succeed`), est comparée, chemins normalisés, à `project.Documents`. Tout élément `Compile` absent de `project.Documents`, ou tout document de `project.Documents` absent de `Compile`, fait échouer le run. Les documents générés (`project.GetSourceGeneratedDocumentsAsync()`) sont listés à part.
6. Un projet multi-cible est chargé pour `net10.0` seulement ; un projet sans `net10.0` fait échouer le run (aucun dans `NopCommerce.sln` à la présélection).

Documents inclus : **tous** les arbres de `await project.GetCompilationAsync()` (`Compilation.SyntaxTrees`) de chaque projet C# chargé, y compris les arbres générés, marqués `generated=true` dans le CSV. Exclus : aucun. Le nombre d'arbres par projet est publié. Ordre d'analyse fixé : ordre ordinal des chemins normalisés.

**Mesure primaire, représentative (CompilationWithAnalyzers réutilisé par répétition).** Elle reproduit le fonctionnement d'un build : un analyseur démarre une fois par compilation, puis analyse tous les arbres.
1. Warm-up : 2 répétitions complètes non mesurées par projet.
2. 10 répétitions mesurées par projet. Chaque répétition `r` crée **un seul** `CompilationWithAnalyzers` pour la compilation du projet, puis analyse chaque arbre dans l'ordre fixé :
   ```csharp
   var options = new CompilationWithAnalyzersOptions(
       project.AnalyzerOptions, onAnalyzerException: Fail,
       concurrentAnalysis: false, logAnalyzerExecutionTime: true,
       reportSuppressedDiagnostics: false);
   var cwa = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(hs016), options);
   foreach (var t in treesInFixedOrder)
   {
       var before = (await cwa.GetAnalyzerTelemetryInfoAsync(hs016, ct)).ExecutionTime;
       var sw = Stopwatch.StartNew();
       var model = cwa.Compilation.GetSemanticModel(t);
       var syntaxDiags = await cwa.GetAnalyzerSyntaxDiagnosticsAsync(t, ct);
       var semanticDiags = await cwa.GetAnalyzerSemanticDiagnosticsAsync(model, filterSpan: null, ct);
       sw.Stop();
       var after = (await cwa.GetAnalyzerTelemetryInfoAsync(hs016, ct)).ExecutionTime;
       // d[t, r] = after - before ; w[t, r] = sw.Elapsed
   }
   var total = (await cwa.GetAnalyzerTelemetryInfoAsync(hs016, ct)).ExecutionTime;
   ```
3. `d[t, r]` = temps passé dans les callbacks de HS016 pour l'arbre `t` pendant la répétition `r`. Les callbacks de démarrage de compilation ne s'exécutent qu'une fois par répétition : leur coût est **amorti** sur la compilation. Il est attribué au premier arbre de l'ordre fixé, et cette valeur du premier arbre est publiée à part.
4. **Contrôle de validité** : pour chaque répétition, la télémétrie est monotone non décroissante et `Σ_t d[t, r] = total` (en ticks). Sinon la mesure (2) est **invalide, donc `non mesuré`**, sans repli silencieux sur une autre méthode.
5. Agrégation complémentaire publiée : `A_r = total / N` (moyenne amortie par arbre pour la répétition `r`), et sa médiane sur les répétitions.
6. Une exception de l'analyseur (`Fail`) arrête le run.

**Verdict** : pour chaque arbre, `x_t` = médiane des 10 valeurs `d[t, r]`. Le budget de **20 ms s'applique à la médiane des `x_t`** sur tous les arbres de la solution.

**Borne conservatrice secondaire (publiée, sans verdict).** Après la mesure primaire, chaque arbre est mesuré avec un `CompilationWithAnalyzers` **neuf par document**, selon les mêmes appels. Les callbacks de démarrage sont alors comptés à chaque document. Cette borne indique le coût d'une analyse isolée d'un document ; elle ne sert pas au verdict.

**Contrôle d'équivalence** scoped / complète, avant les mesures, sur un sous-ensemble fixé : les projets de rang 1, 11, 21, 31 dans l'ordre alphabétique ordinal de leur nom (`--equivalence-every 10`). Le prototype est exécuté en **mode statistique** (`SPKOPP` actif). Pour chacun de ces projets, l'union des diagnostics obtenus arbre par arbre avec un `CompilationWithAnalyzers` réutilisé doit être identique, comme multiensemble de (ID, chemin, début, fin, message), à `await cwa2.GetAnalyzerDiagnosticsAsync(ct)` sur la compilation complète avec un `CompilationWithAnalyzers` distinct, opportunités `SPKOPP` comprises. Toute différence invalide la mesure (2). Les mesures sont faites en mode normal (`SPKOPP` inactif).

##### 6.5.5 Conventions statistiques

Toutes les valeurs sont en millisecondes, sauf les ratios.
- Pour une série triée par ordre croissant `x(1) ≤ … ≤ x(n)`, le quantile d'ordre `p` est calculé par **interpolation linéaire, type 7 de Hyndman et Fan** (méthode par défaut de R `quantile` et de NumPy `percentile`, équivalente à `QUARTILE.INC` d'Excel) : `h = (n − 1) × p + 1`, `Q(p) = x(⌊h⌋) + (h − ⌊h⌋) × (x(⌊h⌋ + 1) − x(⌊h⌋))`, avec `Q(p) = x(n)` si `⌊h⌋ = n`.
- **Médiane** = `Q(0,5)`. Pour 10 répétitions, c'est la moyenne des 5ᵉ et 6ᵉ valeurs ; pour 5 ratios, la 3ᵉ valeur.
- **Q1** = `Q(0,25)`, **Q3** = `Q(0,75)`, **écart interquartile** = Q3 − Q1. Pour 5 ratios : Q1 = 2ᵉ valeur, Q3 = 4ᵉ valeur.
- **p95 sur les documents** = rang le plus proche (valeur observée, conservatrice) : valeur de rang `⌈0,95 × N⌉` dans les `x_t` triés.
- **Maximum** = plus grande valeur.
- Publiés pour la mesure primaire, pour `w` (temps mural), pour la borne secondaire et pour les arbres non générés seuls : médiane, Q1, Q3, écart interquartile, p95, maximum.

#### 6.6 Q6 — Exposition (FSH obligatoire ; fdmm exploratoire)

**Préalable** : Q2 conforme pour X1 et X2 avec le même code d'analyseur (même commit HardSlice, ou `git diff` vide sur `spikes/s1-roslyn/src/HardSlice.Spike.Analyzer` entre les deux commits). Sinon Q6 est `non mesuré`.

Exécution dans le **compte dédié** (§3.4), lancée par le mainteneur après OP-01, sous son propre `<RUN_ID>`. Elle commence par le bootstrap complet du §6.5.1, avec le clone `https://github.com/fullstackhero/dotnet-starter-kit` @ `a967537526f3b769c7d268da3ed2a6920af65ce9` dans `$HS_CLONES/dotnet-starter-kit`. Elle ne réutilise aucun binaire ni aucune sortie de Q5.

**Restore et audit (B10 pour FSH).**
1. Audit **informatif**, réseau limité : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/fsh-restore-audit.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx"`. Classe `informative` : son code non nul n'interrompt pas la série. Le code et les avis NU1902/NU1903 (paquet, version, identifiant d'avis) sont publiés dans la fiche.
2. Restore **mesuré** (`must_succeed`), réseau limité : même commande avec `-p:NuGetAudit=false`, log `fsh-restore.log`. **Cette option sert uniquement à éviter que les avis de vulnérabilité soient promus en erreur** par la configuration du dépôt (constaté à la présélection). Elle désactive l'audit pendant le restore, sans changer ni les paquets résolus ni les entrées de la compilation. L'audit de l'étape 1 reste publié.
3. B11 : coupure du réseau.
4. Build de contrôle (`must_succeed`) : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/fsh-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" -c Release -nologo --no-restore --no-incremental -p:NuGetAudit=false`.

**Configuration de classification FSH**, écrite, commitée et hachée **avant** le run (`config/fsh.globalconfig`), selon ces principes :
- K2 = `AssemblyName` des projets `Modules.<M>` et `Modules.<M>.Contracts` des 10 modules. Les projets de tests, `Architecture.Tests` et les outils ne sont pas dans K2, donc hors périmètre ; K6 est vide.
- K3 : une entrée par module, `{ FSH.Modules.<M>.Features.v1, depth }`. `depth` = 2 pour les modules qui ont un niveau `Area` (Identity, Catalog, Chat, Billing, Tickets), 1 pour les autres (Auditing, Files, Multitenancy, Notifications, Webhooks). À confirmer par l'inventaire des namespaces fait au moment d'écrire la configuration, et publié avec elle.
- K5 : `{ FSH.Modules.<M>.Contracts, subtree }` (contrat public du module) ; `{ FSH.Modules.<M>.Domain, subtree }` ; `{ FSH.Modules.<M>.Features.v1.Internal, subtree }` ; la racine de composition et les namespaces transverses identifiés dans l'inventaire.
- Toute entrée ajoutée ou modifiée après le run donne une nouvelle tentative ; toutes sont publiées.

Exposition (réseau coupé, `must_succeed`) : `"$HS_REPO/spikes/s1-roslyn/tools/run-logged.sh" "$HS_RUN/exposure-fsh.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" --inject "$HS_REPO/spikes/s1-roslyn/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_REPO/spikes/s1-roslyn/config/fsh.globalconfig" --out "$HS_RUN/exposure-fsh.csv"`. Mêmes contrôles de chargement que Q5 (2), points 1 à 6.

Sorties : opportunités évaluées (`SPKOPP`), ventilées en même slice / autre slice / frontière autorisée / non classifiable ; diagnostics HS016 et `SPKCFG` émis ; nombres bruts.

**Triage HS016 : exhaustif, par le mainteneur, ou non mesuré** (§6.1 du plan).
- L'agent produit une **liste factuelle** de tous les diagnostics HS016, sans verdict proposé : identifiant de ligne, emplacement normalisé, slice source, type cible et classification de la cible, ligne de code.
- **Le mainteneur effectue le triage initial** de chaque ligne : vrai positif, faux positif ou ambigu, avec justification. Vrai positif : la référence relie deux slices distinctes au sens de la configuration et ne vise pas une frontière. Faux positif : code conforme à l'intention de la règle (même slice mal reconnue, frontière, code généré réel, référence non dépendante).
- Tout cas que le mainteneur juge **ambigu**, ou qui fait l'objet d'une **contestation documentée** (y compris par l'agent), exige un **second relecteur indépendant** (M-10). Tant que ce cas n'est pas tranché, le triage n'est pas complet et **Q6 reste `non mesuré`**.

**Triage `SPKCFG` : séparé, et zéro non résolu pour conclure.**
- Chaque `SPKCFG` est trié par le mainteneur dans une table distincte, avec sa cause :
  - (i) configuration incomplète ou erronée ;
  - (ii) faux `SPKCFG`, dû à un défaut du prototype ;
  - (iii) type réellement non exprimable avec K1–K7.
- Un cas (i) ou (ii) n'est **résolu** que par une correction justifiée (entrée de configuration ou correctif du prototype), commitée, puis par une **nouvelle tentative complète** sous un nouveau `<RUN_ID>`. Les tentatives précédentes restent publiées. Le triage HS016 est refait sur la nouvelle tentative ; un verdict ne peut être repris que pour une ligne strictement identique (ID, emplacement, message), avec renvoi à la ligne d'origine.
- Un cas (iii) est un **constat d'expressivité** publié pour S2a ; il n'est pas résolu.
- Les règles d'ambiguïté et de second relecteur s'appliquent aussi aux `SPKCFG`.
- **Q6 ne conclut qu'avec zéro `SPKCFG` non résolu dans la tentative finale** ; sinon Q6 est `non mesuré`.

**Volume et timebox.**
- Dès que le nombre de diagnostics (HS016 et `SPKCFG`) est connu, et avant de trier, ce nombre est publié ; le mainteneur juge s'il tient dans le reste de la timebox S1-07. Sinon, il décide d'une extension D-12 **avant** l'épuisement ; sans extension, S1-07 est `non mesuré`.
- **Aucun échantillonnage. Aucun seuil n'est conclu d'un triage incomplet.**
- Proposition argumentée d'un seuil d'opportunités pour le corpus de 1B, citant les nombres observés (200 est une cible initiale, pas un seuil imposé).

**fdmm** (`https://github.com/mehdihadeli/food-delivery-modular-monolith` @ `cb7c30c803a293d2257e063f978c80afa62fd8f1`) est **exploratoire**. Il n'est lancé que si le triage FSH est intégralement terminé et si la timebox le permet, dans les mêmes conditions d'isolation et avec son propre bootstrap. Il n'est jamais échantillonné et n'entre dans aucun critère de porte ni dans la conclusion D-S1. Son statut publié est l'un des trois suivants :

| Statut fdmm | Définition |
|---|---|
| **non exécuté** | Aucun `<RUN_ID>` fdmm n'a été créé |
| **commencé, exploratoire incomplet** | Un `<RUN_ID>` existe, mais le run a échoué, a été interrompu, ou le triage n'est pas intégral ; les sorties sont publiées, aucun nombre n'est interprété |
| **exploratoire complet** | Run conforme et triage intégral selon les règles FSH ; publié à titre exploratoire |

#### 6.7 Q7 — Frontière analyseur / `check`

Contrôles à affecter, au minimum :
- détection HS016 par référence ;
- type applicatif non classifiable, référencé ou non (F20, F26, X4) ;
- comportement CLI sur une compilation en erreur (Q1-2) ;
- validation du fichier de conventions ; cohérence de la configuration entre projets ;
- correspondance projet → `AssemblyName` et entrées K6 hors K2 ;
- présence du niveau d'analyse à la racine ;
- dédoublonnage multi-TFM ;
- faux marquage de code généré.

Chaque affectation cite l'artefact Q1–Q5 qui la justifie.

### 7. Règles de conclusion (pré-enregistrées pour D-S1)

| Constat | Conséquence |
|---|---|
| Faux positif connu non résolu (Q1, Q2, Q6) | **no-go technique** |
| Incapacité démontrée à détecter une forme obligatoire ou à éviter un faux positif par opération ou symbole | **no-go technique** |
| Budget mesurable dépassé (Q5 (1) médiane > 5 % ou Q5 (2) médiane des `x_t` > 20 ms) | **no-go technique** |
| Une mesure obligatoire (Q1 à Q6) non exécutée ou invalide, dont un triage FSH incomplet, un cas ambigu non tranché, un `SPKCFG` non résolu ou Q2 non conforme pour X1/X2 | sprint **partiel**, porte fermée |
| Inconnues non bloquantes seulement | **go sous conditions** possible, conditions listées |
| Aucune des lignes précédentes | **go** possible |

Une décision humaine ne convertit pas un no-go technique en go. La recommandation de l'agent n'est pas la décision : D-S1 est signée par le mainteneur. Les résultats exploratoires sur fdmm, quel que soit leur statut, sont publiés mais n'entrent pas dans ces règles.

### 8. Journaux de temps

Deux journaux séparés (D-12). **Seul le journal humain est décompté dans la timebox de 3 j-p (21 h).** Le temps de l'agent et la durée de calcul des séries sont publiés à part et ne sont jamais convertis en jours-personne.

#### 8.1 Journal humain (temps focalisé du mainteneur, déclaré par lui)

| Date | Début | Fin | Durée | Ticket | Activité | Déclaré par |
|---|---|---|---|---|---|---|

Aucune entrée : les durées sont fournies par le mainteneur ; l'agent n'en inscrit aucune de lui-même.

#### 8.2 Journal agent et compute (temps mural, non converti)

| Date | Début | Fin | Durée murale | Ticket | Activité |
|---|---|---|---|---|---|
| 2026-10-28 | ≈ 17:00 CET | ≈ 17:20 CET | ≈ 20 min | S1-01 | Étape A : protocole initial et présélection D-06 en lecture seule. Début approximatif, premier horodatage relevé : 17:04 CET |
| 2026-10-28 | avant 17:27 CET | 17:36 CET | ≈ 10 à 15 min | S1-01 | Passe corrective demandée par A-01, dont une vérification de l'évaluation MSBuild avec le SDK 10.0.102 sur les clones nopCommerce et FSH, hors mesure. Début non relevé ; premier horodatage : copie de l'état de l'étape A à 17:27:09 CET |
| 2026-10-28 | 18:39 CET | 19:09 CET | ≈ 30 min | S1-01 | Passe corrective demandée par A-02, dont l'écriture et le test du wrapper `run-logged.sh`, hors mesure. Premier horodatage : copie de l'état de la passe précédente à 18:39:10 CET |
| 2026-10-28 | 19:31 CET | 19:40 CET | ≈ 10 min | S1-01 | Passe corrective demandée par A-03 : wrapper durci, test versionné (`passed=34 failed=0`), protocole, hors mesure. Premier horodatage : copie de l'état de la passe précédente à 19:31:04 CET |

### 9. Répartition de la timebox (proposition soumise à V-S1-01)

Total inchangé : **3 j-p = 21 h** de temps focalisé humain. S1-07 passe de 0,25 à 0,5 j-p ; la différence est prise sur S1-05 et S1-06. Pour S1-05, l'exécution est scriptée par l'agent dans le compte du mainteneur. Pour S1-06, le mainteneur lance une série scriptée dans le compte dédié ; seul son temps d'attention (préparation, lancement, contrôle, relecture) compte, et la durée de calcul va au journal compute.

| Ticket | Backlog | Proposé | Heures | Raison |
|---|---:|---:|---:|---|
| S1-01 | 0,25 | 0,25 | 1 h 45 | Relecture et validation du protocole, y compris les passes correctives |
| S1-02 | 0,5 | 0,5 | 3 h 30 | 29 formes et leurs oracles à relire, chaque écart à arbitrer |
| S1-03 | 0,25 | 0,25 | 1 h 45 | — |
| S1-04 | 0,25 | 0,25 | 1 h 45 | Manipulation IDE entièrement humaine (D-07, PRE-S1-04) |
| S1-05 | 0,5 | **0,375** | 2 h 37 min 30 s | Pack et 8 cas scriptés par l'agent ; humain : relecture de la table de précédence |
| S1-06 | 0,5 | **0,375** | 2 h 37 min 30 s | Série lancée par le mainteneur dans le compte dédié ; humain : conditions M-08, lancement, relecture |
| S1-07 | 0,25 | **0,5** | 3 h 30 | Lancement de la série FSH, **triage initial intégral HS016 et `SPKCFG` par le mainteneur** ; le temps d'un second relecteur (M-10) est publié à part |
| S1-08 | 0,25 | 0,25 | 1 h 45 | Décision de frontière par le mainteneur |
| S1-09 | 0,25 | 0,25 | 1 h 45 | Conclusion et signature D-S1 |
| **Total** | **3,0** | **3,0** | **21 h** | |

Règles :
- Un reliquat sur un ticket peut servir à un ticket ultérieur ; un dépassement est pris sur le reliquat disponible, jamais sur la mesure obligatoire d'un autre ticket.
- La préparation du compte dédié (OP-01) et l'installation de l'IDE (PRE-S1-04) sont des prérequis opérationnels ; le mainteneur décide s'il les impute à la gouvernance ou à S1, et le consigne dans le journal humain.
- Si le volume de diagnostics FSH, publié avant le triage, ne tient pas dans le reste de S1-07 : demande d'extension D-12 (au plus 0,5 j-p, une seule fois) **avant** l'épuisement ; sans extension, S1-07 est `non mesuré`.
- `phase-0-backlog.md` et `sprint-01.md` ne sont alignés sur cette répartition qu'après V-S1-01.

### Présélection D-06

Vérification de compilabilité en lecture seule, le 28/10/2026, **pas une mesure** : clones `git clone --depth 1` et builds dans un dossier temporaire de session hors dépôt, **dans le compte du mainteneur et sans l'isolation de M-08-A1**, qui n'existait pas encore. Ces sorties sont éphémères et ne servent à aucune mesure. Chaque clone retenu est identifié par **URL + SHA + worktree propre** ; les mesures reclonent dans le compte dédié et revérifient ces trois éléments. Temps de build donnés à titre indicatif (cache NuGet froid ou chaud selon le cas). Sortie MSBuild localisée en français.

Critère « grosse », fixé avant la présélection : ≥ 2 000 fichiers C# compilés (hors `obj/`, `bin/`) et ≥ 20 projets dans la solution mesurée, `dotnet build` simple, SDK 10.0.1xx.

#### Grosse solution (S1-06)

| Candidat | SHA | Licence | Solution, projets | `.cs` dans la solution, LOC | SDK 10.0.102 | Critère |
|---|---|---|---|---|---|---|
| nopSolutions/nopCommerce | `221305873bc81c47981b2e510b2e9bed7e5b2076` | NPL 4.0 (AGPL-3.0 + mention « powered by ») | `src/NopCommerce.sln`, 40 | 3 661, 386 109 | exit 0, 0 erreur, 1 warning, 67,6 s | **oui** — **retenu (D-06)** |
| Squidex/squidex | `8a345bb750907f6730bde900c75be8462e7ced11` | MIT | `backend/Squidex.slnx`, 20 | 2 743, 318 429 | exit 0, 0 erreur, 70 warnings dont CS9057 (générateur de tests du dépôt ignoré), 48,3 s | oui, au plancher de 20 projets |
| OrchardCMS/OrchardCore | `55db477a88eed6a0dd09cc5f86dd30190a61c321` | BSD-3-Clause | `OrchardCore.slnx`, 256 | 6 292, 430 420 | exit 1, 9 erreurs : ses analyseurs et générateurs exigent Roslyn ≥ 5.9.0 (CS9057) ; même échec avec 10.0.201 | non |
| jellyfin/jellyfin | `02c2951eb72d7282ff9e1c7610489edb924898c3` | GPL-2.0 | `Jellyfin.sln`, 41 | 2 248, 365 970 | exit 1, 1 erreur CS9057 (analyseur du dépôt sur Roslyn 5.9.0) ; même échec avec 10.0.201 | non |
| umbraco/Umbraco-CMS | `611520f82495f5d8cb6cdca67534d149b8e36c8a` | MIT | `umbraco.sln`, 30 | 7 788, 803 716 | non construit : le build exécute `npm` | non |

Anomalies : nopCommerce épingle le SDK 10.0.401 (non installé) ; la présélection a remplacé temporairement son `global.json` puis l'a restauré (`git checkout`). Pour les mesures, la règle d'invocation du §3.1 s'applique sans modifier le clone (évaluation vérifiée le 28/10/2026). Squidex a été forcé sur 10.0.102 par un `global.json` placé dans le dossier parent, hors clone.

Constat utile au plan (annexe A.3) : deux grosses solutions récentes ne compilent plus sur les SDK installés parce que leurs propres analyseurs référencent un Roslyn plus récent que le compilateur (CS9057). Cela confirme la règle « un analyseur ne référence pas un Roslyn plus récent que le plus ancien compilateur visé ».

#### Dépôts en slices (S1-07)

Le build initial des dépôts en slices a utilisé le SDK 10.0.201. Les deux premiers candidats ont été recompilés avec 10.0.102 (invocation directe du `dotnet.dll` de ce SDK, `--no-incremental`), sans modifier les clones.

| Candidat | SHA | Licence | Motif de namespace des slices | Slices observées | `.cs`, LOC, projets | Build |
|---|---|---|---|---|---|---|
| fullstackhero/dotnet-starter-kit (FSH) | `a967537526f3b769c7d268da3ed2a6920af65ce9` | MIT | `FSH.Modules.<Module>.Features.v1.[<Area>.]<Slice>` ; commandes et requêtes dans des projets `Modules.<M>.Contracts` séparés | 178 namespaces feuilles (10 modules) | 1 716, 131 302, 52 | 10.0.102 : exit 0, 0 warning, 0 erreur (`-p:NuGetAudit=false`) — **retenu, obligatoire (D-06)** |
| mehdihadeli/food-delivery-modular-monolith (fdmm) | `cb7c30c803a293d2257e063f978c80afa62fd8f1` | MIT | `FoodDelivery.Modules.<Module>.<Aggregate>.Features.<Slice>[.v1]` | 48 | 434, 18 128, 16 (hors sous-module) | 10.0.102 : exit 0, 1 469 warnings, 0 erreur (`-p:NuGetAudit=false -p:NBGV_GitEngine=Disabled`) — **exploratoire (D-06)** |
| mehdihadeli/food-delivery-microservices | `a5750a994acfb64c8d939a3b622bbd0504ce1796` | MIT | `FoodDelivery.Services.<Service>.<Aggregate>.Features.<Slice>.v1` | 42 | 629, 29 202, 62 (hors sous-module) | 10.0.201 seulement : exit 0 |
| kgrzybek/modular-monolith-with-ddd | `91c8ef24b4cb6ef558c95d8267fa07d68c7059f8` | MIT | `CompanyName.MyMeetings.Modules.<Module>.Application.<Feature>.<Slice>` (structure en couches, inactif depuis 2024, `net8.0`) | 88 | 1 062, 37 294, 47 | 10.0.201 seulement, avec filtre de solution hors clone : exit 0 |
| jbogard/ContosoUniversityDotNetCore-Pages | `abb4c08bd4fbc14fc2ea6c56b31c3381d83ea9ab` | MIT | slices = types imbriqués, pas namespaces | 19 | 70, 4 591, 4 | 10.0.201 seulement : exit 0 |

Méthode de comptage des slices : `grep` des déclarations de namespace contenant `.Features.`, tronquées au segment de slice et dédoublonnées. Il s'agit d'un **décompte structurel**, pas d'opportunités. fdmm et food-delivery-microservices partagent le sous-module `BuildingBlocks` (initialisé avec `--depth 1`). Écartés sans build : evolutionary-architecture-by-example (≈ 8 slices par chapitre), booking-microservices et booking-modular-monolith (18 et 19 slices).

## Résultats

Aucun. Aucune mesure n'a été exécutée.

## Frontière

À rédiger par S1-08 après les mesures.

## Conclusion

À rédiger par S1-09 après les mesures. D-S1 reste `OPEN` jusqu'à la signature du mainteneur.

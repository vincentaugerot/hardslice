# S1 — Spike Roslyn (HS016)

Statut : **protocole DRAFT**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02, A-03, A-05, A-06), **non validé** (V-S1-01 `OPEN` dans [phase-0-decisions.md](../execution/phase-0-decisions.md)). **Aucune mesure n'a été exécutée.**
Sources : [plan.md](../plan.md) §6.1, §6.2, §6.3, §6.4, §6.5, §7.2, §10, annexe A.3 ; backlog S1-01 à S1-09 ; [s2a-conventions.md](s2a-conventions.md).

## Protocole

### 1. Objet et périmètre

Déterminer, mesures à l'appui, si HS016 (« une slice n'utilise aucun type d'une autre slice ») est détectable par opération ou par symbole, à travers les assemblies, en IDE et en CLI, dans les budgets du §6.4, avec un `.globalconfig` par niveau livré par paquet ; fixer la frontière analyseur / `dotnet hardslice check` et une première calibration de l'exposition HS016.

- Le code du spike est **jetable** : il n'est pas l'implémentation de HS016 de la phase 1A et n'en préjuge pas la forme.
- Pas d'analyseur de fin de compilation (`RegisterCompilationEndAction`) dans le prototype (§6.1) ; s'il s'avérait nécessaire, c'est un résultat à consigner comme écart, pas une solution adoptée.
- Identifiants jetables du prototype : `HS016` pour la règle (nécessaire pour tester la précédence de sa sévérité) ; `SPKCFG` pour un type applicatif non classifiable, de sévérité **`DiagnosticSeverity.Error`** (diagnostic de configuration bloquant, plan §6.2) ; `SPKOPP` (sévérité Info, mode statistique seulement) pour compter les opportunités. Aucun identifiant n'est alloué (D-09).
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
| **D-18** | Protection des preuves contre une commande exécutée sous le même UID (§3.4) | **`OPEN`** — V-S1-01 reste `OPEN` tant que D-18 n'est pas décidée |
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
| 4 | Prologue P0 (§6.5.1) : lancement par la commande `env -i` imposée ; `<RUN_ID>` généré à partir de l'heure UTC, de la question, du SHA, du numéro de tentative et d'un UUID ; réservation exclusive dans le registre hors Git (§3.2) ; worktree propre ; `$HS_ART` possédé par le compte, en `0700`, sans ACL permissive et hors de tout dépôt ou worktree Git ; export exact du spike au commit mesuré sous `$HS_RUN` ; arborescence créée en `0700` sans `-p` ; chemins canoniques sans lien symbolique ; manifeste privé initialisé | Script de série | Toute tentative (Q1 à Q6) |
| 5 | Inventaire de l'environnement effectif archivé (étape B0a) | Script de série | Toute tentative |
| 6 | `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` = `10.0.102` (étape `must_succeed` dont la sortie est comparée) | Script de série | Toute tentative |
| 7 | Journal humain : heure de début déclarée par le mainteneur | Mainteneur | Tout ticket S1 |
| 8 | OP-01 attestée (compte dédié, `HOME` vide, aucun secret ni donnée UXR, environnement en liste blanche, cache NuGet dédié, NuGet public, réseau limité, accès limité, conditions M-08) | Mainteneur | Premier restore de nopCommerce ou de FSH |
| 9 | Bootstrap Q5/Q6 (§6.5.1) terminé, empreintes des binaires enregistrées, **avant** la coupure réseau | Script de série, lancé par le mainteneur | Q5, Q6 |
| 10 | Clones vérifiés : URL, SHA, `git status --porcelain` vide, avant et après la série | Script de série | Q5, Q6 |
| 11 | **Q5** : algorithme et outil `classify-worst-case` commités avant le run ; classification nopCommerce générée en C1 puis hachée en C2 avant toute mesure. **Q6** : `config/fsh.globalconfig` commitée et hachée avant le run | Agent | Q5, Q6 |
| 12 | Q2 conforme pour X1 et X2 au même code d'analyseur | Agent | Q6 |
| 13 | PRE-S1-04 attestée (versions exactes de VS Code stable, C# Dev Kit, extension C#, runtime) | Mainteneur | S1-04 |
| 14 | M-10 identifié, ou risque accepté que tout cas ambigu laisse Q6 `non mesuré` | Mainteneur | Triage S1-07 |
| 15 | D-18 décidée, et protocole amendé en conséquence si l'option A est retenue | Mainteneur | V-S1-01, donc tout run |
| 16 | Fin de série OP-01 (arrêt des processus, inventaire, remise à zéro) attestée pour la série précédente avant toute réutilisation du compte dédié | Mainteneur | Toute nouvelle série Q5/Q6 |
| 17 | `$HS_ART` hors synchronisation iCloud, Dropbox, OneDrive ou autre ; emplacement attesté dans la fiche. P0 contrôle la partie Git, cette attestation couvre la synchronisation | Mainteneur | Toute tentative (Q1 à Q6) |

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

**`<RUN_ID>`** identifie une tentative de façon immuable : `<AAAAMMJJ>T<HHMMSS>Z-<Qn>-<SHA court de 12 caractères du commit HardSlice>-a<NN>-u<UUID sans tirets, 32 hexadécimaux minuscules>`, par exemple `20261030T091500Z-Q1-0123456789ab-a01-u2393e983a9c1489c9fbc780a231f1cb4`. Le mainteneur fournit seulement `ATTEMPT=<NN>`, étiquette humaine à deux chiffres consignée dans la fiche et qui n'est pas le mécanisme d'unicité ; P0 la valide, relève lui-même l'heure UTC, la question et le SHA, génère l'UUID, valide chacun de ces composants puis compose le `RUN_ID`. L'UUID rend les collisions opérationnellement négligeables même entre deux racines d'artefacts ; la réservation exclusive `$HS_ART/.run-ids/<RUN_ID>/` interdit en plus toute réutilisation locale. Ce registre est hors Git, ne contient aucune sortie brute et n'est jamais supprimé, y compris après le rangement ou le nettoyage d'une tentative. Le dossier versionnable `results/<RUN_ID>/` n'est créé qu'au moment de publier la fiche et les extraits normalisés, avec refus s'il existe déjà : le créer avant la mesure salirait le worktree dont P0 doit attester la propreté. Une tentative interrompue garde sa réservation, et la suivante reçoit un nouvel identifiant.

Les commandes versionnées n'emploient que ces variables ; leur expansion exacte est écrite dans le manifeste privé `$HS_RUN/manifest.private.md`, hors git.

| Variable | Contenu |
|---|---|
| `$HS_REPO` | Copie de travail propre du dépôt HardSlice au commit mesuré ; variante actuelle de l'option D-18-B. Si D-18-A est retenue, elle est remplacée par un export minimal attesté avant V-S1-01 |
| `$HS_ART` | Dossier dédié `0700`, possédé par le compte d'exécution et situé strictement sous son `$HOME`, hors git et hors synchronisation cloud |
| `$HS_RUN` | `$HS_ART/<RUN_ID>`, dossier de la tentative : **seul emplacement des sorties brutes** |
| `$HS_CLONES` | `$HS_RUN/clones`, clones OSS de la tentative |
| `$HS_SPIKE` | `$HS_RUN/work/spikes/s1-roslyn`, export `git archive` exact du spike au commit `$HS_REPO_HEAD` ; toutes les compilations du spike travaillent sur cette copie |

Permissions : dossiers `0700`, preuves et données `0600`, scripts exportés exécutables uniquement par leur propriétaire (le wrapper impose `umask 077`, le script de série aussi). Les sorties sont ensuite rangées par le mainteneur sous l'emplacement D-10 (`~/Documents/hardslice-artifacts/s1-roslyn/<RUN_ID>/`), avec vérification des SHA-256 du manifeste et refus d'écraser un dossier existant.

#### 3.3 Wrapper de commande et vérification d'intégrité

Toute commande passe par le wrapper versionné [`spikes/s1-roslyn/tools/run-logged.sh`](../../spikes/s1-roslyn/tools/run-logged.sh) :

```
"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/<étape>.log" -- <commande> [arguments…]
```

Comportement :
- **Création** : `umask 077`. Le chemin du log est absolu et son parent doit être **canonique** (`pwd -P` du parent égal au chemin donné) : aucun lien symbolique dans aucun ancêtre, ni `.` ni `..`. Le log et son sidecar `<log>.exit` sont créés exclusivement (`noclobber`, donc `O_EXCL`), en mode `0600`.
- **Préflight** : refus avant lancement (code 70, commande non lancée) si le parent est absent ou non canonique, si le log ou le `.exit` existe déjà, ou si l'un d'eux est un lien symbolique (même cassé). Chemin relatif ou usage incorrect : 64.
- **En-tête** : horodatage UTC, `# log-id: <device>:<inode>` du log, commande exacte ; écrit puis relu avant le lancement.
- **Exécution** : la commande n'hérite jamais du descripteur du log ; stdout et stderr passent par un pipe consommé par le wrapper. Le pipe n'atteint EOF qu'après la fermeture de tous les descendants qui l'ont hérité : leur sortie est donc complète avant l'écriture du pied. Un descendant persistant qui conserve le pipe ouvert maintient volontairement la commande en cours au lieu de produire une preuve prématurée.
- **Après la commande, avant de faire confiance au résultat** :
  - même `device:inode`, pas de lien symbolique, en-tête intact ;
  - aucun `.exit` apparu pendant l'exécution ;
  - pied `# end` / `# exit: <code>` écrit, puis revérifié après fermeture, avec l'identité et l'en-tête ;
  - `.exit` créé exclusivement, relu, cohérent avec le pied.
- **Codes de sortie** : le code réel de la commande si tout l'enregistrement est cohérent ; 64 sur erreur d'usage ; **70 pour toute panne de préflight ou d'enregistrement, quel que soit le code retourné par la commande**. En cas de panne d'enregistrement, le vrai code est écrit sur stderr (« not usable as a result ») et, si possible, dans le pied ; il n'est **jamais** utilisable comme résultat.

**Vérification par le script de série, dans cet ordre, avant tout oracle métier** :
1. **Identité et intégrité du log** : fichier régulier, pas un lien symbolique, propriétaire = compte d'exécution, mode `0600`, `device:inode` actuel égal à la ligne `# log-id` ; SHA-256 relevé dans le manifeste privé.
2. **En-tête complet** : lignes `# start`, `# log-id` et `# cmd` présentes ; `# cmd` égal à la commande lancée par le script.
3. **Pied présent et cohérent** : dernière ligne `# exit: N`, précédée de `# end`, aucune ligne `# record: FAILED`.
4. **Sidecar `.exit` présent et cohérent** : fichier régulier, mode `0600`, contenu `N`, et code de retour du wrapper égal à `N`.
5. **Seulement ensuite**, comparaison de `N` et des diagnostics à la classe `must_succeed`, `must_fail`, `must_fail_partial` ou `informative` (§3.5).

Un échec à l'une des étapes 1 à 4 est une **panne d'infrastructure** : la tentative est invalide, quelle que soit la classe de l'étape. Un échec attendu (`must_fail` ou `must_fail_partial`) n'est jamais atteint par un code 70 sans enregistrement complet et cohérent ; un code 70 avec pied et `.exit` égaux à 70 est le vrai code de la commande. Le « corps » d'un log est l'ensemble des lignes entre l'en-tête et le pied ; les comparaisons de sortie portent sur lui.

Ces contrôles détectent les erreurs et les écrasements accidentels. **Ils ne protègent pas contre une commande malveillante exécutée sous le même UID** (§3.4, D-18).

Test automatisé versionné : [`spikes/s1-roslyn/tools/test-run-logged.sh`](../../spikes/s1-roslyn/tools/test-run-logged.sh). Il travaille dans un dossier temporaire privé et canonique, et lance le wrapper sous `umask 022` pour prouver que le wrapper impose lui-même `0600`. Cas couverts :
- succès, échec, stdout et stderr, sortie sans fin de ligne, code 127, ligne `# log-id` égale au `device:inode` réel ;
- descendant asynchrone qui écrit après la sortie de son parent : sa sortie précède le pied, jamais l'inverse ;
- quatre formes de mauvais usage, dont le chemin relatif ;
- préflight : parent absent ou non canonique (`..`), log existant, `.exit` existant, lien symbolique cassé ou valide pour le log, lien symbolique pour le `.exit`, parent lien symbolique, **lien symbolique dans un ancêtre** ; pour chacun, preuve que la commande (création d'un fichier marqueur) n'a pas été exécutée ;
- **pannes après lancement, toutes à 70** :
  - commande qui retourne 1 puis rend impossible la création du `.exit` (code 1 reporté sur stderr et dans le pied, pas de `.exit`) ;
  - commande qui écrit un faux `.exit` (ni écrasé ni pris en compte) ;
  - commande qui déplace le log, qui le remplace par un autre fichier (autre inode), ou par un lien symbolique ;
  - commande qui altère l'en-tête en place (même inode) ;
- permissions `0600`, restauration des permissions modifiées, suppression du dossier temporaire vérifiée.

Résultat du 28/10/2026, hors mesure : `passed=45 failed=0`.

#### 3.4 Isolation et environnement d'exécution (M-08-A1, OP-01)

Tout restore, build ou benchmark de **nopCommerce et de FSH** s'exécute sur le M4 Max, sous un compte macOS local **dédié et non privilégié**, préparé par le mainteneur et attesté par **OP-01** avant le premier restore :
- `HOME` dédié et vide ;
- aucun secret, trousseau, agent SSH, credential cloud ou donnée UXR accessible ;
- configuration NuGet limitée aux sources publiques requises ;
- cache NuGet dédié à la tentative ;
- accès uniquement aux entrées autorisées par l'option D-18 retenue et au dossier d'artefacts (`$HS_ART`) : export minimal sans `.git` ni `docs/` pour A, ou copie complète avec risque explicitement accepté pour B ;
- réseau limité pendant le restore et le clonage : LAN bloqué, sortie limitée aux flux nécessaires (`api.nuget.org` et ses CDN, `github.com` pour les clones), puis coupure complète après le bootstrap. Si ce filtrage n'est pas réalisable, le **risque résiduel** est décrit et accepté explicitement par le mainteneur dans OP-01. Un échec dû à l'absence de réseau est publié ; toute réactivation est consignée avec sa raison.

**Limite du même UID et de la copie source (D-18, `OPEN`).** Dans la variante actuelle, le code OSS, les outils de build et le wrapper s'exécutent sous le même compte : une commande peut modifier ses propres logs, sidecars, binaires ou configurations. Pendant les restores avec réseau limité, elle peut aussi lire tout contenu accessible dans la copie `$HS_REPO` et tenter de l'exfiltrer vers une destination autorisée. Les contrôles d'inode et d'en-tête du §3.3 ne résolvent aucune de ces deux limites. D-18 choisit entre :
- **(A) recommandée** : superviseur et logger sous une identité distincte du runner ; répertoire de preuves non inscriptible par le runner ; stdout et stderr capturés par pipe par le superviseur ; entrées et binaires en lecture seule pour le runner ; sorties du runner écrites dans un staging, puis copiées et hachées par le superviseur. Le runner ne voit jamais le dépôt complet : le superviseur lui fournit un export minimal du commit, limité à `spikes/s1-roslyn/`, sans `.git` ni `docs/`, accompagné du SHA source et d'un manifeste SHA-256 ;
- **(B) alternative** : accepter explicitement que les preuves protègent contre les erreurs et les écrasements accidentels, mais pas contre une commande malveillante exécutée sous le même UID, et accepter le risque de lecture ou d'exfiltration de tout contenu accessible dans `$HS_REPO` pendant la fenêtre réseau.

V-S1-01 reste `OPEN` tant que D-18 n'est pas décidée. Si (A) est retenue, ce protocole est amendé (invocation du wrapper par le superviseur, répartition des répertoires), puis soumis de nouveau à V-S1-01.

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
| `HS_REPO`, `HS_ART`, `HS_RUN`, `HS_CLONES`, `HS_SPIKE`, `RUN_ID` | §3.2 ; `RUN_ID` est généré et validé par P0 |

#### 3.5 Classes de commandes et oracles de code de sortie

Chaque étape d'un script de série porte une classe pré-enregistrée. Le wrapper conserve toujours le code réel quand l'enregistrement est complet. **Le script de série** vérifie d'abord l'intégrité de l'enregistrement (§3.3, étapes 1 à 4), puis compare le code et, si la classe l'exige, les diagnostics observés à l'oracle :

| Classe | Oracle | Série si l'oracle n'est pas atteint |
|---|---|---|
| `must_succeed` | Code 0 ; sortie comparée quand c'est précisé | Arrêt ; tentative invalide |
| `must_fail(<code>, <diagnostics>)` | Code exact et ensemble exact de diagnostics attendus (ID, sévérité, nombre, emplacement si précisé) ; 0 AD0001 ; aucun autre diagnostic d'erreur | Arrêt ; tentative invalide |
| `must_fail_partial(<code>, requis, autorisés)` | Code exact ; diagnostics requis exacts ; tout diagnostic supplémentaire appartient à l'allowlist explicite. Les diagnostics autorisés sont publiés mais leur présence et leur nombre ne participent pas au succès de l'oracle | Arrêt ; tentative invalide |
| `informative` | Aucun sur le code ; code et sortie publiés | Continue |

Un échec attendu conforme à son oracle (`must_fail` ou `must_fail_partial` atteint) **ne stoppe pas** la série ; tout écart à l'oracle (code, diagnostic requis manquant, diagnostic interdit en trop ou mauvaise sévérité) invalide la tentative. Pour `must_fail`, aucun diagnostic supplémentaire n'est autorisé ; pour `must_fail_partial`, seuls ceux de l'allowlist le sont. Une panne d'infrastructure (§3.3) invalide la tentative quelle que soit la classe. Les diagnostics sont lus dans les SARIF par projet, jamais dans le texte : chaque fixture définit, dans `fixtures/Directory.Build.props`, `ErrorLog = $(HardSliceSpikeSarifDir)/$(HardSliceSpikeStep)-$(MSBuildProjectName).sarif`, et les commandes passent `-p:HardSliceSpikeStep=<étape>` et `-p:HardSliceSpikeSarifDir=$HS_RUN/sarif`. Pour l'hôte de benchmark, ils sont lus dans sa sortie structurée. Une tentative invalide reste publiée.

**Hôte de benchmark et diagnostics bloquants.** Les commandes `exposure`, `inspect-symbols`, `classify-worst-case` et `perf` de l'hôte sortent en **0 dès que l'analyse est complète** et que leurs contrôles de chargement passent, quel que soit le nombre de HS016 ou de `SPKCFG` observés. Un `SPKCFG`, même de sévérité Error, est un **diagnostic** compté et listé, jamais une panne du programme. Les codes non nuls sont réservés aux pannes : 2 pour un échec de chargement ou de contrôle (diagnostics du workspace, documents ignorés, SDK effectif), 3 pour une exception d'analyseur, 4 pour une incohérence de contrôle (télémétrie, diagnostics différents entre répétitions), 64 pour un usage incorrect. L'oracle sur les nombres de diagnostics est appliqué ensuite par le script de série.

### 4. Organisation du spike (D-10, D-10-A1)

```
spikes/.gitignore                   garde-fous : *.log, *.exit, *.binlog, *.sarif, *.csv, raw/
spikes/s1-roslyn/
  global.json                       SDK 10.0.102
  README.md                         reproduction pas à pas
  src/HardSlice.Spike.Analyzer/     prototype HS016 (netstandard2.0)
  src/HardSlice.Spike.Package/      paquet local + level-N.globalconfig (Q4)
  bench/HardSlice.Spike.Bench/      hôte CompilationWithAnalyzers (Q1, Q5, Q6)
  fixtures/Directory.Build.props    SARIF par projet (HardSliceSpikeSarifDir, HardSliceSpikeStep)
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

**Toute sortie brute reste exclusivement sous `$HS_RUN`**, quelle que soit sa taille : export de travail, `bin/obj`, binlogs, logs complets, `.exit`, SARIF, CSV, sorties MSBuild, captures brutes, clones OSS. `results/<RUN_ID>/` ne reçoit que des fiches et des extraits **normalisés** : nom d'utilisateur, `$HOME` et chemins locaux remplacés par `<USER>`, `<HOME>`, `<HS_ART>`, `<HS_RUN>`, `<HS_CLONES>`, `<HS_SPIKE>`, `<HS_REPO>`, puis contrôle de secrets ; un extrait qui échoue au contrôle n'est pas versionné. `spikes/.gitignore` ignore les extensions brutes et les dossiers `raw/` comme garde-fou.

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

Chaque série Q1 à Q6 commence par P0, puis par les contrôles communs B0, B0a et B1 du §6.5.1. Les tables Q1 à Q4 ne répètent ensuite que leurs étapes métier. B2 à B11 constituent le bootstrap supplémentaire, avec réseau limité, propre à Q5 et Q6.

| Q | Ticket | Question | Mesure | Critère | Artefact versionné |
|---|---|---|---|---|---|
| Q1 | S1-02 | HS016 est-elle détectable par opération ou par symbole dans une compilation, sur les formes 1A ? | Opportunités et diagnostics observés par forme, comparés aux oracles pré-enregistrés (§6.1) | Oracles atteints ; 0 faux positif ; 0 doublon ; 0 AD0001 ; formes manquées nommées avec l'API en cause | `results/<RUN_ID>/forms.md` |
| Q2 | S1-03 | La détection fonctionne-t-elle quand le type cible vient d'un autre assembly ? | Diagnostics X1–X4 ; informations de symbole observées X5 | Oracles X1–X5 atteints | `results/<RUN_ID>/cross-assembly.md` |
| Q3 | S1-04 | Comportement dans l'IDE retenu et en CLI, depuis un paquet local ? | Observation humaine des états I1–I7 et CLI côte à côte | Divergences listées ; captures datées | `results/<RUN_ID>/ide-cli.md` |
| Q4 | S1-05 | `.globalconfig` par niveau livré par paquet et précédence effective ? | Cas (a)–(h), attendu du plan contre observé | Tout écart au §6.3 signalé comme écart | `results/<RUN_ID>/globalconfig-precedence.md` |
| Q5 | S1-06 | Coût sur nopCommerce ? | (1) part de HS016 dans le temps de compilation ; (2) temps total de HS016 sur une analyse complète, rapporté au nombre de documents | (1) ≤ 5 % ; (2) médiane des 10 `m_r` ≤ 20 ms par document | `results/<RUN_ID>/perf.md` |
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
| Q1-1 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/forms-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/forms/Forms.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=forms "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif"` | `must_fail(1, { HS016 × 29, sévérité Warning, aux sites canoniques du tableau ; SPKCFG × 2, sévérité Error, sur les déclarations de `Fx.Misc.Helper` et `Fx.Misc.Orphan` })` ; 0 AD0001 ; aucun autre diagnostic d'erreur. Le code 1 vient des deux `SPKCFG` bloquants |
| Q1-2 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/forms-broken-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/forms-broken/FormsBroken.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=forms-broken "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif"` | `must_fail_partial(1, requis = { CS0246 × 1 sur Missing ; 0 AD0001 }, autorisés = { HS016 Warning sur BookRoomDto })` ; présence et nombre de HS016 observés et publiés sans oracle ; aucun autre diagnostic d'erreur (entrée de Q7) |
| Q1-3 | Construction de l'hôte : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/bench-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release -o "$HS_RUN/bin/bench"` | `must_succeed` |
| Q1-4 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/forms-exposure.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_SPIKE/fixtures/forms/Forms.slnx" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_SPIKE/config/forms.globalconfig" --evidence-prefix forms --out "$HS_RUN/forms-exposure.csv"` | `must_succeed` (l'hôte sort en 0 malgré les `SPKCFG`, §3.5) ; oracle : colonnes `SPKOPP`, HS016 et `SPKCFG` du tableau, sans F23 |
| Q1-5 | Même commande sur `fixtures/forms-broken/FormsBroken.slnx` avec `config/forms-broken.globalconfig`, `--evidence-prefix forms-broken`, sortie `forms-broken-exposure.csv`. L'hôte accepte une compilation en erreur pour ce cas | `must_succeed` ; oracle F23 : 1 `SPKOPP`, 1 HS016, 0 exception d'analyseur |

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
| F23 (`fixtures/forms-broken`) | Compilation cassée : `BookRoomDto d = default; Missing m = default; m.Foo();` dans la slice `CancelBooking`, `BookRoomDto` déclaré dans la slice `BookRoom` du même projet | 1 (autre slice) ; 0 sur les symboles en erreur | Hôte : 1, sur `BookRoomDto`. CLI : `must_fail_partial(1, requis = CS0246 × 1, autorisé = HS016 Warning sur BookRoomDto)`, 0 AD0001 ; présence et nombre de HS016 observés sans oracle | 0 |

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

Le périmètre applicatif (K2) est défini **par `AssemblyName`**, comparé en ordinal à `IAssemblySymbol.Name` du type cible et à `Compilation.AssemblyName` du consommateur. Les consommateurs X1, X3 et X4 sont dans la même solution ; X2 en est la variante par paquet.

| # | Configuration | K2 | Attendu |
|---|---|---|---|
| X1 | Consommateur `AssemblyName = Fx.Bookings.CancelBooking` (slice `Fx.Features.Bookings.CancelBooking`) → `ProjectReference` vers `AssemblyName = Fx.Bookings.BookRoom` (slice `Fx.Features.Bookings.BookRoom`) | `Fx.Bookings.CancelBooking`, `Fx.Bookings.BookRoom` | 1 × HS016 (Warning) dans le consommateur |
| X2 | Même consommateur → `PackageReference` vers un paquet local dont l'assembly garde `AssemblyName = Fx.Bookings.BookRoom` | idem | 1 × HS016 (Warning) |
| X3 | Consommateur → assembly leurre `AssemblyName = Decoy.Lib`, namespace `Fx.Features.Bookings.Decoy` | idem (sans `Decoy.Lib`) | 0 : hors périmètre malgré le namespace |
| X4 | Consommateur → assembly `AssemblyName = Fx.Bookings.Misc` (dans K2), type `Fx.Misc.Helper` (namespace non classifiable) | idem + `Fx.Bookings.Misc` | 1 × `SPKCFG` (Error) au site de référence dans le consommateur (la déclaration n'est pas dans sa compilation), jamais silencieux |
| X5 | Informations de symbole visibles depuis les **métadonnées** de `Fx.Bookings.BookRoom.dll`, par `ProjectReference` puis par paquet | — | Oracle X5 ci-dessous |

Types inspectés pour X5, déclarés dans le projet `Fx.Bookings.BookRoom` :
- `Fx.Features.Bookings.BookRoom.BookRoomPartial` : type partiel sur deux fichiers ;
- `Fx.Features.Bookings.BookRoom.BookRoomMarked` : porte `[BookRoomMarker]` et `[GeneratedCode("fixture", "1")]` ;
- `Fx.Features.Bookings.BookRoom.BookRoomGeneratedName` : déclaré dans un fichier `BookRoomGeneratedName.g.cs` ;
- `Fx.Features.Bookings.BookRoom.BookRoomAutoHeader` : déclaré dans un fichier qui commence par `// <auto-generated/>`.

**Oracle X5 (pré-enregistré, identique pour les deux modes)** :

| Information | Attendu |
|---|---|
| Namespace de chaque type | Disponible, égal au namespace source |
| `ContainingAssembly.Name` | `Fx.Bookings.BookRoom` |
| Attributs appliqués en source (`BookRoomMarker`, `GeneratedCode`) | Disponibles par `GetAttributes()` |
| Parties d'un type partiel | Non disponibles : un seul symbole, `DeclaringSyntaxReferences` vide |
| Chemins de fichiers | Non disponibles : toutes les `Locations` ont `IsInMetadata = true`, aucun chemin source |
| Marquage généré par nom (`*.g.cs`) ou en-tête `<auto-generated/>` | Perdu : rien dans les métadonnées ne distingue `BookRoomGeneratedName` ni `BookRoomAutoHeader` d'un type ordinaire |
| Marquage par attribut `[GeneratedCode]` | Disponible |

Un écart à cet oracle est consigné comme résultat mesuré, positif ou négatif, avec ses conséquences pour Q7.

| Étape | Commande | Classe |
|---|---|---|
| Q2-1 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/cross-assembly-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=cross-assembly "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif"` | `must_fail(1, { X1 : HS016 × 1, Warning ; X4 : SPKCFG × 1, Error ; X3 : aucun })` ; 0 AD0001 ; aucun autre diagnostic d'erreur |
| Q2-2 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/bookroom-pack.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_SPIKE/fixtures/cross-assembly/src/Fx.Bookings.BookRoom" -c Release -o "$HS_RUN/feed"` | `must_succeed` ; contrôle : `lib/net10.0/Fx.Bookings.BookRoom.dll` présent dans le `.nupkg` |
| Q2-3 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/cross-assembly-package-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental -p:UsePackage=true "-p:LocalFeed=$HS_RUN/feed" -p:HardSliceSpikeStep=cross-assembly-package "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif"` | `must_fail(1, { X2 : HS016 × 1, Warning ; X4 : SPKCFG × 1, Error ; X3 : aucun })` ; 0 AD0001 ; aucun autre diagnostic d'erreur |
| Q2-4 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/bench-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release -o "$HS_RUN/bin/bench"` | `must_succeed` |
| Q2-5 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/x5-hashes.log" -- shasum -a 256 "$HS_SPIKE/fixtures/cross-assembly/src/Fx.Bookings.BookRoom/bin/Debug/net10.0/Fx.Bookings.BookRoom.dll" "$HS_RUN/nuget/packages/fx.bookings.bookroom/1.0.0-spike/lib/net10.0/Fx.Bookings.BookRoom.dll"` | `must_succeed` ; les deux empreintes sont reportées dans la fiche |
| Q2-6 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/x5-projectref.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" inspect-symbols --reference "$HS_SPIKE/fixtures/cross-assembly/src/Fx.Bookings.BookRoom/bin/Debug/net10.0/Fx.Bookings.BookRoom.dll" --types Fx.Features.Bookings.BookRoom.BookRoomPartial,Fx.Features.Bookings.BookRoom.BookRoomMarked,Fx.Features.Bookings.BookRoom.BookRoomGeneratedName,Fx.Features.Bookings.BookRoom.BookRoomAutoHeader --out "$HS_RUN/x5-projectref.json"` | `must_succeed` ; oracle X5 |
| Q2-7 | Même commande avec `--reference "$HS_RUN/nuget/packages/fx.bookings.bookroom/1.0.0-spike/lib/net10.0/Fx.Bookings.BookRoom.dll"`, log `x5-package.log`, sortie `x5-package.json` | `must_succeed` ; oracle X5 |

`inspect-symbols` crée une compilation minimale qui ne référence que l'assembly indiquée et les assemblies de référence du framework, puis relève, pour chaque type demandé : namespace, assembly, attributs, nombre de `DeclaringSyntaxReferences`, `Locations` (`IsInMetadata`, chemin éventuel). C'est exactement l'information de métadonnées dont dispose l'analyseur dans la compilation du consommateur. Le chemin du `.dll` sous `bin/Debug/net10.0/` est celui de la sortie de Q2-1 ; la version de paquet `1.0.0-spike` est fixée par le projet de la fixture.

**Preuve** : `results/<RUN_ID>/cross-assembly.md` contient, pour X1 à X4, les diagnostics observés (extraits normalisés des SARIF) face à l'oracle, et pour X5, la table « information → attendu → observé (ProjectReference) → observé (paquet) », avec les empreintes des deux `.dll`.

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

Commande CLI par état : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/cli-<état>-<sans|warnaserror>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/ide/states/<état>" -nologo --no-incremental -p:ReportAnalyzer=true "-p:LocalFeed=$HS_RUN/feed" -p:HardSliceSpikeStep=cli-<état>-<sans|warnaserror> "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif" [-warnaserror]`.

#### 6.4 Q4 — `.globalconfig` par niveau et précédence (états commités `fixtures/precedence/case-a … case-h`)

Paquet local : `build/` et `buildTransitive/` contiennent un `.props`/`.targets` qui ajoute `HardSlice.Analyzers.level-1.globalconfig` à `GlobalAnalyzerConfigFiles` quand `HardSliceAnalysisLevel` vaut 1. Chaque cas contient exactement une violation F01.

Pack (`must_succeed`) : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/pack.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_SPIKE/src/HardSlice.Spike.Package" -c Release -o "$HS_RUN/feed"` ; empreinte `shasum -a 256` du `.nupkg` dans la fiche.

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

Commande par cas : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/precedence-<x>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/precedence/case-<x>" -nologo --no-incremental "-p:LocalFeed=$HS_RUN/feed" -p:HardSliceSpikeStep=precedence-<x> "-p:HardSliceSpikeSarifDir=$HS_RUN/sarif" [options du cas]`.

#### 6.5 Q5 — Performance sur nopCommerce (D-06, M-08, M-08-A1)

Exécution dans le **compte dédié** (§3.4), lancée par le mainteneur après OP-01, par le script de série versionné qui enchaîne exactement les étapes ci-dessous. Conditions M-08 consignées par le mainteneur. Serveur de compilation laissé actif (défaut, consigné).

##### 6.5.1 Prologue et contrôles communs Q1–Q6 ; bootstrap supplémentaire Q5/Q6

**P0 — prologue exact, commun à toutes les séries (Q1 à Q6), exécuté avant B0** dans le compte qui exécute la série. Il sera versionné tel quel dans `tools/series-prologue.sh`, puis il appelle le corps de la série (`tools/series-<qn>.sh`, écrit avec l'échafaudage après V-S1-01). `HS_REPO`, `HS_ART`, `SERIES` et `ATTEMPT` sont fournis au lancement ; P0 génère `RUN_ID`, exporte le spike exact du commit mesuré sous `$HS_RUN`, puis écrit toutes les expansions dans le manifeste privé.

**Commande de lancement obligatoire** (valeurs absolues remplacées par le mainteneur). Le prologue n'est jamais invoqué directement depuis le shell ambiant : le premier Bash lui-même démarre sous `env -i`, sans `BASH_ENV`, fonctions exportées ni options héritées.

```bash
/usr/bin/env -i \
  HOME="/Users/<compte>" PATH=/usr/bin:/bin:/usr/sbin:/sbin LANG=C LC_ALL=C \
  HS_REPO="/chemin/canonique/hardslice" \
  HS_ART="/Users/<compte>/hardslice-artifacts" SERIES=q1 ATTEMPT=01 \
  /bin/bash --noprofile --norc -c \
  'exec /bin/bash --noprofile --norc "$HS_REPO/spikes/s1-roslyn/tools/series-prologue.sh"'
```

```bash
umask 077
set -u
: "${HOME:?}" "${HS_REPO:?}" "${HS_ART:?}" "${ATTEMPT:?}" "${SERIES:?}"
case "$ATTEMPT" in [0-9][0-9]) ;; *) echo "ATTEMPT invalide" >&2; exit 70 ;; esac
case "$SERIES" in q1|q2|q3|q4|q5|q6) ;; *) echo "SERIES invalide" >&2; exit 70 ;; esac
for p in "$HOME" "$HS_REPO" "$HS_ART"; do
  [ "$(cd -- "$p" && pwd -P)" = "$p" ] || { echo "chemin non canonique : $p" >&2; exit 70; }
done
case "$HS_ART" in "$HOME"/*) ;; *) echo "HS_ART doit être un sous-dossier dédié de HOME" >&2; exit 70 ;; esac
[ "$(/usr/bin/stat -f '%Lp' "$HS_ART")" = 700 ] \
  && [ "$(/usr/bin/stat -f '%u' "$HS_ART")" = "$(/usr/bin/id -u)" ] \
  || { echo "HS_ART doit appartenir au compte courant et être en 0700" >&2; exit 70; }
hs_art_acl=$(/bin/ls -lde "$HS_ART") || { echo "ACL de HS_ART illisible" >&2; exit 70; }
if printf '%s\n' "$hs_art_acl" | /usr/bin/awk 'NR > 1 && / allow / { found = 1 } END { exit found ? 0 : 1 }'; then
  echo "HS_ART porte une ACL permissive" >&2
  exit 70
fi
clean_git() {
  /usr/bin/env -i \
    HOME="$HOME" PATH=/usr/bin:/bin:/usr/sbin:/sbin LANG=C LC_ALL=C \
    GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_TERMINAL_PROMPT=0 \
    /usr/bin/git "$@"
}
repo_top=$(clean_git -C "$HS_REPO" rev-parse --show-toplevel) \
  || { echo "racine Git illisible" >&2; exit 70; }
[ "$repo_top" = "$HS_REPO" ] || { echo "HS_REPO n'est pas la racine du worktree" >&2; exit 70; }
repo_head=$(clean_git -C "$HS_REPO" rev-parse --verify 'HEAD^{commit}') \
  || { echo "HEAD illisible" >&2; exit 70; }
repo_short=$(clean_git -C "$HS_REPO" rev-parse --short=12 "$repo_head") \
  || { echo "SHA court illisible" >&2; exit 70; }
repo_status=$(clean_git -C "$HS_REPO" status --porcelain=v1 --untracked-files=all) \
  || { echo "statut Git illisible" >&2; exit 70; }
[ -z "$repo_status" ] || { echo "worktree HardSlice non propre" >&2; exit 70; }
question=$(printf '%s' "$SERIES" | /usr/bin/tr '[:lower:]' '[:upper:]')
run_stamp=$(/bin/date -u +%Y%m%dT%H%M%SZ) || { echo "heure UTC illisible" >&2; exit 70; }
uuid_raw=$(/usr/bin/uuidgen) || { echo "UUID impossible" >&2; exit 70; }
run_nonce=$(printf '%s' "$uuid_raw" | /usr/bin/tr -d '-' | /usr/bin/tr '[:upper:]' '[:lower:]')
[ "${#run_nonce}" -eq 32 ] || { echo "UUID invalide" >&2; exit 70; }
case "$run_nonce" in *[!0-9a-f]*) echo "UUID invalide" >&2; exit 70 ;; esac
RUN_ID=${run_stamp}-${question}-${repo_short}-a${ATTEMPT}-u${run_nonce}
git_probe=$(clean_git -C "$HS_ART" rev-parse --git-dir 2>&1)
git_probe_status=$?
if [ "$git_probe_status" -eq 0 ]; then
  echo "HS_ART se trouve dans un dépôt ou worktree Git" >&2
  exit 70
fi
expected_git_outside='fatal: not a git repository (or any of the parent directories): .git'
[ "$git_probe_status" -eq 128 ] && [ "$git_probe" = "$expected_git_outside" ] \
  || { echo "impossible de prouver que HS_ART est hors Git : $git_probe" >&2; exit 70; }
run_registry=$HS_ART/.run-ids
if [ ! -e "$run_registry" ] && [ ! -L "$run_registry" ]; then
  /bin/mkdir -m 0700 -- "$run_registry" || exit 70
fi
[ -d "$run_registry" ] && [ ! -L "$run_registry" ] \
  && [ "$(cd -- "$run_registry" && pwd -P)" = "$run_registry" ] \
  || { echo "registre RUN_ID invalide" >&2; exit 70; }
[ "$(/usr/bin/stat -f '%Lp' "$run_registry")" = 700 ] \
  && [ "$(/usr/bin/stat -f '%u' "$run_registry")" = "$(/usr/bin/id -u)" ] \
  || { echo "permissions du registre RUN_ID invalides" >&2; exit 70; }
/bin/mkdir -m 0700 -- "$run_registry/$RUN_ID" || { echo "RUN_ID déjà utilisé" >&2; exit 70; }
HS_RUN=$HS_ART/$RUN_ID
HS_CLONES=$HS_RUN/clones
/bin/mkdir -m 0700 -- "$HS_RUN" || exit 70
for d in tmp dotnet-home nuget nuget/packages nuget/http-cache nuget/plugins-cache bin staging sarif clones work; do
  /bin/mkdir -m 0700 -- "$HS_RUN/$d" || exit 70
done
[ "$(cd -- "$HS_RUN" && pwd -P)" = "$HS_RUN" ] || exit 70
spike_archive=$HS_RUN/staging/spike-source.tar
clean_git -C "$HS_REPO" archive --format=tar "$repo_head" spikes/s1-roslyn > "$spike_archive" \
  || { echo "export du spike impossible" >&2; exit 70; }
/usr/bin/tar -xf "$spike_archive" -C "$HS_RUN/work" \
  || { echo "extraction du spike impossible" >&2; exit 70; }
HS_SPIKE=$HS_RUN/work/spikes/s1-roslyn
[ -d "$HS_SPIKE" ] && [ "$(cd -- "$HS_SPIKE" && pwd -P)" = "$HS_SPIKE" ] \
  || { echo "export du spike invalide" >&2; exit 70; }
if ! symlinks=$(/usr/bin/find "$HS_RUN" -type l -print); then
  echo "parcours des liens symboliques impossible" >&2
  exit 70
fi
[ -z "$symlinks" ] || { printf 'liens symboliques interdits :\n%s\n' "$symlinks" >&2; exit 70; }
spike_archive_sha=$(/usr/bin/shasum -a 256 "$spike_archive") \
  || { echo "empreinte de l'export impossible" >&2; exit 70; }
spike_archive_sha=${spike_archive_sha%% *}
( set -o noclobber
  { printf '# manifest.private.md - hors git\n'
    printf 'RUN_ID=%s\nSERIES=%s\nATTEMPT=%s\nstart_utc=%s\naccount=%s\nhost=%s\n' "$RUN_ID" "$SERIES" "$ATTEMPT" "$(/bin/date -u +%Y-%m-%dT%H:%M:%SZ)" "$(/usr/bin/id -un)" "$(/usr/bin/uname -a)"
    printf 'HS_REPO=%s\nHS_ART=%s\nHS_RUN=%s\nHS_CLONES=%s\nHS_SPIKE=%s\n' "$HS_REPO" "$HS_ART" "$HS_RUN" "$HS_CLONES" "$HS_SPIKE"
    printf 'HS_REPO_HEAD=%s\nSPIKE_ARCHIVE_SHA256=%s\n' "$repo_head" "$spike_archive_sha"
  } > "$HS_RUN/manifest.private.md" ) || exit 70
exec /usr/bin/env -i \
  HOME="$HOME" PATH=/usr/bin:/bin:/usr/sbin:/sbin TMPDIR="$HS_RUN/tmp" \
  LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 DOTNET_CLI_UI_LANGUAGE=en-US \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_CLI_HOME="$HS_RUN/dotnet-home" \
  NUGET_PACKAGES="$HS_RUN/nuget/packages" NUGET_HTTP_CACHE_PATH="$HS_RUN/nuget/http-cache" \
  NUGET_PLUGINS_CACHE_PATH="$HS_RUN/nuget/plugins-cache" \
  MSBuildSDKsPath=/usr/local/share/dotnet/sdk/10.0.102/Sdks \
  GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_TERMINAL_PROMPT=0 \
  HS_REPO="$HS_REPO" HS_ART="$HS_ART" HS_RUN="$HS_RUN" HS_CLONES="$HS_CLONES" HS_SPIKE="$HS_SPIKE" RUN_ID="$RUN_ID" \
  /bin/bash "$HS_SPIKE/tools/series-$SERIES.sh"
```

Toute sortie non nulle de P0 est une panne d'infrastructure : aucune étape métier ne s'exécute. Dès que `$HS_ART/.run-ids/<RUN_ID>/` a été créé, la réservation reste conservée même si `$HS_RUN` ou l'export n'a pas pu être créé ; la tentative est publiée comme invalide. Le registre `.run-ids` est préservé par la fin de série OP-01. L'archive source, son empreinte et tous les `bin/obj` produits depuis `$HS_SPIKE` restent sous `$HS_RUN` : aucune compilation ne travaille dans `$HS_REPO`.

**Contrôles communs Q1 à Q6, après P0** :

| Étape | Commande | Classe |
|---|---|---|
| B0 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b0-sdk-version.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` | `must_succeed` ; corps = `10.0.102` |
| B0a | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b0a-environment.log" -- /usr/bin/env` | `must_succeed` ; corps = exactement les variables de la liste blanche (§3.4), plus `PWD`, `SHLVL` et `_` posées par bash |
| B1 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b1-wrapper-tests.log" -- "$HS_SPIKE/tools/test-run-logged.sh"` | `must_succeed` ; dernière ligne du corps `passed=N failed=0` |

**Bootstrap supplémentaire avec réseau limité, avant la coupure — Q5 et Q6 seulement**, chacun dans son propre `$HS_RUN`. **Q6 est autonome**, que Q5 ait été exécutée ou non.

| Étape | Commande | Classe |
|---|---|---|
| B2 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b2-analyzer-restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_SPIKE/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj"` | `must_succeed` |
| B3 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b3-analyzer-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj" -c Release --no-restore -o "$HS_RUN/bin/analyzer"` | `must_succeed` |
| B4 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b4-bench-restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj"` | `must_succeed` |
| B5 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b5-bench-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release --no-restore -o "$HS_RUN/bin/bench"` | `must_succeed` |
| B6 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b6-binaries-sha256.log" -- shasum -a 256 "$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll" "$HS_RUN"/bin/bench/*.dll "$HS_RUN/bin/bench/HardSlice.Spike.Bench.runtimeconfig.json" "$HS_RUN/bin/bench/HardSlice.Spike.Bench.deps.json"` | `must_succeed` ; empreintes reportées dans le manifeste et la fiche |
| B7–B9 | Sous-séquence **N** (nopCommerce, Q5) ou **F** (FSH, Q6), ci-dessous | voir la sous-séquence |
| B10 | Restore de la solution externe : §6.5.2 (nopCommerce) ou §6.6 (FSH) | voir la section |
| B11 | **Coupure réseau** par le mainteneur selon le plan OP-01, heure consignée | — |

**Sous-séquence N — nopCommerce** (`https://github.com/nopSolutions/nopCommerce.git` @ `221305873bc81c47981b2e510b2e9bed7e5b2076`) :

| Étape | Commande | Classe |
|---|---|---|
| N7a | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n7a-git-init.log" -- /usr/bin/git init --quiet "$HS_CLONES/nopCommerce"` | `must_succeed` |
| N7b | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n7b-git-remote.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" remote add origin https://github.com/nopSolutions/nopCommerce.git` | `must_succeed` |
| N7c | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n7c-git-fetch.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" fetch --depth 1 origin 221305873bc81c47981b2e510b2e9bed7e5b2076` | `must_succeed` |
| N7d | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n7d-git-checkout.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" checkout --quiet --detach FETCH_HEAD` | `must_succeed` |
| N8a | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n8a-head.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" rev-parse HEAD` | `must_succeed` ; corps = `221305873bc81c47981b2e510b2e9bed7e5b2076` |
| N8b | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n8b-origin.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" remote get-url origin` | `must_succeed` ; corps = `https://github.com/nopSolutions/nopCommerce.git` |
| N8c | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n8c-status.log" -- /usr/bin/git -C "$HS_CLONES/nopCommerce" status --porcelain` | `must_succeed` ; corps vide |
| N9 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/n9-external-sdk.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild "$HS_CLONES/nopCommerce/src/Libraries/Nop.Core/Nop.Core.csproj" -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath` | `must_succeed` ; `NETCoreSdkVersion` = `10.0.102`, `RoslynTargetsPath` commence par `/usr/local/share/dotnet/sdk/10.0.102/` |

**Sous-séquence F — FSH** (`https://github.com/fullstackhero/dotnet-starter-kit.git` @ `a967537526f3b769c7d268da3ed2a6920af65ce9`) :

| Étape | Commande | Classe |
|---|---|---|
| F7a | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f7a-git-init.log" -- /usr/bin/git init --quiet "$HS_CLONES/dotnet-starter-kit"` | `must_succeed` |
| F7b | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f7b-git-remote.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" remote add origin https://github.com/fullstackhero/dotnet-starter-kit.git` | `must_succeed` |
| F7c | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f7c-git-fetch.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" fetch --depth 1 origin a967537526f3b769c7d268da3ed2a6920af65ce9` | `must_succeed` |
| F7d | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f7d-git-checkout.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" checkout --quiet --detach FETCH_HEAD` | `must_succeed` |
| F8a | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f8a-head.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" rev-parse HEAD` | `must_succeed` ; corps = `a967537526f3b769c7d268da3ed2a6920af65ce9` |
| F8b | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f8b-origin.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" remote get-url origin` | `must_succeed` ; corps = `https://github.com/fullstackhero/dotnet-starter-kit.git` |
| F8c | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f8c-status.log" -- /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" status --porcelain` | `must_succeed` ; corps vide |
| F9 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/f9-external-sdk.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild "$HS_CLONES/dotnet-starter-kit/src/Modules/Catalog/Modules.Catalog.Contracts/Modules.Catalog.Contracts.csproj" -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath` | `must_succeed` ; `NETCoreSdkVersion` = `10.0.102`, `RoslynTargetsPath` commence par `/usr/local/share/dotnet/sdk/10.0.102/` |

Les mêmes vérifications N8 et F8 sont rejouées en fin de série (étapes `N8a′` à `N8c′`, `F8a′` à `F8c′`, logs suffixés `-end`). Après B11, aucune commande ne restaure ni ne compile le prototype : les builds externes utilisent `--no-restore`, et l'hôte est lancé depuis `$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll`. Les targets d'injection reçoivent `-p:HardSliceSpikeAnalyzerPath=$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll`.

**Fin de série** : la procédure OP-01 « fin de série » (arrêt des processus du compte dédié, inventaire, remise à zéro) est exécutée et attestée par le mainteneur après le rangement des sorties. L'agent n'exécute aucune opération système.

##### 6.5.2 Restore, injection et classification pour nopCommerce

**Injection sans modifier la solution.** `"-p:CustomAfterMicrosoftCommonTargets=$HS_SPIKE/inject/HardSlice.Spike.Inject.targets"` :
- le fichier ajoute `<CompilerVisibleProperty Include="NETCoreSdkVersion" />` ; il ajoute le `.globalconfig` désigné par `HardSliceSpikeClassification` quand cette propriété est définie ;
- il n'ajoute l'analyseur `$(HardSliceSpikeAnalyzerPath)` que si `HardSliceSpikeInjectAnalyzer=true` ;
- les builds de référence passent par la même injection avec `HardSliceSpikeInjectAnalyzer=false`, de sorte que seul l'analyseur diffère ;
- contrôle : l'analyseur figure dans la sortie `ReportAnalyzer` de chaque projet C# de chaque build `H`, et d'aucun build `R`. Sinon la tentative est invalide.

B10 (`must_succeed`) : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/b10-restore.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/nopCommerce/src/NopCommerce.sln" "-p:CustomAfterMicrosoftCommonTargets=$HS_SPIKE/inject/HardSlice.Spike.Inject.targets"`.

**Classification « pire cas », fermée, produite de façon déterministe après B11.** `SPKCFG` étant bloquant, une classification incomplète ferait échouer les builds `H` ; elle est donc générée par un algorithme versionné et pré-enregistré, puis hachée avant toute mesure :
- K2 = `AssemblyName` de tous les projets C# de `NopCommerce.sln` ; K6 vide ;
- K3 = `{ Nop, 2 }` : chaque namespace `Nop.<A>.<B>` et ses descendants forment une slice ;
- K5 = `{ Nop, exact }` et `{ Nop.<A>, exact }` pour chaque namespace de ce niveau qui déclare des types ; `{ <global>, exact }` si le namespace global déclare des types ; `{ <X>, subtree }` pour chaque namespace racine `<X>` autre que `Nop` qui déclare des types.

Elle maximise les opportunités et les diagnostics HS016 créés, et laisse **0 `SPKCFG`** attendu. Les diagnostics HS016 émis sur nopCommerce ne sont **pas** triés (la solution n'est pas organisée en slices) ; ils sont comptés.

| Étape | Commande | Classe |
|---|---|---|
| C1 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/c1-classify.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" classify-worst-case --solution "$HS_CLONES/nopCommerce/src/NopCommerce.sln" --inject "$HS_SPIKE/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --root Nop --depth 2 --evidence-prefix c1 --out "$HS_RUN/staging/nopcommerce.globalconfig"` | `must_succeed` ; mêmes contrôles de chargement que §6.5.4, points 1 à 6 |
| C2 | `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/c2-classification-sha256.log" -- shasum -a 256 "$HS_RUN/staging/nopcommerce.globalconfig"` | `must_succeed` ; empreinte reportée dans le manifeste et la fiche avant tout build mesuré |

##### 6.5.3 (1) Part de HS016 dans la compilation — budget ≤ 5 %

1. Build `<k>-<X>` (`must_succeed` ; un `SPKCFG` ferait échouer le build `H`, donc la tentative), avec `<X>` = `R` (`<inj>` = `false`) ou `H` (`<inj>` = `true`) :
   `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/build-<k>-<X>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/nopCommerce/src/NopCommerce.sln" -c Release --no-restore --no-incremental -nologo -p:ReportAnalyzer=true "-p:CustomAfterMicrosoftCommonTargets=$HS_SPIKE/inject/HardSlice.Spike.Inject.targets" "-p:HardSliceSpikeAnalyzerPath=$HS_RUN/bin/analyzer/HardSlice.Spike.Analyzer.dll" "-p:HardSliceSpikeClassification=$HS_RUN/staging/nopcommerce.globalconfig" -p:HardSliceSpikeInjectAnalyzer=<inj> "-bl:$HS_RUN/build-<k>-<X>.binlog" "-flp:logfile=$HS_RUN/build-<k>-<X>.msbuild.log;verbosity=detailed;performancesummary"`
2. **Warm-up non mesuré** : `W1 = R`, puis `W2 = H`. Sorties conservées, exclues du calcul.
3. **Ordre mesuré, fixé et publié avant le run** (contrebalancé ABBA) : `1 R, 2 H, 3 H, 4 R, 5 R, 6 H, 7 H, 8 R, 9 R, 10 H`, soit 5 `R` et 5 `H`. Un build en échec invalide toute la série, qui est rejouée en entier dans le même ordre sous un nouveau `<RUN_ID>` ; la tentative invalide reste publiée.
4. Ratio par build `H` = Σ sur les projets du temps d'exécution de l'analyseur HS016 (lignes `ReportAnalyzer`) / Σ de la durée de la tâche `Csc` (résumé de performance). Verdict sur la **médiane des 5 ratios**. Dispersion publiée selon les conventions du §6.5.5. Temps murs `R` et `H` publiés de même, à titre informatif.

##### 6.5.4 (2) Temps de HS016 par document sur une analyse complète — budget ≤ 20 ms

**Méthode retirée.** La version précédente mesurait chaque arbre par `GetAnalyzerSyntaxDiagnosticsAsync` / `GetAnalyzerSemanticDiagnosticsAsync` avec un `CompilationWithAnalyzers` réutilisé, censé amortir le démarrage. Le mainteneur a signalé à la revue (A-05) que, dans Microsoft.CodeAnalysis 5.0.0, chaque appel restreint à un fichier clone la compilation et initialise un nouvel `AnalyzerDriver`, de sorte que `CompilationStartAction` est repayée à chaque arbre. L'agent n'a pas revérifié ce point dans les sources de Roslyn ; il est retenu comme contrainte du protocole. En conséquence, sont supprimés :
- la boucle d'analyse par arbre ;
- l'affirmation d'amortissement ;
- le contrôle `Σ d[t, r] = total` ;
- la borne par document neuf ;
- tout verdict fondé sur une distribution issue d'appels par arbre.

Commande (réseau coupé, `must_succeed`) : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/perf.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" perf --solution "$HS_CLONES/nopCommerce/src/NopCommerce.sln" --inject "$HS_SPIKE/inject/HardSlice.Spike.Inject.targets" --classification "$HS_RUN/staging/nopcommerce.globalconfig" --sdk /usr/local/share/dotnet/sdk/10.0.102 --warmup 2 --reps 10 --evidence-prefix perf --out "$HS_RUN/perf.csv"`.

Chargement :
1. `MSBuildLocator.RegisterMSBuildPath("/usr/local/share/dotnet/sdk/10.0.102")` avant tout chargement MSBuild ; `MSBuildSDKsPath` vient de l'environnement en liste blanche.
2. `MSBuildWorkspace.Create(properties)` avec `Configuration=Release`, `TargetFramework=net10.0`, `CustomAfterMicrosoftCommonTargets=$HS_SPIKE/inject/HardSlice.Spike.Inject.targets`, `HardSliceSpikeClassification=$HS_RUN/staging/nopcommerce.globalconfig`, `HardSliceSpikeInjectAnalyzer=false` ; `await workspace.OpenSolutionAsync(sln)`.
3. **Échec immédiat (code 2, aucune mesure)** si `workspace.Diagnostics` contient au moins une entrée, quel que soit son `WorkspaceDiagnosticKind` ; toutes les entrées sont écrites dans la sortie. Un assouplissement exigerait une nouvelle validation explicite.
4. **Contrôle du SDK effectif** : pour chaque projet, `project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.NETCoreSdkVersion", out v)` doit donner `10.0.102` ; sinon code 2.
5. **Contrôle des documents, sans faux positif sur les fichiers générés par le SDK** : les commandes `classify-worst-case`, `perf` et `exposure` exigent `--evidence-prefix <préfixe>`, validé par l'hôte avec `[a-z0-9-]+`. Pour chaque projet, la commande `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/<préfixe>-compile-items-<projet>.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <csproj> -getItem:Compile -getProperty:BaseIntermediateOutputPath -p:Configuration=Release -p:TargetFramework=net10.0 "-p:CustomAfterMicrosoftCommonTargets=$HS_SPIKE/inject/HardSlice.Spike.Inject.targets"` (`must_succeed`) produit les entrées évaluées et la racine intermédiaire dans le même TFM que `MSBuildWorkspace`. Les préfixes sont uniques dans une tentative (`forms`, `forms-broken`, `c1`, `perf`, `exposure-fsh`, et `exposure-fdmm` si l'exploratoire est lancé) : aucun log exclusif n'est réutilisé. Après normalisation :
   - chaque élément `Compile` évalué doit correspondre à un `project.Document` ; sinon code 2 ;
   - un `project.Document` absent de `Compile` n'est admis que si son chemin se trouve sous le `BaseIntermediateOutputPath` évalué du projet ; ces documents générés par le build (par exemple `AssemblyInfo` ou `GlobalUsings`) sont listés séparément ; tout autre document donne le code 2 ;
   - les arbres obtenus respectivement par `GetSyntaxTreeAsync()` sur tous les `project.Documents` et sur tous les `project.GetSourceGeneratedDocumentsAsync()` doivent former, par identité de référence et sans doublon, exactement l'ensemble de `compilation.SyntaxTrees` ; sinon code 2.
6. Un projet multi-cible est chargé pour `net10.0` seulement ; un projet sans `net10.0` donne le code 2 (aucun dans `NopCommerce.sln` à la présélection).

**Mesure primaire** :
1. **Compilation fixe** : `await project.GetCompilationAsync()` une fois par projet, réutilisée pour toutes les répétitions.
2. **Deux warm-ups complets**, non mesurés, identiques à une répétition.
3. **Dix répétitions mesurées** `r = 1 … 10`. Pour chaque répétition et chaque projet `p` (ordre ordinal des noms de projet), un **nouveau** `CompilationWithAnalyzers` et **un seul appel d'analyse complète** :
   ```csharp
   var options = new CompilationWithAnalyzersOptions(
       project.AnalyzerOptions, onAnalyzerException: Fail,
       concurrentAnalysis: false, logAnalyzerExecutionTime: true,
       reportSuppressedDiagnostics: false);
   var cwa = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(hs016), options);
   var sw = Stopwatch.StartNew();
   var result = await cwa.GetAnalysisResultAsync(ct);
   sw.Stop();
   var t = result.AnalyzerTelemetryInfo[hs016].ExecutionTime;   // temps total de HS016 pour p
   var n = compilation.SyntaxTrees.Count();                        // documents analysés de p
   ```
4. `T_r = Σ_p t(p, r)` ; `N = Σ_p n(p)`, constant d'une répétition à l'autre ; **`m_r = T_r / N`**, en millisecondes par document.
5. **Verdict** : **médiane des dix `m_r` ≤ 20 ms par document.**
6. **Contrôles internes (code 4 si l'un échoue)** :
   - `N` égal au total du point 5 du chargement ;
   - diagnostics identiques entre les dix répétitions, comme multiensemble de (ID, chemin, début, fin, message).

   Une exception de l'analyseur (`Fail`) donne le code 3. Le temps mural de chaque appel (`sw`) est publié à titre informatif.
7. **Oracle après retour 0 de l'hôte** : le script de série exige 0 `SPKCFG`, puisque la classification est fermée. Tout `SPKCFG` invalide la tentative et interdit toute conclusion de performance, mais ne transforme pas le diagnostic en panne interne de l'hôte (§3.5).

**Distribution secondaire facultative (sans verdict).** Si elle est exécutée, le prototype tourne dans un mode instrumenté (`--instrument-trees`) : il chronomètre le corps de chacun de ses callbacks et l'impute à l'arbre syntaxique concerné ; le temps des callbacks de démarrage de compilation est publié à part, sans imputation. Cette distribution par document (médiane, Q1, Q3, écart interquartile, p95, maximum) est publiée sans verdict. Son **overhead** est mesuré par dix répétitions instrumentées après la mesure primaire : `overhead = médiane(m_r instrumentés) / médiane(m_r primaires) − 1`, publié avec elle.

##### 6.5.5 Conventions statistiques

Toutes les valeurs sont en millisecondes, sauf les ratios.
- Pour une série triée par ordre croissant `x(1) ≤ … ≤ x(n)`, le quantile d'ordre `p` est calculé par **interpolation linéaire, type 7 de Hyndman et Fan** (méthode par défaut de R `quantile` et de NumPy `percentile`, équivalente à `QUARTILE.INC` d'Excel) : `h = (n − 1) × p + 1`, `Q(p) = x(⌊h⌋) + (h − ⌊h⌋) × (x(⌊h⌋ + 1) − x(⌊h⌋))`, avec `Q(p) = x(n)` si `⌊h⌋ = n`.
- **Médiane** = `Q(0,5)`. Pour 10 répétitions, c'est la moyenne des 5ᵉ et 6ᵉ valeurs ; pour 5 ratios, la 3ᵉ valeur.
- **Q1** = `Q(0,25)`, **Q3** = `Q(0,75)`, **écart interquartile** = Q3 − Q1. Pour 5 ratios : Q1 = 2ᵉ valeur, Q3 = 4ᵉ valeur.
- **p95** (distribution secondaire par document seulement) = rang le plus proche : valeur de rang `⌈0,95 × N⌉` dans les valeurs triées.
- **Minimum, maximum** = plus petite et plus grande valeur.
- Publiés pour Q5 (1) : médiane, Q1, Q3, écart interquartile, min et max des 5 ratios et des temps murs `R` et `H`. Pour Q5 (2) : médiane, Q1, Q3, écart interquartile, min et max des 10 `m_r` et des temps murs ; distribution secondaire si elle est exécutée.

#### 6.6 Q6 — Exposition (FSH obligatoire ; fdmm exploratoire)

**Préalable** : Q2 conforme pour X1 et X2 avec le même code d'analyseur (même commit HardSlice, ou `git diff` vide sur `spikes/s1-roslyn/src/HardSlice.Spike.Analyzer` entre les deux commits). Sinon Q6 est `non mesuré`.

Exécution dans le **compte dédié** (§3.4), lancée par le mainteneur après OP-01, sous son propre `<RUN_ID>`. Elle commence par le prologue P0 puis le bootstrap complet du §6.5.1 avec la **sous-séquence F** (`https://github.com/fullstackhero/dotnet-starter-kit.git` @ `a967537526f3b769c7d268da3ed2a6920af65ce9`, dans `$HS_CLONES/dotnet-starter-kit`). Elle ne réutilise aucun binaire ni aucune sortie de Q5.

**Restore et audit (B10 pour FSH).**
1. Audit **informatif**, réseau limité : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/fsh-restore-audit.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx"`. Classe `informative` : son code non nul n'interrompt pas la série. Le code et les avis NU1902/NU1903 (paquet, version, identifiant d'avis) sont publiés dans la fiche.
2. Restore **mesuré** (`must_succeed`), réseau limité : même commande avec `-p:NuGetAudit=false`, log `fsh-restore.log`. **Cette option sert uniquement à éviter que les avis de vulnérabilité soient promus en erreur** par la configuration du dépôt (constaté à la présélection). Elle désactive l'audit pendant le restore, sans changer ni les paquets résolus ni les entrées de la compilation. L'audit de l'étape 1 reste publié.
3. B11 : coupure du réseau.
4. Build de contrôle (`must_succeed`) : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/fsh-build.log" -- /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" -c Release -nologo --no-restore --no-incremental -p:NuGetAudit=false`.

**Configuration de classification FSH**, écrite, commitée et hachée **avant** le run (`config/fsh.globalconfig`), selon ces principes :
- K2 = `AssemblyName` des projets `Modules.<M>` et `Modules.<M>.Contracts` des 10 modules. Les projets de tests, `Architecture.Tests` et les outils ne sont pas dans K2, donc hors périmètre ; K6 est vide.
- K3 : une entrée par module, `{ FSH.Modules.<M>.Features.v1, depth }`. `depth` = 2 pour les modules qui ont un niveau `Area` (Identity, Catalog, Chat, Billing, Tickets), 1 pour les autres (Auditing, Files, Multitenancy, Notifications, Webhooks). À confirmer par l'inventaire des namespaces fait au moment d'écrire la configuration, et publié avec elle.
- K5 : `{ FSH.Modules.<M>.Contracts, subtree }` (contrat public du module) ; `{ FSH.Modules.<M>.Domain, subtree }` ; `{ FSH.Modules.<M>.Features.v1.Internal, subtree }` ; la racine de composition et les namespaces transverses identifiés dans l'inventaire.
- Toute entrée ajoutée ou modifiée après le run donne une nouvelle tentative ; toutes sont publiées.

Exposition (réseau coupé, `must_succeed`) : `"$HS_SPIKE/tools/run-logged.sh" "$HS_RUN/exposure-fsh.log" -- /usr/local/share/dotnet/dotnet "$HS_RUN/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" --inject "$HS_SPIKE/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_SPIKE/config/fsh.globalconfig" --evidence-prefix exposure-fsh --out "$HS_RUN/exposure-fsh.csv"`. Mêmes contrôles de chargement que Q5 (2), points 1 à 6.

Sorties : opportunités évaluées (`SPKOPP`), ventilées en même slice / autre slice / frontière autorisée / non classifiable ; diagnostics HS016 et `SPKCFG` émis ; nombres bruts. L'hôte sort en 0 dès que l'analyse est complète, quel que soit le nombre de `SPKCFG` : un `SPKCFG` est un diagnostic à trier, jamais une panne du programme (§3.5). Le build de contrôle de l'étape 4 n'injecte pas l'analyseur ; il ne peut donc pas échouer à cause d'un `SPKCFG`.

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

**fdmm** (`https://github.com/mehdihadeli/food-delivery-modular-monolith` @ `cb7c30c803a293d2257e063f978c80afa62fd8f1`) est **exploratoire**. Il n'est lancé que si le triage FSH est intégralement terminé et si la timebox le permet, dans les mêmes conditions d'isolation et avec son propre bootstrap ; son éventuelle commande `exposure` utilise le préfixe unique `--evidence-prefix exposure-fdmm`. Il n'est jamais échantillonné et n'entre dans aucun critère de porte ni dans la conclusion D-S1. Son statut publié est l'un des trois suivants :

| Statut fdmm | Définition |
|---|---|
| **non exécuté** | Aucun `<RUN_ID>` fdmm n'a été créé |
| **commencé, exploratoire incomplet** | Un `<RUN_ID>` existe, mais le run a échoué, a été interrompu, ou le triage n'est pas intégral ; les sorties sont publiées, aucun nombre n'est interprété |
| **exploratoire complet** | Run conforme et triage intégral selon les règles FSH ; publié à titre exploratoire |

#### 6.7 Q7 — Frontière analyseur / `check`

Contrôles à affecter, au minimum :
- détection HS016 par référence ;
- type applicatif non classifiable, référencé ou non (F20, F26, X4), avec `SPKCFG` en sévérité Error ;
- informations disponibles en métadonnées pour la classification et le code généré (X5) ;
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
| Budget mesurable dépassé (Q5 (1) médiane des ratios > 5 % ou Q5 (2) médiane des 10 `m_r` > 20 ms par document) | **no-go technique** |
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
| 2026-10-28 | non relevé (postérieur à 20:08:52 CET) | 20:26 CET | inconnue, au plus ≈ 17 min | S1-01 | Passe corrective demandée par A-05, à partir du commit `40a2763` : wrapper (panne d'enregistrement → 70, identité du log, ancêtres canoniques), test versionné (`passed=43 failed=0`), D-18, métrique Q5(2), SPKCFG bloquant, X5, P0 et sous-séquences N/F, hors mesure. Premier horodatage relevé : 20:23 CET, en cours de passe |
| 2026-10-28 → 2026-10-29 | 20:34 CET | 01:06 CET | ≈ 4 h 32 | S1-01 | Passe corrective directe autorisée par A-06 : revue croisée gouvernance/protocole/sécurité, wrapper et test du descendant asynchrone (`passed=45 failed=0`), P0 assaini dès le lancement, `RUN_ID`, confinement de l'export et des `bin/obj`, contrôle Roslyn des documents, portes UXR et consentement ; hors mesure, sans restore ni build OSS |

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

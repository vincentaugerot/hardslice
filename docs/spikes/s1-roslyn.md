> **Statut : référence technique non exécutée.** Ce protocole historique peut alimenter l'implémentation et les tests de HS016, mais il n'est plus une porte préalable au développement. Aucune mesure S1 n'a été réalisée.

# S1 — Spike Roslyn (HS016)

Statut : **protocole DRAFT**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02, A-03, A-05, A-06), puis le 29/10/2026 (A-07 ; A-08 : amendement D-18-A), **non validé** (V-S1-01 `OPEN` dans [phase-0-decisions.md](../execution/phase-0-decisions.md)). **Aucune mesure n'a été exécutée.**
Sources historiques : [evidence-first-plan.md](../evidence-first-plan.md) §6.1, §6.2, §6.3, §6.4, §6.5, §7.2, §10, annexe A.3 ; backlog S1-01 à S1-09 ; [s2a-conventions.md](s2a-conventions.md).

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
| M-08, M-08-A1 | Poste local ; builds OSS dans un compte macOS dédié et non privilégié, qui est le runner de D-18-A | `DECIDED` |
| OP-01 | Attestation d'isolation des comptes superviseur et runner, avant **toute série** Q1 à Q6 (D-18-A), et fin de série après chacune | `OPEN` — S1-02 à S1-07 fermés |
| M-10 | Second relecteur indépendant pour les cas de triage ambigus ou contestés | `OPEN` |
| D-18 | Protection des preuves et de la copie source contre le code exécuté : **option A** (superviseur distinct du runner, preuves et registre hors de portée du runner, export minimal), §3.4 et §3.6 | `DECIDED` le 29/10/2026 (A-08) ; protocole amendé, soumis à V-S1-01 |
| **V-S1-01** | Validation de ce protocole amendé pour D-18-A, y compris les oracles, les classes de commandes, la répartition du §9, le wrapper, P0, S0, K, `as-runner.sh`, les fonctions du §3.6 et l'observation IDE de Q3 | **`OPEN`** |
| AUT-01 | Commits locaux sans push ; commit de baseline **après** V-S1-01, avant le premier run mesuré | `DECIDED` ; commit non créé |

Dépendance entre questions : **Q6 (S1-07) dépend de Q2 (S1-03)**. Si X1 ou X2 n'atteint pas son oracle, Q6 est `non mesuré`.

D-01, D-13 et les validations UXR ne bloquent pas S1.

#### 2.1 Checklist opérationnelle avant le premier run

Chaque case est cochée dans la fiche de la première tentative de la série concernée, avec sa preuve.

| # | Point | Qui | Avant |
|---|---|---|---|
| 1 | V-S1-01 `DECIDED` | Mainteneur | Tout run |
| 2 | Échafaudage du spike (prototype, fixtures, états Q3, scripts de série) commité localement en commits atomiques, sans push ; commit de baseline créé ; le run cite un SHA dont le worktree est propre (`git status --porcelain` vide) | Agent, après V-S1-01 | Tout run |
| 3 | `tools/test-run-logged.sh` exécuté par le superviseur au commit du run (B1) : `passed=N failed=0`, code 0 | Script de série (superviseur) | Tout run |
| 4 | Prologue P0 (§6.5.1), sous le superviseur : lancement par la commande `env -i` imposée ; superviseur et runner distincts, non root, runner hors `admin` et `wheel` ; `HOME` du superviseur en `0700` ; `$HS_ART` possédé par le superviseur, en `0700`, sans ACL permissive, hors de tout dépôt Git et ne contenant que `.run-ids/` aux réservations vides ; racine d'échange vide ; worktree propre ; `<RUN_ID>` généré à partir de l'heure UTC, de la question, du SHA, du numéro de tentative et d'un UUID, puis réservé exclusivement (§3.2) ; manifeste privé créé aussitôt ; export minimal sans `.git` ni `docs/`, non inscriptible, avec `SOURCE` et `MANIFEST.sha256` | Script de série (superviseur) | Toute tentative (Q1 à Q6) |
| 5 | Sondes S0 sous le runner (identité, `HOME` du superviseur non traversable, ni dépôt ni registre accessibles, export non inscriptible, absence de terminal, capture des descendants, canari du presse-papiers), staging exclusif et copie de travail vérifiée ; inventaire de l'environnement effectif du runner (B0a) | Script de série | Toute tentative |
| 6 | `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` = `10.0.102` (étape `must_succeed` dont la sortie est comparée) | Script de série | Toute tentative |
| 7 | Journal humain : heure de début déclarée par le mainteneur | Mainteneur | Tout ticket S1 |
| 8 | OP-01 attestée (comptes superviseur et runner, règle de délégation unique, `HOME` du runner à l'état de référence propre sans état HardSlice, `.run-ids` comme seul état HardSlice persistant sous `$HS_ART`, chemins, propriétaires, modes et ACL, aucun secret ni donnée UXR accessible au runner, environnements en liste blanche, cache NuGet dédié, NuGet public, réseau limité, conditions M-08) | Mainteneur | Toute série (Q1 à Q6) |
| 9 | Bootstrap Q5/Q6 (§6.5.1) terminé, empreintes des binaires enregistrées, **avant** la coupure réseau | Script de série, lancé par le mainteneur | Q5, Q6 |
| 10 | Clones vérifiés : URL, SHA, `git status --porcelain` vide, avant et après la série | Script de série | Q5, Q6 |
| 11 | **Q5** : algorithme et outil `classify-worst-case` commités avant le run ; classification nopCommerce générée en C1 puis hachée en C2 avant toute mesure. **Q6** : `config/fsh.globalconfig` commitée et hachée avant le run | Agent | Q5, Q6 |
| 12 | Q2 conforme pour X1 et X2 au même code d'analyseur | Agent | Q6 |
| 13 | PRE-S1-04 attestée (versions exactes de VS Code stable, C# Dev Kit, extension C#, runtime) | Mainteneur | S1-04 |
| 14 | M-10 identifié, ou risque accepté que tout cas ambigu laisse Q6 `non mesuré` | Mainteneur | Triage S1-07 |
| 15 | D-18 décidée (option A, A-08) et protocole amendé en conséquence (fait le 29/10/2026) ; amendement validé dans V-S1-01 | Mainteneur | V-S1-01, donc tout run |
| 16 | Finalisation K réussie, puis fin de série OP-01 (contrôle des processus, inventaire, rangement, retour à l'état de référence et préservation append-only de `.run-ids`) attestée pour la série précédente avant toute réutilisation du runner | Mainteneur | Toute nouvelle série (Q1 à Q6) |
| 17 | `$HS_ART` et l'emplacement D-10 hors synchronisation iCloud, Dropbox, OneDrive ou autre ; emplacement attesté dans la fiche. P0 contrôle la partie Git, cette attestation couvre la synchronisation | Mainteneur | Toute tentative (Q1 à Q6) |
| 18 | V-S1-01 valide l'observation IDE de Q3 dans la session graphique du runner, ou acte S1-04 `non mesuré` (§6.3) | Mainteneur | S1-04 |

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

L'hôte de benchmark construit est lancé sous le runner par `/usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" …`, c'est-à-dire depuis sa copie promue en lecture seule (§3.6), sans `dotnet run`. Aucune commande `dotnet <verbe>` nue, aucun hôte trouvé par le `PATH`. Chaque série vérifie, par une étape `must_succeed` à sortie comparée, `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` et, pour une solution externe, `/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <un .csproj de la solution> -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath`. Une valeur qui ne désigne pas 10.0.102 arrête la série.

Vérification préalable du 28/10/2026, hors mesure : sur le clone nopCommerce, dont le `global.json` épingle 10.0.401, l'invocation du SDK 10.0.102 par son `dotnet.dll` évalue `NETCoreSdkVersion = 10.0.102` et `RoslynTargetsPath` sous `sdk/10.0.102`, sans modification du clone. Le spike pose aussi `spikes/s1-roslyn/global.json` (`"version": "10.0.102"`, `"rollForward": "disable"`), ceinture en plus de la règle d'invocation.

#### 3.2 Identifiant de tentative et variables symboliques (D-10-A1)

**`<RUN_ID>`** identifie une tentative de façon immuable : `<AAAAMMJJ>T<HHMMSS>Z-<Qn>-<SHA court de 12 caractères du commit HardSlice>-a<NN>-u<UUID sans tirets, 32 hexadécimaux minuscules>`, par exemple `20261030T091500Z-Q1-0123456789ab-a01-u2393e983a9c1489c9fbc780a231f1cb4`. Le mainteneur fournit seulement `ATTEMPT=<NN>`, étiquette humaine à deux chiffres consignée dans la fiche et qui n'est pas le mécanisme d'unicité ; P0 la valide, relève lui-même l'heure UTC, la question et le SHA, génère l'UUID, valide chacun de ces composants puis compose le `RUN_ID`. L'UUID rend les collisions opérationnellement négligeables même entre deux racines d'artefacts ; la réservation exclusive `$HS_ART/.run-ids/<RUN_ID>/` interdit en plus toute réutilisation locale. Ce registre est hors Git, ne contient aucune sortie brute et n'est jamais supprimé, y compris après le rangement ou le nettoyage d'une tentative. **Sous D-18-A, le superviseur génère et réserve le `RUN_ID` et possède le registre** : `$HS_ART` est sous son `HOME`, que le runner ne peut pas traverser (S0-3, S0-5). Le registre est le seul état HardSlice persistant autorisé par l'état de référence OP-01 ; ses réservations sont append-only. Face au code exécuté sous le runner, cette propriété est garantie par les permissions ; face à une erreur côté superviseur, elle reste procédurale et vérifiée par la fin de série OP-01. Le dossier versionnable `results/<RUN_ID>/` n'est créé qu'au moment de publier la fiche et les extraits normalisés, avec refus s'il existe déjà : le créer avant la mesure salirait le worktree dont P0 doit attester la propreté. Une tentative interrompue garde sa réservation, et la suivante reçoit un nouvel identifiant.

Les commandes versionnées n'emploient que ces variables ; leur expansion exacte est écrite dans le manifeste privé `$HS_RUN/manifest.private.md`, hors git.

| Variable | Propriétaire | Accès du runner | Contenu |
|---|---|---|---|
| `$HS_REPO` | superviseur | aucun | Worktree propre du dépôt HardSlice au commit mesuré, sous le `HOME` du superviseur ; lu par P0 seulement (statut, `git archive`) |
| `$HS_ART` | superviseur | aucun | Racine des preuves, `0700`, strictement sous le `HOME` du superviseur, hors git et hors synchronisation cloud ; ne contient que `.run-ids/` entre les séries |
| `$HS_RUN` | superviseur | aucun | `$HS_ART/<RUN_ID>` : journaux du wrapper, sidecars `.exit`, manifeste privé, archive source, instantanés, collecte finale. **Seul emplacement où les sorties brutes sont conservées** |
| `$HS_XROOT` | superviseur | lecture | `/Users/Shared/hardslice-s1`, racine d'échange `0755`, vide entre les séries |
| `$HS_EXPORT` | superviseur | lecture seule | `$HS_XROOT/<RUN_ID>` : `SOURCE` (SHA du commit), `MANIFEST.sha256` (empreinte de chaque fichier exporté), `src/` et `ro/` |
| `$HS_SRC` | superviseur | lecture seule | `$HS_EXPORT/src/spikes/s1-roslyn` : **export minimal**, `git archive` de `spikes/s1-roslyn/` seulement, sans `.git` ni `docs/` |
| `$HS_TOOLS` | superviseur | lecture seule | `$HS_SRC/tools` : scripts que le superviseur exécute (wrapper, `as-runner.sh`, scripts de série) |
| `$HS_RO` | superviseur | lecture seule | `$HS_EXPORT/ro` : sorties du runner promues en lecture seule pour être réutilisées (§3.6) |
| `$HS_STAGE` | runner | lecture et écriture | `<HOME du runner>/hardslice-stage` : **staging**, seul emplacement où écrivent les commandes du runner ; créé par S0-7, détruit par K5 |
| `$HS_SPIKE` | runner | lecture et écriture | `$HS_STAGE/work/spikes/s1-roslyn` : copie de travail de `$HS_SRC`, vérifiée par S0-10 ; toutes les compilations du spike y travaillent |
| `$HS_CLONES` | runner | lecture et écriture | `$HS_STAGE/clones`, clones OSS de la tentative |

Permissions : côté superviseur, dossiers `0700` et preuves `0600` (le wrapper, P0 et le script de série imposent `umask 077`) ; côté export, dossiers `0755` et fichiers `0644` ou `0755`, sans droit d'écriture pour le groupe ni les autres ; côté runner, `umask 077`. Les sorties brutes produites dans `$HS_STAGE` restent hors git ; elles ne sont conservées que sous `$HS_RUN`, sous forme d'instantanés et d'archive collectée (§3.6). `$HS_RUN` est ensuite rangé par le mainteneur sous l'emplacement D-10 (`~/Documents/hardslice-artifacts/s1-roslyn/<RUN_ID>/`), avec vérification des SHA-256 du manifeste et refus d'écraser un dossier existant.

#### 3.3 Wrapper de commande et vérification d'intégrité

Toute commande de série passe par le wrapper versionné [`spikes/s1-roslyn/tools/run-logged.sh`](../../spikes/s1-roslyn/tools/run-logged.sh), **exécuté par le superviseur** depuis l'export (`$HS_TOOLS`). Une commande du runner lui est passée à travers `as-runner.sh` (§3.6) ; un contrôle du superviseur (B1, K3, K7, K8) lui est passé directement :

```
"$HS_TOOLS/run-logged.sh" "$HS_RUN/<étape>.log" -- "$HS_TOOLS/as-runner.sh" <commande> [arguments…]
"$HS_TOOLS/run-logged.sh" "$HS_RUN/<étape>.log" -- <contrôle du superviseur> [arguments…]
```

Le log et son `.exit` sont donc créés et vérifiés sous l'identité du superviseur, dans `$HS_RUN`, que le runner ne peut ni lire ni modifier. Le runner ne reçoit que l'extrémité d'écriture du pipe de stdout et stderr.

Comportement :
- **Création** : `umask 077`. Le chemin du log est absolu et son parent doit être **canonique** (`pwd -P` du parent égal au chemin donné) : aucun lien symbolique dans aucun ancêtre, ni `.` ni `..`. Le log et son sidecar `<log>.exit` sont créés exclusivement (`noclobber`, donc `O_EXCL`), en mode `0600`.
- **Préflight** : refus avant lancement (code 70, commande non lancée) si le parent est absent ou non canonique, si le log ou le `.exit` existe déjà, ou si l'un d'eux est un lien symbolique (même cassé). Chemin relatif ou usage incorrect : 64.
- **En-tête** : horodatage UTC, `# log-id: <device>:<inode>` du log, commande exacte ; écrit puis relu avant le lancement.
- **Exécution** : la commande n'hérite jamais du descripteur du log ; stdout et stderr passent par un pipe consommé par le wrapper. Le pipe n'atteint EOF qu'après la fermeture de tous les descendants qui l'ont hérité : leur sortie est donc complète avant l'écriture du pied. Pour une commande du runner, cette garantie suppose que `sudo` n'intercale pas de pseudo-terminal : `as-runner.sh` l'obtient en détachant la commande de tout terminal (§3.6), et S0-12 le vérifie. Un descendant persistant qui conserve le pipe ouvert maintient volontairement la commande en cours au lieu de produire une preuve prématurée.
- **Après la commande, avant de faire confiance au résultat** :
  - même `device:inode`, pas de lien symbolique, en-tête intact ;
  - aucun `.exit` apparu pendant l'exécution ;
  - pied `# end` / `# exit: <code>` écrit, puis revérifié après fermeture, avec l'identité et l'en-tête ;
  - `.exit` créé exclusivement, relu, cohérent avec le pied.
- **Codes de sortie** : le code réel de la commande si tout l'enregistrement est cohérent ; 64 sur erreur d'usage ; **70 pour toute panne de préflight ou d'enregistrement, quel que soit le code retourné par la commande**. En cas de panne d'enregistrement, le vrai code est écrit sur stderr (« not usable as a result ») et, si possible, dans le pied ; il n'est **jamais** utilisable comme résultat.

**Vérification par le script de série, dans cet ordre, avant tout oracle métier** :
1. **Identité et intégrité du log** : fichier régulier, pas un lien symbolique, propriétaire = superviseur, mode `0600`, `device:inode` actuel égal à la ligne `# log-id` ; SHA-256 relevé dans le manifeste privé.
2. **En-tête complet** : lignes `# start`, `# log-id` et `# cmd` présentes ; `# cmd` égal à la commande lancée par le script.
3. **Pied présent et cohérent** : dernière ligne `# exit: N`, précédée de `# end`, aucune ligne `# record: FAILED`.
4. **Sidecar `.exit` présent et cohérent** : fichier régulier, mode `0600`, contenu `N`, et code de retour du wrapper égal à `N`.
5. **Seulement ensuite**, comparaison de `N` et des diagnostics à la classe `must_succeed`, `must_fail`, `must_fail_partial` ou `informative` (§3.5).

Un échec à l'une des étapes 1 à 4 est une **panne d'infrastructure** : la tentative est invalide, quelle que soit la classe de l'étape. Un échec attendu (`must_fail` ou `must_fail_partial`) n'est jamais atteint par un code 70 sans enregistrement complet et cohérent ; un code 70 avec pied et `.exit` égaux à 70 est le vrai code de la commande. Le « corps » d'un log est l'ensemble des lignes entre l'en-tête et le pied ; les comparaisons de sortie portent sur lui.

À eux seuls, ces contrôles détectent les erreurs et les écrasements accidentels ; ils ne protégeraient pas un log contre une commande malveillante exécutée sous le même UID. **Sous D-18-A, cette protection vient de la séparation des identités** : la commande s'exécute sous le runner, le log, le `.exit` et le manifeste appartiennent au superviseur et se trouvent hors de portée du runner (§3.4, S0-3 à S0-5). Le *contenu* du corps reste produit par la commande ; seuls l'en-tête, le pied, le code de sortie et l'intégrité du fichier sont garantis par le superviseur.

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

Résultat du 28/10/2026, hors mesure : `passed=45 failed=0`. Ce test porte sur le wrapper seul ; `as-runner.sh`, P0 et les fonctions du §3.6 ne sont pas encore écrits ni testés : ils le seront avec l'échafaudage, après V-S1-01.

#### 3.4 Isolation et environnement d'exécution (M-08-A1, D-18-A, OP-01)

**D-18 est décidée : option A** ([phase-0-decisions.md, D-18](../execution/phase-0-decisions.md#d-18--protection-des-preuves-et-de-la-copie-source-contre-le-code-exécuté), A-08). Le mainteneur retient un superviseur et logger sous une identité distincte du runner, un registre et un répertoire de preuves non inscriptibles par le runner, et un export minimal limité à `spikes/s1-roslyn/`, sans `.git`, sans `docs/` et sans accès au dépôt complet.

**Portée.** Toutes les commandes des séries Q1 à Q6 qui lancent le SDK, git sur un clone, l'hôte de benchmark ou tout autre code tiers s'exécutent sous le runner. Les fixtures Q1 à Q4 suivent le même chemin que nopCommerce et FSH : leurs restores et leurs builds exécutent aussi du code de paquets NuGet, et le runner ne doit jamais voir le dépôt complet. Seule l'observation IDE de Q3 échappe au wrapper ; elle n'est compatible avec D-18-A que dans la session graphique du runner, point ouvert de V-S1-01 (§6.3).

**Répartition des responsabilités**

| Rôle | Identité | Exécute | Possède |
|---|---|---|---|
| Superviseur | Compte macOS local du mainteneur | P0, script de série, `run-logged.sh`, `as-runner.sh`, instantanés, promotions et collecte (§3.6) ; uniquement des scripts de l'export et des outils système sous `/usr/bin` et `/bin`. Il n'exécute jamais un fichier produit par le runner | `$HS_ART`, `.run-ids`, `$HS_RUN` (journaux, sidecars `.exit`, manifeste privé, archive source, instantanés, collecte), `$HS_XROOT` et `$HS_EXPORT` |
| Runner | Compte macOS local dédié, standard, distinct du superviseur (le compte dédié de M-08-A1) | Uniquement les commandes que lui passe `as-runner.sh` | `$HS_STAGE` pendant la série |
| Mainteneur | Personne | Prépare les deux comptes et la délégation, lance chaque série depuis un terminal du superviseur (§6.5.1), réalise et atteste OP-01 | — |
| Agent | Compte du mainteneur | Aucune commande de série. Il écrit et commite les scripts, puis analyse les sorties rangées. Il ne crée aucun compte et ne modifie aucune configuration système | — |

Le superviseur n'obtient l'identité du runner que par `as-runner.sh`, c'est-à-dire par `sudo -n -u <runner> -- /usr/bin/env -i …`, autorisé par une **règle sudoers unique** que le mainteneur pose et atteste (OP-01, point 2). Les propriétaires, modes et ACL de chaque chemin sont fixés par OP-01 et contrôlés par P0 et S0.

**Flux d'une série.**
1. P0, sous le superviseur : contrôle des identités et de l'état de référence, génération et réservation du `RUN_ID`, création de `$HS_RUN` et du manifeste, puis export minimal en lecture seule sous `$HS_EXPORT`.
2. S0 : sondes d'isolation exécutées sous le runner, création exclusive du staging, copie de travail `$HS_SPIKE` vérifiée contre `MANIFEST.sha256`.
3. Étapes de série : le wrapper du superviseur lance chaque commande sous le runner et capture stdout et stderr par pipe. Le runner lit l'export, les copies promues et les outils système, et n'écrit que dans `$HS_STAGE`.
4. Lectures et réutilisations : le superviseur ne lit jamais directement le staging. Il prend des **instantanés** (archive `tar` produite sous le runner et reçue par pipe), puis les vérifie et les hache. Toute sortie réutilisée comme entrée d'une étape ultérieure (binaires, flux de paquets, classification générée, assemblies X5) est **promue** en lecture seule sous `$HS_RO` (§3.6).
5. Finalisation K : arrêt des processus du runner, collecte finale, suppression du staging et de l'export (§6.5.1). La fin de série OP-01 vient ensuite.

**Contraintes d'environnement** (M-08-A1, OP-01) :
- `HOME` du runner dans l'état de référence propre défini par OP-01 : éléments créés par macOS seulement, sans aucun état HardSlice. Le registre `.run-ids` est sous `$HS_ART`, chez le superviseur ; avant la série, `$HS_ART` ne contient que lui ;
- aucun secret, trousseau personnel, agent SSH, credential cloud ou donnée UXR accessible au runner ; le `HOME` du superviseur, qui contient `$HS_REPO`, `$HS_ART`, l'emplacement D-10 et les données D-13, n'est pas traversable par le runner ;
- configuration NuGet limitée aux sources publiques requises ;
- cache NuGet dédié à la tentative, sous `$HS_STAGE/nuget/` ;
- entrées du runner limitées à l'export minimal et aux copies promues, en lecture seule, au SDK et aux outils système, non inscriptibles par lui ;
- réseau limité pour les processus du runner : LAN bloqué, sortie limitée aux flux nécessaires (`api.nuget.org` et ses CDN, `github.com` pour les clones). Pour Q5 et Q6, coupure complète après le bootstrap (B11). Pour Q1 à Q4, dont les builds restaurent implicitement, le mode limité dure toute la série et la fiche le consigne. Si ce filtrage n'est pas réalisable, le **risque résiduel** est décrit et accepté explicitement par le mainteneur dans OP-01. Un échec dû à l'absence de réseau est publié ; toute réactivation est consignée avec sa raison.

**Limites résiduelles de D-18-A.**
- **Sorties du runner.** Jusqu'à leur instantané, elles restent sous le contrôle du runner : le superviseur garantit leur conservation et leur intégrité après l'instantané, pas leur véracité. Du code tiers exécuté dans une série peut altérer les sorties ultérieures du staging de la même série. Il ne peut altérer ni les entrées et binaires promus, ni les journaux, sidecars, manifestes, instantanés ou le registre.
- **Confidentialité.** Pendant la fenêtre réseau, le runner peut tenter d'exfiltrer ce qu'il peut lire : l'export minimal, ses copies promues, son staging et les fichiers système lisibles par tout compte. Le dépôt complet, `.git`, `docs/`, le `HOME` du superviseur et les preuves restent hors de sa portée (S0-3 à S0-5, OP-01).
- **Même UID côté superviseur.** Le superviseur partage l'UID du mainteneur et de l'agent. D-18-A protège les preuves contre le code exécuté sous le runner, pas contre un processus du compte du mainteneur. La règle sudoers permet aussi à tout processus de ce compte d'agir comme runner ; l'agent ne lance aucune série.
- **Session du superviseur.** `sudo` ne déplace pas le runner dans une autre session de connexion : il hérite du port bootstrap de la session graphique du superviseur, et certains services de cette session (presse-papiers, `open`) pourraient lui rester joignables. S0-13 sonde le presse-papiers par un canari ; les autres services ne sont pas vérifiés, et ce risque est un point de V-S1-01.
- **IDE de Q3** : seule la session graphique du runner est compatible avec D-18-A ; à valider dans V-S1-01 (§6.3).

**Environnement en liste blanche.** Le superviseur est lancé par `env -i` (commande de lancement de P0), puis P0 lance le script de série par un second `env -i`. Chaque commande du runner reçoit sa propre liste par `env -i`, dans `as-runner.sh`. Toute autre variable est absente. L'environnement effectif du runner est archivé par B0a ; celui du superviseur est fixé par P0 et consigné dans le manifeste privé.

| Variable | Superviseur (script de série) | Runner (`as-runner.sh`) |
|---|---|---|
| `HOME` | `HOME` du superviseur | `$HS_RUNNER_HOME` |
| `PATH` | `/usr/bin:/bin:/usr/sbin:/sbin` | idem |
| `TMPDIR` | `$HS_RUN/tmp` | `$HS_STAGE/tmp` |
| `LANG`, `LC_ALL` | `en_US.UTF-8` | idem |
| `DOTNET_CLI_UI_LANGUAGE` | — | `en-US` (sorties MSBuild en anglais, stables pour l'extraction) |
| `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_NOLOGO` | — | `1` |
| `DOTNET_CLI_HOME` | — | `$HS_STAGE/dotnet-home` |
| `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH`, `NUGET_PLUGINS_CACHE_PATH` | — | sous `$HS_STAGE/nuget/` (cache dédié à la tentative) |
| `MSBuildSDKsPath` | — | `/usr/local/share/dotnet/sdk/10.0.102/Sdks` |
| `GIT_CONFIG_NOSYSTEM`, `GIT_CONFIG_GLOBAL`, `GIT_TERMINAL_PROMPT` | `1`, `/dev/null`, `0` | idem |
| `HS_REPO`, `HS_ART`, `HS_RUN`, `HS_XROOT`, `HS_TOOLS`, `HS_RUNNER`, `HS_RUNNER_UID`, `HS_RUNNER_HOME` | §3.2 | jamais transmises |
| `HS_EXPORT`, `HS_SRC`, `HS_RO`, `HS_STAGE`, `HS_SPIKE`, `HS_CLONES`, `RUN_ID` | §3.2 ; `RUN_ID` est généré et validé par P0 | idem |

#### 3.5 Classes de commandes et oracles de code de sortie

Chaque étape d'un script de série porte une classe pré-enregistrée. Le wrapper conserve toujours le code réel quand l'enregistrement est complet. **Le script de série** vérifie d'abord l'intégrité de l'enregistrement (§3.3, étapes 1 à 4), puis compare le code et, si la classe l'exige, les diagnostics observés à l'oracle :

| Classe | Oracle | Série si l'oracle n'est pas atteint |
|---|---|---|
| `must_succeed` | Code 0 ; sortie comparée quand c'est précisé | Arrêt ; tentative invalide |
| `must_fail(<code>, <diagnostics>)` | Code exact et ensemble exact de diagnostics attendus (ID, sévérité, nombre, emplacement si précisé) ; 0 AD0001 ; aucun autre diagnostic d'erreur | Arrêt ; tentative invalide |
| `must_fail_partial(<code>, requis, autorisés)` | Code exact ; diagnostics requis exacts ; tout diagnostic supplémentaire appartient à l'allowlist explicite. Les diagnostics autorisés sont publiés mais leur présence et leur nombre ne participent pas au succès de l'oracle | Arrêt ; tentative invalide |
| `informative` | Aucun sur le code ; code et sortie publiés | Continue |

Un échec attendu conforme à son oracle (`must_fail` ou `must_fail_partial` atteint) **ne stoppe pas** la série ; tout écart à l'oracle (code, diagnostic requis manquant, diagnostic interdit en trop ou mauvaise sévérité) invalide la tentative. Pour `must_fail`, aucun diagnostic supplémentaire n'est autorisé ; pour `must_fail_partial`, seuls ceux de l'allowlist le sont. Une panne d'infrastructure (§3.3) invalide la tentative quelle que soit la classe. Les diagnostics sont lus dans les SARIF par projet, jamais dans le texte, et toujours dans l'instantané de l'étape (§3.6) : chaque fixture définit, dans `fixtures/Directory.Build.props`, `ErrorLog = $(HardSliceSpikeSarifDir)/$(HardSliceSpikeStep)-$(MSBuildProjectName).sarif`, et les commandes passent `-p:HardSliceSpikeStep=<étape>` et `-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif`. Pour l'hôte de benchmark, ils sont lus dans sa sortie structurée, également par instantané. Un échec d'instantané, de promotion ou de collecte est une panne d'infrastructure.

**Instantanés d'oracle, règle générale de série.** Immédiatement après chaque étape dont l'oracle ou un calcul lit un fichier du staging, et avant l'étape suivante, le script de série prend `snapshot <étape> <chemins>`, l'étiquette étant l'identifiant de l'étape en minuscules (`q1-1`, `cli-i2-sans`, `precedence-b`, `build-3-h`…) : `sarif` après chaque build qui produit des SARIF (Q1-1, Q1-2, Q2-1, Q2-3, builds d'état de Q3, cas de Q4) ; le fichier `out/…` produit par chaque commande de l'hôte (Q1-4, Q1-5, Q2-6, Q2-7, C1, `perf`, `exposure`) ; `out/build-<k>-<X>.msbuild.log` après chaque build de Q5 (1) ; les `nuget.config` listés par B9n. L'oracle lit cet instantané, jamais le staging, de sorte qu'un build ultérieur qui écrit dans le même dossier ne peut pas modifier ce qui a été jugé. Une tentative invalide reste publiée.

**Hôte de benchmark et diagnostics bloquants.** Les commandes `exposure`, `inspect-symbols`, `classify-worst-case` et `perf` de l'hôte sortent en **0 dès que l'analyse est complète** et que leurs contrôles de chargement passent, quel que soit le nombre de HS016 ou de `SPKCFG` observés. Un `SPKCFG`, même de sévérité Error, est un **diagnostic** compté et listé, jamais une panne du programme. Les codes non nuls sont réservés aux pannes : 2 pour un échec de chargement ou de contrôle (diagnostics du workspace, documents ignorés, SDK effectif), 3 pour une exception d'analyseur, 4 pour une incohérence de contrôle (télémétrie, diagnostics différents entre répétitions), 64 pour un usage incorrect. L'oracle sur les nombres de diagnostics est appliqué ensuite par le script de série.

#### 3.6 Délégation au runner, instantanés, promotions et collecte (D-18-A)

`tools/as-runner.sh`, `tools/series-lib.sh` et `tools/series-finalize.sh` (§6.5.1) seront versionnés tels quels avec l'échafaudage, après V-S1-01, et exécutés par le superviseur depuis `$HS_TOOLS`.

**`as-runner.sh`** : seul passage du superviseur vers le runner. Le répertoire courant devient `/` pour que le runner n'hérite pas d'un répertoire du superviseur. L'entrée standard est `/dev/null`, et `sudo` ne transmet que les descripteurs 0 à 2, donc l'extrémité d'écriture du pipe du wrapper. Le processus se détache d'abord de tout terminal de contrôle par `POSIX::setsid` de `/usr/bin/perl`, interpréteur déjà requis par `shasum`. Sans terminal, `sudo` n'alloue aucun pseudo-terminal (même si `use_pty` est actif par défaut) et le runner ne peut ni lire ni injecter de frappes dans le terminal du superviseur ; la sortie passe intégralement par le pipe du wrapper, ce qui préserve la garantie du §3.3 sur les descendants. Un échec de `setsid` arrête la commande avant `sudo`.

```bash
#!/bin/bash
set -u
umask 077
: "${HS_RUNNER:?}" "${HS_RUNNER_HOME:?}" "${HS_EXPORT:?}" "${HS_SRC:?}" "${HS_RO:?}" \
  "${HS_STAGE:?}" "${HS_SPIKE:?}" "${HS_CLONES:?}" "${RUN_ID:?}"
[ "$#" -ge 1 ] || { echo "usage: as-runner.sh <commande> [arguments…]" >&2; exit 64; }
cd / || exit 70
exec /usr/bin/perl -e 'use POSIX (); POSIX::setsid() >= 0 or die "setsid: $!\n"; exec { $ARGV[0] } @ARGV or die "exec: $!\n";' -- \
  /usr/bin/sudo -n -u "$HS_RUNNER" -- /usr/bin/env -i \
  HOME="$HS_RUNNER_HOME" PATH=/usr/bin:/bin:/usr/sbin:/sbin TMPDIR="$HS_STAGE/tmp" \
  LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 DOTNET_CLI_UI_LANGUAGE=en-US \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_CLI_HOME="$HS_STAGE/dotnet-home" \
  NUGET_PACKAGES="$HS_STAGE/nuget/packages" NUGET_HTTP_CACHE_PATH="$HS_STAGE/nuget/http-cache" \
  NUGET_PLUGINS_CACHE_PATH="$HS_STAGE/nuget/plugins-cache" \
  MSBuildSDKsPath=/usr/local/share/dotnet/sdk/10.0.102/Sdks \
  GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_TERMINAL_PROMPT=0 \
  HS_EXPORT="$HS_EXPORT" HS_SRC="$HS_SRC" HS_RO="$HS_RO" HS_STAGE="$HS_STAGE" \
  HS_SPIKE="$HS_SPIKE" HS_CLONES="$HS_CLONES" RUN_ID="$RUN_ID" \
  "$@" < /dev/null
```

Un refus de `sudo` (règle absente ou mot de passe requis) donne un code non nul qui fait échouer S0-1 avant toute autre commande du runner. Le détachement du terminal et la capture des descendants sont vérifiés sous le runner par S0-11 et S0-12.

**Fonctions du superviseur (`series-lib.sh`).** Un `return 70` est une panne d'infrastructure : la tentative est invalide et la série passe à K. Ces opérations ne passent pas par le wrapper, parce qu'elles transportent des archives binaires : le `tar` du runner écrit dans un pipe, que lit un `cat` du superviseur ; leurs propres sorties (archive, stderr, code, listing, empreintes) sont créées exclusivement sous `$HS_RUN`.

```bash
# snapshot ÉTIQUETTE CHEMIN… : instantané de CHEMINS relatifs à $HS_STAGE, lu sous le runner, reçu par pipe.
snapshot() {
  label=$1; shift
  case "$label" in ''|*[!a-z0-9-]*) return 70 ;; esac
  [ "$#" -ge 1 ] || return 70
  for p in "$@"; do
    case "$p" in ''|-*|/*|*..*|*[!A-Za-z0-9._/+@-]*) return 70 ;; esac
  done
  snap=$HS_RUN/snapshots/$label
  /bin/mkdir -m 0700 -- "$snap" "$snap/tree" || return 70
  ( set -o noclobber
    "$HS_TOOLS/as-runner.sh" /usr/bin/tar -cf - -C "$HS_STAGE" "$@" 2> "$snap/tar.stderr" \
      | /bin/cat > "$snap/archive.tar"
    printf '%s\n' "$?" > "$snap/tar.exit"
  ) || return 70
  [ "$(/bin/cat "$snap/tar.exit")" = 0 ] && [ ! -s "$snap/tar.stderr" ] || return 70
  # Seuls des fichiers réguliers et des dossiers : ni lien symbolique, ni lien physique, ni fichier spécial.
  /usr/bin/tar -tvf "$snap/archive.tar" > "$snap/listing.txt" || return 70
  /usr/bin/awk '{ t = substr($0, 1, 1) } t != "-" && t != "d" { bad = 1 } END { exit bad }' "$snap/listing.txt" || return 70
  /usr/bin/tar -tf "$snap/archive.tar" > "$snap/names.txt" || return 70
  /usr/bin/awk '/^\// || /(^|\/)\.\.(\/|$)/ || !/^[A-Za-z0-9._\/+@-]+$/ { bad = 1 } END { exit bad }' "$snap/names.txt" || return 70
  /usr/bin/tar -xf "$snap/archive.tar" -C "$snap/tree" || return 70
  [ -z "$(/usr/bin/find "$snap/tree" ! -type f ! -type d -print)" ] || return 70
  ( cd "$snap/tree" && /usr/bin/find "$@" -type f -exec /usr/bin/shasum -a 256 {} + ) \
    | LC_ALL=C /usr/bin/sort -k 2 > "$snap/tree.sha256" || return 70
  archive_sha=$(/usr/bin/shasum -a 256 "$snap/archive.tar") && tree_sha=$(/usr/bin/shasum -a 256 "$snap/tree.sha256") \
    || return 70
  printf 'SNAPSHOT %s archive=%s tree=%s\n' "$label" "${archive_sha%% *}" "${tree_sha%% *}" \
    >> "$HS_RUN/manifest.private.md" || return 70
}

# promote ÉTIQUETTE CHEMIN… : instantané, puis copie en lecture seule pour le runner, $HS_STAGE/CHEMIN → $HS_RO/CHEMIN.
promote() {
  label=$1; shift
  snapshot "$label" "$@" || return 70
  for p in "$@"; do
    [ ! -e "$HS_RO/$p" ] && [ ! -L "$HS_RO/$p" ] || return 70
  done
  ( umask 022 && /usr/bin/tar -xf "$HS_RUN/snapshots/$label/archive.tar" -C "$HS_RO" ) || return 70
  for p in "$@"; do
    /usr/bin/find "$HS_RO/$p" -type d -exec /bin/chmod 0755 {} + || return 70
    /usr/bin/find "$HS_RO/$p" -type f -exec /bin/chmod 0644 {} + || return 70
  done
  [ -z "$(/usr/bin/find "$HS_RO" \( ! -type f ! -type d -o -perm -020 -o -perm -002 -o ! -user "$(/usr/bin/id -u)" \) -print)" ] \
    || return 70
  ( cd "$HS_RO" && /usr/bin/find "$@" -type f -exec /usr/bin/shasum -a 256 {} + ) | LC_ALL=C /usr/bin/sort -k 2 \
    | /usr/bin/cmp -s - "$HS_RUN/snapshots/$label/tree.sha256" || return 70
  printf 'PROMOTED %s\n' "$label" >> "$HS_RUN/manifest.private.md" || return 70
}

# collect [-finalize] : archive finale du staging (hors caches), conservée et hachée, jamais extraite pendant la série.
collect() {
  sfx=${1:-}
  case "$sfx" in ''|-finalize) ;; *) return 70 ;; esac
  out=$HS_RUN/collected
  ( set -o noclobber
    "$HS_TOOLS/as-runner.sh" /usr/bin/tar -cf - -C "$HS_STAGE" work bin sarif feed clones out \
      2> "$out/tar$sfx.stderr" | /bin/cat > "$out/staging$sfx.tar"
    printf '%s\n' "$?" > "$out/tar$sfx.exit"
  ) || return 70
  [ "$(/bin/cat "$out/tar$sfx.exit")" = 0 ] || return 70
  /usr/bin/tar -tvf "$out/staging$sfx.tar" > "$out/listing$sfx.txt" || return 70
  collected_sha=$(/usr/bin/shasum -a 256 "$out/staging$sfx.tar") || return 70
  printf 'COLLECTED staging%s.tar %s\n' "$sfx" "${collected_sha%% *}" >> "$HS_RUN/manifest.private.md" || return 70
}
```

Règles :
- **Lecture.** Toute lecture par le superviseur d'un fichier produit par le runner (oracle, contrôle, calcul) porte sur un instantané pris immédiatement après l'étape qui le produit, par exemple `snapshot q1-1 sarif` après Q1-1. Le superviseur lit ce fichier comme une donnée, avec des outils système ou des scripts de l'export, et ne l'exécute jamais. Un script Python du superviseur est lancé par `/usr/bin/python3 -I` ; aucun contenu produit par le runner n'est affiché sur un terminal sans neutralisation préalable des caractères de contrôle.
- **Réutilisation.** Toute sortie du runner qui sert d'entrée à une étape ultérieure est d'abord promue ; l'étape ultérieure lit la copie sous `$HS_RO`, jamais celle du staging. La promotion suit immédiatement l'étape productrice, avant toute autre commande du runner. Les promotions sont des étapes explicites des tables du §6 (Q1-3p, Q2-1p, Q2-2p, Q2-3p, Q2-4p, `q3-feed`, `q4-pack`, B3p, B6, C2).
- **Collecte.** K4 archive `work`, `bin`, `sarif`, `feed`, `clones` et `out`. Les caches `nuget/`, `dotnet-home/` et `tmp/` ne sont pas conservés ; ce qui en a servi de preuve (paquet X5) a été promu. Le staging n'est arrêté par K1 à K3 qu'avant la collecte : un instantané intermédiaire peut donc refléter un fichier encore modifié par un processus du runner, ce qui relève de la première limite résiduelle du §3.4.
- **Normalisation.** Les extraits versionnés sont tirés des instantanés et normalisés selon le §4.

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
  tools/                            run-logged.sh, test-run-logged.sh, as-runner.sh, series-lib.sh, series-finalize.sh, scripts de série, extraction, normalisation
  results/<RUN_ID>/                 fiches et extraits normalisés seulement
```

**Toute sortie brute reste hors git, et n'est conservée que sous `$HS_RUN`**, quelle que soit sa taille : logs complets et `.exit` (écrits directement par le superviseur), ainsi que copie de travail, `bin/obj`, binlogs, SARIF, CSV, sorties MSBuild, captures brutes et clones OSS, produits par le runner dans `$HS_STAGE`, puis conservés comme instantanés et archive collectée (§3.6). Le staging et l'export sont transitoires et ne sont jamais des preuves. `results/<RUN_ID>/` ne reçoit que des fiches et des extraits **normalisés** : nom d'utilisateur, `$HOME` et chemins locaux remplacés par `<USER>`, `<HOME>`, `<HS_ART>`, `<HS_RUN>`, `<HS_XROOT>`, `<HS_EXPORT>`, `<HS_SRC>`, `<HS_RO>`, `<HS_STAGE>`, `<HS_CLONES>`, `<HS_SPIKE>`, `<HS_REPO>`, puis contrôle de secrets ; un extrait qui échoue au contrôle n'est pas versionné. `spikes/.gitignore` ignore les extensions brutes et les dossiers `raw/` comme garde-fou.

### 5. Fiche de run (obligatoire pour chaque tentative)

Chaque tentative produit une fiche normalisée `results/<RUN_ID>/README.md` avec :
- `<RUN_ID>`, question (Q1–Q7), date et heure ;
- machine, identités sous leur rôle (« superviseur », « runner »), noms réels dans le manifeste privé seulement, conditions M-08 ;
- versions : sortie `--version` de la règle d'invocation, compilateur, runtime de l'hôte de benchmark, paquets Roslyn, IDE le cas échéant ;
- SHA du commit HardSlice, `git status --porcelain`, résultat de `test-run-logged.sh` ;
- pour chaque dépôt externe : **URL, SHA de `HEAD` et `git status --porcelain` vide**, avant et après. Un clone est identifié ainsi, pas par le SHA-256 de ses fichiers ;
- pour chaque étape : commande en variables symboliques, classe, code de sortie (lu dans `.exit`), oracle atteint ou non ;
- SHA-256 de l'archive source et de `MANIFEST.sha256` ; configuration de classification et son SHA-256 ; empreintes SHA-256 des fichiers promus, dont les binaires construits ;
- références des sorties brutes : nom, emplacement symbolique et SHA-256, le chemin réel restant dans le manifeste privé ;
- résultats des sondes S0 et de la finalisation K ;
- résultats bruts résumés ; **interprétation séparée** ; limites et anomalies.

Règles : une valeur n'est « mesurée » que si la commande a été exécutée ; une tentative ratée, invalide ou rejouée est conservée et publiée avec les autres ; une configuration ou un prototype modifié après une tentative donne une nouvelle tentative avec un nouveau `<RUN_ID>`, l'ancienne reste publiée.

### 6. Questions

Chaque série Q1 à Q6 est lancée par le mainteneur depuis le compte superviseur. Elle commence par P0, puis par les contrôles communs S0, B0, B0a, B0b et B1 du §6.5.1, et se termine par la finalisation K du même paragraphe. Les tables Q1 à Q4 ne répètent que leurs étapes métier. B2 à B11 constituent le bootstrap supplémentaire, avec réseau limité, propre à Q5 et Q6. Sauf mention « superviseur », chaque commande des tables s'exécute sous le runner.

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

Étapes de la série Q1 (commandes du runner, lancées par le superviseur ; promotion par le superviseur, §3.6) :

| Étape | Commande | Classe |
|---|---|---|
| Q1-1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/forms-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/forms/Forms.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=forms "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif"` | `must_fail(1, { HS016 × 29, sévérité Warning, aux sites canoniques du tableau ; SPKCFG × 2, sévérité Error, sur les déclarations de `Fx.Misc.Helper` et `Fx.Misc.Orphan` })` ; 0 AD0001 ; aucun autre diagnostic d'erreur. Le code 1 vient des deux `SPKCFG` bloquants |
| Q1-2 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/forms-broken-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/forms-broken/FormsBroken.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=forms-broken "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif"` | `must_fail_partial(1, requis = { CS0246 × 1 sur Missing ; 0 AD0001 }, autorisés = { HS016 Warning sur BookRoomDto })` ; présence et nombre de HS016 observés et publiés sans oracle ; aucun autre diagnostic d'erreur (entrée de Q7) |
| Q1-3 | Construction de l'hôte : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/bench-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release -o "$HS_STAGE/bin/bench"` | `must_succeed` |
| Q1-3p | Promotion par le superviseur : `promote q1-3p bin/bench` | Panne d'infrastructure si la promotion échoue ; Q1-4 et Q1-5 lancent la copie promue `$HS_RO/bin/bench/` |
| Q1-4 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/forms-exposure.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_SPIKE/fixtures/forms/Forms.slnx" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_SRC/config/forms.globalconfig" --evidence-prefix forms --out "$HS_STAGE/out/forms-exposure.csv"` | `must_succeed` (l'hôte sort en 0 malgré les `SPKCFG`, §3.5) ; oracle : colonnes `SPKOPP`, HS016 et `SPKCFG` du tableau, sans F23 |
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
| Q2-1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/cross-assembly-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental -p:HardSliceSpikeStep=cross-assembly "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif"` | `must_fail(1, { X1 : HS016 × 1, Warning ; X4 : SPKCFG × 1, Error ; X3 : aucun })` ; 0 AD0001 ; aucun autre diagnostic d'erreur |
| Q2-1p | Promotion par le superviseur : `promote q2-1p work/spikes/s1-roslyn/fixtures/cross-assembly/src/Fx.Bookings.BookRoom/bin/Debug/net10.0/Fx.Bookings.BookRoom.dll` | Panne d'infrastructure si la promotion échoue ; fige la sortie de Q2-1 avant que Q2-3 ne reconstruise le même projet |
| Q2-2 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/bookroom-pack.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_SPIKE/fixtures/cross-assembly/src/Fx.Bookings.BookRoom" -c Release -o "$HS_STAGE/feed"` | `must_succeed` |
| Q2-2p | Promotion par le superviseur : `promote q2-2p feed` | Panne d'infrastructure si la promotion échoue ; contrôle sur la copie promue : `lib/net10.0/Fx.Bookings.BookRoom.dll` présent dans le `.nupkg` ; Q2-3 lit `$HS_RO/feed` |
| Q2-3 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/cross-assembly-package-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/cross-assembly/CrossAssembly.slnx" -nologo --no-incremental -p:UsePackage=true "-p:LocalFeed=$HS_RO/feed" -p:HardSliceSpikeStep=cross-assembly-package "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif"` | `must_fail(1, { X2 : HS016 × 1, Warning ; X4 : SPKCFG × 1, Error ; X3 : aucun })` ; 0 AD0001 ; aucun autre diagnostic d'erreur |
| Q2-3p | Promotion par le superviseur : `promote q2-3p nuget/packages/fx.bookings.bookroom/1.0.0-spike/lib/net10.0/Fx.Bookings.BookRoom.dll` | Panne d'infrastructure si la promotion échoue |
| Q2-4 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/bench-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release -o "$HS_STAGE/bin/bench"` | `must_succeed` |
| Q2-4p | Promotion par le superviseur : `promote q2-4p bin/bench` | Panne d'infrastructure si la promotion échoue |
| Q2-5 | Superviseur : report dans la fiche des deux empreintes calculées par Q2-1p et Q2-3p (`tree.sha256`) | — |
| Q2-6 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/x5-projectref.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" inspect-symbols --reference "$HS_RO/work/spikes/s1-roslyn/fixtures/cross-assembly/src/Fx.Bookings.BookRoom/bin/Debug/net10.0/Fx.Bookings.BookRoom.dll" --types Fx.Features.Bookings.BookRoom.BookRoomPartial,Fx.Features.Bookings.BookRoom.BookRoomMarked,Fx.Features.Bookings.BookRoom.BookRoomGeneratedName,Fx.Features.Bookings.BookRoom.BookRoomAutoHeader --out "$HS_STAGE/out/x5-projectref.json"` | `must_succeed` ; oracle X5 |
| Q2-7 | Même commande avec `--reference "$HS_RO/nuget/packages/fx.bookings.bookroom/1.0.0-spike/lib/net10.0/Fx.Bookings.BookRoom.dll"`, log `x5-package.log`, sortie `x5-package.json` | `must_succeed` ; oracle X5 |

`inspect-symbols` crée une compilation minimale qui ne référence que l'assembly indiquée et les assemblies de référence du framework, puis relève, pour chaque type demandé : namespace, assembly, attributs, nombre de `DeclaringSyntaxReferences`, `Locations` (`IsInMetadata`, chemin éventuel). C'est exactement l'information de métadonnées dont dispose l'analyseur dans la compilation du consommateur. Le `.dll` sous `bin/Debug/net10.0/` est la sortie de Q2-1 et celui du paquet vient du restore de Q2-3 ; tous deux sont lus dans leurs copies promues par Q2-1p et Q2-3p ; la version de paquet `1.0.0-spike` est fixée par le projet de la fixture.

**Preuve** : `results/<RUN_ID>/cross-assembly.md` contient, pour X1 à X4, les diagnostics observés (extraits normalisés des SARIF) face à l'oracle, et pour X5, la table « information → attendu → observé (ProjectReference) → observé (paquet) », avec les empreintes des deux `.dll`.

**Si X1 ou X2 n'atteint pas son oracle, Q6 est `non mesuré`** (§2).

#### 6.3 Q3 — IDE et CLI (états commités `fixtures/ide/states/I1 … I7`)

L'agent **ne manipule pas l'IDE**, n'installe rien et ne déduit jamais le comportement IDE d'un succès CLI. S1-04 ne s'ouvre qu'après **PRE-S1-04** : installation par le mainteneur de VS Code stable et de C# Dev Kit stable, et relevé des versions exactes (VS Code, C# Dev Kit, extension C#, runtime .NET de l'extension) **avant le test**. Sans retour du mainteneur, S1-04 est `non mesuré`.

**États commités.** Chaque état est un dossier complet et commité, dont l'empreinte (SHA-256 de l'arborescence) figure dans la fiche. Le mainteneur ouvre dans VS Code un dossier de travail `<IDE_WORK>`, hors dépôt, défini ci-dessous (session graphique du runner). Les transitions sont appliquées, IDE ouvert, par le script versionné `tools/q3-apply-state.sh <état>`, qui copie l'état dans `<IDE_WORK>`, journalise l'heure UTC de la copie et vérifie l'empreinte. Le mainteneur chronomètre l'apparition ou la disparition du diagnostic à partir de cette heure. Les paquets locaux (version 1 et version 2 de l'analyseur, pour I7) sont produits par le runner dans `$HS_STAGE/feed`, puis promus sous `$HS_RO/feed` par `promote q3-feed feed` (§3.6) immédiatement après leur production, avant le premier build d'état.

**Observation IDE et D-18-A (point ouvert de V-S1-01).** L'IDE est une application graphique interactive : il ne passe ni par le wrapper ni par `as-runner.sh`, et il exécute l'analyseur du paquet local ainsi que ses propres outils de build. Une seule variante est compatible avec D-18-A :
- **session graphique du runner** : `<IDE_WORK>` = `$HS_STAGE/ide-work/`, `q3-apply-state.sh` lancé sous le runner. Le confinement est conservé, mais l'installation de C# Dev Kit et la session graphique modifient le `HOME` du runner, que la fin de série OP-01 doit ramener à l'état de référence ; les téléchargements de l'IDE doivent entrer dans le plan réseau d'OP-01 ; le mainteneur ferme la session graphique du runner avant la finalisation K, faute de quoi K3 n'atteint pas son oracle.

Ouvrir l'IDE dans la session du mainteneur ferait exécuter sous l'UID du superviseur l'analyseur construit par le runner et le restore de l'IDE : ce code pourrait modifier ou supprimer tous les journaux et le registre `.run-ids` de la racine `$HS_ART`, ce qui annule les garanties de D-18-A et d'A-07. Ce n'est donc pas une variante du protocole : elle exigerait un amendement de D-18 par le mainteneur. V-S1-01 valide la variante « session graphique du runner », ou acte que S1-04 reste `non mesuré`. Tant que ce point n'est pas tranché, S1-04 reste fermé, indépendamment de PRE-S1-04. Les commandes CLI ci-dessous sont des commandes du runner.

| État | Contenu | Observé dans l'IDE | CLI (build sans puis avec `-warnaserror`) et classe |
|---|---|---|---|
| I1 | Fixture sans violation, paquet v1 | Analyseur chargé depuis le paquet (nœud Analyzers ou journal du serveur de langage) ; aucun HS016 | sans : `must_succeed`, 0 HS016 ; avec : `must_succeed` |
| I2 | I1 + violation F01 | Présence, ID, emplacement, sévérité, délai d'apparition | sans : `must_succeed`, 1 HS016 warning ; avec : `must_fail(1, HS016 × 1 en erreur)` |
| I3 | Retour à I1 | Disparition, délai | comme I1 |
| I4 | I2 + `<HardSliceAnalysisLevel>1</HardSliceAnalysisLevel>` à la racine | Sévérité après rechargement ; rechargement nécessaire ou non | sans : `must_fail(1, HS016 × 1 en erreur)` ; avec : idem |
| I5 | I4 + `.editorconfig` utilisateur `dotnet_diagnostic.HS016.severity = suggestion` | Prise en compte sans redémarrage ? | sans : `must_succeed`, HS016 en `note` dans le SARIF ; avec : `must_succeed` |
| I6 | I4 + `.globalconfig` utilisateur, même réglage | idem | comme I5 |
| I7 | I2 avec le paquet v2 | Nouvelle version prise en compte (cache d'analyseurs) ? | sans : `must_succeed`, 1 HS016 ; la version de l'assembly v2 apparaît dans `ReportAnalyzer` |

Commande CLI par état : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/cli-<état>-<sans|warnaserror>.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/ide/states/<état>" -nologo --no-incremental -p:ReportAnalyzer=true "-p:LocalFeed=$HS_RO/feed" -p:HardSliceSpikeStep=cli-<état>-<sans|warnaserror> "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif" [-warnaserror]`.

#### 6.4 Q4 — `.globalconfig` par niveau et précédence (états commités `fixtures/precedence/case-a … case-h`)

Paquet local : `build/` et `buildTransitive/` contiennent un `.props`/`.targets` qui ajoute `HardSlice.Analyzers.level-1.globalconfig` à `GlobalAnalyzerConfigFiles` quand `HardSliceAnalysisLevel` vaut 1. Chaque cas contient exactement une violation F01.

Pack (`must_succeed`) : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/pack.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll pack "$HS_SPIKE/src/HardSlice.Spike.Package" -c Release -o "$HS_STAGE/feed"`, puis promotion par le superviseur `promote q4-pack feed` (§3.6) ; les cas lisent `$HS_RO/feed` et l'empreinte du `.nupkg` relevée par la promotion figure dans la fiche.

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

Commande par cas : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/precedence-<x>.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/fixtures/precedence/case-<x>" -nologo --no-incremental "-p:LocalFeed=$HS_RO/feed" -p:HardSliceSpikeStep=precedence-<x> "-p:HardSliceSpikeSarifDir=$HS_STAGE/sarif" [options du cas]`.

#### 6.5 Q5 — Performance sur nopCommerce (D-06, M-08, M-08-A1)

Exécution sous le **runner**, pilotée par le superviseur (§3.4, §3.6), lancée par le mainteneur après OP-01, par le script de série versionné qui enchaîne exactement les étapes ci-dessous. Conditions M-08 consignées par le mainteneur. Serveur de compilation laissé actif (défaut, consigné).

##### 6.5.1 Prologue et contrôles communs Q1–Q6 ; bootstrap supplémentaire Q5/Q6

**P0 — prologue exact, commun à toutes les séries (Q1 à Q6), exécuté par le superviseur avant S0.** Il sera versionné tel quel dans `tools/series-prologue.sh`, puis il appelle le corps de la série (`tools/series-<qn>.sh`, écrit avec l'échafaudage après V-S1-01, lancé depuis l'export `$HS_TOOLS`). `HS_REPO`, `HS_ART`, `HS_RUNNER`, `HS_RUNNER_HOME`, `SERIES` et `ATTEMPT` sont fournis au lancement. P0 vérifie les deux identités et l'état de référence, génère et réserve le `RUN_ID`, crée `$HS_RUN` et son manifeste privé, puis produit l'export minimal en lecture seule pour le runner. P0 ne lance aucune commande sous le runner.

**Commande de lancement obligatoire**, tapée par le mainteneur dans un terminal du compte superviseur (valeurs absolues remplacées par lui). Le prologue n'est jamais invoqué directement depuis le shell ambiant : le premier Bash lui-même démarre sous `env -i`, sans `BASH_ENV`, fonctions exportées ni options héritées.

```bash
/usr/bin/env -i \
  HOME="/Users/<superviseur>" PATH=/usr/bin:/bin:/usr/sbin:/sbin LANG=C LC_ALL=C \
  HS_REPO="/chemin/canonique/hardslice" \
  HS_ART="/Users/<superviseur>/hardslice-artifacts" \
  HS_RUNNER="<runner>" HS_RUNNER_HOME="/Users/<runner>" \
  SERIES=q1 ATTEMPT=01 \
  /bin/bash --noprofile --norc -c \
  'exec /bin/bash --noprofile --norc "$HS_REPO/spikes/s1-roslyn/tools/series-prologue.sh"'
```

```bash
umask 077
set -u
set -o pipefail
: "${HOME:?}" "${HS_REPO:?}" "${HS_ART:?}" "${HS_RUNNER:?}" "${HS_RUNNER_HOME:?}" "${ATTEMPT:?}" "${SERIES:?}"
HS_XROOT=/Users/Shared/hardslice-s1
case "$ATTEMPT" in [0-9][0-9]) ;; *) echo "ATTEMPT invalide" >&2; exit 70 ;; esac
case "$SERIES" in q1|q2|q3|q4|q5|q6) ;; *) echo "SERIES invalide" >&2; exit 70 ;; esac
case "$HS_RUNNER" in ''|*[!a-z0-9_-]*) echo "HS_RUNNER invalide" >&2; exit 70 ;; esac
case "$HS_RUNNER_HOME" in /Users/*) ;; *) echo "HS_RUNNER_HOME invalide" >&2; exit 70 ;; esac
case "$HS_RUNNER_HOME" in *[!A-Za-z0-9/_-]*) echo "HS_RUNNER_HOME invalide" >&2; exit 70 ;; esac
canonical() { [ "$(cd -- "$1" 2> /dev/null && pwd -P)" = "$1" ]; }
no_allow_acl() {
  acl=$(/bin/ls -lde "$1") || return 1
  ! printf '%s\n' "$acl" | /usr/bin/awk 'NR > 1 && / allow / { found = 1 } END { exit found ? 0 : 1 }'
}
clean_git() {
  /usr/bin/env -i \
    HOME="$HOME" PATH=/usr/bin:/bin:/usr/sbin:/sbin LANG=C LC_ALL=C \
    GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_TERMINAL_PROMPT=0 \
    /usr/bin/git "$@"
}
outside_git() {
  probe=$(clean_git -C "$1" rev-parse --git-dir 2>&1)
  probe_status=$?
  [ "$probe_status" -eq 128 ] \
    && [ "$probe" = 'fatal: not a git repository (or any of the parent directories): .git' ]
}
for p in "$HOME" "$HS_REPO" "$HS_ART"; do
  canonical "$p" || { echo "chemin non canonique : $p" >&2; exit 70; }
done
# Deux identités distinctes, non root ; le runner n'est pas administrateur.
sup_uid=$(/usr/bin/id -u) && sup_name=$(/usr/bin/id -un) \
  || { echo "identité du superviseur illisible" >&2; exit 70; }
runner_uid=$(/usr/bin/id -u "$HS_RUNNER") && runner_groups=$(/usr/bin/id -Gn "$HS_RUNNER") \
  || { echo "compte runner inconnu" >&2; exit 70; }
[ "$sup_uid" != 0 ] && [ "$runner_uid" != 0 ] && [ "$runner_uid" != "$sup_uid" ] \
  || { echo "superviseur et runner doivent être deux comptes non root distincts" >&2; exit 70; }
case " $runner_groups " in
  *" admin "*|*" wheel "*) echo "le runner appartient à admin ou wheel" >&2; exit 70 ;;
esac
runner_record=$(/usr/bin/dscl . -read "/Users/$HS_RUNNER" NFSHomeDirectory) \
  && [ "$runner_record" = "NFSHomeDirectory: $HS_RUNNER_HOME" ] \
  || { echo "HS_RUNNER_HOME ne correspond pas au compte runner" >&2; exit 70; }
sup_record=$(/usr/bin/dscl . -read "/Users/$sup_name" NFSHomeDirectory) \
  && [ "$sup_record" = "NFSHomeDirectory: $HOME" ] \
  || { echo "HOME ne correspond pas au compte superviseur" >&2; exit 70; }
case "$HS_RUNNER_HOME" in "$HOME"|"$HOME"/*) echo "HOME du runner sous celui du superviseur" >&2; exit 70 ;; esac
# Le HOME du superviseur, qui contient HS_ART, la copie HS_REPO, D-10 et D-13, n'est pas traversable.
[ "$(/usr/bin/stat -f '%u %Lp' "$HOME")" = "$sup_uid 700" ] && no_allow_acl "$HOME" \
  || { echo "le HOME du superviseur doit être en 0700, sans ACL permissive" >&2; exit 70; }
case "$HS_ART" in "$HOME"/*) ;; *) echo "HS_ART doit être un sous-dossier dédié du HOME du superviseur" >&2; exit 70 ;; esac
[ -d "$HS_ART" ] && [ ! -L "$HS_ART" ] \
  && [ "$(/usr/bin/stat -f '%u %Lp' "$HS_ART")" = "$sup_uid 700" ] && no_allow_acl "$HS_ART" \
  || { echo "HS_ART doit appartenir au superviseur, en 0700, sans ACL permissive" >&2; exit 70; }
outside_git "$HS_ART" || { echo "HS_ART est dans un dépôt Git, ou ce point n'est pas prouvable" >&2; exit 70; }
# État de référence de HS_ART : uniquement le registre, aux réservations vides.
run_registry=$HS_ART/.run-ids
if [ ! -e "$run_registry" ] && [ ! -L "$run_registry" ]; then
  [ -z "$(/bin/ls -A "$HS_ART")" ] || { echo "HS_ART non vide sans registre" >&2; exit 70; }
  /bin/mkdir -m 0700 -- "$run_registry" || exit 70
fi
[ -d "$run_registry" ] && [ ! -L "$run_registry" ] && canonical "$run_registry" \
  && [ "$(/usr/bin/stat -f '%u %Lp' "$run_registry")" = "$sup_uid 700" ] && no_allow_acl "$run_registry" \
  || { echo "registre RUN_ID invalide" >&2; exit 70; }
[ "$(/bin/ls -A "$HS_ART")" = .run-ids ] \
  || { echo "HS_ART ne doit contenir que .run-ids (état de référence)" >&2; exit 70; }
registry_bad=$(/usr/bin/find "$run_registry" -mindepth 1 -maxdepth 1 \( ! -type d -o ! -perm 700 -o ! -user "$sup_uid" \) -print) \
  && registry_nested=$(/usr/bin/find "$run_registry" -mindepth 2 -print) \
  && [ -z "$registry_bad" ] && [ -z "$registry_nested" ] \
  || { echo "registre RUN_ID hors état de référence" >&2; exit 70; }
registry_before=$(/bin/ls -1 "$run_registry") || exit 70
# Racine d'échange : seul emplacement partagé avec le runner, en lecture seule pour lui.
[ "$(/usr/bin/stat -f '%u %Mp%Lp' /Users/Shared)" = "0 1777" ] \
  || { echo "/Users/Shared doit appartenir à root, en 1777" >&2; exit 70; }
if [ ! -e "$HS_XROOT" ] && [ ! -L "$HS_XROOT" ]; then
  /bin/mkdir -m 0755 -- "$HS_XROOT" || exit 70
fi
[ -d "$HS_XROOT" ] && [ ! -L "$HS_XROOT" ] && canonical "$HS_XROOT" \
  && [ "$(/usr/bin/stat -f '%u %Mp%Lp' "$HS_XROOT")" = "$sup_uid 0755" ] && no_allow_acl "$HS_XROOT" \
  && [ -z "$(/bin/ls -A "$HS_XROOT")" ] \
  || { echo "racine d'échange hors état de référence" >&2; exit 70; }
outside_git "$HS_XROOT" || { echo "HS_XROOT est dans un dépôt Git, ou ce point n'est pas prouvable" >&2; exit 70; }
# Commit mesuré.
repo_top=$(clean_git -C "$HS_REPO" rev-parse --show-toplevel) \
  || { echo "racine Git illisible" >&2; exit 70; }
[ "$repo_top" = "$HS_REPO" ] || { echo "HS_REPO n'est pas la racine du worktree" >&2; exit 70; }
repo_head=$(clean_git -C "$HS_REPO" rev-parse --verify 'HEAD^{commit}') \
  || { echo "HEAD illisible" >&2; exit 70; }
repo_short=$(clean_git -C "$HS_REPO" rev-parse --short=12 "$repo_head") && [ "${#repo_short}" -eq 12 ] \
  || { echo "SHA court illisible ou ambigu" >&2; exit 70; }
repo_status=$(clean_git -C "$HS_REPO" status --porcelain=v1 --untracked-files=all) \
  || { echo "statut Git illisible" >&2; exit 70; }
[ -z "$repo_status" ] || { echo "worktree HardSlice non propre" >&2; exit 70; }
# RUN_ID, réservation, puis manifeste immédiatement : la fenêtre « réservé sans manifeste » est réduite à deux mkdir.
question=$(printf '%s' "$SERIES" | /usr/bin/tr '[:lower:]' '[:upper:]')
run_stamp=$(/bin/date -u +%Y%m%dT%H%M%SZ) || { echo "heure UTC illisible" >&2; exit 70; }
uuid_raw=$(/usr/bin/uuidgen) || { echo "UUID impossible" >&2; exit 70; }
run_nonce=$(printf '%s' "$uuid_raw" | /usr/bin/tr -d '-' | /usr/bin/tr '[:upper:]' '[:lower:]')
[ "${#run_nonce}" -eq 32 ] || { echo "UUID invalide" >&2; exit 70; }
case "$run_nonce" in *[!0-9a-f]*) echo "UUID invalide" >&2; exit 70 ;; esac
RUN_ID=${run_stamp}-${question}-${repo_short}-a${ATTEMPT}-u${run_nonce}
/bin/mkdir -m 0700 -- "$run_registry/$RUN_ID" || { echo "RUN_ID déjà utilisé" >&2; exit 70; }
HS_RUN=$HS_ART/$RUN_ID
manifest=$HS_RUN/manifest.private.md
/bin/mkdir -m 0700 -- "$HS_RUN" || exit 70
( set -o noclobber
  { printf '# manifest.private.md - hors git\n'
    printf 'RUN_ID=%s\nSERIES=%s\nATTEMPT=%s\nstart_utc=%s\nhost=%s\n' \
      "$RUN_ID" "$SERIES" "$ATTEMPT" "$(/bin/date -u +%Y-%m-%dT%H:%M:%SZ)" "$(/usr/bin/uname -a)"
    printf 'supervisor=%s uid=%s\nrunner=%s uid=%s groups=%s\n' \
      "$sup_name" "$sup_uid" "$HS_RUNNER" "$runner_uid" "$runner_groups"
    printf 'HS_REPO=%s\nHS_REPO_HEAD=%s\nHS_ART=%s\nHS_RUN=%s\nHS_XROOT=%s\nHS_RUNNER_HOME=%s\n' \
      "$HS_REPO" "$repo_head" "$HS_ART" "$HS_RUN" "$HS_XROOT" "$HS_RUNNER_HOME"
    printf 'registry_before:\n'
    if [ -n "$registry_before" ]; then printf '%s\n' "$registry_before" | /usr/bin/sed 's/^/  reserved /'; fi
  } > "$manifest" ) || exit 70
HS_EXPORT=$HS_XROOT/$RUN_ID
HS_SRC=$HS_EXPORT/src/spikes/s1-roslyn
HS_RO=$HS_EXPORT/ro
HS_TOOLS=$HS_SRC/tools
HS_STAGE=$HS_RUNNER_HOME/hardslice-stage
HS_SPIKE=$HS_STAGE/work/spikes/s1-roslyn
HS_CLONES=$HS_STAGE/clones
for d in tmp source snapshots collected; do
  /bin/mkdir -m 0700 -- "$HS_RUN/$d" || exit 70
done
# Export minimal : spikes/s1-roslyn au commit mesuré, sans .git ni docs/, lisible mais non inscriptible par le runner.
spike_archive=$HS_RUN/source/spike-source.tar
( set -o noclobber
  clean_git -C "$HS_REPO" archive --format=tar "$repo_head" spikes/s1-roslyn > "$spike_archive" ) \
  || { echo "export du spike impossible" >&2; exit 70; }
/bin/mkdir -m 0755 -- "$HS_EXPORT" "$HS_EXPORT/src" "$HS_RO" || exit 70
( umask 022 && /usr/bin/tar -xf "$spike_archive" -C "$HS_EXPORT/src" ) \
  || { echo "extraction de l'export impossible" >&2; exit 70; }
[ -d "$HS_SRC" ] && canonical "$HS_SRC" \
  && [ "$(/bin/ls -A "$HS_EXPORT/src")" = spikes ] && [ "$(/bin/ls -A "$HS_EXPORT/src/spikes")" = s1-roslyn ] \
  || { echo "export minimal invalide" >&2; exit 70; }
export_bad=$(/usr/bin/find "$HS_EXPORT" \( ! -type f ! -type d -o -perm -020 -o -perm -002 -o ! -user "$sup_uid" \) -print) \
  && [ -z "$export_bad" ] \
  || { echo "export : lien, droit d'écriture ou propriétaire interdit" >&2; exit 70; }
( umask 022 && cd "$HS_SRC" \
    && /usr/bin/find . -type f -exec /usr/bin/shasum -a 256 {} + \
    | LC_ALL=C /usr/bin/sort -k 2 > "$HS_EXPORT/MANIFEST.sha256" ) \
  && ( umask 022 && printf '%s\n' "$repo_head" > "$HS_EXPORT/SOURCE" ) \
  || { echo "manifeste de l'export impossible" >&2; exit 70; }
spike_archive_sha=$(/usr/bin/shasum -a 256 "$spike_archive") \
  && export_manifest_sha=$(/usr/bin/shasum -a 256 "$HS_EXPORT/MANIFEST.sha256") \
  || { echo "empreintes de l'export impossibles" >&2; exit 70; }
{ printf 'SPIKE_ARCHIVE_SHA256=%s\nEXPORT_MANIFEST_SHA256=%s\n' "${spike_archive_sha%% *}" "${export_manifest_sha%% *}"
  printf 'HS_EXPORT=%s\nHS_SRC=%s\nHS_RO=%s\nHS_TOOLS=%s\nHS_STAGE=%s\nHS_SPIKE=%s\nHS_CLONES=%s\n' \
    "$HS_EXPORT" "$HS_SRC" "$HS_RO" "$HS_TOOLS" "$HS_STAGE" "$HS_SPIKE" "$HS_CLONES"
} >> "$manifest" || exit 70
exec /usr/bin/env -i \
  HOME="$HOME" PATH=/usr/bin:/bin:/usr/sbin:/sbin TMPDIR="$HS_RUN/tmp" \
  LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 \
  GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null GIT_TERMINAL_PROMPT=0 \
  HS_REPO="$HS_REPO" HS_ART="$HS_ART" HS_RUN="$HS_RUN" HS_XROOT="$HS_XROOT" \
  HS_EXPORT="$HS_EXPORT" HS_SRC="$HS_SRC" HS_RO="$HS_RO" HS_TOOLS="$HS_TOOLS" \
  HS_RUNNER="$HS_RUNNER" HS_RUNNER_UID="$runner_uid" HS_RUNNER_HOME="$HS_RUNNER_HOME" \
  HS_STAGE="$HS_STAGE" HS_SPIKE="$HS_SPIKE" HS_CLONES="$HS_CLONES" RUN_ID="$RUN_ID" \
  /bin/bash --noprofile --norc "$HS_TOOLS/series-$SERIES.sh"
```

Le script de série commence par `umask 077`, `set -u` et `set -o pipefail`, puis charge `"$HS_TOOLS/series-lib.sh"` (§3.6). Il enchaîne S0, B0, B0a, B0b et B1, le bootstrap éventuel, les étapes métier, puis la finalisation K. Dès qu'une étape arrête la série, les étapes métier restantes ne s'exécutent pas ; K s'exécute alors aussi, à condition que S0-7 ait créé le staging de cette tentative.

**Échecs de P0, trois classes (A-07).** Une sortie non nulle de P0 est une panne d'infrastructure : aucune commande du runner ne s'exécute.
- **(a) Avant la création de `$HS_ART/.run-ids/<RUN_ID>/`** : échec de lancement, sans `RUN_ID` réservé, sans tentative ni publication, consigné dans l'attestation OP-01.
- **(b) Après la réservation mais avant `manifest.private.md`** (échec de `mkdir "$HS_RUN"` ou de l'écriture du manifeste) : la réservation reste conservée et la tentative est invalide. Le mainteneur crée, sous l'identité du superviseur, un **manifeste privé de récupération** directement dans le dossier D-10 exclusif de ce `RUN_ID`, selon OP-01, point 16, avant de ranger ou supprimer tout `$HS_RUN` partiel.
- **(c) Après la création du manifeste P0** : la tentative invalide suit le rangement normal de `$HS_RUN` ; un `$HS_EXPORT` partiel est supprimé par le superviseur après inventaire consigné dans le manifeste. Aucun staging n'existe encore, puisque P0 ne lance rien sous le runner.

Avant toute nouvelle série, P0 refuse de démarrer tant que `$HS_ART` contient autre chose que `.run-ids/` ou que `$HS_XROOT` n'est pas vide, et S0-7 refuse un staging préexistant. Le registre `.run-ids` est préservé par la fin de série OP-01. L'archive source et son empreinte restent sous `$HS_RUN/source/` ; toutes les compilations du spike travaillent dans `$HS_SPIKE`, sous le staging : aucune ne travaille dans `$HS_REPO` ni dans l'export.

**Contrôles communs Q1 à Q6, après P0** : S0 (identités, isolation et staging), puis B0, B0a, B0b et B1. Dans les commandes S0, les variables hors apostrophes sont développées par le script de série, donc par le superviseur ; `$HOME` y désigne le `HOME` du superviseur. Les variables entre apostrophes sont développées par le shell du runner, à partir de sa liste blanche.

| Étape | Commande | Classe |
|---|---|---|
| S0-1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-1-runner-identity.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/id -un` | `must_succeed` ; corps = exactement `$HS_RUNNER`, sans retour chariot : la délégation `sudo -n` fonctionne sans mot de passe et ne transforme pas la sortie |
| S0-2 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-2-runner-home.log" -- "$HS_TOOLS/as-runner.sh" /bin/sh -c 'cd "$HOME" && [ "$(pwd -P)" = "$HOME" ]'` | `must_succeed` ; corps vide : le `HOME` du runner est canonique |
| S0-3 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-3-supervisor-home.log" -- "$HS_TOOLS/as-runner.sh" /bin/test -x "$HOME"` | `must_fail(1, ∅)` ; corps vide : le `HOME` du superviseur n'est pas traversable par le runner |
| S0-4 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-4-no-repo.log" -- "$HS_TOOLS/as-runner.sh" /bin/test -e "$HS_REPO/.git"` | `must_fail(1, ∅)` ; corps vide : ni le dépôt complet ni `.git` ne sont accessibles au runner |
| S0-5 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-5-no-registry.log" -- "$HS_TOOLS/as-runner.sh" /bin/test -e "$HS_ART/.run-ids"` | `must_fail(1, ∅)` ; corps vide : le registre et les preuves sont hors d'atteinte du runner |
| S0-6 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-6-export-readonly.log" -- "$HS_TOOLS/as-runner.sh" /bin/test -w "$HS_EXPORT"` | `must_fail(1, ∅)` ; corps vide : l'export n'est pas inscriptible par le runner (P0 a vérifié qu'aucune entrée de l'export ne l'est) |
| S0-7 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-7-stage.log" -- "$HS_TOOLS/as-runner.sh" /bin/mkdir -m 0700 "$HS_STAGE"` | `must_succeed` ; création exclusive : un staging préexistant arrête la série sans finalisation K, car il n'appartient pas à cette tentative (OP-01, point 16) |
| S0-8 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-8-stage-tree.log" -- "$HS_TOOLS/as-runner.sh" /bin/mkdir -m 0700 "$HS_STAGE/work" "$HS_STAGE/work/spikes" "$HS_STAGE/tmp" "$HS_STAGE/dotnet-home" "$HS_STAGE/nuget" "$HS_STAGE/nuget/packages" "$HS_STAGE/nuget/http-cache" "$HS_STAGE/nuget/plugins-cache" "$HS_STAGE/bin" "$HS_STAGE/sarif" "$HS_STAGE/feed" "$HS_STAGE/clones" "$HS_STAGE/out"` | `must_succeed` ; corps vide |
| S0-9 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-9-working-copy.log" -- "$HS_TOOLS/as-runner.sh" /bin/cp -R "$HS_SRC" "$HS_STAGE/work/spikes/"` | `must_succeed` ; corps vide |
| S0-10 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-10-working-copy-sha256.log" -- "$HS_TOOLS/as-runner.sh" /bin/sh -c 'cd "$HS_SPIKE" && exec /usr/bin/shasum -a 256 -c --quiet --strict "$HS_EXPORT/MANIFEST.sha256"'` | `must_succeed` ; corps vide : la copie de travail `$HS_SPIKE` est identique à l'export, avant toute exécution de code tiers dans la tentative |
| S0-11 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-11-no-tty.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/perl -e 'open(my $t, "<", "/dev/tty") and exit 0; exit 1'` | `must_fail(1, ∅)` ; corps vide : le runner n'a pas de terminal de contrôle |
| S0-12 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-12-descendant.log" -- "$HS_TOOLS/as-runner.sh" /bin/sh -c '( /bin/sleep 2; echo late ) & echo early'` | `must_succeed` ; corps = `early` puis `late` : la sortie d'un descendant qui survit à la commande traverse `sudo` et précède le pied |
| S0-13 | Superviseur : canari aléatoire (`/usr/bin/uuidgen`) consigné dans le manifeste et placé dans son presse-papiers par `/usr/bin/pbcopy` (le contenu antérieur du presse-papiers est perdu), puis `"$HS_TOOLS/run-logged.sh" "$HS_RUN/s0-13-pasteboard.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/pbpaste` | `informative` sur le code ; oracle bloquant sur le corps : le canari est absent, sinon arrêt et tentative invalide |
| B0 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b0-sdk-version.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll --version` | `must_succeed` ; corps = `10.0.102` |
| B0a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b0a-environment.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/env` | `must_succeed` ; corps = exactement les variables de la colonne « runner » de la liste blanche (§3.4), sans aucune autre : aucun shell ne s'intercale entre `env -i` et `/usr/bin/env` |
| B0b | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b0b-nuget-sources.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll nuget list source` | `must_succeed` ; oracle : seule source activée `https://api.nuget.org/v3/index.json` (OP-01, point 7) |
| B1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b1-wrapper-tests.log" -- "$HS_TOOLS/test-run-logged.sh"` | Commande du superviseur, qui exécute le wrapper. `must_succeed` ; dernière ligne du corps `passed=N failed=0` |

**Bootstrap supplémentaire avec réseau limité, avant la coupure — Q5 et Q6 seulement**, chacun dans son propre `$HS_RUN`. **Q6 est autonome**, que Q5 ait été exécutée ou non.

| Étape | Commande | Classe |
|---|---|---|
| B2 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b2-analyzer-restore.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_SPIKE/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj"` | `must_succeed` |
| B3 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b3-analyzer-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/src/HardSlice.Spike.Analyzer/HardSlice.Spike.Analyzer.csproj" -c Release --no-restore -o "$HS_STAGE/bin/analyzer"` | `must_succeed` |
| B3p | Promotion par le superviseur : `promote b3p bin/analyzer` (§3.6) | Panne d'infrastructure si la promotion échoue ; l'analyseur est figé avant que B4 et B5 n'exécutent du code de paquets |
| B4 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b4-bench-restore.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj"` | `must_succeed` |
| B5 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b5-bench-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_SPIKE/bench/HardSlice.Spike.Bench/HardSlice.Spike.Bench.csproj" -c Release --no-restore -o "$HS_STAGE/bin/bench"` | `must_succeed` |
| B6 | Promotion par le superviseur : `promote b6 bin/bench` (§3.6) | Panne d'infrastructure si la promotion échoue ; empreintes de tous les fichiers promus par B3p et B6 (`tree.sha256`, dont `HardSlice.Spike.Analyzer.dll`, les `.dll` de l'hôte, `runtimeconfig.json` et `deps.json`) reportées dans le manifeste et la fiche ; toutes les étapes suivantes utilisent ces copies en lecture seule |
| B7–B9 | Sous-séquence **N** (nopCommerce, Q5) ou **F** (FSH, Q6), ci-dessous | voir la sous-séquence |
| B9n | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b9n-clone-nuget-configs.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/find "$HS_CLONES" -iname nuget.config -type f` | `must_succeed` ; le corps liste les fichiers, puis le superviseur en prend l'instantané `b9n` (chemins relatifs à `$HS_STAGE`) et relève leurs sources dans la fiche ; une source non publique arrête la série avant B10 (OP-01, point 7) |
| B10 | Restore de la solution externe : §6.5.2 (nopCommerce) ou §6.6 (FSH) | voir la section |
| B11 | **Coupure réseau** par le mainteneur selon le plan OP-01, heure consignée | — |

**Sous-séquence N — nopCommerce** (`https://github.com/nopSolutions/nopCommerce.git` @ `221305873bc81c47981b2e510b2e9bed7e5b2076`) :

| Étape | Commande | Classe |
|---|---|---|
| N7a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n7a-git-init.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git init --quiet "$HS_CLONES/nopCommerce"` | `must_succeed` |
| N7b | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n7b-git-remote.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" remote add origin https://github.com/nopSolutions/nopCommerce.git` | `must_succeed` |
| N7c | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n7c-git-fetch.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" fetch --depth 1 origin 221305873bc81c47981b2e510b2e9bed7e5b2076` | `must_succeed` |
| N7d | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n7d-git-checkout.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" checkout --quiet --detach FETCH_HEAD` | `must_succeed` |
| N8a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n8a-head.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" rev-parse HEAD` | `must_succeed` ; corps = `221305873bc81c47981b2e510b2e9bed7e5b2076` |
| N8b | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n8b-origin.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" remote get-url origin` | `must_succeed` ; corps = `https://github.com/nopSolutions/nopCommerce.git` |
| N8c | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n8c-status.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/nopCommerce" status --porcelain` | `must_succeed` ; corps vide |
| N9 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/n9-external-sdk.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild "$HS_CLONES/nopCommerce/src/Libraries/Nop.Core/Nop.Core.csproj" -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath` | `must_succeed` ; `NETCoreSdkVersion` = `10.0.102`, `RoslynTargetsPath` commence par `/usr/local/share/dotnet/sdk/10.0.102/` |

**Sous-séquence F — FSH** (`https://github.com/fullstackhero/dotnet-starter-kit.git` @ `a967537526f3b769c7d268da3ed2a6920af65ce9`) :

| Étape | Commande | Classe |
|---|---|---|
| F7a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f7a-git-init.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git init --quiet "$HS_CLONES/dotnet-starter-kit"` | `must_succeed` |
| F7b | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f7b-git-remote.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" remote add origin https://github.com/fullstackhero/dotnet-starter-kit.git` | `must_succeed` |
| F7c | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f7c-git-fetch.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" fetch --depth 1 origin a967537526f3b769c7d268da3ed2a6920af65ce9` | `must_succeed` |
| F7d | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f7d-git-checkout.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" checkout --quiet --detach FETCH_HEAD` | `must_succeed` |
| F8a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f8a-head.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" rev-parse HEAD` | `must_succeed` ; corps = `a967537526f3b769c7d268da3ed2a6920af65ce9` |
| F8b | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f8b-origin.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" remote get-url origin` | `must_succeed` ; corps = `https://github.com/fullstackhero/dotnet-starter-kit.git` |
| F8c | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f8c-status.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/git -C "$HS_CLONES/dotnet-starter-kit" status --porcelain` | `must_succeed` ; corps vide |
| F9 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/f9-external-sdk.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild "$HS_CLONES/dotnet-starter-kit/src/Modules/Catalog/Modules.Catalog.Contracts/Modules.Catalog.Contracts.csproj" -getProperty:NETCoreSdkVersion -getProperty:RoslynTargetsPath` | `must_succeed` ; `NETCoreSdkVersion` = `10.0.102`, `RoslynTargetsPath` commence par `/usr/local/share/dotnet/sdk/10.0.102/` |

Les mêmes vérifications N8 et F8 sont rejouées en fin de série (étapes `N8a′` à `N8c′`, `F8a′` à `F8c′`, logs suffixés `-end`). Après B11, aucune commande ne restaure ni ne compile le prototype : les builds externes utilisent `--no-restore`, et l'hôte est lancé depuis `$HS_RO/bin/bench/HardSlice.Spike.Bench.dll`. Les targets d'injection reçoivent `-p:HardSliceSpikeAnalyzerPath=$HS_RO/bin/analyzer/HardSlice.Spike.Analyzer.dll`.

**Finalisation K, commune à toutes les séries (Q1 à Q6)**, exécutée par le script de série après la dernière étape ou dès le premier arrêt, si S0-7 a créé le staging de la tentative. K5a à K8 ne s'exécutent que si K3 atteint son oracle et si K4 réussit ; sinon le staging et l'export restent en place, la tentative est invalide et la série suivante est bloquée jusqu'au traitement OP-01 (point 16).

| Étape | Commande | Classe |
|---|---|---|
| K1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k1-build-server-shutdown.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build-server shutdown` | `informative` |
| K2 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k2-runner-kill.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/pkill -KILL -U "$HS_RUNNER_UID"` | `informative` (0 si des processus restaient, 1 sinon) |
| K3 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k3-runner-processes.log" -- /bin/ps -A -o uid=,pid=,comm=` | Commande du superviseur. `must_succeed` ; oracle : aucune ligne du corps dont le premier champ vaut `$HS_RUNNER_UID` |
| K4 | Collecte par le superviseur : `collect` (§3.6) | Panne d'infrastructure si la collecte échoue |
| K5a | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k5a-stage-flags.log" -- "$HS_TOOLS/as-runner.sh" /usr/bin/chflags -R nouchg,nouappnd "$HS_STAGE"` | `must_succeed` ; retire les drapeaux d'immutabilité qu'une commande du runner aurait pu poser |
| K5 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k5-stage-remove.log" -- "$HS_TOOLS/as-runner.sh" /bin/rm -rf "$HS_STAGE"` | `must_succeed` |
| K6 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k6-stage-absent.log" -- "$HS_TOOLS/as-runner.sh" /bin/test -e "$HS_STAGE"` | `must_fail(1, ∅)` ; corps vide |
| K7 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k7-export-remove.log" -- /bin/rm -rf "$HS_EXPORT"` | Commande du superviseur. `must_succeed` |
| K8 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/k8-xroot-empty.log" -- /bin/ls -A "$HS_XROOT"` | Commande du superviseur. `must_succeed` ; corps vide |

Si le script de série est interrompu avant la fin de K (arrêt forcé, panne de la machine), le mainteneur lance `tools/series-finalize.sh`, versionné avec l'échafaudage après V-S1-01 : démarré comme P0 par `env -i`, avec `HS_ART`, `HS_RUNNER`, `HS_RUNNER_HOME` et `RUN_ID`, il relit les variables `HS_*` dans le manifeste privé de la tentative et exécute K1 à K8 avec les mêmes fonctions, sous de nouveaux noms de log suffixés `-finalize` et avec `collect -finalize`, de sorte qu'une collecte K4 déjà commencée n'est jamais écrasée.

Après K, la **fin de série OP-01** (points 14 à 17) est réalisée et attestée par le mainteneur : contrôle des journaux K, inventaire du runner, rangement de `$HS_RUN` sous l'emplacement D-10, retour à l'état de référence et croissance append-only de `.run-ids`. L'agent n'exécute aucune de ces opérations.

##### 6.5.2 Restore, injection et classification pour nopCommerce

**Injection sans modifier la solution.** `"-p:CustomAfterMicrosoftCommonTargets=$HS_SRC/inject/HardSlice.Spike.Inject.targets"` :
- le fichier ajoute `<CompilerVisibleProperty Include="NETCoreSdkVersion" />` ; il ajoute le `.globalconfig` désigné par `HardSliceSpikeClassification` quand cette propriété est définie ;
- il n'ajoute l'analyseur `$(HardSliceSpikeAnalyzerPath)` que si `HardSliceSpikeInjectAnalyzer=true` ;
- les builds de référence passent par la même injection avec `HardSliceSpikeInjectAnalyzer=false`, de sorte que seul l'analyseur diffère ;
- contrôle : l'analyseur figure dans la sortie `ReportAnalyzer` de chaque projet C# de chaque build `H`, et d'aucun build `R`. Sinon la tentative est invalide.

B10 (`must_succeed`) : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/b10-restore.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/nopCommerce/src/NopCommerce.sln" "-p:CustomAfterMicrosoftCommonTargets=$HS_SRC/inject/HardSlice.Spike.Inject.targets"`.

**Classification « pire cas », fermée, produite de façon déterministe après B11.** `SPKCFG` étant bloquant, une classification incomplète ferait échouer les builds `H` ; elle est donc générée par un algorithme versionné et pré-enregistré, puis hachée avant toute mesure :
- K2 = `AssemblyName` de tous les projets C# de `NopCommerce.sln` ; K6 vide ;
- K3 = `{ Nop, 2 }` : chaque namespace `Nop.<A>.<B>` et ses descendants forment une slice ;
- K5 = `{ Nop, exact }` et `{ Nop.<A>, exact }` pour chaque namespace de ce niveau qui déclare des types ; `{ <global>, exact }` si le namespace global déclare des types ; `{ <X>, subtree }` pour chaque namespace racine `<X>` autre que `Nop` qui déclare des types.

Elle maximise les opportunités et les diagnostics HS016 créés, et laisse **0 `SPKCFG`** attendu. Les diagnostics HS016 émis sur nopCommerce ne sont **pas** triés (la solution n'est pas organisée en slices) ; ils sont comptés.

| Étape | Commande | Classe |
|---|---|---|
| C1 | `"$HS_TOOLS/run-logged.sh" "$HS_RUN/c1-classify.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" classify-worst-case --solution "$HS_CLONES/nopCommerce/src/NopCommerce.sln" --inject "$HS_SRC/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --root Nop --depth 2 --evidence-prefix c1 --out "$HS_STAGE/out/nopcommerce.globalconfig"` | `must_succeed` ; mêmes contrôles de chargement que §6.5.4, points 1 à 6 |
| C2 | Promotion par le superviseur : `promote c2 out/nopcommerce.globalconfig` (§3.6) | Panne d'infrastructure si la promotion échoue ; empreinte reportée dans le manifeste et la fiche avant tout build mesuré ; les builds `R`/`H` et `perf` lisent la copie promue `$HS_RO/out/nopcommerce.globalconfig` |

##### 6.5.3 (1) Part de HS016 dans la compilation — budget ≤ 5 %

1. Build `<k>-<X>` (`must_succeed` ; un `SPKCFG` ferait échouer le build `H`, donc la tentative), avec `<X>` = `R` (`<inj>` = `false`) ou `H` (`<inj>` = `true`) :
   `"$HS_TOOLS/run-logged.sh" "$HS_RUN/build-<k>-<X>.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/nopCommerce/src/NopCommerce.sln" -c Release --no-restore --no-incremental -nologo -p:ReportAnalyzer=true "-p:CustomAfterMicrosoftCommonTargets=$HS_SRC/inject/HardSlice.Spike.Inject.targets" "-p:HardSliceSpikeAnalyzerPath=$HS_RO/bin/analyzer/HardSlice.Spike.Analyzer.dll" "-p:HardSliceSpikeClassification=$HS_RO/out/nopcommerce.globalconfig" -p:HardSliceSpikeInjectAnalyzer=<inj> "-bl:$HS_STAGE/out/build-<k>-<X>.binlog" "-flp:logfile=$HS_STAGE/out/build-<k>-<X>.msbuild.log;verbosity=detailed;performancesummary"`
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

Commande (réseau coupé, `must_succeed`) : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/perf.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" perf --solution "$HS_CLONES/nopCommerce/src/NopCommerce.sln" --inject "$HS_SRC/inject/HardSlice.Spike.Inject.targets" --classification "$HS_RO/out/nopcommerce.globalconfig" --sdk /usr/local/share/dotnet/sdk/10.0.102 --warmup 2 --reps 10 --evidence-prefix perf --out "$HS_STAGE/out/perf.csv"`.

Chargement :
1. `MSBuildLocator.RegisterMSBuildPath("/usr/local/share/dotnet/sdk/10.0.102")` avant tout chargement MSBuild ; `MSBuildSDKsPath` vient de l'environnement en liste blanche.
2. `MSBuildWorkspace.Create(properties)` avec `Configuration=Release`, `TargetFramework=net10.0`, `CustomAfterMicrosoftCommonTargets=$HS_SRC/inject/HardSlice.Spike.Inject.targets`, `HardSliceSpikeClassification=$HS_RO/out/nopcommerce.globalconfig`, `HardSliceSpikeInjectAnalyzer=false` ; `await workspace.OpenSolutionAsync(sln)`.
3. **Échec immédiat (code 2, aucune mesure)** si `workspace.Diagnostics` contient au moins une entrée, quel que soit son `WorkspaceDiagnosticKind` ; toutes les entrées sont écrites dans la sortie. Un assouplissement exigerait une nouvelle validation explicite.
4. **Contrôle du SDK effectif** : pour chaque projet, `project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.NETCoreSdkVersion", out v)` doit donner `10.0.102` ; sinon code 2.
5. **Contrôle des documents, sans faux positif sur les fichiers générés par le SDK** : les commandes `classify-worst-case`, `perf` et `exposure` exigent `--evidence-prefix <préfixe>`, validé par l'hôte avec `[a-z0-9-]+`. Pour chaque projet, la commande `"$HS_TOOLS/run-logged.sh" "$HS_RUN/<préfixe>-compile-items-<projet>.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll msbuild <csproj> -getItem:Compile -getProperty:BaseIntermediateOutputPath -p:Configuration=Release -p:TargetFramework=net10.0 "-p:CustomAfterMicrosoftCommonTargets=$HS_SRC/inject/HardSlice.Spike.Inject.targets"` (`must_succeed`) produit les entrées évaluées et la racine intermédiaire dans le même TFM que `MSBuildWorkspace`. Les préfixes sont uniques dans une tentative (`forms`, `forms-broken`, `c1`, `perf`, `exposure-fsh`, et `exposure-fdmm` si l'exploratoire est lancé) : aucun log exclusif n'est réutilisé. Après normalisation :
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

Exécution sous le **runner**, pilotée par le superviseur (§3.4, §3.6), lancée par le mainteneur après OP-01, sous son propre `<RUN_ID>`. Elle commence par le prologue P0, les contrôles S0, B0, B0a, B0b et B1, puis le bootstrap complet du §6.5.1 avec la **sous-séquence F** (`https://github.com/fullstackhero/dotnet-starter-kit.git` @ `a967537526f3b769c7d268da3ed2a6920af65ce9`, dans `$HS_CLONES/dotnet-starter-kit`). Elle ne réutilise aucun binaire ni aucune sortie de Q5.

**Restore et audit (B10 pour FSH).**
1. Audit **informatif**, réseau limité : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/fsh-restore-audit.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll restore "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx"`. Classe `informative` : son code non nul n'interrompt pas la série. Le code et les avis NU1902/NU1903 (paquet, version, identifiant d'avis) sont publiés dans la fiche.
2. Restore **mesuré** (`must_succeed`), réseau limité : même commande avec `-p:NuGetAudit=false`, log `fsh-restore.log`. **Cette option sert uniquement à éviter que les avis de vulnérabilité soient promus en erreur** par la configuration du dépôt (constaté à la présélection). Elle désactive l'audit pendant le restore, sans changer ni les paquets résolus ni les entrées de la compilation. L'audit de l'étape 1 reste publié.
3. B11 : coupure du réseau.
4. Build de contrôle (`must_succeed`) : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/fsh-build.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet /usr/local/share/dotnet/sdk/10.0.102/dotnet.dll build "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" -c Release -nologo --no-restore --no-incremental -p:NuGetAudit=false`.

**Configuration de classification FSH**, écrite, commitée et hachée **avant** le run (`config/fsh.globalconfig`), selon ces principes :
- K2 = `AssemblyName` des projets `Modules.<M>` et `Modules.<M>.Contracts` des 10 modules. Les projets de tests, `Architecture.Tests` et les outils ne sont pas dans K2, donc hors périmètre ; K6 est vide.
- K3 : une entrée par module, `{ FSH.Modules.<M>.Features.v1, depth }`. `depth` = 2 pour les modules qui ont un niveau `Area` (Identity, Catalog, Chat, Billing, Tickets), 1 pour les autres (Auditing, Files, Multitenancy, Notifications, Webhooks). À confirmer par l'inventaire des namespaces fait au moment d'écrire la configuration, et publié avec elle.
- K5 : `{ FSH.Modules.<M>.Contracts, subtree }` (contrat public du module) ; `{ FSH.Modules.<M>.Domain, subtree }` ; `{ FSH.Modules.<M>.Features.v1.Internal, subtree }` ; la racine de composition et les namespaces transverses identifiés dans l'inventaire.
- Toute entrée ajoutée ou modifiée après le run donne une nouvelle tentative ; toutes sont publiées.

Exposition (réseau coupé, `must_succeed`) : `"$HS_TOOLS/run-logged.sh" "$HS_RUN/exposure-fsh.log" -- "$HS_TOOLS/as-runner.sh" /usr/local/share/dotnet/dotnet "$HS_RO/bin/bench/HardSlice.Spike.Bench.dll" exposure --solution "$HS_CLONES/dotnet-starter-kit/src/FSH.Starter.slnx" --inject "$HS_SRC/inject/HardSlice.Spike.Inject.targets" --sdk /usr/local/share/dotnet/sdk/10.0.102 --classification "$HS_SRC/config/fsh.globalconfig" --evidence-prefix exposure-fsh --out "$HS_STAGE/out/exposure-fsh.csv"`. Mêmes contrôles de chargement que Q5 (2), points 1 à 6.

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
| 2026-10-29 | non relevé (premier horodatage 09:24:58 CET) | 09:29 CET | inconnue, au moins ≈ 4 min | S1-01 | Correction A-07 : état de référence propre du compte dédié, registre `.run-ids` persistant et append-only, preuve structurelle, récupération des échecs P0 et quarantaine d'une racine compromise ; deux contre-relectures ciblées, hors mesure |
| 2026-10-29 | non relevé (premier horodatage 09:45:46 CET) | 10:18 CET | inconnue, au moins ≈ 32 min | S1-01 | Amendement D-18-A demandé par A-08 : superviseur et runner, délégation `as-runner.sh`, P0 réécrit, sondes S0, export minimal, instantanés, promotions et collecte, finalisation K, OP-01 amendée, variante IDE de Q3 ouverte ; relecture indépendante ciblée sécurité et cohérence puis corrections (détachement du terminal, promotions immédiates, instantanés d'oracle, IDE de Q3), test du wrapper seul, hors mesure |

### 9. Répartition de la timebox (proposition soumise à V-S1-01)

Total inchangé : **3 j-p = 21 h** de temps focalisé humain. S1-07 passe de 0,25 à 0,5 j-p ; la différence est prise sur S1-05 et S1-06. Sous D-18-A, chaque série est scriptée par l'agent et lancée par le mainteneur depuis le compte superviseur, ses commandes s'exécutant sous le runner ; seul le temps d'attention du mainteneur (préparation, lancement, contrôle, relecture) compte, et la durée de calcul va au journal compute.

| Ticket | Backlog | Proposé | Heures | Raison |
|---|---:|---:|---:|---|
| S1-01 | 0,25 | 0,25 | 1 h 45 | Relecture et validation du protocole, y compris les passes correctives |
| S1-02 | 0,5 | 0,5 | 3 h 30 | 29 formes et leurs oracles à relire, chaque écart à arbitrer |
| S1-03 | 0,25 | 0,25 | 1 h 45 | — |
| S1-04 | 0,25 | 0,25 | 1 h 45 | Manipulation IDE entièrement humaine (D-07, PRE-S1-04) |
| S1-05 | 0,5 | **0,375** | 2 h 37 min 30 s | Pack et 8 cas scriptés par l'agent ; humain : lancement de la série, relecture de la table de précédence |
| S1-06 | 0,5 | **0,375** | 2 h 37 min 30 s | Série lancée par le mainteneur depuis le superviseur, commandes sous le runner ; humain : conditions M-08, lancement, relecture |
| S1-07 | 0,25 | **0,5** | 3 h 30 | Lancement de la série FSH, **triage initial intégral HS016 et `SPKCFG` par le mainteneur** ; le temps d'un second relecteur (M-10) est publié à part |
| S1-08 | 0,25 | 0,25 | 1 h 45 | Décision de frontière par le mainteneur |
| S1-09 | 0,25 | 0,25 | 1 h 45 | Conclusion et signature D-S1 |
| **Total** | **3,0** | **3,0** | **21 h** | |

Règles :
- Un reliquat sur un ticket peut servir à un ticket ultérieur ; un dépassement est pris sur le reliquat disponible, jamais sur la mesure obligatoire d'un autre ticket.
- La préparation des comptes superviseur et runner et de la règle de délégation, les attestations OP-01 de chaque série et leur fin de série, ainsi que l'installation de l'IDE (PRE-S1-04), sont des prérequis opérationnels ; le mainteneur décide s'il les impute à la gouvernance ou à S1, et le consigne dans le journal humain. Sous D-18-A, OP-01 s'applique à chacune des séries Q1 à Q6 et plus seulement à Q5 et Q6 ; ce temps n'est pas réparti ci-dessus.
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

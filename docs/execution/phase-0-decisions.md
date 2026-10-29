# Phase 0 — Journal des décisions (GOV-00)

Statut : registre ouvert le 28/10/2026. Backlog : [phase-0-backlog.md](phase-0-backlog.md) (§10, §11). Sprint : [sprint-01.md](sprint-01.md). **Ce registre est la source de vérité des statuts de décision** ; les autres documents y renvoient.

## Règles du registre

1. **Seul le mainteneur décide.** Une décision passe à `DECIDED` ou `DEFERRED` uniquement si le mainteneur la formule explicitement, avec son identifiant, dans une conversation ou un écrit daté. La référence de cette formulation est notée dans « Preuves ».
2. Ne constituent **jamais** une décision : une recommandation de l'agent, une valeur par défaut, le silence, une valeur présente dans le dépôt ou un document préparé.
3. Statuts : `OPEN` (non tranchée), `DECIDED` (tranchée par le mainteneur), `DEFERRED` (reportée explicitement par le mainteneur, avec l'échéance). Une attestation opérationnelle (`OP-…`, `PRE-…`) est `OPEN` jusqu'à ce que le mainteneur atteste lui-même, avec date, chaque point de sa liste.
4. La « Recommandation » est celle de l'agent ; elle n'engage pas la décision. Le champ « Décision humaine » reprend les termes du mainteneur.
5. Une décision modifiée ne s'efface pas : on ajoute une nouvelle entrée datée qui l'amende ou la remplace et la référence.
6. Aucun secret, aucune donnée personnelle de participant n'entre dans ce fichier (H-P7).

## Gabarit d'entrée

```
### D-xx — <titre>
- Statut : OPEN | DECIDED | DEFERRED
- Responsable : Mainteneur
- Date : <date de la décision, AAAA-MM-JJ>
- Contexte :
- Options :
- Recommandation (agent, non engageante) :
- Décision humaine :
- Justification :
- Conséquences :
- Preuves :
- Tickets débloqués :
```

## Attestations du mainteneur

| Id | Date | Nature | Référence |
|---|---|---|---|
| A-00 | 2026-10-28 | Le mainteneur valide `phase-0-backlog.md` et `sprint-01.md` comme **base d'exécution**. Cette validation ne tranche aucune décision D-xx et n'autorise ni commit, ni tag, ni push, ni PR, ni release, ni changement GitHub, ni contact externe, ni dépense, ni run Claude/Codex supplémentaire, ni création de secret, ni démarrage de S4, du sprint 2 ou de la phase 1A | Instruction du mainteneur, session Claude Code du 28/10/2026 (première instruction) |
| A-01 | 2026-10-28 | Le mainteneur consigne les décisions D-01, D-02, D-02b, D-03, D-04, D-05, D-06, D-07, D-10, D-11, D-12, D-13, D-16, M-08 et AUT-01, refuse de valider à ce stade V-S1-01, D-UXR-01 et D-UXR-02, et demande une passe corrective. Aucune mesure S1, aucun contact externe, aucun push, aucun changement GitHub, aucune installation et aucun commit de baseline avant sa nouvelle validation | Instruction du mainteneur, session Claude Code du 28/10/2026 (deuxième instruction) |
| A-02 | 2026-10-28 | Le mainteneur valide D-UXR-01, amende M-08 (M-08-A1) et D-10 (D-10-A1), laisse V-S1-01 et D-UXR-02 `OPEN` et demande une dernière passe corrective. Aucune mesure, aucun contact, aucun commit de baseline, aucun push et aucune modification GitHub avant sa nouvelle validation | Instruction du mainteneur, session Claude Code du 28/10/2026 (troisième instruction) |
| A-03 | 2026-10-28 | Le mainteneur demande une dernière passe corrective limitée (wrapper, classes de commandes, bootstrap Q5/Q6, métrique Q5(2), triage SPKCFG, D-10-A1, UXR, cohérence documentaire, OP-01), aligne l'échéance de D-02 sur la revue du sprint 2 avant D-03 et S4-01, et maintient V-S1-01 et D-UXR-02 `OPEN`. Seuls les tests triviaux et automatisés du wrapper sont autorisés ; aucune mesure, aucun contact, aucune installation, aucune modification système ou GitHub, aucun commit, push ou changement de remote | Instruction du mainteneur, session Claude Code du 28/10/2026 (quatrième instruction) |
| A-04 | 2026-10-28 | Fait déclaré par le mainteneur : il a créé le commit `40a2763`, l'a poussé alors que le remote était public, puis a rendu le dépôt **privé** le 28/10/2026. Vérification en lecture seule par l'agent le 28/10/2026 : `gh repo view toregua/hardslice --json visibility,isEmpty,defaultBranchRef,pushedAt` → `visibility: PRIVATE`, `isEmpty: false`, `defaultBranchRef: main`, `pushedAt: 2026-10-28T19:08:52Z` ; `git ls-remote origin` → `refs/heads/main` = `40a2763096f4a3688bf00c25c66c233206b45f62`. L'heure exacte du passage en privé n'est pas connue de l'agent | Déclaration du mainteneur (cinquième instruction) ; sorties de commandes ci-contre |
| A-05 | 2026-10-28 | Le mainteneur demande une passe corrective ciblée à partir de `40a2763` : état GitHub, wrapper (panne d'enregistrement → 70), limite du même UID (D-18), métrique Q5(2), `SPKCFG` bloquant, X5, dépendance S1-07 → S1-03, P0 et fin de série, UXR, échéance D-02. Il fusionnera ou squashera lui-même les commits. Interdits : commit, push, amend, rebase, réécriture d'historique, modification de remote ou de visibilité, mesure S1, restore ou build OSS, contact, installation ; V-S1-01 et D-UXR-02 restent `OPEN` | Instruction du mainteneur, session Claude Code du 28/10/2026 (cinquième instruction) |
| A-06 | 2026-10-28 | Le mainteneur demande à Codex d'appliquer directement la passe corrective issue de la revue de `40a2763`, de la vérifier, puis de créer un commit local à la fin. Cette instruction lève uniquement l'interdiction de commit correctif d'A-05 ; elle n'autorise aucun push, amend, rebase, changement GitHub, mesure S1, restore ou build OSS, contact, installation ou décision humaine implicite. D-18, V-S1-01, D-UXR-01-A1 et D-UXR-02 restent `OPEN` | Instruction du mainteneur, session Codex du 28/10/2026 (sixième instruction) |
| A-07 | 2026-10-29 | Le mainteneur relève la contradiction entre le `HOME` dédié « vide », l'existence préalable de `$HS_ART`, la conservation de `$HS_ART/.run-ids/` et la remise à zéro de fin de série. Il approuve la correction suivante : état de référence propre sous le `HOME`, registre `.run-ids` persistant et append-only, aucun autre état HardSlice persistant ; il demande son application dans un commit correctif séparé. Cette clarification ne décide ni D-18 ni V-S1-01 | Instruction du mainteneur, session Codex du 29/10/2026 (septième instruction) |
| A-08 | 2026-10-29 | Le mainteneur décide explicitement : « D-18 : option A. Je retiens un superviseur/logger sous une identité distincte du runner, un registre et un répertoire de preuves non inscriptibles par le runner, ainsi qu'un export minimal limité à `spikes/s1-roslyn/`, sans `.git`, sans `docs/` et sans accès au dépôt complet. » Il demande, à partir de `0600b74`, d'amender uniquement la documentation et le protocole S1 pour rendre D-18-A cohérente et exécutable, puis de créer un commit local atomique sans push. V-S1-01 reste `OPEN` jusqu'à sa validation explicite ; AUT-01 est inchangée : aucun commit de baseline avant V-S1-01. Interdits : création de compte macOS, modification de configuration système, installation, restore ou build OSS, mesure S1, contact externe ou UXR, push, validation de V-S1-01, commit de baseline, modification d'une décision humaine autre que D-18. Seul le test automatisé existant du wrapper est autorisé | Instruction du mainteneur, session Claude Code du 29/10/2026 (huitième instruction) |

## Portes

Les portes sont indépendantes : une porte ouverte n'en ouvre aucune autre.

| Porte | Entrées requises | État au 29/10/2026 |
|---|---|---|
| **Exécution technique de S1** (mesures S1-02 à S1-08) | D-05, D-06, D-10 + D-10-A1, D-11, D-12, M-08 + M-08-A1, D-18 (option A), **V-S1-01** sur le protocole amendé pour D-18-A, AUT-01 + commit de baseline ; **OP-01** avant **toute série** Q1 à Q6 ; **PRE-S1-04** et observation IDE de Q3 validée dans V-S1-01 avant S1-04 ; Q2 conforme (X1, X2) avant Q6 ; M-10 si un cas de triage S1-07 est ambigu ou contesté | **Fermée** : D-18 `DECIDED` (A-08) mais V-S1-01 `OPEN` ; commit de baseline non créé ; OP-01 et PRE-S1-04 non attestés |
| **Push** | Remote privé attesté ; autorisation explicite du mainteneur pour tout push fait par l'agent ; porte D-16 avant toute remise en public | Le premier push a eu lieu **alors que le remote était public** (écart à D-01, voir D-01) ; remote **privé** depuis (A-04). Aucun push n'est autorisé à l'agent |
| **Premier contact UXR** | D-UXR-01 et son amendement **D-UXR-01-A1**, **D-UXR-02**, moyen de demande de suppression renseigné, **OP-UXR-01** (stockage vérifié) | **Fermée** : D-UXR-01-A1 et D-UXR-02 `OPEN` ; placeholder de contact non remplacé ; OP-UXR-01 non attestée |
| **Entretien UXR** | Porte « premier contact » ouverte ; participant qualifié C1–C7 ; preuve de consentement préparée | **Fermée** |

D-01, D-13 et les validations UXR ne bloquent pas l'exécution technique de S1.

---

## Décisions du backlog (§10)

### D-01 — Visibilité du dépôt avant le premier push
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : le plan prévoit un dépôt privé jusqu'à la sortie de 1B (§10, §16). Constat du 28/10/2026 (`gh repo view toregua/hardslice --json visibility,isEmpty`) : `visibility: PUBLIC`, `isEmpty: true`. Le spec v2 contient du contenu personnel (§2 de `specifications.md`), voir D-16.
- Options : (a) rendre le remote privé avant le premier push, conformément au plan ; (b) le garder public mais non annoncé, avec README « experimental », après alignement de `plan.md` et décision D-16.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « option (a), dépôt privé avant le premier push. Cette décision n'autorise pas encore le changement de visibilité GitHub et ne bloque ni les corrections locales ni S1. Demande une autorisation distincte avant toute action GitHub. »
- Justification : conformité au plan (§10, §16).
- Conséquences : aucun alignement de `plan.md`. Le changement de visibilité relevait du mainteneur.
- **Écart historique (A-04)** : D-01 **n'a pas été respectée « avant le premier push »**. Le premier push (`50112a3`, `b8f5b0e`, `40a2763`, dernier push à `2026-10-28T19:08:52Z`) a eu lieu alors que le remote était public. Le mainteneur a ensuite rendu le dépôt privé le 28/10/2026. L'historique, dont le contenu personnel du spec v2, a donc été accessible publiquement pendant une durée que l'agent ne connaît pas. Rendre le dépôt privé n'efface ni une éventuelle copie, ni un clone, ni un fork, ni une mise en cache faits pendant cette période (voir D-16).
- Preuves : A-01 ; A-04.
- Tickets débloqués : GOV-01 : visibilité effective désormais privée (A-04), avec l'écart historique ci-dessus.

### D-02 — Budget financier maximal du pilote
- Statut : DEFERRED
- Responsable : Mainteneur / financeur
- Date : 2026-10-28
- Contexte : critère de sortie « coût estimé du pilote ≤ budget » (EXIT-01). Le backlog ne propose aucun montant. Échéance décidée : revue de planification du sprint 2, avant D-03, avant S4-01 et avant tout travail ou résultat S4, ce qui évite l'ancrage (R-12).
- Options : montant en dollars ; couverture : (i) tokens des 24 tentatives du pilote seuls, (ii) (i) + contrôle de manipulation, (iii) (ii) + répétitions éventuelles d'une nouvelle expérience.
- Recommandation (agent, non engageante) : aucun montant. Couverture (ii).
- Décision humaine : « DEFERRED jusqu'à la planification du sprint 2, avant tout travail ou résultat S4. Aucun calcul ni signal de coût S4 ne doit être présenté avant cette décision. » Précision du mainteneur (A-03) : « Aligner D-02 sur la revue du sprint 2, avant D-03/S4-01. »
- Justification : —
- Conséquences : échéance = **revue de planification du sprint 2**, avant D-03, avant S4-01 et avant tout travail ou résultat S4. Aucun calcul, estimation ou signal de coût S4 n'est produit ni présenté avant D-02.
- Preuves : A-01 ; A-03.
- Tickets débloqués : aucun (GOV-02 reste ouvert).

### D-02b — Couverture budgétaire des runs agents de 1A et de la phase 2
- Statut : DEFERRED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : le plan ne fixe que le budget du pilote ; 1A prévoit 5 runs agent, la phase 2 des scénarios E2E à 5 runs.
- Options : budget dédié ; inclusion dans D-02 ; décision reportée au début de 1A.
- Recommandation (agent, non engageante) : reporter au début de 1A.
- Décision humaine : « DEFERRED jusqu'à la planification de la phase 1A, avant ses runs agent. »
- Justification : —
- Conséquences : échéance = planification de 1A, avant tout run agent de 1A.
- Preuves : A-01.
- Tickets débloqués : aucun.

### D-03 — Enveloppe de dépense de S4 et authentification en conteneur
- Statut : DEFERRED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : S4 exécute Claude Code headless dans un conteneur neuf. Aucun secret ne doit entrer dans le dépôt ni dans l'image.
- Options : (a) clé API dédiée dans un espace de travail dédié avec plafond de dépense côté fournisseur, injectée à l'exécution, révoquée après le spike ; (b) authentification par abonnement (OAuth) dans le conteneur ; (c) report de S4.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « DEFERRED jusqu'à la même revue de sprint 2, après D-02 et obligatoirement avant S4-01. »
- Justification : —
- Conséquences : D-03 est tranchée après D-02, à la revue du sprint 2 ; S4-01 ne démarre pas avant D-03.
- Preuves : A-01.
- Tickets débloqués : aucun (S4-01 reste bloqué).

### D-04 — Codex envisagé ou non en phase 0
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : §9.1 : Codex est « expérimental », en phase 2 seulement si l'adaptateur tient en 2 jours. La timebox S4 reste 3 jours dans les deux variantes.
- Options : (a) non envisagé en phase 0 ; (b) envisagé (0,75 j réalloués à S4-C1/C2).
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « Codex non envisagé en phase 0. »
- Justification : —
- Conséquences : S4-C1 et S4-C2 restent dormants ; S4 suit l'allocation « sans Codex » ; la question revient au début de la phase 2 (§17).
- Preuves : A-01.
- Tickets débloqués : GOV-04 (terminé).

### D-05 — Capacité, cadence de sprint, contributeurs
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : hypothèse provisoire de `sprint-01.md` §2.
- Options : confirmer l'hypothèse ou donner d'autres valeurs.
- Recommandation (agent, non engageante) : donner les valeurs réelles.
- Décision humaine : « sprint de 2 semaines ; 5 j-p focalisés = 35 h ; 6 h de recherche ; 3 h 30 de gouvernance ; aucun autre contributeur. »
- Justification : —
- Conséquences : à 7 h par j-p (D-12) : focalisé 35 h = 5 j-p ; recherche 6 h ≈ 0,86 j-p ; gouvernance 3 h 30 = 0,5 j-p ; total 44 h 30 ≈ 6,36 j-p. La ligne « 5 j-p focalisés + humain » de la table d'ajustement de `sprint-01.md` §2 s'applique : contenu du §3 inchangé. `sprint-01.md` §2 est corrigé en conséquence. « Aucun autre contributeur » implique qu'aucun second relecteur n'est disponible aujourd'hui (voir M-10).
- Preuves : A-01.
- Tickets débloqués : GOV-05.

### D-06 — Grosse solution OSS de mesure et dépôt(s) en slices de calibration
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : S1-06 (performance) et S1-07 (exposition). Critère « grosse » fixé avant toute présélection : ≥ 2 000 fichiers C# compilés (hors `obj/`, `bin/`) et ≥ 20 projets, `dotnet build` simple, SDK 10.0.1xx. Présélection en lecture seule : [s1-roslyn.md §Présélection D-06](../spikes/s1-roslyn.md#présélection-d-06).
- Options : grosse solution : nopCommerce ou Squidex ; dépôts en slices : FSH, fdmm, food-delivery-microservices, kgrzybek, Contoso.
- Recommandation (agent, non engageante) : nopCommerce ; FSH + fdmm.
- Décision humaine : « grosse solution = nopCommerce@221305873bc81c47981b2e510b2e9bed7e5b2076 ; calibration obligatoire = FSH@a967537526f3b769c7d268da3ed2a6920af65ce9 ; fdmm reste exploratoire uniquement si le triage FSH est intégralement terminé et si la timebox le permet. Il ne doit pas devenir une mesure de porte incomplète ni faire l'objet d'un échantillonnage. »
- Justification : —
- Conséquences : S1-06 mesure nopCommerce à ce SHA. S1-07 mesure FSH à ce SHA, avec triage de **tous** ses diagnostics. fdmm (`cb7c30c803a293d2257e063f978c80afa62fd8f1`) n'est lancé qu'après le triage FSH complet et dans la timebox ; il n'entre dans aucun critère de porte. Son statut publié est « non exécuté » (aucun `<RUN_ID>` créé), « commencé, exploratoire incomplet » (run échoué, interrompu ou triage non intégral ; aucun nombre interprété) ou « exploratoire complet » (protocole S1 §6.6). Chaque clone est identifié par URL + SHA + worktree propre (D-10-A1).
- Preuves : A-01.
- Tickets débloqués : S1-06, S1-07 (sous réserve de V-S1-01 et OP-01).

### D-07 — IDE couvert par S1-04
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : observation IDE réelle requise ; l'agent ne peut pas manipuler l'IDE. Constat du 28/10/2026 : VS Code Insiders 1.144.0-insider avec C# Dev Kit 3.40.215 ; Rider 2026.2.1 ; VS Code stable absent.
- Options : (a) VS Code Insiders ; (b) VS Code stable + C# Dev Kit ; (c) Rider ; (d) deux IDE.
- Recommandation (agent, non engageante) : (b).
- Décision humaine : « VS Code stable + C# Dev Kit stable. Je réaliserai moi-même l'installation et les manipulations IDE. Les versions exactes devront être relevées avant le test. N'installe rien toi-même. »
- Justification : —
- Conséquences : **le choix de l'IDE est décidé ; le prérequis opérationnel ne l'est pas.** S1-04 reste fermé tant que PRE-S1-04 n'est pas attesté. L'agent n'installe rien. Sans observation humaine, S1-04 est `non mesuré`.
- Preuves : A-01 ; A-02 (distinction choix / prérequis).
- Tickets débloqués : aucun tant que PRE-S1-04 est `OPEN`.

### D-08 — Tâche proxy de la répétition à blanc
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : S4-06 (sprint 2 au plus tôt).
- Options : proxy distinct des énoncés (recommandation du backlog) ; énoncés du pilote.
- Recommandation (agent, non engageante) : proxy distinct (R-16).
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : S4-06.

### D-09 — Identifiant(s) des diagnostics de configuration
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : S2A-04 propose ; un ID n'est jamais réutilisé (§6.3).
- Options : à proposer par S2A-04 (sprint 2).
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : S2A-05.

### D-10 — Conservation du code de spike et stockage des artefacts volumineux
- Statut : DECIDED, **amendée par D-10-A1**
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : les binlogs MSBuild enregistrent les variables d'environnement et les chemins locaux ; les clones OSS pèsent plusieurs centaines de Mo.
- Options : (a) code, fixtures, notes et logs texte ≤ 1 Mo versionnés dans `spikes/s1-roslyn/`, volumineux hors git ; (b) notes seules ; (c) tout dans le dépôt.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « option (a), avec les artefacts volumineux sous ~/Documents/hardslice-artifacts/s1-roslyn/<date>/, hors git, et SHA-256 consigné. Les binlogs ne doivent jamais entrer dans le dépôt. »
- Justification : —
- Conséquences : versionnés dans `spikes/s1-roslyn/` : code jetable et fixtures. Emplacement hors git : `~/Documents/hardslice-artifacts/s1-roslyn/<AAAA-MM-JJ>/`. Le seuil « logs texte ≤ 1 Mo versionnés » est **remplacé par D-10-A1**.
- Preuves : A-01.
- Tickets débloqués : premier artefact S1 (sous réserve de V-S1-01).

### D-10-A1 — Amendement : sorties brutes hors git, résumés normalisés
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : confidentialité des sorties de build (noms d'utilisateur, `$HOME`, chemins, variables d'environnement) ; amende D-10.
- Options : —
- Recommandation (agent, non engageante) : voir « Conséquences », points 5 à 7.
- Décision humaine : « toutes les sorties brutes restent hors git, quelle que soit leur taille : binlogs, logs complets, SARIF, CSV et sorties MSBuild ; le dépôt ne reçoit que des résumés ou extraits normalisés après contrôle de secrets et remplacement du nom utilisateur, de $HOME et des chemins locaux ; les commandes versionnées utilisent des variables symboliques ; leur expansion locale exacte reste dans le manifeste privé ; un clone est identifié par URL, SHA et état propre, pas par le SHA-256 de chacun de ses fichiers. »
- Justification : —
- Conséquences :
  1. Toute sortie brute (export de travail, `bin/obj`, binlog, log complet, `.exit`, SARIF, CSV, sortie MSBuild, capture brute, clone) va **exclusivement** sous `$HS_ART/<RUN_ID>/` (`$HS_RUN`), hors git ; `<RUN_ID>` est un identifiant immuable de tentative, généré par P0 avec un UUID (protocole S1 §3.2).
  2. `spikes/<spike>/results/<RUN_ID>/` ne reçoit que des fiches et des extraits normalisés ; `spikes/.gitignore` ignore `*.log`, `*.exit`, `*.binlog`, `*.sarif`, `*.csv` et `raw/` comme garde-fou.
  3. Les commandes versionnées emploient `$HS_REPO`, `$HS_ART`, `$HS_RUN`, `$HS_CLONES`, `$HS_SPIKE` ; leur expansion exacte est consignée dans le manifeste privé `$HS_RUN/manifest.private.md`, hors git, avec le SHA-256 de chaque sortie brute. `$HS_SPIKE` est un export `git archive` exact du spike au commit mesuré sous `$HS_RUN/work/` ; aucune compilation ne travaille dans `$HS_REPO`.
  4. Un clone est attesté par son URL, son SHA de `HEAD` et un `git status --porcelain` vide, archivés avant et après chaque série.
  5. Recommandation : la normalisation remplace le nom d'utilisateur, `$HOME` et tout chemin absolu local par `<USER>`, `<HOME>`, `<HS_ART>`, `<HS_RUN>`, `<HS_CLONES>`, `<HS_SPIKE>`, `<HS_REPO>`, puis un contrôle de secrets (motifs de clés et de jetons connus) précède tout ajout au dépôt ; un extrait qui échoue au contrôle n'est pas versionné.
  6. Recommandation : `~/Documents` est le périmètre par défaut de la synchronisation iCloud « Bureau et Documents ». OP-01 vérifie donc que l'emplacement D-10 n'est pas synchronisé.
  7. Recommandation : un compte dédié ne peut normalement pas lire le dossier personnel du mainteneur. Les sorties sont donc produites sous un `$HS_ART` accessible au compte dédié, puis rangées par le mainteneur sous l'emplacement D-10, avec vérification des SHA-256 du manifeste et refus d'écraser un dossier `<RUN_ID>` existant.
  8. Application opérationnelle : P0 refuse un `$HS_ART` situé dans un dépôt ou worktree Git ; pour toute série Q1 à Q6, la fiche atteste aussi que `$HS_ART` n'est synchronisé par aucun fournisseur cloud. P0 génère le `RUN_ID` (heure UTC, question, SHA, tentative et UUID) et le réserve exclusivement dans `$HS_ART/.run-ids/`. Ce garde local ne contient aucune sortie brute et est conservé après le rangement des tentatives ; l'UUID couvre les racines d'artefacts distinctes.
  9. Application sous D-18-A (A-08) : la décision humaine ci-dessus est inchangée ; la lecture opérationnelle des points 1, 3 et 7 sous D-18-A est donnée par D-18, Conséquences, point 6.
- Preuves : A-02 ; A-07 (clarification opérationnelle de l'état de référence) ; A-08 (renvoi D-18-A).
- Tickets débloqués : premier artefact S1 (sous réserve de V-S1-01).

### D-11 — Langue des notes
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : H-P6.
- Options : (a) notes en français, supports participants FR + EN ; (b) tout en anglais ; (c) tout en français.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « notes internes en français ; supports destinés aux participants en français et en anglais. »
- Justification : —
- Conséquences : notes de spike, d'exécution et de recherche en français ; script d'entretien, questions de présélection et messages en FR et EN.
- Preuves : A-01.
- Tickets débloqués : S1-01, UXR-01, UXR-02 (rédaction).

### D-12 — Politique d'extension de timebox
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : S1 = 3 j-p focalisés (H-P2).
- Options : (a) aucune extension ; (b) une seule extension de 0,5 j-p au plus ; (c) au cas par cas.
- Recommandation (agent, non engageante) : (b).
- Décision humaine : « une seule extension de 0,5 j-p maximum, demandée avant l'épuisement de la timebox et nécessitant une nouvelle validation explicite. 1 j-p = 7 h de travail focalisé du mainteneur. Tiens deux journaux séparés : temps focalisé humain, seul décompté dans les 3 j-p ; temps mural/compute de l'agent, publié séparément et jamais converti en jours-personne. Les 20 minutes déjà inscrites vont dans le journal agent. »
- Justification : —
- Conséquences : la timebox S1 = 21 h de travail focalisé du mainteneur, déclaré par lui. Le temps de l'agent et la durée de calcul des runs sont publiés à part, sans conversion. Au plus une extension de 3 h 30, demandée par écrit avant l'épuisement et validée explicitement. Journaux dans [s1-roslyn.md §8](../spikes/s1-roslyn.md#8-journaux-de-temps).
- Preuves : A-01.
- Tickets débloqués : S1-02 à S1-09 (sous réserve de V-S1-01).

### D-13 — Stockage hors dépôt et conservation des données de recherche
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : bloque tout contact, toute collecte nominative et tout entretien. FileVault actif sur le poste (`fdesetup status`, 28/10/2026).
- Options : (a) dossier local hors git ; (b) cloud privé ; (c) outil tiers.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « dossier local ~/Documents/hardslice-research/, hors git et hors synchronisation cloud, sur le disque protégé par FileVault. Enregistrements supprimés après vérification des notes et au plus tard sous 30 jours. Notes brutes, table d'identité et tracker supprimés à la fin de la recherche de phase 4 ou au plus tard 12 mois après l'entretien. Données des prospects non interviewés supprimées à la clôture du recrutement ou sous 90 jours après le dernier contact. Les sauvegardes doivent suivre les mêmes échéances. La preuve de consentement et la table identité ↔ pseudonyme restent hors git et séparées. Aucun contact tant que le script corrigé n'est pas validé. »
- Justification : —
- Conséquences : le guide d'entretien reprend ces durées dans le consentement ; la preuve de consentement et la table identité ↔ pseudonyme sont deux fichiers distincts ; aucun contact avant D-UXR-02 et OP-UXR-01. Point d'attention : « les sauvegardes suivent les mêmes échéances » vaut aussi pour Time Machine ou toute autre sauvegarde locale qui conserverait des versions supprimées (OP-UXR-01, point 4).
- Preuves : A-01.
- Tickets débloqués : aucun contact tant que la porte « premier contact UXR » est fermée.

### D-14 — Épinglage du SDK dans `global.json`
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : REPO-02 (sprint 2). SDK 10.0.102 (Roslyn 5.0.0-2.25612.105) et 10.0.201 (Roslyn 5.3.0-2.26153.122) installés. Le spike S1 épingle son propre SDK dans son protocole, sans trancher D-14.
- Options : à instruire dans REPO-02.
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : REPO-02.

### D-15 — Règle de marge des plafonds par run
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : S4-07 (sprint 2 au plus tôt).
- Options : à instruire dans S4-07.
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : S4-07.

### D-16 — Contenu personnel du spec v2 et publication
- Statut : DEFERRED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : `specifications.md` §2 et « Projet lié » contiennent des éléments personnels et professionnels. Le fichier a été ajouté par le commit `50112a3` ; il figure dans l'arbre de ce commit et de tous ses descendants (dont `b8f5b0e`, qui ne le modifie pas).
- Options : (a) sans objet si D-01 = privé, à reprendre au passage en public (DOC-02) ; (b) si public : retirer ces passages de l'historique avant le premier push ; (c) si public : accepter leur publication.
- Recommandation (agent, non engageante) : suit D-01.
- Décision humaine : « DEFERRED jusqu'à DOC-02, mais ajoute une porte obligatoire avant tout passage public : le contenu personnel du spec v2 ne doit pas rester accessible dans l'historique public. Prévoir assainissement ou réécriture de l'historique avant publication. »
- Justification : —
- Conséquences : **porte obligatoire avant tout passage public** (visibilité publique du remote, push vers un remote public, publication d'une archive) : le contenu personnel du spec v2 est retiré de tout l'historique publié, par assainissement ou réécriture de l'historique, et la vérification est consignée. DOC-02 décrit la méthode. Un historique privé poussé puis rendu public reste soumis à cette porte.
- **Exposition passée (A-04)** : l'historique contenant ce contenu a été poussé pendant que le remote était public, puis le dépôt a été rendu privé. Cette exposition n'est pas effacée : une copie antérieure (clone, fork, cache, archive) peut exister hors du contrôle du mainteneur. La porte D-16 reste obligatoire avant **toute future remise en public** ; elle ne peut pas corriger l'exposition passée.
- Preuves : A-01 ; A-04.
- Tickets débloqués : aucun ; ajoute une condition à DOC-02 et au passage en public (§16).

### D-17 — Relecteurs des énoncés et des ADR
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : TASK-03, ADR-07 (hors sprint 1).
- Options : à instruire.
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : TASK-03 ; ADR-07.


### D-18 — Protection des preuves et de la copie source contre le code exécuté
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-29
- Contexte : dans la variante du protocole S1 antérieure à cette décision, le code OSS, les outils de build et le wrapper `run-logged.sh` s'exécutent sous le même compte. Une commande peut donc modifier ses propres logs, sidecars, binaires ou configurations. Pendant un restore avec réseau limité, elle peut aussi lire tout contenu accessible dans la copie `$HS_REPO` et tenter de l'exfiltrer vers une destination autorisée. Les contrôles d'inode, d'en-tête et de sidecar du wrapper détectent les erreurs et écrasements accidentels ; ils ne résolvent ni l'intégrité face au même UID, ni la confidentialité de la copie source (protocole S1 §3.3, §3.4).
- Options :
  - **(A)** superviseur et logger sous une identité distincte du runner ; répertoire de preuves non inscriptible par le runner ; stdout et stderr capturés par pipe par le superviseur ; entrées et binaires en lecture seule pour le runner ; sorties du runner écrites dans un staging, puis copiées et hachées par le superviseur. Le runner reçoit un export minimal du commit limité à `spikes/s1-roslyn/`, sans `.git` ni `docs/`, avec SHA source et manifeste SHA-256 ; le dépôt complet lui reste inaccessible ;
  - **(B)** accepter explicitement que les preuves protègent contre les erreurs et les écrasements accidentels, mais pas contre une commande malveillante exécutée sous le même UID, et accepter le risque de lecture ou d'exfiltration de tout contenu accessible dans `$HS_REPO` pendant la fenêtre réseau.
- Recommandation (agent, non engageante) : (A). Elle demande une seconde identité locale, un export minimal et une préparation système, que seul le mainteneur peut réaliser et attester.
- Décision humaine : « D-18 : option A. Je retiens un superviseur/logger sous une identité distincte du runner, un registre et un répertoire de preuves non inscriptibles par le runner, ainsi qu'un export minimal limité à `spikes/s1-roslyn/`, sans `.git`, sans `docs/` et sans accès au dépôt complet. »
- Justification : —
- Conséquences :
  1. Le protocole S1 est amendé le 29/10/2026 ([s1-roslyn.md §3.4](../spikes/s1-roslyn.md#34-isolation-et-environnement-dexécution-m-08-a1-d-18-a-op-01), [§3.6](../spikes/s1-roslyn.md#36-délégation-au-runner-instantanés-promotions-et-collecte-d-18-a), [§6.5.1](../spikes/s1-roslyn.md#651-prologue-et-contrôles-communs-q1q6--bootstrap-supplémentaire-q5q6)). Le **superviseur** est le compte macOS local du mainteneur ; le **runner** est le compte dédié de M-08-A1 ; le superviseur n'agit comme runner que par `as-runner.sh`, au moyen d'une règle sudoers unique posée et attestée par le mainteneur (OP-01). Toutes les commandes des séries Q1 à Q6 qui exécutent le SDK, git sur un clone, l'hôte de benchmark ou du code tiers s'exécutent sous le runner, fixtures comprises.
  2. Le superviseur génère et réserve le `RUN_ID` (P0). Il possède `$HS_ART`, le registre `.run-ids`, `$HS_RUN` (journaux, sidecars, manifeste privé, archive source, instantanés, collecte finale) et l'export. Le runner ne peut ni lire, ni modifier, ni supprimer les réservations ou les preuves : le `HOME` du superviseur, qui les contient, ne lui est pas traversable (contrôlé par P0 et par les sondes S0).
  3. Le runner reçoit uniquement, en lecture seule, l'export minimal `$HS_EXPORT` : `git archive` de `spikes/s1-roslyn/` au commit mesuré, sans `.git` ni `docs/`, avec le SHA source (`SOURCE`) et un manifeste SHA-256 par fichier (`MANIFEST.sha256`), plus les sorties qu'il a produites et que le superviseur a promues. Le dépôt HardSlice complet, `.git` et `docs/` lui sont inaccessibles.
  4. Le runner n'écrit que dans son staging `$HS_STAGE`, transitoire. stdout et stderr sont capturés par pipe par le wrapper du superviseur. Le superviseur ne lit le staging que par instantanés `tar` produits sous le runner et reçus par pipe ; il les vérifie (fichiers réguliers et dossiers seulement), les copie et les hache sous `$HS_RUN`. Toute sortie réutilisée est promue en lecture seule. La finalisation K arrête les processus du runner, collecte et hache le staging, puis le supprime.
  5. Réseau et caches : OP-01, point 12, s'applique aux processus du runner. Les caches NuGet et .NET sont dédiés à la tentative, sous le staging, et détruits avec lui.
  6. **Application de D-10-A1** (décision humaine inchangée) : les sorties brutes restent hors git ; elles sont produites dans le staging et conservées uniquement sous `$HS_RUN`, comme instantanés et archive collectée. C'est ainsi que se lisent désormais, pour S1, le point 1 (« exclusivement sous `$HS_RUN` » : seul emplacement conservé) et le point 3 (`$HS_SPIKE` devient la copie de travail du runner dans le staging, vérifiée contre l'export ; aucune compilation ne travaille dans `$HS_REPO`). Pour le point 7, `$HS_ART` est sous le `HOME` du superviseur et reste inaccessible au runner ; seul le staging lui est accessible.
  7. **Application de M-08-A1** (décision humaine inchangée) : le compte dédié est le runner. « Accès uniquement à une copie du commit HardSlice mesuré et au dossier d'artefacts » se lit : export minimal en lecture seule et staging de la tentative, sans accès à `$HS_ART`. Les séries sont lancées par le mainteneur depuis le compte superviseur, et non plus dans le compte dédié.
  8. OP-01 est amendée : elle distingue les deux comptes, fixe la délégation, les chemins, propriétaires, modes et ACL, et s'applique désormais avant et après **chaque** série Q1 à Q6.
  9. Limites résiduelles, à valider dans V-S1-01 : les sorties du runner restent sous son contrôle jusqu'à leur instantané ; le superviseur partage l'UID du mainteneur et de l'agent, si bien que D-18-A ne protège pas les preuves contre un processus de ce compte, et la règle sudoers permet à tout processus de ce compte d'agir comme runner ; le runner, détaché de tout terminal, hérite encore de la session graphique du superviseur, dont seul le presse-papiers est sondé ; l'observation IDE de Q3 n'est compatible avec D-18-A que dans la session graphique du runner, l'ouvrir dans la session du mainteneur exigerait un amendement de D-18.
  10. V-S1-01 reste `OPEN` jusqu'à la validation explicite du protocole amendé. AUT-01 est inchangée : aucun commit de baseline avant V-S1-01.
- Preuves : A-08.
- Tickets débloqués : aucun directement ; D-18 n'est plus un préalable ouvert de V-S1-01.
---

## Validations, autorisations, prérequis opérationnels et inputs du sprint 1

### M-08 — Machine de référence des mesures
- Statut : DECIDED, **amendée par M-08-A1**
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : Apple M4 Max, 16 cœurs, 64 Go, macOS 26.6.2 (25G83), arm64, disque chiffré.
- Options : (a) poste local ; (b) runner dédié.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « poste local M4 Max, sur secteur, applications lourdes fermées, conditions et variance publiées. »
- Justification : —
- Conséquences : chaque fiche de performance consigne l'alimentation et les applications ouvertes déclarées par le mainteneur, et publie la dispersion (min, Q1, médiane, Q3, écart interquartile, max ; convention dans le protocole §6.5).
- Preuves : A-01.
- Tickets débloqués : S1-06 (sous réserve de V-S1-01 et OP-01).

### M-08-A1 — Amendement : isolation des builds OSS
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : nopCommerce et FSH exécutent du code tiers pendant le restore et le build (tâches MSBuild, générateurs, analyseurs) ; amende M-08.
- Options : —
- Recommandation (agent, non engageante) : —
- Décision humaine : « tout restore, build ou benchmark de nopCommerce et FSH doit être effectué sur le M4 Max sous un compte macOS local dédié et non privilégié, avec : HOME dédié et vide ; aucun secret, trousseau, agent SSH, credential cloud ou donnée UXR accessible ; configuration NuGet limitée aux sources publiques requises ; accès uniquement à une copie du commit HardSlice mesuré et au dossier d'artefacts ; réseau désactivé après le restore lorsque le build le permet. Le mainteneur réalisera lui-même cette préparation. Ne crée aucun compte ni configuration système. Ajoute une attestation opérationnelle obligatoire avant le premier restore. »
- Justification : —
- Conséquences : l'agent ne crée ni compte ni configuration système. Les runs nopCommerce et FSH sont **lancés par le mainteneur** dans le compte dédié, via les scripts versionnés du spike, puisque l'agent s'exécute sous le compte du mainteneur. L'agent prépare les scripts et analyse les sorties rangées. L'attestation OP-01 est obligatoire avant le premier restore. **Clarification opérationnelle A-07** : la formulation historique « `HOME` dédié et vide » désigne un état de référence propre, inventorié après la création de `$HS_ART/.run-ids/`, et non un `HOME` littéralement vide ; le registre est le seul état HardSlice qui persiste entre les séries. **Application sous D-18-A (A-08)** : la décision humaine ci-dessus est inchangée ; le compte dédié est le runner de D-18-A, et la lecture opérationnelle de ces conséquences est donnée par D-18, Conséquences, point 7.
- Preuves : A-02 ; A-07 (clarification opérationnelle de l'état de référence) ; A-08 (renvoi D-18-A).
- Tickets débloqués : aucun avant OP-01.

### OP-01 — Attestation d'isolation des comptes superviseur et runner, avant et après chaque série S1
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : M-08-A1, D-10-A1, A-07, D-18 (option A, A-08). Sous D-18-A, toutes les séries Q1 à Q6 exécutent leurs commandes sous le runner : OP-01 est donc à attester par le mainteneur, point par point, **avant toute série** S1, et non plus seulement avant le premier restore de nopCommerce ou de FSH. Une attestation vaut pour une série ; tout changement de compte, de machine, de règle de délégation ou de configuration impose une nouvelle attestation. Les contrôles automatiques de P0, S0 et K (protocole S1 §6.5.1) produisent des preuves que l'attestation cite ; ils ne la remplacent pas. Les sorties demandées avant la série sont archivées dans un **fichier d'attestation privé** de la série, hors git, créé par le mainteneur avant le lancement sous `~/Documents/hardslice-artifacts/s1-roslyn/attestations/` ; l'entrée de ce registre ne cite que sa date et son nom. Les sorties de fin de série vont dans le manifeste privé de la tentative, avant son rangement. L'agent ne crée aucun compte, ne modifie aucune configuration système et n'exécute aucune des opérations ci-dessous.
- Identités et lancement :
  - **Superviseur** : le compte macOS local du mainteneur. Il exécute P0, le script de série, le wrapper, `as-runner.sh`, les instantanés, les promotions et la collecte. Il possède `$HS_ART`, `.run-ids`, `$HS_RUN` et l'export. Il n'exécute jamais un fichier produit par le runner.
  - **Runner** : compte macOS local dédié et standard (le compte dédié de M-08-A1), distinct du superviseur, dont le nom n'est consigné que dans le manifeste privé. Il exécute uniquement les commandes que lui passe `as-runner.sh`.
  - **Qui lance quoi** : le mainteneur lance chaque série dans un terminal du compte superviseur, par la commande `env -i` du protocole S1 §6.5.1. Le superviseur n'obtient l'identité du runner que par `sudo -n -u <runner> -- /usr/bin/env -i …`, dans `as-runner.sh`. Le mainteneur réalise lui-même, dans son propre compte ou avec ses droits d'administrateur, la préparation des comptes, de la délégation et des chemins, puis les vérifications ci-dessous. L'agent écrit et commite les scripts, analyse les sorties rangées et ne lance aucune série.
- Points à attester avant la série :
  1. **Comptes** : runner local, standard, non membre des groupes `admin` et `wheel`, d'UID non nul et différent de celui du superviseur ; sorties de `id <runner>` et `id -Gn <runner>` archivées dans le fichier d'attestation. P0 refait ces contrôles et vérifie aussi que `HOME` est bien le dossier personnel du superviseur.
  2. **Délégation** : un seul fichier `/etc/sudoers.d/<fichier>` (root, `0440`) ajoute exactement la règle `<superviseur> ALL=(<runner>) NOPASSWD: /usr/bin/env`. `visudo -c` réussit ; `sudo -l`, lancé par le superviseur, montre cette règle et aucune autre règle `NOPASSWD` ; la règle par défaut `%admin ALL=(ALL) ALL`, qui exige le mot de passe du superviseur, subsiste et est consignée telle quelle. `sudo -l -U <runner>` montre que le runner ne peut rien lancer par `sudo`. Les sorties sont archivées dans le fichier d'attestation. La règle est retirée à la fin de S1, avec une attestation datée.
  3. **`HOME` du runner dans un état de référence propre** : seuls les chemins créés par macOS pour ce compte, explicitement listés dans l'attestation par un inventaire structurel (chemins, types, propriétaires, modes, sans horodatages). Aucun état HardSlice, notamment pas de `hardslice-stage`. Aucun cache .NET, NuGet ou MSBuild. `crontab -l` du runner vide, aucun agent ou démon de lancement, aucune session graphique du runner ouverte pendant la série, sauf pour l'observation IDE de Q3 si V-S1-01 la valide ; cette session est alors fermée avant la finalisation K.
  4. **Aucun secret ni donnée accessible au runner** : aucun trousseau personnel, agent SSH, credential cloud, jeton GitHub, configuration Claude/Codex, variable d'environnement de secret ni donnée UXR dans le compte runner. Le `HOME` du superviseur est en `0700`, sans ACL accordant un accès (P0 vérifie) : `$HS_REPO`, `.git`, `docs/`, `$HS_ART`, l'emplacement D-10 et `~/Documents/hardslice-research/` sont donc hors de portée du runner (sondes S0-3 à S0-5). Si `$HS_REPO` est hors de ce `HOME`, son inaccessibilité au runner est attestée à part. Les volumes externes sont démontés, ou leur inaccessibilité au runner est attestée.
  5. **Entrées en lecture seule** : le runner ne reçoit que l'export minimal `$HS_EXPORT` (spike au commit mesuré, `SOURCE`, `MANIFEST.sha256`, copies promues), sans `.git` ni `docs/` (P0 et S0-6 vérifient). Le SDK n'est pas inscriptible par le runner : `find /usr/local/share/dotnet \( ! -user root -o -perm -020 -o -perm -002 \) -print` ne renvoie rien, sortie archivée dans le fichier d'attestation.
  6. **Staging** : `$HS_STAGE` = `<HOME du runner>/hardslice-stage`, absent avant la série (S0-7 le crée exclusivement), en `0700`, possédé par le runner. Par construction, c'est le seul emplacement où écrivent les commandes du runner (`TMPDIR`, `DOTNET_CLI_HOME`, caches NuGet, sorties) ; une écriture ailleurs est un état parasite, détecté au point 15.
  7. **Configuration NuGet** : seule(s) la ou les sources publiques requises, y compris celles déclarées par les `nuget.config` des clones. Le relevé est fait dans la série, sous le runner et avec sa liste blanche, pour ne rien écrire dans son `HOME` : B0b (`dotnet nuget list source`) pour toute série, et B9n (instantané des `nuget.config` des clones) pour Q5 et Q6 ; une source non publique arrête la série. L'attestation cite ces deux preuves.
  8. **Cache NuGet dédié à la tentative** (`NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH`, `NUGET_PLUGINS_CACHE_PATH` sous `$HS_STAGE/nuget/`), jamais partagé avec le superviseur, détruit avec le staging.
  9. **Environnements en liste blanche** : superviseur lancé par la commande `env -i` du protocole S1 §6.5.1, sans `BASH_ENV` ni fonction exportée, puis second `env -i` avant le script de série ; chaque commande du runner reçoit sa liste par `env -i` dans `as-runner.sh`. L'inventaire effectif du runner est archivé par B0a ; celui du superviseur est fixé par P0 et consigné dans le manifeste privé.
  10. **Chemins, propriétaires, modes et ACL** conformes au tableau ci-dessous. Pour chaque chemin, la sortie de `ls -lde` est archivée dans le fichier d'attestation. « Sans ACL » signifie aucune entrée `allow` accordée à un autre utilisateur ou groupe ; l'entrée macOS par défaut `group:everyone deny delete` est admise.
  11. **`$HS_ART` et registre** : `$HS_ART` et l'emplacement D-10 sont hors synchronisation iCloud, Dropbox ou OneDrive. Avant P0, `$HS_ART` ne contient que `.run-ids/`, dont les réservations sont des dossiers vides en `0700` (P0 vérifie). Le registre est le seul état HardSlice persistant autorisé : ses réservations sont append-only, préservées entre les séries et après leur rangement. Le runner ne peut ni le lire ni le modifier (S0-5). Face à une erreur côté superviseur, la préservation reste procédurale et vérifiée au point 17.
  12. **Réseau**, pour les processus du runner : LAN bloqué, sortie limitée aux flux nécessaires (`api.nuget.org` et ses CDN, `github.com` pour les clones) ; méthode consignée. Pour Q5 et Q6, coupure complète après le bootstrap (B11). Pour Q1 à Q4, dont les builds restaurent implicitement, le mode limité dure toute la série. Si ce filtrage n'est pas réalisable, le **risque résiduel** est décrit (ce qui reste joignable, par quel code) et **accepté explicitement** par le mainteneur dans cette attestation. Un échec dû à l'absence de réseau est publié ; toute réactivation est consignée avec sa raison.
  13. Conditions M-08 : sur secteur, applications lourdes fermées.
- Chemins, propriétaires et modes attendus (point 10) :

| Chemin | Propriétaire | Mode et ACL | Accès du runner |
|---|---|---|---|
| `HOME` du superviseur | superviseur | `0700`, sans ACL | aucun, pas même la traversée |
| `$HS_REPO` et l'emplacement D-10, sous ce `HOME` | superviseur | inchangés | aucun |
| `$HS_ART`, strictement sous ce `HOME` | superviseur | `0700`, sans ACL | aucun |
| `$HS_ART/.run-ids/` et chaque `.run-ids/<RUN_ID>/` | superviseur | `0700`, réservations vides | aucun |
| `$HS_RUN` = `$HS_ART/<RUN_ID>` | superviseur | dossiers `0700`, fichiers `0600` | aucun |
| `/Users/Shared` | root | `1777` | traversée |
| `$HS_XROOT` = `/Users/Shared/hardslice-s1` | superviseur | `0755`, sans ACL, vide entre les séries | lecture |
| `$HS_EXPORT`, `$HS_SRC`, `$HS_RO` | superviseur | dossiers `0755`, fichiers `0644` ou `0755`, aucun droit d'écriture pour le groupe ni les autres | lecture seule |
| `HOME` du runner | runner | défaut macOS | lecture et écriture, à l'état de référence |
| `$HS_STAGE` = `<HOME du runner>/hardslice-stage` | runner | `0700` | lecture et écriture, pendant la série seulement |
| `/usr/local/share/dotnet` | root | aucun droit d'écriture pour le groupe ni les autres | lecture et exécution |
| `/etc/sudoers.d/<fichier>` | root | `0440` | aucun |

- **Fin de série**, à réaliser et attester par le mainteneur après **chaque** série Q1 à Q6, avant toute réutilisation du runner ou toute nouvelle série :
  14. **Processus** : les journaux K1 à K3 montrent l'arrêt des serveurs de build, puis aucun processus du runner (oracle de K3). Le mainteneur le vérifie et consigne l'heure. Si K3 n'atteint pas son oracle, aucune nouvelle série n'a lieu avant examen.
  15. **Inventaire des fichiers persistants hors staging** : fichiers créés ou modifiés dans le `HOME` du runner pendant la série, comparés à l'état de référence du point 3 ; fichiers appartenant au runner sous `/tmp`, `/private/var/tmp`, `/Users/Shared` et ses dossiers `getconf DARWIN_USER_TEMP_DIR` et `DARWIN_USER_CACHE_DIR` ; crontab et agents ou démons de lancement. Ces commandes de lecture (`find`, `ls`, `crontab -l`, `getconf`) sont lancées sous le runner par `sudo -n -u <runner> -- /usr/bin/env -i HOME=<HOME du runner> PATH=/usr/bin:/bin:/usr/sbin:/sbin <commande>`, que la règle du point 2 autorise, et l'inventaire est archivé dans le manifeste privé.
  16. **Rangement et cas d'échec** : si K n'est pas allée à son terme alors que le staging de la tentative existe, le mainteneur lance `tools/series-finalize.sh` depuis le superviseur (protocole S1 §6.5.1) avant toute autre opération. Si la collecte reste impossible, l'inventaire du staging produit sous le runner (`/usr/bin/find <staging> -ls`, par la même forme `sudo -n -u <runner> -- /usr/bin/env -i …` qu'au point 15) est consigné dans le manifeste privé, puis le staging est supprimé sous le runner et la tentative est invalide. Un staging trouvé par S0-7 et n'appartenant à aucune tentative en cours suit le même traitement, rattaché à la tentative qui l'a créé si elle est identifiable, sinon consigné comme incident. Ensuite, `$HS_RUN` est copié sous l'emplacement D-10 avec création exclusive du dossier cible (aucun écrasement), les SHA-256 du manifeste privé sont vérifiés, puis `$HS_RUN` est supprimé. Les échecs P0 suivent trois cas : **(a)** avant réservation, échec de lancement sans `RUN_ID` réservé, consigné dans l'attestation mais non publié comme tentative ; **(b)** après réservation mais avant création de `manifest.private.md`, tentative invalide pour laquelle le mainteneur crée directement dans le dossier D-10 un manifeste privé de récupération (RUN_ID, heure, étape d'échec, présence ou absence de `$HS_RUN` et de `$HS_EXPORT`, inventaire et SHA-256 de tout fichier partiel) avant de ranger ou supprimer le `$HS_RUN` partiel ; **(c)** manifeste P0 présent, procédure normale, un `$HS_EXPORT` partiel étant inventorié dans le manifeste puis supprimé par le superviseur. Après traitement : `$HS_ART` ne contient que `.run-ids/`, `$HS_XROOT` est vide (K8) et `$HS_STAGE` est absent (K6). Le registre n'est jamais supprimé ; seul l'ajout d'une nouvelle réservation vide en `0700` est autorisé ; aucune réservation existante n'est supprimée ni modifiée.
  17. **Retour à l'état de référence** : le `HOME` du runner revient à l'état du point 3, tous les caches dédiés sont supprimés avec le staging, et aucun nouvel état HardSlice ne subsiste chez le runner ni dans la racine d'échange. La vérification structurelle finale est consignée dans l'attestation : les chemins macOS autorisés restent ceux du point 3, `$HS_ART` ne contient que `.run-ids/`, et la seule différence admise dans ce registre, par rapport à la liste `registry_before` du manifeste privé, est l'ajout des répertoires vides `0700` `.run-ids/<RUN_ID>/` réservés depuis ; toute réservation antérieure reste inchangée. Un état parasite hors registre bloque la série suivante jusqu'à son examen et sa suppression. La disparition ou la modification d'une réservation historique est irréversible : elle déclenche un incident consigné, examiné aussi comme une rupture possible de l'isolation D-18-A ; elle interdit définitivement une nouvelle série sous cette racine `$HS_ART` et impose de quarantainer ce qui subsiste sous D-10 avec ses empreintes ; une nouvelle racine ne peut être provisionnée qu'après décision humaine explicite et nouvelle attestation OP-01. Les métadonnées volatiles des dossiers parents, notamment leurs horodatages, ne participent pas à la comparaison.
- Décision humaine (attestation) : —
- Preuves : —
- Tickets débloqués : toute série S1, Q1 à Q6 (S1-02 à S1-07) ; réutilisation du runner après une série (points 14 à 17).

### PRE-S1-04 — Prérequis opérationnel IDE
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : D-07. Le choix est décidé ; ce prérequis atteste que l'IDE est prêt.
- Points à attester : installation par le mainteneur de VS Code stable et de C# Dev Kit stable ; versions exactes, relevées avant le test, de VS Code, C# Dev Kit, extension C# et runtime .NET utilisé par l'extension.
- Décision humaine (attestation) : —
- Preuves : —
- Tickets débloqués : S1-04.

### M-10 — Second relecteur indépendant du triage S1-07
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : protocole S1 §6.6 et plan §6.1 : tout cas de triage ambigu ou contesté exige un second relecteur indépendant. D-05 indique qu'aucun autre contributeur n'est disponible.
- Options : identifier un relecteur (design partner, contributeur) avant S1-07 ; sinon, tout cas ambigu laisse Q6 `non mesuré`.
- Recommandation (agent, non engageante) : l'identifier avant le run FSH, pour ne pas bloquer S1-07 sur un cas ambigu. Tout contact externe pour cela relève d'une autorisation distincte.
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : clôture de S1-07 si au moins un cas est ambigu ou contesté.

### V-S1-01 — Validation du protocole S1
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : [s1-roslyn.md §Protocole](../spikes/s1-roslyn.md#protocole), corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02, A-03, A-05, A-06), y compris les classes de commandes, la répartition de la timebox (§9), le wrapper `spikes/s1-roslyn/tools/run-logged.sh` et son test `test-run-logged.sh`, puis le 29/10/2026 (A-07) et amendé pour D-18-A le même jour (A-08) : superviseur et runner, `as-runner.sh`, P0 réécrit, sondes S0, export minimal, instantanés, promotions et collecte, finalisation K, OP-01 amendée. Refus de validation consignés dans A-01, A-02, A-03 et A-05. D-18 est `DECIDED` (A-08) ; **V-S1-01 reste `OPEN` jusqu'à la validation explicite du mainteneur**, qui doit notamment valider l'observation IDE de Q3 dans la session graphique du runner ou acter S1-04 `non mesuré` (§6.3).
- Options : valider ; valider avec modifications ; refuser.
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : commit de baseline ; S1-02 à S1-09.

### D-UXR-01 — Validation de la note persona
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : [persona.md](../research/persona.md), version corrigée le 28/10/2026 (A-01).
- Options : valider ; valider avec modifications ; refuser.
- Recommandation (agent, non engageante) : —
- Décision humaine : « D-UXR-01 est validée : consigne-la comme DECIDED à la date de cette instruction. »
- Justification : —
- Conséquences : la note persona est validée dans sa version du 28/10/2026 contenue dans `40a2763` (critères C1–C7) ; toute modification ultérieure exige une nouvelle validation. Elle a été amendée par la suite : voir D-UXR-01-A1.
- Preuves : A-02.
- Tickets débloqués : UXR-02 ; UXR-03 (préparation des canaux).

### D-UXR-01-A1 — Revalidation de la note persona amendée
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : [persona.md](../research/persona.md) amendée le 28/10/2026 à la demande du mainteneur (A-05) : relances conditionnelles de présélection pour C1 et C4, règle « indéterminé = non qualifié », remplacement des participants disqualifiés. La version précédemment validée (D-UXR-01) est celle de `40a2763` ; **l'amendement n'est pas couvert par D-UXR-01**.
- Options : valider l'amendement ; le valider avec modifications ; le refuser (retour à la version de `40a2763`).
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : porte « premier contact UXR », avec D-UXR-02, moyen de suppression renseigné et OP-UXR-01.

### D-UXR-02 — Validation du guide d'entretien et de la règle de codage
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : [interview-guide.md](../research/interview-guide.md), brouillon `DRAFT` corrigé le 28/10/2026 (A-01, A-02, A-03, A-05, A-06). Reste `OPEN` tant que le mainteneur n'a pas fourni le contact pour les demandes de suppression et n'a pas validé le document. Doit être daté avant le premier entretien.
- Options : valider ; valider avec modifications ; refuser.
- Recommandation (agent, non engageante) : —
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : porte « premier contact UXR », avec D-UXR-01-A1, moyen de suppression renseigné et OP-UXR-01 ; ne débloque pas UXR-04 à elle seule.

### OP-UXR-01 — Attestation du stockage de recherche avant tout contact
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : D-13. À attester avant tout premier contact.
- Points à attester :
  1. `~/Documents/hardslice-research/` n'est synchronisé ni par iCloud (notamment l'option « Bureau et Documents », dont `~/Documents` est le périmètre par défaut), ni par Dropbox, ni par OneDrive.
  2. Permissions `0700` sur le dossier (et ses sous-dossiers), `0600` sur chaque fichier ; vérifiables par `stat -f '%Sp %N'` et `find ~/Documents/hardslice-research \( -type d ! -perm 700 \) -o \( -type f ! -perm 600 \)` (sortie vide attendue).
  3. Hors de tout dépôt git ; preuve de consentement et table identité ↔ pseudonyme dans deux fichiers distincts.
  4. Sauvegardes : soit le dossier est exclu de Time Machine et de toute autre sauvegarde, soit les sauvegardes respectent les échéances de D-13 ; méthode consignée.
  5. Moyen de demande de suppression opérationnel et renseigné dans le guide.
  6. **Chemin canonique** : `cd ~/Documents/hardslice-research && pwd -P` donne le chemin attendu, consigné ; aucun composant du chemin n'est un lien symbolique.
  7. **Propriétaire** : le dossier et chaque fichier appartiennent au compte du mainteneur (`stat -f '%Su %N'`).
  8. **Aucun lien symbolique** dans le dossier : `find ~/Documents/hardslice-research -type l` ne renvoie rien.
  9. **ACL macOS** : `ls -led` sur le dossier et `ls -le` sur son contenu ne montrent aucune entrée d'ACL qui accorde un accès à un autre utilisateur ou groupe.
  10. **Attributs de synchronisation** : `xattr -lr ~/Documents/hardslice-research` ne montre aucun attribut de fournisseur de fichiers ou de synchronisation (par exemple `com.apple.fileprovider.*`, `com.apple.icloud.*`, `com.dropbox.*`, `com.microsoft.OneDrive.*`) ; la sortie est consignée.
- Décision humaine (attestation) : —
- Preuves : —
- Tickets débloqués : porte « premier contact UXR », avec D-UXR-01-A1, D-UXR-02 et moyen de suppression renseigné.

### AUT-01 — Commits locaux atomiques
- Statut : DECIDED
- Responsable : Mainteneur
- Date : 2026-10-28
- Contexte : les mesures doivent référencer un état reproductible.
- Options : (a) commits locaux atomiques, sans push ; (b) refus avec dégradation approuvée.
- Recommandation (agent, non engageante) : (a).
- Décision humaine : « commits locaux atomiques autorisés, sans push. Toutefois, ne crée pas encore le commit de baseline : présente d'abord le diff de cette passe corrective. Le baseline sera créé après validation explicite de V-S1-01. »
- Justification : —
- Conséquences : aucun push. A-06 autorise le commit local de la présente passe corrective, qui **n'est pas** le commit de baseline. L'ordre de mesure reste : D-18 décidée → éventuel amendement → validation explicite de V-S1-01 → commit(s) local(aux) atomique(s) de baseline → premier run mesuré, dont la fiche cite le SHA du commit.
- Preuves : A-01 ; A-02 (aucun commit de baseline avant nouvelle validation) ; A-06 (commit correctif seulement).
- Tickets débloqués : commit de baseline après V-S1-01.

### D-S1 — Décision go / no-go technique de S1
- Statut : OPEN
- Responsable : Mainteneur
- Date : —
- Contexte : S1-09. Un faux positif connu non résolu, une incapacité démontrée à l'éviter ou un budget mesurable dépassé impose un no-go technique ; aucune décision humaine ne le convertit en go. Une mesure obligatoire absente laisse le sprint partiel et la porte fermée.
- Options : go ; go sous conditions non bloquantes listées ; no-go technique.
- Recommandation (agent, non engageante) : à rédiger après les mesures.
- Décision humaine : —
- Justification : —
- Conséquences : —
- Preuves : —
- Tickets débloqués : S2A-05 ; ADR-07 (0001) ; TASK-03.

---

## Recommandations consignées (non engageantes)

| Id | Date | Recommandation | Statut |
|---|---|---|---|
| REC-01 | 2026-10-28 | Tant que le remote `origin` reste public, désactiver localement le push vers `origin`, par exemple `git remote set-url --push origin DISABLED` (réversible avec `git remote set-url --push origin https://github.com/toregua/hardslice.git`) ou un hook `pre-push` qui refuse `origin`. **Non appliquée** : aucun hook installé, aucun remote modifié (A-02) | Sans objet tant que le remote reste privé (A-04) ; peut rester non appliquée. À réexaminer avant toute remise en public |

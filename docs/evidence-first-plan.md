> **Statut : historique — inactif depuis le 29/10/2026.** Cet ancien plan « evidence first » est remplacé par le [plan de livraison actif](plan.md). Il reste une référence technique et un registre de risques ; ses phases, portes et critères ne gouvernent plus l'exécution.

# HardSlice : plan « evidence first »

Statut : plan v3.2, 28/10/2026 (passe de cohérence et d'exécutabilité de la v3.1, même stratégie). Correspondance avec la roadmap d'origine en §15.

**Statut historique.** Le texte ci-dessous est conservé dans son état de planification antérieur. La [spécification produit](specifications.md) et le [plan de livraison actif](plan.md) font désormais autorité. **Langue** : document de travail en français, retiré ou traduit au passage en public (§16). **Faits volatils** (versions d'outils, état des concurrents, comportements de bibliothèques) : regroupés et sourcés dans l'annexe A, datée ; le corps du plan n'en garde que les conséquences.

---

## 1. Hypothèses à tester

> **H1 — autocorrection.** Un diagnostic de build conçu comme une instruction de réparation permet-il à un agent de corriger une violation architecturale de façon autonome, avec un meilleur taux de réussite ou un coût de correction inférieur ?

- **H1a (garde-fou)** : à guidance et boucle de vérification identiques, les diagnostics HardSlice améliorent-ils le résultat ? (B contre C)
- **H1b (rédaction du message)** : à détection identique, un message de réparation fait-il mieux qu'un message laconique ? (C contre C−)

> **H2 — adoption.** Un cliquet architectural déterministe (règles précises, baseline, CI protégée) apporte-t-il assez de valeur à une équipe qui l'installe sur un dépôt existant pour qu'elle le garde ?

**Conclusions autorisées**
- Un échec de H1 invalide la promesse d'autocorrection et le positionnement éditorial (« des erreurs écrites pour les agents »), **pas** H2.
- H1 ne se teste que dans un environnement contrôlé (phases 1A à 3). **Aucune conclusion sur le persona d'adoption ni sur H2 avant la validation externe de la phase 4.**
- Les portes sont indépendantes : la **porte technique** de 1A autorise 1B puis H2 ; la **porte autocorrection** autorise seulement les phases 2 et 3. Un échec de la seconde ne bloque jamais la branche H2 (§10).
- L'absence de gardien indépendant du holdout laisse H1 **non mesurée** ; ce n'est ni un échec ni un signal favorable. Elle bloque la phase 3, jamais H2.
- Le pilote produit des **heuristiques de continuation**, pas une preuve statistique (§9.3).

## 2. Décisions conservées

| Sujet | Décision |
|---|---|
| Nom | **HardSlice**, préfixe `HS`, paquets `HardSlice.*`, CLI `hardslice` |
| Invocation | **Canonique : `dotnet hardslice <commande>`**, outil local épinglé dans `.config/dotnet-tools.json` à la même version que les analyseurs. C'est la seule forme utilisée dans les messages, les fiches, les hooks, la CI et les tests ; aucune installation globale n'est documentée. Ce plan abrège parfois en `check`, `verify`, etc. |
| Langue | Dépôt public en **anglais** |
| Cœur | **Diagnostics directement exploitables par un agent** ; **analyseurs Roslyn** comme mécanisme principal |
| Distribution | **Paquets NuGet analyseurs + outil local dès le premier incrément** (flux local, puis préversions publiques de même version) ; jamais de règles copiées dans la solution de l'utilisateur |
| Architecture de référence | Domaine dans un **projet séparé**, requêtes par **projection**, **pas de médiateur** (handlers injectés + décorateurs), **`Result` / `Error`** indépendants du transport |
| Runtime | **Aucune dépendance HardSlice au runtime** de l'application |
| Documentation des règles | **Hors ligne** pour l'agent, `helpLinkUri` pour les humains |
| Adoption | **Progressive** : conventions, baseline, cliquet |
| Benchmark | **Transparent, isolé, pré-enregistré**, jugé par des **tests indépendants de HardSlice**, résultats défavorables publiés |
| Persistance de l'application E1 | **PostgreSQL uniquement** (§8) |
| Licence | MIT, DCO |

Restent acquis de la revue du spec v2 : `internal` ne protège pas les slices entre elles (seule HS016 le fait) ; invariant de chevauchement garanti par une contrainte d'exclusion et expliqué par un service de domaine ; `ICommand<T>` / `IQuery<T>` ; messages sur une ligne ; pas de `TreatWarningsAsErrors` global à l'adoption ; contenu personnel hors du dépôt ; cas d'usage `BookRoom` / `CancelBooking` / `GetBooking`.

## 3. Recentrage

| Produit potentiel | Statut |
|---|---|
| Moteur d'analyse et d'adoption (analyseurs, diagnostics, `dotnet hardslice check`, conventions, baseline, cliquet) | **Noyau** |
| Template architectural (application Bookings complète, `dotnet new`) | Extension conditionnelle **E1** ; avant cela, une mini-application de test écrite à la main |
| Plateforme de benchmark multi-agents | Instrument réduit au pilote ; Q1 et Q2 complets en **E3**, **E4** |

**Persona prioritaire** : le lead technique d'une équipe produit .NET (3 à 15 développeurs) qui maintient une API ASP.NET Core organisée en feature folders ou vertical slices (souvent MediatR ou handlers maison), utilise déjà un agent de code, voit les PR générées faire dériver l'architecture, et veut bloquer les **nouvelles** dérives sans corriger tout le passé. Personas secondaires, plus tard : développeur individuel en greenfield (E1), équipe plateforme (après la v1 core).

**Premier cas d'usage** : adoption sur un dépôt existant organisé en slices. La mini-application (phases 1A–3) n'est qu'un environnement contrôlé et reproductible pour H1.

**Positionnement** : mécanisme indépendant du style d'architecture, **un seul profil livré, vertical slices avec domaine riche**. Les règles qui ne dépendent pas des slices (HS019, HS020) s'appliquent aussi à une solution en couches ; un profil couches (HS002) n'est construit que si un design partner le demande.

**Recherche utilisateurs** : 5 entretiens de 45 min, dont 3 avant la sortie de phase 0 et les 5 avant l'entrée en phase 1B. Après 5 entretiens, au moins 3 sur 5 doivent citer spontanément une dérive d'architecture due à un agent pour conserver le persona ; à 2 sur 5 ou moins, la branche produit s'arrête jusqu'à un pivot de persona et une nouvelle recherche. La phase 1A, qui reste une preuve technique bornée, peut avancer pendant les deux derniers entretiens, **sauf si le signal initial est ≤ 1 sur 3**. **Au moins un design partner** est engagé avant la phase 4 (dépôt réel, 1 h toutes les deux semaines, accord pour mesurer faux positifs, churn et rétention). Aucune extension du catalogue sans validation du noyau par un partenaire.

## 4. Modèle de menace

HardSlice **n'est pas impossible à contourner**.

| Acteur | Visé | Non visé |
|---|---|---|
| Agent coopératif qui dérive par accident (cas principal) | Build rouge avec un diagnostic qui dit comment corriger ; hook Stop qui l'empêche de rendre la main sur un build rouge | — |
| Agent qui cherche à faire passer le build (suppression, sévérité baissée, ajout à la baseline, retrait du paquet, édition du hook) | Contournements **visibles, comptés, et bloqués en CI protégée** | Les empêcher localement : un agent qui écrit dans le dépôt peut retirer tout mécanisme local |
| Administrateur malveillant du dépôt ou de la CI | — | Hors périmètre |
| Chaîne d'approvisionnement | Trusted publishing, versions épinglées | Compromission de nuget.org |

- Analyseurs, hooks et `dotnet hardslice verify` sont des **garde-fous contre les dérives accidentelles**, pas une frontière de sécurité.
- **L'autorité est la CI protégée** : branche protégée, statuts requis (build, `dotnet hardslice verify --base <SHA de base>`), **CODEOWNERS** avec revue obligatoire d'un code owner sur les fichiers sensibles (`CODEOWNERS` lui-même, conventions, baseline, `.config/dotnet-tools.json`, `NuGet.config`, `global.json`, `.editorconfig`, `*.globalconfig`, `*.rsp`, `Directory.Build.*`, `Directory.Packages.props`, `.github/**`, scripts/actions appelés transitivement, configuration des hooks et agents), compte d'agent non code owner.
- **Partage des rôles** : `verify` est un contrôle déterministe et portable. Il vérifie la présence et la couverture syntaxique de `CODEOWNERS`, mais **ne peut pas prouver** sans API GitHub qu'une revue, un statut requis ou une protection de branche sont activés. GitHub impose ces réglages ; le guide d'installation exige leur capture ou leur contrôle manuel, et le CLI ne consulte pas l'API GitHub (§7.3).
- Un hook qui bloque l'édition de fichiers protégés est un confort, hors du noyau.

## 5. Positionnement concurrentiel

Le détail daté et sourcé est en annexe A.2. Conséquences :
- **Ne sont plus des différenciateurs** : « le build bloque » (`dotnet-agent-harness`, Cratis/Architecture), des messages pour agents en soi (Corral, indices de correction de `dotnet-agent-harness`), le gel de la dette (Corral en Java, gel d'ArchUnitNET), les skills `add-slice` (SSW).
- **`dotnet-agent-harness`**, le plus proche en .NET, couvre de la qualité de code générique sur des templates en couches, copie ses analyseurs dans chaque projet (problème de dérive qu'il cherche lui-même à résoudre par un paquet NuGet) et possède une **boucle de collecte des échecs** ; il n'a ni adoption sur l'existant, ni baseline, ni benchmark. HardSlice ne reproduit pas la collecte des échecs dans le noyau : la boucle de retour passe par les transcriptions du pilote et le design partner.
- **Corral** (Java) a publié 12 runs Claude Code : le guidage a changé *comment* l'agent corrige, pas *s'il* corrige. C'est la barre à dépasser et un signal préalable sur H1b : le pilote mesure la qualité structurelle et le coût, pas seulement le build vert.

**Moat** : (1) qualité des diagnostics **mesurée** contre un message laconique ; (2) autocorrection **mesurée**, publiée par agent et par tâche ; (3) adoption incrémentale par conventions ; (4) baseline et cliquet **au niveau du build**, avec contrôle de croissance en CI ; (5) preuves publiques, reproductibles, pré-enregistrées. Le README cite ces projets dans « Prior art » ; l'étude est rafraîchie à chaque go/no-go.

## 6. Règles

### 6.1 Principes

- Peu de règles, très précises, **sans faux positif connu** : un faux positif apprend à l'agent à supprimer la règle.
- Une règle, un mécanisme, un format de message. Analyse par symbole ou par opération ; pas d'analyseur de fin de compilation dans le noyau. Ce qui exige une vue de la solution relève de `dotnet hardslice check`.
- Rendre l'erreur impossible par les types avant de la détecter ; réutiliser les règles .NET existantes avant d'en écrire.
- **Faux positif** : diagnostic émis sur du code qui respecte l'intention documentée dans la fiche de la règle. **Arbitre** : triage publié par le mainteneur avec justification ; les cas contestés sont tranchés par un second relecteur (design partner ou contributeur) ; sur le dépôt d'un partenaire, le partenaire tranche.
- **Opportunité d'évaluation** : site de code classifié avant la décision conforme/non conforme. Pour HS016, toute référence depuis une slice vers un type du périmètre applicatif (même slice, autre slice, frontière explicitement autorisée ou namespace non classifiable) ; pour HS019, toute dépendance ou instanciation d'un handler concret, y compris dans une racine de composition ou un projet de tests autorisé. Le mode statistique interne publie séparément les opportunités évaluées et les diagnostics émis. Tous les diagnostics sont triés ; les seuils d'exposition sont calibrés par règle dans S1, S3 ou le spike de la 3e règle.

### 6.2 Trois catégories

**Catégorie 1 — invariants architecturaux forts.** Candidats au niveau bloquant après validation externe.

| Id | Règle | Remarques |
|---|---|---|
| **HS016** | Une slice n'utilise aucun type d'une autre slice | Règle phare, phase 1A. Classification fermée par namespace : tout type du périmètre applicatif est une slice ou une frontière explicitement autorisée ; un namespace non classifiable est un diagnostic de configuration bloquant |
| **HS019** | Pas de dépendance vers un handler concret ni de `new` hors de la racine de composition et des tests | Phase 1B. Sans elle, les décorateurs sautent en silence |
| HS020 | Un `Result` n'est pas ignoré | Candidate à la 3ᵉ règle (phase 1B) |
| HS003 | Un endpoint n'accède pas à la base | Candidate à la 3ᵉ règle ; exige EF Core dans la mini-application si retenue |
| HS001, HS012, HS005, HS008, HS017 | Domain limité au BCL et à une liste autorisée ; frontières de modules ; handler de commande sans entité en retour ; référence entre agrégats par identifiant ; `Shared` ne dépend pas de `Features` | E2 |
| HS006 (révisée) | Un handler de requête **n'injecte pas** le contexte d'écriture et n'appelle ni `SaveChanges` ni `ExecuteUpdate` / `ExecuteDelete` | Liste d'interdits : autorisation, cache, `TimeProvider`, journalisation restent légitimes. E2 |
| HS002 | Dépendances entre couches | Seulement sur demande d'un partenaire |

**Catégorie 2 — conventions du profil**, jamais incluses dans un niveau bloquant par défaut : HS004 (nommage et emplacement d'une slice), HS007 (pas de setter public ni de collection mutable : empêche certaines formes de domaine anémique, **ne garantit pas** un domaine riche), HS009 (événement = `sealed record` + `IDomainEvent`), HS013 (validateur pour une commande paramétrée), HS015 (forme de l'endpoint : **convention du template**, activée seulement par E1, candidate à la suppression). Toutes en E2.

**Catégorie 3 — recommandations**, jamais bloquantes : HS014/018 (présence d'un test : **prouve la présence, pas la qualité**, devient un indicateur de `dotnet hardslice report` en E2) ; HS010/011 (`DateTime.Now`, `.Result`, `async void`) : configuration des analyseurs existants, sans ID HS.

**Intégrité** : il n'y a **aucune suppression HS dans le code**. Toute dérogation passe par un fichier protégé : la baseline, ou le niveau d'analyse (§7.3). L'ancienne HS000 (« suppression justifiée ») est donc abandonnée.

### 6.3 Sévérité et versions

- **Toute règle HS a une sévérité par défaut Warning, pour toujours.** Le caractère bloquant passe par un **niveau d'analyse opt-in** : `<HardSliceAnalysisLevel>N</HardSliceAnalysisLevel>` sélectionne un `.globalconfig` livré dans le paquet (`HardSlice.Analyzers.level-N.globalconfig`) qui passe en erreur les règles du niveau N.
- **Une version mineure ne rend jamais bloquante une règle qui ne l'était pas** : une nouvelle règle rejoint un nouveau niveau N+1, que l'utilisateur adopte explicitement. Une version majeure peut changer le niveau par défaut, avec note de migration. Sans propriété, aucun niveau n'est bloquant.
- **Précédence** (documentation Microsoft, annexe A.3) : `.editorconfig` utilisateur > `.globalconfig` utilisateur (`global_level` 100 par défaut) > `.globalconfig` du paquet (`global_level` 0 par défaut) ; `-warnaserror` / `-nowarn` en ligne de commande l'emportent sur tout. L'utilisateur peut donc toujours rétrograder une règle localement ; c'est `verify` qui le détecte en CI.
- **Le paquet ne configure aucune règle non-HS.** IDE0130 (namespace = dossier) n'est pas imposée : HS016 déduit la slice du namespace, pas du chemin. L'application de référence d'E1 peut l'activer (elle exige `EnforceCodeStyleInBuild`).
- Le packaging et la précédence du `.globalconfig` sont validés par le spike S1 et par des cas E2E dès la phase 1A.
- Un ID n'est jamais réutilisé ; suivi via `AnalyzerReleases.Shipped.md` / `Unshipped.md`.

### 6.4 Performance

- **Part des analyseurs dans le build** : `-p:ReportAnalyzer=true` et binlog sur la mini-application et une grosse solution OSS ; budget ≤ 5 % du temps de compilation.
- **Latence par analyseur** : `ReportAnalyzer` ne donne pas de p95 par document, donc **benchmark dédié** (exécution des analyseurs sur une compilation fixe via `CompilationWithAnalyzers`, warm-up, ≥ 10 répétitions, médiane) ; budget : médiane ≤ 20 ms par document sur la grosse solution.
- `dotnet hardslice check` sur la mini-application : ≤ 30 s à chaud, ≤ 90 s à froid. Hook Stop : timeout dur à 120 s.
- **Régressions** : le job de performance est informatif sur PR. Une régression > 20 % déclenche une série confirmatoire (5 exécutions sur le même runner) ; seule une régression confirmée rend la CI rouge.

### 6.5 Matrice de tests (chaque règle du noyau)

La colonne « Dès » indique la phase qui exige la ligne ; un critère de sortie qui cite la matrice ne porte que sur les lignes déjà exigibles.

| Cas | Attendu | Dès |
|---|---|---|
| Code généré (sortie de générateur, migrations et racines protégées) | Pas de diagnostic dans le vrai code généré ; un fichier applicatif suivi par git ne peut pas s'auto-exclure par `[GeneratedCode]`, suffixe `*.g.cs` ou `generated_code = true` hors allowlist protégée | 1A |
| Types partiels | Identité déduite du namespace ; un diagnostic par violation | 1A |
| Alias, `global using`, génériques, `typeof` | Violation détectée à travers l'alias et l'argument de type | 1A |
| Compilation déjà cassée | Aucun crash (AD0001), aucun diagnostic sur un symbole en erreur | 1A |
| Niveau d'analyse et précédence | Niveau N bloquant via le `.globalconfig` du paquet ; une rétrogradation utilisateur est appliquée localement comme documenté | 1A |
| Configuration invalide (fichier de conventions mal formé, clé inconnue, version inconnue) | Un diagnostic de configuration clair, jamais de désactivation silencieuse | 1A |
| Classification fermée HS016 | Tout type du périmètre applicatif est rattaché à une slice ou à une frontière autorisée ; un namespace non classifiable est signalé | 1A |
| Multi-targeting | Les violations propres à un TFM sont conservées ; `check` ne dédoublonne que les occurrences identiques (ID, empreinte, fichier et position) observées sur plusieurs TFM | 1B |
| Formes HS019 | Constructeur classique ou primaire, champ, propriété, paramètre, `new`, handler dérivé ou générique fermé, et wrappers configurés (`Func<T>`, `Lazy<T>`, `IEnumerable<T>`) ; cas autorisés et interdits figés avant le corpus | 1B |
| Grosse solution | Budgets §6.4 | 1B |
| Projets de tests | Exclus par convention | 1B |
| Chemins et fins de ligne Windows | Normalisés dans les messages | 1B |
| Détection par `verify` | Toute rétrogradation non autorisée, désactivation des analyseurs, exclusion de leurs assets, suppression HS hors baseline ou divergence de versions fait échouer `verify` (§7.3) | 4 |
| Baseline | Empreintes normalisées (chemins, fins de ligne, TFM), compteurs, nouvelles occurrences, entrées périmées | 4 |
| Versions de schéma (conventions, baseline) | Lecture de N et N−1 ; au-delà, diagnostic de configuration | 4 (exercée au premier passage N → N+1) |

## 7. Diagnostics, conventions, baseline

### 7.1 Diagnostics

Une ligne, quatre parties fixes, figée par capture de référence :

```
src/MyApp/Features/Bookings/CancelBooking/CancelBooking.cs(42,17): error HS016: 'CancelBookingHandler' (slice Bookings/CancelBooking) uses 'BookRoomHandler' (slice Bookings/BookRoom). Slices never reference each other. Fix: move the shared rule into the Booking aggregate (src/MyApp.Domain/Bookings/Booking.cs) or raise a domain event. Doc: dotnet hardslice explain HS016
```

- HS016 est le **diagnostic exemplaire**, relu en phase 1A ; les autres suivent son gabarit.
- **Fiches** embarquées dans le paquet, lues par `dotnet hardslice explain HSxxx` (pas de copie, pas de dérive de version). Sections : pourquoi, comment corriger, **comment ne pas corriger** (supprimer, dupliquer, ajouter à la baseline), skill utile. La copie des fiches dans le dépôt reste l'alternative, tranchée en phase 2 d'après les transcriptions.
- **Message laconique** (`HS016: slice boundary violation.`) : propriété MSBuild interne de traitement, injectée par le runner hors du worktree et utilisée uniquement par le bras C− ; **seul le texte visible du diagnostic change** (fiches, `explain`, `helpLinkUri`, skills, commandes et toute la configuration hors traitement identiques).

### 7.2 Conventions

- **S2a (phase 0)** fixe le minimum pour HS016 : projets du périmètre applicatif, racine et motif de namespace des slices (`<Root>.Features.<Module>.<Slice>`), frontières de namespace explicitement autorisées, projets exclus et allowlist protégée des vraies racines générées ou migrations. La classification est fermée : dans le périmètre applicatif, « non classifiable » n'équivaut jamais à « autorisé ». Ce contrôle de complétude n'est pas HS004 : il n'impose ni dossier ni convention esthétique, seulement une frontière explicite pour rendre HS016 fiable. **S3 (phase 1B)** ajoute pour HS019 les interfaces qui marquent un handler (`ICommandHandler<,>` / `IQueryHandler<,>` par défaut, `MediatR.IRequestHandler<,>` possible), les wrappers de dépendance reconnus, les racines de composition autorisées et l'identification des projets de tests. Ces clés et la matrice des formes HS019 précèdent obligatoirement le corpus externe.
- Format, tranché par S2a : clés `.editorconfig` / `.globalconfig` (natif, pas de parseur) contre `hardslice.json` en `AdditionalFiles` (un fichier versionné, mais un parseur JSON dans un analyseur `netstandard2.0`). Dans les deux cas : version de schéma dès le départ, clés inconnues rejetées.

### 7.3 Baseline et cliquet (conçus par S2b, construits en phase 4)

**Empreinte** (proposition, schéma v1) : `règle | projet (nom d'assembly) | type source (ID de documentation) | cible ou frontière normalisée | nature`, par exemple :

```
HS016|MyApp|T:MyApp.Features.Bookings.CancelBooking.CancelBookingHandler|slice:Bookings/BookRoom|type-reference
```

Jamais de ligne ni de chemin de fichier ; jamais de TFM. En multi-targeting, une même occurrence vue par plusieurs TFM compte **une fois** (dédoublonnage par fichier et position dans `check`). Le fichier a une version de schéma en en-tête et un compteur d'occurrences par empreinte.

**Nouvelles occurrences d'une empreinte déjà listée.** Si la baseline supprime toutes les occurrences d'une empreinte, une nouvelle référence du même type vers la même slice serait invisible dans l'IDE et le hook Stop. S2b choisit explicitement entre :
1. une empreinte plus fine (membre source en plus du type) ;
2. un contrôle local des compteurs par `dotnet hardslice check`, qui compte les occurrences dans le SARIF (les diagnostics supprimés par un `DiagnosticSuppressor` y restent visibles) et échoue si une empreinte dépasse son compteur ;
3. une limite documentée : ce cas n'est bloqué qu'en CI.

Critère : **la boucle locale doit signaler une nouvelle dérive avant la CI** autant que possible. Hypothèse de départ : l'option 2, avec un `DiagnosticSuppressor` unique qui lit la baseline (possible parce que les règles gardent une sévérité par défaut Warning, §6.3 ; annexe A.3).

**Opérations**

| Opération | Commande | Effet |
|---|---|---|
| Initialisation | `dotnet hardslice baseline init --reason "…"` | Crée la baseline depuis les violations actuelles ; la raison d'initialisation est liée exactement à cet instantané ; refusée si elle existe |
| Réduction | `dotnet hardslice baseline update` | Retire les entrées corrigées, baisse les compteurs ; **n'ajoute jamais** |
| Ajout exceptionnel | `dotnet hardslice baseline add <diagnostic> --reason "…"` | Ajoute une empreinte ou augmente son compteur pour une violation actuelle ; la raison est liée à ce delta précis |

**Principe** : **toute dérogation et sa raison vivent dans un fichier protégé par CODEOWNERS**. Chaque raison est liée à un delta déterminé : instantané initial, nouvelle empreinte, hausse d'un compteur ou transition exacte de niveau `N → M`. Une raison ne peut ni autoriser un delta ultérieur ni rester après sa disparition ; `verify` rejette les justifications orphelines ou réutilisées. Une baseline vide est valide et peut ne contenir que la justification d'une transition de niveau. Une dérogation ailleurs (fichier source, `.editorconfig` ou `.globalconfig` de sous-dossier, `.csproj`) n'est jamais acceptée, avec ou sans raison, puisque rien n'y garantit une revue. S2b fixe le schéma exact de ces métadonnées.

**Ce que `dotnet hardslice verify --base <SHA de base>` contrôle** (déterministe, sans API GitHub) : le commit est présent localement, immuable et affiché dans la sortie ; la CI fetch le SHA de base ou le merge-base avant l'appel, sans dépendre d'un checkout shallow ni d'une branche mutable.
- **échec inconditionnel** : tout `#pragma warning disable HS…`, tout `[SuppressMessage]` visant une règle HS, tout `NoWarn` HS, et toute sévérité HS configurée dans un `.editorconfig`, un `.globalconfig` ou un projet autre que le `.globalconfig` livré par le paquet, même pour la rehausser (le seul réglage admis est le niveau d'analyse) ; paquet ou outil local absent ; nouveau marqueur `[GeneratedCode]`, suffixe généré ou configuration `generated_code = true` sur un fichier applicatif suivi par git hors allowlist protégée ;
- **chaîne de confiance et état MSBuild effectif** : version CLI identique à celle des analyseurs et jamais rétrogradée par rapport au SHA de base ; `HardSliceAnalysisLevel` défini uniquement à la racine ; inventaire construit depuis tous les `*.csproj` suivis par git, indépendamment d'une solution modifiable, avec exclusions dans la configuration protégée ; pour chaque projet, TFM et configuration supportés, assembly HardSlice réellement chargé à la bonne version et analyseurs actifs ; aucun `RunAnalyzers=false`, `RunAnalyzersDuringBuild=false`, `ExcludeAssets="analyzers"`, `IncludeAssets` omettant `analyzers`, `Analyzer Remove` ni `DiagnosticSuppressor` tiers visant un ID HS ;
- **couverture des racines de confiance** : `CODEOWNERS` couvre syntaxiquement son propre fichier, le manifeste du tool, les sources NuGet, les workflows et leurs scripts/actions transitifs, les fichiers de build sensibles et l'allowlist de code généré ;
- **baseline** : format et version ; chaque entrée correspond à une violation actuelle (**pas de pré-autorisation**) ; aucune entrée ni justification périmée ; compteurs respectés ; chaque raison correspond exactement au delta observé contre le SHA de base ;
- **croissance par rapport au SHA de base** (nouvelle empreinte, compteur plus élevé, niveau d'analyse abaissé) : elle doit porter une raison stockée dans la baseline protégée, et `verify` la liste dans sa sortie pour la revue.

**Ce que GitHub impose** : les racines de confiance du §4 sont soumises à revue obligatoire d'un code owner et les statuts sont requis par la protection de branche ; toute modification sensible exige cette revue. C'est le prix d'une approbation portable, acceptable puisque chaque PR est déjà revue. Une GitHub Action qui lirait les reviews serait une intégration spécifique, hors du noyau. Un canary embarqué ou généré par la CLI, non modifiable par la PR, vérifie en CI qu'une violation HS est effectivement produite par la chaîne analyseur + configuration avant de tester les cas de contournement.

**Renommages et ré-indexation.** **Toute nouvelle empreinte est une croissance soumise à approbation**, y compris après un renommage : l'outil ne peut pas distinguer de façon déterministe un renommage d'une violation corrigée suivie d'une nouvelle violation vers la même cible. `baseline update` peut **suggérer** des paires probables pour aider la revue, sans les accepter. Seule exception : une migration de schéma (recalcul des empreintes depuis le code, compteur par règle inchangé), qui est déterministe.

**Migrations.** Version de schéma dès la v1 ; lecture de N et N−1. **`dotnet hardslice migrate` n'est construit qu'au premier vrai passage N → N+1.**

## 8. Application de référence complète (conception pour E1)

La mini-application du pilote est **en mémoire, sans base de données** : PostgreSQL, EF Core, `ReadDbContext`, contrainte d'exclusion, événements de domaine et Testcontainers ne précèdent le pilote que si une tâche retenue l'exige (décision en phase 1B). Cette section fixe la conception de l'application complète d'E1, qui sera l'exemple que les agents recopieront.

**Pipeline.** Commandes : Validation → Transaction → Handler ; requêtes : Validation → Handler. Handlers en `Task<Result<T>>`. Tout identifiant nécessaire à l'idempotence est créé **une fois avant l'entrée dans la stratégie de réessai**, par la fabrique du type d'identifiant du domaine (`Guid.CreateVersion7()`), puis porté par la commande et fourni à l'agrégat. Il n'est jamais régénéré dans le handler ; `SaveChanges` n'est donc pas nécessaire pour le connaître. Enregistrement des décorateurs : helper explicite et test de démarrage qui vérifie la chaîne de chaque handler (Scrutor écarté : son `Decorate` générique ouvert ne décore pas les enregistrements génériques ouverts, annexe A.3).

**Validation.** FluentValidation est retenu parce que les règles s'écrivent en code dans la slice, à côté de la commande, qu'elles peuvent dépendre de services injectés et que la bibliothèque est mature. `Microsoft.Extensions.Validation` est utilisable hors d'ASP.NET Core, mais repose sur des attributs DataAnnotations, ses attributs sont expérimentaux en .NET 10 dans une bibliothèque de classes, et la validation asynchrone n'arrive qu'en .NET 11 (annexe A.3). Réévaluation lors du passage à .NET 11.

**Cycle d'une commande**

```
Validation : échec → Result.Failure(Validation), aucune transaction
Transaction (dans la stratégie d'exécution Npgsql si les réessais sont activés)
  pour chaque tentative : nouveau scope DI ; nouveau DbContext ; handler et gestionnaires résolus dans ce scope
    BEGIN
    result ← handler(command)          // AppDbContext, domaine ; n'appelle PAS SaveChanges
    si échec → rollback
    vagues ← 0
    boucle :
      SaveChanges()                    // flush dans la transaction
      événements ← retirer les événements des agrégats suivis, dans l'ordre de levée
      si aucun : sortir
      vagues ← vagues + 1 ; si vagues > 3 : exception (limite de sécurité)
      pour chaque événement, pour chaque gestionnaire : r ← gestionnaire(événement, ct)
        si échec ou exception → rollback
    COMMIT
rollback (tout chemin : échec du handler, échec ou exception d'un gestionnaire, exception, avant chaque réessai)
  = disposer la transaction puis le scope de tentative ; aucun état scoped mutable ne survit au réessai
```

- **`SaveChanges` vit dans le seul décorateur de transaction.**
- **Limite de sécurité de 3 vagues d'événements** : ce n'est **pas** une détection de cycle. Une chaîne légitime plus longue échoue aussi, et un cycle est arrêté sans être identifié.
- **Contrat du dispatcher** : événements traités dans l'ordre de levée **au sein d'un agrégat**. Entre agrégats, l'ordre n'est garanti que si une séquence de levée globale est introduite dans le scope de la commande (à trancher en E1 ; sinon, documenté comme non garanti) ; pour un événement, de 0 à n gestionnaires, appelés **séquentiellement** dans l'ordre d'enregistrement ; le `CancellationToken` de la commande est transmis ; **arrêt au premier échec** (`Result` en échec ou exception), qui annule toute la commande.
- **Réessais et commit incertain** (vigilance E1) : la commande et son identifiant stable existent avant le délégué réessayé ; chaque tentative recrée tout le graphe scoped qui peut porter un état mutable. Une perte de connexion pendant le commit laisse un état indéterminé (documentation EF Core, annexe A.3) : E1 choisit entre une opération idempotente fondée sur cet identifiant stable et une vérification du commit avant tout réessai (`ExecuteInTransactionAsync` avec `verifySucceeded`). Un test d'intégration couvre le rejeu et prouve qu'il ne crée pas un second agrégat.
- **Aucun I/O externe avant le commit** (email, webhook, réseau) : un réessai rejouerait l'effet, un rollback le laisserait orphelin. Les effets externes exigeront une **outbox**, hors v1.
- **Contraintes → `Error`.** `Shared/Persistence` définit une abstraction `ConstraintErrorMapping(schéma, table, nom de contrainte, Error)` ; chaque module enregistre ses entrées par DI dans son `XxxModule.cs` ; le décorateur reçoit `IEnumerable<ConstraintErrorMapping>`. `Shared` ne dépend donc jamais de `Features`. Après rollback, une violation `23P01` (exclusion) ou `23505` (unicité) dont le triplet est déclaré devient l'`Error` correspondante ; une contrainte non déclarée reste une exception. **Le wrapper n'est pas supposé identique** pour `SaveChanges` (`DbUpdateException` avec `PostgresException` interne) et pour le commit (exception éventuellement levée directement) : les tests d'intégration couvrent les deux chemins et les champs `SchemaName`, `TableName` et `ConstraintName`. Concurrence `xmin` → `Error.Conflict` ; à partir de Npgsql 11, `40001` / `40P01` remontent aussi en `DbUpdateConcurrencyException`, à distinguer au passage à EF Core 11.
- **`ReadDbContext`** : l'interface `IReadDb` (`IQueryable<T>` sans suivi) **réduit la surface d'écriture** sans la garantir ; `SaveChanges` qui lève ne couvre ni `ExecuteUpdate` / `ExecuteDelete` ni le SQL brut. **La garantie forte est un rôle PostgreSQL limité à `SELECT`** pour la connexion de lecture, prouvée par un test d'intégration.
- **PostgreSQL uniquement** : `EXCLUDE USING gist` (SQL brut en migration, `btree_gist`), `xmin` (`[Timestamp] uint`), un schéma par module ; SQL Server n'a ni contrainte d'exclusion ni `xmin`. Réévaluation après la v1 core, sur demande d'un partenaire.

## 9. Agents et benchmark

### 9.1 Support des agents

**Le cœur est agnostique : le build et la CI.** Les hooks sont des adaptateurs optionnels et volatils derrière `dotnet hardslice hook <agent> <event>`. Un seul hook dans le noyau, **Stop** ; pas de hook par édition (états intermédiaires non compilables).

| Niveau | Définition | Agents |
|---|---|---|
| **Supporté** | Adaptateur maintenu, version épinglée, scénarios E2E en nightly (≥ 4/5), une régression bloque la release | **Claude Code** |
| **Expérimental** | Adaptateur présent, test de fumée manuel à chaque release | **Codex CLI**, en phase 2 seulement si l'adaptateur tient en 2 jours, sinon E5 |
| **Best effort** | Aucun adaptateur ; `AGENTS.md` demande `dotnet hardslice check` avant de finir ; build et CI seulement | Copilot CLI, Gemini CLI, Antigravity CLI |

Cette matrice est la cible de la branche H1. Si la phase 2 est sautée ou échoue, aucun agent n'est déclaré « supporté » dans la v1 core ; le build et la CI restent utilisables sans adaptateur.

Conséquences (faits datés en annexe A.1) : `AGENTS.md` source unique, plus `CLAUDE.md` = `@AGENTS.md` ; ni `GEMINI.md` ni `copilot-instructions.md` ; skills dans `.claude/skills` seulement tant que Claude Code est le seul supporté (l'emplacement multi-agents est tranché en E5) ; garde anti-boucle HardSlice à 5 blocages consécutifs, sous le plafond natif de Claude Code.

**Skills** (phase 2, présentes dans B, C et C−) : `add-slice` (déclencheur : ajouter ou modifier un cas d'usage) et `extract-to-domain` (déclencheur : deux slices ont besoin de la même règle métier). Étroites, divulgation progressive, ~60 lignes, **aucune redondance** avec les diagnostics ou `AGENTS.md`. `fix-architecture-violation` est supprimée (redondante avec le diagnostic) ; les autres skills attendent E1.

### 9.2 Protocole du pilote et des benchmarks (pré-enregistré sous tag git avant tout run mesuré)

| Élément | Règle |
|---|---|
| Holdout | Suite cachée **fonctionnelle** et **structurelle** (réflexion ou inspection d'assemblies, sans code HardSlice), écrite à partir d'énoncés figés, **scellée** (empreinte SHA-256 publiée dans le pré-enregistrement), **conservée par un tiers** et jamais consultée par le mainteneur avant la fin de tous les runs mesurés. Le tiers ne révèle ni tests ni résultats détaillés intermédiaires. Sans gardien indépendant, aucun pilote mesuré n'est lancé et aucune conclusion sur H1 n'est autorisée |
| Fixtures de développement | Les scénarios E2E des phases 1A et 2 et les tests des analyseurs sont **distincts** des tâches et du holdout du pilote |
| Budgets par run | Temps mur et tokens par type plafonnés (valeurs issues de S4) ; plafond en dollars dérivé de ces plafonds et de la grille figée ; toute valeur et toute formule sont pré-enregistrées ; dépassement = échec |
| Exclusions | **Seules les pannes externes pré-enregistrées** (API du modèle indisponible après réessais du client, crash de l'hôte du runner, quota) sont exclues, et le run est **remplacé**. Un crash, un timeout ou un dysfonctionnement dû à HardSlice, à son hook ou à sa configuration est un **échec** |
| Contournements | Suppression, sévérité ou niveau abaissé, retrait du paquet, ajout à la baseline, édition du hook ou de la configuration, faux marquage de code généré, déplacement vers un namespace non classifiable, désactivation ou suppression de tests : le run est un **échec composite**, compté et publié à part |
| Ordre | **Randomisation bloquée par tâche × répétition** : chaque bloc contient une fois chaque bras, dans un ordre tiré (graine publiée) |
| Isolation | Worktree neuf au même commit, conteneur, **aucune configuration utilisateur ni mémoire** (répertoire personnel neuf ; pour Claude Code, sources de configuration du projet seulement et **pas `--bare`**, qui désactive hooks et skills ; pour Codex, commande candidate `codex -a never -s workspace-write exec --ignore-user-config --dangerously-bypass-hook-trust "<prompt>"`, dont l'ordre exact des options est validé et publié par S4), holdout copié après le run hors du conteneur de l'agent |
| Versions | Agent, modèle, SDK .NET, paquets analyseurs et outil HardSlice épinglés et publiés |
| Analyse | **Par agent et par tâche**, jamais agrégée naïvement ; taux de succès et coûts **toujours publiés séparément** ; données brutes et résultats défavorables publiés |

### 9.3 Micro-pilote (phase 3)

**Bras** (même mini-application, même commit, même agent et modèle) :

| | `AGENTS.md` + 2 skills | Hook Stop → `dotnet hardslice check` | Analyseurs HS (niveau bloquant) | Texte du diagnostic |
|---|---|---|---|---|
| **B — guidance + boucle de vérification, sans analyseurs** | oui | oui | non | — |
| **C — HardSlice** | oui | oui | oui | instruction de réparation |
| **C− — ablation** | oui | oui | oui | laconique |

B/C isole l'effet des analyseurs (H1a) ; C/C− isole le texte du message (H1b). **2 tâches** de fonctionnalité dont un raccourci naturel viole une règle du noyau × **3 bras** × **3 répétitions** = **18 runs valides**, exactement 6 par bras. Plafond : **24 tentatives** au total ; s'il est atteint sans 18 runs valides, le résultat est **inconclusif**.

**Contrôle de manipulation avant les runs mesurés** : sur une fixture semée, le runner prouve que B ne charge aucun analyseur HS ; C et C− chargent exactement le même assembly et la même configuration hors propriété de traitement, injectée par le runner et invisible dans le worktree. Ils produisent les mêmes IDs, emplacements et sévérités ; seul le texte visible diffère. Les résultats de ce contrôle sont joints au pré-enregistrement.

**Métriques**

| Métrique | Définition | Bras |
|---|---|---|
| `succes_fonctionnel` | Les tests fonctionnels du holdout passent à la fin du run | B, C, C− |
| `succes_structurel` | Les tests structurels du holdout passent | B, C, C− |
| **`succes_composite`** (primaire) | Les deux, sans intervention humaine, dans les budgets, sans contournement | B, C, C− |
| `temps_end_to_end`, `cout_end_to_end` | Du lancement du prompt à la fin du run | B, C, C− |
| `temps_diagnostic_vers_vert`, `cout_diagnostic_vers_vert` | De la réception du premier diagnostic HS au premier `check` vert ; le coût commence au premier appel de modèle qui suit cette réception | C, C− seulement |
| Itérations, fichiers lus, violations HS comptées après coup, suppressions | Secondaires | selon le bras |

**Unité de coût** : les seuils portent sur les **dollars**, calculés à partir des tokens comptés (entrée, sortie, lecture et écriture de cache) et d'une **grille de prix figée dans le pré-enregistrement**. Le coût dépend du mélange de types de tokens selon cette grille, mais jamais d'un changement de tarif postérieur au pré-enregistrement. Les tokens par type, la formule et le plafond en dollars sont publiés aussi.

**Exposition au diagnostic** : un run C ou C− est « exposé » s'il a reçu au moins un diagnostic HS. Lire H1a exige **au moins 2 runs C exposés sur 3 pour chaque tâche** ; lire H1b exige la même condition **pour C et pour C−**. Une tâche qui ne la remplit pas rend la lecture `inconclusive` pour l'hypothèse concernée.

**Runs en échec** : les médianes end-to-end portent sur **tous** les runs. Règle pré-enregistrée : un run en échec reçoit la **valeur du plafond pré-enregistré** pour le temps et le coût (pénalisation au plafond). Pour « diagnostic vers vert », un run C/C− sans diagnostic HS n'a pas de valeur et est compté à part ; un run exposé qui n'atteint jamais le vert est pénalisé au plafond. Le critère de coût H1b n'est lisible que si C et C− sont exposés dans 3 runs sur 3 pour chaque tâche. Les coûts observés bruts sont publiés aussi.

**Priorités** : `succes_composite` d'abord, puis `succes_structurel`, puis le coût, qui ne départage qu'à succès composite égal.

**Décision** (heuristiques de continuation produit sur 6 runs par bras, **pas une preuve statistique**) :
- **`continue`** : C atteint d'abord **au moins 2 `succes_composite` sur 3 pour chacune des deux tâches**, avec une exposition suffisante. Ensuite, C a au moins 1 `succes_composite` de plus que B **sur chacune des deux tâches** ; **ou** succès composite égal sur chaque tâche et `cout_end_to_end` médian de C, en dollars, ≤ 75 % de celui de B sur chaque tâche. Dans les deux cas : **sans régression fonctionnelle sur aucune tâche** (`succes_fonctionnel` de C ≥ B séparément pour chaque tâche), 0 faux positif confirmé, 0 contournement dans C. Un avantage porté par une seule tâche n'est pas un signal général.
- **`inconclusive`** : tout le reste, en particulier un signal sur une seule tâche, des écarts d'un run, des données contradictoires, le plafond de tentatives atteint, ou une exposition insuffisante sur une tâche (moins de 2 runs C exposés sur 3 : la tâche ne tentait pas assez la violation). Suite : **une nouvelle expérience** (tâches revues ou répétitions ajoutées), nouveau pré-enregistrement et nouveau holdout.
- **`stop/recentrage`** : incapacité fondamentale confirmée par les transcriptions (**au moins 2 runs C exposés sur 3 pour chacune des deux tâches** où l'agent ne corrige pas ou corrige à côté), ou régression nette (`succes_composite` de C au moins 1 point sous B **sur chacune des deux tâches**). Un échec concentré sur une seule tâche reste `inconclusive` ou spécifique à cette tâche. Conséquence : abandon de la promesse d'autocorrection (H1) ; la phase 4 teste H2 seule.
- **H1b**, lu séparément et non bloquant, si l'exposition de C et de C− est suffisante sur les deux tâches. Signal si, **sur chaque tâche**, C a au moins autant de `succes_composite` que C−, **et** si l'une des deux conditions suivantes est vraie **sur chaque tâche** :
  - C a au moins 1 `succes_composite` de plus que C− ;
  - si C et C− sont exposés dans 3 runs sur 3 pour chaque tâche, le `cout_diagnostic_vers_vert` médian (en dollars) de C est **≤ 80 %** de celui de C−.

  Sinon : `inconclusive` pour H1b.

**Itération** (phases 1A et 2) : une modification du message, de la fiche ou d'une skill, suivie d'une nouvelle série de runs **sur les fixtures de développement**, jamais sur le holdout.

### 9.4 Extensions du benchmark

Q1 complet (E3) : estimand primaire défini avant le calcul ; taille fondée sur un **effet minimal pertinent pré-enregistré**, une cible explicite de puissance ou de précision et une variance conservatrice ou une méthode binomiale exacte. La variance du micro-pilote n'est qu'une entrée d'une analyse de sensibilité, jamais l'unique base du dimensionnement ; revue externe du protocole avant les runs. Q2, couches contre slices (E4) : même domaine et même holdout, version en couches **validée par écrit par un relecteur extérieur favorable aux couches** avant les runs, revue publiée.

## 10. Roadmap

Chaque phase recevra son plan détaillé au moment de démarrer. Les phases 0, 1A, 1B, 4 et la v1 core forment la branche produit minimale. Les phases 2 et 3 forment la branche H1 et sont sautées si la porte autocorrection de 1A reste fermée. Les extensions ne démarrent que si leur déclencheur est rempli.

### Phase 0 — Réduction des risques

**Objectif** : lever les risques qui bloqueraient la première boucle verticale, préqualifier le persona et fixer les tâches et le protocole.

**Livrables**
- **Prioritaires** :
  - **S1 Roslyn** (3 j) : HS016 détectable par opération, à travers les assemblies, en IDE et en CLI ; coût sur une grosse solution ; **packaging du `.globalconfig` par niveau et précédence** (§6.3) ; première calibration du nombre d'opportunités HS016. Décision : frontière analyseur / `check`, faisabilité des budgets et seuil d'exposition HS016.
  - **S4 Runner** (3 j) : run headless de Claude Code dans un conteneur neuf, sans configuration utilisateur mais **avec hooks et skills du projet actifs** (vérifier que le hook Stop se déclenche en mode `-p`) ; capture tokens, coût, appels d'outils ; une répétition à blanc pour chiffrer budgets et coût du pilote. Si Codex est envisagé : validation et publication de la commande exacte, candidate `codex -a never -s workspace-write exec --ignore-user-config --dangerously-bypass-hook-trust "<prompt>"` ; E2E d'une modification réelle puis du hook Stop.
- **S2a Conventions minimales** (1 j) : clés nécessaires à HS016, format (§7.2).
- Note persona et guide d'entretien ; ≥ 3 entretiens ; démarchage d'un design partner et d'un **tiers gardien du holdout**.
- **Énoncés des 2 tâches du pilote** (le holdout sera écrit par le tiers en phase 1B, une fois la mini-application figée).
- **5 ADR** (MADR) : 0001 diagnostics de réparation, analyseurs Roslyn, NuGet ; 0002 slices, domaine séparé, projections, PostgreSQL seul ; 0003 pas de médiateur, décorateurs ; 0004 `Result` / `Error` ; 0005 modèle de menace et autorité de la CI. Reportés : conventions (après S2a), baseline (après S2b), décorateurs (après S3), livraison des fiches (phase 2), kit agents (phase 2).
- Squelette du dépôt privé : LICENSE, README (« experimental »), CONTRIBUTING (DCO), `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `HardSlice.slnx`, CI de build (actions épinglées par SHA, permissions minimales).
- Bannière « superseded, see plan.md » en tête du spec v2.
- **Plan de découpage documentaire** (exécuté au passage en public, §16) : roadmap courte et durable ; recherche concurrentielle datée ; matrice de support des agents datée ; protocole sous `bench/protocols/` ; décisions techniques dans le spec et les ADR.

**Reportés hors phase 0** : S2b (baseline complète) avant la phase 4 ; S3 (décorateurs, réduit au minimum pour HS019) au début de la phase 1B.

**Critère de sortie** : notes S1, S4, S2a conclues avec mesures ; 5 ADR acceptés ; ≥ 3 entretiens synthétisés ; énoncés des tâches figés ; coût estimé du pilote ≤ budget ; CI verte.

**Go/no-go** : no-go technique si S1 montre que HS016 ne peut pas être détectée sans faux positif connu ou dans les budgets → repenser le mécanisme. Si au plus 1 entretien sur 3 confirme le problème, les 2 entretiens restants ont lieu avant la phase 1A ; sinon, 1A peut avancer pendant leur réalisation. Dans tous les cas, les 5 entretiens et la décision ≥ 3/5 ou ≤ 2/5 du §3 précèdent la phase 1B.

### Phase 1A — Première boucle verticale

**Objectif** : la première preuve réelle de `violation → diagnostic → correction par l'agent → build vert`, au plus tôt.

**Livrables**
- **Mini-application compilable sans base de données** : un projet Domain minimal, un projet application avec 2 à 3 slices en mémoire, tests unitaires.
- **HS016 seule**, tests sur les lignes 1A de la matrice §6.5 ; niveau d'analyse 1 par `.globalconfig` du paquet.
- Paquets **locaux** des analyseurs et de l'outil (même version, flux local), avec manifeste `.config/dotnet-tools.json` ; un E2E part d'un checkout neuf, exécute `dotnet tool restore`, puis `dotnet hardslice check`. Les préversions publiques peuvent sortir en 1A si le trusted publishing est prêt, sans retarder l'E2E.
- **Diagnostic exemplaire** HS016, fiche, `dotnet hardslice explain`.
- **`dotnet hardslice check` minimal** : build, tests unitaires, diagnostics HS dédoublonnés et compacts, code de sortie.
- **Cas E2E de build** : un patch de violation fait échouer le build avec l'ID et le message attendus ; un patch conforme passe ; une rétrogradation utilisateur est appliquée localement comme documenté.
- **Un scénario E2E agent** (violation semée, fixture de développement) : l'agent, guidé par `AGENTS.md` qui demande `dotnet hardslice check`, **sans hook**, corrige jusqu'au vert. 5 runs.

**Dépendances** : S1, S2a, S4.

**Deux portes de sortie**
- **Porte technique** : tests et cas E2E de build verts en CI (Linux, Windows) ; HS016, le paquet, le niveau d'analyse et `check` produisent le résultat attendu sans faux positif connu. Cette porte autorise 1B puis la branche H2.
- **Porte autocorrection** : **au moins 3 runs agent sur 5** atteignent un build vert conforme sans intervention ni contournement. Cette porte autorise les phases 2 et 3.

**Go/no-go** : si la porte technique reste fermée, pas de 1B avant révision du mécanisme. À 2/5 ou moins sur la porte autocorrection, jusqu'à 2 itérations (§9.3) ; toujours sous le seuil → analyse publiée des transcriptions, décision `stop/recentrage` anticipée sur H1 et saut des phases 2–3. Si la porte technique est ouverte, 1B puis H2 continuent malgré cet échec.

### Phase 1B — Compléter le noyau mesurable

**Objectif** : les règles du noyau validées hors de la mini-application ; si la branche H1 reste ouverte, l'environnement du pilote est également figé.

**Livrables**
- **S3 minimal** (1 j) puis **HS019** et le pipeline minimal qu'elle protège (interfaces de handlers, racines de composition et projets de tests configurables, décorateur de validation, enregistrement explicite, test de démarrage) ; calibration de son seuil d'exposition.
- **3ᵉ règle** choisie selon les énoncés des tâches si H1 reste ouverte, sinon selon les entretiens et, s'il est déjà engagé, la valeur d'adoption recherchée par le partenaire (HS020 ou HS003 ; HS003 ferait entrer EF Core dans la mini-application), avec un spike borné qui calibre son seuil d'exposition.
- **Si la branche H1 reste ouverte**, mini-application figée pour le pilote, limitée à ce que les tâches exigent ; PostgreSQL, événements, `ReadDbContext`, contrainte d'exclusion et Testcontainers seulement si une tâche l'impose (sinon E1).
- **Si la branche H1 reste ouverte**, holdout écrit et conservé par le tiers, scellé, empreinte publiée. Sans tiers, H1 reste non mesurée et la phase 3 attend ; la phase 2 peut encore produire ses fixtures de développement, et H2 n'est pas bloquée.
- **Corpus externe** : les 3 règles en warning sur ≥ 2 solutions OSS organisées en slices. Pour chaque règle, une note de calibration fixe avant le scan le seuil minimal d'opportunités évaluées ; **200 est la cible initiale, pas un seuil uniforme imposé**. Tous les diagnostics sont triés (vrai ou faux positif, justification publiée). Si le seuil calibré n'est toujours pas atteint après ajout d'un dépôt, la règle n'entre pas dans le niveau 1 : elle est remplacée ou le périmètre v1 est réduit par ADR.
- Lignes 1A et 1B de la matrice §6.5, budgets §6.4 avec benchmark dédié.
- **Préversions analyseurs + outil sur nuget.org**, de même version (trusted publishing, SourceLink).
- Spec v3 et passage du dépôt en public (§16).

**Dépendances** : porte technique de 1A ouverte ; 5 entretiens terminés et persona confirmé selon le §3. La porte autocorrection n'est requise que pour les livrables H1.

**Critère de sortie produit** : toutes les règles retenues pour le niveau 1 sont testées sur les lignes 1A et 1B de la matrice ; **0 faux positif confirmé** sur la mini-application et le corpus, avec le seuil calibré atteint pour chacune ; budgets respectés ; paquet installé depuis nuget.org reproduisant le diagnostic HS016. La sortie produit ne dépend pas du holdout ; la branche H1 ne peut pas entrer en phase 3 tant que son environnement et son holdout ne sont pas prêts.

**Go/no-go** : une règle qui garde un faux positif ou une exposition impossible à obtenir est resserrée, remplacée ou retirée du périmètre v1 par ADR ; la phase ne passe jamais silencieusement avec « exposition insuffisante ». Si HS016 elle-même n'y parvient pas, arrêt et révision du mécanisme.

### Phase 2 — Boucle agent outillée

**Objectif** : la boucle complète, hook compris, fiable sur plusieurs scénarios.

**Livrables** : adaptateur **hook Stop** Claude Code (`dotnet hardslice hook claude stop`, garde anti-boucle) ; les **2 skills** ; `AGENTS.md` (≤ 100 lignes) et `CLAUDE.md` ; runner `bench/run` durci ; **scénarios E2E** de développement pour chaque règle retenue, 5 runs chacun ; décision sur la livraison des fiches ; adaptateur Codex expérimental si ≤ 2 jours.

**Dépendances** : phase 1B et porte autocorrection de 1A ouverte. Si H1 a reçu `stop/recentrage`, cette phase est sautée.

**Critère de sortie** : **≥ 4 runs sur 5** par scénario atteignent un build vert conforme ; temps médian jusqu'au vert ≤ 10 min ; 0 contournement ; garde anti-boucle et timeout testés.

**Go/no-go** : sous le seuil → jusqu'à 2 itérations ; toujours sous le seuil → pas de pilote, analyse publiée des transcriptions et décision `stop/recentrage` anticipée sur H1 ; la phase 4 (H2) reste ouverte.

### Phase 3 — Micro-pilote B/C/C−

**Objectif** : valider le protocole et chercher un signal pratique sur H1 (§9.3).

**Livrables** : pré-enregistrement tagué (`bench/protocols/pilot-v1.md`) avec seuils, empreinte du holdout et graine ; 18 runs valides ; données brutes ; rapport par tâche et par bras ; publication quel que soit le résultat.

**Dépendances** : phase 2 ; gardien indépendant du holdout disponible.

**Critère de sortie** : 18 runs valides en 24 tentatives au plus, ou résultat déclaré inconclusif ; rapport publié.

**Décision** : `continue`, `inconclusive` ou `stop/recentrage` (§9.3). Elle porte sur H1 seulement.

### Phase 4 — Adoption minimale (H2)

**Objectif** : prouver que le noyau s'installe sur un vrai dépôt externe, sans faux positif, qu'il y reste et que l'équipe lui attribue une valeur concrète.

**Livrables** : conventions configurables complètes pour les règles retenues (dont la correspondance MediatR) ; S2b conclu ; `dotnet hardslice baseline init --reason` / `update` / `add` ; `dotnet hardslice verify --base <SHA de base>` ; versionnement du schéma (sans `migrate`) ; **guide d'installation pas à pas** (`dotnet tool restore`, versions CLI/analyseurs identiques, référence du paquet, niveau d'analyse, conventions, `baseline init --reason`, fetch du SHA de base ou du merge-base, workflow CI, racines de confiance sous CODEOWNERS, vérification manuelle des protections GitHub) ; validation sur le dépôt d'un **design partner externe**. Un dépôt OSS peut s'y ajouter comme exposition supplémentaire, **jamais comme substitut**.

**Dépendances** : phase 1B ; S2b ; un design partner externe engagé. **Pas de go vers la v1 core sans lui** : tant qu'il manque, la phase 4 reste ouverte. **Ne dépend pas du résultat de H1** : la phase 4 peut démarrer pendant la phase 3 et se poursuit quelle que soit sa décision.

**Critère de sortie**
- installation par le partenaire **en ≤ 30 min** en suivant le guide, sans aide du mainteneur ;
- exposition minimale : **≥ 10 PR fusionnées** avec les règles actives et, pour chaque règle, le seuil externe fixé par sa note de calibration (**100 opportunités est la cible initiale**) ; tous les diagnostics sont triés. Si le seuil n'est pas atteint en 2 semaines, la période s'étend jusqu'à 4 semaines puis à un second partenaire ; s'il reste inaccessible, la règle sort du niveau 1 ou le périmètre v1 est révisé par ADR ;
- **0 faux positif confirmé** par le partenaire sur cette période ;
- **churn de baseline hors corrections** ≤ 2 % sur toute la période : `100 × (nouvelles occurrences + hausses de compteurs + ré-indexations hors correction) / max(1, occurrences baselinées au début)`. Les réductions dues à des corrections et la PR semée non fusionnée sont exclues ; le nombre absolu est publié avec le taux ;
- une croissance de baseline semée dans une PR est listée par `verify` et bloquée par la revue CODEOWNERS ;
- `check` dans les budgets sur le dépôt du partenaire ;
- règles toujours actives **un mois** après l'installation ;
- entretien de sortie documenté : le partenaire choisit librement de garder ou retirer HardSlice. Le go exige qu'il le garde et cite au moins une valeur concrète (dérive réelle interceptée, revue de baseline utile ou réduction de la charge de revue) ; la seule PR semée ne suffit pas.

**Go/no-go** : **go vers la v1 core** si les critères sont remplis ; sinon, on reste sur le noyau et on itère sur les conventions et les faux positifs.

### v1 core

**Déclencheur** : go de la phase 4. **Ne dépend ni du catalogue étendu ni du benchmark complet.**

**Périmètre** : les règles du noyau validées dans le niveau d'analyse 1 figé ; conventions ; baseline, cliquet et `verify` ; `check`, `explain` ; adaptateur Claude Code obligatoire dans la v1 seulement si la phase 2 a satisfait son critère de sortie **et** si la phase 3 a conclu `continue`, sinon optionnel et non bloquant ; schéma versionné (avec `migrate` seulement si un changement de schéma a eu lieu) ; documentation minimale (README, guide d'installation, une fiche par règle, politique de compatibilité et de versions).

**Critère de sortie** : ≥ 2 dépôts externes actifs depuis ≥ 30 jours, dont le partenaire ; tous encore actifs 30 jours après au moins une mise à jour mineure ; **aucun faux positif confirmé non corrigé** ; **aucun signalement non trié depuis plus de 30 jours** ; paquets stables publiés.

### Extensions conditionnelles

| | Extension | Déclencheur | Critère de sortie |
|---|---|---|---|
| **E1** | Application de référence complète (§8) et template `hardslice-api` + templates d'éléments | v1 core, et ≥ 2 entretiens ou un partenaire qui demandent le greenfield | `dotnet new` puis build et tests verts sur 3 OS ; capture de référence ; une slice conforme ajoutée en ≤ 15 min par un nouvel utilisateur |
| **E2** | Catalogue étendu, une règle à la fois (catégorie 1, puis 2) | v1 core | Par règle : 0 faux positif sur ≥ 3 dépôts dont un partenaire avec l'exposition minimale, budgets, fiche, cas E2E, nouveau niveau d'analyse opt-in |
| **E3** | Benchmark Q1 complet | Décision `continue` en phase 3 et budget | Tous les runs valides ou résultat inconclusif déclaré ; protocole revu de l'extérieur avant les runs ; résultats publiés |
| **E4** | Benchmark Q2, couches contre slices | E3 publié, profil couches existant, relecteur extérieur recruté | Version en couches validée par écrit avant les runs ; résultats et revue publiés |
| **E5** | Autres agents (expérimental → supporté) | Demande de ≥ 2 utilisateurs ou d'un partenaire, et décision `continue` | E2E ≥ 4/5 en nightly sur 2 versions successives de l'agent |

Hors plan : plugin et marketplaces d'agents, PreToolUse, hook par édition, `dotnet hardslice init`, `map`, `migrate` anticipé, collecte des échecs, Aspire, Azure Pipelines, skill `migrate-to-slices`, outbox, SQL Server.

## 11. Indicateurs

**Principaux** : taux de correction autonome (build vert conforme en phases 1A et 2, `succes_composite` en phase 3) ; temps et coût end-to-end, et diagnostic vers vert (§9.3) ; taux de faux positifs par règle, avec l'exposition (phases 1B, 4) ; suppressions et contournements (runs, puis `verify`) ; latence des analyseurs et durée de `check` (§6.4) ; churn de baseline (phase 4) ; dépôts externes actifs ≥ 30 jours ; décision motivée du partenaire de garder ou retirer l'outil ; rétention après une mise à jour mineure (v1 core).

**Secondaires** : étoiles GitHub, téléchargements NuGet, articles, talks.

## 12. Hygiène open source, au bon moment

- **Phase 0** : fichiers de base et CI de build (§10, phase 0).
- **Phase 1B** (premier paquet public) : analyseurs `netstandard2.0`, `EnforceExtendedAnalyzerRules`, correctifs dans un assembly séparé (RS1038), `helpLinkUri` (RS1015), suivi des versions (RS2008), `Microsoft.CodeAnalysis.CSharp` épinglé sur la version du SDK 10.0.1xx (annexe A.3), `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`, trusted publishing, SourceLink, MinVer, CODE_OF_CONDUCT, SECURITY.md.
- **Avant la v1 core** : macOS en CI, CodeQL, Dependabot, Scorecard, SBOM, modèles d'issue et de PR, réservation du préfixe NuGet.
- Licences de l'application de référence : seulement des licences permissives ; état daté en annexe A.3.

## 13. Risques

| Risque | Parade |
|---|---|
| Faux positifs | Peu de règles, corpus externe avec seuil calibré par règle, niveaux opt-in |
| H1 échoue avant le pilote | Deux portes en 1A ; les phases 2–3 s'arrêtent, la branche H2 continue |
| Pilote sans signal faute de tentation | Statut `inconclusive` et une nouvelle expérience, pas de no-go |
| Pilote trop petit | Assumé : heuristiques de continuation ; E3 dimensionné avec effet minimal, méthode conservatrice/exacte et analyse de sensibilité |
| Biais du holdout | Tiers gardien, scellement, fixtures de développement distinctes |
| CLI d'agents volatiles | Build et CI comme cœur ; un seul agent supporté ; faits datés en annexe |
| `verify` ou analyseurs rétrogradés/désactivés | Racines de confiance sous CODEOWNERS, versions identiques, état MSBuild effectif et canary E2E |
| Pas de design partner | Recrutement dès la phase 0 ; pas de go vers la v1 core sans design partner externe |
| Charge d'un mainteneur principal | Phases 1A et 1B courtes, spikes bornés, tout le reste conditionnel |
| Coût du benchmark | Répétition à blanc (S4), plafonds par run et 24 tentatives |

## 14. Incohérences résolues

| Incohérence | Résolution |
|---|---|
| Skills de B livrées après le pilote | 2 skills en phase 2 ; B renommé « guidance + boucle de vérification, sans analyseurs » |
| Validation externe tardive | Corpus externe avec exposition minimale en phase 1B ; partenaire en phase 4 |
| 4 CLI promis, 3 supportés | Matrice §9.1 : un seul agent supporté |
| Phases finales sans critère de sortie | Critères pour chaque phase, pour la v1 core et pour chaque extension |
| Adoption trop tardive | Persona d'adoption ; H2 explicite ; phase 4 indépendante du résultat de H1 |
| Deux sources de vérité | Calendrier du spec v3 et découpage documentaire (§16) |
| SQL Server annoncé en option | PostgreSQL seul (§8) |
| Phase 1 trop lourde avant le premier E2E agent | Phase 1A (HS016, sans base) puis 1B ; baseline et décorateurs reportés |
| Coût « depuis le premier diagnostic » non comparable entre B et C | Métriques end-to-end pour les trois bras ; diagnostic vers vert pour C et C− seulement |
| Exclusions incompatibles avec 6 runs par bras | Remplacement, 24 tentatives au plus, sinon inconclusif ; les échecs dus à HardSlice ne sont pas exclus |
| « Absence de H1 = pas de raison d'être » contre « recentrage sur l'adoption » | H1 et H2 séparées, conclusions autorisées (§1) |
| `verify` censé savoir qu'un code owner a approuvé | `verify` déterministe ; approbation par CODEOWNERS et protections de branche |
| Nouvelles occurrences invisibles localement | Choix explicite par S2b, priorité à la détection locale |
| Ré-indexation automatique ambiguë | Toute nouvelle empreinte exige une approbation, sauf migration de schéma |
| Passage en erreur dans une version mineure | Niveaux d'analyse opt-in ; une mineure ne rend jamais une règle bloquante |
| IDE0130 en erreur pour un profil censé ne bloquer que les règles HS | Le paquet ne configure aucune règle non-HS |
| E6 exigeait catalogue étendu et benchmark complet avant toute v1 | v1 core sans E2 ni E3 |
| Critère de phase 4 fondé sur `dotnet hardslice init`, absent des livrables | Guide d'installation pas à pas ; `init` hors plan |
| `migrate` construit sans migration réelle | Construit au premier passage N → N+1 |
| p95 par document attendu de `ReportAnalyzer` ; CI rouge sur une régression bruitée | Benchmark dédié ; série confirmatoire |
| « Profondeur 3 » présentée comme détection de cycle ; contrat du dispatcher et nettoyage du `DbContext` implicites ; table des contraintes créant une dépendance `Shared` → `Features` ; wrappers d'exception supposés identiques | §8 |
| `IReadDb` présenté comme garantie | Réduction de surface ; garantie par le rôle `SELECT` |
| Validation .NET dite « liée aux endpoints » | Justification de FluentValidation corrigée (§8, annexe A.3) |
| Antigravity « version non trouvée » ; options de Codex absentes ; versions d'agents dans le corps du plan | Annexe A datée et sourcée ; options intégrées à S4 |
| Matrice exigée en entier en 1B alors que `verify` et la baseline arrivent en phase 4 | Colonne « Dès » dans la matrice §6.5 |
| Suppression HS acceptée « avec raison » dans un fichier source, hors CODEOWNERS | Aucune suppression dans le code ; dérogations seulement dans des fichiers protégés (§7.3) ; HS000 abandonnée |
| Exposition au diagnostic globale (3 sur 6), unité de coût et portée du « +1 » de H1b ambiguës | Exposition par tâche (≥ 2/3, pour C et C−), dollars selon une grille figée, conditions exigées sur chaque tâche (§9.3) |
| Repli sur un dépôt OSS en phase 4 alors que le partenaire est exigé | Pas de go vers la v1 core sans design partner externe |
| Critère v1 tolérant un faux positif récent | Aucun faux positif confirmé non corrigé, aucun signalement non trié depuis plus de 30 jours |
| `hardslice` et `dotnet hardslice` mélangés | `dotnet hardslice` canonique (§2) |
| H2 annoncée indépendante mais bloquée par le go agent de 1A | Deux portes en 1A ; seule la porte technique conditionne 1B et H2 |
| Runner Codex en lecture seule | Sandbox `workspace-write` et politique d'approbation non interactive validés par S4 |
| `verify` exécuté depuis un manifeste modifiable et matrice de contournement incomplète | Racines de confiance protégées, versions CLI/analyseurs identiques, état MSBuild effectif et canary (§4, §7.3) |
| Baseline initiale et baisse de niveau sans emplacement de raison | `baseline init --reason` et métadonnées de justification protégées dans la baseline |
| HS019 scanné sur des dépôts externes sans connaître les racines de composition | Conventions HS019 minimales livrées par S3 avant le corpus |
| Seuils d'exposition uniformes et phase sans issue | Calibration par règle ; remplacement ou réduction de périmètre par ADR si le seuil reste inaccessible |
| Critères du pilote encore agrégés ou sans effet minimal | Non-régression et échec lus par tâche ; effet H1b ≥ 20 % ; contrôle de manipulation |
| Namespace non classifiable ou faux code généré utilisés comme échappatoire | Classification fermée ; allowlist générée protégée ; détection par `verify` et comptage comme contournement |
| Base mutable, checkout shallow ou projet retiré de la solution | SHA immuable fetché ; inventaire de tous les projets suivis par git ; état MSBuild vérifié par TFM/configuration |
| Raisons de baseline génériques ou réutilisables | Chaque raison est liée à un delta exact et devient invalide avec lui |
| Réessai avec identifiant ou état scoped régénéré | Identifiant stable avant le retry ; nouveau scope DI et nouveau `DbContext` par tentative |
| Héritées de la v3 : `--bare` dans le benchmark ; affirmations inexactes sur `DiagnosticSuppressor`, `copilot-instructions.md`, Gemini CLI ; « build qui bloque » comme différenciateur ; plan en français dans un dépôt anglais | §9.2, §7.3, §9.1, §5, §16 |

## 15. Correspondance avec la roadmap d'origine

| Ancienne phase | Devient |
|---|---|
| 0 — Spec v3, 10 ADR, squelette | Phase 0 : S1, S4, S2a, 5 ADR, squelette ; S2b → avant la phase 4 ; S3 → phase 1B ; spec v3 → sortie de 1B |
| 1 — Application de référence complète et template | Mini-application sans base (1A), complétée au strict besoin du pilote en 1B seulement si H1 reste ouverte ; application complète et template → E1 |
| 2 — 9 analyseurs, `check`, hook, NuGet | HS016 + `check` (1A) ; HS019 + 3ᵉ règle + NuGet public (1B) ; hook (phase 2) ; 6 règles → E2 |
| 3 — Pilote B/C | Phase 3 : B renommé, bras C−, métriques end-to-end, décision à trois issues |
| 4 — Catalogue, `verify`, PreToolUse, templates d'éléments, 6 skills, 4 CLI | `verify` → phase 4 ; catalogue → E2 ; templates → E1 ; 2 skills → phase 2 ; autres agents → E5 ; PreToolUse hors plan |
| 5 — Benchmark Q1 | E3 |
| 6 — Adoption | Phase 4 (H2), sans `init` ni `migrate` |
| 7 — Benchmark Q2 | E4 |
| 8 — Stabilisation v1.0 | **v1 core** après la phase 4, sans catalogue étendu ni benchmark complet |

## 16. Spec v3 et découpage documentaire

- **Phase 0** : bannière « superseded » sur le v2 ; ce plan fait autorité.
- **Phase 1B** : rédaction du spec v3 en anglais (`docs/specification.md`), limité au noyau validé ; les extensions y figurent comme hors périmètre conditionnel.
- **Sortie de 1B**, au passage en public, **dans un même commit** :
  - ajout du spec v3 et suppression du v2 (conservé dans l'historique) ;
  - découpage de ce plan en : `docs/roadmap.md` (court et durable : hypothèses, phases, critères, décisions ouvertes) ; une note de recherche concurrentielle datée ; une matrice de support des agents datée ; `bench/protocols/` pour le protocole ;
  - ce fichier en français est retiré du dépôt.
- **Ensuite** : le spec évolue par versions mineures (v3.1 après la phase 2 si elle a lieu, version suivante après la phase 4) ; chaque changement structurant passe par un ADR.

**Sections à reprendre dans le spec v3** : H1/H2 et positionnement (§1, §3) ; modèle de menace (§4) ; règles, catégories, sévérités et niveaux d'analyse (§6) ; diagnostics, fiches, conventions, baseline et `verify` (§7) ; application de référence (§8, au moment d'E1) ; support des agents (§9.1) ; CLI (`check`, `explain`, `hook`, `baseline`, `verify`) ; indicateurs (§11). Le protocole (§9.2–9.4) part dans `bench/protocols/`.

## 17. Décisions ouvertes

| Décision | Tranchée par | Quand |
|---|---|---|
| Frontière analyseur / `check` ; faisabilité des budgets ; packaging du `.globalconfig` ; seuil d'exposition HS016 | S1 | Phase 0 |
| Isolation du runner, budgets par run, coût du pilote | S4 | Phase 0 |
| Format et clés minimales des conventions | S2a | Phase 0 |
| Persona confirmé | 5 entretiens | Avant la phase 1B |
| Enregistrement des décorateurs, conventions minimales et seuil d'exposition HS019 | S3 minimal | Début de phase 1B |
| 3ᵉ règle (HS020 ou HS003) ; besoin éventuel de persistance dans la mini-application | Tâches du pilote si H1 reste ouverte ; entretiens et partenaire sinon | Phase 1B |
| Livraison des fiches : `explain` ou copie dans le dépôt | Transcriptions E2E | Phase 2 |
| Adaptateur Codex expérimental | Effort mesuré (≤ 2 j) | Début de phase 2 |
| Seuils, graine et holdout du pilote | Pré-enregistrement | Avant le premier run |
| `continue` / `inconclusive` / `stop/recentrage` sur H1 | Pilote | Fin de phase 3 |
| Empreinte (granularité, projet), compteurs, nouvelles occurrences locales, analyseur ou `DiagnosticSuppressor`, format et métadonnées de justification | S2b | Avant la phase 4 |
| v1 core | Critères de phase 4 | Fin de phase 4 |
| HS002, SQL Server, hook par édition | Demande d'un partenaire, ou résultat du pilote | Après la v1 core |

---

## Annexe A — Note de recherche datée (vérifiée le 28/10/2026)

Faits volatils, à revérifier à chaque go/no-go. Sources primaires entre parenthèses.

### A.1 Agents

| Agent | Version observée | Instructions | Skills | Hook de fin de tour | À noter |
|---|---|---|---|---|---|
| Claude Code | 2.1.312 (npm, 27/10/2026) | `AGENTS.md` natif depuis 2.1.277 s'il n'y a pas de `CLAUDE.md` ; `@AGENTS.md` supporté ([memory](https://code.claude.com/docs/en/memory#agents-md)) | `.claude/skills` uniquement ([skills](https://code.claude.com/docs/en/skills)) | Stop : exit 2 ou `decision: "block"`, `stop_hook_active`, plafond de 8 blocages consécutifs ; PostToolBatch existe ([hooks](https://code.claude.com/docs/en/hooks)) | `--bare` désactive hooks, skills et `CLAUDE.md` ([CLI](https://code.claude.com/docs/en/cli-reference)) |
| Codex CLI | 0.165.0 (27/10/2026) | `AGENTS.md` natif, 32 Kio par défaut ([agents-md](https://learn.chatgpt.com/docs/agent-configuration/agents-md)) | `.agents/skills` ([skills](https://learn.chatgpt.com/docs/build-skills)) | `.codex/hooks.json` ou `config.toml`, projet de confiance requis ; Stop `decision: "block"` ou exit 2 ; `--dangerously-bypass-hook-trust` ([hooks](https://learn.chatgpt.com/docs/hooks)) | L'approbation et le sandbox sont des options globales placées avant `exec` ; commande candidate `codex -a never -s workspace-write exec --ignore-user-config --dangerously-bypass-hook-trust "<prompt>"` ([CLI](https://learn.chatgpt.com/docs/cli/reference), [non-interactive](https://learn.chatgpt.com/docs/non-interactive-mode)) ; commande exacte et Stop sous `exec` validés par S4 |
| Copilot CLI | 1.0.105 (28/10/2026), GA le 25/02/2026 ([changelog](https://github.blog/changelog/2026-02-25-github-copilot-cli-is-now-generally-available/)) | `AGENTS.md` natif ; la revue de code sur GitHub.com lit aussi `AGENTS.md` ([instructions](https://docs.github.com/en/copilot/reference/custom-instructions-support)) | `.github/skills`, `.claude/skills`, `.agents/skills` ([skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills)) | `agentStop` `decision: "block"`, plafond de 8 ([hooks](https://docs.github.com/en/copilot/reference/hooks-reference)) | Lit aussi les hooks de `.claude/settings.json` : risque de double exécution |
| Gemini CLI | 0.66.0 (27/10/2026) | `GEMINI.md`, `context.fileName` | `.agents/skills` | AfterAgent `decision: "deny"` ou exit 2 | Ne sert plus les comptes individuels depuis le 18/06/2026 ([annonce](https://github.com/google-gemini/gemini-cli/discussions/28017)) |
| Antigravity CLI | 1.4.0 (16/10/2026, [changelog](https://antigravity.google/docs/changelog)) | `GEMINI.md` et `AGENTS.md` | `.agents/skills` ([skills](https://antigravity.google/docs/skills)) | `.agents/hooks.json`, Stop `decision: "continue"` ([hooks](https://antigravity.google/docs/hooks)) | Schéma de hooks différent de Gemini CLI |

### A.2 Concurrents

- **[ryan75195/dotnet-agent-harness](https://github.com/ryan75195/dotnet-agent-harness)** : MIT, 2 étoiles, créé le 02/05/2026, dernier push le 22/09/2026, aucune release ni paquet NuGet (issue #17 ouverte pour en créer un). 16 analyseurs communs + 3 Durable Functions, de qualité de code générique (longueur de méthodes et de classes, interdiction des commentaires, couverture des méthodes publiques…), warning promus par `TreatWarningsAsErrors`, sans correctif, sans `helpLinkUri`. Messages d'une ligne avec un indice de correction, sans lien vers une fiche. Templates `cli`, `etl-api`, `mcp`, `durable` en couches. Analyseurs copiés dans chaque projet (75 % des lignes selon leur mesure), mise à jour par réinstallation du template ou une skill `harness-update`. Collecte des échecs du pre-commit dans `~/.agent-harness/feedback/events.jsonl`, regroupés en propositions d'issues. Hooks Claude Code (PreToolUse), Codex dans un template, hooks git, plugin Claude. Ni baseline, ni adoption sur l'existant, ni benchmark.
- **[MilczekT1/Corral](https://github.com/MilczekT1/Corral)** (Java/ArchUnit, 2 étoiles, push le 25/10/2026) : messages WHY / HOW TO FIX / HOW NOT TO FIX, dette gelée, `docs/evidence.md` avec 12 runs Claude Code.
- **[Cratis/Architecture](https://github.com/Cratis/Architecture)** : 26 analyseurs destinés aux agents, sur NuGet.
- **[codewithmukesh/dotnet-claude-kit](https://github.com/codewithmukesh/dotnet-claude-kit)** : 791 étoiles, skills, hooks et serveur MCP Roslyn de détection, sans blocage du build ni baseline.
- **[SSW.VerticalSliceArchitecture](https://github.com/SSWConsulting/SSW.VerticalSliceArchitecture)** : 412 étoiles, template slices avec règles `.claude` et skills `add-slice`.
- Plus jeunes : `e128/dotnet-reference`, `heurexai/speckit-doti`, `purview-dev/architests`, `dca-dotnet`, `AI.Analyzer.Rules`.

### A.3 Plateforme et bibliothèques

- **`DiagnosticSuppressor`** : ne peut pas supprimer un diagnostic dont la sévérité **par défaut** est Error, ni un diagnostic `NotConfigurable` ; peut supprimer un warning promu en erreur ([DiagnosticSuppressor.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/DiagnosticSuppressor.cs)).
- **Précédence des configurations** : `.editorconfig` > `.globalconfig` ; entre `.globalconfig`, le `global_level` le plus élevé l'emporte (100 par défaut pour un fichier nommé `.globalconfig`, 0 sinon) ; `-warnaserror` / `-nowarn` l'emportent sur tout ; distribution dans un paquet via `GlobalAnalyzerConfigFiles` ([configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)).
- **Roslyn** : SDK 10.0.1xx livré avec `Microsoft.CodeAnalysis.CSharp` 5.0.0 ; un analyseur ne doit pas référencer un Roslyn plus récent que le plus ancien compilateur visé ([NuGet packages](https://github.com/dotnet/roslyn/blob/main/docs/wiki/NuGet-packages.md)) ; RS0030 n'a pas d'ID personnalisable.
- **`Microsoft.Extensions.Validation`** : « can be used in scenarios outside ASP.NET Core » ; fondé sur DataAnnotations ; attributs expérimentaux en .NET 10 dans une bibliothèque de classes (ASP0029) ; validation asynchrone à partir de .NET 11 ([validation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/validation?view=aspnetcore-10.0)).
- **Injection de dépendances** : pas d'API de décoration en .NET 10 ; `Decorate` de Scrutor 7 avec types ouverts ne décore que les enregistrements fermés ([source](https://github.com/khellang/Scrutor/blob/master/src/Scrutor/OpenGenericDecorationStrategy.cs)).
- **Npgsql EF Core 10** : contraintes d'exclusion non gérées par les migrations ([efcore.pg#1975](https://github.com/npgsql/efcore.pg/issues/1975)) ; `xmin` par `[Timestamp] uint` ; à partir de Npgsql 11, `40001` / `40P01` en `DbUpdateConcurrencyException` ([concurrency](https://www.npgsql.org/efcore/modeling/concurrency.html)) ; `ExecuteUpdate` / `ExecuteDelete` contournent `SaveChanges` ([EF Core](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)) ; un échec de connexion pendant le commit laisse l'issue de la transaction inconnue, d'où l'idempotence ou la vérification du commit ([connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue)).
- **Licences** : FluentValidation Apache-2.0 ; Shouldly BSD-3-Clause (dernière stable 4.3.0, janvier 2025) ; exclus : MediatR ≥ 13 et AutoMapper ≥ 15 (Lucky Penny, RPL-1.5 ou commerciale, gratuite sous 5 M$ de CA), FluentAssertions 8 (Xceed, non commerciale seulement), MassTransit 9 (commerciale, gratuite sous 1 M$ de CA) ([nuget.org](https://www.nuget.org/packages/Shouldly), [mediatr.io](https://mediatr.io/), [xceed](https://xceed.com/fluent-assertions-faq/), [massient](https://massient.com/)).
- **Plateforme** : .NET 10 LTS jusqu'au 14/11/2028, .NET 8 et 9 jusqu'au 10/11/2026 ([releases-index](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json)) ; NuGet trusted publishing via OIDC GitHub Actions ([doc](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)).

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

## 2. Incrément terminé — première boucle verticale, pré-v0.1

Statut : terminé le 29/10/2026. PB-001 à PB-008 sont validés et poussés ; la CI est verte. Cette section est conservée comme historique.

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

## 3. Incrément actif — Fondation v0.1

### Résultat attendu

> Depuis un checkout neuf, `dotnet new hardslice-api` génère une application Bookings représentative : un domaine riche dont l'invariant de chevauchement tient sous concurrence, des commandes validées qui passent par un médiateur maison, une requête lue par projection, une persistance PostgreSQL par EF Core et ses migrations, des tests unitaires, des tests d'intégration Testcontainers et les tests d'architecture HS001, HS012 à HS014 et HS016 à HS018. `dotnet new hardslice-api --no-sample` génère la même fondation sans exemple. Dans les deux cas, `scripts/check.sh` et `scripts/check.ps1` sont verts, et un agent peut modifier l'application par les chemins documentés.

### Choix de périmètre

- PostgreSQL est le seul fournisseur de l'incrément. SQL Server en option et l'orchestration Aspire (`--aspire`) sont différés à l'incrément 8.
- `hardslice.json` et `.editorconfig` sont générés sous une forme minimale (PB-014). La configurabilité générale des conventions reste en v0.5, les niveaux d'analyse en v0.2.
- L'emplacement de la persistance et des configurations EF Core, issu de la question n° 7 de la spécification, est tranché par PB-009.
- Le choix entre ArchUnitNET et NetArchTest est réalisé dans PB-014.
- Les tests d'intégration font partie de `scripts/check.sh` et `scripts/check.ps1`, qui détectent clairement l'absence de Docker.
- `BookingConfirmed` est levé et enregistré par l'agrégat. Sa publication et ses handlers ne font pas partie de l'acceptation v0.1.
- Chaque règle d'architecture livrée dans cet incrément est livrée avec sa fiche `docs/rules/HSxxx.md`.

### Critères communs à tous les tickets de l'incrément

Ces critères s'ajoutent à l'acceptation propre de chaque ticket :

- tout ticket qui modifie le template met à jour `scripts/test-template.sh` pour couvrir sa capacité dans la solution générée depuis le paquet local, et l'exécute : tester les sources de `content/hardslice-api` depuis la solution du dépôt ne suffit pas ;
- à partir de PB-009, la CI exécute `scripts/demo-hs016-agent-fix.sh prepare` ; tout ticket qui modifie la structure du template ou des slices adapte la fixture PB-008 dans le même changement et garde ce job vert ;
- `BookingsReferenceTests` reste vert : la référence Bookings compile avec ses dépendances réelles et ne produit aucun HS016 ;
- `AGENTS.md` et les fiches de règles sont mis à jour dans le ticket qui change la carte, la consigne ou l'exemple qu'ils décrivent ;
- aucun assembly HardSlice n'est chargé au runtime de l'application générée ;
- la licence de toute nouvelle dépendance est vérifiée au moment de son introduction.

### Graphe des dépendances

Les tickets sont numérotés dans un ordre d'exécution possible. Le graphe réel autorise le parallélisme suivant :

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-009 | incrément 1 | PB-014 |
| PB-010 | PB-009 | PB-011, PB-012, PB-013, PB-014, PB-015 |
| PB-011 | PB-009 | PB-010, PB-012, PB-013, PB-014, PB-015 |
| PB-012 | PB-009 | PB-010, PB-011, PB-014 |
| PB-013 | PB-009, PB-012 | PB-010, PB-011, PB-014, PB-015 |
| PB-014 | incrément 1 | PB-009 à PB-013 |
| PB-015 | PB-012, PB-014 | PB-010, PB-011, PB-013 |
| PB-016 | PB-010, PB-011, PB-013, PB-015 | aucun |
| PB-017 | PB-016 | aucun |

PB-010, PB-011 et PB-012 modifient tous `Booking` ou `ConfirmBooking` : ce sont des points de fusion, pas des dépendances. Le ticket intégré en second se rebase sur le premier.

### PB-009 — Persister les réservations dans PostgreSQL

**Résultat visible**

L'application générée confirme et annule réellement des réservations dans PostgreSQL, avec le comportement HTTP existant : `POST /bookings`, `POST /bookings/{id}/cancel`. Ses tests d'intégration Testcontainers passent dans la solution générée depuis le paquet local.

**Décision — emplacement de la persistance (résolution de la question n° 7 de la spécification)**

- `Shared/Persistence/` contient l'infrastructure transverse : `AppDbContext`, son enregistrement, la configuration de la connexion et les migrations. `AppDbContext` ne déclare aucun type de module : il applique les configurations découvertes dans l'assembly, et les slices accèdent aux agrégats par `Set<T>()`.
- La configuration EF Core d'un agrégat vit au niveau du module qui l'utilise (`Features/<Module>/<Aggregate>Configuration.cs`, `internal`), hors de toute slice. Ajouter un module ne modifie donc pas `Shared`.
- Compatibilité HS001 : le mapping est entièrement en API fluente hors du Domain ; `MyApp.Domain` ne reçoit ni attribut ni référence EF Core.
- Compatibilité HS017 : `Shared` ne référence aucun type de `Features` ; il dépend uniquement du domaine et du framework, dans le dossier autorisé `Persistence`.
- Compatibilité HS016 : la configuration appartient au module, pas à une slice, et aucune slice ne la référence.

**Livrables**

- `AppDbContext`, EF Core et Npgsql dans `src/MyApp`, versions centralisées dans `Directory.Packages.props` du template ;
- configuration EF Core de `Booking` conforme à la décision ci-dessus, sans constructeur public ni setter ajoutés au domaine ;
- migration initiale versionnée, valide après le renommage `MyApp` → nom choisi par `dotnet new` ;
- connexion lue dans `ConnectionStrings`, sans secret de production versionné ;
- outil `dotnet-ef` épinglé dans `.config/dotnet-tools.json` ; les migrations s'appliquent par `dotnet ef database update` ou par la fixture de tests, et l'application ne migre pas la base à son démarrage ;
- `ConfirmBooking` et `CancelBooking` utilisent `AppDbContext` ; `IBookingStore` et `InMemoryBookingStore` sont supprimés ;
- projet `tests/MyApp.IntegrationTests` (Testcontainers PostgreSQL, hôte web de test, migrations appliquées sur le conteneur), ajouté à `MyApp.slnx` et à `HardSlice.slnx` ;
- tests d'intégration au même chemin que leur slice ; les tests de handlers qui dépendaient du stockage en mémoire deviennent des tests d'intégration, sans fournisseur EF InMemory ni SQLite ;
- `scripts/check.sh` et `scripts/check.ps1` exécutent les tests d'intégration après les tests unitaires et d'architecture ; avant cette étape, ils échouent avec un message qui nomme le prérequis si aucun runtime de conteneurs compatible Docker n'est joignable ;
- `scripts/test-template.sh` exécute les tests d'intégration de la solution générée par les deux scripts et contient une sonde isolée qui simule un runtime de conteneurs injoignable afin de vérifier le message de prérequis sans désactiver le parcours positif ;
- job CI exécutant `scripts/demo-hs016-agent-fix.sh prepare` depuis un checkout propre ; fixture PB-008 et vérificateur de la démonstration adaptés à la nouvelle forme des slices et aux chemins de tests qu'elle utilise ;
- `AGENTS.md` et `docs/rules/HS016.md` ne citent plus `IBookingStore` et donnent un exemple de contrat public de module valide.

**Dépendances**

Incrément 1 terminé.

**Critères d'acceptation**

- Une réservation confirmée par HTTP est relue depuis PostgreSQL par un contexte neuf, et une seconde instance de l'application sur la même base l'annule.
- Annuler deux fois renvoie 400 et l'état persisté reste `Cancelled` ; une réservation inconnue renvoie 404.
- Le modèle EF Core ne contient aucune modification en attente par rapport aux migrations, dans la solution générée.
- `MyApp.Domain` ne référence toujours que la bibliothèque de classes de base : les tests HS001 existants restent verts.
- Aucun type de `Shared` ne référence un type de `Features`. Si PB-014 est intégré, HS017 le vérifie ; sinon, PB-014 en fait un cas d'acceptation.
- Sans runtime de conteneurs joignable, les deux scripts de vérification échouent avec un code non nul et le message de prérequis.
- Les jobs CI build, template et démonstration réussissent ; le job build exécute aussi les tests d'intégration du template.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- invariant de chevauchement (PB-010), événement de domaine (PB-011), médiateur et validation (PB-012), requête `GetBooking` (PB-013) ;
- SQL Server et Aspire (incrément 8) ;
- migration automatique au démarrage, données d'exemple, multi-tenant ;
- test d'architecture HS017 lui-même (PB-014).

### PB-010 — Interdire le chevauchement de deux réservations d'une même salle

**Résultat visible**

`POST /bookings` refuse une réservation dont la période chevauche une réservation confirmée de la même salle avec une réponse 409 `ProblemDetails` qui explique le conflit. L'invariant tient sous concurrence : sur des confirmations simultanées pour un même créneau, une seule réussit.

**Livrables**

- `BookingPeriod.Overlaps` sur des intervalles semi-ouverts `[début, fin)` : deux périodes adjacentes ne se chevauchent pas ;
- règle « pas deux réservations confirmées qui se chevauchent sur la même salle » portée par `MyApp.Domain/Bookings/` ; le handler charge les réservations confirmées de la salle sur la période et les fournit au domaine, sans comparer lui-même de périodes ;
- erreur de domaine dédiée au conflit, traduite en 409 ; les autres violations de règles métier restent en 400 ;
- contrainte d'exclusion PostgreSQL créée par migration (`btree_gist`, même salle, `tstzrange(début, fin, '[)')` qui se chevauchent, réservations confirmées uniquement) ;
- violation de cette contrainte traduite en la même réponse 409, sans exposer l'erreur PostgreSQL ;
- tests unitaires du domaine et tests d'intégration au chemin de `ConfirmBooking` ;
- `scripts/test-template.sh` et fixture PB-008 mis à jour si nécessaire.

**Dépendances**

PB-009.

**Critères d'acceptation**

- Domaine : chevauchement partiel, inclusion, égalité et périodes adjacentes couverts ; une réservation annulée ne bloque pas ; une autre salle n'est pas concernée.
- Intégration : un chevauchement séquentiel renvoie 409 et n'écrit aucune ligne ; une période adjacente, une autre salle ou un créneau libéré par une annulation renvoient 201.
- Concurrence : des confirmations simultanées pour le même créneau produisent exactement une réservation et des 409 pour les autres, garanties par la base et non par un verrou applicatif.
- Le modèle EF Core ne contient aucune modification en attente.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- liste des salles libres sur une période, remboursement partiel, équipements et refactorisation de la règle : ce sont des tâches du benchmark v0.4 ;
- agrégat `Room`, modification ou déplacement d'une réservation ;
- verrouillage optimiste général.

### PB-011 — Lever `BookingConfirmed` à la confirmation

**Résultat visible**

Confirmer une réservation enregistre un événement `BookingConfirmed` sur l'agrégat. L'exemple Bookings montre ainsi le modèle d'événement de domaine que l'agent recopie.

**Livrables**

- `MyApp.Domain/Bookings/Events/BookingConfirmed.cs` : un `record` nommé au passé, porteur de l'identifiant, de la salle et de la période ;
- l'agrégat enregistre les événements qu'il lève et les expose en lecture seule, sans setter public ;
- configuration EF Core qui ignore ces événements : une réservation rechargée n'en contient aucun ;
- tests unitaires du domaine ;
- `scripts/test-template.sh` et fixture PB-008 mis à jour si nécessaire.

**Dépendances**

PB-009, pour la configuration EF Core de l'agrégat.

**Critères d'acceptation**

- Une confirmation enregistre exactement un `BookingConfirmed` dont les données correspondent à la réservation.
- Une annulation n'enregistre pas de `BookingConfirmed`.
- Le domaine reste limité à la bibliothèque de classes de base et le modèle EF Core ne contient aucune modification en attente.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- la publication des événements et leurs handlers ne font pas partie de l'acceptation v0.1 ;
- outbox, bus de messages, événement d'annulation ;
- analyseur HS009 (v0.2).

### PB-012 — Faire passer les commandes par un médiateur maison avec validation

**Résultat visible**

Les endpoints envoient leurs commandes par `IMediator`. Une commande invalide reçoit un 400 `ValidationProblem` qui nomme les champs fautifs avant d'atteindre son handler. Chaque commande traverse les comportements de validation, de transaction et de logs.

**Livrables**

- `Shared/Messaging/` : `IRequest`, `IRequestHandler`, `IPipelineBehavior`, `IMediator` et leur implémentation, de l'ordre de 100 lignes, sans MediatR ni dépendance runtime HardSlice ;
- distinction explicite entre commandes et requêtes, exploitable par les tests d'architecture ;
- `Shared/Behaviors/` : validation par FluentValidation, frontière transactionnelle unique par commande, logs du nom de la requête, de sa durée et de son issue, sans valeurs du contenu ;
- `ConfirmBooking` et `CancelBooking` migrés : commande publique, handler, validateur et endpoint `internal`, endpoint qui envoie par `IMediator` et renvoie des `TypedResults` ;
- validateurs `ConfirmBookingValidator` et `CancelBookingValidator` ;
- enregistrement des handlers et validateurs par le module, du médiateur et des comportements par l'hôte ;
- tests unitaires du médiateur, de l'ordre du pipeline et des validateurs ; tests d'intégration des réponses 400 ;
- `BookingsReferenceTests` compile la référence avec FluentValidation ;
- section « Adding a slice » d'`AGENTS.md` mise à jour : commande ou requête, validateur, handler, endpoint par le médiateur, tests ;
- `scripts/test-template.sh` et fixture PB-008 mis à jour.

**Dépendances**

PB-009, pour la frontière transactionnelle sur `AppDbContext`.

**Critères d'acceptation**

- Une commande invalide renvoie 400 avec les champs fautifs, n'atteint pas son handler et n'écrit rien en base.
- Une commande valide est validée, exécutée puis enregistrée dans une seule transaction ; une exception annule la transaction.
- Les comportements s'exécutent dans un ordre déterministe, couvert par un test.
- Les endpoints n'appellent plus aucun handler directement.
- Les comportements HTTP de PB-009, et de PB-010 s'il est intégré, restent inchangés.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- notifications ou publication d'événements par le médiateur ;
- MediatR ou toute autre bibliothèque de médiation ;
- test d'architecture HS013 (PB-015) et analyseurs HS003 à HS005 (v0.2) ;
- templates `hardslice-command` et `hardslice-query` (v0.3) ;
- requête `GetBooking` (PB-013).

### PB-013 — Lire une réservation par une projection

**Résultat visible**

`GET /bookings/{id}` renvoie 200 avec l'identifiant, la salle, la période et le statut de la réservation, ou 404. L'URL `Location` renvoyée par `POST /bookings` mène désormais à une ressource existante.

**Livrables**

- slice `Features/Bookings/GetBooking/GetBooking.cs` : requête publique, DTO, handler et endpoint `internal` ;
- handler qui lit par `AsNoTracking` et une projection, sans charger l'agrégat ni appeler `SaveChanges`, hors frontière transactionnelle ;
- endpoint mappé dans le groupe du module et renvoyant des `TypedResults` ;
- tests d'intégration au chemin de la slice ;
- carte des slices d'`AGENTS.md` mise à jour ;
- `scripts/test-template.sh` mis à jour.

**Dépendances**

PB-009 pour la persistance, PB-012 pour le médiateur.

**Critères d'acceptation**

- Une réservation confirmée est lue à l'URL `Location` renvoyée par sa création.
- Une réservation annulée est lue avec le statut `Cancelled`.
- Un identifiant inconnu renvoie 404.
- La requête ne suit aucune entité dans le contexte EF Core, ce qui est vérifié par un test.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- liste, recherche, pagination et salles libres ;
- cache ;
- analyseur HS006 (v0.2).

### PB-014 — Bloquer les violations de frontières par des tests d'architecture

**Résultat visible**

Dans une application générée, une violation de HS001, HS012, HS016 ou HS017 fait échouer `dotnet test` et les deux scripts de vérification. Le message nomme le symbole fautif, la règle, la correction attendue et la fiche. La solution générée contient un `hardslice.json` et un `.editorconfig` minimaux.

**Livrables**

- choix entre ArchUnitNET et NetArchTest, évalué sur la maintenance, la licence, l'expressivité des règles de ce ticket et la durée des tests sur la solution générée ; le choix et son évaluation sont consignés dans ce ticket à sa livraison ;
- `hardslice.json` minimal à la racine générée : style `slices` et dossiers autorisés de `Shared` (par défaut `Messaging`, `Persistence`, `Behaviors`) lus par HS017 ; un fichier absent ou invalide fait échouer les tests d'architecture avec un message explicite ;
- `.editorconfig` minimal déclarant la sévérité `error` de HS016, sans autre règle HS ni niveau d'analyse ;
- tests d'architecture dans `tests/MyApp.ArchitectureTests` :
  - HS001 : le Domain ne dépend d'aucun autre projet, ni d'EF Core, ni d'ASP.NET Core, ni de la DI ;
  - HS012 : un module n'utilise que le contrat public d'un autre module, tel que HS016 le définit ;
  - HS016 : aucun type d'une slice n'utilise un type d'une autre slice dans l'assembly compilé, et les handlers, validateurs et endpoints sont `internal` ;
  - HS017 : `Shared` ne dépend jamais de `Features` et ne contient que les dossiers autorisés par `hardslice.json` ;
- messages en anglais au format de HS016 : symbole fautif, règle en une phrase, correction attendue, `Doc: 'docs/rules/HSxxx.md'` ;
- fiches `docs/rules/HS001.md`, `HS012.md` et `HS017.md`, et `HS016.md` complétée par sa partie test d'architecture, avec les mêmes sections que la fiche HS016 ;
- `AGENTS.md` : édition de `hardslice.json` et de `.editorconfig` ajoutée aux contournements interdits ;
- `scripts/test-template.sh` : une violation semée par règle dans l'application générée, avec un échec attendu des deux scripts, le message de la règle et une fiche existante ; la sonde HS012 est une violation que l'analyseur HS016 ne couvre pas.

**Décision : bibliothèque des tests d'architecture (résolution de la question n° 1 de la spécification)**

ArchUnitNET (`TngTech.ArchUnitNET` 0.13.4) est retenu, référencé par `tests/MyApp.ArchitectureTests` seulement. Évaluation faite à la livraison, le 29 octobre 2026 :

- maintenance : ArchUnitNET publie régulièrement (0.13.3 en mars 2026, 0.13.4 en août, 0.13.5 en octobre), porté par TNG Technology Consulting. NetArchTest.Rules n'a plus de version depuis la 1.3.2 de mai 2021 ; son fork NetArchTest.eNhancedEdition (1.4.5, juin 2025) repose sur un seul mainteneur ;
- licence : ArchUnitNET est sous Apache-2.0, ses dépendances (Mono.Cecil, Newtonsoft.Json, CycleDetection, JetBrains.Annotations) sous MIT ; NetArchTest est sous MIT. Les deux conviennent : la dépendance reste dans un projet de tests et n'est jamais chargée au runtime de l'application ;
- expressivité : chaque message doit nommer le symbole fautif et le type qu'il utilise. ArchUnitNET expose, pour chaque type, ses dépendances (signatures, corps de méthodes, arguments génériques, attributs) avec leur cible, et rattache les types générés par le compilateur (lambdas, machines à états async) au type qui les déclare. HS001, HS012, HS016 et HS017 s'écrivent comme des requêtes sur ce graphe, au format de message de HS016. NetArchTest répond à « ce type dépend-il de ce namespace ? » sans dire par quelle dépendance : « aucune slice n'utilise une autre slice » demanderait un prédicat par paire de slices, et retrouver la cible des règles personnalisées sur Mono.Cecil ;
- durée, mesurée sur l'application du template : ArchUnitNET charge les deux assemblies en 450 ms environ à froid (50 ms à chaud), NetArchTest en 45 ms environ. Le projet de tests d'architecture initial (21 tests, avant la revue) s'exécutait en moins de 2 s, surtout le démarrage de l'hôte de tests : l'écart n'est pas déterminant.

La bibliothèque fournit le graphe des dépendances ; son API fluente et ses messages ne sont pas utilisés, pour garder le format HardSlice. La 0.13.4 est préférée à la 0.13.5, publiée trois jours avant la livraison.

La revue a mis en évidence deux omissions du graphe : les types nommés uniquement dans les clauses `catch` et les dépendances d'attributs d'assembly. Les premières sont ajoutées par réflexion sur les corps des méthodes et constructeurs, en rattachant les types générés (async, itérateurs, lambdas) à leur déclarant ; les secondes restent couvertes par le contrôle des références de l'assembly Domain. La visibilité des handlers, validateurs et endpoints est vérifiée sur toute la chaîne des types déclarants, y compris les types imbriqués `protected` et `protected internal`. HS017 inclut également les types directement dans le namespace racine `Features`. Des sondes de non-régression couvrent ces quatre cas dans les deux scripts de vérification.

**Dépendances**

Incrément 1 terminé. Le ticket peut avancer en parallèle de PB-009 à PB-013.

**Critères d'acceptation**

- L'application générée passe toutes les règles sans exception ni suppression.
- Chaque sonde fait échouer les deux scripts avec le diagnostic attendu et une fiche existante au chemin annoncé.
- La disposition de persistance de PB-009, dès qu'elle est intégrée, passe HS001 et HS017 sans dossier ni exception supplémentaires.
- Un `hardslice.json` absent ou invalide produit un échec explicite.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- HS002 et le mode couches (v0.5) ;
- nouveaux analyseurs, autres sévérités HS et niveaux d'analyse (v0.2) ;
- `dotnet hardslice check` et `explain` (v0.2) ;
- clés de `hardslice.json` pour les modules, les chemins ou les couches, et configurabilité générale (v0.5) ;
- HS000 (v0.3).

### PB-015 — Bloquer les slices sans validateur ni tests

**Résultat visible**

Dans une application générée, une commande paramétrée sans validateur, un handler de commande sans tests ou une slice sans dossier de tests au même chemin fait échouer `dotnet test` et les deux scripts de vérification, avec un message réparable et sa fiche.

**Livrables**

- tests d'architecture :
  - HS013 : toute commande qui a des paramètres a un validateur ;
  - HS014 : tout handler de commande a au moins un test au chemin de sa slice, dans `MyApp.Tests` ou `MyApp.IntegrationTests` ;
  - HS018 : les tests de `Features/<Module>/<Slice>/` sont au même chemin dans `tests/` ; une slice sans dossier de tests et un dossier de tests sans slice correspondante sont signalés ;
- messages au format de PB-014 ;
- fiches `docs/rules/HS013.md`, `HS014.md` et `HS018.md` ;
- `scripts/test-template.sh` : une violation semée par règle, avec un échec attendu des deux scripts.

**Livraison**

HS013 lit l'assembly compilé : une commande est un type concret qui implémente `ICommand<T>`, ses paramètres sont ses propriétés et champs publics d'instance, son validateur un type concret qui implémente `IValidator<T>`. Les arguments et contraintes génériques sont vérifiés : un validateur spécialisé ou plus contraint ne couvre pas toute une commande générique. L'enregistrement du validateur reste prouvé par le test d'intégration d'une requête invalide.

HS014 et HS018 lisent les sources des tests sur le disque plutôt que les assemblies de tests, que le projet de tests d'architecture ne construit ni ne référence : un assembly périmé masquerait un test supprimé. Un test est une méthode marquée `[Fact]`, `[Theory]` ou un attribut xUnit nommé comme eux, lue dans l'arbre syntaxique de `Microsoft.CodeAnalysis.CSharp` 5.0.0, la version du dépôt. Sa licence MIT a été vérifiée à son introduction ; la dépendance reste dans `tests/MyApp.ArchitectureTests`. Les slices de HS018 sont les dossiers `Features/<Module>/<Slice>/` du disque et les namespaces de l'assembly compilé.

HS014 analyse les branches conditionnelles avec les symboles de chaque projet de tests : framework, `DefineConstants` propres au projet et configuration Debug/Release. `Directory.Build.targets` les transmet aux métadonnées de l'assembly d'architecture au build.

**Dépendances**

PB-012 pour l'identification des commandes, handlers et validateurs ; PB-014 pour la bibliothèque, le format des messages et des fiches.

**Critères d'acceptation**

- L'exemple Bookings passe les trois règles ; HS014 et HS018 acceptent des tests situés dans `MyApp.Tests` comme dans `MyApp.IntegrationTests`.
- Chaque sonde fait échouer les deux scripts avec le diagnostic attendu et une fiche existante.
- `GetBooking`, qu'il soit intégré avant ou après ce ticket, est soumis à HS018. En tant que requête, il reste explicitement hors du périmètre de HS013 et HS014.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- mesure de couverture ou de qualité des tests ;
- analyseurs HS004 et HS015 (v0.2) ;
- exécution des seuls tests de la slice touchée par les hooks (v0.3).

### PB-016 — Générer une application vide avec `--no-sample`

**Résultat visible**

`dotnet new hardslice-api --no-sample` génère une solution sans module Bookings, qui compile et dont les tests unitaires, d'architecture et d'intégration ainsi que les deux scripts de vérification sont verts.

**Livrables**

- option `--no-sample` du template, désactivée par défaut ;
- exclusion du domaine Bookings, du module `Features/Bookings`, de leurs tests et des migrations de l'exemple ;
- conservation de `Shared/Messaging`, `Shared/Persistence` sans configuration d'agrégat, `Shared/Behaviors`, `hardslice.json`, `.editorconfig`, des tests d'architecture et des scripts ;
- aucun projet de tests vide : chaque projet conservé exécute au moins un test pertinent, par exemple le démarrage de l'application sur PostgreSQL ;
- `AGENTS.md` généré sans module dans sa carte et avec la marche à suivre pour un premier module ;
- `scripts/test-template.sh` génère et vérifie les deux variantes.

**Dépendances**

PB-010, PB-011 et PB-013 pour l'exemple complet à exclure ; PB-015, et par lui PB-014, pour l'ensemble des règles v0.1 à vérifier sur une application vide.

**Critères d'acceptation**

- Les deux variantes passent les deux scripts de vérification depuis le paquet local.
- La variante vide ne contient aucune référence à Bookings dans son code, ses tests, ses migrations ou sa carte.
- Une violation semée dans la variante vide fait toujours échouer au moins une règle d'architecture et l'analyseur HS016 : les règles ne passent pas faute de cible.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- template `hardslice-module` et génération d'un premier module (v0.3) ;
- options `--aspire` et SQL Server (incrément 8).

### PB-017 — Documenter le template v0.1 et le valider depuis un checkout propre

**Résultat visible**

Un nouvel utilisateur suit le `README.md` du dépôt pour construire le paquet local, générer une application avec ou sans exemple, appliquer les migrations sur PostgreSQL, lancer l'API et exécuter la vérification. La CI prouve tout le contrat v0.1 depuis des runners neufs.

**Livrables**

- `README.md` à la racine du dépôt, en anglais : rôle de HardSlice, statut v0.1 en préversion locale, prérequis (.NET SDK épinglé, runtime de conteneurs compatible Docker, PowerShell pour `check.ps1`), installation du paquet local, `dotnet new hardslice-api` et `--no-sample`, exécution de l'application, vérification, règles livrées et lien vers leurs fiches, limites connues ;
- `LICENSE` MIT ;
- `AGENTS.md` du template final et cohérent, de moins de 150 lignes : carte `ConfirmBooking`, `CancelBooking`, `GetBooking` et contrat public du module, ajout d'une slice, vérification avec Docker, règles HS001, HS012 à HS014 et HS016 à HS018 et leurs fiches ;
- `scripts/test-template.sh` exécute le parcours documenté sur la solution générée : restauration des outils, application des migrations sur un conteneur PostgreSQL, démarrage de l'API, création puis lecture d'une réservation ;
- README de la démonstration PB-008 exact pour le template v0.1 ;
- CI : jobs build, template et démonstration, chacun depuis un checkout neuf, sans cache ni état hérité d'un autre job.

**Dépendances**

PB-016, et par lui PB-009 à PB-015.

**Critères d'acceptation**

- Chaque commande du parcours de démarrage du README est exécutée par `scripts/test-template.sh` ou par la CI.
- Les trois jobs CI sont verts sur `main`.
- Tous les livrables v0.1 de la spécification sont présents dans la solution générée par défaut.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- publication NuGet en préversion (v0.2) ou stable (v1.0), site de documentation (v1.0) ;
- `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, skills et hooks (v0.3) ;
- Azure Pipelines (v1.0) ;
- rendre le dépôt public, qui reste une action du mainteneur ;
- nouveau run d'agent enregistré pour la démonstration PB-008.

### Définition de fini

L'incrément est terminé lorsque PB-009 à PB-017 sont satisfaits depuis un checkout propre et que les jobs CI build, template et démonstration sont verts sur `main`. Concrètement :

- la solution du dépôt passe `dotnet restore`, `dotnet build --no-restore` et `dotnet test --no-build`, tests d'intégration compris ;
- `scripts/test-template.sh` génère depuis le paquet local la variante par défaut et la variante `--no-sample`, les vérifie avec `scripts/check.sh` et `scripts/check.ps1`, exécute le parcours documenté, détecte l'absence de Docker, et prouve que HS001, HS012, HS013, HS014, HS016, HS017 et HS018 échouent sur leur sonde avec une fiche existante ;
- `scripts/demo-hs016-agent-fix.sh prepare` reproduit le diagnostic HS016 attendu sur le template v0.1 ;
- la solution générée par défaut contient les livrables v0.1 de la spécification : module Bookings avec agrégat `Booking`, objet valeur `BookingPeriod`, événement `BookingConfirmed`, invariant de chevauchement garanti par PostgreSQL, slices `ConfirmBooking`, `CancelBooking` et `GetBooking`, tests unitaires, d'intégration et d'architecture, `hardslice.json`, `.editorconfig`, `AGENTS.md`, `scripts/check.sh` et `scripts/check.ps1`.

Aucune porte documentaire supplémentaire ne s'applique.

## 4. Incréments suivants

| Incrément | Résultat produit | Contenu principal | Acceptation globale |
|---|---|---|---|
| **3 — Analyseurs v0.2** | Les dérives principales sont détectées par des diagnostics réparables | HS003 à HS011 et HS015, détection inter-assemblies, diagnostic de configuration pour les types applicatifs non classifiables, API bannies, fiches, `check`, `explain`, niveaux d'analyse, faux positifs et performance | Chaque règle possède fixtures conforme/fautive, tests et fiche ; aucun faux positif connu sur la référence |
| **4 — Boucle agent v0.3** | Un agent génère puis corrige par les chemins officiels | Templates d'éléments, six skills, fichiers de guidage des quatre agents, hooks après édition et de fin de tâche, scripts uniques pour hooks/pre-commit/CI, scénarios E2E | Une slice est générée, complétée et vérifiée sans recréer manuellement sa structure ; les quatre agents ciblés disposent d'un adaptateur natif ou du fallback documenté |
| **5 — Benchmark v0.4** | L'effet des garde-fous est mesuré et publié | Configurations A/B/C, tâches Bookings, runner, répétitions, coûts et qualité | Protocole, prompts, versions, résultats bruts et analyse sont publiables et reproductibles |
| **6 — Adoption v0.5** | Une équipe ajoute HardSlice sans corriger toute sa dette | `init`, `map`, conventions, baseline, cliquet, `report`, `verify`, mode couches applicable | Les violations existantes sont baselinées, une nouvelle violation est bloquée et une correction réduit la baseline |
| **7 — Comparaison v0.6** | Les architectures couches et slices sont comparées | Applications équivalentes, mêmes tâches, agents, modèles et budgets | Les conclusions distinguent l'effet de l'architecture de celui des garde-fous |
| **8 — Distribution v1.0** | HardSlice devient un produit open source installable et maintenable | Paquets stables, documentation, site, CI de publication, compatibilité, sécurité et contribution, options de template `--aspire` et SQL Server | Un utilisateur externe réussit les parcours greenfield et adoption depuis la documentation publique |

## 5. Différé, mais non abandonné

| Élément | Incrément prévu |
|---|---:|
| Catalogue initial complet | 3 |
| Templates d'éléments, six skills et hooks | 4 |
| Claude Code, Codex, GitHub Copilot et Gemini CLI | 4, avec adaptateur natif si disponible et fallback `AGENTS.md` + vérification sinon |
| Benchmark statistique A/B/C | 5 |
| `init`, `map`, baseline, cliquet, `report` et `verify` | 6 |
| Mode couches complet | 6–7 |
| Benchmark couches contre slices | 7 |
| Paquets stables et site public | 8 |
| SQL Server en option et orchestration Aspire (`--aspire`) | 8 |

La recherche utilisateur et la mécanique du benchmark ne sont pas des prérequis aux incréments 1 et 2.

# HardSlice — backlog produit

Statut : backlog actif, 30/10/2026.

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

HS014 analyse les branches conditionnelles avec les symboles de chaque projet de tests : framework, `DefineConstants` propres au projet et configuration Debug/Release. `Directory.Build.targets` les transmet aux métadonnées de l'assembly d'architecture au build, avec les fichiers `Compile` évalués par MSBuild : les sources exclues ne comptent pas. Les alias d'attributs sont résolus selon leur portée (namespace, fichier ou projet), y compris les alias globaux générés par le SDK. Les fichiers restent lus sur le disque ; un fichier supprimé ne compte pas via un ancien assembly. L'inventaire doit être reconstruit après modification des entrées du projet.

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

**Livraison**

`--no-sample` est le paramètre booléen `NoSample` de `template.json`, nommé pour la ligne de commande par `dotnetcli.host.json`. Il exclut le domaine `Bookings`, le module `Features/Bookings`, les migrations, les tests unitaires et d'intégration de l'exemple, et `TransactionBehaviorTests`, qui a besoin de l'agrégat `Booking`. `Program.cs` et `AGENTS.md` portent les seules conditions du moteur de templates. Écrites `#if (!NoSample)`, celles de `Program.cs` restent vraies pour le compilateur : la solution du dépôt compile la variante par défaut, la variante vide n'est compilée que par `scripts/test-template.sh`. `TemplatePackageTests` vérifie `AGENTS.md` dans chacune des deux variantes.

Dans la variante vide, `MyApp.Tests` garde les tests de `Shared/Messaging` et `Shared/Behaviors`. `MyApp.IntegrationTests` garde `MigrationsTests`, qui échoue dès qu'un premier agrégat est mappé sans sa migration, et reçoit `ProgramTests`, présent dans les deux variantes : l'application démarre en Development, ce qui valide ses enregistrements, répond en HTTP et trouve sa base PostgreSQL migrée. Les tests d'architecture et leurs propres tests prennent leurs exemples dans un module fictif `Orders`. Les fiches `docs/rules/` gardent leurs exemples Bookings : elles documentent les règles, pas l'application générée.

`scripts/test-template.sh` génère `Blank` avec `--no-sample` depuis le même paquet. Il vérifie qu'aucun fichier hors `docs/rules/` ne mentionne Bookings, que les chemins cités par `AGENTS.md` existent, que les deux scripts et `dotnet ef migrations has-pending-model-changes` passent et que chaque projet de tests exécute les tests de la fondation. Il sème ensuite un premier module fautif : HS016 (analyseur et test d'architecture), HS018, HS013, HS014, HS017 et HS001 font échouer les deux scripts.

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

**Livraison**

Le `README.md` décrit le parcours d'un nouvel utilisateur en cinq étapes : construction des deux paquets dans `artifacts/packages` et installation du template ; `nuget.config` dont la correspondance de sources réserve les paquets `HardSlice.*` au dossier local ; génération avec ou sans exemple ; vérification par les deux scripts ; démarrage de l'API (PostgreSQL dans un conteneur publié sur 127.0.0.1 seulement, attente bornée à 60 secondes de `pg_isready` en TCP dans le conteneur, `dotnet tool restore`, `dotnet ef database update`, `dotnet run` en Development, réservation créée puis relue avec `curl`). Le paquet garde la version fixe 0.1.0 : le README indique comment réinstaller le template et vider le dossier global des paquets NuGet après une reconstruction.

`scripts/generate-app.sh` exécute les étapes 1 et 2 avec les mêmes commandes et le même `nuget.config`, isolés dans le dossier de travail (flux, ruche de templates, dossier des paquets). `scripts/test-template.sh` exécute les étapes 3 et 4 sur les deux variantes : un seul conteneur `postgres:18-alpine` sur un port libre de 127.0.0.1, attendu par la même commande que le README, la chaîne de connexion générée avec ce port, l'API sur un port libre. Avec l'exemple, la réservation du README est créée (201 et `Location`), relue (200) puis refusée en double (409) ; sans exemple, l'API démarre sur sa base créée par `dotnet ef database update` et répond 404. Le script vérifie aussi que la solution générée par défaut contient les livrables v0.1 de la spécification, contrainte d'exclusion PostgreSQL comprise.

`ReadmeTests` vérifie que les liens relatifs du README existent et que sa table des règles cite exactement les fiches livrées par le template. `AGENTS.md` nomme `BookingsModule` comme contrat public du module, conformément à HS012 et HS016, et reçoit une section « Running the API » ; il compte 118 lignes générées avec l'exemple et 125 sans. Le README de la démonstration PB-008 précise ce qui distingue la trace enregistrée du template v0.1. La CI déclare l'absence de cache (`cache: false`) et ne conserve pas les identifiants du checkout.

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

## 4. Incrément préparé : Analyseurs v0.2

Statut : préparé le 30/10/2026. Il démarre lorsque la définition de fini de l'incrément 2 est atteinte. Aucun ticket n'est commencé.

### Résultat attendu

> Dans une solution générée par `dotnet new hardslice-api`, avec ou sans Bookings, `dotnet build` arrête chaque dérive principale par un diagnostic HS qui porte son propre identifiant : accès direct à la base depuis un endpoint, slice mal formée, handler de commande qui renvoie une entité, handler de requête qui suit ou écrit, état d'entité modifiable de l'extérieur, référence directe entre agrégats, événement qui n'est pas un `record`, lecture de l'horloge système, appel bloquant ou `async void`, endpoint hors de sa slice. Chaque règle a sa fiche, ses tests et sa place dans un niveau d'analyse. `dotnet hardslice check` et `dotnet hardslice explain` sont livrés par un outil local épinglé à la version des analyseurs. Les paquets `0.2.0-preview.1` sont publiés sur NuGet.org.

### Arbitrages de l'incrément

Décisions du mainteneur du 30/10/2026, reportées dans la [spécification](specifications.md) et le [plan](plan.md) :

- **Classification par contrats applicatifs.** Le Domain généré déclare `IEntity` et `IAggregateRoot : IEntity`, à côté d'`IDomainEvent`. Ces interfaces appartiennent à l'application : aucun paquet HardSlice n'est requis au runtime. Les analyseurs reconnaissent les contrats par leur nom de métadonnée et leur assembly déclarant, jamais par leur nom simple. Une propriété `Id` ne classe jamais un type.
- **HS010 et HS011 sont des analyseurs HardSlice ciblés.** `BannedApiAnalyzers` n'est pas utilisé : il produirait `RS0030` pour les deux règles, qu'on ne pourrait alors ni configurer, ni supprimer, ni compter séparément. HardSlice n'ajoute aucun autre analyseur qui signalerait les mêmes appels.
- **HS009 vérifie la structure, HS019 le nommage.** `record` et `IDomainEvent` sont vérifiés de façon fiable par HS009. Le temps passé du nom est une convention : HS019 la signale comme suggestion, jamais comme erreur.
- **Diagnostics de configuration HS900 à HS902.** Ils signalent une classification impossible, au lieu de laisser une règle se taire.
- **Niveaux d'analyse.** `Minimum`, `Recommended` et `All` activent ou désactivent des règles ; ils ne changent jamais la sévérité d'une règle active.
- **Messages.** Pas de `CodeFixProvider` en v0.2. Pas de référence `Skill:` avant la livraison de la skill en v0.3.
- **Outil.** `dotnet hardslice check` porte la vérification ; `scripts/check.sh` et `scripts/check.ps1` deviennent des lanceurs minces. Les diagnostics restent produits par `dotnet build` seul.

### Contrats communs aux analyseurs

Ces contrats s'appliquent à tous les tickets de règle. Le premier ticket intégré qui en a besoin les met en place ; les suivants les réutilisent.

**Cartographie des slices et classification des types sont séparées.** `SliceMap` reste fondé sur les espaces de noms `<Assembly>.Features.<Module>.<Slice>` de la compilation analysée. La classification (`DomainModel`, PB-018) travaille sur les symboles, y compris ceux des assemblies référencés : un handler de `MyApp` reconnaît `Booking` de `MyApp.Domain` compilé à part. Aucun analyseur ne charge la solution entière.

**Où chaque règle s'exécute.** Une règle sur la déclaration d'un type (HS007, HS008, HS009, HS019) s'exécute dans la compilation qui déclare ce type, donc HS007 dans celle du Domain. Les autres règles (HS003 à HS006, HS010, HS011, HS015, HS902) s'exécutent dans la compilation du code examiné et lisent les symboles référencés.

**Projets de tests.** Les règles HS003 à HS011, HS015, HS019 et HS900 à HS902 ne s'appliquent pas aux projets dont `IsTestProject` vaut `true`. HS016 garde son comportement actuel.

**Lecture de la configuration.** Le paquet `HardSlice.Analyzers` livre `build/HardSlice.Analyzers.props` et `build/HardSlice.Analyzers.targets`, importés par la `GlobalPackageReference` du template (actifs `Build` inclus). Ils ajoutent le `hardslice.json` le plus proche au-dessus du projet comme `AdditionalFiles` et exposent `IsTestProject`, `MSBuildProjectDirectory` et `HardSliceAnalysisMode` par `CompilerVisibleProperty`. La lecture de `hardslice.json` doit fonctionner dans tous les hôtes du compilateur : un analyseur `netstandard2.0` ne peut pas compter sur le chargement de `System.Text.Json` dans chacun d'eux. Le moyen de lecture est choisi en proportion du format attendu, petit et fermé, sans imposer un parseur JSON généraliste. Une entrée hors de ce format est rejetée par HS900, qui nomme la clé en cause ; les tests couvrent le fichier absent, le JSON invalide, la clé inconnue et la valeur mal formée.

**Format des messages.** Une ligne en anglais : `HSxxx: <symbole fautif et sa position architecturale>. <règle en une phrase>. Fix: <correction>. Doc: docs/rules/HSxxx.md`. La fiche contient : pourquoi, mauvais exemple, bon exemple, contournements interdits et ce que la règle ne détecte pas.

**Livrables de chaque ticket de règle**, dans le même changement :

- descripteur dans `AnalyzerReleases.Unshipped.md` ;
- fiche `docs/rules/HSxxx.md` dans le template, ligne de la table des règles du `README.md` (vérifiée par `ReadmeTests`) et de `AGENTS.md`, qui reste sous 150 lignes ;
- tests dans `HardSlice.Analyzers.Tests` : cas conformes, cas fautifs, cas génériques et, lorsque la règle lit un symbole d'un autre projet, montage application + Domain réellement séparé (Domain compilé à part et référencé en métadonnées) ;
- `BookingsReferenceTests` vert : la référence ne produit aucun diagnostic HS ;
- `scripts/test-template.sh` : une sonde par règle, dans la variante Bookings et dans la variante `--no-sample` dès que la règle y a une cible ; les sondes existantes restent. La sonde d'une règle bloquante fait échouer `check.sh` et `check.ps1` avec le message attendu et une fiche existante. La sonde de HS019, non bloquante, vérifie qu'avec la configuration livrée le build et les deux scripts réussissent et que la suggestion HS019 figure dans le journal SARIF du compilateur (`ErrorLog`) ;
- ligne de la règle dans la matrice des niveaux d'analyse si PB-020 est intégré, sinon PB-020 l'ajoute ;
- mesure de performance enregistrée dès que PB-019 est intégré, seuils respectés.

### Contrats déclarés dans `hardslice.json`

PB-018 ajoute une section `contracts` au fichier généré. Chaque valeur est un nom de métadonnée suivi de l'assembly qui doit le déclarer :

```json
{
  "style": "slices",
  "contracts": {
    "entity": "MyApp.Domain.IEntity, MyApp.Domain",
    "aggregateRoot": "MyApp.Domain.IAggregateRoot, MyApp.Domain",
    "domainEvent": "MyApp.Domain.IDomainEvent, MyApp.Domain",
    "command": "MyApp.Shared.Messaging.ICommand`1, MyApp",
    "query": "MyApp.Shared.Messaging.IQuery`1, MyApp",
    "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp"
  },
  "shared": {
    "allowedFolders": ["Messaging", "Persistence", "Behaviors"]
  }
}
```

L'assembly déclarant est nécessaire : il distingue un contrat absent d'un contrat simplement invisible. La compilation de `MyApp.Domain` ne référence pas `MyApp` et ne voit donc pas `ICommand<T>` ; ce n'est pas une erreur. Si la compilation de `MyApp` ne le trouve plus, c'en est une. Un contrat est **requis** dans toute compilation qui est son assembly déclarant ou le référence. Les types de bibliothèque utilisés par les règles (`IValidator<T>` de FluentValidation, `DbContext` et `DbSet<T>` d'EF Core, `IResult` et `HttpResults` d'ASP.NET Core) sont reconnus par leur symbole et ne sont pas configurables.

La configurabilité générale des conventions reste en v0.5 : ce fichier déclare des contrats, il ne permet pas encore de renommer `Features` ni de décrire une architecture en couches.

### Prérequis de la préversion NuGet

Identifiés dès le démarrage de l'incrément. Le développement et la validation locale des paquets n'en dépendent pas : seul PB-033 les exige.

- compte NuGet.org du mainteneur, disponibilité vérifiée des identifiants `HardSlice.Analyzers`, `HardSlice.Templates` et `HardSlice.Tool`, réservation du préfixe `HardSlice.` demandée ;
- publication de confiance NuGet.org (OIDC GitHub) configurée pour le dépôt, ou à défaut clé d'API limitée au push de `HardSlice.*`, stockée dans un environnement GitHub protégé `nuget` avec approbation du mainteneur ;
- dépôt GitHub public avant toute publication sur NuGet.org : les liens des paquets vers le code, les fiches et la documentation doivent être utilisables. Ce prérequis ne bloque ni la construction ni la validation des paquets sur la source locale (PB-032) ;
- version `0.2.0-preview.1`.

### Graphe des dépendances

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-018 | incrément 2 | PB-019 à PB-024, PB-032 |
| PB-019 | incrément 2 | tous ; mesure ensuite chaque ticket de règle |
| PB-020 | incrément 2 | tous ; point de fusion avec chaque ticket de règle |
| PB-021 | incrément 2 | tous les tickets de règle |
| PB-022 (HS003) | incrément 2 | tous |
| PB-023 (HS015) | incrément 2 | tous |
| PB-024 (HS011) | incrément 2 | tous |
| PB-025 (HS010) | PB-018 | PB-026 à PB-031 |
| PB-026 (HS004) | PB-018 | PB-025, PB-027 à PB-031 |
| PB-027 (HS005) | PB-018 | PB-025, PB-026, PB-028 à PB-031 |
| PB-028 (HS006) | PB-018 | PB-025 à PB-027, PB-029 à PB-031 |
| PB-029 (HS007) | PB-018 | PB-025 à PB-028, PB-030, PB-031 |
| PB-030 (HS008) | PB-018 | PB-025 à PB-029, PB-031 |
| PB-031 (HS009, HS019) | PB-018 | PB-025 à PB-030 |
| PB-032 | PB-021 | tous les tickets de règle |
| PB-033 | PB-018 à PB-032, prérequis NuGet | aucun |

```text
incrément 2 ─┬─ PB-018 classification ──┬─ PB-025 HS010
             │                          ├─ PB-026 HS004
             │                          ├─ PB-027 HS005
             │                          ├─ PB-028 HS006
             │                          ├─ PB-029 HS007
             │                          ├─ PB-030 HS008
             │                          └─ PB-031 HS009, HS019
             ├─ PB-022 HS003
             ├─ PB-023 HS015
             ├─ PB-024 HS011
             ├─ PB-019 performance (mesure chaque règle)
             ├─ PB-020 niveaux d'analyse (point de fusion des règles)
             └─ PB-021 outil ── PB-032 paquets locaux
                                          └─ (tout) ── PB-033 publication
```

L'outil ne bloque aucune règle : `explain` embarque toutes les fiches présentes au moment du pack. PB-027 et PB-028 partagent l'analyse « ce type contient-il une entité » : le premier intégré la livre, le second la réutilise.

### PB-018 : Classer les types du domaine et de la messagerie par leurs contrats

**Résultat observable**

Le Domain généré déclare `IEntity` et `IAggregateRoot`, `Booking` implémente `IAggregateRoot`, et `hardslice.json` déclare les contrats. Retirer un contrat requis, rendre un type ambigu ou mapper un type du Domain sans contrat fait échouer `dotnet build` avec HS900, HS901 ou HS902.

**Périmètre**

- `src/MyApp.Domain/IEntity.cs` et `IAggregateRoot.cs` dans les deux variantes du template, documentés dans `AGENTS.md` ;
- `Booking : IAggregateRoot` ;
- section `contracts` de `hardslice.json` ; `HardSliceConfiguration` des tests d'architecture l'accepte et valide sa forme ;
- props et targets du paquet décrits dans les contrats communs ;
- `DomainModel` : résolution des contrats dans la compilation et ses références, implémentation directe ou héritée (classe de base, interface dérivée), types génériques construits ramenés à leur définition ;
- trois diagnostics de configuration et leurs fiches.

Un type est une **entité** s'il implémente le contrat `entity`, une **racine d'agrégat** s'il implémente `aggregateRoot`, un **événement de domaine** s'il implémente `domainEvent`, une **commande** ou une **requête** s'il implémente `command` ou `query`, un **handler** s'il implémente `requestHandler`. Ces catégories ne reposent sur aucune autre heuristique.

**Diagnostics de configuration**

HS900, configuration inutilisable. Signalé une fois par compilation, uniquement si une règle active a besoin de l'élément en cause :

- `hardslice.json` introuvable ou invalide ;
- clé de `contracts` absente ou mal formée ;
- contrat requis (au sens ci-dessus) introuvable dans son assembly déclarant ;
- incohérence entre contrats : `aggregateRoot` ne dérive pas de `entity`, deux clés désignent le même type.

HS901, classification ambiguë, signalé sur la déclaration du type :

- type à la fois entité et événement de domaine ;
- type à la fois commande et requête ;
- type à la fois entité et requête ou commande.

HS902, type soumis à la classification mais non classifiable. Un type est soumis à la classification lorsqu'il est déclaré dans l'assembly du contrat `entity` et qu'il est mappé comme entité EF Core par l'une de ces formes :

- argument de type d'une implémentation d'`IEntityTypeConfiguration<T>` ;
- `T` d'une propriété `DbSet<T>` d'un type dérivé de `DbContext` ;
- appel générique `ModelBuilder.Entity<T>()`.

S'il n'implémente pas `entity`, HS902 est signalé sur la forme de mapping, dans la compilation qui la contient.

**Ce qui reste indétectable**, documenté dans les fiches HS902 et HS007 :

- entité mappée par convention (découverte par navigation), par `Entity(typeof(T))` ou par nom ;
- type possédé (`OwnsOne`, `OwnsMany`) et type complexe (`ComplexProperty`) : HS902 ne les soumet pas à la classification. Un type possédé peut être un objet valeur ou une entité enfant ; HardSlice ne le déduit pas du mapping, et seuls `IEntity` et `IAggregateRoot` font autorité. Une entité enfant possédée qui n'implémente pas `IEntity` échappe donc à HS005, HS007 et HS008 sans diagnostic ;
- entité non persistée qui n'implémente aucun contrat ;
- mapping dans un projet qui n'utilise pas les analyseurs HardSlice.

**Dépendances**

Incrément 2.

**Cas conformes**

- Bookings généré : `Booking : IAggregateRoot`, `BookingPeriod` en type complexe, aucun diagnostic ;
- `BookingLine : IEntity` mappé par `OwnsMany(booking => booking.Lines)` : aucun HS902, et l'entité reste couverte par HS005, HS007 et HS008 grâce à son contrat ;
- variante `--no-sample` : contrats présents, aucune entité, aucun diagnostic ;
- `MyApp.Domain` compilé seul : les contrats de messagerie, invisibles, ne sont pas requis.

**Cas fautifs**

- `IEntity` supprimé : HS900 dans `MyApp.Domain` et dans `MyApp`, puisque HS901 et HS902 en dépendent ;
- `contracts.command` renommé : HS900 dans `MyApp` seulement ;
- `Room` configuré par `RoomConfiguration : IEntityTypeConfiguration<Room>` sans `IEntity` : HS902 ;
- type qui implémente `IAggregateRoot` et `IDomainEvent` : HS901.

**Génériques et inter-assemblies**

- contrat implémenté par une classe de base générique `Entity<TId> : IEntity` ;
- interface applicative dérivée `IAuditedEntity : IEntity` ;
- Domain référencé en métadonnées et en référence de projet ;
- type homonyme `IEntity` d'un autre espace de noms ou d'un autre assembly : ignoré.

**Sévérité et diagnostic**

HS900, HS901 et HS902 en `error`, dans tous les niveaux d'analyse, lorsqu'une règle active en dépend.

```text
HS902: 'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'. HardSlice classifies domain types only through these contracts. Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: docs/rules/HS902.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-019 : Mesurer le coût des analyseurs sur un build incrémental réel

**Résultat observable**

`scripts/measure-analyzers.sh` produit un rapport de durées avec et sans analyseurs HardSlice sur une fixture documentée ; le job CI `performance` échoue si un seuil est dépassé.

**Périmètre**

- fixture générée depuis le paquet local : application `PerfApp` avec Bookings et 20 modules synthétiques de 10 slices chacun, agrégats compris, produits par un script déterministe ;
- variante sans analyseurs : copie de la fixture dont un `Directory.Build.targets` retire les éléments `Analyzer` du paquet `HardSlice.Analyzers`. Aucune propriété de désactivation n'est ajoutée au paquet ;
- scénarios, chacun avec et sans analyseurs :
  - A : build incrémental après modification du corps d'une méthode d'une slice ;
  - B : build incrémental après modification de l'API publique d'un agrégat du Domain, qui recompile les projets dépendants ;
  - C : build complet `--no-incremental`, comme référence ;
- un build sans changement n'est pas mesuré : il peut ne rien analyser ;
- une exécution de chauffe, puis cinq exécutions mesurées, médiane retenue, serveur de compilation actif ;
- `-p:ReportAnalyzer=true` en complément, pour expliquer le coût de chaque analyseur ; seuls les seuils sur la durée totale font foi ;
- rapport `docs/performance/analyzers.md` : SDK effectif (`dotnet --version`), runner, nombre de cœurs, mémoire, fixture, médianes ;
- job CI `performance` sur `ubuntu-latest`, sur `main` et sur les pull requests qui touchent les analyseurs ou le template.

**Seuils initiaux**, sur la médiane, pour la fixture, le SDK épinglé par `global.json` et le runner documentés :

| Scénario | Durée totale avec analyseurs | Surcoût des analyseurs HardSlice |
|---|---:|---:|
| A | ≤ 10 s, budget du build après édition de la spécification | ≤ 1 s |
| B | ≤ 20 s | ≤ 2 s |
| C | mesuré, sans seuil | ≤ 15 % |

Un seuil ne change que par une décision du mainteneur consignée dans le rapport, jamais automatiquement.

**Dépassement d'un seuil**

Le rapport donne, pour chaque scénario et chaque variante, le minimum, la médiane et le maximum des cinq mesures. Un dépassement est d'abord qualifié :

- **variabilité de l'environnement** : la dispersion d'une série (maximum moins minimum, rapporté à la médiane) dépasse 20 %. La série est instable : le job est relancé sur un nouveau runner, sans conclusion ;
- **coût du build sans HardSlice** : la variante sans analyseurs dépasse déjà le budget de durée totale. Le dépassement ne vient pas des analyseurs : il est signalé dans le rapport et l'arbitrage porte sur la fixture, le runner ou le budget ;
- **surcoût des analyseurs** : la variante sans analyseurs respecte le budget, mais le surcoût dépasse son seuil. `ReportAnalyzer` désigne l'analyseur en cause.

Un surcoût n'est une régression confirmée qu'après deux exécutions stables consécutives du job au-dessus du seuil. Il bloque alors l'intégration du ticket qui l'introduit, et seulement celui-là. Les autres catégories ne font pas échouer les pull requests : elles ouvrent un arbitrage.

Si la mesure initiale, avec HS016 seul, dépasse déjà un seuil après qualification, PB-019 attend la décision du mainteneur. Les tickets de règle continuent pendant ce temps : ils enregistrent leurs mesures dans le rapport, et le job `performance` reste informatif jusqu'à la décision.

**Dépendances**

Incrément 2.

**Acceptation**

```text
scripts/measure-analyzers.sh
```

Le rapport est produit, les seuils sont respectés avec HS016, et le job CI `performance` est vert.

### PB-020 : Choisir les règles actives par un niveau d'analyse

**Résultat observable**

`<HardSliceAnalysisMode>` dans `Directory.Build.props` sélectionne `Minimum`, `Recommended` ou `All`. Le template utilise `All`. Un réglage individuel de `.editorconfig` l'emporte sur le niveau.

**Périmètre**

- un fichier `.globalconfig` par niveau, livré dans le paquet et ajouté par `build/HardSlice.Analyzers.targets` aux `GlobalAnalyzerConfigFiles` ;
- valeur absente : `Recommended`. Valeur inconnue : erreur MSBuild qui liste les valeurs admises, jamais un repli silencieux ;
- les sévérités par défaut des descripteurs correspondent à `Recommended` ;
- un niveau active ou désactive une règle ; il ne change jamais sa sévérité. Aucun niveau ne désactive tout ;
- `.editorconfig` du template : il garde la ligne HS016 et le rappel qu'abaisser une sévérité est un contournement.

**Matrice**

| Règle | Minimum | Recommended | All |
|---|---|---|---|
| HS003 | error | error | error |
| HS004 | – | error | error |
| HS005 | – | error | error |
| HS006 | – | error | error |
| HS007 | error | error | error |
| HS008 | – | error | error |
| HS009 | – | error | error |
| HS010 | error | error | error |
| HS011 | error | error | error |
| HS015 | – | error | error |
| HS016 | error | error | error |
| HS019 | – | – | suggestion |
| HS900 à HS902 | error si une règle active en dépend | idem | idem |

`Minimum` réunit les règles qui ne supposent ni la forme d'une slice ni la messagerie du template, plus HS016. Le template utilise `TreatWarningsAsErrors=true` : un `warning` y bloque. HS019, heuristique, est donc en `suggestion`, visible dans l'IDE et sans effet sur le build ; aucun niveau ne la rend bloquante.

**Dépendances**

Incrément 2. Point de fusion avec chaque ticket de règle : le ticket intégré en second ajoute la ligne manquante.

**Cas vérifiés**

- chaque niveau active exactement les règles de sa ligne, vérifié par un test qui compile une sonde par règle ;
- `dotnet_diagnostic.HS004.severity = none` dans `.editorconfig` désactive HS004 sous `All` ;
- `dotnet_diagnostic.HS004.severity = error` dans `.editorconfig` active HS004 sous `Minimum` ;
- `#pragma warning disable` et `[SuppressMessage]` agissent sur une seule règle HS ;
- valeur `Strict` : le build échoue avec la liste des niveaux.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-021 : Livrer `dotnet hardslice check` et `explain` dans un outil local épinglé

**Résultat observable**

Dans une solution générée, `dotnet tool restore` installe `HardSlice.Tool` à la même version que `HardSlice.Analyzers`. `dotnet hardslice check` exécute la vérification complète ; `dotnet hardslice explain HS016` affiche la fiche.

**Périmètre**

- projet `src/HardSlice.Tool`, outil .NET `net10.0`, commande `dotnet-hardslice`, appelée `dotnet hardslice` ;
- `.config/dotnet-tools.json` du template : entrée `hardslice` à la version de `HardSlice.Analyzers`, `rollForward: false`. `dotnet-ef` garde sa version, alignée sur EF Core (10.0.14 aujourd'hui) ;
- `check` reprend tout le contrat actuel des scripts, dans le même ordre et avec les mêmes messages : build de la solution (restauration comprise), tests unitaires, tests d'architecture, détection d'un runtime de conteneurs compatible Docker avec le message actuel, tests d'intégration. Il propage le code de sortie de la première étape en échec et rend `1` sans Docker ;
- `check` échoue avec un message explicite si la version de `HardSlice.Analyzers` déclarée dans `Directory.Packages.props` diffère de la sienne ;
- `check` ne réinterprète pas la sortie du compilateur : les diagnostics restent ceux de `dotnet build` ;
- `scripts/check.sh` et `scripts/check.ps1` deviennent des lanceurs minces : `dotnet tool restore`, puis `dotnet hardslice check`, code de sortie propagé ;
- `explain <id>` affiche la fiche embarquée et rend `0` ; un identifiant inconnu rend `2` et liste les fiches disponibles ;
- source unique des fiches : `src/HardSlice.Templates/content/hardslice-api/docs/rules/`, embarquée dans l'outil au build et livrée par le template ;
- `README.md` et `AGENTS.md` décrivent `check` et `explain`.

**Limites**

Pas de `init`, `map`, `baseline`, `report` ni `verify` (v0.5). Pas d'option qui saute une étape de la vérification.

**Dépendances**

Incrément 2.

**Cas vérifiés**

- les sondes existantes de `scripts/test-template.sh` échouent toujours par les deux scripts, avec les mêmes messages ;
- `DOCKER_HOST` vers un socket absent : message actuel et code `1` ;
- test de l'outil : le texte de chaque fiche embarquée est identique au fichier livré par le template ; l'ensemble des fiches est égal à l'ensemble des règles livrées ;
- version de l'outil, de l'entrée `hardslice` du manifeste et de la `GlobalPackageReference` identiques ; une version différente fait échouer `check`. La version de `dotnet-ef` n'est pas concernée.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-022 : HS003, un endpoint n'accède jamais directement à la base

**Résultat observable**

Un endpoint qui reçoit, injecte ou obtient un `DbContext` fait échouer le build avec HS003.

**Périmètre**

- endpoint : délégué passé à une méthode de mapping d'ASP.NET Core reconnue par son symbole (`MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`, `MapMethods`, `Map` d'`EndpointRouteBuilderExtensions`, `MapFallback`), sur un `IEndpointRouteBuilder` ou un `RouteGroupBuilder`. Les noms `Map*` et `*Endpoint` ne suffisent jamais ;
- formes du délégué : lambda, méthode anonyme, groupe de méthodes statique ou d'instance déclaré dans la compilation ;
- accès à la base : dans le corps du délégué, lambdas et fonctions locales comprises, tout paramètre, variable locale, champ, propriété ou résultat d'appel dont le type est ou dérive de `DbContext`, `DbSet<T>`, `IDbContextFactory<T>`, `DatabaseFacade`, `DbConnection`, `IDbConnection` ou `DbDataSource` ;
- s'applique partout dans la compilation, slices, module et `Program.cs` compris.

**Limites**

Les méthodes appelées par l'endpoint ne sont pas suivies. Un délégué venu d'une variable, d'un champ ou d'un assembly en métadonnées n'est pas analysable et ne produit aucun diagnostic. Les contrôleurs MVC ne sont pas couverts.

**Dépendances**

Incrément 2.

**Cas conformes**

Endpoints de Bookings qui reçoivent `IMediator` ; endpoint qui n'utilise que `HttpContext`.

**Cas fautifs**

- paramètre `AppDbContext db` d'une lambda ;
- groupe de méthodes `MapGet("/", List)` dont la méthode reçoit `AppDbContext` ;
- champ `DbContext` d'une classe dont une méthode d'instance est passée à `MapPost` ;
- `context.RequestServices.GetRequiredService<AppDbContext>()` ;
- `NpgsqlDataSource` injecté.

**Génériques et inter-assemblies**

`DbSet<Booking>`, `IDbContextFactory<AppDbContext>`, contexte dérivé d'une base intermédiaire, contexte déclaré dans un projet référencé.

**Sévérité et diagnostic**

`error`.

```text
HS003: endpoint 'POST /bookings' in slice 'Bookings/ConfirmBooking' uses 'AppDbContext' (parameter 'db'). Endpoints never reach the database. Fix: move the data access into the handler of 'ConfirmBookingCommand' and send the command with IMediator. Doc: docs/rules/HS003.md
```

Un diagnostic par endpoint et par symbole fautif.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-023 : HS015, l'endpoint d'une slice reste dans sa slice, son groupe et ses `TypedResults`

**Résultat observable**

Un endpoint mappé hors de sa slice, hors du groupe du module, non enregistré par son module ou qui ne renvoie pas un type de `HttpResults` fait échouer le build avec HS015.

**Périmètre**

Pour tout appel de mapping reconnu comme dans PB-022 et situé dans l'espace de noms `Features` :

- emplacement : l'appel est dans une slice. Un appel dans le type du module ou à la racine de `Features` est fautif ;
- groupe : le récepteur de l'appel est un `RouteGroupBuilder`. Un récepteur `IEndpointRouteBuilder` ou `WebApplication` est fautif ;
- enregistrement : la méthode de la slice qui contient l'appel est invoquée par le type `<Module>Module` de son module. Vérifié en fin de compilation ;
- résultat : le type de retour du délégué, après `Task<T>` ou `ValueTask<T>`, est un type de l'espace de noms `Microsoft.AspNetCore.Http.HttpResults` (`Ok<T>`, `Created`, `NotFound`, `Results<T1, T2>`…). `IResult`, un DTO, `object`, `void`, `Task` et `ValueTask` sont fautifs.

**Limites**

Les endpoints hors de `Features`, comme un endpoint technique de `Program.cs`, ne sont pas des endpoints de slice : HS015 ne les vérifie pas, HS003 si. Un type de résultat personnalisé qui implémente `IResult` est fautif en v0.2. Dans l'IDE, l'enregistrement par le module n'est signalé qu'en analyse complète de la solution.

**Dépendances**

Incrément 2.

**Cas conformes**

Les trois endpoints de Bookings ; `Task<Results<Ok<BookingDto>, NotFound>>` ; groupe imbriqué `group.MapGroup("/{id}")`.

**Cas fautifs**

`app.MapPost` dans une slice ; mapping dans `BookingsModule` ; `Results.Ok(dto)` (type `IResult`) ; lambda qui renvoie le DTO ; `ArchiveBookingEndpoint.Map` jamais appelé par `BookingsModule`.

**Génériques et inter-assemblies**

`Results<Created<T>, Conflict>` ; groupe de méthodes générique. Module et slices sont dans la même compilation : pas de cas inter-assemblies.

**Sévérité et diagnostic**

`error`.

```text
HS015: endpoint 'GET /bookings/{id:guid}' in slice 'Bookings/GetBooking' returns 'IResult'. A slice endpoint returns TypedResults, so its responses are part of its signature. Fix: return 'Results<Ok<BookingDto>, NotFound>' built with TypedResults. Doc: docs/rules/HS015.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-024 : HS011, pas d'appel bloquant ni d'`async void`

**Résultat observable**

`.Result`, `.Wait()`, `GetAwaiter().GetResult()` et toute forme d'`async void` font échouer le build avec HS011.

**Périmètre**

Symboles reconnus, et seulement eux :

- propriété `Result` de `Task<TResult>` et de `ValueTask<TResult>` ;
- méthodes `Wait` de `Task`, `Task.WaitAll` et `Task.WaitAny`, toutes surcharges ;
- appel explicite de `GetResult()` sur `TaskAwaiter`, `TaskAwaiter<TResult>`, `ValueTaskAwaiter<TResult>` et leurs variantes configurées.

Formes d'`async void` :

- méthode, fonction locale, implémentation d'interface ou substitution déclarée `async` qui renvoie `void` ;
- lambda ou méthode anonyme `async` convertie en un délégué dont `Invoke` renvoie `void` (`Action`, `Action<T>`, `EventHandler`, délégué personnalisé), par exemple `list.ForEach(async item => …)`.

S'applique à tous les projets de production.

**Limites**

`await` n'est pas un appel bloquant. Un membre nommé `Result` ou `Wait` d'un autre type n'est pas concerné. `.Result` sur une tâche déjà attendue est signalé quand même : la fiche indique d'utiliser `await` une seconde fois.

**Dépendances**

Incrément 2.

**Cas conformes**

`await task` ; `Task.Run(async () => …)`, converti en `Func<Task>` ; propriété `Result` d'un type `OperationResult` de l'application ; méthode d'extension `Wait` d'une bibliothèque tierce.

**Cas fautifs**

Les trois appels bloquants, sur `Task<List<T>>` et `ValueTask<T>` ; `async void` sur une méthode, une fonction locale et une lambda passée à `List<T>.ForEach`.

**Génériques et inter-assemblies**

Tâches génériques imbriquées ; `async void` dans le Domain, signalé dans sa propre compilation.

**Sévérité et diagnostic**

`error`.

```text
HS011: 'ConfirmBookingHandler.Handle' blocks on 'Task<TResult>.Result'. Code never blocks on asynchronous work. Fix: make the caller async and await the task. Doc: docs/rules/HS011.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-025 : HS010, pas d'horloge système dans le domaine et les slices

**Résultat observable**

Une lecture de l'horloge système dans le Domain ou dans une slice fait échouer le build avec HS010.

**Périmètre**

- symboles : `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now` et `DateTimeOffset.UtcNow` ;
- périmètre annoncé par la spécification, sans élargissement : code de l'assembly déclarant du contrat `entity`, et code des slices de la compilation analysée (`SliceMap`) ;
- lectures dans un arbre d'expression (filtre EF Core) comprises : la fiche indique de passer l'instant en paramètre ;
- `nameof` n'est pas une lecture.

**Limites**

Le code du module (`BookingsModule`, configurations EF Core), `Shared`, `Program.cs` et les tests ne sont pas couverts. Une lecture déplacée dans un utilitaire de `Shared` appelé par une slice n'est pas détectée : la fiche la cite comme contournement interdit. `TimeProvider.System` n'est pas signalé.

**Dépendances**

PB-018, pour reconnaître l'assembly du Domain.

**Cas conformes**

`TimeProvider` injecté et `GetUtcNow()` ; instant reçu en paramètre par le domaine ; `DateTime.UtcNow` dans `Shared/Behaviors`.

**Cas fautifs**

`DateTimeOffset.UtcNow` dans `Booking`, dans `ConfirmBookingHandler` et dans un filtre `Where` ; `using static System.DateTime;` puis `Now`.

**Génériques et inter-assemblies**

Domain compilé à part : signalé dans la compilation du Domain. Méthode générique d'une slice.

**Sévérité et diagnostic**

`error`.

```text
HS010: 'ConfirmBookingHandler.Handle' in slice 'Bookings/ConfirmBooking' reads 'DateTimeOffset.UtcNow'. The domain and the slices never read the system clock. Fix: inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter. Doc: docs/rules/HS010.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-026 : HS004, structure, nommage et emplacement d'une slice

**Résultat observable**

Une slice sans requête, sans handler ou sans endpoint, mal nommée ou dont un fichier est hors de son dossier fait échouer le build avec HS004.

**Périmètre**

Pour chaque slice `Features/<Module>/<Slice>` :

- exactement une requête : `<Slice>Command` qui implémente `command`, ou `<Slice>Query` qui implémente `query` ;
- son handler, nommé `<Slice>Handler`, dans la slice ;
- son endpoint, type statique `<Slice>Endpoint`, dans la slice ;
- un validateur de la requête, s'il existe (`IValidator<T>` de FluentValidation), est nommé `<Slice>Validator` et déclaré dans la slice. Son absence relève de HS013 ;
- une requête, un handler ou un validateur hors de toute slice (racine du module, `Shared`, ailleurs) est fautif ;
- emplacement : chaque déclaration d'un type de la slice est dans un fichier sous `<projet>/Features/<Module>/<Slice>/`, et chaque type déclaré dans ce dossier appartient à l'espace de noms de la slice. Les types partiels sont vérifiés déclaration par déclaration ; le code généré est exclu.

**Limites**

Le nom et le nombre des fichiers ne sont pas contrôlés (question ouverte n° 5 de la spécification). L'obligation d'un endpoint vaut pour toutes les slices de v0.2 ; elle sera revue en v0.3 pour les slices déclenchées par un événement. Un handler placé dans une autre slice relève de HS016, qui le signale déjà : HS004 ne le signale pas une seconde fois.

**Dépendances**

PB-018, pour les contrats de messagerie.

**Cas conformes**

Les trois slices de Bookings, dont `GetBooking` sans validateur et avec un DTO ; variante `--no-sample` sans slice.

**Cas fautifs**

Slice sans requête ; `ArchiveCommand` dans `ArchiveBooking` ; deux requêtes dans une slice ; handler `ArchiveHandler` ; endpoint absent ; validateur `ArchiveRules` ; validateur à la racine du module ; fichier `Features/Bookings/ConfirmBooking.cs` avec l'espace de noms de la slice.

**Génériques et inter-assemblies**

Commande générique `ArchiveBookingCommand<T>`, nommée sans son arité ; handler qui hérite d'une base générique ; contrats de messagerie résolus en métadonnées.

**Sévérité et diagnostic**

`error`.

```text
HS004: slice 'Bookings/ArchiveBooking' declares 'ArchiveCommand'. A slice holds one request named after it, its handler and its endpoint. Fix: rename the command to 'ArchiveBookingCommand'. Doc: docs/rules/HS004.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-027 : HS005, un handler de commande ne renvoie jamais d'entité

**Résultat observable**

Un handler de commande dont la réponse contient une entité, directement, dans une enveloppe ou dans un DTO, fait échouer le build avec HS005.

**Périmètre**

- handler de commande : implémentation, directe ou héritée, de `requestHandler` pour une requête qui implémente `command`. La réponse examinée est son argument `TResponse` ;
- la réponse contient une entité si l'on atteint un type qui implémente `entity`, racine ou entité enfant, en parcourant :
  - le type lui-même et `Nullable<T>` ;
  - les éléments de tableau ;
  - les arguments de tout type générique construit (`List<T>`, `IReadOnlyList<T>`, `Dictionary<TKey, TValue>`, `Result<T>`…) ;
  - les éléments de tuple ;
- **DTO contenant une entité** : parcours distinct des propriétés et champs publics d'instance des types déclarés dans l'assembly du Domain, dans la compilation analysée ou dans un assembly qui référence le Domain. Ce sont les seuls où un DTO peut contenir une entité. Les types du framework et des bibliothèques ne sont pas parcourus. Le parcours est récursif et protégé contre les cycles ; le message nomme le chemin jusqu'à l'entité.

**Limites**

Une entité typée `object`, `dynamic` ou rangée dans une collection non générique, un sous-type connu seulement à l'exécution, ou un membre non public ne sont pas détectés. Un handler générique dont `TResponse` est un paramètre de type sans contrainte n'est pas analysable ; contraint à `IEntity`, il est fautif.

**Dépendances**

PB-018.

**Cas conformes**

`BookingId` (objet valeur du Domain, parcouru : il ne contient qu'un `Guid`) ; `bool` de `CancelBooking` ; DTO de scalaires ; `Unit`.

**Cas fautifs**

`Booking` ; `List<Booking>` ; `Booking[]` ; `(BookingId, Booking)` ; `Result<Booking>` ; entité enfant `BookingLine` ; DTO `ConfirmationDto` dont la propriété `Booking` est l'entité ; DTO dont une propriété est `IReadOnlyList<BookingLine>`.

**Génériques et inter-assemblies**

Domain compilé à part et référencé en métadonnées ; DTO déclaré dans un projet qui référence le Domain ; enveloppe générique déclarée dans `Shared`.

**Sévérité et diagnostic**

`error`, sur la déclaration du handler.

```text
HS005: handler 'ConfirmBookingHandler' of command 'ConfirmBookingCommand' returns 'ConfirmationDto', whose property 'Booking' holds the entity 'Booking'. Command handlers return an identifier, a DTO of values or nothing. Fix: return 'BookingId', or a DTO built from the entity's values. Doc: docs/rules/HS005.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-028 : HS006, un handler de requête ne suit ni n'écrit

**Résultat observable**

Un handler de requête qui écrit par EF Core, réactive ou reconfigure le suivi, ou lit une entité sans garantir localement qu'elle n'est pas suivie fait échouer le build avec HS006.

**Contrat**

Références : [Tracking vs. No-Tracking Queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking), [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).

Un handler de requête implémente `requestHandler` pour une requête qui implémente `query`. Tout son code est examiné : méthodes, lambdas, fonctions locales et initialiseurs. HS006 exige une **garantie locale** d'absence de suivi, visible dans le handler. Le réglage global `UseQueryTrackingBehavior` n'est pas analysé : il vit dans la racine de composition et peut changer sans que le handler change. L'absence d'`AsNoTracking` local ne prouve donc pas que la lecture est suivie ; elle signifie que le handler ne garantit pas qu'elle ne l'est pas, et c'est ce que HS006 signale.

**Fautif** :

1. **Écriture EF Core directe** : appel de `SaveChanges`, `SaveChangesAsync`, `ExecuteUpdate`, `ExecuteUpdateAsync`, `ExecuteDelete`, `ExecuteDeleteAsync` (`EntityFrameworkQueryableExtensions`), `ExecuteSql`, `ExecuteSqlAsync`, `ExecuteSqlRaw`, `ExecuteSqlRawAsync`, `ExecuteSqlInterpolated`, `ExecuteSqlInterpolatedAsync` (`RelationalDatabaseFacadeExtensions`) ; ou modification du suivi par `Add`, `AddAsync`, `AddRange`, `AddRangeAsync`, `Attach`, `AttachRange`, `Update`, `UpdateRange`, `Remove` et `RemoveRange` de `DbContext` ou `DbSet<T>`.
2. **Suivi réactivé ou reconfiguré** : appel d'`AsTracking`, ou affectation de `ChangeTracker.QueryTrackingBehavior`, quelle que soit la valeur. C'est une convention du handler : il ne modifie pas la configuration du contexte qu'il reçoit, car ce contexte peut servir à d'autres composants de la même portée (c'est le cas du `DbContext` à portée de requête du template), ce que le handler n'a pas à connaître.
3. **Lecture qui suit toujours** : `Find` et `FindAsync`.
4. **Lecture sans garantie locale** : une requête reconnue, enracinée dans un ensemble d'entités, dont le résultat contient une entité, et dont la chaîne garantie ne contient ni `AsNoTracking` ni `AsNoTrackingWithIdentityResolution`.

**Requête reconnue** :

- racine : propriété `DbSet<T>`, `Set<T>()`, `FromSql`, `FromSqlRaw` ou `FromSqlInterpolated`, avec `T` entité au sens de PB-018 ;
- chaîne d'opérateurs LINQ et EF Core dans une même expression ;
- variable locale `IQueryable<T>` de la même méthode, initialisée par une requête reconnue et réaffectée seulement sous la forme `query = query.<opérateurs>`, que la réaffectation soit conditionnelle ou non ; toute autre réaffectation rend la requête non analysable ;
- **chaîne garantie** : l'expression elle-même, ou pour une variable locale son initialiseur. Les réaffectations sont supposées pouvoir ne pas s'exécuter, faute d'analyse du flot de contrôle : un `AsNoTracking` ajouté par une réaffectation ne garantit rien, alors qu'un `AsTracking` ajouté par une réaffectation reste fautif ;
- exécution : opérateur terminal synchrone ou asynchrone (`ToList`, `ToArray`, `ToDictionary`, `First`, `Single`, `Last`, `ElementAt` et leurs variantes, `Load`, `AsAsyncEnumerable`), ou `foreach` sur la requête.

**Le résultat contient une entité** lorsque le type de l'élément final contient une entité au sens de PB-027, ou lorsque la dernière projection fait passer une expression de type entité dans le résultat : membre d'un type anonyme, argument d'un constructeur ou d'un initialiseur de DTO, argument d'une méthode évaluée côté client.

**Conforme** :

- requête reconnue avec `AsNoTracking` ou `AsNoTrackingWithIdentityResolution` dans sa chaîne garantie, sans `AsTracking` ;
- requête reconnue dont le résultat ne contient aucune entité, par exemple `Select(b => b.Id)` ou un DTO de scalaires : EF Core ne suit rien.

**Non analysable** : HS006 ne produit aucun diagnostic et ne déclare pas le code conforme. Un utilitaire peut parfaitement respecter la règle sans que HS006 puisse le vérifier :

- requête obtenue d'un champ, d'une propriété, d'un paramètre ou d'une méthode utilitaire ;
- requête ou contexte passé à une méthode qui n'est pas un opérateur LINQ ou EF Core : la méthode n'est pas suivie ;
- variable locale réaffectée autrement que par `query = query.<opérateurs>` ;
- requête compilée (`EF.CompileQuery`, `EF.CompileAsyncQuery`) ;
- écriture faite dans un utilitaire, par ADO.NET ou par une autre bibliothèque.

**Limites**

Pas d'analyse interprocédurale ni de flot de contrôle général en v0.2. La fiche HS006 sépare deux listes : les formes que la règle ne sait pas analyser, permises lorsqu'elles respectent le contrat ; les **contournements interdits**, c'est-à-dire déplacer une écriture ou une lecture sans garantie locale dans un utilitaire, ou passer par une autre bibliothèque, pour échapper au diagnostic.

**Dépendances**

PB-018.

**Cas conformes**

`GetBookingHandler` actuel ; `Select(b => b.Id).ToListAsync()` sans `AsNoTracking` ; `AsNoTrackingWithIdentityResolution` puis projection d'entités ; filtre conditionnel `if (roomId is { } id) query = query.Where(b => b.RoomId == id);` sur une variable initialisée par `Set<Booking>().AsNoTracking()`.

**Cas fautifs**

`SaveChangesAsync` ; `ExecuteDeleteAsync` ; `ExecuteSqlRawAsync` ; `db.Add(...)` ; `AsNoTracking().AsTracking()` ; `ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking` ; `FindAsync` ; `Set<Booking>().ToListAsync()` ; `Select(b => new { b, b.Id })` sans `AsNoTracking` ; `Select(b => Describe(b))` sans `AsNoTracking`, où `Describe` est une méthode C# évaluée côté client ; `AsNoTracking` conditionnel : `var query = db.Set<Booking>(); if (readOnly) query = query.AsNoTracking(); return await query.ToListAsync(ct);` produit HS006, car l'initialiseur ne garantit pas l'absence de suivi sur tous les chemins.

**Génériques et inter-assemblies**

Entités du Domain en métadonnées ; `DbSet<T>` d'un contexte déclaré dans un projet référencé ; projection vers un `Result<BookingDto>` générique.

**Sévérité et diagnostic**

`error`.

```text
HS006: query handler 'GetBookingHandler' reads 'Booking' from 'Set<Booking>()' without a local guarantee that it is not tracked. Query handlers read without tracking and never write. Fix: add AsNoTracking() where the query starts, and project to a DTO. Doc: docs/rules/HS006.md
HS006: query handler 'ListBookingsHandler' writes through 'ExecuteDeleteAsync'. Query handlers read without tracking and never write. Fix: move the write into a command slice. Doc: docs/rules/HS006.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-029 : HS007, l'état d'une entité ne s'écrit pas de l'extérieur

**Résultat observable**

Une entité ou une racine d'agrégat dont un `set`, un `init` ou un champ est accessible en écriture depuis l'extérieur fait échouer le build avec HS007.

**Périmètre**

- types qui implémentent `entity`, directement ou par héritage, racines comprises ;
- critère : un accesseur `set` ou `init` accessible depuis l'extérieur de l'assembly, et pas seulement l'accessibilité apparente sur la classe ;
- propriété d'instance dont l'accesseur `set` ou `init` est public ;
- propriété qui implémente, explicitement ou implicitement, l'accesseur `set` ou `init` d'une interface d'accessibilité effective publique, déclarée dans la compilation ou dans un assembly référencé : `((IEditableBooking)booking).Status = BookingStatus.Cancelled;` écrit l'état sans passer par une méthode du domaine ;
- champ d'instance public non `readonly` : il contourne la règle de la même façon ;
- membre public hérité d'une classe de base qui n'est pas une entité, ou déclarée dans un autre assembly : signalé sur la déclaration de l'entité ;
- record utilisé comme entité : ses propriétés positionnelles ont un `init` public et sont signalées.

**Limites**

DTO, commandes, requêtes, événements et objets valeur ne sont pas concernés. `private`, `protected`, `internal` et `private protected` sont admis, comme l'implémentation d'un setter d'interface `internal`, qui n'est pas accessible hors de l'assembly. Une écriture par réflexion n'est pas détectée.

**Dépendances**

PB-018.

**Cas conformes**

`Booking` actuel ; `{ get; private set; }` ; `BookingPeriod` (objet valeur) ; DTO avec `init` public.

**Cas fautifs**

`public BookingStatus Status { get; set; }` ; `public Guid RoomId { get; init; }` ; `public DateTimeOffset CancelledAt;` ; base `AuditedEntity` non entité avec `public DateTimeOffset UpdatedAt { get; set; }` héritée par une entité ; entité enfant `BookingLine` avec setter public ; `BookingStatus IEditableBooking.Status { get; set; }`, implémentation explicite d'une interface publique.

**Génériques et inter-assemblies**

Base générique `Entity<TId>` avec `public TId Id { get; set; }` ; base déclarée dans un autre assembly : signalé dans la compilation de l'entité dérivée.

**Sévérité et diagnostic**

`error`, dans la compilation qui déclare l'entité.

```text
HS007: entity 'Booking' exposes 'Status' with a public setter. The state of an entity changes only through its methods, which protect its invariants. Fix: make the setter private and change the state in a domain method, such as 'Booking.Cancel()'. Doc: docs/rules/HS007.md
HS007: entity 'Booking' lets 'IEditableBooking.Status' be set from outside. The state of an entity changes only through its methods, which protect its invariants. Fix: remove the setter from the public interface and change the state in a domain method. Doc: docs/rules/HS007.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-030 : HS008, un agrégat référence un autre agrégat par son identifiant

**Résultat observable**

Une entité, racine ou enfant, qui porte une référence directe à une autre racine d'agrégat fait échouer le build avec HS008.

**Périmètre**

- membres examinés : propriétés et champs d'instance de toute accessibilité des types qui implémentent `entity` (EF Core mappe aussi les champs privés) ;
- le type du membre contient une racine si l'on atteint un type qui implémente `aggregateRoot` par le type lui-même, un élément de tableau ou un argument générique (`List<Room>`, `IReadOnlyCollection<Room>`) ;
- **racines propriétaires possibles** d'un type d'entité enfant : les types de racines qui atteignent ce type par leurs membres, directement ou par d'autres entités enfants. HardSlice retient la définition selon laquelle une racine atteint ses entités enfants par ses membres. La déduction porte sur des types, pas sur des instances : deux racines peuvent posséder des instances distinctes d'un même type d'enfant sans partager leur état ;
- fautif, depuis une racine : tout membre dont le type contient une racine, y compris une autre instance du même type de racine. Une racine ne contient jamais un autre agrégat ;
- fautif, depuis une entité enfant : un membre dont le type contient une racine `R` lorsque l'enfant a au moins une racine propriétaire possible, que `R` n'en fait pas partie, et que tous les membres de `R` sont visibles (`R` déclarée dans la compilation analysée) ;
- permis : toute référence d'une racine ou d'une entité enfant à une entité enfant ; la référence inverse d'une entité enfant vers l'une de ses racines propriétaires possibles ;
- ambigu, sans diagnostic : enfant sans racine propriétaire possible dans la compilation, ou racine `R` déclarée dans un autre assembly, dont les membres privés ne sont pas visibles. HardSlice n'affirme pas alors de violation. Aucun mécanisme de configuration de l'appartenance n'est ajouté en v0.2.

**Limites**

Les racines propriétaires possibles ne sont calculées que dans une compilation. La règle ne voit pas les instances : une référence inverse vers une racine propriétaire possible peut, à l'exécution, désigner un autre agrégat du même type, et une référence d'un agrégat à l'entité enfant d'un autre agrégat de même type n'est pas détectable par les types. Une référence typée `object` ou par une interface non classée n'est pas détectée.

**Dépendances**

PB-018.

**Cas conformes**

`Booking.RoomId` (`Guid`) ; `BookingId` ; collection `IReadOnlyList<BookingLine>` d'entités enfants ; `BookingLine.Booking`, référence inverse vers sa racine propriétaire possible ; `Booking` et `Invoice` qui portent chacune une collection `IReadOnlyList<Note>` du même type d'entité enfant `Note`, avec des instances indépendantes ; `Note.Booking` dans ce montage, `Booking` étant une racine propriétaire possible de `Note`.

**Cas fautifs**

`Booking.Room` de type `Room : IAggregateRoot` ; `List<Room>` ; `BookingLine.Room`, référence portée par une entité enfant ; `Booking.PreviousBooking` de type `Booking` ; `Note.Room`, alors que `Note` n'est atteinte que par `Booking` et `Invoice`.

**Génériques et inter-assemblies**

Collection générique de racines ; racine déclarée dans un autre assembly, reconnue par son contrat en métadonnées.

**Sévérité et diagnostic**

`error`.

```text
HS008: entity 'BookingLine' of aggregate 'Booking' references aggregate 'Room' through property 'Room'. An aggregate references another aggregate only by its identifier. Fix: replace the property with 'RoomId' and load 'Room' in the handler that needs it. Doc: docs/rules/HS008.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-031 : HS009 et HS019, forme et nom des événements de domaine

**Résultat observable**

Un événement de domaine qui n'est pas un `record` fait échouer le build avec HS009. Un nom qui ne se lit pas comme un fait passé produit la suggestion HS019, sans bloquer.

**Périmètre**

- HS009 : tout type, non interface, qui implémente `domainEvent`, directement ou par héritage, est un `record class` ou un `record struct` ;
- HS019 : le dernier mot du nom en PascalCase finit par `ed`, ou figure dans une liste fixe de participes passés irréguliers publiée dans la fiche (`Paid`, `Sent`, `Built`, `Held`, `Sold`, `Taken`, `Written`…) ;
- HS009 s'exécute dans la compilation qui déclare l'événement, y compris hors du Domain.

**Limites**

HS009 vérifie que l'événement est un `record`, pas qu'il est immuable : un record peut porter une propriété `set`. Aucun contrôle d'immuabilité n'est ajouté en v0.2.

HS019 est une convention de nommage : elle ne prouve pas que le nom exprime un fait passé. `RoomSeed` et `MediaEmbed` finissent par `ed` sans être au passé ; `BookingPaid` ne finit pas par `ed`, d'où la liste d'irréguliers. Elle n'est jamais bloquante et n'est active qu'en `All`, en `suggestion`. Pas de liste d'exceptions configurable en v0.2.

**Dépendances**

PB-018.

**Cas conformes**

`BookingConfirmed` ; record abstrait de base ; `BookingPaid` ; `record struct`.

**Cas fautifs**

HS009 : `sealed class BookingConfirmed : IDomainEvent` ; classe qui implémente une interface dérivée `IBookingEvent : IDomainEvent` ; classe d'événement déclarée dans `MyApp`. HS019 : `ConfirmBooking`, `BookingConfirmation`.

**Génériques et inter-assemblies**

`EntityChanged<T>` ; contrat en métadonnées depuis `MyApp`.

**Sévérité et diagnostic**

HS009 en `error` ; HS019 en `suggestion`.

```text
HS009: domain event 'BookingConfirmed' is a class. Domain events are records. Fix: declare it as 'public sealed record BookingConfirmed(...) : IDomainEvent'. Doc: docs/rules/HS009.md
HS019: domain event 'ConfirmBooking' is not named as a past fact. Domain events are named in the past tense. Fix: rename it, for example to 'BookingConfirmed'. Doc: docs/rules/HS019.md
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-032 : Préparer les paquets `0.2.0-preview.1` et les valider localement

**Résultat observable**

Les trois paquets `HardSlice.Analyzers`, `HardSlice.Templates` et `HardSlice.Tool` sont produits en `0.2.0-preview.1` dans `artifacts/packages`, et la solution générée depuis eux passe toute la vérification, sans accès à NuGet.org pour les paquets HardSlice.

**Périmètre**

- version unique dans `Directory.Build.props` du dépôt, propagée au moment du pack à la `GlobalPackageReference` et à l'entrée `hardslice` du manifeste d'outil du template, sans toucher à `dotnet-ef` ; un test vérifie l'égalité ;
- métadonnées : description, README de paquet, URL du dépôt, licence, étiquettes ; validation de paquet activée ; avertissements du pack traités comme des erreurs ;
- `scripts/generate-app.sh` et `scripts/test-template.sh` construisent et installent les trois paquets locaux, avec la correspondance de sources qui réserve `HardSlice.*` au dossier local ;
- `README.md` : version de préversion, réinstallation après reconstruction ; l'installation depuis NuGet.org reste décrite par PB-033.

**Dépendances**

PB-021, pour le paquet de l'outil. Peut commencer avec les deux autres paquets.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-033 : Publier la préversion `0.2.0-preview.1`

**Résultat observable**

Un utilisateur installe `HardSlice.Templates::0.2.0-preview.1` depuis NuGet.org, génère une application et passe `dotnet hardslice check`.

**Périmètre**

- workflow `.github/workflows/release.yml` déclenché par l'étiquette `v0.2.0-preview.1`, dans l'environnement protégé `nuget` : construction depuis l'étiquette, tests, pack, publication des trois paquets ;
- job de contrôle sur un runner neuf, après publication : `dotnet new install HardSlice.Templates::0.2.0-preview.1`, génération avec et sans Bookings, `dotnet tool restore`, `dotnet hardslice check` ;
- `README.md` : installation depuis NuGet.org.

**Dépendances**

PB-018 à PB-032 et les prérequis NuGet ci-dessus, dont l'accessibilité publique du dépôt.

**Acceptation**

Le workflow de publication et le job de contrôle sont verts ; les trois paquets sont visibles sur NuGet.org en `0.2.0-preview.1`.

### Définition de fini

L'incrément est terminé lorsque PB-018 à PB-033 sont satisfaits et que les jobs CI build, template, démonstration et performance sont verts sur `main`. Concrètement :

- HS003 à HS011, HS015, HS019 et HS900 à HS902 sont livrés avec leur descripteur, leur fiche, leurs tests et leur sonde dans `scripts/test-template.sh`, dans les deux variantes du template lorsque la règle y a une cible ;
- la référence Bookings ne produit aucun diagnostic HS, et aucun faux positif connu n'est accepté silencieusement ;
- la matrice des niveaux d'analyse est vérifiée et un réglage de `.editorconfig` l'emporte sur elle ;
- `dotnet hardslice check` porte la vérification, `check.sh` et `check.ps1` le lancent, et `dotnet build` seul produit toujours les diagnostics ;
- les seuils de PB-019 sont respectés ;
- les paquets `0.2.0-preview.1` sont publiés et la solution générée depuis NuGet.org passe `dotnet hardslice check`.

## 5. Incréments suivants

| Incrément | Résultat produit | Contenu principal | Acceptation globale |
|---|---|---|---|
| **3 — Analyseurs v0.2** | Les dérives principales sont détectées par des diagnostics réparables | Détaillé en section 4 (PB-018 à PB-033) : classification par contrats, HS003 à HS011, HS015 et HS019, diagnostics de configuration HS900 à HS902, niveaux d'analyse, `check`, `explain`, performance, préversion NuGet | Définition de fini de la section 4 |
| **4 — Boucle agent v0.3** | Un agent génère puis corrige par les chemins officiels | Templates d'éléments, six skills, fichiers de guidage des quatre agents, hooks après édition et de fin de tâche, scripts uniques pour hooks/pre-commit/CI, scénarios E2E | Une slice est générée, complétée et vérifiée sans recréer manuellement sa structure ; les quatre agents ciblés disposent d'un adaptateur natif ou du fallback documenté |
| **5 — Benchmark v0.4** | L'effet des garde-fous est mesuré et publié | Configurations A/B/C, tâches Bookings, runner, répétitions, coûts et qualité | Protocole, prompts, versions, résultats bruts et analyse sont publiables et reproductibles |
| **6 — Adoption v0.5** | Une équipe ajoute HardSlice sans corriger toute sa dette | `init`, `map`, conventions, baseline, cliquet, `report`, `verify`, mode couches applicable | Les violations existantes sont baselinées, une nouvelle violation est bloquée et une correction réduit la baseline |
| **7 — Comparaison v0.6** | Les architectures couches et slices sont comparées | Applications équivalentes, mêmes tâches, agents, modèles et budgets | Les conclusions distinguent l'effet de l'architecture de celui des garde-fous |
| **8 — Distribution v1.0** | HardSlice devient un produit open source installable et maintenable | Paquets stables, documentation, site, CI de publication, compatibilité, sécurité et contribution, options de template `--aspire` et SQL Server | Un utilisateur externe réussit les parcours greenfield et adoption depuis la documentation publique |

## 6. Différé, mais non abandonné

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

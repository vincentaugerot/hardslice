> **Document de conception, en français.** Document de travail du projet, public pour rendre la méthode transparente : chaque ticket, ses arbitrages datés et son acceptation. La documentation d'utilisation est en anglais, dans le [README](../../README.md) et [docs/](../README.md).

# HardSlice : backlog produit

Statut : backlog actif, 04/11/2026.

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

## 2. Incrément 1 terminé : première boucle verticale, pré-v0.1

Statut : terminé le 29/10/2026. PB-001 à PB-008 sont validés et poussés. La CI est verte. Cette section est conservée comme historique.

### Résultat attendu

> Depuis un checkout neuf, `dotnet new hardslice-api` génère une application Bookings minimale qui compile et dont les tests passent. Une référence interdite entre deux slices produit un diagnostic HS016 exploitable par un agent. Un agent corrige la violation sans contourner la règle, puis le build et les tests repassent au vert.

Le travail commence par PB-001. PB-002 ne débute qu'une fois les commandes d'acceptation de PB-001 vertes. Les tickets suivants sont abordés dans l'ordre, sauf dépendance technique explicitement constatée.

### PB-001 : Créer la solution HardSlice compilable

**Livrables**

- Solution et configuration .NET 10.
- `HardSlice.Analyzers` cible `netstandard2.0`. Les tests et l'outillage ciblent `net10.0` lorsque pertinent.
- Projets `HardSlice.Analyzers`, `HardSlice.Analyzers.Tests` et `HardSlice.Templates`.
- Propriétés et versions centralisées.
- CI minimale exécutant restore, build et tests.

**Acceptation**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Les trois commandes réussissent depuis un checkout propre.

### PB-002 : Créer l'application Bookings minimale

**Livrables**

- Projet Domain séparé, sans dépendance runtime vers ASP.NET Core, EF Core ou HardSlice. `HardSlice.Analyzers` peut y être consommé uniquement comme asset d'analyse au build.
- Application ASP.NET Core organisée en vertical slices.
- Au moins deux slices du module Bookings.
- Agrégat `Booking` avec un invariant minimal réel, par exemple une période dont la fin doit être postérieure au début. L'invariant complet de chevauchement arrive en v0.1.
- Tests unitaires et projet de tests d'architecture.
- Aucun package HardSlice chargé au runtime.

**Acceptation**

`dotnet build` et `dotnet test` réussissent. Les tests vérifient au minimum l'invariant du domaine et l'indépendance du projet Domain.

### PB-003 : Implémenter HS016

**Livrables**

- Analyseur Roslyn faisant partie du produit.
- Détection entre slices d'un même assembly, forme nécessaire au premier scénario.
- Identification déterministe des modules et des slices selon la convention du template.
- Tests positifs, négatifs et cas limites couvrant au minimum : déclarations et signatures, génériques, héritage, attributs, construction, membres statiques, `typeof`, `nameof`, casts, méthodes d'extension et types imbriqués.
- Alias, global usings et classes partielles sans diagnostic en double.
- Cas autorisés : même slice, Domain et contrat public du module.
- Aucun faux positif connu dans la référence Bookings.

**Acceptation**

Une référence d'un type de `Bookings/CancelBooking` vers un type de `Bookings/ConfirmBooking` produit HS016. Les références autorisées vers le domaine ou le contrat public du module restent valides.

### PB-004 : Rendre le diagnostic HS016 réparable

**Livrables**

- Message sur une ligne.
- Symbole fautif, slice source et slice cible.
- Règle en une phrase et correction attendue.
- Référence vers une fiche HS016 locale ou embarquée.
- Fiche indiquant également les contournements interdits.

**Acceptation**

Le diagnostic contient assez d'informations pour corriger la fixture sans consulter le code de l'analyseur. Forme attendue :

```text
HS016: 'CancelBookingHandler' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'. Slices never reference each other. Fix: move the shared rule into the Booking aggregate, raise a domain event, or use the module public contract. Doc: ...
```

Dans le premier incrément, le chemin documentaire local est garanti par le template `hardslice-api`. L'installation directe du seul paquet `HardSlice.Analyzers`, hors d'une solution générée, n'est pas encore un parcours supporté. Elle recevra une référence documentaire publique stable avec le kit d'adoption.

### PB-005 : Packager le template local

**Livrables**

- Paquet local `HardSlice.Templates`.
- Commande `dotnet new hardslice-api`.
- Application Bookings minimale incluse.
- Analyseur HS016 installé et actif dans le projet généré.
- Fiche `docs/rules/HS016.md` présente au chemin annoncé par le diagnostic.
- `scripts/test-template.sh`, qui construit et installe le paquet, génère une application dans un répertoire temporaire puis lance ses tests.

**Acceptation**

```text
scripts/test-template.sh
```

La solution générée compile et ses tests passent sans modification manuelle. Le script vérifie également que le chemin `Doc:` annoncé par HS016 existe depuis la racine de la solution générée.

### PB-006 : Ajouter la vérification et le guidage agent

**Livrables**

- `AGENTS.md` court dans le template.
- `scripts/check.sh` et `scripts/check.ps1`.
- Consigne qu'un agent n'a pas terminé tant que la vérification échoue.
- Instructions pour corriger HS016 sans suppression, baisse de sévérité ni modification de l'analyseur.

**Acceptation**

Les deux scripts appliquent le même contrat logique (restore si nécessaire, build, tests unitaires et tests d'architecture) et retournent un code non nul en cas d'échec.

### PB-007 : Vérifier l'installation depuis un checkout propre

**Livrables**

- Exécution de `scripts/test-template.sh` dans un environnement propre de CI.
- Checkout neuf, caches non requis et aucun fichier non versionné.
- Conservation du même scénario que PB-005, sans second harness concurrent.

**Acceptation**

Le job de CI exécute `scripts/test-template.sh` depuis un checkout propre et réussit sans état implicite de la machine du développeur.

### PB-008 : Démontrer la correction par un agent

**Livrables**

- Fixture contenant une violation HS016 volontaire.
- Prompt de développement simple.
- Trace concise de la correction.
- Vérification qu'aucune règle, configuration ou test n'a été contourné.

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

## 3. Incrément 2 terminé : Fondation v0.1

Statut : terminé le 30/10/2026. PB-009 à PB-017 sont validés et poussés. La CI est verte. Cette section est conservée comme historique.

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

- Tout ticket qui modifie le template met à jour `scripts/test-template.sh` pour couvrir sa capacité dans la solution générée depuis le paquet local, et l'exécute : tester les sources de `content/hardslice-api` depuis la solution du dépôt ne suffit pas.
- À partir de PB-009, la CI exécute `scripts/demo-hs016-agent-fix.sh prepare`. Tout ticket qui modifie la structure du template ou des slices adapte la fixture PB-008 dans le même changement et garde ce job vert. Depuis PB-049, ce job exécute `scripts/demo-agent-fix.sh verify-reference` sur les fixtures de toutes les règles bloquantes, dans `demos/agent-fix/`.
- `BookingsReferenceTests` reste vert : la référence Bookings compile avec ses dépendances réelles et ne produit aucun HS016.
- `AGENTS.md` et les fiches de règles sont mis à jour dans le ticket qui change la carte, la consigne ou l'exemple qu'ils décrivent.
- Aucun assembly HardSlice n'est chargé au runtime de l'application générée.
- La licence de toute nouvelle dépendance est vérifiée au moment de son introduction.

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

### PB-009 : Persister les réservations dans PostgreSQL

**Résultat visible**

L'application générée confirme et annule réellement des réservations dans PostgreSQL, avec le comportement HTTP existant : `POST /bookings`, `POST /bookings/{id}/cancel`. Ses tests d'intégration Testcontainers passent dans la solution générée depuis le paquet local.

**Décision : emplacement de la persistance (résolution de la question n° 7 de la spécification)**

- `Shared/Persistence/` contient l'infrastructure transverse : `AppDbContext`, son enregistrement, la configuration de la connexion et les migrations. `AppDbContext` ne déclare aucun type de module : il applique les configurations découvertes dans l'assembly, et les slices accèdent aux agrégats par `Set<T>()`.
- La configuration EF Core d'un agrégat vit au niveau du module qui l'utilise (`Features/<Module>/<Aggregate>Configuration.cs`, `internal`), hors de toute slice. Ajouter un module ne modifie donc pas `Shared`.
- Compatibilité HS001 : le mapping est entièrement en API fluente hors du Domain. `MyApp.Domain` ne reçoit ni attribut ni référence EF Core.
- Compatibilité HS017 : `Shared` ne référence aucun type de `Features`. Il dépend uniquement du domaine et du framework, dans le dossier autorisé `Persistence`.
- Compatibilité HS016 : la configuration appartient au module, pas à une slice, et aucune slice ne la référence.

**Livrables**

- `AppDbContext`, EF Core et Npgsql dans `src/MyApp`, versions centralisées dans `Directory.Packages.props` du template.
- Configuration EF Core de `Booking` conforme à la décision ci-dessus, sans constructeur public ni setter ajoutés au domaine.
- Migration initiale versionnée, valide après le renommage `MyApp` → nom choisi par `dotnet new`.
- Connexion lue dans `ConnectionStrings`, sans secret de production versionné.
- Outil `dotnet-ef` épinglé dans `.config/dotnet-tools.json`. Les migrations s'appliquent par `dotnet ef database update` ou par la fixture de tests, et l'application ne migre pas la base à son démarrage.
- `ConfirmBooking` et `CancelBooking` utilisent `AppDbContext`. `IBookingStore` et `InMemoryBookingStore` sont supprimés.
- Projet `tests/MyApp.IntegrationTests` (Testcontainers PostgreSQL, hôte web de test, migrations appliquées sur le conteneur), ajouté à `MyApp.slnx` et à `HardSlice.slnx`.
- Tests d'intégration au même chemin que leur slice. Les tests de handlers qui dépendaient du stockage en mémoire deviennent des tests d'intégration, sans fournisseur EF InMemory ni SQLite.
- `scripts/check.sh` et `scripts/check.ps1` exécutent les tests d'intégration après les tests unitaires et d'architecture. Avant cette étape, ils échouent avec un message qui nomme le prérequis si aucun runtime de conteneurs compatible Docker n'est joignable.
- `scripts/test-template.sh` exécute les tests d'intégration de la solution générée par les deux scripts et contient une sonde isolée qui simule un runtime de conteneurs injoignable afin de vérifier le message de prérequis sans désactiver le parcours positif.
- Job CI exécutant `scripts/demo-hs016-agent-fix.sh prepare` depuis un checkout propre. Fixture PB-008 et vérificateur de la démonstration adaptés à la nouvelle forme des slices et aux chemins de tests qu'elle utilise.
- `AGENTS.md` et `docs/rules/HS016.md` ne citent plus `IBookingStore` et donnent un exemple de contrat public de module valide.

**Dépendances**

Incrément 1 terminé.

**Critères d'acceptation**

- Une réservation confirmée par HTTP est relue depuis PostgreSQL par un contexte neuf, et une seconde instance de l'application sur la même base l'annule.
- Annuler deux fois renvoie 400 et l'état persisté reste `Cancelled`. Une réservation inconnue renvoie 404.
- Le modèle EF Core ne contient aucune modification en attente par rapport aux migrations, dans la solution générée.
- `MyApp.Domain` ne référence toujours que la bibliothèque de classes de base : les tests HS001 existants restent verts.
- Aucun type de `Shared` ne référence un type de `Features`. Si PB-014 est intégré, HS017 le vérifie. Sinon, PB-014 en fait un cas d'acceptation.
- Sans runtime de conteneurs joignable, les deux scripts de vérification échouent avec un code non nul et le message de prérequis.
- Les jobs CI build, template et démonstration réussissent. Le job build exécute aussi les tests d'intégration du template.

**Vérification**

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

**Exclusions**

- Invariant de chevauchement (PB-010), événement de domaine (PB-011), médiateur et validation (PB-012), requête `GetBooking` (PB-013).
- SQL Server et Aspire (incrément 8).
- Migration automatique au démarrage, données d'exemple, multi-tenant.
- Test d'architecture HS017 lui-même (PB-014).

### PB-010 : Interdire le chevauchement de deux réservations d'une même salle

**Résultat visible**

`POST /bookings` refuse une réservation dont la période chevauche une réservation confirmée de la même salle avec une réponse 409 `ProblemDetails` qui explique le conflit. L'invariant tient sous concurrence : sur des confirmations simultanées pour un même créneau, une seule réussit.

**Livrables**

- `BookingPeriod.Overlaps` sur des intervalles semi-ouverts `[début, fin)` : deux périodes adjacentes ne se chevauchent pas.
- Règle « pas deux réservations confirmées qui se chevauchent sur la même salle » portée par `MyApp.Domain/Bookings/`. Le handler charge les réservations confirmées de la salle sur la période et les fournit au domaine, sans comparer lui-même de périodes.
- Erreur de domaine dédiée au conflit, traduite en 409. Les autres violations de règles métier restent en 400.
- Contrainte d'exclusion PostgreSQL créée par migration (`btree_gist`, même salle, `tstzrange(début, fin, '[)')` qui se chevauchent, réservations confirmées uniquement).
- Violation de cette contrainte traduite en la même réponse 409, sans exposer l'erreur PostgreSQL.
- Tests unitaires du domaine et tests d'intégration au chemin de `ConfirmBooking`.
- `scripts/test-template.sh` et fixture PB-008 mis à jour si nécessaire.

**Dépendances**

PB-009.

**Critères d'acceptation**

- Domaine : chevauchement partiel, inclusion, égalité et périodes adjacentes couverts. Une réservation annulée ne bloque pas. Une autre salle n'est pas concernée.
- Intégration : un chevauchement séquentiel renvoie 409 et n'écrit aucune ligne. Une période adjacente, une autre salle ou un créneau libéré par une annulation renvoient 201.
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

- Liste des salles libres sur une période, remboursement partiel, équipements et refactorisation de la règle : ce sont des tâches du benchmark v0.4.
- Agrégat `Room`, modification ou déplacement d'une réservation.
- Verrouillage optimiste général.

### PB-011 : Lever `BookingConfirmed` à la confirmation

**Résultat visible**

Confirmer une réservation enregistre un événement `BookingConfirmed` sur l'agrégat. L'exemple Bookings montre ainsi le modèle d'événement de domaine que l'agent recopie.

**Livrables**

- `MyApp.Domain/Bookings/Events/BookingConfirmed.cs` : un `record` nommé au passé, porteur de l'identifiant, de la salle et de la période.
- L'agrégat enregistre les événements qu'il lève et les expose en lecture seule, sans setter public.
- Configuration EF Core qui ignore ces événements : une réservation rechargée n'en contient aucun.
- Tests unitaires du domaine.
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

- La publication des événements et leurs handlers ne font pas partie de l'acceptation v0.1.
- Outbox, bus de messages, événement d'annulation.
- Analyseur HS009 (v0.2).

### PB-012 : Faire passer les commandes par un médiateur maison avec validation

**Résultat visible**

Les endpoints envoient leurs commandes par `IMediator`. Une commande invalide reçoit un 400 `ValidationProblem` qui nomme les champs fautifs avant d'atteindre son handler. Chaque commande traverse les comportements de validation, de transaction et de logs.

**Livrables**

- `Shared/Messaging/` : `IRequest`, `IRequestHandler`, `IPipelineBehavior`, `IMediator` et leur implémentation, de l'ordre de 100 lignes, sans MediatR ni dépendance runtime HardSlice.
- Distinction explicite entre commandes et requêtes, exploitable par les tests d'architecture.
- `Shared/Behaviors/` : validation par FluentValidation, frontière transactionnelle unique par commande, logs du nom de la requête, de sa durée et de son issue, sans valeurs du contenu.
- `ConfirmBooking` et `CancelBooking` migrés : commande publique, handler, validateur et endpoint `internal`, endpoint qui envoie par `IMediator` et renvoie des `TypedResults`.
- Validateurs `ConfirmBookingValidator` et `CancelBookingValidator`.
- Enregistrement des handlers et validateurs par le module, du médiateur et des comportements par l'hôte.
- Tests unitaires du médiateur, de l'ordre du pipeline et des validateurs. Tests d'intégration des réponses 400.
- `BookingsReferenceTests` compile la référence avec FluentValidation.
- Section « Adding a slice » d'`AGENTS.md` mise à jour : commande ou requête, validateur, handler, endpoint par le médiateur, tests.
- `scripts/test-template.sh` et fixture PB-008 mis à jour.

**Dépendances**

PB-009, pour la frontière transactionnelle sur `AppDbContext`.

**Critères d'acceptation**

- Une commande invalide renvoie 400 avec les champs fautifs, n'atteint pas son handler et n'écrit rien en base.
- Une commande valide est validée, exécutée puis enregistrée dans une seule transaction. Une exception annule la transaction.
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

- Notifications ou publication d'événements par le médiateur.
- MediatR ou toute autre bibliothèque de médiation.
- Test d'architecture HS013 (PB-015) et analyseurs HS003 à HS005 (v0.2).
- Templates `hardslice-command` et `hardslice-query` (v0.3).
- Requête `GetBooking` (PB-013).

### PB-013 : Lire une réservation par une projection

**Résultat visible**

`GET /bookings/{id}` renvoie 200 avec l'identifiant, la salle, la période et le statut de la réservation, ou 404. L'URL `Location` renvoyée par `POST /bookings` mène désormais à une ressource existante.

**Livrables**

- Slice `Features/Bookings/GetBooking/GetBooking.cs` : requête publique, DTO, handler et endpoint `internal`.
- Handler qui lit par `AsNoTracking` et une projection, sans charger l'agrégat ni appeler `SaveChanges`, hors frontière transactionnelle.
- Endpoint mappé dans le groupe du module et renvoyant des `TypedResults`.
- Tests d'intégration au chemin de la slice.
- Carte des slices d'`AGENTS.md` mise à jour.
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

- Liste, recherche, pagination et salles libres.
- Cache.
- Analyseur HS006 (v0.2).

### PB-014 : Bloquer les violations de frontières par des tests d'architecture

**Résultat visible**

Dans une application générée, une violation de HS001, HS012, HS016 ou HS017 fait échouer `dotnet test` et les deux scripts de vérification. Le message nomme le symbole fautif, la règle, la correction attendue et la fiche. La solution générée contient un `hardslice.json` et un `.editorconfig` minimaux.

**Livrables**

- Choix entre ArchUnitNET et NetArchTest, évalué sur la maintenance, la licence, l'expressivité des règles de ce ticket et la durée des tests sur la solution générée. Le choix et son évaluation sont consignés dans ce ticket à sa livraison.
- `hardslice.json` minimal à la racine générée : style `slices` et dossiers autorisés de `Shared` (par défaut `Messaging`, `Persistence`, `Behaviors`) lus par HS017. Un fichier absent ou invalide fait échouer les tests d'architecture avec un message explicite.
- `.editorconfig` minimal déclarant la sévérité `error` de HS016, sans autre règle HS ni niveau d'analyse.
- Tests d'architecture dans `tests/MyApp.ArchitectureTests` :
  - HS001 : le Domain ne dépend d'aucun autre projet, ni d'EF Core, ni d'ASP.NET Core, ni de la DI.
  - HS012 : un module n'utilise que le contrat public d'un autre module, tel que HS016 le définit.
  - HS016 : aucun type d'une slice n'utilise un type d'une autre slice dans l'assembly compilé, et les handlers, validateurs et endpoints sont `internal`.
  - HS017 : `Shared` ne dépend jamais de `Features` et ne contient que les dossiers autorisés par `hardslice.json`.
- Messages en anglais au format de HS016 : symbole fautif, règle en une phrase, correction attendue, `Doc: 'docs/rules/HSxxx.md'`.
- Fiches `docs/rules/HS001.md`, `HS012.md` et `HS017.md`, et `HS016.md` complétée par sa partie test d'architecture, avec les mêmes sections que la fiche HS016.
- `AGENTS.md` : édition de `hardslice.json` et de `.editorconfig` ajoutée aux contournements interdits.
- `scripts/test-template.sh` : une violation semée par règle dans l'application générée, avec un échec attendu des deux scripts, le message de la règle et une fiche existante. La sonde HS012 est une violation que l'analyseur HS016 ne couvre pas.

**Décision : bibliothèque des tests d'architecture (résolution de la question n° 1 de la spécification)**

ArchUnitNET (`TngTech.ArchUnitNET` 0.13.4) est retenu, référencé par `tests/MyApp.ArchitectureTests` seulement. Évaluation faite à la livraison, le 29 octobre 2026 :

- Maintenance : ArchUnitNET publie régulièrement (0.13.3 en mars 2026, 0.13.4 en août, 0.13.5 en octobre), porté par TNG Technology Consulting. NetArchTest.Rules n'a plus de version depuis la 1.3.2 de mai 2021. Son fork NetArchTest.eNhancedEdition (1.4.5, juin 2025) repose sur un seul mainteneur.
- Licence : ArchUnitNET est sous Apache-2.0, ses dépendances (Mono.Cecil, Newtonsoft.Json, CycleDetection, JetBrains.Annotations) sous MIT. NetArchTest est sous MIT. Les deux conviennent : la dépendance reste dans un projet de tests et n'est jamais chargée au runtime de l'application.
- Expressivité : chaque message doit nommer le symbole fautif et le type qu'il utilise. ArchUnitNET expose, pour chaque type, ses dépendances (signatures, corps de méthodes, arguments génériques, attributs) avec leur cible, et rattache les types générés par le compilateur (lambdas, machines à états async) au type qui les déclare. HS001, HS012, HS016 et HS017 s'écrivent comme des requêtes sur ce graphe, au format de message de HS016. NetArchTest répond à « ce type dépend-il de ce namespace ? » sans dire par quelle dépendance : « aucune slice n'utilise une autre slice » demanderait un prédicat par paire de slices, et retrouver la cible des règles personnalisées sur Mono.Cecil.
- Durée, mesurée sur l'application du template : ArchUnitNET charge les deux assemblies en 450 ms environ à froid (50 ms à chaud), NetArchTest en 45 ms environ. Le projet de tests d'architecture initial (21 tests, avant la revue) s'exécutait en moins de 2 s, surtout le démarrage de l'hôte de tests : l'écart n'est pas déterminant.

La bibliothèque fournit le graphe des dépendances. Son API fluente et ses messages ne sont pas utilisés, pour garder le format HardSlice. La 0.13.4 est préférée à la 0.13.5, publiée trois jours avant la livraison.

La revue a mis en évidence deux omissions du graphe : les types nommés uniquement dans les clauses `catch` et les dépendances d'attributs d'assembly. Les premières sont ajoutées par réflexion sur les corps des méthodes et constructeurs, en rattachant les types générés (async, itérateurs, lambdas) à leur déclarant. Les secondes restent couvertes par le contrôle des références de l'assembly Domain. La visibilité des handlers, validateurs et endpoints est vérifiée sur toute la chaîne des types déclarants, y compris les types imbriqués `protected` et `protected internal`. HS017 inclut également les types directement dans le namespace racine `Features`. Des sondes de non-régression couvrent ces quatre cas dans les deux scripts de vérification.

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

- HS002 et le mode couches (v0.5).
- Nouveaux analyseurs, autres sévérités HS et niveaux d'analyse (v0.2).
- `dotnet hardslice check` et `explain` (v0.2).
- Clés de `hardslice.json` pour les modules, les chemins ou les couches, et configurabilité générale (v0.5).
- HS000 (v0.3).

### PB-015 : Bloquer les slices sans validateur ni tests

**Résultat visible**

Dans une application générée, une commande paramétrée sans validateur, un handler de commande sans tests ou une slice sans dossier de tests au même chemin fait échouer `dotnet test` et les deux scripts de vérification, avec un message réparable et sa fiche.

**Livrables**

- Tests d'architecture :
  - HS013 : toute commande qui a des paramètres a un validateur.
  - HS014 : tout handler de commande a au moins un test au chemin de sa slice, dans `MyApp.Tests` ou `MyApp.IntegrationTests`.
  - HS018 : les tests de `Features/<Module>/<Slice>/` sont au même chemin dans `tests/`. Une slice sans dossier de tests et un dossier de tests sans slice correspondante sont signalés.
- Messages au format de PB-014.
- Fiches `docs/rules/HS013.md`, `HS014.md` et `HS018.md`.
- `scripts/test-template.sh` : une violation semée par règle, avec un échec attendu des deux scripts.

**Livraison**

HS013 lit l'assembly compilé : une commande est un type concret qui implémente `ICommand<T>`, ses paramètres sont ses propriétés et champs publics d'instance, son validateur un type concret qui implémente `IValidator<T>`. Les arguments et contraintes génériques sont vérifiés : un validateur spécialisé ou plus contraint ne couvre pas toute une commande générique. L'enregistrement du validateur reste prouvé par le test d'intégration d'une requête invalide.

HS014 et HS018 lisent les sources des tests sur le disque plutôt que les assemblies de tests, que le projet de tests d'architecture ne construit ni ne référence : un assembly périmé masquerait un test supprimé. Un test est une méthode marquée `[Fact]`, `[Theory]` ou un attribut xUnit nommé comme eux, lue dans l'arbre syntaxique de `Microsoft.CodeAnalysis.CSharp` 5.0.0, la version du dépôt. Sa licence MIT a été vérifiée à son introduction. La dépendance reste dans `tests/MyApp.ArchitectureTests`. Les slices de HS018 sont les dossiers `Features/<Module>/<Slice>/` du disque et les namespaces de l'assembly compilé.

HS014 analyse les branches conditionnelles avec les symboles de chaque projet de tests : framework, `DefineConstants` propres au projet et configuration Debug/Release. `Directory.Build.targets` les transmet aux métadonnées de l'assembly d'architecture au build, avec les fichiers `Compile` évalués par MSBuild : les sources exclues ne comptent pas. Les alias d'attributs sont résolus selon leur portée (namespace, fichier ou projet), y compris les alias globaux générés par le SDK. Les fichiers restent lus sur le disque : un fichier supprimé ne compte pas via un ancien assembly. L'inventaire doit être reconstruit après modification des entrées du projet.

**Dépendances**

PB-012 pour l'identification des commandes, handlers et validateurs. PB-014 pour la bibliothèque, le format des messages et des fiches.

**Critères d'acceptation**

- L'exemple Bookings passe les trois règles. HS014 et HS018 acceptent des tests situés dans `MyApp.Tests` comme dans `MyApp.IntegrationTests`.
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

- Mesure de couverture ou de qualité des tests.
- Analyseurs HS004 et HS015 (v0.2).
- Exécution des seuls tests de la slice touchée par les hooks (v0.3).

### PB-016 : Générer une application vide avec `--no-sample`

**Résultat visible**

`dotnet new hardslice-api --no-sample` génère une solution sans module Bookings, qui compile et dont les tests unitaires, d'architecture et d'intégration ainsi que les deux scripts de vérification sont verts.

**Livrables**

- Option `--no-sample` du template, désactivée par défaut.
- Exclusion du domaine Bookings, du module `Features/Bookings`, de leurs tests et des migrations de l'exemple.
- Conservation de `Shared/Messaging`, `Shared/Persistence` sans configuration d'agrégat, `Shared/Behaviors`, `hardslice.json`, `.editorconfig`, des tests d'architecture et des scripts.
- Aucun projet de tests vide : chaque projet conservé exécute au moins un test pertinent, par exemple le démarrage de l'application sur PostgreSQL.
- `AGENTS.md` généré sans module dans sa carte et avec la marche à suivre pour un premier module.
- `scripts/test-template.sh` génère et vérifie les deux variantes.

**Livraison**

`--no-sample` est le paramètre booléen `NoSample` de `template.json`, nommé pour la ligne de commande par `dotnetcli.host.json`. Il exclut le domaine `Bookings`, le module `Features/Bookings`, les migrations, les tests unitaires et d'intégration de l'exemple, et `TransactionBehaviorTests`, qui a besoin de l'agrégat `Booking`. `Program.cs` et `AGENTS.md` portent les seules conditions du moteur de templates. Écrites `#if (!NoSample)`, celles de `Program.cs` restent vraies pour le compilateur : la solution du dépôt compile la variante par défaut, la variante vide n'est compilée que par `scripts/test-template.sh`. `TemplatePackageTests` vérifie `AGENTS.md` dans chacune des deux variantes.

Dans la variante vide, `MyApp.Tests` garde les tests de `Shared/Messaging` et `Shared/Behaviors`. `MyApp.IntegrationTests` garde `MigrationsTests`, qui échoue dès qu'un premier agrégat est mappé sans sa migration, et reçoit `ProgramTests`, présent dans les deux variantes : l'application démarre en Development, ce qui valide ses enregistrements, répond en HTTP et trouve sa base PostgreSQL migrée. Les tests d'architecture et leurs propres tests prennent leurs exemples dans un module fictif `Orders`. Les fiches `docs/rules/` gardent leurs exemples Bookings : elles documentent les règles, pas l'application générée.

`scripts/test-template.sh` génère `Blank` avec `--no-sample` depuis le même paquet. Il vérifie qu'aucun fichier hors `docs/rules/` ne mentionne Bookings, que les chemins cités par `AGENTS.md` existent, que les deux scripts et `dotnet ef migrations has-pending-model-changes` passent et que chaque projet de tests exécute les tests de la fondation. Il sème ensuite un premier module fautif : HS016 (analyseur et test d'architecture), HS018, HS013, HS014, HS017 et HS001 font échouer les deux scripts.

**Dépendances**

PB-010, PB-011 et PB-013 pour l'exemple complet à exclure. PB-015, et par lui PB-014, pour l'ensemble des règles v0.1 à vérifier sur une application vide.

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

- Template `hardslice-module` et génération d'un premier module (v0.3).
- Options `--aspire` et SQL Server (incrément 8).

### PB-017 : Documenter le template v0.1 et le valider depuis un checkout propre

**Résultat visible**

Un nouvel utilisateur suit le `README.md` du dépôt pour construire le paquet local, générer une application avec ou sans exemple, appliquer les migrations sur PostgreSQL, lancer l'API et exécuter la vérification. La CI prouve tout le contrat v0.1 depuis des runners neufs.

**Livrables**

- `README.md` à la racine du dépôt, en anglais : rôle de HardSlice, statut v0.1 en préversion locale, prérequis (.NET SDK épinglé, runtime de conteneurs compatible Docker, PowerShell pour `check.ps1`), installation du paquet local, `dotnet new hardslice-api` et `--no-sample`, exécution de l'application, vérification, règles livrées et lien vers leurs fiches, limites connues.
- `LICENSE` MIT.
- `AGENTS.md` du template final et cohérent, de moins de 150 lignes : carte `ConfirmBooking`, `CancelBooking`, `GetBooking` et contrat public du module, ajout d'une slice, vérification avec Docker, règles HS001, HS012 à HS014 et HS016 à HS018 et leurs fiches.
- `scripts/test-template.sh` exécute le parcours documenté sur la solution générée : restauration des outils, application des migrations sur un conteneur PostgreSQL, démarrage de l'API, création puis lecture d'une réservation.
- README de la démonstration PB-008 exact pour le template v0.1.
- CI : jobs build, template et démonstration, chacun depuis un checkout neuf, sans cache ni état hérité d'un autre job.

**Livraison**

Le `README.md` décrit le parcours d'un nouvel utilisateur en cinq étapes : construction des deux paquets dans `artifacts/packages` et installation du template, `nuget.config` dont la correspondance de sources réserve les paquets `HardSlice.*` au dossier local, génération avec ou sans exemple, vérification par les deux scripts et démarrage de l'API (PostgreSQL dans un conteneur publié sur 127.0.0.1 seulement, attente bornée à 60 secondes de `pg_isready` en TCP dans le conteneur, `dotnet tool restore`, `dotnet ef database update`, `dotnet run` en Development, réservation créée puis relue avec `curl`). Le paquet garde la version fixe 0.1.0 : le README indique comment réinstaller le template et vider le dossier global des paquets NuGet après une reconstruction.

`scripts/generate-app.sh` exécute les étapes 1 et 2 avec les mêmes commandes et le même `nuget.config`, isolés dans le dossier de travail (flux, ruche de templates, dossier des paquets). `scripts/test-template.sh` exécute les étapes 3 et 4 sur les deux variantes : un seul conteneur `postgres:18-alpine` sur un port libre de 127.0.0.1, attendu par la même commande que le README, la chaîne de connexion générée avec ce port, l'API sur un port libre. Avec l'exemple, la réservation du README est créée (201 et `Location`), relue (200) puis refusée en double (409). Sans exemple, l'API démarre sur sa base créée par `dotnet ef database update` et répond 404. Le script vérifie aussi que la solution générée par défaut contient les livrables v0.1 de la spécification, contrainte d'exclusion PostgreSQL comprise.

`ReadmeTests` vérifie que les liens relatifs du README existent et que sa table des règles cite exactement les fiches livrées par le template. `AGENTS.md` nomme `BookingsModule` comme contrat public du module, conformément à HS012 et HS016, et reçoit une section « Running the API ». Il compte 118 lignes générées avec l'exemple et 125 sans. Le README de la démonstration PB-008 précise ce qui distingue la trace enregistrée du template v0.1. La CI déclare l'absence de cache (`cache: false`) et ne conserve pas les identifiants du checkout.

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

- Publication NuGet en préversion (v0.2) ou stable (v1.0), site de documentation (v1.0).
- `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, skills et hooks (v0.3).
- Azure Pipelines (v1.0).
- Rendre le dépôt public, qui reste une action du mainteneur.
- Nouveau run d'agent enregistré pour la démonstration PB-008.

### Définition de fini

L'incrément est terminé lorsque PB-009 à PB-017 sont satisfaits depuis un checkout propre et que les jobs CI build, template et démonstration sont verts sur `main`. Concrètement :

- La solution du dépôt passe `dotnet restore`, `dotnet build --no-restore` et `dotnet test --no-build`, tests d'intégration compris.
- `scripts/test-template.sh` génère depuis le paquet local la variante par défaut et la variante `--no-sample`, les vérifie avec `scripts/check.sh` et `scripts/check.ps1`, exécute le parcours documenté, détecte l'absence de Docker, et prouve que HS001, HS012, HS013, HS014, HS016, HS017 et HS018 échouent sur leur sonde avec une fiche existante.
- `scripts/demo-hs016-agent-fix.sh prepare` reproduit le diagnostic HS016 attendu sur le template v0.1.
- La solution générée par défaut contient les livrables v0.1 de la spécification : module Bookings avec agrégat `Booking`, objet valeur `BookingPeriod`, événement `BookingConfirmed`, invariant de chevauchement garanti par PostgreSQL, slices `ConfirmBooking`, `CancelBooking` et `GetBooking`, tests unitaires, d'intégration et d'architecture, `hardslice.json`, `.editorconfig`, `AGENTS.md`, `scripts/check.sh` et `scripts/check.ps1`.

Aucune porte documentaire supplémentaire ne s'applique.

## 4. Incrément 3 terminé : Analyseurs v0.2

Statut : PB-018 à PB-032 terminés le 02/11/2026. PB-033 n'est pas publié seul : sa publication est reprise par PB-051, qui publie une seule préversion, `0.3.0-preview.1`.

### Résultat attendu

> Dans une solution générée par `dotnet new hardslice-api`, avec ou sans Bookings, `dotnet build` arrête chaque dérive principale par un diagnostic HS qui porte son propre identifiant : accès direct à la base depuis un endpoint, slice mal formée, handler de commande qui renvoie une entité, handler de requête qui suit ou écrit, état d'entité modifiable de l'extérieur, référence directe entre agrégats, événement qui n'est pas un `record`, lecture de l'horloge système, appel bloquant ou `async void`, endpoint hors de sa slice. Chaque règle a sa fiche, ses tests et sa place dans un niveau d'analyse. `dotnet hardslice check` et `dotnet hardslice explain` sont livrés par un outil local épinglé à la version des analyseurs. Les paquets `0.2.0-preview.1` sont publiés sur NuGet.org.

### Arbitrages de l'incrément

Décisions du mainteneur du 30/10/2026, reportées dans la [spécification](specifications.md) et le [plan](plan.md) :

- **Classification par contrats applicatifs.** Le Domain généré déclare `IEntity` et `IAggregateRoot : IEntity`, à côté d'`IDomainEvent`. Ces interfaces appartiennent à l'application : aucun paquet HardSlice n'est requis au runtime. Les analyseurs reconnaissent les contrats par leur nom de métadonnée et leur assembly déclarant, jamais par leur nom simple. Une propriété `Id` ne classe jamais un type.
- **HS010 et HS011 sont des analyseurs HardSlice ciblés.** `BannedApiAnalyzers` n'est pas utilisé : il produirait `RS0030` pour les deux règles, qu'on ne pourrait alors ni configurer, ni supprimer, ni compter séparément. HardSlice n'ajoute aucun autre analyseur qui signalerait les mêmes appels.
- **HS009 vérifie la structure, HS019 le nommage.** `record` et `IDomainEvent` sont vérifiés de façon fiable par HS009. Le temps passé du nom est une convention : HS019 la signale comme suggestion, jamais comme erreur.
- **Diagnostics de configuration HS900 à HS902.** Ils signalent une classification impossible, au lieu de laisser une règle se taire.
- **Niveaux d'analyse.** `Minimum`, `Recommended` et `All` activent ou désactivent des règles. Ils ne changent jamais la sévérité d'une règle active.
- **Messages.** Pas de `CodeFixProvider` en v0.2. Pas de référence `Skill:` avant la livraison de la skill en v0.3.
- **Outil.** `dotnet hardslice check` porte la vérification. `scripts/check.sh` et `scripts/check.ps1` deviennent des lanceurs minces. Les diagnostics restent produits par `dotnet build` seul.

### Contrats communs aux analyseurs

Ces contrats s'appliquent à tous les tickets de règle. Le premier ticket intégré qui en a besoin les met en place. Les suivants les réutilisent.

**Cartographie des slices et classification des types sont séparées.** `SliceMap` reste fondé sur les espaces de noms `<Assembly>.Features.<Module>.<Slice>` de la compilation analysée. La classification (`DomainModel`, PB-018) travaille sur les symboles, y compris ceux des assemblies référencés : un handler de `MyApp` reconnaît `Booking` de `MyApp.Domain` compilé à part. Aucun analyseur ne charge la solution entière.

**Où chaque règle s'exécute.** Une règle sur la déclaration d'un type (HS007, HS008, HS009, HS019) s'exécute dans la compilation qui déclare ce type, donc HS007 dans celle du Domain. Les autres règles (HS003 à HS006, HS010, HS011, HS015, HS902) s'exécutent dans la compilation du code examiné et lisent les symboles référencés.

**Projets de tests.** Les règles HS003 à HS011, HS015, HS019 et HS900 à HS902 ne s'appliquent pas aux projets dont `IsTestProject` vaut `true`. HS016 garde son comportement actuel.

**Lecture de la configuration.** Le paquet `HardSlice.Analyzers` livre `build/HardSlice.Analyzers.props` et `build/HardSlice.Analyzers.targets`, importés par la `GlobalPackageReference` du template (actifs `Build` inclus). Ils ajoutent le `hardslice.json` le plus proche au-dessus du projet comme `AdditionalFiles` et exposent `IsTestProject`, `MSBuildProjectDirectory` et `HardSliceAnalysisMode` par `CompilerVisibleProperty`. La lecture de `hardslice.json` doit fonctionner dans tous les hôtes du compilateur : un analyseur `netstandard2.0` ne peut pas compter sur le chargement de `System.Text.Json` dans chacun d'eux. Le moyen de lecture est choisi en proportion du format attendu, petit et fermé, sans imposer un parseur JSON généraliste. Une entrée hors de ce format est rejetée par HS900, qui nomme la clé en cause. Les tests couvrent le fichier absent, le JSON invalide, la clé inconnue et la valeur mal formée.

**Format des messages.** Une ligne en anglais : `HSxxx: <symbole fautif et sa position architecturale>. <règle en une phrase>. Fix: <correction>. Skill: <skill>. Doc: 'docs/rules/HSxxx.md'.`, comme HS016 (`Skill:` depuis PB-044) : le chemin entre apostrophes et le point final satisfont RS1032, qui exige qu'un message de plusieurs phrases se termine par un point (PB-018). La fiche contient : pourquoi, mauvais exemple, bon exemple, contournements interdits et ce que la règle ne détecte pas.

**Livrables de chaque ticket de règle**, dans le même changement :

- Descripteur dans `AnalyzerReleases.Unshipped.md`.
- Fiche `docs/rules/HSxxx.md` dans le template, ligne de la table des règles du `README.md` (vérifiée par `ReadmeTests`) et de `AGENTS.md`, qui reste sous 150 lignes.
- Tests dans `HardSlice.Analyzers.Tests` : cas conformes, cas fautifs, cas génériques et, lorsque la règle lit un symbole d'un autre projet, montage application + Domain réellement séparé (Domain compilé à part et référencé en métadonnées).
- `BookingsReferenceTests` vert : la référence ne produit aucun diagnostic HS.
- `scripts/test-template.sh` : une sonde par règle, dans la variante Bookings et dans la variante `--no-sample` dès que la règle y a une cible. Les sondes existantes restent. La sonde d'une règle bloquante fait échouer `check.sh` et `check.ps1` avec le message attendu et une fiche existante. La sonde de HS019, non bloquante, vérifie qu'avec la configuration livrée le build et les deux scripts réussissent et que la suggestion HS019 figure dans le journal SARIF du compilateur (`ErrorLog`).
- Ligne de la règle dans la matrice des niveaux d'analyse si PB-020 est intégré, sinon PB-020 l'ajoute.
- Mesure de performance enregistrée dès que PB-019 est intégré, seuils respectés.

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

L'assembly déclarant est nécessaire : il distingue un contrat absent d'un contrat simplement invisible. La compilation de `MyApp.Domain` ne référence pas `MyApp` et ne voit donc pas `ICommand<T>`. Ce n'est pas une erreur. Si la compilation de `MyApp` ne le trouve plus, c'en est une. Un contrat est **requis** dans toute compilation qui est son assembly déclarant ou le référence. Les types de bibliothèque utilisés par les règles (`IValidator<T>` de FluentValidation, `DbContext` et `DbSet<T>` d'EF Core, `IResult` et `HttpResults` d'ASP.NET Core) sont reconnus par leur symbole et ne sont pas configurables.

La configurabilité générale des conventions reste en v0.5 : ce fichier déclare des contrats, il ne permet pas encore de renommer `Features` ni de décrire une architecture en couches.

### Prérequis de la préversion NuGet

Identifiés dès le démarrage de l'incrément. Le développement et la validation locale des paquets n'en dépendent pas : seul PB-033 les exige.

- Compte NuGet.org du mainteneur, disponibilité vérifiée des identifiants `HardSlice.Analyzers`, `HardSlice.Templates` et `HardSlice.Tool`, réservation du préfixe `HardSlice.` demandée.
- Publication de confiance NuGet.org (OIDC GitHub) configurée pour le dépôt, ou à défaut clé d'API limitée au push de `HardSlice.*`, stockée dans un environnement GitHub protégé `nuget` avec approbation du mainteneur.
- Dépôt GitHub public avant toute publication sur NuGet.org : les liens des paquets vers le code, les fiches et la documentation doivent être utilisables. Ce prérequis ne bloque ni la construction ni la validation des paquets sur la source locale (PB-032).
- Version `0.2.0-preview.1`, remplacée le 03/11/2026 par `0.3.0-preview.1`, la seule préversion publiée (PB-051).

### Graphe des dépendances

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-018 | incrément 2 | PB-019 à PB-024, PB-032 |
| PB-019 | incrément 2 | tous (mesure ensuite chaque ticket de règle) |
| PB-020 | incrément 2 | tous (point de fusion avec chaque ticket de règle) |
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

- `src/MyApp.Domain/IEntity.cs` et `IAggregateRoot.cs` dans les deux variantes du template, documentés dans `AGENTS.md`.
- `Booking : IAggregateRoot`.
- Section `contracts` de `hardslice.json`. `HardSliceConfiguration` des tests d'architecture l'accepte et valide sa forme.
- Props et targets du paquet décrits dans les contrats communs.
- `DomainModel` : résolution des contrats dans la compilation et ses références, implémentation directe ou héritée (classe de base, interface dérivée), types génériques construits ramenés à leur définition.
- Trois diagnostics de configuration et leurs fiches.

Un type est une **entité** s'il implémente le contrat `entity`, une **racine d'agrégat** s'il implémente `aggregateRoot`, un **événement de domaine** s'il implémente `domainEvent`, une **commande** ou une **requête** s'il implémente `command` ou `query`, un **handler** s'il implémente `requestHandler`. Ces catégories ne reposent sur aucune autre heuristique.

**Diagnostics de configuration**

HS900, configuration inutilisable. Signalé une fois par compilation, uniquement si une règle active a besoin de l'élément en cause :

- `hardslice.json` introuvable ou invalide.
- Clé de `contracts` absente ou mal formée.
- Contrat requis (au sens ci-dessus) introuvable dans son assembly déclarant.
- Incohérence entre contrats : `aggregateRoot` ne dérive pas de `entity`, deux clés désignent le même type.

HS901, classification ambiguë, signalé sur la déclaration du type :

- Type à la fois entité et événement de domaine.
- Type à la fois commande et requête.
- Type à la fois entité et requête ou commande.

HS902, type soumis à la classification mais non classifiable. Un type est soumis à la classification lorsqu'il est déclaré dans l'assembly du contrat `entity` et qu'il est mappé comme entité EF Core par l'une de ces formes :

- Argument de type d'une implémentation d'`IEntityTypeConfiguration<T>`.
- `T` d'une propriété `DbSet<T>` d'un type dérivé de `DbContext`.
- Appel générique `ModelBuilder.Entity<T>()`.

S'il n'implémente pas `entity`, HS902 est signalé sur la forme de mapping, dans la compilation qui la contient.

**Ce qui reste indétectable**, documenté dans les fiches HS902 et HS007 :

- Entité mappée par convention (découverte par navigation), par `Entity(typeof(T))` ou par nom.
- Type possédé (`OwnsOne`, `OwnsMany`) et type complexe (`ComplexProperty`) : HS902 ne les soumet pas à la classification. Un type possédé peut être un objet valeur ou une entité enfant. HardSlice ne le déduit pas du mapping, et seuls `IEntity` et `IAggregateRoot` font autorité. Une entité enfant possédée qui n'implémente pas `IEntity` échappe donc à HS005, HS007 et HS008 sans diagnostic.
- Entité non persistée qui n'implémente aucun contrat.
- Mapping dans un projet qui n'utilise pas les analyseurs HardSlice.

**Dépendances**

Incrément 2.

**Cas conformes**

- Bookings généré : `Booking : IAggregateRoot`, `BookingPeriod` en type complexe, aucun diagnostic.
- `BookingLine : IEntity` mappé par `OwnsMany(booking => booking.Lines)` : aucun HS902, et l'entité reste couverte par HS005, HS007 et HS008 grâce à son contrat.
- Variante `--no-sample` : contrats présents, aucune entité, aucun diagnostic.
- `MyApp.Domain` compilé seul : les contrats de messagerie, invisibles, ne sont pas requis.

**Cas fautifs**

- `IEntity` supprimé : HS900 dans `MyApp.Domain` et dans `MyApp`, puisque HS901 et HS902 en dépendent.
- `contracts.command` renommé : HS900 dans `MyApp` seulement.
- `Room` configuré par `RoomConfiguration : IEntityTypeConfiguration<Room>` sans `IEntity` : HS902.
- Type qui implémente `IAggregateRoot` et `IDomainEvent` : HS901.

**Génériques et inter-assemblies**

- Contrat implémenté par une classe de base générique `Entity<TId> : IEntity`.
- Interface applicative dérivée `IAuditedEntity : IEntity`.
- Domain référencé en métadonnées et en référence de projet.
- Type homonyme `IEntity` d'un autre espace de noms ou d'un autre assembly : ignoré.

**Sévérité et diagnostic**

HS900, HS901 et HS902 en `error`, dans tous les niveaux d'analyse, lorsqu'une règle active en dépend.

```text
HS902: 'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'. HardSlice classifies domain types only through these contracts. Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: 'docs/rules/HS902.md'.
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-019 : Mesurer le coût des analyseurs sur un build incrémental réel

**Résultat observable**

`scripts/measure-analyzers.sh` produit un rapport de durées avec et sans analyseurs HardSlice sur une fixture documentée. Le job CI `performance` échoue si un seuil est dépassé.

**Périmètre**

- Fixture générée depuis le paquet local : application `PerfApp` avec Bookings et 20 modules synthétiques de 10 slices chacun, agrégats compris, produits par un script déterministe.
- Variante sans analyseurs : copie de la fixture dont un `Directory.Build.targets` retire les éléments `Analyzer` du paquet `HardSlice.Analyzers`. Aucune propriété de désactivation n'est ajoutée au paquet.
- Scénarios, chacun avec et sans analyseurs :
  - A : build incrémental après modification du corps d'une méthode d'une slice.
  - B : build incrémental après modification de l'API publique d'un agrégat du Domain, qui recompile les projets dépendants.
  - C : build complet `--no-incremental`, comme référence.
- Un build sans changement n'est pas mesuré : il peut ne rien analyser.
- Une exécution de chauffe, puis cinq exécutions mesurées, médiane retenue, serveur de compilation actif.
- `-p:ReportAnalyzer=true` en complément, pour expliquer le coût de chaque analyseur, mais seuls les seuils sur la durée totale font foi.
- Rapport `docs/performance/analyzers.md` : SDK effectif (`dotnet --version`), runner, nombre de cœurs, mémoire, fixture, médianes.
- Job CI `performance` sur `ubuntu-latest`, sur `main` et sur les pull requests qui touchent les analyseurs ou le template.

**Seuils initiaux**, sur la médiane, pour la fixture, le SDK épinglé par `global.json` et le runner documentés :

| Scénario | Durée totale avec analyseurs | Surcoût des analyseurs HardSlice |
|---|---:|---:|
| A | ≤ 10 s, budget du build après édition de la spécification | ≤ 1 s |
| B | ≤ 20 s | ≤ 2 s |
| C | mesuré, sans seuil | ≤ 15 % |

Un seuil ne change que par une décision du mainteneur consignée dans le rapport, jamais automatiquement.

**Dépassement d'un seuil**

Le rapport donne, pour chaque scénario et chaque variante, le minimum, la médiane et le maximum des cinq mesures. Un dépassement est d'abord qualifié :

- **variabilité de l'environnement** : la dispersion d'une série (maximum moins minimum, rapporté à la médiane) dépasse 20 %. La série est instable : le job est relancé sur un nouveau runner, sans conclusion.
- **coût du build sans HardSlice** : la variante sans analyseurs dépasse déjà le budget de durée totale. Le dépassement ne vient pas des analyseurs : il est signalé dans le rapport et l'arbitrage porte sur la fixture, le runner ou le budget.
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

- Un fichier `.globalconfig` par niveau, livré dans le paquet et ajouté par `build/HardSlice.Analyzers.targets` aux `GlobalAnalyzerConfigFiles`.
- Valeur absente : `Recommended`. Valeur inconnue : erreur MSBuild qui liste les valeurs admises, jamais un repli silencieux.
- Les sévérités par défaut des descripteurs correspondent à `Recommended`.
- Un niveau active ou désactive une règle. Il ne change jamais sa sévérité. Aucun niveau ne désactive tout.
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

`Minimum` réunit les règles qui ne supposent ni la forme d'une slice ni la messagerie du template, plus HS016. Le template utilise `TreatWarningsAsErrors=true` : un `warning` y bloque. HS019, heuristique, est donc en `suggestion`, visible dans l'IDE et sans effet sur le build. Aucun niveau ne la rend bloquante.

**Dépendances**

Incrément 2. Point de fusion avec chaque ticket de règle : le ticket intégré en second ajoute la ligne manquante.

**Cas vérifiés**

- Chaque niveau active exactement les règles de sa ligne, vérifié par un test qui compile une sonde par règle.
- `dotnet_diagnostic.HS004.severity = none` dans `.editorconfig` désactive HS004 sous `All`.
- `dotnet_diagnostic.HS004.severity = error` dans `.editorconfig` active HS004 sous `Minimum`.
- `#pragma warning disable` et `[SuppressMessage]` agissent sur une seule règle HS.
- Valeur `Strict` : le build échoue avec la liste des niveaux.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-021 : Livrer `dotnet hardslice check` et `explain` dans un outil local épinglé

**Résultat observable**

Dans une solution générée, `dotnet tool restore` installe `HardSlice.Tool` à la même version que `HardSlice.Analyzers`. `dotnet hardslice check` exécute la vérification complète. `dotnet hardslice explain HS016` affiche la fiche.

**Périmètre**

- Projet `src/HardSlice.Tool`, outil .NET `net10.0`, commande `dotnet-hardslice`, appelée `dotnet hardslice`.
- `.config/dotnet-tools.json` du template : entrée `hardslice` à la version de `HardSlice.Analyzers`, `rollForward: false`. `dotnet-ef` garde sa version, alignée sur EF Core (10.0.14 aujourd'hui).
- `check` reprend tout le contrat actuel des scripts, dans le même ordre et avec les mêmes messages : build de la solution (restauration comprise), tests unitaires, tests d'architecture, détection d'un runtime de conteneurs compatible Docker avec le message actuel, tests d'intégration. Il propage le code de sortie de la première étape en échec et rend `1` sans Docker.
- `check` échoue avec un message explicite si la version de `HardSlice.Analyzers` déclarée dans `Directory.Packages.props` diffère de la sienne.
- `check` ne réinterprète pas la sortie du compilateur : les diagnostics restent ceux de `dotnet build`.
- `scripts/check.sh` et `scripts/check.ps1` deviennent des lanceurs minces : `dotnet tool restore`, puis `dotnet hardslice check`, code de sortie propagé.
- `explain <id>` affiche la fiche embarquée et rend `0`. Un identifiant inconnu rend `2` et liste les fiches disponibles.
- Source unique des fiches : `src/HardSlice.Templates/content/hardslice-api/docs/rules/`, embarquée dans l'outil au build et livrée par le template.
- `README.md` et `AGENTS.md` décrivent `check` et `explain`.

**Limites**

Pas de `init`, `map`, `baseline`, `report` ni `verify` (v0.5). Pas d'option qui saute une étape de la vérification.

**Dépendances**

Incrément 2.

**Cas vérifiés**

- Les sondes existantes de `scripts/test-template.sh` échouent toujours par les deux scripts, avec les mêmes messages.
- `DOCKER_HOST` vers un socket absent : message actuel et code `1`.
- Test de l'outil : le texte de chaque fiche embarquée est identique au fichier livré par le template. L'ensemble des fiches est égal à l'ensemble des règles livrées.
- Version de l'outil, de l'entrée `hardslice` du manifeste et de la `GlobalPackageReference` identiques. Une version différente fait échouer `check`. La version de `dotnet-ef` n'est pas concernée.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-022 : HS003, un endpoint n'accède jamais directement à la base

**Résultat observable**

Un endpoint qui reçoit, injecte ou obtient un `DbContext` fait échouer le build avec HS003.

**Périmètre**

- Endpoint : délégué passé à une méthode de mapping d'ASP.NET Core reconnue par son symbole (`MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`, `MapMethods`, `Map` d'`EndpointRouteBuilderExtensions`, `MapFallback`), sur un `IEndpointRouteBuilder` ou un `RouteGroupBuilder`. Les noms `Map*` et `*Endpoint` ne suffisent jamais.
- Formes du délégué : lambda, méthode anonyme, groupe de méthodes statique ou d'instance déclaré dans la compilation.
- Accès à la base : dans le corps du délégué, lambdas et fonctions locales comprises, tout paramètre, variable locale, champ, propriété ou résultat d'appel dont le type est ou dérive de `DbContext`, `DbSet<T>`, `IDbContextFactory<T>`, `DatabaseFacade`, `DbConnection`, `IDbConnection` ou `DbDataSource`.
- S'applique partout dans la compilation, slices, module et `Program.cs` compris.

**Limites**

Les méthodes appelées par l'endpoint ne sont pas suivies. Un délégué venu d'une variable, d'un champ ou d'un assembly en métadonnées n'est pas analysable et ne produit aucun diagnostic. Les contrôleurs MVC ne sont pas couverts.

**Dépendances**

Incrément 2.

**Cas conformes**

Endpoints de Bookings qui reçoivent `IMediator` et endpoint qui n'utilise que `HttpContext`.

**Cas fautifs**

- Paramètre `AppDbContext db` d'une lambda.
- Groupe de méthodes `MapGet("/", List)` dont la méthode reçoit `AppDbContext`.
- Champ `DbContext` d'une classe dont une méthode d'instance est passée à `MapPost`.
- `context.RequestServices.GetRequiredService<AppDbContext>()`.
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

- Emplacement : l'appel est dans une slice. Un appel dans le type du module ou à la racine de `Features` est fautif.
- Groupe : le récepteur de l'appel est un `RouteGroupBuilder`. Un récepteur `IEndpointRouteBuilder` ou `WebApplication` est fautif.
- Enregistrement : la méthode de la slice qui contient l'appel est invoquée par le type `<Module>Module` de son module. Vérifié en fin de compilation.
- Résultat : le type de retour du délégué, après `Task<T>` ou `ValueTask<T>`, est un type de l'espace de noms `Microsoft.AspNetCore.Http.HttpResults` (`Ok<T>`, `Created`, `NotFound`, `Results<T1, T2>`…). `IResult`, un DTO, `object`, `void`, `Task` et `ValueTask` sont fautifs.

**Limites**

Les endpoints hors de `Features`, comme un endpoint technique de `Program.cs`, ne sont pas des endpoints de slice : HS015 ne les vérifie pas, HS003 si. Un type de résultat personnalisé qui implémente `IResult` est fautif en v0.2. Dans l'IDE, l'enregistrement par le module n'est signalé qu'en analyse complète de la solution.

**Dépendances**

Incrément 2.

**Cas conformes**

Les trois endpoints de Bookings, `Task<Results<Ok<BookingDto>, NotFound>>` et groupe imbriqué `group.MapGroup("/{id}")`.

**Cas fautifs**

`app.MapPost` dans une slice, mapping dans `BookingsModule`, `Results.Ok(dto)` (type `IResult`), lambda qui renvoie le DTO et `ArchiveBookingEndpoint.Map` jamais appelé par `BookingsModule`.

**Génériques et inter-assemblies**

`Results<Created<T>, Conflict>` et groupe de méthodes générique. Module et slices sont dans la même compilation : pas de cas inter-assemblies.

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

- Propriété `Result` de `Task<TResult>` et de `ValueTask<TResult>`.
- Méthodes `Wait` de `Task`, `Task.WaitAll` et `Task.WaitAny`, toutes surcharges.
- Appel explicite de `GetResult()` sur `TaskAwaiter`, `TaskAwaiter<TResult>`, `ValueTaskAwaiter<TResult>` et leurs variantes configurées.

Formes d'`async void` :

- Méthode, fonction locale, implémentation d'interface ou substitution déclarée `async` qui renvoie `void`.
- Lambda ou méthode anonyme `async` convertie en un délégué dont `Invoke` renvoie `void` (`Action`, `Action<T>`, `EventHandler`, délégué personnalisé), par exemple `list.ForEach(async item => …)`.

S'applique à tous les projets de production.

**Limites**

`await` n'est pas un appel bloquant. Un membre nommé `Result` ou `Wait` d'un autre type n'est pas concerné. `.Result` sur une tâche déjà attendue est signalé quand même : la fiche indique d'utiliser `await` une seconde fois.

**Dépendances**

Incrément 2.

**Cas conformes**

`await task`. `Task.Run(async () => …)`, converti en `Func<Task>`. Propriété `Result` d'un type `OperationResult` de l'application. Méthode d'extension `Wait` d'une bibliothèque tierce.

**Cas fautifs**

Les trois appels bloquants, sur `Task<List<T>>` et `ValueTask<T>`. `async void` sur une méthode, une fonction locale et une lambda passée à `List<T>.ForEach`.

**Génériques et inter-assemblies**

Tâches génériques imbriquées. `async void` dans le Domain, signalé dans sa propre compilation.

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

- Symboles : `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now` et `DateTimeOffset.UtcNow`.
- Périmètre annoncé par la spécification, sans élargissement : code de l'assembly déclarant du contrat `entity`, et code des slices de la compilation analysée (`SliceMap`).
- Lectures dans un arbre d'expression (filtre EF Core) comprises : la fiche indique de passer l'instant en paramètre.
- `nameof` n'est pas une lecture.

**Limites**

Le code du module (`BookingsModule`, configurations EF Core), `Shared`, `Program.cs` et les tests ne sont pas couverts. Une lecture déplacée dans un utilitaire de `Shared` appelé par une slice n'est pas détectée : la fiche la cite comme contournement interdit. `TimeProvider.System` n'est pas signalé.

**Dépendances**

PB-018, pour reconnaître l'assembly du Domain.

**Cas conformes**

`TimeProvider` injecté et `GetUtcNow()`. Instant reçu en paramètre par le domaine. `DateTime.UtcNow` dans `Shared/Behaviors`.

**Cas fautifs**

`DateTimeOffset.UtcNow` dans `Booking`, dans `ConfirmBookingHandler` et dans un filtre `Where`. `using static System.DateTime;` puis `Now`.

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

- Exactement une requête : `<Slice>Command` qui implémente `command`, ou `<Slice>Query` qui implémente `query`.
- Son handler, nommé `<Slice>Handler`, dans la slice.
- Son endpoint, type statique `<Slice>Endpoint`, dans la slice.
- Un validateur de la requête, s'il existe (`IValidator<T>` de FluentValidation), est nommé `<Slice>Validator` et déclaré dans la slice. Son absence relève de HS013.
- Une requête, un handler ou un validateur hors de toute slice (racine du module, `Shared`, ailleurs) est fautif.
- Emplacement : chaque déclaration d'un type de la slice est dans un fichier sous `<projet>/Features/<Module>/<Slice>/`, et chaque type déclaré dans ce dossier appartient à l'espace de noms de la slice. Les types partiels sont vérifiés déclaration par déclaration. Le code généré est exclu.

**Limites**

Le nom et le nombre des fichiers ne sont pas contrôlés (question ouverte n° 5 de la spécification). L'obligation d'un endpoint vaut pour toutes les slices de v0.2. Elle sera revue en v0.3 pour les slices déclenchées par un événement. Un handler placé dans une autre slice relève de HS016, qui le signale déjà : HS004 ne le signale pas une seconde fois.

**Dépendances**

PB-018, pour les contrats de messagerie.

**Cas conformes**

Les trois slices de Bookings, dont `GetBooking` sans validateur et avec un DTO. Variante `--no-sample` sans slice.

**Cas fautifs**

Slice sans requête, `ArchiveCommand` dans `ArchiveBooking`, deux requêtes dans une slice, handler `ArchiveHandler`, endpoint absent, validateur `ArchiveRules`, validateur à la racine du module et fichier `Features/Bookings/ConfirmBooking.cs` avec l'espace de noms de la slice.

**Génériques et inter-assemblies**

Commande générique `ArchiveBookingCommand<T>`, nommée sans son arité. Handler qui hérite d'une base générique. Contrats de messagerie résolus en métadonnées.

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

- Handler de commande : implémentation, directe ou héritée, de `requestHandler` pour une requête qui implémente `command`. La réponse examinée est son argument `TResponse`.
- La réponse contient une entité si l'on atteint un type qui implémente `entity`, racine ou entité enfant, en parcourant :
  - le type lui-même et `Nullable<T>`.
  - les éléments de tableau.
  - les arguments de tout type générique construit (`List<T>`, `IReadOnlyList<T>`, `Dictionary<TKey, TValue>`, `Result<T>`…).
  - les éléments de tuple.
- **DTO contenant une entité** : parcours distinct des propriétés et champs publics d'instance des types déclarés dans l'assembly du Domain, dans la compilation analysée ou dans un assembly qui référence le Domain. Ce sont les seuls où un DTO peut contenir une entité. Les types du framework et des bibliothèques ne sont pas parcourus. Le parcours est récursif et protégé contre les cycles. Le message nomme le chemin jusqu'à l'entité.

**Limites**

Une entité typée `object`, `dynamic` ou rangée dans une collection non générique, un sous-type connu seulement à l'exécution, ou un membre non public ne sont pas détectés. Un handler générique dont `TResponse` est un paramètre de type sans contrainte n'est pas analysable. Contraint à `IEntity`, il est fautif.

**Dépendances**

PB-018.

**Cas conformes**

`BookingId` (objet valeur du Domain, parcouru : il ne contient qu'un `Guid`), `bool` de `CancelBooking`, DTO de scalaires et `Unit`.

**Cas fautifs**

`Booking`, `List<Booking>`, `Booking[]`, `(BookingId, Booking)`, `Result<Booking>`, entité enfant `BookingLine`, DTO `ConfirmationDto` dont la propriété `Booking` est l'entité et DTO dont une propriété est `IReadOnlyList<BookingLine>`.

**Génériques et inter-assemblies**

Domain compilé à part et référencé en métadonnées. DTO déclaré dans un projet qui référence le Domain. Enveloppe générique déclarée dans `Shared`.

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

Un handler de requête implémente `requestHandler` pour une requête qui implémente `query`. Tout son code est examiné : méthodes, lambdas, fonctions locales et initialiseurs. HS006 exige une **garantie locale** d'absence de suivi, visible dans le handler. Le réglage global `UseQueryTrackingBehavior` n'est pas analysé : il vit dans la racine de composition et peut changer sans que le handler change. L'absence d'`AsNoTracking` local ne prouve donc pas que la lecture est suivie. Elle signifie que le handler ne garantit pas qu'elle ne l'est pas, et c'est ce que HS006 signale.

**Fautif** :

1. **Écriture EF Core directe** : appel de `SaveChanges`, `SaveChangesAsync`, `ExecuteUpdate`, `ExecuteUpdateAsync`, `ExecuteDelete`, `ExecuteDeleteAsync` (`EntityFrameworkQueryableExtensions`), `ExecuteSql`, `ExecuteSqlAsync`, `ExecuteSqlRaw`, `ExecuteSqlRawAsync`, `ExecuteSqlInterpolated`, `ExecuteSqlInterpolatedAsync` (`RelationalDatabaseFacadeExtensions`), ou modification du suivi par `Add`, `AddAsync`, `AddRange`, `AddRangeAsync`, `Attach`, `AttachRange`, `Update`, `UpdateRange`, `Remove` et `RemoveRange` de `DbContext` ou `DbSet<T>`.
2. **Suivi réactivé ou reconfiguré** : appel d'`AsTracking`, ou affectation de `ChangeTracker.QueryTrackingBehavior`, quelle que soit la valeur. C'est une convention du handler : il ne modifie pas la configuration du contexte qu'il reçoit, car ce contexte peut servir à d'autres composants de la même portée (c'est le cas du `DbContext` à portée de requête du template), ce que le handler n'a pas à connaître.
3. **Lecture qui suit toujours** : `Find` et `FindAsync`.
4. **Lecture sans garantie locale** : une requête reconnue, enracinée dans un ensemble d'entités, dont le résultat contient une entité, et dont la chaîne garantie ne contient ni `AsNoTracking` ni `AsNoTrackingWithIdentityResolution`.

**Requête reconnue** :

- Racine : propriété `DbSet<T>`, `Set<T>()`, `FromSql`, `FromSqlRaw` ou `FromSqlInterpolated`, avec `T` entité au sens de PB-018.
- Chaîne d'opérateurs LINQ et EF Core dans une même expression.
- Variable locale `IQueryable<T>` de la même méthode, initialisée par une requête reconnue et réaffectée seulement sous la forme `query = query.<opérateurs>`, que la réaffectation soit conditionnelle ou non. Toute autre réaffectation rend la requête non analysable.
- **chaîne garantie** : l'expression elle-même, ou pour une variable locale son initialiseur. Les réaffectations sont supposées pouvoir ne pas s'exécuter, faute d'analyse du flot de contrôle : un `AsNoTracking` ajouté par une réaffectation ne garantit rien, alors qu'un `AsTracking` ajouté par une réaffectation reste fautif.
- Exécution : opérateur terminal synchrone ou asynchrone (`ToList`, `ToArray`, `ToDictionary`, `First`, `Single`, `Last`, `ElementAt` et leurs variantes, `Load`, `AsAsyncEnumerable`), ou `foreach` sur la requête.

**Le résultat contient une entité** lorsque le type de l'élément final contient une entité au sens de PB-027, ou lorsque la dernière projection fait passer une expression de type entité dans le résultat : membre d'un type anonyme, argument d'un constructeur ou d'un initialiseur de DTO, argument d'une méthode évaluée côté client.

**Conforme** :

- Requête reconnue avec `AsNoTracking` ou `AsNoTrackingWithIdentityResolution` dans sa chaîne garantie, sans `AsTracking`.
- Requête reconnue dont le résultat ne contient aucune entité, par exemple `Select(b => b.Id)` ou un DTO de scalaires : EF Core ne suit rien.

**Non analysable** : HS006 ne produit aucun diagnostic et ne déclare pas le code conforme. Un utilitaire peut parfaitement respecter la règle sans que HS006 puisse le vérifier :

- Requête obtenue d'un champ, d'une propriété, d'un paramètre ou d'une méthode utilitaire.
- Requête ou contexte passé à une méthode qui n'est pas un opérateur LINQ ou EF Core : la méthode n'est pas suivie.
- Variable locale réaffectée autrement que par `query = query.<opérateurs>`.
- Requête compilée (`EF.CompileQuery`, `EF.CompileAsyncQuery`).
- Écriture faite dans un utilitaire, par ADO.NET ou par une autre bibliothèque.

**Limites**

Pas d'analyse interprocédurale ni de flot de contrôle général en v0.2. La fiche HS006 sépare deux listes : les formes que la règle ne sait pas analyser, permises lorsqu'elles respectent le contrat, et les **contournements interdits**, c'est-à-dire déplacer une écriture ou une lecture sans garantie locale dans un utilitaire, ou passer par une autre bibliothèque, pour échapper au diagnostic.

**Dépendances**

PB-018.

**Cas conformes**

`GetBookingHandler` actuel, `Select(b => b.Id).ToListAsync()` sans `AsNoTracking`, `AsNoTrackingWithIdentityResolution` puis projection d'entités et filtre conditionnel `if (roomId is { } id) query = query.Where(b => b.RoomId == id);` sur une variable initialisée par `Set<Booking>().AsNoTracking()`.

**Cas fautifs**

`SaveChangesAsync`, `ExecuteDeleteAsync`, `ExecuteSqlRawAsync`, `db.Add(...)`, `AsNoTracking().AsTracking()`, `ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking`, `FindAsync`, `Set<Booking>().ToListAsync()` et `Select(b => new { b, b.Id })` sans `AsNoTracking`. `Select(b => Describe(b))` sans `AsNoTracking`, où `Describe` est une méthode C# évaluée côté client. `AsNoTracking` conditionnel : `var query = db.Set<Booking>(); if (readOnly) query = query.AsNoTracking(); return await query.ToListAsync(ct);` produit HS006, car l'initialiseur ne garantit pas l'absence de suivi sur tous les chemins.

**Génériques et inter-assemblies**

Entités du Domain en métadonnées, `DbSet<T>` d'un contexte déclaré dans un projet référencé et projection vers un `Result<BookingDto>` générique.

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

- Types qui implémentent `entity`, directement ou par héritage, racines comprises.
- Critère : un accesseur `set` ou `init` accessible depuis l'extérieur de l'assembly, et pas seulement l'accessibilité apparente sur la classe.
- Propriété d'instance dont l'accesseur `set` ou `init` est public.
- Propriété qui implémente, explicitement ou implicitement, l'accesseur `set` ou `init` d'une interface d'accessibilité effective publique, déclarée dans la compilation ou dans un assembly référencé : `((IEditableBooking)booking).Status = BookingStatus.Cancelled;` écrit l'état sans passer par une méthode du domaine.
- Champ d'instance public non `readonly` : il contourne la règle de la même façon.
- Membre public hérité d'une classe de base qui n'est pas une entité, ou déclarée dans un autre assembly : signalé sur la déclaration de l'entité.
- Record utilisé comme entité : ses propriétés positionnelles ont un `init` public et sont signalées.

**Limites**

DTO, commandes, requêtes, événements et objets valeur ne sont pas concernés. `private`, `protected`, `internal` et `private protected` sont admis, comme l'implémentation d'un setter d'interface `internal`, qui n'est pas accessible hors de l'assembly. Une écriture par réflexion n'est pas détectée.

**Dépendances**

PB-018.

**Cas conformes**

`Booking` actuel, `{ get; private set; }`, `BookingPeriod` (objet valeur) et DTO avec `init` public.

**Cas fautifs**

`public BookingStatus Status { get; set; }`, `public Guid RoomId { get; init; }`, `public DateTimeOffset CancelledAt;`, base `AuditedEntity` non entité avec `public DateTimeOffset UpdatedAt { get; set; }` héritée par une entité et entité enfant `BookingLine` avec setter public. `BookingStatus IEditableBooking.Status { get; set; }`, implémentation explicite d'une interface publique.

**Génériques et inter-assemblies**

Base générique `Entity<TId>` avec `public TId Id { get; set; }`. Base déclarée dans un autre assembly : signalé dans la compilation de l'entité dérivée.

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

- Membres examinés : propriétés et champs d'instance de toute accessibilité des types qui implémentent `entity` (EF Core mappe aussi les champs privés).
- Le type du membre contient une racine si l'on atteint un type qui implémente `aggregateRoot` par le type lui-même, un élément de tableau ou un argument générique (`List<Room>`, `IReadOnlyCollection<Room>`).
- **racines propriétaires possibles** d'un type d'entité enfant : les types de racines qui atteignent ce type par leurs membres, directement ou par d'autres entités enfants. HardSlice retient la définition selon laquelle une racine atteint ses entités enfants par ses membres. La déduction porte sur des types, pas sur des instances : deux racines peuvent posséder des instances distinctes d'un même type d'enfant sans partager leur état.
- Fautif, depuis une racine : tout membre dont le type contient une racine, y compris une autre instance du même type de racine. Une racine ne contient jamais un autre agrégat.
- Fautif, depuis une entité enfant : un membre dont le type contient une racine `R` lorsque l'enfant a au moins une racine propriétaire possible, que `R` n'en fait pas partie, et que tous les membres de `R` sont visibles (`R` déclarée dans la compilation analysée).
- Permis : toute référence d'une racine ou d'une entité enfant à une entité enfant, et la référence inverse d'une entité enfant vers l'une de ses racines propriétaires possibles.
- Ambigu, sans diagnostic : enfant sans racine propriétaire possible dans la compilation, ou racine `R` déclarée dans un autre assembly, dont les membres privés ne sont pas visibles. HardSlice n'affirme pas alors de violation. Aucun mécanisme de configuration de l'appartenance n'est ajouté en v0.2.

**Limites**

Les racines propriétaires possibles ne sont calculées que dans une compilation. La règle ne voit pas les instances : une référence inverse vers une racine propriétaire possible peut, à l'exécution, désigner un autre agrégat du même type, et une référence d'un agrégat à l'entité enfant d'un autre agrégat de même type n'est pas détectable par les types. Une référence typée `object` ou par une interface non classée n'est pas détectée.

**Dépendances**

PB-018.

**Cas conformes**

`Booking.RoomId` (`Guid`), `BookingId` et collection `IReadOnlyList<BookingLine>` d'entités enfants. `BookingLine.Booking`, référence inverse vers sa racine propriétaire possible. `Booking` et `Invoice` qui portent chacune une collection `IReadOnlyList<Note>` du même type d'entité enfant `Note`, avec des instances indépendantes. `Note.Booking` dans ce montage, `Booking` étant une racine propriétaire possible de `Note`.

**Cas fautifs**

`Booking.Room` de type `Room : IAggregateRoot` et `List<Room>`. `BookingLine.Room`, référence portée par une entité enfant. `Booking.PreviousBooking` de type `Booking`. `Note.Room`, alors que `Note` n'est atteinte que par `Booking` et `Invoice`.

**Génériques et inter-assemblies**

Collection générique de racines. Racine déclarée dans un autre assembly, reconnue par son contrat en métadonnées.

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

- HS009 : tout type, non interface, qui implémente `domainEvent`, directement ou par héritage, est un `record class` ou un `record struct`.
- HS019 : le dernier mot du nom en PascalCase finit par `ed`, ou figure dans une liste fixe de participes passés irréguliers publiée dans la fiche (`Paid`, `Sent`, `Built`, `Held`, `Sold`, `Taken`, `Written`…).
- HS009 s'exécute dans la compilation qui déclare l'événement, y compris hors du Domain.

**Limites**

HS009 vérifie que l'événement est un `record`, pas qu'il est immuable : un record peut porter une propriété `set`. Aucun contrôle d'immuabilité n'est ajouté en v0.2.

HS019 est une convention de nommage : elle ne prouve pas que le nom exprime un fait passé. `RoomSeed` et `MediaEmbed` finissent par `ed` sans être au passé. `BookingPaid` ne finit pas par `ed`, d'où la liste d'irréguliers. Elle n'est jamais bloquante et n'est active qu'en `All`, en `suggestion`. Pas de liste d'exceptions configurable en v0.2.

**Dépendances**

PB-018.

**Cas conformes**

`BookingConfirmed`, record abstrait de base, `BookingPaid` et `record struct`.

**Cas fautifs**

HS009 : `sealed class BookingConfirmed : IDomainEvent`, classe qui implémente une interface dérivée `IBookingEvent : IDomainEvent` et classe d'événement déclarée dans `MyApp`. HS019 : `ConfirmBooking`, `BookingConfirmation`.

**Génériques et inter-assemblies**

`EntityChanged<T>` et contrat en métadonnées depuis `MyApp`.

**Sévérité et diagnostic**

HS009 en `error` et HS019 en `suggestion`.

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

- Version unique dans `Directory.Build.props` du dépôt, propagée au moment du pack à la `GlobalPackageReference` et à l'entrée `hardslice` du manifeste d'outil du template, sans toucher à `dotnet-ef`. Un test vérifie l'égalité.
- Métadonnées : description, README de paquet, URL du dépôt, licence, étiquettes. Validation de paquet activée. Avertissements du pack traités comme des erreurs.
- `scripts/generate-app.sh` et `scripts/test-template.sh` construisent et installent les trois paquets locaux, avec la correspondance de sources qui réserve `HardSlice.*` au dossier local.
- `README.md` : version de préversion, réinstallation après reconstruction. L'installation depuis NuGet.org reste décrite par PB-033.

**Dépendances**

PB-021, pour le paquet de l'outil. Peut commencer avec les deux autres paquets.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-033 : Publier la préversion `0.2.0-preview.1`

Statut : livré sauf la publication. Le workflow `.github/workflows/release.yml`, `scripts/verify-published.sh` et le README sont livrés. La publication est reportée dans PB-051 par décision du mainteneur du 03/11/2026, et `0.2.0-preview.1` n'est jamais publiée.

**Résultat observable**

Un utilisateur installe `HardSlice.Templates::0.2.0-preview.1` depuis NuGet.org, génère une application et passe `dotnet hardslice check`.

**Périmètre**

- Workflow `.github/workflows/release.yml` déclenché par l'étiquette `v0.2.0-preview.1`, dans l'environnement protégé `nuget` : construction depuis l'étiquette, tests, pack, publication des trois paquets.
- Job de contrôle sur un runner neuf, après publication : `dotnet new install HardSlice.Templates::0.2.0-preview.1`, génération avec et sans Bookings, `dotnet tool restore`, `dotnet hardslice check`.
- `README.md` : installation depuis NuGet.org.

**Dépendances**

PB-018 à PB-032 et les prérequis NuGet ci-dessus, dont l'accessibilité publique du dépôt.

**Acceptation**

Le workflow de publication et le job de contrôle sont verts. Les trois paquets sont visibles sur NuGet.org en `0.2.0-preview.1`.

### Définition de fini

L'incrément est terminé lorsque PB-018 à PB-033 sont satisfaits et que les jobs CI build, template, démonstration et performance sont verts sur `main`. Concrètement :

- HS003 à HS011, HS015, HS019 et HS900 à HS902 sont livrés avec leur descripteur, leur fiche, leurs tests et leur sonde dans `scripts/test-template.sh`, dans les deux variantes du template lorsque la règle y a une cible.
- La référence Bookings ne produit aucun diagnostic HS, et aucun faux positif connu n'est accepté silencieusement.
- La matrice des niveaux d'analyse est vérifiée et un réglage de `.editorconfig` l'emporte sur elle.
- `dotnet hardslice check` porte la vérification, `check.sh` et `check.ps1` le lancent, et `dotnet build` seul produit toujours les diagnostics.
- Les seuils de PB-019 sont respectés.
- Les paquets `0.2.0-preview.1` sont publiés et la solution générée depuis NuGet.org passe `dotnet hardslice check` (reporté : voir le statut de PB-033).

## 5. Incrément 4 terminé : Boucle agent v0.3

Statut : découpé le 31/10/2026, arbitrages validés par le mainteneur le même jour. PB-034 à PB-050 terminés le 02/11/2026. PB-051 publie une seule préversion, `0.3.0-preview.1`, qui reprend PB-033, une fois la préparation de la section 6 (PB-052 à PB-063) et les retours de Ghostline de la section 7 (PB-064 à PB-078, et la revalidation des paquets qui les suit) terminés.

### Résultat attendu

> Dans une solution générée par `dotnet new hardslice-api`, avec ou sans Bookings, un agent ajoute un module, un agrégat, une commande, une requête et un événement par `dotnet new hardslice-module`, `hardslice-aggregate`, `hardslice-command`, `hardslice-query` et `hardslice-event`, appelés par six skills, sans recréer leur structure à la main. Claude Code reçoit les diagnostics HS après chaque édition d'un `.cs` et ne rend pas la main tant que la vérification échoue. Codex, GitHub Copilot et Gemini CLI disposent chacun d'un adaptateur natif ou du fallback documenté par `AGENTS.md` et le script de vérification. Une suppression de règle sans justification échoue avec HS000. Chaque règle bloquante possède une boucle E2E qui prouve le diagnostic, la correction et le rejet des contournements. Les paquets `0.3.0-preview.1` sont publiés sur NuGet.org.

### Arbitrages de l'incrément

Décisions du mainteneur du 31/10/2026, reportées dans la [spécification](specifications.md) et le [plan](plan.md) :

- **Les templates d'éléments génèrent, ils ne modifient pas.** `dotnet new` ne sait pas éditer un fichier existant : un template crée les fichiers de son élément et affiche les lignes à ajouter dans `<Module>Module.cs` ou `Program.cs`. La skill les ajoute. L'enregistrement reste explicite : pas de découverte par réflexion, qui le cacherait à l'agent et à HS015, ni de post-action « run script » de `dotnet new`, qui exige `--allow-scripts`. Un oubli échoue deux fois : le test d'intégration généré appelle l'endpoint, et un test de l'hôte vérifie que chaque `IRequestHandler<,>` et chaque `IValidator<>` déclaré dans l'assembly se résout depuis le conteneur de l'application.
- **Un élément généré n'est jamais « fini ».** Le code généré compile sans aucun diagnostic HS. Ses tests sont de vrais tests (préparation, appel, assertion du statut) dont seule l'assertion métier reste à écrire, et qui échouent avec un message `TODO(hardslice):` explicite. En plus, `dotnet hardslice check` échoue tant qu'un marqueur `TODO(hardslice):` reste dans `src/` ou `tests/`, et liste les fichiers : remplacer l'échec par une assertion triviale ne suffit pas. La vérification ne passe donc jamais sur un squelette, et aucun test vide ne satisfait HS014 ou HS018. Générer un comportement trivial qui passe est écarté : l'agent rendrait la main sur un squelette vert.
- **Les événements de domaine sont publiés dans la transaction de la commande.** Prérequis de `hardslice-event` et de la skill `add-domain-event` : après le handler et avant l'enregistrement, le pipeline publie les événements des agrégats suivis à leurs gestionnaires, dans la même transaction. Une erreur d'un gestionnaire annule la commande. Un gestionnaire n'envoie jamais de requête par `IMediator`, ce qui imbriquerait une transaction dans celle de la commande : il modifie les agrégats par `AppDbContext`, dans la transaction en cours. Il vit dans une slice de réaction. HS004 admet qu'une slice ait exactement une entrée, une requête avec son handler et son endpoint, ou un gestionnaire d'événement. Pas d'effet externe (e-mail, appel HTTP) dans un gestionnaire, qui s'exécuterait même si la transaction est ensuite annulée. Pas de boîte d'envoi (outbox) ni de bus, hors périmètre (section 5 de la spécification).
- **La vérification complète reste le seul verdict, et la mesure décide du mode du hook de fin.** `dotnet hardslice check` est inchangé et seul utilisé par `AGENTS.md`, le pre-commit et la CI. Le hook après édition utilise `--build` (moins de 10 s). PB-045 mesure d'abord `check` complet sur une application générée : s'il tient en 60 s, le hook de fin le lance tel quel et `--affected` n'est pas livré. Sinon `--affected` est livré, avec les tests unitaires et d'architecture toujours complets, la vérification complète dès qu'une modification sort d'une slice, et une sortie qui rappelle que seul `check` fait foi. Les modes sont appelés par `scripts/check.sh` et `scripts/check.ps1`, qui restent le point unique. Cet arbitrage remplace la limite « pas d'option qui saute une étape » de PB-021, pour ces modes seulement.
- **Les adaptateurs suivent l'état réel des CLI.** Les hooks, skills et fichiers d'instructions de Codex, Copilot CLI et Gemini CLI sont vérifiés par PB-034 à leur version du moment. Un adaptateur natif n'est livré que pour un mécanisme documenté et testé. Sinon le fallback s'applique. Les questions 3 et 4 de la spécification sont tranchées par ce ticket.
- **Une seule source par skill.** Les skills sont écrites une fois au format Agent Skills (`SKILL.md`) dans `.claude/skills/`. Un autre emplacement exigé par un CLI est produit au pack du template depuis cette source, jamais maintenu à la main.
- **HS000 contrôle les suppressions écrites dans le code source.** HS000 signale `#pragma warning disable` et `[SuppressMessage]` d'une règle HS sans commentaire `// hardslice-justification: <raison>`. Les suppressions globales (`<NoWarn>`, sévérités de `.editorconfig` ou `.globalconfig`) ne sont pas détectées : les niveaux d'analyse de PB-020 désactivent eux-mêmes des règles par `.globalconfig`, et l'analyseur ne distingue pas de façon fiable un niveau HardSlice d'un réglage manuel, ce qui ferait de HS000 une règle non déterministe. Ces fichiers, comme `hardslice.json`, les tests d'architecture et les scripts, sont protégés par le hook d'édition de Claude Code et, pour tous les agents, par la CI. Le traitement des réglages globaux est repris en v0.5 avec la baseline, qui doit connaître les règles actives.
- **Les messages nomment leur skill.** Les diagnostics et les messages des tests d'architecture gagnent `Skill: <nom>.` avant `Doc:`, une fois la skill livrée (section 8 de la spécification). Pas de `CodeFixProvider` en v0.3 non plus.

### Critères communs à tous les tickets de l'incrément

- Tout ticket qui modifie le template étend `scripts/test-template.sh` dans les deux variantes, sur la solution générée depuis les paquets locaux, et l'exécute.
- Un template d'éléments est testé en l'appliquant à une solution générée : sans Bookings, la suite module, agrégat, commande, requête et événement doit aboutir à une solution dont la vérification passe une fois la complétion de référence appliquée.
- `BookingsReferenceTests` reste vert et la référence Bookings ne produit aucun diagnostic HS.
- Tout le contenu livré à l'application (code, skills, hooks, fichiers d'instructions) est en anglais, sans tiret cadratin.
- `AGENTS.md` généré reste sous 150 lignes dans les deux variantes, vérifié par un test.
- Aucun assembly HardSlice n'est chargé au runtime de l'application générée, et aucune dépendance runtime n'est ajoutée.
- Un hook ou un adaptateur est testé par un test automatisé qui lui fournit la charge utile documentée du CLI et vérifie son code de sortie et sa sortie, sans lancer d'agent.
- Les jobs CI build, template, démonstration et performance restent verts.

### Graphe des dépendances

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-034 (état des CLI) | aucun | tous |
| PB-035 (publication des événements) | incrément 3 | PB-034, PB-036 à PB-039, PB-041, PB-045 |
| PB-036 (`hardslice-command`) | incrément 3 | PB-034, PB-035, PB-037 à PB-039, PB-041, PB-045 |
| PB-037 (`hardslice-query`) | incrément 3 | PB-034 à PB-036, PB-038, PB-039, PB-041, PB-045 |
| PB-038 (`hardslice-module`) | incrément 3 | PB-034 à PB-037, PB-039, PB-041, PB-045 |
| PB-039 (`hardslice-aggregate`) | incrément 3 | PB-034 à PB-038, PB-041, PB-045 |
| PB-040 (`hardslice-event`) | PB-035 | PB-041 à PB-045 |
| PB-041 (HS000) | incrément 3 | tous sauf PB-049 |
| PB-042 (fichiers d'instructions) | PB-034 | PB-035 à PB-041, PB-045 |
| PB-043 (skills de génération) | PB-034, PB-036 à PB-040 | PB-041, PB-045 à PB-048 |
| PB-044 (skills de correction, `Skill:`) | PB-043 | PB-045 à PB-048 |
| PB-045 (modes des hooks, marqueurs `TODO`) | incrément 3 | PB-034 à PB-044 |
| PB-046 (hooks Claude Code) | PB-045 | PB-042 à PB-044, PB-047, PB-048 |
| PB-047 (adaptateurs Codex, Copilot, Gemini) | PB-034, PB-042, PB-045 | PB-043, PB-044, PB-046, PB-048 |
| PB-048 (pre-commit et CI générée) | PB-045 | PB-042 à PB-047 |
| PB-049 (boucles E2E par règle) | PB-041, PB-044, PB-046 | PB-047, PB-048 |
| PB-050 (scénario agent complet) | PB-043, PB-044, PB-046 | PB-049 |
| PB-051 (préversion `0.3.0-preview.1`) | PB-034 à PB-050, le workflow de PB-033, puis PB-052 à PB-078 (sections 6 et 7) | aucun |

```text
PB-034 état des CLI ────────────┬─ PB-042 instructions ──────────────┐
                                │                                    │
incrément 3 ─┬─ PB-035 ── PB-040 hardslice-event ──┐                 │
             ├─ PB-036 hardslice-command ──────────┤                 │
             ├─ PB-037 hardslice-query ────────────┼─ PB-043 skills ─┼─ PB-044 skills, Skill: ─┐
             ├─ PB-038 hardslice-module ───────────┤                 │                         │
             ├─ PB-039 hardslice-aggregate ────────┘                 │                         ├─ PB-049 boucles E2E
             ├─ PB-041 HS000 ────────────────────────────────────────┼─────────────────────────┤
             └─ PB-045 modes ─┬─ PB-046 hooks Claude Code ───────────┼─────────────────────────┴─ PB-050 scénario agent
                              ├─ PB-047 adaptateurs ◀────────────────┘
                              └─ PB-048 pre-commit et CI
(tout), PB-033 ── PB-051 publication
```

PB-036 à PB-040 modifient `content/` et la configuration du paquet de templates : ce sont des points de fusion, pas des dépendances. PB-042, PB-043 et PB-044 modifient tous `AGENTS.md` : le ticket intégré en dernier vérifie la limite de 150 lignes.

### PB-034 : Établir les hooks, skills et instructions que lisent Codex, Copilot CLI et Gemini CLI

**Résultat observable**

`docs/agents/support.md` dit, pour Claude Code, Codex, GitHub Copilot CLI et Gemini CLI, à une version datée de chacun : le fichier d'instructions lu, la lecture d'`AGENTS.md`, l'existence d'un hook après édition et d'un hook de fin de tâche (nom, charge utile, code de sortie qui renvoie une erreur à l'agent), le lieu et le format des skills. Les questions 3 et 4 de la spécification sont tranchées.

**Périmètre**

- Spike borné : documentation officielle et essai réel de chaque mécanisme dans une solution générée, avec la commande d'essai consignée.
- Pour chaque mécanisme retenu : un exemple de configuration minimale, sa charge utile réelle enregistrée comme fixture de test pour PB-046 et PB-047, et sa limite connue.
- Emplacement des skills exigé par chaque CLI, et décision : lecture directe de `.claude/skills/` ou copie produite au pack.
- `specifications.md`, section 13 : questions 3 et 4 marquées résolues avec renvoi au document.

**Limites**

Pas de code de produit : ce ticket décide le contenu de PB-042, PB-043 et PB-047. Un mécanisme seulement annoncé, expérimental sans documentation, ou impossible à tester sans compte payant est noté et non retenu.

**Dépendances**

Aucune.

**Acceptation**

Le document liste les quatre CLI, chaque cellule renvoie à une source et à un essai daté, et chaque charge utile retenue existe comme fixture.

### PB-035 : Publier les événements de domaine à leurs gestionnaires

**Résultat observable**

Confirmer une réservation publie `BookingConfirmed` à un gestionnaire `IDomainEventHandler<BookingConfirmed>` dans la transaction de la commande. Si le gestionnaire lève une exception, la réservation n'est pas enregistrée et la réponse est celle de l'exception.

**Périmètre**

- `IDomainEventHandler<TEvent>` dans `Shared/Messaging`, contrainte `TEvent : IDomainEvent`.
- Les agrégats exposent leurs événements par un contrat du Domain (méthode qui lit et vide la liste), sans dépendance du Domain vers `Shared`.
- Publication par le pipeline après le handler et avant `SaveChanges` : événements de tous les agrégats suivis par `AppDbContext`, dans l'ordre où ils ont été levés, y compris les événements levés par un gestionnaire, avec une profondeur bornée et une erreur explicite au-delà.
- Requêtes : aucune publication.
- Emplacement du gestionnaire : slice de réaction `Features/<Module>/<Reaction>/`, par exemple `NotifyOnBookingConfirmed`, qui contient le seul gestionnaire `internal`. HS004 admet qu'une slice ait exactement une entrée : une requête avec son handler et son endpoint, ou un gestionnaire d'événement sans endpoint. HS014 et HS018 s'appliquent au gestionnaire comme à un handler de commande.
- Le gestionnaire modifie les agrégats par `AppDbContext` dans la transaction en cours. Un appel à `IMediator.Send` pendant la publication lève une erreur explicite qui nomme le gestionnaire.
- `hardslice.json` : contrat `domainEventHandler`, lu par la classification (PB-018). HS900 si absent alors qu'il est référencé.
- Bookings : pas de nouvelle réaction métier. La publication est prouvée par des tests d'intégration avec un gestionnaire de test.
- `AGENTS.md` : la phrase « They are not published to handlers yet » est remplacée par la règle de publication, avec l'interdiction des effets externes dans un gestionnaire.

**Limites**

Pas d'outbox, de publication après commit, de bus ni de traitement asynchrone. Un gestionnaire ne publie pas vers un autre processus.

**Dépendances**

Incrément 3.

**Cas vérifiés**

- Un gestionnaire reçoit l'événement une fois, dans la transaction, et voit l'état non encore validé.
- Exception d'un gestionnaire : rien n'est enregistré, `DomainEvents` est vidé, la réponse est 400 ou 409 selon l'exception.
- Deux gestionnaires du même événement : tous deux appelés, ordre d'enregistrement.
- Boucle d'événements au-delà de la profondeur : erreur qui nomme l'événement.
- `IMediator.Send` depuis un gestionnaire : erreur qui nomme le gestionnaire, rien n'est enregistré.
- HS004, HS014 et HS018 sur une slice de réaction, conforme et fautive.
- `--no-sample` : compile et vérifie sans gestionnaire.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-036 : Générer une slice de commande avec `dotnet new hardslice-command`

**Résultat observable**

Depuis la racine d'une solution générée et restaurée, `dotnet new hardslice-command --project src/<App>/<App>.csproj --module Bookings --name ExtendBooking` crée la slice, son test unitaire de validateur et son test d'intégration aux chemins de HS004 et HS018, puis affiche les deux lignes à ajouter dans `BookingsModule.cs`. Le build passe sans diagnostic HS. La vérification échoue sur les tests `TODO(hardslice):` jusqu'à ce que la slice soit complétée.

**Périmètre**

- Template d'éléments dans le paquet `HardSlice.Templates`, `content/hardslice-command/`, type `item`.
- Paramètres : `--module` et `--name` requis, PascalCase, sans suffixe `Command`. `--response` (`id` par défaut, `none`, ou nom d'un DTO). Nom de l'application lu depuis la solution (`RootNamespace` du projet web, ou paramètre explicite si la liaison n'est pas disponible), jamais `MyApp` en dur.
- Fichiers : `src/<App>/Features/<Module>/<Name>/<Name>.cs` (commande publique, validateur, handler et endpoint `internal`, forme de `ConfirmBooking`), `tests/<App>.Tests/Features/<Module>/<Name>/<Name>ValidatorTests.cs`, `tests/<App>.IntegrationTests/Features/<Module>/<Name>/<Name>Tests.cs`.
- Le handler lève `NotImplementedException` avec un message qui renvoie à l'agrégat. L'endpoint renvoie des `TypedResults`. Aucune lecture de l'horloge ni appel bloquant.
- Module inexistant : erreur explicite qui renvoie à `hardslice-module`, rien n'est créé. Slice existante : refus sans `--force`.
- Instructions affichées après génération : lignes d'enregistrement du handler et du validateur, appel de `Map`.
- Test de l'hôte dans `ProgramTests`, livré dans les deux variantes : chaque `IRequestHandler<,>` et chaque `IValidator<>` déclaré dans l'assembly de l'application se résout depuis le conteneur, et l'échec nomme le type oublié.
- Tests générés : préparation, appel HTTP et assertion du statut écrits. Seule l'assertion métier est marquée `TODO(hardslice):`.
- `README.md` et section « Adding a slice » d'`AGENTS.md`.

**Réalisation (31/10/2026)**

Essais faits avec le SDK 10.0.401, qui fixent ce que `dotnet new` permet à un template d'éléments :

- La liaison `msbuild:RootNamespace` n'est disponible qu'avec `--project src/<App>/<App>.csproj` (le chemin du fichier, pas du dossier) sur un projet restauré : depuis la racine, `dotnet new` ne trouve aucun projet, car il n'en cherche que dans le dossier de sortie et au-dessus. Le paramètre explicite est donc `--project`, l'option de `dotnet new` elle-même, et non un `--app` qui ne permettrait pas de vérifier le module.
- `dotnet new` ne lit aucun fichier avant de créer les autres. Le module est vérifié par la propriété `HardSliceModuleFiles` du `Directory.Build.targets` de la solution, liste des `*Module.cs` sous `Features/`, que le template lit par la même liaison.
- Un refus (application illisible, nom absent, non PascalCase ou suffixé `Command`, module non PascalCase ou absent, réponse inconnue) exclut tous les fichiers et fait échouer une post-action sans script (ajout d'une propriété JSON à un fichier qui n'existe pas), dont les instructions portent l'erreur : code de sortie 105, rien n'est créé. `dotnet new` affiche malgré tout « The template ... was created successfully. » avant « Post action failed. »
- Les lignes d'enregistrement sont des instructions manuelles, dont les noms sont remplacés par `applyFileRenamesToManualInstructions`. Une slice existante est refusée par `dotnet new` lui-même (code 73, `--force`).
- `--response id` renvoie un `Guid` et répond 200. `none` renvoie un `bool`, comme `CancelBooking`, et répond 204 ou 404. Un DTO est déclaré dans la slice et répond 200. La commande a un champ `Id` à remplacer, la route de l'endpoint est le nom de la slice en kebab-case et celle du test suppose le groupe du module en kebab-case, convention de PB-038.
- Sans Bookings, `scripts/test-template.sh` écrit le module `Rooms` comme le décrit « Adding a first module », en attendant `hardslice-module` (PB-038). Aucun agrégat n'est nécessaire à la commande ni à sa complétion de référence.

**Dépendances**

Incrément 3.

**Cas vérifiés**

- Avec Bookings et sans Bookings (après `hardslice-module` et `hardslice-aggregate`) : build sans diagnostic HS, échec de la vérification sur les seuls tests `TODO(hardslice):`.
- Complétion de référence appliquée par `scripts/test-template.sh` : vérification verte.
- Slice non enregistrée : le test de l'hôte nomme le handler et le validateur oubliés, et le test d'intégration généré échoue.
- Nom invalide, module absent, slice existante.
- `--response none` et DTO : HS005 ne signale rien.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-037 : Générer une slice de requête avec `dotnet new hardslice-query`

**Résultat observable**

`dotnet new hardslice-query --module Bookings --name ListRoomBookings` crée la requête, son DTO, son handler `AsNoTracking` avec projection, son endpoint et son test d'intégration. Le build passe sans diagnostic HS, en particulier HS006.

**Périmètre**

- `content/hardslice-query/`, mêmes conventions de paramètres, de nom d'application et d'erreurs que PB-036.
- `--result` : `single` (réponse `null` répondue par 404, forme de `GetBooking`) ou `many` (une liste. `list` serait lu par `dotnet new` comme sa commande `list`, voir la réalisation).
- Handler : `Set<T>().AsNoTracking()` puis `Select` vers le DTO, sur l'agrégat passé par `--aggregate` (requis).
- Fichiers de tests aux chemins de HS018, test d'intégration `TODO(hardslice):`.
- `AGENTS.md` : le paragraphe sur les requêtes renvoie au template.

**Réalisation (31/10/2026)**

Essais faits avec le SDK 10.0.401 :

- Mêmes liaisons et mêmes refus que PB-036 (`--project`, `HardSliceModuleFiles`, post-action sans script, code 105), plus le refus d'un `--aggregate` non PascalCase et du suffixe `Query`. `--aggregate` absent est refusé par `dotnet new` lui-même (paramètre obligatoire, code 102).
- La liste s'écrit `--result many`, pas `list` : `dotnet new hardslice-query ... --result list` est lu comme la commande `dotnet new list` et échoue sur « Unrecognized command or argument(s): 'hardslice-query' ». `--result` est un choix (`single` par défaut, `many`) que `dotnet new` valide lui-même (code 127).
- Le template ne peut pas vérifier l'agrégat : il le cherche dans l'espace de noms `<App>.Domain.<Module>` et suppose la convention de `Booking`, reprise par PB-039 : une propriété `Id` de type `<Aggregate>Id` qui enveloppe un `Guid Value`. Un agrégat inconnu échoue au build sur CS0246, qui le nomme, sans diagnostic HS.
- `single` : requête `<Name>Query(Guid Id)`, DTO `<Name>Dto?`, `GET /<module>/<slice>/{id:guid}` qui répond 200 ou 404, forme de `GetBooking`. `many` : requête sans paramètre, `IReadOnlyList<<Name>Dto>` triée par `Id`, `GET /<module>/<slice>`. Le DTO n'a qu'un champ `Id`, à compléter. Pas de validateur ni de test unitaire : la requête n'a pas de paramètre à valider.
- Le handler fonctionne tel quel : avec `single`, le test d'un id inconnu passe. Le test qui lit ou liste un agrégat stocké échoue sur `TODO(hardslice):` jusqu'à ce que l'agrégat soit créé par sa fabrique, puis sur un second marqueur jusqu'à ce que les valeurs du DTO soient vérifiées : la préparation dépend de l'agrégat, que le template ne connaît pas.
- Sans Bookings, `scripts/test-template.sh` écrit l'agrégat `Room`, son mapping et sa migration à la main, en attendant `hardslice-aggregate` (PB-039).

**Dépendances**

Incrément 3.

**Cas vérifiés**

Mêmes cas que PB-036, plus : aucune variante ne produit HS006 et `--aggregate` inconnu rend une erreur de compilation lisible, pas un diagnostic HS trompeur.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-038 : Générer un module avec `dotnet new hardslice-module`

**Résultat observable**

`dotnet new hardslice-module --name Rooms` crée `Features/Rooms/RoomsModule.cs`, le dossier `src/<App>.Domain/Rooms/` et les dossiers de tests, puis affiche les deux lignes à ajouter dans `Program.cs`. La solution compile et sa vérification passe.

**Périmètre**

- `content/hardslice-module/` : classe publique statique `<Module>Module` avec `Add<Module>Module` et `Map<Module>Module` sur un `MapGroup` (route en kebab-case par défaut, `--route`), forme de `BookingsModule`.
- Un module vide ne fait échouer ni HS012, ni HS017, ni HS018.
- Module existant : refus sans `--force`.
- Section « Adding a first module » d'`AGENTS.md` en variante `--no-sample` : renvoie au template.

**Réalisation (31/10/2026)**

Essais faits avec le SDK 10.0.401 :

- Même liaison que PB-036 et PB-037 : `dotnet new hardslice-module --project src/<App>/<App>.csproj --name Rooms`, le nom de l'application lu par `msbuild:RootNamespace`. Le module n'a pas besoin de `HardSliceModuleFiles` : il n'en lit aucun.
- `dotnet new` ne crée pas de dossier vide : le dossier du domaine `src/<App>.Domain/<Module>/` et les dossiers de tests `tests/<App>.Tests/Domain/<Module>/`, `tests/<App>.Tests/Features/<Module>/` et `tests/<App>.IntegrationTests/Features/<Module>/` reçoivent chacun un `.gitkeep`. Les tests d'architecture ne lisent que les fichiers C# : HS018 ne le prend pas pour un test, et un dossier de tests de module sans slice est admis tant que le module existe.
- Refus (code 105, rien n'est créé, même post-action sans script que PB-036) : application illisible, nom absent, non PascalCase ou suffixé `Module`, route invalide. `--route` est facultatif : sans lui, le groupe est le nom du module en kebab-case (`MeetingRooms` donne `/meeting-rooms`). Une valeur vide revient à ce défaut. Une barre oblique initiale est retirée, et le reste doit être fait de mots en minuscules séparés par des tirets ou des barres obliques. Les tests générés par `hardslice-command` et `hardslice-query` supposent la route par défaut.
- Un module existant est refusé par `dotnet new` lui-même (code 73, `--force`), qui trouve `<Module>Module.cs`. Avec `--force`, le contrat du module est réécrit, ses enregistrements compris.
- Le contrat a la forme de `BookingsModule`, sans slice : `Add<Module>Module` renvoie `services`, `Map<Module>Module` crée le groupe et renvoie `endpoints`, lignes que complètent les instructions de `hardslice-command` et `hardslice-query`. Les instructions affichées donnent le `using`, puis `builder.Services.Add<Module>Module();` avant `var app = builder.Build();` et `app.Map<Module>Module();` avant `app.Run();`.
- Les messages de `hardslice-command` et `hardslice-query` pour un module absent donnent la commande complète, `--project` compris.
- `scripts/test-template.sh` génère `Rooms` dans les deux variantes avec chacun de ses refus et de ses routes, l'enregistre dans `Program.cs` par les lignes affichées et lance la vérification, verte. Avec Bookings, le module généré qui utilise `BookingDto` fait échouer HS012 dans les deux scripts. Sans Bookings, il reçoit ensuite les slices de commande et de requête.

**Dépendances**

Incrément 3.

**Cas vérifiés**

Variante sans Bookings : module créé, `Program.cs` complété, vérification verte. Variante Bookings : second module à côté de Bookings, vérification verte, HS012 actif entre les deux.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-039 : Générer un agrégat avec `dotnet new hardslice-aggregate`

**Résultat observable**

`dotnet new hardslice-aggregate --module Rooms --name Room` crée l'agrégat, son identifiant typé, son mapping EF Core au niveau du module et son test unitaire. Le build passe sans HS007, HS008 ni HS902. Le message de fin rappelle d'ajouter la migration.

**Périmètre**

- `content/hardslice-aggregate/` : classe `sealed` qui implémente `IAggregateRoot`, constructeur privé sans paramètre pour la persistance, fabrique statique qui vérifie ses règles, état sans `set` ni `init` public, liste privée d'événements que `DequeueDomainEvents()`, membre du contrat `IAggregateRoot` depuis PB-035, renvoie et vide. `readonly record struct <Name>Id`. `<Name>Configuration` `internal` en API fluente dans `Features/<Module>/`.
- Test unitaire `tests/<App>.Tests/Domain/<Module>/<Name>Tests.cs` en `TODO(hardslice):`.
- Pas de migration générée : la commande `dotnet ef migrations add` à lancer est affichée, et le test d'intégration existant sur les modifications en attente la rend obligatoire.
- Module inexistant : erreur qui renvoie à `hardslice-module`.

**Réalisation (31/10/2026)**

Essais faits avec le SDK 10.0.401 :

- Mêmes liaisons et mêmes refus que PB-036 et PB-037 : `dotnet new hardslice-aggregate --project src/<App>/<App>.csproj --module Rooms --name Room`, application lue par `msbuild:RootNamespace`, module vérifié par `HardSliceModuleFiles`, post-action sans script et code 105 pour une application illisible, un nom absent ou non PascalCase, un module non PascalCase ou absent. `--module` absent est refusé par `dotnet new` lui-même (code 102), un agrégat existant aussi (code 73, `--force`).
- Fichiers : `src/<App>.Domain/<Module>/<Name>.cs`, `<Name>Id.cs`, `src/<App>/Features/<Module>/<Name>Configuration.cs` et `tests/<App>.Tests/Domain/<Module>/<Name>Tests.cs`. L'agrégat a la forme de `Booking`, sans état autre que son `Id` : constructeur privé qui reçoit l'identifiant, constructeur privé sans paramètre pour la persistance, liste privée d'événements exposée en lecture seule par `DomainEvents` et vidée par `DequeueDomainEvents()`, fabrique `Create()` qui fonctionne telle quelle, à renommer d'après le fait métier et à compléter de l'état et des règles. Le template ne connaît pas l'état : une fabrique qui lèverait `NotImplementedException` rendrait l'agrégat inutilisable par `hardslice-query`.
- Le mapping convertit l'identifiant, ignore `DomainEvents` et nomme la table d'après l'agrégat suivi de `s` (`Room` donne `Rooms`). `--table` en donne une autre, faite de lettres, de chiffres et de soulignés, sinon refus (code 105). Une valeur vide revient au défaut.
- Les noms des paramètres des lambdas et des variables de test sont fixes (`aggregate`) : un nom dérivé de l'agrégat donnerait un mot clé C# pour `Event` ou `Class`.
- La vérification arrête au premier échec, tests unitaires avant tests d'intégration : elle échoue d'abord sur le test `TODO(hardslice):` de l'état, puis, une fois complété, sur la migration manquante. `MigrationsTests` échoue toujours. Dès que la solution a une migration, EF Core refuse aussi de migrer la base des autres tests d'intégration tant que le modèle a des changements en attente (`PendingModelChangesWarning`), et tous échouent avec « Add a new migration before updating the database ». Sans aucune migration (variante `--no-sample`), seul `MigrationsTests` échoue.
- Les instructions affichées donnent `dotnet tool restore` et `dotnet ef migrations add Add<Table> --project src/<App> --output-dir Shared/Persistence/Migrations` : rien à enregistrer, `AppDbContext` applique la configuration lui-même.
- `scripts/test-template.sh` génère `BookingCategory` (`--table BookingCategories`) dans Bookings, puis le retire avec sa migration. Sans Bookings, il génère `Room` dans le module `Rooms` à la place de l'agrégat écrit à la main, le migre par la commande affichée, et les slices de requête le lisent par `Room.Create()`. La complétion de référence retire le marqueur : l'état de l'agrégat généré est son identifiant, déjà vérifié.

**Dépendances**

Incrément 3.

**Cas vérifiés**

HS007, HS008, HS901 et HS902 muets sur l'agrégat généré. Migration manquante détectée par la vérification. Après migration et complétion de référence, vérification verte.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-040 : Générer un événement de domaine et sa réaction avec `dotnet new hardslice-event`

**Résultat observable**

`dotnet new hardslice-event --module Rooms --name RoomRenamed` crée le `record` dans `src/<App>.Domain/Rooms/Events/`. Avec `--reaction <Module>/<Slice>`, il crée aussi la slice de réaction et ses tests, puis affiche la ligne d'enregistrement du gestionnaire.

**Périmètre**

- `content/hardslice-event/` : `sealed record` qui implémente `IDomainEvent`.
- Nom qui n'est pas au passé selon la règle de HS019 : avertissement à la génération, sans refus (HS019 reste une suggestion).
- Réaction : gestionnaire `internal` `IDomainEventHandler<TEvent>` nommé `<Reaction>Handler`, seul dans sa slice (HS004, PB-035), qui agit par `AppDbContext`, sans `IMediator`, test d'intégration `TODO(hardslice):` aux chemins de HS018. La réaction peut être dans un autre module que l'événement, sans dépendre de ce module (HS012).
- L'agrégat qui lève l'événement n'est pas modifié : la skill `add-domain-event` le fait.

**Réalisation (31/10/2026)**

Essais faits avec le SDK 10.0.401 :

- Mêmes liaisons et mêmes refus que PB-036 à PB-039 : `dotnet new hardslice-event --project src/<App>/<App>.csproj --module Rooms --name RoomRenamed [--reaction <Module>/<Slice>]`, application lue par `msbuild:RootNamespace`, modules vérifiés par `HardSliceModuleFiles`, post-action sans script et code 105 pour une application illisible, un nom absent ou non PascalCase, un module non PascalCase ou absent. `--module` absent est refusé par `dotnet new` lui-même (code 102), un événement existant aussi (code 73, `--force`).
- `--reaction` est facultatif. Sa valeur est découpée par deux générateurs `regex` en module et slice, et refusée (code 105) si elle n'est pas `<Module>/<Slice>` en PascalCase, si la slice finit par `Handler` (le template nomme le gestionnaire `<Slice>Handler`), si elle porte le nom de l'événement (son espace de noms masquerait le type et le build échouerait sur CS0118) ou si son module n'existe pas. Le module de la réaction peut différer de celui de l'événement : la réaction ne dépend que du domaine, HS012 ne signale rien.
- Fichiers : `src/<App>.Domain/<Module>/Events/<Name>.cs`, un `sealed record <Name>(Guid Id) : IDomainEvent` dont `Id` est à remplacer par les valeurs du fait. Avec `--reaction`, `src/<App>/Features/<RModule>/<Slice>/<Slice>.cs`, qui ne contient que le gestionnaire `internal sealed class <Slice>Handler : IDomainEventHandler<Name>`, et `tests/<App>.IntegrationTests/Features/<RModule>/<Slice>/<Slice>Tests.cs`. Le gestionnaire lève `NotImplementedException` avec un message `TODO(hardslice):` qui rappelle `AppDbContext`, et interdit `SaveChangesAsync`, `IMediator` et les effets externes.
- Le test d'intégration ne peut pas envoyer la commande qui lève l'événement : le template ne connaît ni l'agrégat ni la commande. Il fait ce que fait le pipeline : il résout le gestionnaire depuis le conteneur (un oubli d'enregistrement le fait échouer en nommant le module), l'appelle avec un événement qu'il construit, enregistre, puis échoue sur `TODO(hardslice):`. Il échoue donc d'abord sur la `NotImplementedException`, puis sur le marqueur.
- Un nom que HS019 ne lit pas comme un fait passé est créé avec un avertissement (`warning: <Name> is not named as a past fact`), post-action d'instructions sans échec. Son motif reprend la règle de l'analyseur sur les noms PascalCase que le template admet (dernier mot qui finit par `ed` ou participe irrégulier de la fiche, suffixe de version ignoré). Un test le compare à l'analyseur sur chaque participe de la fiche et sur les cas limites.
- `ProgramTests` résout aussi chaque `IDomainEventHandler<>` de l'assembly : une réaction non enregistrée échoue deux fois, par son test et par le test de l'hôte, qui la nomme. Le test devient `Resolves_every_handler_and_validator_of_the_application_by_its_contract`.
- Sans `--reaction`, les instructions disent comment lever l'événement depuis l'agrégat (`_domainEvents.Add(new <Name>(...));`). Le template crée toujours l'événement : une réaction s'ajoute à un événement encore inchangé en relançant le template avec `--reaction` et `--force`, qui le réécrit à l'identique. La réaction à un événement déjà complété s'écrit à la main, sur le modèle de la réaction générée.
- `scripts/test-template.sh` parcourt tous les chemins dans les deux variantes : `BookingExtended` et `BookingArchived` dans Bookings, `RoomRenamed` et `RoomClosed` dans Rooms, chacun avec une réaction dans son module (ajoutée par `--force`) et une dans un module `Housekeeping` généré pour l'occasion. La complétion de référence ajoute par `AppDbContext` un agrégat du module, `Booking.Confirm(...)` ou `Room.Create()`, et le test le relit dans PostgreSQL.

**Dépendances**

PB-035.

**Cas vérifiés**

HS009 muet. Réaction dans le même module et dans un autre module, HS012 et HS016 muets. Complétion de référence appliquée, vérification verte.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-041 : HS000, toute suppression d'une règle HS porte sa justification

**Résultat observable**

Un `#pragma warning disable HS016` ou un `[SuppressMessage("HardSlice.Architecture", "HS016")]` sans commentaire `// hardslice-justification: <raison>` fait échouer le build avec HS000.

**Périmètre**

- `#pragma warning disable` et `#pragma warning restore` absent : chaque identifiant HS de la directive. Identifiants hors HS ignorés.
- `SuppressMessageAttribute` et `UnconditionalSuppressMessageAttribute` reconnus par symbole, sur tout membre, type ou assembly, catégorie quelconque.
- Justification valide : commentaire sur la ligne de la directive ou sur la ligne précédente, de la forme `// hardslice-justification: ` suivie d'au moins dix caractères qui ne sont pas des espaces. La propriété `Justification` de l'attribut ne suffit pas.
- HS000 elle-même n'est pas justifiable : `#pragma warning disable HS000` produit HS000.
- S'applique aux projets de tests, qui ne peuvent pas cacher une règle HS.
- Niveau d'analyse : `error` dans `Minimum`, `Recommended` et `All`.
- Livrables communs des tickets de règle de la section 4 (descripteur, fiche, `README.md`, `AGENTS.md`, tests, sonde, matrice, mesure).

**Limites**

HS000 ne voit pas les suppressions globales (`<NoWarn>`, sévérités de `.editorconfig` ou `.globalconfig`) : les niveaux d'analyse en produisent eux-mêmes, et les distinguer d'un réglage manuel ne serait pas déterministe. Ces fichiers et les tests d'architecture sont protégés par PB-046 et la CI (PB-048), et la fiche le dit dans « ce que la règle ne détecte pas ». Reprise en v0.5 avec la baseline. Une justification fausse n'est pas détectée : elle reste visible en revue.

**Réalisation (31/10/2026)**

Essais faits avec Roslyn 5 du SDK 10.0.401, qui fixent comment HS000 se protège elle-même :

- Un diagnostic d'analyseur marqué `NotConfigurable` échappe aux pragmas, à `<NoWarn>` et aux sévérités de `.editorconfig` et `.globalconfig`, mais pas à `[SuppressMessage]` : `[assembly: SuppressMessage("x", "HS000")]` le faisait taire dans toute la compilation, et un `[SuppressMessage]` de HS000 sur une classe taisait le HS000 de ses autres attributs. Le descripteur porte donc aussi le tag `Compiler`, auquel le compilateur n'applique jamais `SuppressMessage`. HS000 n'est désactivable par aucun moyen, `<NoWarn>` compris. Sa ligne `error` reste dans les trois niveaux pour la matrice, et le test de préséance de `.editorconfig` vérifie qu'elle n'a pas d'effet.
- Le compilateur compare les identifiants en respectant la casse et tels qu'écrits : `#pragma warning disable hs016` et `[SuppressMessage("x", " HS016")]` ne suppriment rien. HS000 ne signale donc que `HS` suivi de chiffres, à la casse exacte, et lit `checkId` avant le premier `:` sans le rogner, comme le compilateur.
- Un `#pragma warning disable` sans identifiant désactive aussi les règles HS : HS000 le signale, et il se justifie comme les autres, sauf dans le code généré. Le générateur de `[GeneratedRegex]`, utilisé par les tests d'architecture, commence sa sortie par cette directive, que personne ne peut justifier : la première exécution de `scripts/test-template.sh` échouait sur `RegexGenerator.g.cs`. Les identifiants HS restent vérifiés dans le code généré. Un en-tête `<auto-generated>` écrit à la main pour y cacher une directive sans identifiant est un contournement interdit de la fiche. Une directive d'une région `#if` inactive, que Roslyn garde dans l'arbre avec `IsActive` faux, n'est pas signalée.
- Justification : commentaire `//` qui termine la première ligne de la suppression ou la ligne précédente, après du code ou une directive compris. `// hardslice-justification:` sans espace avant la raison est admis. Un commentaire `/* */`, un commentaire de documentation `///` ou un texte de chaîne ne comptent pas. Un commentaire couvre tous les identifiants de sa directive et tous les attributs de sa ligne.
- Attributs reconnus par symbole sur tout `AttributeSyntax`, donc assembly, module, type, membre, paramètre, valeur de retour et fonction locale, alias et arguments nommés compris. Un attribut de l'application qui s'appelle `SuppressMessage` n'est pas signalé. Le message reprend la catégorie et le `checkId` tels qu'écrits.
- `scripts/test-template.sh` sonde, dans les deux variantes, un pragma non justifié dans une slice, un `[SuppressMessage]` non justifié dans `tests/<App>.Tests`, une suppression justifiée du domaine qui compile sans diagnostic HS, puis une suppression de HS000 qui échoue même justifiée. `AGENTS.md` dit qu'un agent n'écrit jamais de justification lui-même.

**Dépendances**

Incrément 3.

**Sévérité et diagnostic**

```text
HS000: '#pragma warning disable HS016' in 'CancelBooking.cs' has no justification. A HardSlice rule is suppressed only with a reason a reviewer can read. Fix: remove the suppression and fix the code; if a maintainer approved it, add '// hardslice-justification: <reason>' on the line above. Doc: 'docs/rules/HS000.md'.
```

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-042 : Livrer `CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md`

**Résultat observable**

Une solution générée contient les quatre fichiers d'instructions. `CLAUDE.md` importe `AGENTS.md` (`@AGENTS.md`). `GEMINI.md` et `.github/copilot-instructions.md` y renvoient selon le mécanisme retenu par PB-034, sans dupliquer ses règles.

Réalisé : `CLAUDE.md` est livré dans `.claude/CLAUDE.md` avec `@../AGENTS.md`. L'essai demandé ci-dessous a montré qu'un `CLAUDE.md` à la racine, lu par Copilot, lui faisait charger `AGENTS.md` deux fois ([prise en charge des agents](../agents/support.md), E12 à E14).

**Périmètre**

- Chaque fichier ne contient que ce qui est propre à son CLI : lien ou import d'`AGENTS.md`, hooks actifs ou fallback « run the check script before you finish », emplacement des skills.
- Mécanismes retenus par PB-034 ([prise en charge des agents](../agents/support.md)) : `GEMINI.md` importe `@./AGENTS.md`. `.github/copilot-instructions.md` ne réimporte pas `AGENTS.md`, que Copilot lit déjà. Copilot lit aussi `CLAUDE.md` et `GEMINI.md`, qui commencent donc par la CLI qu'ils visent. Essai à consigner dans `docs/agents/support.md` : Copilot ne charge pas `AGENTS.md` une seconde fois par l'import de `CLAUDE.md`.
- Test : aucun fichier ne recopie une règle d'or d'`AGENTS.md`, et chaque lien résout vers un fichier livré.
- `AGENTS.md` : section « Agent kit » courte qui liste les quatre fichiers, les skills et les hooks.
- `README.md` : agents pris en charge et leur niveau (adaptateur natif ou fallback).

**Dépendances**

PB-034.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-043 : Livrer les skills `add-slice`, `add-module`, `add-aggregate` et `add-domain-event`

**Résultat observable**

Une solution générée contient quatre skills au format Agent Skills qui ajoutent chacune leur élément en appelant son template, sans écrire la structure à la main, puis lancent la vérification.

**Périmètre**

- `.claude/skills/<skill>/SKILL.md` dans le template, avec en-tête `name` et `description` qui déclenchent la skill sur la demande correspondante.
- `add-slice` : lit l'agrégat, cherche si le domaine porte déjà la règle métier et l'y ajoute avec son test sinon, choisit `hardslice-command` ou `hardslice-query`, enregistre la slice, remplit la logique, remplace chaque `TODO(hardslice):` par une vraie assertion, lance la vérification.
- `add-module` : `hardslice-module`, enregistrement dans `Program.cs`, carte des modules d'`AGENTS.md`.
- `add-aggregate` : `hardslice-aggregate`, invariants et tests, migration.
- `add-domain-event` : `hardslice-event`, levée par l'agrégat, réaction et ses tests.
- Chaque skill rappelle les contournements interdits et ne propose jamais de modifier un fichier protégé.
- Copie de chaque skill dans `.agents/skills/`, produite au pack, pour Codex et Gemini CLI (PB-034 : Copilot lit `.claude/skills/` et ne garde qu'une skill par nom). Test d'égalité avec la source.
- `AGENTS.md` : « Adding a slice » et « Adding a first module » renvoient aux skills et aux templates, ce qui libère la place sous 150 lignes.

**Réalisation (31/10/2026)**

- Skills `add-module`, `add-aggregate`, `add-slice` et `add-domain-event` dans `content/hardslice-api/.claude/skills/<skill>/SKILL.md`, en-tête `name` (nom du dossier) et `description` (ce que fait la skill, son template, et « Use when » avec des demandes métier). Corps : prérequis, étapes, puis une section « Never ».
- Chaque skill lance son template avec `--project src/<App>/<App>.csproj`, recopie les lignes qu'il imprime, remplit l'élément, remplace les marqueurs `TODO(hardslice):`, met à jour la carte des modules d'`AGENTS.md`, et lance `scripts/check.sh` ou `scripts/check.ps1` jusqu'au code `0`. `add-slice` lit l'agrégat et y ajoute la règle avec ses tests si elle manque. `add-aggregate` ajoute la migration imprimée et la relit. `add-domain-event` lève l'événement dans la méthode de l'agrégat, teste qu'il est enregistré, et implémente la réaction par `AppDbContext`.
- La section « Never » renvoie à « Forbidden » d'`AGENTS.md`, rappelle les contournements propres à l'élément, nomme les fichiers protégés de PB-046 (plus `.agents/`) et dit de s'arrêter en expliquant le conflit. Hors de cette section, une skill ne nomme aucun fichier protégé, sauf pour lancer les scripts de vérification.
- Aucune condition de template ni mention de Bookings : la même skill sert les deux variantes.
- Copie au pack : un élément `None` du projet de templates écrit `.claude/skills/**` dans `content/hardslice-api/.agents/skills/` du paquet. Le dossier `.agents/` n'existe pas dans les sources.
- `AGENTS.md` : « Adding a first module » et « Adding a slice » renvoient aux skills et gardent les commandes des templates et ce que contient une slice. « Agent kit » liste les skills. 139 lignes avec l'exemple, 146 avec `--no-sample`. `.claude/CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md` nomment les skills de leur dossier.
- Tests (`TemplatePackageTests`) : en-tête valide et nom du dossier, quatre skills et pas de `.agents/` dans les sources. Chaque `dotnet new` cité nomme un template du paquet, des options qu'il déclare et une valeur parmi ses choix, y compris les options citées dans le texte. Chaque chemin cité existe dans le template hors exemple. Liens résolus. « Never » complet. Dans le paquet, la copie de `.agents/skills/` est identique à la source. `scripts/test-template.sh` vérifie dans les deux variantes générées les deux dossiers, leur égalité, le nom et les chemins cités.
- Exécutions manuelles avec Claude Code 2.1.316 (`claude-sonnet-5-5`) sur une solution `--no-sample`, une demande métier qui ne nomme pas la skill : [prise en charge des agents](../agents/support.md), E15 à E18. Chaque fois la skill attendue est chargée, l'élément est généré par son template, la vérification relancée hors de l'agent passe, aucun fichier protégé n'est modifié et aucun marqueur ne reste.

**Dépendances**

PB-034, PB-036 à PB-040.

**Cas vérifiés**

- Test de structure : en-tête valide, chaque commande `dotnet new` citée existe dans le paquet avec ses paramètres, chaque chemin cité existe dans une solution générée.
- Exécution manuelle consignée par skill, avec Claude Code, sur une solution `--no-sample` : élément ajouté, vérification verte, aucun fichier protégé modifié.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-044 : Livrer `extract-to-domain` et `fix-architecture-violation`, et nommer la skill dans chaque message

**Résultat observable**

Un diagnostic HS ou un échec de test d'architecture se termine par `Skill: <nom>. Doc: 'docs/rules/HSxxx.md'.` et la skill nommée existe dans la solution générée.

**Périmètre**

- `fix-architecture-violation` : lit le diagnostic, ouvre la fiche (`dotnet hardslice explain`), applique la première correction qui convient, relance la vérification, et s'arrête en expliquant le conflit si la correction exige un contournement.
- `extract-to-domain` : repère la règle dupliquée dans deux slices, la déplace dans l'agrégat avec son test unitaire, fait appeler la méthode du domaine par les deux slices, relance la vérification.
- Correspondance règle et skill, une skill par message :

| Skill | Règles |
|---|---|
| `add-slice` | HS004, HS013, HS014, HS015, HS018 |
| `extract-to-domain` | HS007, HS016 |
| `add-aggregate` | HS008, HS902 |
| `add-domain-event` | HS009, HS012, HS019 |
| `fix-architecture-violation` | HS000, HS001, HS003, HS005, HS006, HS010, HS011, HS017, HS900, HS901 |

- Messages des descripteurs, des tests d'architecture et de leurs tests mis à jour. Fiches : section « Skill ». Chaîne attendue de la démonstration PB-008 adaptée.
- Format des messages de la section 4 : `Skill:` entre `Fix:` et `Doc:`.

**Réalisation (31/10/2026)**

- Skills `extract-to-domain` et `fix-architecture-violation` dans `content/hardslice-api/.claude/skills/<skill>/SKILL.md`, copiées au pack dans `.agents/skills/` comme celles de PB-043, avec le même en-tête et une section « Never ». Leur description dit qu'un message les nomme (`Skill <nom>`).
- `fix-architecture-violation` : lit le message, passe la main à la skill nommée si ce n'est pas elle, ouvre la fiche par `dotnet hardslice explain <rule>`, applique la première correction qui convient en déplaçant les tests avec le code, cas propres à HS000 (aucune justification écrite par l'agent), HS900 (configuration restaurée, jamais adaptée) et HS901, relance la vérification jusqu'au code `0`, et s'arrête en expliquant le conflit si toute correction exige un contournement, casse une autre règle ou change un comportement décrit par un test.
- `extract-to-domain` : lit le message (HS016 : les deux types. HS007 : l'entité et le membre), traite à part la visibilité de HS016 (`internal`, sans déplacement), trie ce qui est partagé (règle métier, réaction confiée à `add-domain-event`, port dans le contrat du module, DTO propre à chaque slice), ajoute à l'agrégat la méthode nommée d'après l'opération avec ses tests unitaires, fait appeler la méthode par chaque slice, garde les tests d'intégration inchangés et relance la vérification.
- Messages : tous les descripteurs des analyseurs et les messages des tests d'architecture finissent par `Skill: <skill>. Doc: 'docs/rules/HSxxx.md'.`, selon la table ci-dessus. Les messages de `hardslice.json:` (HS017, HS900) nomment `fix-architecture-violation`.
- Fiches : section « Skill » avant « Forbidden workarounds », qui nomme la skill et ce qu'elle fait. Exemples de diagnostics mis à jour. `AGENTS.md` : « Agent kit » liste les six skills et « Fixing a HardSlice failure » dit d'utiliser la skill après `Skill:`, sans ligne de plus (139 et 146 lignes). `.claude/CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md` nomment les six skills.
- Tests : `RuleSkillTests` (table règle et skill sur toutes les fiches, chaque descripteur et chaque message des sources des tests d'architecture nomme la skill de sa règle entre `Fix:` et `Doc:`, chaque skill est livrée, la section « Skill » de chaque fiche nomme la même et ses diagnostics complets finissent par elle). `TemplatePackageTests` passe à six skills et vérifie que les skills de correction lisent le message, lancent la vérification et ne génèrent rien. `scripts/test-template.sh` exige `Skill:` dans le diagnostic de chaque sonde et la présence de la skill nommée dans la solution générée. Chaîne attendue de `scripts/demo-hs016-agent-fix.sh` : `Skill: extract-to-domain.`.
- Non fait : exécution manuelle des deux skills par un agent, reprise par les boucles de PB-049.

**Dépendances**

PB-043.

**Cas vérifiés**

Test : chaque descripteur et chaque test d'architecture nomme une skill livrée par le template, et la fiche de la règle nomme la même.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
```

### PB-045 : Donner aux hooks une vérification après édition et une vérification avant de rendre la main

**Résultat observable**

`scripts/check.sh --build` rend les erreurs du build incrémental en moins de 10 s sur la fixture de performance. Le hook de fin dispose d'une vérification qui tient en 60 s : `check` complet si la mesure le permet, sinon `scripts/check.sh --affected`. `scripts/check.sh` sans argument reste la vérification complète, et échoue tant qu'un marqueur `TODO(hardslice):` reste dans la solution.

**Périmètre**

- D'abord, mesure de `check` complet sur une application générée avec Bookings, sur le runner `ubuntu-latest` documenté (médiane de cinq exécutions, conteneur PostgreSQL compris), consignée dans `docs/performance/analyzers.md` avec la décision : sous 60 s, le hook de fin utilise `check` et `--affected` n'est pas livré. Au-delà, `--affected` est livré comme ci-dessous.
- `check`, tous modes : étape qui échoue tant qu'un marqueur `TODO(hardslice):` reste dans `src/` ou `tests/`, et liste les fichiers et les lignes.
- `dotnet hardslice check --build` : `dotnet build --no-restore` de la solution, restauration seulement si nécessaire. Sortie réduite aux erreurs, diagnostics du compilateur inchangés.
- Si la mesure l'exige, `dotnet hardslice check --affected` : slices touchées lues depuis `git` (modifications non validées et non suivies par rapport à `HEAD`), mappées sur `Features/<Module>/<Slice>/` et leurs tests. Tests unitaires et d'architecture toujours complets. Tests d'intégration filtrés sur les slices touchées. Toute modification hors d'une slice (Domain, `Shared`, configuration, migrations, projets) ou un dépôt sans `git` déclenche la vérification complète.
- Chaque mode affiche ce qu'il a réduit et rappelle que seule la vérification complète fait foi.
- `scripts/check.sh` et `scripts/check.ps1` : les modes livrés transmis, tout autre argument refusé.
- `docs/performance/analyzers.md` : mesure de `--build` sur la fixture et du mode de fin retenu sur l'application générée. Question 8 de la spécification tranchée pour ces mesures.

**Limites**

Pas de cache de résultats de tests ni de serveur de build propre à HardSlice.

**Dépendances**

Incrément 3.

**Cas vérifiés**

- Marqueur `TODO(hardslice):` restant dans un test ou dans un handler : `check` échoue et nomme le fichier.
- Si `--affected` est livré : modification d'une slice, seuls ses tests d'intégration tournent. Modification du Domain, vérification complète.
- Erreur HS dans le mode `--build` : code non nul, message du compilateur intact.
- Si `--affected` est livré, sans Docker et avec des tests d'intégration à lancer : message actuel et code `1`.
- Argument inconnu : code `2`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
scripts/measure-analyzers.sh
```

### PB-046 : Boucler Claude Code sur les diagnostics par ses hooks

**Résultat observable**

Dans une solution générée, après chaque édition d'un `.cs` par Claude Code, les erreurs du build lui reviennent. Avant de rendre la main, il relance la vérification retenue par PB-045 et continue tant qu'elle échoue. Une édition d'un fichier protégé est refusée avec la raison.

**Périmètre**

- `.claude/settings.json` du template :
  - `PostToolUse` sur les outils d'édition, seulement pour un `.cs` : `scripts/check.sh --build`. En échec, erreurs sur la sortie d'erreur et code `2`.
  - `Stop` : la vérification retenue par PB-045, `scripts/check.sh` ou `scripts/check.sh --affected`. En échec, la fin est bloquée avec les erreurs. Si le hook de fin est déjà actif pour la même erreur, au plus trois relances, puis l'agent rend la main en disant que la vérification échoue.
  - `PreToolUse` sur les outils d'édition : refus des fichiers protégés (`.editorconfig`, `hardslice.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `scripts/`, `tests/<App>.ArchitectureTests/`, `.claude/`), avec renvoi à la section « Forbidden ».
- La traduction entre la charge utile du hook et le script est faite par `dotnet hardslice hook claude <événement>`, qui lit l'entrée standard. Une commande identique sous Linux, macOS et Windows.
- Copilot CLI exécute aussi les hooks de `.claude/settings.json`, sans renvoyer leur code `2` à l'agent (PB-034) : quand `COPILOT_CLI` est défini, `dotnet hardslice hook claude` sort avec le code `0` sans rien lancer.
- `AGENTS.md` et `CLAUDE.md` : hooks actifs et leur budget.

**Limites**

Les hooks accélèrent et ne sont pas le garde-fou : un agent ou un humain qui les désactive est arrêté par la vérification complète et la CI.

**Réalisation (31/10/2026)**

- `dotnet hardslice hook claude <événement>` (`PreToolUse`, `PostToolUse`, `Stop`) lit la charge utile sur l'entrée standard et lance la vérification dans le processus de l'outil local, sa sortie et celle du compilateur recueillies pour l'agent. Une CLI ou un événement inconnu, ou une charge utile qui n'est pas du JSON, sort avec le code `1`, jamais `2`, que Claude Code lirait comme un refus.
- `.claude/settings.json` du template : `PreToolUse` et `PostToolUse` sur `Edit|Write` (délais de 30 et 120 s), `Stop` (600 s), la même commande sous Linux, macOS et Windows.
- `PreToolUse` : chemins lus avec `/` ou `\`, `.` et `..` résolus, sans casse. Refus avec le code `2` de `.editorconfig`, `Directory.Build.props`, `Directory.Build.targets` et `Directory.Packages.props` dans tout dossier (une copie dans un sous-dossier prime sur celle de la racine), de `hardslice.json`, `.config/dotnet-tools.json`, `scripts/`, `tests/<App>.ArchitectureTests/` et `.claude/`. La raison renvoie à « Forbidden » d'`AGENTS.md`, qui nomme ces fichiers.
- `PostToolUse` : rien pour un fichier qui n'est pas un `.cs` ou hors de la solution. Sinon `check --build`, et en échec ses erreurs sur la sortie d'erreur avec le code `2`.
- `Stop` : `check` complet (PB-045 l'a retenu). En échec, code `2` avec l'échec et le numéro de la relance. Un fichier par session dans le dossier temporaire compte les relances par empreinte de l'échec (lignes d'erreur et d'échec, sans leurs nombres) depuis la dernière fin sans hook actif. La troisième relance dit à l'agent de finir en disant que la vérification échoue s'il ne peut pas la corriger, la suivante le laisse finir et le dit à l'utilisateur (`systemMessage`). Une vérification réussie l'annonce à l'utilisateur, l'agent ayant écrit son dernier message avant elle.
- `COPILOT_CLI` défini : code `0` sans rien lancer.
- `AGENTS.md`, `.claude/CLAUDE.md`, `.github/copilot-instructions.md`, `README.md` et `docs/agents/support.md` : hooks actifs, budgets et fichiers protégés.
- Tests : `ClaudeHookTests` sur les fixtures de PB-034 (`.cs` fautif, fichier non `.cs`, fichiers protégés et faux amis, chemins Windows, fin en échec puis plafond, autre échec, nouveau tour, mode compatible de Copilot, configuration du template). `scripts/test-template.sh` lance les trois hooks sur la solution générée.
- Essais manuels avec Claude Code 2.1.316 sur la solution générée : HS010 renvoyé et corrigé après édition, édition de `scripts/check.sh` refusée, fin bloquée par un test en échec puis corrigée ([E19] à [E21] de la [prise en charge des agents](../agents/support.md)).
- Limites : une écriture par le shell échappe aux hooks d'édition. Le hook de fin lance la vérification complète à chaque fin de tour, même sans modification. Sans `dotnet tool restore`, la commande est introuvable et l'agent continue sans vérification.

**Dépendances**

PB-045.

**Cas vérifiés**

Tests de `HardSlice.Tool` avec les charges utiles de PB-034 (`tests/HardSlice.Tool.Tests/Fixtures/hooks/claude/`) : `.cs` fautif (code `2`, diagnostic), fichier non `.cs` (aucun build), fichier protégé (refus), fin sur échec puis plafond de relances, chemin Windows, charge utile de Copilot en mode compatible (`copilot-claude-compat/`, aucun build).

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-047 : Adapter Codex, GitHub Copilot et Gemini CLI, ou documenter leur fallback

**Résultat observable**

Pour chacun des trois CLI, la solution générée contient l'adaptateur natif retenu par PB-034, qui appelle les mêmes modes que PB-046, ou bien `AGENTS.md` et son fichier d'instructions lui demandent de lancer `scripts/check.sh` avant de finir. `README.md` dit lequel des deux s'applique.

**Périmètre**

- Configuration native à l'emplacement exigé par le CLI, versionnée dans le template. Traduction de sa charge utile par `dotnet hardslice hook <cli> <événement>`.
- Retenus par PB-034 ([prise en charge des agents](../agents/support.md)) : Codex, `.codex/hooks.json` (`PostToolUse` sur `apply_patch` et `Stop`, code `2`), avec l'approbation par `/hooks` décrite dans `README.md` et `AGENTS.md` et une commande stable d'une version à l'autre. Copilot CLI, `.github/hooks/hardslice.json` (`postToolUse` sur `edit` et `create` par `additionalContext`, `agentStop` par `decision: "block"`). Gemini CLI, fallback, faute d'essai possible sans clé API.
- Protection des fichiers protégés là où le CLI la permet.
- Tableau des capacités par CLI dans `docs/agents/support.md` et `README.md`, tenu à jour par ce ticket.
- Un CLI sans mécanisme retenu n'a pas de configuration morte dans le template.

**Réalisation (01/11/2026)**

- `dotnet hardslice hook <cli> <événement>` généralise le hook de PB-046 : une même boucle (refus avant l'édition, `check --build` après celle d'un `.cs`, `check` avant de rendre la main, trois relances au plus pour le même échec, comptées par CLI et par session), et pour chaque CLI ses événements, l'endroit de sa charge utile qui nomme les fichiers et sa façon de répondre.
- Codex, `.codex/hooks.json` : `PreToolUse` et `PostToolUse` sur `Edit|Write`, qui couvre `apply_patch`, `Stop`. Fichiers lus sur les lignes `*** Add File:`, `*** Update File:`, `*** Delete File:` et `*** Move to:` du patch, un patch refusé dès qu'un de ses fichiers est protégé, un build quand il touche un `.cs`. Code `2` et sortie d'erreur, et JSON (`systemMessage`) avec le code `0` quand la vérification de fin passe, comme Codex l'exige. Commandes stables d'une version à l'autre, approbation par `/hooks` décrite dans `README.md` et `AGENTS.md`.
- Copilot CLI, `.github/hooks/hardslice.json` : `preToolUse` et `postToolUse` avec le matcher `edit|create`, `agentStop`, commandes `bash` et `powershell` identiques, délais de 30, 120 et 600 s. Réponses en JSON avec le code `0` : `permissionDecision: "deny"`, `additionalContext` coupé à 9 000 caractères (Copilot le borne à 10 Kio), `decision: "block"`. Rien quand la vérification de fin passe, Copilot n'ayant pas de message à l'utilisateur.
- Gemini CLI : fallback, aucun `.gemini/settings.json`. `hook gemini` n'existe pas.
- `.codex/` et `.github/hooks/` rejoignent les fichiers protégés, pour les trois adaptateurs. « Forbidden » d'`AGENTS.md` les nomme.
- `AGENTS.md`, `.github/copilot-instructions.md`, `README.md` (tableau des CLI et de leurs hooks), le README de l'outil et la [prise en charge des agents](../agents/support.md) (tableau des capacités) décrivent l'adaptateur de chaque CLI.
- Tests : `CodexHookTests` et `CopilotHookTests` sur les fixtures de PB-034 (patch d'un `.cs`, de plusieurs fichiers, d'un fichier protégé par chaque en-tête, ajout d'un fichier non `.cs`, autre outil de Copilot, erreurs trop longues, fin en échec puis plafond, relances propres à chaque CLI, `COPILOT_CLI` sans effet sur ces hooks, configuration du template). `scripts/test-template.sh` lance les hooks de Codex et de Copilot sur la solution générée.
- Essais manuels avec Copilot CLI 1.0.105 et Codex 0.165.0 sur la solution générée : HS010 renvoyé et corrigé après édition, édition de `scripts/check.sh` refusée, fin bloquée par un échec puis corrigée ([E22] à [E26] de la [prise en charge des agents](../agents/support.md)).
- Limites : sans `dotnet tool restore`, `dotnet hardslice` sort avec le code `0` et les hooks ne font rien. Une écriture par le shell échappe aux hooks d'édition. Copilot termine un tour après huit blocages consécutifs. Un outil d'édition de Copilot autre qu'`edit` et `create` n'est pas couvert.

**Dépendances**

PB-034, PB-042, PB-045.

**Cas vérifiés**

Tests de `HardSlice.Tool` avec les charges utiles enregistrées de chaque CLI. Essai manuel daté de chaque adaptateur consigné dans `docs/agents/support.md`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-048 : Appeler la vérification depuis le pre-commit et la CI de l'application générée

**Résultat observable**

Une solution générée contient un hook git `pre-commit` et un workflow GitHub Actions, qui lancent tous deux `scripts/check.sh` sans argument. Un commit qui casse une règle est refusé une fois le hook activé. Le workflow échoue sur la même règle.

**Périmètre**

- `.githooks/pre-commit`, activé par `git config core.hooksPath .githooks`, commande documentée dans `AGENTS.md` et `README.md`. Jamais activé à l'insu de l'utilisateur.
- `.github/workflows/hardslice.yml` : checkout, SDK de `global.json`, `scripts/check.sh`. Sur une pull request, échoue aussi si un fichier protégé est modifié sans étiquette d'approbation du mainteneur, ce qui remplace pour tous les CLI la protection du hook d'édition.
- Actions épinglées par empreinte, comme la CI du dépôt.

**Limites**

Azure Pipelines et la politique de branche protégée restent en v1.0.

**Réalisation (01/11/2026)**

- `.githooks/pre-commit` du template lance `scripts/check.sh` sans argument, retrouvé depuis le dossier du hook, et refuse le commit tant qu'il échoue. Rien ne l'active : `dotnet new` ne garde pas le bit d'exécution (fichiers en `0600`), donc `AGENTS.md` et `README.md` donnent `chmod +x .githooks/pre-commit` (macOS, Linux) puis `git config core.hooksPath .githooks`, et `AGENTS.md` interdit à l'agent de l'activer ou de le sauter par `--no-verify`.
- `.github/workflows/hardslice.yml` du template : job `check` (checkout, SDK de `global.json`, `bash scripts/check.sh`) sur chaque push sur `main` et chaque pull request. Job `protected-files` sur une pull request seulement, qui passe `git diff --name-only --no-renames -z HEAD^1 HEAD` du commit de fusion (`fetch-depth: 2`) à `dotnet hardslice protected` et échoue sur un fichier protégé sans l'étiquette `hardslice-approved`. Les événements `labeled` et `unlabeled` relancent le workflow, car une relance garde la charge utile d'origine et ses étiquettes. Actions épinglées aux empreintes de la CI du dépôt, `permissions: contents: read`, `persist-credentials: false`.
- `dotnet hardslice protected` lit les chemins sur l'entrée standard, séparés par des lignes ou des caractères NUL (`git diff -z` ne met aucun nom entre guillemets, alors qu'un nom non ASCII cité aurait échappé à la liste), et liste les fichiers protégés : code `1` s'il y en a, `0` sinon, `2` hors d'une solution générée, pour que la CI ne prenne jamais une erreur pour un fichier que l'étiquette approuverait. La liste est celle des hooks (`ProtectedFiles`), qui gagne `.githooks/` et `.github/workflows/hardslice.yml`. « Forbidden » d'`AGENTS.md` les nomme.
- `scripts/generated-workflow.sh` : `lint` valide le workflow avec actionlint 1.7.12 (shellcheck des étapes `run` compris), `run <solution> <job>` exécute les étapes `run` d'un job dans la solution comme un runner (`bash --noprofile --norc -eo pipefail`), les étapes `uses` étant reproduites par l'appelant. Actionlint et yq tournent dans des conteneurs épinglés par empreinte. Job CI `generated-workflow` du dépôt : génère une solution, valide son workflow, installe le SDK de son `global.json` comme son étape `setup-dotnet`, puis exécute son job `check`. `TemplatePackageTests` vérifie que ses actions sont celles de `ci.yml` aux mêmes empreintes.
- `scripts/test-template.sh` : actionlint sur les deux variantes. Avec l'exemple, dépôt git temporaire dans la solution, hook activé par les commandes d'`AGENTS.md`, commit d'une violation de HS010 refusé, commit conforme accepté après la vérification complète, puis job `protected-files` exécuté sur un changement de `scripts/check.sh` (échec, puis succès avec l'étiquette) et sur un changement de documentation (succès). Le dépôt est retiré ensuite.
- Limites : le hook vérifie l'arbre de travail, modifications non indexées comprises. Une pull request exécute sa propre version du workflow, et toute personne qui peut étiqueter peut poser `hardslice-approved` : seule la protection de branche de v1.0 (vérifications requises, propriétaires du code) rend ces contrôles obligatoires. Le déclencheur de push vise `main` seulement.

**Dépendances**

PB-045.

**Cas vérifiés**

`scripts/test-template.sh` : dépôt git temporaire, hook activé, commit fautif refusé et commit conforme accepté. Workflow validé par un analyseur de syntaxe et exécuté sur la solution générée par un job CI du dépôt.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
```

### PB-049 : Prouver la boucle de correction de chaque règle bloquante

**Résultat observable**

`scripts/demo-agent-fix.sh prepare <règle> <dossier>` prépare, pour chaque règle bloquante, une solution générée qui échoue avec le diagnostic attendu. `verify` accepte une correction conforme et rejette chaque contournement connu. La CI exécute `prepare` et une correction de référence pour toutes les règles.

**Périmètre**

- Généralisation de `scripts/demo-hs016-agent-fix.sh` et de `demos/hs016-agent-fix/` : une fixture par règle bloquante (HS000, HS001, HS003 à HS018, HS900 à HS902), avec son diagnostic exact, son invite d'agent et une correction de référence.
- `verify` : vérification complète verte, aucun fichier protégé modifié, aucun motif de contournement (expressions de la démonstration PB-008 étendues à HS000).
- Job CI `demo` : `prepare` puis correction de référence et `verify` pour chaque règle.
- Traces d'agent réelles consignées pour un échantillon, au moins une règle du domaine, une de la messagerie, une d'architecture et HS000, avec Claude Code et hooks actifs.

**Réalisation (01/11/2026)**

- `demos/agent-fix/<règle>/` pour les 21 règles bloquantes : `fixture/` posée sur `Demo` (template `hardslice-api` avec l'exemple), `diagnostic.txt` (chaque diagnostic imprimé par la vérification, de `HSxxx:` à `Doc: '...'.`, sans fichier ni position), `prompt.md` (la demande de développement, jamais la règle ni la correction), `reference/` et `reference.remove`, et pour HS004, HS008, HS013, HS014, HS016 et HS018 un contournement propre dans `workarounds/`. La démonstration PB-008 devient `demos/agent-fix/HS016`, `scripts/demo-hs016-agent-fix.sh` est retiré.
- `scripts/demo-agent-fix.sh` : `prepare <règle> <dossier>` génère `Demo`, prouve sa vérification complète verte, pose la fixture, la commite pour le diff local de l'agent et échoue sauf si `scripts/check.sh` échoue avec exactement les diagnostics de `diagnostic.txt`, sans erreur de compilation d'un autre code. `prepare-all` et `verify-reference` génèrent une seule fois pour toutes les règles.
- `verify <règle> <dossier>` n'exécute rien du dossier de l'agent : il régénère une copie de confiance, reprouve les diagnostics de la fixture, valide le candidat puis copie les seuls changements acceptés dans un arbre propre et y lance le `scripts/check.sh` de confiance. Validation : chemins limités aux `.cs` et `.csproj` de `src/`, `tests/Demo.Tests/` et `tests/Demo.IntegrationTests/`, aucun fichier protégé selon `dotnet hardslice protected` de confiance, un projet seulement ramené à son contenu généré, attributs de test, déclarations et assertions des tests de la fixture conservés (au même chemin ou sous le même nom dans le même projet de tests), et dans les fichiers changés ni directive de préprocesseur, ni suppression, ni `hardslice-justification`, ni réflexion, ni `dynamic`, ni test sauté ou toujours vrai, ni validateur sans règle, ni fichier de tests sans assertion.
- `verify-reference` : pour chaque règle, cinq contournements génériques (suppression justifiée, sévérité dans `.editorconfig`, `<NoWarn>`, dossier ajouté à `hardslice.json`, tests d'architecture supprimés) et les contournements propres doivent échouer à la validation, la correction de référence doit passer `verify`. Le job CI `demo` exécute `verify-reference` sur toutes les règles.
- Traces Claude Code 2.1.316 (`claude-sonnet-5-5`, réglages du projet, hooks actifs) pour HS000, HS005 (messagerie), HS008 (domaine) et HS012 (architecture), dans `demos/agent-fix/<règle>/trace.md` et `docs/agents/support.md` [E27] à [E30] : les quatre corrections passent `verify`, aucun agent n'a tenté de contournement ni chargé la skill nommée par le diagnostic.
- Limites : les contournements sont reconnus par motifs. Un test vide ou une règle recopiée écrits autrement se jugent à la lecture du diff, comme dans les traces.

**Dépendances**

PB-041, PB-044, PB-046.

**Cas vérifiés**

Chaque fixture produit exactement son diagnostic, et lui seul. Chaque correction de référence passe `verify`. Chaque contournement connu échoue `verify`.

**Acceptation**

```text
scripts/demo-agent-fix.sh prepare-all "$(mktemp -d)"
scripts/demo-agent-fix.sh verify-reference "$(mktemp -d)"
```

### PB-050 : Démontrer qu'un agent ajoute une fonctionnalité par les chemins officiels

**Résultat observable**

À partir d'une solution `--no-sample`, Claude Code reçoit une demande métier (un module, son agrégat, une commande, une requête et un événement avec sa réaction) et la livre par les skills et les templates. La vérification complète est verte, aucun fichier protégé n'est modifié, et la trace montre l'usage des templates. La trace et une courte vidéo servent de démonstration publique de v0.3.

**Périmètre**

- `demos/add-feature/` : invite, vérificateur qui contrôle la présence des éléments aux chemins attendus, l'absence de `TODO(hardslice):` restant, l'usage des commandes `dotnet new hardslice-*` dans la trace, et la vérification complète.
- Deux exécutions consignées avec Claude Code. Une exécution avec chacun des trois autres CLI dans son mode (adaptateur ou fallback), résultat publié même défavorable.
- `README.md` : lien vers la démonstration.

**Réalisation (01/11/2026)**

- `demos/add-feature/prompt.md` : une demande métier en anglais, le catalogue d'une boutique (un produit, son nom et son prix, son enregistrement, sa lecture, et un stock ouvert à zéro par réaction à l'enregistrement plutôt que dans le cas d'utilisation). Elle nomme les éléments (module `Catalog`, agrégats `Product` et `Stock`, `RegisterProduct`, `GetProduct`, `ProductRegistered`, réaction `OpenStockOnProductRegistered`) pour que leurs chemins soient connus, jamais une skill, un template ni une règle. La réaction ajoutant un agrégat, la demande en compte deux.
- `scripts/demo-add-feature.sh` : `prepare <dossier>` génère `Shop` avec `--no-sample`, installe les templates dans le dossier personnel isolé de la CLI dotnet où le `dotnet new` de l'agent les trouve, prouve la vérification complète verte et commite la solution. `verify <dossier> <trace>` n'exécute rien du dossier de l'agent, comme celui de PB-049 : il régénère une solution de confiance, valide le candidat (seuls `AGENTS.md`, les `.cs` de `src/` et des tests unitaires et d'intégration et les `.gitkeep` du template de module changent, aucun projet, aucun fichier protégé selon `dotnet hardslice protected` de confiance, aucune construction interdite hors les directives qu'écrit `dotnet ef` dans une migration), copie les changements acceptés dans un arbre propre, y contrôle les 18 fichiers aux chemins de leurs templates, les enregistrements, l'événement levé par `Product` et sa réaction, le modèle des migrations et la carte d'`AGENTS.md`, l'absence de `TODO(hardslice):` et la vérification complète de confiance, puis exige dans la trace les sept appels `dotnet new hardslice-*` attendus. `record` conserve les changements et la trace d'une exécution dans `demos/add-feature/runs/<exécution>/`, chemins de la machine remplacés. `verify-recorded` revérifie chaque exécution enregistrée depuis ses changements et sa trace contre le verdict écrit dans `verdict`.
- `scripts/agent-trace.py` lit les traces JSONL de Claude Code (`stream-json`), de Codex (`exec --json`) et de Copilot CLI (`--output-format json`) : commandes shell et skills des seuls appels d'outil exécutés avec succès (un appel refusé par les permissions ou en échec ne compte pas), appels `dotnet new` et leurs options quel que soit leur ordre.
- `scripts/demo-common.sh` : la validation d'un candidat partagée avec `scripts/demo-agent-fix.sh`, qui la source. `scripts/generate-app.sh` transmet au template les options qui suivent le nom de l'application, comme `--no-sample`.
- Exécutions consignées dans `demos/add-feature/runs/`, chacune avec sa trace, ses changements, son verdict et son compte rendu, et dans `docs/agents/support.md` [E31] à [E35] : Claude Code 2.1.316 avec `claude-sonnet-5-5` (45 tours, 3 min 22 s, 0,53 USD) et `claude-opus-5-5` (81 tours, 6 min 26 s, 1,51 USD), Codex CLI 0.165.0 avec ses hooks approuvés (6 min 37 s) et Copilot CLI 1.0.105 avec `claude-sonnet-4.6` (5 min 26 s) passent `verify` et `verify-recorded`. Gemini CLI 0.64.0, en fallback, s'arrête à l'authentification (`UNSUPPORTED_CLIENT`, compte personnel), sans trace ni changement.
- `README.md` : lien vers la démonstration depuis « Change it », commande et chemin dans « Working on HardSlice ».
- Constats : aucun agent n'a écrit la structure d'un élément à la main ni modifié un fichier protégé, tous ont mis les règles dans les agrégats et ouvert le stock dans la réaction. Les deux exécutions Claude Code ont chargé les quatre skills, Copilot `add-module` seule (`AGENTS.md` nomme les autres templates), Codex les a lues par `cat`. Chaque blocage d'un hook après édition était un état intermédiaire des éditions de l'agent. `codex exec --json` ne rapporte aucun événement de hook : le hook de fin de Codex n'est attesté que par ses effets.
- Non fait : la courte vidéo de la démonstration publique, à enregistrer depuis une exécution consignée. L'exécution de Gemini CLI, à reprendre avec une clé API, une fois les événements `stream-json` de Gemini CLI lus par `agent-trace.py`.

**Limites**

Ce n'est pas le benchmark de v0.4 : pas de répétitions statistiques ni de comparaison A/B/C.

**Dépendances**

PB-043, PB-044, PB-046.

**Acceptation**

Le vérificateur passe sur les exécutions Claude Code consignées. Chaque exécution des autres CLI est consignée avec son résultat.

### PB-051 : Publier la préversion `0.3.0-preview.1`

Statut : suspendu. Décision du mainteneur du 04/11/2026, après PB-080 : aucune version n'est publiée sur NuGet.org pour l'instant. Le but est la version la plus stable possible, quel que soit son numéro, et Ghostline sert à itérer tout de suite (section 7, PB-081 à PB-088). L'étiquette visera le commit du dernier ticket de revalidation des paquets le jour où le mainteneur décidera la publication. PB-080, terminé, a revalidé les paquets après PB-075 à PB-079 et PB-085 (les textes des diagnostics, des erreurs de l'outil et des réponses de ses hooks sont figés par la publication). L'étiquette `v0.3.0-preview.1` vise le commit de `main` de PB-080 (« revalidate the 0.3.0-preview.1 packages after PB-075 to PB-079 and PB-085 »), sur lequel les paquets ont été revalidés en dernier le 04/11/2026. PB-074 les avait revalidés après PB-060 à PB-073.

**Résultat observable**

Un utilisateur installe `HardSlice.Templates::0.3.0-preview.1` depuis NuGet.org, génère une application, ajoute une slice par `dotnet new hardslice-command` et passe `dotnet hardslice check`.

**Périmètre**

- Étiquette `v0.3.0-preview.1` posée sur le commit de `main` du dernier ticket de revalidation des paquets, après le feu vert du mainteneur.
- Workflow de publication de PB-033, avec le job de contrôle étendu par PB-059 aux templates d'éléments.
- GitHub Release de la version, avec les notes de `CHANGELOG.md` (PB-058).

**Dépendances**

PB-034 à PB-050, PB-033, PB-052 à PB-080, PB-085.

**Acceptation**

Le workflow de publication et le job de contrôle sont verts. Les trois paquets sont visibles sur NuGet.org en `0.3.0-preview.1`.

### Définition de fini

L'incrément est terminé lorsque PB-034 à PB-051 sont satisfaits et que les jobs CI build, template, démonstration et performance sont verts sur `main`. Concrètement :

- Les cinq templates d'éléments génèrent leur élément dans les deux variantes, sans diagnostic HS, et une solution construite uniquement par eux passe la vérification après complétion.
- Les six skills sont livrées et nommées par les messages de toutes les règles.
- Les quatre fichiers d'instructions sont livrés et `AGENTS.md` reste sous 150 lignes.
- Claude Code boucle sur le build après édition et sur la vérification avant de rendre la main, dans les budgets de 10 et 60 secondes mesurés.
- Codex, Copilot CLI et Gemini CLI ont chacun un adaptateur natif testé ou le fallback documenté.
- HS000 est livrée, et le pre-commit et la CI générée appellent la vérification complète.
- Chaque règle bloquante a sa boucle E2E en CI, et la démonstration d'ajout de fonctionnalité est publiée.
- Les paquets `0.3.0-preview.1` sont publiés.

## 6. Incrément actif : Préparation de la publication 0.3.0-preview.1

Statut : découpé le 03/11/2026 à partir d'une revue complète du dépôt avant publication (code, template, kit agents, outil, CI, documentation), arbitrages validés par le mainteneur le même jour. Une version publiée sur NuGet.org ne se remplace jamais : ce qui est figé dans les paquets (code généré, kit agents, liste des fichiers protégés, textes des diagnostics, README des paquets, format de `hardslice.json`, codes de sortie) est corrigé avant PB-051.

Complété le 04/11/2026 par les frictions relevées en construisant Ghostline, la première application réelle sur la préversion (`ghostline/docs/hardslice-friction.md`) : PB-060 à PB-063 ici, PB-064 à PB-073 dans la section 7, PB-074 qui revalide les paquets après eux, PB-075, qui change un diagnostic après cette revalidation, PB-076, qui change les erreurs de l'outil sur les fichiers HardSlice, PB-077, qui change la réponse du hook après une édition, et PB-078, qui change la réponse du hook de fin de tour sur un fichier HardSlice protégé. Décision du mainteneur du 04/11/2026 : tout est fait avant PB-051, pour publier d'emblée la préversion la plus stable possible.

### Résultat attendu

> `dotnet new hardslice-api` génère une solution qui compile et passe la vérification quel que soit le nom accepté par `dotnet new`, `Acme.Rooms` et `my-app` compris. Aucun fichier qui règle les analyseurs ne peut être modifié par un agent sans être refusé par les hooks et par la CI. Les analyseurs ne font jamais tomber le compilateur ni l'IDE sur du code en cours d'écriture, et ne se taisent jamais sans HS900 sur une configuration incomplète. Les contrats publics figés par la préversion (`hardslice.json`, codes de sortie de `check`, liste des fichiers protégés) laissent évoluer les versions suivantes. La documentation publique est exacte, en anglais, sans information personnelle, et un visiteur venu de NuGet.org trouve en moins d'une page ce qu'il obtient et comment l'essayer. Les trois paquets sont prêts à être publiés en `0.3.0-preview.1`.

### Arbitrages de l'incrément

Décisions du mainteneur du 03/11/2026 :

- **Une seule forme du nom de l'application.** Le nom passé à `--name` est converti une fois en identifiant .NET sûr (`my-app` devient `my_app`, `1Rooms` devient `_1Rooms`, `Acme.Rooms` reste `Acme.Rooms`), et cette forme sert partout : dossiers, projets, assemblies, espaces de noms, `.slnx`, `hardslice.json`. Seul le dossier de sortie garde le nom tapé. Aucun nom de type ne dérive du nom de l'application. Refuser les noms non identifiants est écarté : `dotnet new` ne sait pas refuser avant de créer les fichiers.
- **`.agents/` est protégé**, comme `.claude/` : c'est la copie des skills que lisent Codex, Gemini CLI et Copilot CLI.
- **`hardslice.json` porte une version.** `"version": 1` est exigé. Une version inconnue produit HS900 avec un message qui demande de mettre à jour `HardSlice.Analyzers`. `$schema` est accepté. Le reste du format reste strict : une clé inconnue est une faute de frappe qui désactiverait une règle sans le dire.
- **Codes de sortie de `check` :** `0` vérification réussie, `1` une étape a échoué (sa sortie reste affichée telle quelle), `2` mauvais usage seulement.
- **Les alertes de sécurité NuGet ne cassent pas la restauration** d'une solution générée : NU1901 à NU1904 restent des avertissements, affichés, malgré `TreatWarningsAsErrors`.
- **La documentation de conception reste publique, en français.** Spécification, plan et backlog vont dans `docs/design/`, nettoyés de ce qui concerne le mainteneur plutôt que le produit, avec une bannière qui les présente comme documents de travail. `docs/plans/` est supprimé. Tout ce que le README et les paquets présentent à l'utilisateur est en anglais.
- **Les démonstrations gardent leurs traces brutes,** filtrées de l'inventaire local des CLI et des blocs de raisonnement chiffrés, et leurs README disent exactement dans quelles conditions elles ont tourné.
- **L'historique git n'est pas réécrit.** Le dépôt est déjà public. L'historique est conservé tel quel.

### Critères communs à tous les tickets de l'incrément

- Les critères communs de la section 5 restent applicables (anglais, sans tiret cadratin, `AGENTS.md` généré sous 150 lignes dans les deux variantes, aucune dépendance runtime, hooks testés par leur charge utile documentée).
- Tout constat corrigé reçoit un test qui échouait avant la correction : test unitaire, test du template ou sonde de `scripts/test-template.sh`.
- Chaque ticket lance localement `dotnet build --no-restore`, `dotnet test --no-build` et les sondes de `scripts/test-template.sh` de sa zone, extraites et lancées seules sur une solution générée depuis les paquets locaux. Le passage complet de `scripts/test-template.sh` tourne en CI à chaque push et localement dans PB-059.
- Aucune étiquette `v*` n'est posée et rien n'est publié sur NuGet.org avant PB-051.
- Les jobs CI build, template, démonstration et performance restent verts sur `main`.

### Graphe des dépendances

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-052 (nom de l'application) | incrément 4 | PB-053 à PB-055, PB-057 |
| PB-053 (fichiers protégés) | incrément 4 | PB-052, PB-054, PB-055, PB-057 |
| PB-054 (robustesse des analyseurs) | incrément 4 | PB-052, PB-053, PB-055 à PB-057 |
| PB-055 (contrats publics) | incrément 4 | PB-052 à PB-054, PB-057 |
| PB-056 (application générée et kit agents) | PB-052, PB-053 | PB-054, PB-055, PB-057 |
| PB-057 (démonstrations) | incrément 4 | PB-052 à PB-056 |
| PB-058 (documentation) | PB-052 à PB-057 | aucun |
| PB-059 (paquets prêts à publier) | PB-058 | aucun |
| PB-060 (paquets de l'application) | PB-059 | PB-061 à PB-063 |
| PB-061 (restauration avant build) | PB-059 | PB-060, PB-062, PB-063 |
| PB-062 (secrets et tests d'intégration) | PB-059 | PB-060, PB-061, PB-063 |
| PB-063 (kit agents, commandes) | PB-059 | PB-060 à PB-062 |

Le dépôt n'a qu'un agent : les tickets passent l'un après l'autre, dans l'ordre des numéros.

### PB-052 : Générer une solution valide quel que soit le nom de l'application

**Résultat observable**

`dotnet new hardslice-api --name Acme.Rooms`, `--name my-app` et `--name 1Rooms --no-sample` génèrent des solutions qui compilent et passent `dotnet hardslice check`. Les templates d'éléments s'y appliquent, et une violation semée y est détectée.

**Constat**

Le 03/11/2026, avec les paquets construits depuis `main` : `--name my-app` crée `src/my-app/` alors que `.slnx`, les `ProjectReference` et `hardslice.json` visent `my_app` (MSB3202). `--name 1Rooms` échoue de même avec `_1Rooms`. `--name Acme.Rooms` génère `public sealed class Acme.RoomsFactory` (CS1022). `sourceName` sert à la fois de nom de fichier, d'espace de noms, d'assembly et de nom de classe (`MyAppFactory`). `scripts/test-template.sh` ne génère que `Demo` et une solution vierge.

**Périmètre**

- `hardslice-api` : un symbole dérivé du nom, en forme d'espace de noms sûr, remplace `MyApp` dans les contenus et dans les noms de fichiers et de dossiers. Le mécanisme exact est libre tant que toutes les occurrences partagent une forme.
- Aucun nom de type ne dérive du nom de l'application : `MyAppFactory` devient un nom fixe (par exemple `AppFactory`) dans `hardslice-api`, `hardslice-command` et `hardslice-query`.
- Templates d'éléments : leurs fichiers atterrissent dans les projets de la solution avec ces noms (symbole `AppName`, `msbuild:RootNamespace`).
- Analyseurs : `SliceMap` reconnaît les slices par `AssemblyName`. Vérifier qu'avec la forme unique l'assembly et le préfixe des espaces de noms coïncident. Sinon, lire `RootNamespace` (exposé par `CompilerVisibleProperty`), `AssemblyName` en repli.
- Tests d'architecture : vérifier que HS012, HS016 et HS017, qui déduisent le préfixe des espaces de noms du nom d'assembly, ne passent pas à vide avec ces noms.
- `scripts/test-template.sh` : génère `Acme.Rooms` avec Bookings et `my-app` sans, vérifie les deux, y applique `hardslice-module` et `hardslice-command`, et y sème une violation HS016 qui doit échouer.
- `README.md` : la conversion du nom est dite en une phrase.

**Cas vérifiés**

- `Acme.Rooms`, `my-app` et `1Rooms` : solution générée, vérification verte.
- HS016 semée dans `Acme.Rooms` : diagnostic de l'analyseur et échec du test d'architecture.
- `hardslice-command` dans `my-app` : fichiers créés dans `src/my_app/Features/...` et `tests/my_app.*`, vérification verte après complétion de référence.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « nom de l'application » de scripts/test-template.sh
```

### PB-053 : Protéger tous les fichiers qui règlent les analyseurs, à partir d'une seule liste

**Résultat observable**

Un agent qui crée ou modifie un `.globalconfig`, un `Directory.Build.rsp` ou un fichier sous `.agents/` est refusé par le hook avant édition de Claude Code, Codex et Copilot CLI, et la CI générée échoue sur une pull request qui le fait sans le label `hardslice-approved`. La liste des fichiers protégés est la même dans l'outil, `AGENTS.md` et les skills.

**Constat**

`src/HardSlice.Tool/ProtectedFiles.cs` protège `.editorconfig`, `Directory.Build.props`, `Directory.Build.targets` et `Directory.Packages.props`, mais ni `.globalconfig` ni `Directory.Build.rsp`, alors que la spécification, le README et `AGENTS.md` annoncent `.globalconfig` protégé. Le SDK charge tout `.globalconfig` d'un dossier parent d'un fichier compilé, et celui de la solution l'emporte sur `HardSlice.All.globalconfig` (`global_level = -1`) : `dotnet_diagnostic.HS016.severity = none` y éteint la règle, hooks et CI verts. `Directory.Build.rsp` avec `-p:RunAnalyzers=false` fait de même. Les six skills recopient une liste qui diverge : elles annoncent `.agents/` protégé, que rien ne protège, et omettent `.codex/`, `.github/hooks/`, `.githooks/` et le workflow.

**Périmètre**

- `ProtectedFiles` : `.globalconfig` et `Directory.Build.rsp` dans tout dossier, `Directory.Solution.props`, `Directory.Solution.targets` et `nuget.config` à la racine, `.agents/` parmi les dossiers.
- `check` échoue (code `1`, message explicite) quand un des trois projets de tests n'est plus dans `<Name>.slnx` : sinon `dotnet test --no-build` exécute un ancien binaire et le check local est vert à tort.
- Une seule liste écrite : `AGENTS.md` (section « Forbidden ») la porte, les skills y renvoient au lieu de la recopier. `AGENTS.md` reste sous 150 lignes en `--no-sample` (il en a 149 : l'énumération des 22 fiches peut devenir « `docs/rules/<rule>.md` »).
- `dotnet hardslice protected` en mode ligne refuse (code `2`) un chemin cité par git (qui commence par `"`), et son aide recommande `-z`.
- CI générée : `defaults: run: shell: bash` (donc `pipefail`) dans `.github/workflows/hardslice.yml`. `scripts/generated-workflow.sh` reproduit le shell effectif d'une étape, avec ou sans clé `shell`.
- `README.md`, Known limitations : le label `hardslice-approved` vaut aussi pour les commits poussés après sa pose.
- `docs/specifications.md` et `README.md` : liste des fichiers protégés à jour.

**Cas vérifiés**

- Hook avant édition, pour chaque CLI : création de `src/MyApp/.globalconfig`, de `Directory.Build.rsp` et de `.agents/skills/add-slice/SKILL.md` refusée.
- `protected` : ces chemins listés, code `1`. Une ligne citée par git, code `2`.
- Un projet de tests retiré du `.slnx` : `check` échoue avec un message qui le nomme.
- Sonde de `test-template.sh` : un `.globalconfig` qui éteint HS016 fait échouer le job `protected-files` du workflow généré.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « fichiers protégés » et « workflow généré » de scripts/test-template.sh
```

### PB-054 : Rendre les analyseurs sûrs sur du code en cours d'écriture et sur une configuration incomplète

**Résultat observable**

Aucun analyseur ne lève d'exception ni ne fait déborder la pile sur du code qui ne compile pas encore. Un contrat de `hardslice.json` inutilisable produit toujours HS900 au lieu de taire des règles. Les textes figés par la préversion (titres, correctifs) sont lisibles.

**Constat**

- `QueryHandlerTrackingAnalyzer` (`Query.Recognize`) suit une locale vers son initialiseur sans détecter de cycle : `IQueryable<Booking> query = query.Where(...)` dans un handler de requête, erreur banale pendant la frappe, fait déborder la pile et tue le compilateur ou l'hôte d'analyse de l'IDE.
- `Configuration/JsonReader.cs` n'a pas de limite de profondeur : un `hardslice.json` très imbriqué fait de même.
- Le harnais de tests (`AnalyzerHarness.Compile`) exige une compilation sans erreur : aucun test n'exerce les analyseurs sur du code incomplet.
- `DomainModel.cs` ignore sans problème un contrat dont l'assembly n'est ni la compilation ni une référence : un projet renommé fait taire HS004, HS005, HS006 et une partie de HS901 sans HS900.
- En mode `Minimum`, HS901 exige les contrats `command` et `query`, que ce mode ne suppose pas.
- HS016 se déclenche sur un `<see cref>` de commentaire de documentation.
- `SliceStructureAnalyzer` construit un `Lazy` qui capture le jeton d'annulation du démarrage de compilation (exception mise en cache possible dans l'IDE).
- Sans contrats utilisables, chaque slice de réaction reçoit HS004 « declares no endpoint » à côté de HS900.
- La configuration et le modèle de domaine sont recalculés par neuf analyseurs à chaque compilation.
- Le titre de HS004 est long et à la forme négative, et le correctif de HS019 propose toujours `BookingConfirmed`.

**Périmètre**

- Garde de cycle dans `Query.Recognize` et limite de profondeur (64) dans `JsonReader`, HS900 « nesting too deep ».
- Harnais : une variante qui accepte du code en erreur, et un test sur des extraits incomplets (locale auto-référente, générique inachevé, record tronqué, lambda inachevée, `MapGet` sans arguments, `partial` async) qui exige zéro exception d'analyseur.
- HS900 quand la compilation contient `<Assembly>.Features` et qu'un contrat désigne un assembly ni courant ni référencé. Le Domain, sans `Features`, garde le comportement actuel.
- HS901 : la vérification commande et requête ne réclame `command` et `query` que dans les modes qui les supposent. La matrice des niveaux et la fiche suivent.
- HS016 ignore les noms situés dans une trivia structurée (`cref`).
- `Lazy` sans jeton capturé, comme `AggregateReferenceAnalyzer`.
- Contrôle d'endpoint de HS004 sauté quand les contrats de messagerie sont inutilisables.
- Cache de la configuration et du modèle de domaine indexé par compilation (`ConditionalWeakTable`), partagé par les analyseurs. Les seuils de `scripts/measure-analyzers.sh` restent tenus.
- Descripteurs : `helpLinkUri` vers la fiche de la règle, titre de HS004 raccourci et correctif de HS019 qui demande un nom au participe passé sans imposer `BookingConfirmed`. Fiches et `README.md` alignés.

**Limites**

Les duplications entre analyseurs (formats d'affichage, `OperationLocation`, détection des arbres d'expression) sont factorisées plus tard, sans changement de comportement.

**Cas vérifiés**

- Locale auto-référente dans un handler de requête : aucun débordement, aucun AD0001.
- `hardslice.json` imbriqué sur 10 000 niveaux : HS900.
- Contrat `"..., MyApp.Web"` absent : HS900 dans le projet web.
- Solution en `Minimum` sans `command` ni `query` : pas de HS900.
- `<see cref>` vers une autre slice : pas de HS016.
- Toutes les règles : diagnostics inchangés sur la référence Bookings et sur les cas existants.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/measure-analyzers.sh
```

### PB-055 : Figer des contrats publics qui laissent évoluer les versions suivantes

**Résultat observable**

`hardslice.json` porte `"version": 1`, `check` sort avec `0`, `1` ou `2` selon un contrat documenté, une nouvelle alerte de sécurité NuGet ne casse pas la restauration d'une solution générée, la solution générée a un `.gitignore`, et un hook ne laisse jamais passer un agent parce qu'il a lui-même planté.

**Constat**

- `HardSliceConfiguration` refuse toute clé racine inconnue et n'a pas de version : les analyseurs figés refuseraient tout format enrichi plus tard (`baseline` en v0.5, `$schema`). Son commentaire de tête dit le contraire du code.
- `check` renvoie tel quel le code de l'étape qui échoue : `2` veut dire à la fois « argument invalide » et « un test a échoué » (code de Microsoft.Testing.Platform).
- Avec `TreatWarningsAsErrors`, NU1901 à NU1904 deviennent des erreurs de restauration : la première vulnérabilité publiée sur une dépendance épinglée casserait toute nouvelle solution générée par cette version figée.
- La solution générée n'a pas de `.gitignore` : le premier `git add -A` commite `bin/`, `obj/` et `TestResults/` (`scripts/test-template.sh` contourne le manque par `.git/info/exclude`).
- `AgentHook` n'attrape aucune exception imprévue : le processus sort avec un code que Claude Code et Codex lisent comme non bloquant. L'état des relances vit dans `/tmp/hardslice/hooks`, partagé entre les comptes de la machine, et son écriture lève une exception pour le second.
- Après une édition, le hook dit « the build fails » quand le build passe et que seuls des marqueurs `TODO(hardslice):` restent.
- L'empreinte d'un échec ne normalise que les chiffres : un GUID dans une erreur relance le compteur à chaque check.
- L'entrée des hooks est lue dans l'encodage de la console : sous Windows, un chemin non ASCII peut échapper au refus d'un fichier protégé.

**Périmètre**

- `hardslice.json` : `version` exigée (absente ou inconnue : HS900 explicite), `$schema` accepté, le reste strict. Le template l'écrit. Fiche HS900, commentaire de `HardSliceConfiguration` et spécification à jour.
- `check` : `1` pour toute étape en échec, `2` pour l'usage seulement. `check.sh`, `check.ps1`, hooks, aide, README de l'outil et tests suivent.
- `Directory.Build.props` généré : `WarningsNotAsErrors` pour NU1901 à NU1904.
- `.gitignore` dans `hardslice-api` (`bin/`, `obj/`, `TestResults/`, `.vs/`, `.idea/`, `*.user`). `TemplatePackageTests` vérifie qu'il est packé. Le contournement de `test-template.sh` est retiré.
- Hooks : `AgentHook.Run` attrape toute exception. Après édition et en fin de tour, l'échec est rendu à l'agent comme une vérification en échec, avec le message. Avant édition, l'erreur est affichée et l'édition refusée. État des relances dans un dossier propre à l'utilisateur, erreurs d'entrée-sortie attrapées. `dotnet` lancé par `DOTNET_HOST_PATH` quand il est défini.
- Message après édition distinct quand seuls des marqueurs restent : le build passe, N marqueurs `TODO(hardslice):` sont à finir avant de rendre la main.
- Empreinte : GUID et identifiants hexadécimaux normalisés. Plafond de relances par session, toutes empreintes confondues.
- Entrée et sorties des hooks en UTF-8 explicite.

**Cas vérifiés**

- `hardslice.json` sans `version`, avec `"version": 2`, avec `$schema`.
- `check` : argument inconnu `2`, test en échec `1`, marqueurs restants `1`, tout vert `0`.
- Solution générée : `git init && git add -A` ne suit ni `bin/` ni `obj/` après la vérification.
- État des relances non inscriptible : le hook de fin bloque l'agent avec le message.
- Après édition avec marqueurs restants et build vert : message des marqueurs.
- Erreur contenant un GUID différent à chaque passage : le compteur de relances avance.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « modes de check », « hooks » et « pre-commit » de scripts/test-template.sh
```

### PB-056 : Corriger l'application générée et le kit agents avant de les figer

**Résultat observable**

L'application générée et le kit agents ne contiennent plus d'exemple trompeur, d'instruction contradictoire ni de renvoi à un fichier absent, dans les deux variantes.

**Constat et périmètre**

Application générée :
- Une date JSON sans décalage horaire est lue dans le fuseau du serveur (`ConfirmBookingCommand`, `BookingPeriod`) : la refuser (400).
- L'agrégat `Booking` n'a pas de jeton de concurrence optimiste : deux annulations simultanées répondent 204 toutes les deux. Ajouter le jeton (`xmin` de PostgreSQL) et la traduction d'un conflit en 409, avec la migration.
- Commentaires qui disent les événements publiés avant l'enregistrement, alors que `ConfirmBooking` enregistre avant de publier : les corriger.
- Identifiant `BookingId` à la création, `Guid` en lecture et en annulation : un seul type dans les trois slices.
- Dates des tests figées dans le passé et différentes selon les classes : une base unique, dans le futur.
- En `--no-sample`, `TransactionBehavior` et `DomainEventPublisher` n'ont plus de test : un test minimal sans Bookings.
- `.githooks/pre-commit` perd son bit exécutable : post-action `chmod` de `dotnet new`.

Kit agents et templates d'éléments :
- `add-domain-event` dit à l'étape 3 d'écrire à la main la réaction à un événement déjà complété, et l'interdit dans « Never ». Son `--force` réécrit aussi un événement complété : une seule consigne, cohérente avec `hardslice-event`.
- `hardslice-aggregate` accepte un agrégat du nom de son module (CS0118) ou d'un type des usings implicites, comme `Task` (CS0104) : les refuser. `hardslice-command --response` accepte `Id`, `Guid`, `None` et le nom de la slice, qui ne compilent pas ou trompent : les refuser.
- Un module créé avec `--route` : les tests générés par `hardslice-command` et `hardslice-query` appellent la route par défaut (404). `AGENTS.md` et `add-slice` le disent, ou les templates lisent la route du module.
- En `--no-sample`, six fiches de règles et un commentaire de l'agrégat généré renvoient à `ConfirmBooking`, `CancelBooking` ou `BookingConfiguration` : les rendre justes dans les deux variantes.
- HS014 sur un gestionnaire d'événement nomme `add-slice`, qui ne traite pas les réactions. `AGENTS.md` ne parle que des handlers de commande pour HS014.
- `AGENTS.md` décrit `--response none` comme « returns nothing » alors qu'il renvoie un `bool` (404 pour un agrégat inconnu).
- Le test d'agrégat généré n'a pas `using <App>.Domain;`.
- Les skills ne disent pas qu'un refus affiche « created successfully » avant d'échouer avec le code 105 sans rien créer.
- Descriptions de `extract-to-domain` et `fix-architecture-violation` : « when a message names `Skill: ...` ».
- `HS016.md` demande `dotnet build` et `dotnet test` au lieu du script de vérification.
- Le détail de migration propre à Bookings quitte `AGENTS.md` pour un commentaire de la migration.
- `CLAUDE.md`, `GEMINI.md` et `copilot-instructions.md` ne recopient plus la liste des skills ni ce que dit `AGENTS.md`, et disent la même chose des hooks avant `dotnet tool restore`.
- Matcher `Edit|Write` de `.claude/settings.json` : vérifier qu'aucun outil d'édition de la version courante de Claude Code n'y échappe.

**Cas vérifiés**

- `POST /bookings` avec une date sans décalage : 400.
- Deux annulations simultanées : 204 puis 409.
- `hardslice-aggregate --name Task` et un agrégat du nom de son module : refus.
- Module `--route`, commande générée : vérification verte après complétion de référence, ou consigne présente dans `AGENTS.md` et `add-slice`.
- `--no-sample` : aucun fichier livré ne cite un type de Bookings.
- `AGENTS.md` sous 150 lignes dans les deux variantes.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application et des templates d'éléments de scripts/test-template.sh, dans les deux variantes
```

### PB-057 : Publier des démonstrations exactes et sans inventaire local

**Résultat observable**

Les traces de `demos/` ne contiennent plus l'inventaire local des CLI ni de blocs de raisonnement chiffrés, et `scripts/demo-agent-fix.sh verify-reference` et `scripts/demo-add-feature.sh verify-recorded` passent sur les traces filtrées. Les README des démonstrations décrivent exactement leurs conditions.

**Constat**

- `demos/add-feature/README.md` affirme « project settings only », mais le run Codex a chargé une skill personnelle de `~/.agents/skills`, dont le texte est dans la trace.
- Les `trace.jsonl` de Claude Code publient l'inventaire du CLI (outils, plugins, chemins locaux) et des blocs de raisonnement chiffrés.
- `scripts/agent-trace.py` découpe les commandes sur `;`, `|` et les retours à la ligne avant `shlex`, compte `true || dotnet new ...` comme exécuté et une commande Codex sans `exit_code` comme réussie.

**Périmètre**

- `scripts/agent-trace.py` : filtre à l'enregistrement (événements d'initialisation, blocs chiffrés) et découpage des commandes par `shlex` avec ponctuation. Une commande sans code de sortie n'est pas réussie.
- Traces existantes filtrées par le même code, sans relancer les agents.
- `demos/add-feature/README.md` : ce que le run Codex a chargé, et l'isolation complète renvoyée au runner du benchmark (v0.4).
- `demos/README.md` : index des deux démonstrations et de ce qu'elles prouvent.

**Acceptation**

```text
scripts/demo-agent-fix.sh verify-reference "$(mktemp -d)"
scripts/demo-add-feature.sh verify-recorded "$(mktemp -d)"
```

### PB-058 : Réorganiser la documentation publique

**Résultat observable**

Le README racine se lit en une page par un nouveau venu, en anglais. Le détail est dans des pages anglaises de `docs/`. Les documents de conception sont dans `docs/design/`, exacts, sans information personnelle. Le dépôt a ses fichiers de contribution, de sécurité et de notes de version.

**Périmètre**

- `README.md` racine, environ 130 lignes : accroche, diagnostic HS016 montré d'abord, statut, « Getting started » (ancre conservée : les README de paquets y pointent), ce qu'on obtient, agents pris en charge, « Rules » (ancre et tableau conservés, exigés par `ReadmeTests`), preuves (démonstrations, coût mesuré), cinq limites principales, feuille de route, liens.
- Nouvelles pages anglaises : `docs/getting-started.md` (prérequis, étapes 1 à 5, PostgreSQL, migration), `docs/item-templates.md`, `docs/agent-loop.md` (hooks, fichiers protégés, skills, pre-commit, CI générée, approbation Codex), `docs/rules.md` (niveaux d'analyse, limites règle par règle), `docs/README.md` (index).
- `docs/design/` : `specifications.md`, `plan.md` et `product-backlog.md` déplacés, liens relatifs corrigés, bannière. La spécification perd « Pour Vincent », « Monétisation », les indicateurs personnels de la section 14 et « Projet lié ». Spécification, plan et backlog à jour (incréments terminés, prochaine action, « 7 règles d'or » devenues 11, `--result many`). Aucun tiret cadratin.
- `docs/plans/` supprimé.
- `docs/agents/support.md` et `docs/performance/analyzers.md` gardent leur chemin (tests et scripts en dépendent), avec un résumé anglais en tête. `docs/releasing.md` traduit en anglais.
- `CONTRIBUTING.md` (construire, tester, conventions de commit et de ticket, contenu de « Working on HardSlice »), `AGENTS.md` racine suivi qui y renvoie, `SECURITY.md`, `CHANGELOG.md` avec l'entrée `0.3.0-preview.1`.
- Commentaires des scripts et de `release.yml` qui citent des sections du README : pointés vers les nouvelles pages.
- `ReadmeTests` : les liens relatifs de `docs/*.md` sont vérifiés aussi.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
git grep -nP '\x{2014}' -- . ':!demos'   # aucune ligne (tiret cadratin)
```

### PB-059 : Préparer les paquets 0.3.0-preview.1

Statut : terminé le 03/11/2026. Écart : l'en-tête de `AnalyzerReleases.Shipped.md` est `## Release 0.3.0`, car le suivi des versions de Roslyn n'accepte qu'une `System.Version` (RS2007 sur `0.3.0-preview.1`). Les règles de toutes les préversions de 0.3.0 vont sous cet en-tête. NuGet.org ajoute sa signature de dépôt à chaque paquet : la publication et `verify` comparent donc le contenu des paquets, signature exclue (`scripts/nupkg.py digest`), et non les octets.

**Résultat observable**

`Directory.Build.props` porte `0.3.0-preview.1`, les trois paquets ont un README exact, une icône et des liens qui ne dérivent pas, la publication refuse de mélanger deux commits, et le parcours complet de `scripts/test-template.sh` et de `scripts/verify-published.sh` passe sur les paquets locaux. Il ne reste qu'à poser l'étiquette (PB-051).

**Périmètre**

- Version `0.3.0-preview.1` dans `Directory.Build.props`, le README, `docs/releasing.md` et partout où `0.2.0-preview.1` apparaît encore.
- README des trois paquets : HS000 listée, et la nuance « `.editorconfig` l'emporte sur le niveau, sauf HS000 ». Ligne d'installation. Hooks des trois CLI et approbation de Codex dans `/hooks`. Exemple de réaction sans effet externe (`InvoiceOnBookingExtended` au lieu de `ChargeOnBookingExtended`). Liens vers l'étiquette de la version plutôt que `main`, sauf la licence.
- Icône de paquet (`PackageIcon`).
- `AnalyzerReleases.Shipped.md` : les règles sous `## Release 0.3.0-preview.1`, `Unshipped.md` vidé et l'étape ajoutée à `docs/releasing.md`.
- `release.yml` : avant chaque push, une version déjà présente sur NuGet.org doit avoir le SHA-512 de l'artefact, sinon échec. L'étiquette doit toujours viser le commit du run. `verify` compare les paquets servis à l'artefact `packages`. `docs/releasing.md` : rejeter le déploiement en attente avant de reposer une étiquette, et passer à la préversion suivante après un rejet non transitoire.
- `scripts/verify-published.sh` étendu à `hardslice-module` et `hardslice-command` sur la solution générée.
- `ContinuousIntegrationBuild` sur GitHub Actions.
- Passage complet de `scripts/test-template.sh` et de `scripts/verify-published.sh --source artifacts/packages`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
scripts/verify-published.sh 0.3.0-preview.1 "$(mktemp -d)" --source artifacts/packages
```

### PB-060 : Laisser l'application gérer ses propres paquets

Statut : terminé le 04/11/2026. `Directory.HardSlice.props`, protégé à la racine, porte la `GlobalPackageReference` de `HardSlice.Analyzers` et `Directory.Packages.props` l'importe sans condition. En plus de l'import, `check` lit après le build le `obj/project.assets.json` de chaque projet du `.slnx` : le paquet à la version de l'outil, avec ses analyseurs et ses fichiers de build (un `Remove`, un `Update` de version, un `ExcludeAssets` ou un autre `Directory.Packages.props` dans un dossier de projet le font échouer en nommant le projet). `README.md` ne porte pas la liste des fichiers protégés : il n'a pas changé.

**Résultat observable**

Un agent ajoute `CsvHelper` ou `Google.Protobuf` à une solution générée sans toucher un fichier protégé, et la vérification reste verte. Retirer ou modifier la référence à `HardSlice.Analyzers` reste refusé par les hooks et fait échouer `check` et la CI. Les tests d'une solution générée peuvent utiliser `FakeTimeProvider`.

**Constat**

Ghostline, 03/11/2026 (entrées « adding a package needs the protected Directory.Packages.props », « HS010 recommends FakeTimeProvider, which no project can add » et « the frictions of roadmap 1.1 came back ») :

- `Directory.Packages.props` est protégé en entier (`ProtectedFiles.cs`), alors qu'`AGENTS.md` n'interdit que de retirer, modifier ou rétrograder `HardSlice.Analyzers` et sa référence. Toute dépendance de l'application oblige à arrêter la tâche et à expliquer le conflit.
- L'agent a donc écrit à la main un lecteur CSV (RFC 4180) et un lecteur du format binaire de protobuf (environ 200 lignes), avec leurs tests : du code moins sûr que les paquets qu'il remplace.
- `HS010.md` recommande `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`), absent des paquets de la solution et impossible à ajouter. Les tests du collecteur de Ghostline attendent un vrai intervalle de 100 ms, avec une boucle d'attente bornée à 15 secondes.

**Périmètre**

- La `GlobalPackageReference` de `HardSlice.Analyzers` quitte `Directory.Packages.props` pour un fichier protégé (par exemple `Directory.HardSlice.props`) que `Directory.Packages.props` importe. Le mécanisme exact est libre s'il garde la gestion centrale des versions et `CentralPackageTransitivePinningEnabled`.
- `Directory.Packages.props` sort de la liste protégée : `ProtectedFiles`, `AGENTS.md`, spécification, `docs/agent-loop.md`, `README.md`. Le nouveau fichier y entre.
- `check` échoue (code `1`, message qui nomme le fichier) quand l'import a disparu ou qu'un projet de la solution ne reçoit plus `HardSlice.Analyzers` : sans analyseur, aucun HS900 ne peut le dire.
- Le job `protected-files` de la CI générée suit la nouvelle liste.
- `Microsoft.Extensions.TimeProvider.Testing` dans les paquets de la solution, référencé par `<App>.Tests` et `<App>.IntegrationTests`.
- `AGENTS.md` dit en une ligne qu'une dépendance de l'application s'ajoute dans `Directory.Packages.props`, et reste sous 150 lignes en `--no-sample`.

**Cas vérifiés**

- `CsvHelper` ajouté à `Directory.Packages.props` et à `<App>.csproj` : hook accepté, vérification verte.
- Édition du fichier qui porte `HardSlice.Analyzers` : refusée par le hook de chaque CLI, job `protected-files` en échec.
- Import retiré de `Directory.Packages.props` : `check` en échec avec le nom du fichier.
- Un test qui avance un `FakeTimeProvider` compile et passe dans `<App>.Tests`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « fichiers protégés », « hooks » et « workflow généré » de scripts/test-template.sh
```

### PB-061 : Ne restaurer avant le build que lorsqu'un fichier lu par la restauration a changé

Statut : terminé le 04/11/2026. La trace `obj/hardslice.restore` porte l'empreinte SHA-256 du contenu de chaque fichier lu (fichiers de la racine présents et projet), relevée avant la restauration et écrite seulement si elle réussit : un fichier modifié pendant la restauration la relance au build suivant, et un fichier de la racine supprimé aussi (« was removed »). Un fichier est à jour si son contenu est celui de la trace, ou s'il est plus ancien que `project.assets.json`. Le build du check complet, qui restaure, écrit aussi la trace.

**Résultat observable**

Après une modification d'un `.csproj` qui ne change aucun paquet, le hook après édition restaure une fois, puis plus du tout tant qu'aucun fichier lu par la restauration ne change.

**Constat**

Ghostline, 03/11/2026 (entrée « the build hook restores after every edit ») : le hook affiche `Restoring Ghostline.slnx: src/Ghostline/Ghostline.csproj changed since the last restore` après chaque édition d'un `.cs`. `RestoreState.Outdated` compare la date du projet à celle de `obj/project.assets.json`. `dotnet user-secrets init` a modifié le `.csproj` à 18:22, `project.assets.json` date de 17:51, et une restauration qui ne change aucun paquet ne réécrit pas ce fichier : chaque build restaure, sur le budget de 10 secondes du hook. Le commentaire de `RestoreState.cs` présente la limite comme un coût passager, mais elle ne cesse jamais d'elle-même. L'hypothèse de l'entrée (une restauration lancée à la main) n'est pas la cause.

**Périmètre**

- Après une restauration réussie lancée par `check`, une trace propre à HardSlice est écrite pour chaque projet (par exemple `obj/hardslice.restore`), et `Outdated` compare les fichiers lus par la restauration à la plus récente de cette trace et de `project.assets.json`. Le mécanisme est libre, à condition de ne jamais conclure « à jour » quand un fichier lu a changé après la dernière restauration.
- Fichiers lus : ceux de la racine déjà listés, le fichier qui porte `HardSlice.Analyzers` depuis PB-060, et le projet.
- Commentaire de `RestoreState.cs` à jour.

**Cas vérifiés**

- `.csproj` modifié sans changement de paquet : une restauration au premier build après édition, aucune au second.
- Paquet ajouté au `.csproj` : restauration, puis build qui voit le paquet.
- Dossier `obj` supprimé : « has never been restored ».

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « modes de check » et « hooks » de scripts/test-template.sh
```

### PB-062 : Isoler les tests d'intégration des secrets et des services du développeur

Statut : terminé le 04/11/2026. `AppFactory` lance l'application dans l'environnement `IntegrationTests` : les secrets utilisateur, que seul Development charge, ne sont jamais lus, et la chaîne de connexion passe toujours par `UseSetting`. Elle garde ce que Development change pour les tests : validation des enregistrements au démarrage (`ValidateOnBuild`, `ValidateScopes`) et `ThrowOnBadRequest`, dont dépendent les réponses de `BadRequestExceptionHandler`. Elle retire tous les `IHostedService` présents quand `ConfigureTestServices` s'exécute : ce sont ceux de l'application et de ses paquets, ASP.NET Core n'enregistrant les siens qu'après. `KeepHostedService<T>()` en garde un, et refuse un type que l'application n'enregistre pas. Le `UserSecretsId` vient de la liste `guids` de `template.json`.

**Résultat observable**

Une solution générée accepte `dotnet user-secrets set` sans préparation. Ses tests d'intégration ne démarrent aucun service hébergé de l'application et ne lisent aucun secret du développeur, sauf un test qui garde explicitement un service.

**Constat**

Ghostline, 03/11/2026 (entrées « hardslice-api template, no UserSecretsId » et « integration tests run every hosted service of the application ») :

- `hardslice-api` ne génère pas de `UserSecretsId` : `dotnet user-secrets set` échoue avec « Could not find the global property 'UserSecretsId' ». `dotnet user-secrets init` écrit la propriété en première ligne du `.csproj`, avec un BOM, et déclenche le défaut de PB-061.
- `AppFactory` démarre l'hôte complet en environnement Development, où les secrets utilisateur sont chargés, services hébergés compris. Dès l'enregistrement du collecteur de Ghostline, chaque test d'intégration aurait lu les vrais flux NextRip avec la clé du développeur et écrit leurs courses dans la base de test, sans rien signaler. Contournement : un réglage `Realtime:CollectorEnabled`, faux dans `AppFactory`.

**Périmètre**

- `UserSecretsId` dans `<App>.csproj`, un GUID neuf par solution générée.
- `AppFactory` : aucun `IHostedService` enregistré par l'application ne démarre par défaut, et un moyen documenté d'en garder un pour son propre test (méthode ou paramètre de la fabrique, forme libre). Les services d'hébergement qu'ASP.NET Core exige restent.
- Les secrets utilisateur ne sont pas chargés dans les tests d'intégration (environnement dédié ou source de configuration retirée, mécanisme libre), sans perdre la chaîne de connexion de la base de test.
- `docs/getting-started.md` : les secrets locaux en une ligne.
- `AGENTS.md` ou `add-slice` : un service hébergé ne tourne dans les tests d'intégration que si le test le garde.

**Cas vérifiés**

- Solution générée : `dotnet user-secrets set Key value` réussit sans `init`, deux solutions générées ont deux identifiants différents.
- Un `BackgroundService` semé dans la solution : il ne démarre pas dans les tests d'intégration existants, et démarre dans un test qui le garde.
- Un secret utilisateur défini : absent de la configuration vue par un test d'intégration.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application de scripts/test-template.sh, dans les deux variantes
```

### PB-063 : Dire dans le kit agents ce que fait une commande sans paramètre et ce qu'un command handler peut faire au dehors

Statut : terminé le 04/11/2026. `AGENTS.md` le dit dans des paragraphes existants (149 lignes sans l'exemple, comme avant) : une commande sans paramètre est un record vide, sans validateur, et ses tests couvrent un échec du domaine, ou son succès, à la place de la requête invalide. Un command handler lit au dehors par des services enregistrés dans son module (`IHttpClientFactory`, les options du module). Un effet qu'il produit au dehors reste sans conséquence après un rollback, et un effet que le rollback doit annuler (un e-mail, un paiement) est signalé à l'utilisateur. `add-slice` détaille ce qu'il faut retirer de la slice générée en attendant PB-064, et où enregistrer ces services. Les fiches HS013, HS014 et HS018 et le message de HS014 ne demandent ni validateur ni requête invalide à une commande sans paramètre.

**Résultat observable**

Le kit agents ne demande plus ce qu'une règle interdit, et dit ce qu'un command handler peut faire hors de sa transaction.

**Constat**

Ghostline, 03/11/2026 (entrées « hardslice-command, no option for a command without parameters » et « external effects of a command handler are not documented ») :

- `AGENTS.md` demande pour chaque slice des tests « an invalid request included ». Une commande sans paramètre n'a pas de requête invalide, et HS013 interdit un validateur vide. L'agent a dû trancher seul entre deux consignes, deux fois (`ImportSchedule`, `CollectRealtimeFeeds`).
- `AGENTS.md` interdit les effets externes dans une réaction, parce qu'ils survivent à un rollback, mais ne dit rien d'un command handler. `ImportSchedule` télécharge une archive et l'écrit sur disque : l'agent n'a pas pu savoir si c'était permis, ni où placer le client HTTP et l'accès aux fichiers.

**Périmètre**

- `AGENTS.md` et `add-slice` : une commande sans paramètre n'a pas de validateur, et ses tests couvrent un échec du domaine (ou son succès) à la place de la requête invalide.
- `AGENTS.md` : un command handler peut lire au dehors (téléchargement, fichier) par des services injectés, comme `IHttpClientFactory` et les options de son module. Un effet qu'il produit au dehors doit rester sans conséquence si la transaction est annulée (idempotent, comme un fichier nommé par son empreinte). Un effet qui ne le peut pas (un e-mail, un paiement) n'a pas encore de mécanisme dans le template : l'agent le signale à l'utilisateur au lieu de l'écrire.
- `AGENTS.md` reste sous 150 lignes en `--no-sample`. Si la place manque, le détail va dans `add-slice` et `AGENTS.md` y renvoie.

**Cas vérifiés**

- `AGENTS.md` des deux variantes : les deux consignes présentes, sous la limite de lignes.
- Aucune consigne du kit agents (`AGENTS.md`, skills, fiches HS013 et HS014) n'exige de validateur ni de test de requête invalide pour une commande sans paramètre.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « instructions des agents » de scripts/test-template.sh
```

### Définition de fini

L'incrément est terminé lorsque PB-052 à PB-063 sont satisfaits et que les jobs CI build, template, démonstration et performance sont verts sur `main`. La publication attend la section 7 : PB-051 est lancé après PB-080, qui revalide les paquets après PB-075 à PB-079, sur le feu vert du mainteneur.

## 7. Incrément actif, suite : Retours de Ghostline avant la publication

Statut : découpé le 04/11/2026 à partir des frictions relevées en construisant Ghostline, la première application réelle sur la préversion (`ghostline/docs/hardslice-friction.md`, entrées du 03/11/2026), triées par pertinence et priorité. Décision du mainteneur du 04/11/2026 : ces tickets passent avant PB-051, pour que la première préversion publiée soit la plus stable possible (une version publiée sur NuGet.org ne se remplace jamais). Ils suivent PB-063, et PB-074 revalide les paquets après eux. PB-075, ajouté le même jour à partir d'une entrée du 04/11/2026, change le texte d'un diagnostic après PB-074. PB-076, ajouté le même jour à partir de l'entrée « no path to bring a generated solution to a newer template », change le texte des erreurs de l'outil sur les fichiers HardSlice. PB-077, ajouté le même jour à partir de l'entrée « the build hook reports the files not yet edited of a planned change », change la réponse du hook après une édition. PB-078, ajouté le même jour à partir de l'entrée « the Stop hook relaunches the agent on a failure only a protected file can fix », change la réponse du hook de fin de tour quand la vérification s'arrête sur un fichier HardSlice protégé. Un ticket de revalidation des paquets les suit.

Complété le 04/11/2026 par PB-079 à PB-086, tirés des six tâches encore ouvertes issues des entrées du 04/11/2026 (authentification dans `Shared` et réponses du middleware, endpoint protégé, service hébergé non périodique, condition de démarrage d'un job, migration qui convertit des lignes, mise à niveau d'une solution générée), et des entrées sans tâche qui leur sont liées. Leurs arbitrages sont validés par le mainteneur le 04/11/2026 (bloc suivant). PB-079 corrige avant PB-051 des textes que la préversion figerait sous une forme trompeuse. PB-085, une consigne de plus, est avancé avant PB-080 par décision du mainteneur : il rejoint la seule reconstruction des paquets sans purge de plus pour Ghostline. PB-080 revalide les paquets après PB-075 à PB-079 et PB-085, et PB-051 en dépend. PB-081 à PB-084 et PB-086 ajoutent sans rien corriger de figé : ils devaient passer après PB-051 et former l'incrément `0.3.0-preview.2`.

Décision du mainteneur du 04/11/2026, après PB-080 : aucune version n'est publiée sur NuGet.org pour l'instant, PB-051 compris. Le but est la version la plus stable possible, quel que soit son numéro, et Ghostline sert à itérer tout de suite. PB-081 à PB-084 et PB-086 ne dépendent donc plus de PB-051 et passent maintenant, avec PB-087, tiré de l'entrée « the 409 on a unique violation does not cover a command that merges into existing aggregates » (arbitrage 8, option B validée le même jour). PB-088 revalide ensuite les paquets, toujours sous `0.3.0-preview.1`, et Ghostline se met à niveau sur son commit avec `dotnet hardslice upgrade` (PB-086). Le critère de placement des arbitrages ci-dessous (avant ou après PB-051) ne décide plus que de l'ordre des tickets.

Complété le 05/11/2026 par PB-090 à PB-097, tirés des sept entrées du 04/11/2026 que `ghostline/docs/hardslice-friction.md` laisse ouvertes après la mise à niveau de Ghostline sur les paquets de PB-088 (d78ba83) : la fusion à trois voies de `dotnet hardslice upgrade`, que la section 9 attendait d'arbitrer sur une vraie mise à niveau, la sortie de `upgrade`, la condition d'une commande planifiée, le test du jeton de concurrence, la constante d'une politique, `--help` sur chaque commande et le texte de HS006 sur une lecture sans entité. Leurs arbitrages (9 à 13) sont validés par le mainteneur le 05/11/2026. PB-097 revalide les paquets après eux, toujours sous `0.3.0-preview.1`.

### Résultat attendu

> Les deux modules de Ghostline se construisent avec les templates et les skills, sans réécrire de code généré ni écrire à la main ce qu'un template devrait produire : commande sans paramètre, route choisie, entité enfant, commande lancée par une tâche de fond, requête filtrée. Les cas avancés rencontrés (unicité sous concurrence, règle du domaine dans une requête, grandes collections, écritures en masse) ont une consigne écrite.

### Arbitrages de l'incrément

Validés par le mainteneur le 04/11/2026 :

- **Une commande système n'a pas d'endpoint HTTP.** Un contrat de messagerie dédié, `ISystemCommand<TResponse>`, qui dérive de `ICommand<TResponse>`, marque une commande envoyée seulement par l'application (commande récurrente, service hébergé). Elle traverse le même pipeline qu'une commande. Sa clé `systemCommand` dans `hardslice.json` est facultative : une application sans travail en arrière-plan n'a rien à déclarer. HS004 n'exige pas d'endpoint pour elle, signale un endpoint qui la mappe, et signale une commande système qu'aucun type de son module ne référence hors de son handler et de son validateur (construction ou argument générique). Une commande ordinaire garde l'endpoint obligatoire. Le nom évite `Internal`, que les messages confondraient avec le mot-clé `internal` des handlers. La reconnaissance reste par symbole, jamais par attribut ni par forme.
- **Le travail périodique passe par un seul service générique de `Shared/Messaging`,** enregistré par le module avec `services.AddRecurringCommand<TCommand>(interval)` (forme exacte libre), pour une commande système sans paramètre (`TCommand : ISystemCommand<TResponse>, new()`). Il porte une seule fois le comportement délicat : un scope et un envoi par `IMediator` par cycle, une exception d'un cycle journalisée sans arrêter l'hôte, un `PeriodicTimer` construit avec `TimeProvider`. `hardslice-command --every <interval>` génère la commande et son enregistrement. Pas de template `hardslice-job` : un service hébergé non périodique (consommer une file, par exemple) est seulement documenté, et son template est différé.
- **Aucune aide d'écriture en masse ni de verrou consultatif n'entre dans `Shared/Persistence`.** L'écriture en masse devrait suivre les convertisseurs, les types Npgsql et les propriétés cachées du mapping, pour un cas rare. Le verrou tient en une ligne de SQL, et le 409 de PB-069 couvre le cas courant. Ces besoins sont documentés, pas générés.
- **La route par défaut reste tirée du nom de la slice.** Elle ne peut pas entrer en collision avec celle d'une autre slice du module, alors qu'un défaut à la REST donnerait le même `GET /{id:guid}` à deux requêtes par identifiant, une ambiguïté visible seulement à l'exécution. La skill `add-slice` demande de toujours passer `--route`, choisie d'après la ressource.
- **Aucune compatibilité à garder.** Rien n'est publié avant ces tickets : un nom ou une forme générée peut changer sans précaution pour des solutions existantes (noms des DTO de PB-070 compris).

### Arbitrages validés le 04/11/2026

Pour PB-079 à PB-086. Validés par le mainteneur le 04/11/2026 : les recommandations ci-dessous, avec deux écarts. PB-085 passe avant PB-080, donc avant PB-051 (point 6). PB-086 se limite à l'option B, la fusion à trois voies de l'option C étant différée en section 9 (point 7). Critère de placement : un ticket passe avant PB-051 s'il corrige un texte, un format ou un contrat que la préversion figerait sous une forme fausse ou trompeuse. Un ajout (option, template, consigne, commande) passe après : il arrive dans `0.3.0-preview.2`, et une solution générée en `0.3.0-preview.1` le reçoit par la mise à niveau de PB-086. Une version publiée sur NuGet.org ne se remplace jamais, mais chaque reconstruction sous la même version oblige Ghostline à purger ses caches et à refaire sa migration : les tickets d'avant PB-051 passent tous avant la seule reconstruction, celle de PB-080, nécessaire de toute façon puisque PB-075 à PB-078 n'ont pas reconstruit les paquets.

1. **Dossier de `Shared` pour l'authentification (PB-079).** `hardslice.json` autorise `Messaging`, `Persistence` et `Behaviors`. Son format est figé par la publication, et sa valeur générée l'est en pratique : le fichier est protégé, et une solution générée en `0.3.0-preview.1` ne reçoit un dossier ajouté plus tard que par un mainteneur ou par la mise à niveau.
   - Option A : un quatrième dossier autorisé par défaut, `Http`, pour le pipeline HTTP. Il porte l'authentification, les politiques d'autorisation, la limitation de débit et CORS, et reçoit les réponses HTTP aux requêtes illisibles et aux erreurs, qui quittent `Behaviors` (`BadRequestExceptionHandler`, `ValidationExceptionHandler`, `DomainExceptionHandler`, `ExplicitOffsetJsonConverter`). `Behaviors` ne garde que le pipeline du médiateur. Le format de `hardslice.json` ne change pas, seule sa liste générée.
   - Option B : le même dossier `Http`, vide à la génération, les réponses HTTP restant dans `Behaviors`. Moins de fichiers déplacés, mais deux dossiers pour du code HTTP, sans règle pour choisir.
   - Option C : `hardslice.json` inchangé, et le rôle de `Behaviors` élargi à « the HTTP and mediator pipelines » dans `HS017.md`, `AGENTS.md` et la spécification, avec l'authentification en exemple. Aucun code déplacé, mais un nom que le lecteur prend pour `IPipelineBehavior<,>`, comme Ghostline l'a relevé.
   - Recommandation : A, sous le nom `Http` (`Security` ne couvrirait pas les réponses aux erreurs). Rien n'étant publié, aucune compatibilité n'est à garder, et c'est le seul moment où le déplacement ne coûte rien aux solutions existantes. Avant PB-051 : la liste générée, la disposition de `Shared`, `HS017.md` et `AGENTS.md` seraient figés sous la forme que Ghostline a trouvée trompeuse.
2. **Réponses du middleware et HS015 (PB-079).** `HS015.md` dit que `TypedResults` mettent toutes les réponses de l'endpoint dans sa signature, et que le module porte les politiques de ses endpoints par son groupe.
   - Option A : un paragraphe de `HS015.md` sur les réponses du middleware (401, 403, 429). La politique va sur le groupe quand elle couvre tout le module, sur l'endpoint dans sa slice sinon, et un test d'intégration du 401 tient lieu de la signature. La phrase sur la signature est restreinte aux réponses que renvoie le délégué. Aucun message de diagnostic ne change.
   - Option B : A, plus une règle qui exige `RequireAuthorization` ou `AllowAnonymous` sur un endpoint d'écriture dès qu'une politique existe.
   - Recommandation : A, avant PB-051, puisque la fiche figerait une affirmation fausse. La règle de B est différée (section 9) : l'existence d'une politique se lit à l'exécution (`AddAuthorization`, politique par défaut, `FallbackPolicy`), un seul cas réel la demande, et `FallbackPolicy`, que le paragraphe de A documente, protège déjà tout endpoint qui ne dit pas `AllowAnonymous`.
3. **Endpoint protégé généré (PB-081).**
   - Option : `--require <policy>` sur `hardslice-command` et `hardslice-query`, qui chaîne `.RequireAuthorization` à l'endpoint dans sa slice, génère le test du 401 à côté de celui du 400, et fait passer les autres tests générés par un client authentifié.
   - Client authentifié, option (a) : `AppFactory`, que l'application possède, rend le client d'une politique à partir d'une table qu'elle remplit une fois, et échoue avec un message qui dit où l'ajouter quand la politique n'y est pas.
   - Client authentifié, option (b) : `AppFactory` remplace les schémas de l'application par un schéma de test. Le vrai schéma n'est alors plus testé, alors que Ghostline teste le 401 d'une mauvaise clé.
   - Client authentifié, option (c) : rien de généré, un marqueur `TODO(hardslice):` à la place du client.
   - Consigne : une skill `add-authorization` (schéma, politique, client de test), ou une section « Protected endpoints » de `add-slice`.
   - Recommandation : `--require`, le client (a) et la section de `add-slice`. Pas de septième skill : le schéma s'écrit une fois par application, en ASP.NET Core standard, et PB-079 dit où il va. Après PB-051 : une option, un client de test et une consigne s'ajoutent sans rien corriger de figé. Dépend de PB-079.
4. **Service hébergé non périodique (PB-082).** Élément de la section 9, « à arbitrer après PB-068 ». Le cas de Ghostline, `ScheduleImporter`, est une heure quotidienne dans un fuseau, avec des reprises après un échec.
   - Option A : étendre `AddRecurringCommand` d'un horaire quotidien (heure locale et fuseau IANA, instant suivant calculé depuis `TimeProvider`) et de reprises après un cycle en échec (délai, nombre), avec des options de `hardslice-command` comme `--daily-at <hh:mm> --time-zone <id> --retry <délai>x<nombre>` (noms et forme libres). Le service générique de `Shared/Messaging` garde seul le comportement délicat, et `RecurringCommandCycles` avance l'horloge jusqu'à l'instant dû, quel que soit l'horaire.
   - Option B : un template `hardslice-worker` qui génère un `BackgroundService` dans la slice (garde de l'interrupteur, scope et `Send`, échec journalisé, annulation à l'arrêt, ligne d'`AppFactory`, test d'intégration). Il couvre tout déclencheur, mais recopie dans chaque application le comportement que PB-068 a mis une seule fois dans `Shared`, et ses tests demandent une horloge qui expose ses minuteries (la `WaitingClock` de Ghostline).
   - Option C : A et B.
   - Commande envoyée par l'horaire : une commande système seulement, comme `AddRecurringCommand` aujourd'hui, ou toute commande sans paramètre. `ImportSchedule` de Ghostline est envoyée par la nuit et par son endpoint d'administration.
   - Recommandation : A, pour toute commande sans paramètre, système ou non. Elle remplace `ScheduleImporter` et sa `WaitingClock` sans code délicat dans l'application. Le template d'un service qui consomme une file, ou de tout déclencheur autre qu'une horloge, reste différé (section 9) : aucun cas réel ne le demande encore. La puce « Background work » d'`AGENTS.md` suit dans tous les cas. Après PB-051 : une surcharge et des options s'ajoutent, et la puce actuelle n'est pas fausse.
5. **Condition de démarrage d'un job, et HS003 ou HS006 sur les services hébergés d'une slice (PB-083, PB-084).**
   - Condition, option A : documenter seulement la lecture de Ghostline (`AppDbContext` en `AsNoTracking()`, dans le scope du service, dans le dossier de la slice).
   - Condition, option B : une condition facultative du service générique de PB-082, évaluée dans un scope à elle avant un envoi, qui retient la commande et journalise une seule fois pourquoi. Elle couvre la clé manquante de `CollectRealtimeFeeds` (entrée « AddRecurringCommand has no start condition ») et l'import au démarrage de `ScheduleImporter` quand aucune version ne couvre le jour.
   - Règle, option A : étendre HS003 aux services hébergés d'une slice, qui n'accèdent plus du tout à la base. La lecture de la condition devrait alors passer par une requête, qu'une slice ne peut ni ajouter (HS004) ni emprunter à une autre (HS016).
   - Règle, option B : étendre HS006 aux services hébergés d'une slice et aux conditions de PB-083 : ils lisent sans suivi et n'écrivent jamais. La lecture de Ghostline passe, une écriture hors de la commande est signalée.
   - Règle, option C : une nouvelle règle, avec sa fiche et son identifiant.
   - Recommandation : condition B, règle B. HS006 porte déjà la détection des écritures et du suivi, et ce qu'un job doit garantir est ce qu'elle garantit d'un handler de requête. Après PB-051 : la condition est un ajout, et l'extension de la règle ajoute des diagnostics sur du code accepté jusque-là, ce qu'une préversion suivante peut faire, sans qu'aucun texte figé soit faux. PB-083 dépend de PB-082, et PB-084 de PB-083, pour que la règle accepte la forme sanctionnée.
6. **Migration qui convertit des lignes, et mapping de champs privés (PB-085).**
   - Option A : un paragraphe « a migration that changes a table with rows » dans « Changing the database » d'`AGENTS.md`. Le fichier généré fait 149 lignes sans l'exemple, pour une limite de 150 : le paragraphe ne tient pas sans retirer autant ailleurs.
   - Option B : une skill `reshape-entity` (nouvelle forme du domaine, mapping, migration qui convertit dans les deux sens, délai de commande), ou une option `--reshape` de `hardslice-entity`.
   - Option C : deux sections de `add-aggregate`, qui porte déjà le mapping et la migration d'un agrégat : « Changing the shape of stored rows » et « Mapping private fields ». `AGENTS.md` y renvoie dans une phrase existante.
   - Recommandation : C. Après PB-051 : c'est une consigne de plus, et la skill actuelle n'est pas fausse. Comme PB-080 reconstruit les paquets de toute façon, le mainteneur peut l'avancer avant PB-051 sans purge de plus pour Ghostline, au prix d'un ticket de plus avant la publication.
   - Décision du mainteneur : C, avancé avant PB-080. Le piège qu'il documente fait perdre des données, et il ne coûte aucune reconstruction de plus.
7. **Périmètre de `dotnet hardslice upgrade` (PB-086).**
   - Option A : aucune commande. La consigne de PB-076 reste, et chaque changement de la disposition générée reçoit une nouvelle version, ce que la publication rend automatique.
   - Option B : `upgrade` réécrit les fichiers que le template possède (fichiers protégés, fiches des règles, skills, hooks, scripts, tests d'architecture) depuis une génération à la version de l'outil, sous le nom et la variante de la solution, et liste avec leur diff les fichiers que l'application possède et qui diffèrent, à fusionner à la main ou par l'agent.
   - Option C : B, plus une fusion à trois voies des fichiers que le template a générés et que l'application a changés (`AGENTS.md`, `Program.cs`, `AppFactory.cs`, projets, `Shared/`). La base est la génération du template à la version de la solution, restaurée depuis NuGet.org où elle ne change jamais, et un conflit reste marqué dans le fichier. Elle règle aussi l'entrée « AGENTS.md mixes the text of the template and the text of the application » sans marqueurs dans `AGENTS.md`.
   - Ce qui devrait passer avant PB-051 : rien, si `upgrade` lit la version de la solution dans `Directory.HardSlice.props`, protégé, et reçoit la variante (`--no-sample`) ou la déduit. Écrire la version et les options du template dans un fichier de la solution figerait un fichier de plus, et une entrée de plus de la liste protégée, pour une information que la commande peut lire ou demander.
   - Recommandation : C, après PB-051, dans `0.3.0-preview.2`. Le premier cas réel est le passage d'une solution `0.3.0-preview.1` à `0.3.0-preview.2`, et la base d'une fusion n'existe que pour une version publiée. Une solution générée par une construction locale non publiée, comme Ghostline aujourd'hui, se migre à la main.
   - Décision du mainteneur : B d'abord, après PB-051. La réécriture des fichiers que le template possède et la liste avec diff des autres règlent l'essentiel d'une migration, sans restaurer de génération d'origine. La fusion à trois voies de C est différée (section 9), à arbitrer une fois B utilisé sur une vraie mise à niveau.
8. **Le 409 d'un index unique et une modification concurrente (PB-087).** Entrée « the 409 on a unique violation does not cover a command that merges into existing aggregates » (04/11/2026). Le 409 de PB-069 couvre deux créations concurrentes du même agrégat. Deux commandes qui modifient un agrégat déjà stocké s'écrasent l'une l'autre sans erreur : seul l'exemple `Booking` a un jeton de concurrence, et `add-slice` dit que « the index and the 409 cover the others ».
   - Option A : une consigne seule, dans `AGENTS.md`, `add-slice` et `add-aggregate`. Le 409 d'un index unique ne couvre que les créations, une modification concurrente demande un jeton de concurrence (`xmin`), et une fusion qui ne doit jamais échouer garde le verrou de transaction.
   - Option B : A, plus un jeton `xmin` généré par défaut par `hardslice-aggregate`, comme celui de `Booking`. Une modification concurrente d'un agrégat généré répond 409 au lieu de perdre une écriture.
   - Option C : A, plus une règle qui signale une entité mappée sans jeton. Bruyante, car beaucoup d'agrégats ne sont jamais modifiés en concurrence.
   - Décision du mainteneur du 04/11/2026 : B. La perte silencieuse est le pire cas, `TransactionBehavior` traduit déjà le conflit en 409, et `xmin` ne coûte rien en PostgreSQL.

Entrées sans tâche reprises : « RecurringCommandCycles takes a bare AppFactory, with no way to configure the instance it builds » par PB-082, « FakeTimeProvider does not say which timers wait on it » par PB-082 pour une commande planifiée (l'horloge générique reste différée avec le template d'un service non périodique), « AddRecurringCommand has no start condition, so a missing key fails every cycle » et « AppFactory also removes the hosted services that only check the configuration at start » par PB-083, « AGENTS.md mixes the text of the template and the text of the application » par PB-086 en partie (le diff d'`AGENTS.md` est listé), et par la fusion différée en section 9 pour le reste.

| Point | Ticket | Placement | Raison |
|---|---|---|---|
| Dossier `Shared/Http` | PB-079 | avant PB-051 | liste générée de `hardslice.json`, disposition de `Shared`, `HS017.md` et `AGENTS.md` figés sous une forme trompeuse |
| Réponses du middleware | PB-079 | avant PB-051 | `HS015.md` figerait une affirmation fausse sur la signature |
| Règle d'autorisation déclarée | aucun | section 9 | un seul cas réel, existence d'une politique lue à l'exécution |
| Paquets revalidés | PB-080 | avant PB-051 | PB-075 à PB-079 non reconstruits, l'étiquette de PB-051 vise ce commit |
| `--require` et client authentifié | PB-081 | après PB-080 | ajout, dépend de PB-079 |
| Heure quotidienne et reprises | PB-082 | après PB-080 | ajout au service générique, la consigne actuelle n'est pas fausse |
| Condition de démarrage | PB-083 | après PB-080 | ajout, dépend de PB-082 |
| HS006 sur les services hébergés | PB-084 | après PB-080 | nouveaux diagnostics, dépend de PB-083 |
| Migrations et champs privés | PB-085 | avant PB-080, donc avant PB-051 | décision du mainteneur : consigne contre une perte de données, sans purge de plus puisqu'elle rejoint PB-080 |
| `dotnet hardslice upgrade` (option B) | PB-086 | après PB-080 | ajout d'une commande, la consigne de PB-076 suffit d'ici là |
| Jeton de concurrence des agrégats | PB-087 | après PB-080 | consigne fausse dans `add-slice`, écriture perdue sans erreur |
| Paquets revalidés | PB-088 | après PB-081 à PB-087 | Ghostline se met à niveau sur ce commit |
| Fusion à trois voies de `upgrade` | aucun | section 9 | la base de la fusion n'existe que pour une version publiée, à arbitrer après PB-086 |

### Critères communs à tous les tickets de l'incrément

- Les critères communs des sections 5 et 6 restent applicables.
- Chaque template ou option ajouté a sa sonde dans `scripts/test-template.sh` (génération, complétion de référence, vérification verte, refus des combinaisons invalides avec le code 105), son entrée dans `docs/item-templates.md` et sa consigne dans la skill qui le concerne.
- Chaque ticket cite l'entrée de `ghostline/docs/hardslice-friction.md` qu'il ferme, et dit dans son résumé le contournement que Ghostline peut retirer.

### Graphe des dépendances

| Ticket | Dépend de | Peut avancer en parallèle de |
|---|---|---|
| PB-064 (commande sans paramètre) | PB-063 | PB-066, PB-069, PB-071 |
| PB-065 (route et verbe) | PB-064 | PB-066, PB-069, PB-071 |
| PB-066 (entités enfants) | PB-063 | PB-064, PB-065, PB-069 |
| PB-067 (commande système) | PB-064 | PB-066, PB-069 |
| PB-068 (commande récurrente) | PB-060, PB-062, PB-067 | PB-069 à PB-071 |
| PB-069 (unicité sous concurrence) | PB-063 | PB-064 à PB-068 |
| PB-070 (requête filtrée) | PB-065 | PB-068, PB-069 |
| PB-071 (HS006 et règles du domaine) | PB-063 | PB-064 à PB-070 |
| PB-072 (écritures en masse) | PB-066 | PB-071 |
| PB-073 (`.gitkeep` des modules) | PB-066 | PB-072 |
| PB-074 (paquets revalidés) | PB-060 à PB-073 | aucun |
| PB-075 (HS902 et objets valeur) | PB-074 | aucun |
| PB-076 (`Fix:` et `Doc:` des erreurs de l'outil) | PB-075 | aucun |
| PB-077 (erreurs des fichiers édités d'abord) | PB-076 | aucun |
| PB-078 (fin de tour sur un fichier HardSlice protégé) | PB-076, PB-077 | aucun |
| PB-079 (`Shared/Http` et réponses du middleware) | PB-078 | aucun |
| PB-080 (paquets revalidés) | PB-075 à PB-079, PB-085 | aucun |
| PB-081 (endpoint protégé, `--require`) | PB-079, PB-080 | PB-082, PB-086, PB-087 |
| PB-082 (heure quotidienne et reprises) | PB-080 | PB-081, PB-086, PB-087 |
| PB-083 (condition de démarrage) | PB-082 | PB-081, PB-086, PB-087 |
| PB-084 (HS006 sur les services hébergés d'une slice) | PB-083 | PB-081, PB-086, PB-087 |
| PB-085 (migrations qui convertissent des lignes) | PB-079 | aucun |
| PB-086 (`dotnet hardslice upgrade`) | PB-080 | PB-081 à PB-084, PB-087 |
| PB-087 (jeton de concurrence des agrégats) | PB-080 | PB-081 à PB-084, PB-086 |
| PB-088 (paquets revalidés) | PB-081 à PB-087 | aucun |
| PB-089 (groupes de `test-template.sh`) | PB-088 | aucun |
| PB-090 (HS006 et une lecture sans entité) | PB-089 | aucun |
| PB-091 (`--help` et `-h`) | PB-089 | aucun |
| PB-092 (constante d'une politique) | PB-089 | aucun |
| PB-093 (test du jeton de concurrence) | PB-089 | aucun |
| PB-094 (condition et cycle au démarrage) | PB-090 | aucun |
| PB-095 (fusion à trois voies de `upgrade`) | PB-090 à PB-094 | aucun |
| PB-096 (sortie de `upgrade`) | PB-095 | aucun |
| PB-097 (paquets revalidés) | PB-090 à PB-096 | aucun |

Le dépôt n'a qu'un agent : les tickets passent l'un après l'autre, dans l'ordre PB-079, PB-085, PB-080, puis PB-081, PB-082, PB-083, PB-084, PB-087, PB-086 et PB-088. PB-086 passe après les autres pour que sa liste des fichiers du template et sa sonde voient tout ce qu'ils ajoutent. Puis PB-089, et PB-090 à PB-097 dans l'ordre de leur numéro : PB-095 passe après les tickets qui changent le template, pour que la base qu'il enregistre et sa sonde les voient. PB-051 est suspendu par décision du mainteneur du 04/11/2026.

### PB-064 : Générer une commande sans paramètre avec `hardslice-command --parameters none`

Statut : terminé le 04/11/2026. Ferme les entrées « hardslice-command, no option for a command without parameters » et « the frictions of roadmap 1.1 came back » (pour sa commande) de `ghostline/docs/hardslice-friction.md`. `--parameters` est un choix, `id` par défaut (fichiers et lignes imprimées identiques à ceux d'avant, vérifié contre le template de HEAD) ou `none` : record vide, endpoint sans corps, ni validateur ni tests de validation, enregistrement du handler seul, test d'intégration du succès qui poste sans corps. `--parameters none --response none` est refusé (code 105, rien de créé) : le 404 répond à un agrégat inconnu que la commande ne peut pas nommer. Une valeur inconnue est refusée par `dotnet new` (code 127), comme `--result` de `hardslice-query`. `add-slice` ne dit plus quoi supprimer de la slice générée : il renvoie vers l'option, comme `AGENTS.md` (142 et 149 lignes, inchangé). Contournement que Ghostline peut retirer : aucun code à changer dans `ImportSchedule` ni `CollectRealtimeFeeds`, déjà réécrits, mais ses prochaines commandes sans paramètre se génèrent avec `--parameters none` au lieu de supprimer le validateur, ses tests et le test du 400, puis de réécrire l'endpoint.

**Résultat observable**

`dotnet new hardslice-command --module Schedule --name ImportScheduleCommand --parameters none` génère une commande sans paramètre, sans validateur ni tests de validation, un endpoint qui ne lit pas de corps, et un test d'intégration qui ne vérifie pas de 400. La vérification est verte après complétion de référence.

**Constat**

Ghostline, entrées « hardslice-command, no option for a command without parameters » et « the frictions of roadmap 1.1 came back » : pour `ImportSchedule` puis `CollectRealtimeFeeds`, le template a généré un paramètre `Guid Id`, un validateur, ses tests unitaires, un endpoint qui lit la commande dans le corps et un test d'intégration du 400. Tout a été supprimé ou réécrit. Tant que ce n'était pas fait, chaque édition faisait échouer le build du hook sur les tests générés, qui ne compilaient plus.

**Périmètre**

- `hardslice-command` : option `--parameters` (`id` par défaut, comportement actuel, et `none`), combinable avec `--response`. Une combinaison sans sens est refusée avant de créer un fichier, comme les refus de PB-056 (par exemple `--response none`, qui répond 404 à un agrégat inconnu, sans identifiant pour le chercher), ou reçoit un sens documenté.
- Avec `none` : la commande est un record vide, l'endpoint n'a pas de corps, aucun validateur ni test de validation, et le test d'intégration généré couvre le succès ou un échec du domaine.
- `add-slice`, `docs/item-templates.md` et `AGENTS.md` (sous 150 lignes) : l'option, et la consigne de PB-063 qui renvoie vers elle.

**Cas vérifiés**

- `--parameters none` avec chaque valeur acceptée de `--response` : vérification verte après complétion de référence, aucun validateur généré.
- Combinaison refusée : code 105, rien de créé.
- Sans l'option : fichiers identiques à ceux d'aujourd'hui.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-065 : Choisir la route et le verbe d'une slice à la génération

Statut : terminé le 04/11/2026. Ferme les entrées « hardslice-command and hardslice-query, routes named after the slice » et « the frictions of roadmap 1.1 came back » (pour la route de `CollectRealtimeFeeds`) de `ghostline/docs/hardslice-friction.md`. `--route` est relatif au groupe du module : mots en minuscules séparés par des tirets et paramètres de route entre accolades (`{roomId:guid}`, contraintes et `?` compris), séparés par des barres obliques, `/` pour le groupe lui-même, une barre initiale retirée, une valeur vide revenant au défaut, le nom de la slice en kebab-case. `--method` est insensible à la casse : `POST` par défaut, `PUT`, `PATCH` ou `DELETE` pour une commande, `GET` seul pour une requête. Un `DELETE` n'a pas de corps (ASP.NET Core refuse d'y inférer la commande) : avec `--parameters id`, son endpoint lit l'`Id` de la route, suivie de `/{id:guid}` comme pour une requête `single`. Refus (code 105, rien de créé) : route invalide, route qui nomme déjà le paramètre `id` que le template ajoute, `GET` pour une commande avec un message qui renvoie vers `hardslice-query`, verbe inconnu, autre verbe que `GET` pour une requête. Le module ne pouvant être lu par `dotnet new` (une propriété MSBuild construite sur des items n'est pas développée à l'évaluation), le contrat d'un module porte sa route de groupe dans `public const string Route`, que `hardslice-module` et `BookingsModule` écrivent et que `MapGroup` utilise : les tests générés appellent `<Module>Module.Route + "/<route>"`, et un module créé avec son propre `--route` n'a plus de test à corriger. Un paramètre de route autre que l'`{id:guid}` du template reste entre accolades dans `Route`, avec un marqueur `TODO(hardslice):`. `add-slice` et `AGENTS.md` (141 et 149 lignes) demandent de toujours passer `--route`, choisie d'après la ressource, et ne disent plus de corriger `Route` des tests. Contournement que Ghostline peut retirer : aucune route réécrite à la main pour ses prochaines slices (`--route versions --method PUT`), mais ses modules `Schedule` et `Realtime`, écrits avant, doivent recevoir `public const string Route = "/schedule";` (et `"/realtime"`) avant de générer une slice, sans quoi les tests générés échouent sur CS0117.

**Résultat observable**

`hardslice-command` et `hardslice-query` acceptent `--route` et `--method`, et le test d'intégration généré appelle la route choisie.

**Constat**

Ghostline, entrées « hardslice-command and hardslice-query, routes named after the slice » et « the frictions of roadmap 1.1 came back » : les templates ont mappé `POST /import-schedule`, `GET /get-scheduled-trips/{id:guid}` et `POST /collect-realtime-feeds`. Le verbe du nom de la slice finit dans l'URL. Les trois routes ont été réécrites (`POST /schedule/versions`, `GET /schedule/trips?date=`, `POST /realtime/collections`), puis la constante `Route` de chaque test généré.

**Périmètre**

- `--route` : chemin relatif au groupe du module, validé comme le `--route` de `hardslice-module` (segments, paramètres de route acceptés par ASP.NET Core).
- `--method` : `POST`, `PUT`, `PATCH` ou `DELETE` pour une commande, `GET` seul pour une requête (refus explicite sinon).
- Valeurs par défaut inchangées : la route tirée du nom de la slice ne peut pas entrer en collision (arbitrage de l'incrément).
- Endpoint et constante de route du test suivent. `add-slice` demande de toujours passer `--route`, choisie d'après la ressource. `docs/item-templates.md` : les deux options et ce conseil.

**Cas vérifiés**

- Commande `--route versions --method PUT` et requête `--route trips` : vérification verte, tests générés verts sur la route choisie.
- Route invalide, verbe refusé : code 105, rien de créé.
- Module créé avec son propre `--route` : la route de la slice s'y ajoute.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-066 : Générer une entité enfant avec `dotnet new hardslice-entity`

Statut : terminé le 04/11/2026. Ferme les entrées « no template nor skill for the child entities of an aggregate », « HS008 and an aggregate with millions of child rows » et « the frictions of roadmap 1.1 came back » (pour `TripObservation`) de `ghostline/docs/hardslice-friction.md`. `hardslice-entity` prend `--module`, `--aggregate`, `--name`, `--table` (le nom suivi de `s` par défaut) et `--link` : `collection` par défaut, l'enfant tenu dans une collection de la racine nommée comme sa table, identifiant de la racine en propriété cachée du mapping, ou `id`, l'enfant qui porte `<Aggregate>Id` sans navigation. Le template ne modifie jamais la racine : il imprime la liste privée et sa vue en lecture seule à lui ajouter, et tant qu'elle manque, EF Core refuse de construire le modèle (message qui nomme la navigation, annoncé par les instructions). L'agrégat est reconnu par son mapping `Features/<Module>/<Aggregate>Configuration.cs`, lu par une nouvelle propriété évaluée `HardSliceMappingFiles` de `Directory.Build.targets` (mappings et convertisseurs d'identifiants du web), comme `HardSliceModuleFiles`. `<Aggregate>IdConverter`, un `ValueConverter` du module, est créé avec le premier enfant et exclu de la génération dès qu'il existe : `hardslice-aggregate` reste inchangé (conversion en ligne), et la racine peut adopter le convertisseur. Refus (code 105, rien de créé) : application illisible, nom, module ou agrégat absent ou hors PascalCase, module inconnu, agrégat sans mapping dans le module, table invalide, nom d'un module, d'un espace de noms, d'un type importé, ou déjà mappé dans le module (l'agrégat lui-même compris). `add-aggregate` a une section « Child entities » (choix de la forme, mapping à côté de celui de la racine, convertisseur partagé, méthodes de la racine), `HS008.md` un paragraphe « Large child collections » qui dit que l'enfant porteur de l'identifiant de sa racine est la forme voulue. `AGENTS.md` : 141 et 149 lignes, inchangé. Contournement que Ghostline peut retirer : aucun code à changer, ses enfants existants ont déjà la forme `--link id` et ses convertisseurs `ScheduleVersionIdConverter` et `ObservedTripIdConverter` le nom et la place que le template reconnaît, mais ses prochaines entités enfants se génèrent avec `hardslice-entity` au lieu d'être écrites à la main, une fois son `Directory.Build.targets`, protégé, repris du template pour `HardSliceMappingFiles`.

**Résultat observable**

`dotnet new hardslice-entity --module Schedule --aggregate ScheduleVersion --name StopTime` génère l'entité enfant, son identifiant, son mapping et ses tests unitaires. La skill `add-aggregate` dit où vit le mapping d'un enfant, et quand un enfant porte l'identifiant de sa racine au lieu d'être tenu par elle.

**Constat**

Ghostline, entrées « no template nor skill for the child entities of an aggregate », « HS008 and an aggregate with millions of child rows » et « the frictions of roadmap 1.1 came back » :

- `hardslice-aggregate` crée une racine, son identifiant et son mapping, rien ne crée d'entité enfant. Sept entités (`Route`, `Trip`, `Stop`, `StopTime`, `Calendar`, `CalendarDate`, `TripObservation`), leurs `IEntityTypeConfiguration` et leurs tests ont été écrits à la main, contre la consigne « never by hand » faute de template.
- Rien ne dit où va le mapping d'un enfant (un fichier par entité à côté de `<Aggregate>Configuration.cs` a été choisi), ni comment partager la conversion de l'identifiant de la racine (un `ValueConverter` dans l'espace de noms du module, `ObservedTripIdConverter`).
- Une racine ne peut pas tenir 3,8 millions d'horaires. Les enfants portent alors le `ScheduleVersionId`, sans navigation, et HS008 ne connaît pas de propriétaire (comportement prévu par sa fiche). Rien ne dit que c'est la forme voulue.

**Périmètre**

- Template `hardslice-entity` : `--module`, `--aggregate`, `--name`, et une option qui choisit entre l'enfant tenu par sa racine (collection de la racine, forme par défaut) et l'enfant qui porte l'identifiant de sa racine, sans navigation, pour les grandes collections. Noms d'option libres.
- Fichiers : l'entité (`IEntity`) dans `<App>.Domain/<Module>/`, son identifiant, `<Entity>Configuration.cs` à côté du mapping de la racine, ses tests unitaires.
- Conversion de l'identifiant de la racine partagée par un `ValueConverter` du module, créé au premier enfant et réutilisé ensuite. Si `hardslice-aggregate` doit le générer d'emblée, le changement est fait ici.
- Refus comme `hardslice-aggregate` : module ou agrégat inconnu, nom réservé ou déjà pris, nom du module.
- `add-aggregate` : où vit le mapping d'un enfant, le choix entre les deux formes. `HS008.md` : un paragraphe « large child collections ».

**Limites**

Un contrat de données de référence immuables hors `IEntity` (HS902 l'exige aujourd'hui de tout type du domaine mappé) reste différé : voir la section 9.

**Cas vérifiés**

- Les deux formes : vérification verte, migration générée, tests unitaires verts.
- Deux enfants du même agrégat : un seul `ValueConverter`.
- Refus : code 105, rien de créé.
- HS008 : l'enfant qui porte l'identifiant de sa racine ne produit pas de diagnostic.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-067 : Accepter une commande système sans endpoint HTTP

Statut : terminé le 04/11/2026. Ferme l'entrée « HS004 requires an HTTP endpoint for a command only a timer sends » de `ghostline/docs/hardslice-friction.md`. `ISystemCommand<TResponse> : ICommand<TResponse>` est dans `Shared/Messaging/Requests.cs` des deux variantes, et la clé `systemCommand`, écrite par le template, est la seule clé facultative de `contracts` : sans elle, HS900 ne dit rien et une commande garde l'endpoint obligatoire. HS900 signale une clé `systemCommand` qui ne dérive pas du contrat `command`, côté analyseur comme côté tests d'architecture. HS004 n'exige pas d'endpoint d'une commande système et signale trois cas : une classe `<Slice>Endpoint` dans sa slice, un `Map*` dont le délégué la reçoit ou la crée (dans un module ou dans `Program.cs`), et, en fin de compilation, une commande qu'aucun type de son module n'envoie. Un type l'envoie s'il la crée, la passe en argument de type d'une méthode ou d'un type de base (`AddRecurringCommand<T>`, un service générique), hors de la commande, de son handler, de son validateur et de leurs enregistrements. `hardslice-command --endpoint none` génère la commande système sans endpoint ni test HTTP, avec un test d'intégration qui l'envoie par `IMediator` (et vérifie le rejet par `ValidationBehavior` avec `--parameters id`), et imprime ses enregistrements sans appel de `Map`. `--route` et `--method` sont refusés avec elle (code 105, rien de créé), une valeur inconnue par `dotnet new` (code 127). La sortie par défaut est inchangée. Le message de HS004 sur un endpoint manquant renvoie vers `ISystemCommand`, et `add-slice` dit comment l'envoyer d'un service hébergé de la slice. `AGENTS.md` : 141 et 149 lignes, inchangé. Contournement que Ghostline peut retirer : l'endpoint public `POST /realtime/collections` de `CollectRealtimeFeeds`, `CollectRealtimeFeedsCommand` pouvant implémenter `ISystemCommand`, envoyée par `RealtimeCollector`, une fois `Requests.cs` et `hardslice.json` repris du template (son test HTTP devient un envoi par `IMediator`).

**Résultat observable**

Une commande envoyée seulement par l'application implémente `ISystemCommand<TResponse>`, n'a pas d'endpoint, et HS004 l'accepte. Une commande système mappée en HTTP, ou que rien ne référence dans son module, produit un diagnostic.

**Constat**

Ghostline, entrée « HS004 requires an HTTP endpoint for a command only a timer sends » : HS004 exige `<Slice>Endpoint` dans chaque slice de requête et interdit un endpoint vide ou jamais mappé. `CollectRealtimeFeeds`, envoyée toutes les 30 secondes par le collecteur, a donc reçu `POST /realtime/collections`, public et sans authentification : n'importe qui peut consommer le quota de la clé NextRip.

**Périmètre**

- `ISystemCommand<TResponse> : ICommand<TResponse>` dans `Shared/Messaging/Requests.cs`, dans les deux variantes. Clé facultative `systemCommand` dans `hardslice.json`, écrite par le template. Un fichier `"version": 1` sans elle reste valide (PB-055). HS900 signale une clé `systemCommand` dont le type ne dérive pas du contrat `command`.
- HS004 : pas d'endpoint exigé pour une commande système. Un endpoint qui la mappe produit un diagnostic. Une commande système qu'aucun type de son module ne référence, hors de son handler et de son validateur, produit un diagnostic (analyse de fin de compilation, comme les appels de module de HS015).
- Pipeline : `LoggingBehavior`, `ValidationBehavior` et `TransactionBehavior` s'appliquent sans changement. Les tests d'architecture (`CommandTests`, `RequestTypes`) reconnaissent la commande système comme une commande.
- `hardslice-command --endpoint none` génère une commande système, sans endpoint ni test HTTP, avec un test d'intégration qui l'envoie par `IMediator`.
- Fiches HS004 et HS900, `AGENTS.md`, `add-slice`, `fix-architecture-violation`, `docs/rules.md`, `docs/item-templates.md` et spécification (contrats de `hardslice.json`) alignés.

**Cas vérifiés**

- Commande système référencée par un type de son module : aucun diagnostic.
- Commande système mappée par un endpoint : diagnostic.
- Commande système jamais référencée : diagnostic.
- `hardslice.json` sans `systemCommand` : aucun HS900, et une commande qui n'implémente que `ICommand` garde l'endpoint obligatoire.
- Clé `systemCommand` vers un type qui ne dérive pas de `ICommand` : HS900.
- Référence Bookings : diagnostics inchangés. Seuils de `scripts/measure-analyzers.sh` tenus.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/measure-analyzers.sh
sondes des templates d'éléments et des règles de scripts/test-template.sh
```

### PB-068 : Envoyer une commande système à intervalle régulier avec `AddRecurringCommand`

Statut : terminé le 04/11/2026. Ferme l'entrée « no template nor documentation for a background service » de `ghostline/docs/hardslice-friction.md`. `Shared/Messaging/RecurringCommand.cs`, dans les deux variantes : `services.AddRecurringCommand<TCommand, TResponse>(section, interval)` pour `TCommand : ISystemCommand<TResponse>, new()` (deux arguments de type, C# ne déduit pas `TResponse`). `RecurringCommandService` est un `BackgroundService` qui crée son `PeriodicTimer` sur le `TimeProvider` du conteneur dans `StartAsync`, pour qu'une horloge avancée juste après le démarrage ne perde pas son premier tick, puis envoie une commande neuve par `IMediator`, dans un scope à elle, un intervalle après le démarrage puis à chaque intervalle, sans chevauchement. Une exception d'un cycle est journalisée en erreur et le cycle suivant s'exécute, l'arrêt de l'hôte est propre. La clé `<section>:Interval` de la configuration remplace l'intervalle, validée au démarrage (plus que zéro, moins que la limite d'un timer, environ 49 jours). Un second enregistrement de la même commande est refusé. Ses tests unitaires, sur un hôte et un `FakeTimeProvider` : trois cycles sans attente réelle, aucun avant le premier intervalle, un cycle en échec journalisé suivi du suivant sans arrêter l'hôte, l'arrêt, la configuration, les intervalles refusés. `hardslice-command --every <interval>` (`[d.]hh:mm:ss`, plus que zéro, moins de 49 jours) implique `--endpoint none` et `--parameters none`, qui perdent leur valeur par défaut pour que `--every` refuse `--parameters id` ou `--endpoint http` écrits (code 105, rien de créé), comme un intervalle invalide et `--response none`. Il imprime l'enregistrement du handler et `.AddRecurringCommand<…>("<Module>:<Name>", TimeSpan.Parse("…", CultureInfo.InvariantCulture))`, et génère un test d'intégration qui fait passer trois cycles par `RecurringCommandCycles<TCommand, TResponse>`, une aide de `tests/MyApp.IntegrationTests/` qui garde le service par `KeepHostedService` (PB-062), remplace l'horloge par un `FakeTimeProvider` (PB-060), avance d'un intervalle et rend la réponse du cycle ou lève son exception. HS004 nomme `AddRecurringCommand` dans le correctif d'une commande système que rien n'envoie. HS010 signale dans les slices toute surcharge de `Task.Delay` et du constructeur de `PeriodicTimer` qui ne prend pas de `TimeProvider`, avec un second message (« waits on the system clock »), et pas dans le domaine. `AGENTS.md` : une puce « Background work » dans « Adding a slice », `--every` à l'étape 2, `AddRecurringCommand` dans la carte et la règle d'or 1, les étapes 2 et 3 d'« Adding a first module » fusionnées pour rester à 149 lignes sans l'exemple (142 avec). `add-slice`, `docs/item-templates.md`, `docs/rules.md`, les fiches HS004 et HS010 et le CHANGELOG suivent. Le nom de l'exemple du ticket, `--name CollectRealtimeFeedsCommand`, est refusé depuis PB-036 (suffixe `Command`) : la commande se génère avec `--name CollectRealtimeFeeds`. Contournement que Ghostline peut retirer : `RealtimeCollector` et son interrupteur `Realtime:CollectorEnabled`, remplacés par `.AddRecurringCommand<CollectRealtimeFeedsCommand, CollectedFeedsDto>("Realtime:CollectRealtimeFeeds", …)` dans `RealtimeModule` (l'intervalle passe de `Realtime:Interval` à `Realtime:CollectRealtimeFeeds:Interval`), et `RealtimeCollectorTests` réécrit avec `RecurringCommandCycles`, une fois `Shared/Messaging/RecurringCommand.cs` et `RecurringCommandCycles.cs` repris du template. La vérification de la clé NextRip manquante passe dans le handler, qui échoue alors à chaque cycle au lieu d'arrêter le collecteur. `ScheduleImporter`, qui attend une heure calculée et réessaie, reste un service hébergé.

**Résultat observable**

`dotnet new hardslice-command --module Realtime --name CollectRealtimeFeedsCommand --every 00:00:30` génère une commande système sans paramètre, son handler et son enregistrement périodique dans le module, avec un test qui fait passer trois cycles sans attente réelle. `AGENTS.md` dit où vit le travail en arrière-plan. HS010 signale un timer construit sans `TimeProvider` dans une slice.

**Constat**

Ghostline, entrée « no template nor documentation for a background service » : un service hébergé n'est ni une slice ni un endpoint, et rien ne dit où il va, comment il envoie ses commandes ni comment il se teste. Chaque choix a été deviné : `BackgroundService` dans le dossier de la slice qu'il déclenche, enregistré par le module avec `AddHostedService`, un scope `IServiceScopeFactory` et un `IMediator.Send` par cycle, toute exception attrapée et journalisée (depuis .NET 8, une exception qui sort de `ExecuteAsync` arrête l'hôte), `new PeriodicTimer(interval, TimeProvider)`, options dans l'espace de noms du module, clé manquante journalisée en critique sans arrêter l'API. L'entrée suggérait déjà un envoi récurrent générique dans `Shared/Messaging`. HS010 ne signale que les lectures de l'horloge (`DateTime.UtcNow`) : `new PeriodicTimer(interval)` et `Task.Delay(interval)` passent, et rendent le service impossible à tester avec une horloge simulée.

**Périmètre**

- `Shared/Messaging` : un service hébergé générique et son extension d'enregistrement (`services.AddRecurringCommand<TCommand>(interval)`, forme exacte libre), pour `TCommand : ISystemCommand<TResponse>, new()` (PB-067). Un scope et un envoi par `IMediator` par cycle, une exception d'un cycle journalisée sans arrêter l'hôte, un `PeriodicTimer` construit avec le `TimeProvider` du conteneur, un arrêt propre avec l'hôte. Livré dans les deux variantes, avec ses tests.
- `hardslice-command --every <interval>` : implique `--endpoint none` (PB-067) et `--parameters none` (PB-064), refuse une combinaison contraire ou un intervalle invalide (code 105). Il génère l'enregistrement dans le module, l'intervalle lu dans les options du module avec la valeur passée par défaut, et un test d'intégration qui garde le service (PB-062) et avance un `FakeTimeProvider` (PB-060).
- `AGENTS.md` : un paragraphe « Background work », sous 150 lignes en `--no-sample`, ou un renvoi vers `add-slice` qui le porte. Une tâche périodique passe par `AddRecurringCommand`. Un service hébergé non périodique vit dans le dossier de la slice qu'il déclenche, envoie ses commandes dans un scope par message et reçoit `TimeProvider`.
- HS010 : dans les espaces de noms de slice, `new PeriodicTimer(TimeSpan)` et `Task.Delay(TimeSpan)` sans `TimeProvider` produisent le diagnostic. Fiche, cas positifs et négatifs, seuils de `scripts/measure-analyzers.sh` tenus.
- `docs/item-templates.md` et `add-slice` : l'option `--every`.

**Cas vérifiés**

- Commande générée avec `--every` : vérification verte, son test fait passer trois cycles sans attente réelle.
- Handler qui lève une exception : le cycle suivant s'exécute, l'hôte continue.
- Les autres tests d'intégration ne démarrent pas le service (PB-062).
- `--every` avec `--parameters id`, ou un intervalle invalide : code 105, rien de créé.
- `Task.Delay(TimeSpan.FromSeconds(1))` dans un handler : HS010. `Task.Delay(interval, timeProvider)` : aucun diagnostic.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/measure-analyzers.sh
sondes des templates d'éléments et des règles de scripts/test-template.sh
```

### PB-069 : Répondre 409 à deux commandes qui créent le même agrégat

Statut : terminé le 04/11/2026. Ferme l'entrée « nothing says how two commands that create the same aggregate are serialized » de `ghostline/docs/hardslice-friction.md`. `TransactionBehavior`, dans les deux variantes, traduit une violation d'unicité de PostgreSQL (`PostgresException` de `SqlState` `23505`, levée telle quelle par du SQL du handler ou dans la `DbUpdateException` de `SaveChangesAsync`, au commit compris) en `ConflictException`, répondue 409 avec un message qui ne reprend jamais les valeurs de la clé, après le conflit de concurrence qu'il traduisait déjà. Une exception levée par le handler qui a lui-même attrapé la violation n'est plus une `PostgresException` et passe telle quelle : la contrainte d'exclusion de `ConfirmBooking` (`23P01`, de toute façon hors du filtre) garde sa `BookingConflictException`, ses tests inchangés. Tests d'intégration de `TransactionBehaviorTests`, dans les deux variantes : deux commandes concurrentes sous la même clé, la seconde attendant sur l'index (vérifié par `pg_blocking_pids`) jusqu'au commit de la première, qui réussit pendant que la seconde lève une `ConflictException` sans la clé, une seule ligne gardée. Un handler qui traduit la violation garde son exception. Avec l'exemple, le chemin EF Core : une réservation enregistrée de nouveau sous sa clé primaire donne une `ConflictException`. `scripts/test-template.sh` exige ces tests dans les deux variantes. `AGENTS.md` : une phrase dans la puce du handler de « Adding a slice » (142 et 149 lignes, inchangé). `add-slice` : une puce sur la commande qui crée sous une clé unique (l'index décide, le test d'intégration envoie deux fois la même création et attend le 409, la traduction propre au domaine l'emporte, le verrou `pg_advisory_xact_lock` réservé au handler qui fusionne avec ce qu'une exécution concurrente a créé). Contournement que Ghostline peut retirer : le verrou de `CollectRealtimeFeeds` n'est plus nécessaire pour éviter le 500 : sans lui, une collection concurrente échoue en 409 sans rien écrire, et le cycle suivant de la commande récurrente la reprend. Ghostline le garde si une collection ne doit jamais échouer, puisque le handler fusionne les courses déjà vues, le cas que `add-slice` réserve au verrou. Celui d'`ImportSchedule` reste, pour la même raison.

**Résultat observable**

Deux commandes concurrentes qui créent un agrégat sous la même clé unique répondent l'une 201 (ou 204), l'autre 409, et jamais 500.

**Constat**

Ghostline, entrée « nothing says how two commands that create the same aggregate are serialized » : `CollectRealtimeFeeds` crée un `ObservedTrip` par course et jour de service, sous un index unique. Le collecteur, une seconde instance de l'API ou l'endpoint peuvent lancer la commande en même temps, et le second commit échoue sur l'index avec une `DbUpdateException`, répondue 500. `TransactionBehavior` traduit un conflit de concurrence (`DbUpdateConcurrencyException`) en `ConflictException`, pas une violation d'unicité. Contournement : un verrou consultatif de transaction (`pg_advisory_xact_lock`) pris avant la lecture, comme dans `ImportSchedule`.

**Périmètre**

- `TransactionBehavior` traduit une violation d'unicité de PostgreSQL (`PostgresException`, `SqlState` `23505`) en `ConflictException`, répondue 409 sans exposer les valeurs de la contrainte. Une traduction propre au domaine faite par le handler (comme `ConfirmBooking` pour sa contrainte d'exclusion) l'emporte toujours.
- `AGENTS.md` (ou `add-slice`) : une commande qui crée sous une clé unique s'appuie sur l'index et le 409. Un handler qui doit lire puis créer sans conflit peut prendre un verrou de transaction, documenté, non généré (arbitrage de l'incrément).
- Test de `TransactionBehavior` dans les deux variantes.

**Cas vérifiés**

- Deux créations concurrentes sous la même clé : 201 puis 409.
- Contrainte d'exclusion de Bookings : toujours `BookingConflictException`, réponse inchangée.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application de scripts/test-template.sh, dans les deux variantes
```

### PB-070 : Générer une requête filtrée et des DTO nommés sans le verbe

Statut : terminé le 04/11/2026. Ferme l'entrée « hardslice-query covers reads by id and lists of one aggregate only » de `ghostline/docs/hardslice-friction.md`. `--result filtered` s'ajoute à `single` et `many`, avec `--parameter <Name>:<Type>`, exigé avec lui et refusé avec les autres résultats : un nom en PascalCase et un type écrit comme en C#, parmi `string`, `bool`, `int`, `long`, `decimal`, `double`, `Guid`, `DateOnly`, `TimeOnly` et `DateTimeOffset`. Refus (code 105, rien de créé) : `filtered` sans filtre, filtre sans `filtered`, filtre qui n'est pas `<Name>:<Type>`, type hors de la liste (un enum du domaine se génère avec le type le plus proche, puis se change). La requête porte le filtre nullable (`GetBookingsOnDateQuery(DateOnly? Date)`), lu dans la chaîne de requête par `[AsParameters]`, pour qu'une requête sans lui atteigne le validateur (`NotNull()`, `NotEmpty()` pour une `string`), qui répond 400 en le nommant, au lieu d'un 400 du binding sans corps. `[AsParameters]` évite aussi un nom de paramètre en camelCase qui serait un mot-clé C# (`event`) ou rencontrerait `mediator`, et un second filtre n'est qu'une propriété de la requête et une règle du validateur. Le template génère les tests unitaires du validateur, un test d'intégration de la liste filtrée (marqueur `TODO(hardslice):`) et un du 400 sans filtre, imprime l'enregistrement du handler et du validateur, et laisse dans le handler un marqueur à l'endroit du `Where` et des jointures. Une valeur de test par type (expression C# et texte de la chaîne de requête) : celle de `DateTimeOffset` est encodée (`%2B00:00`), le moteur de templates lisant `2026-11-04T08:30:00Z` comme une date et la réécrivant au format local. Le DTO de toutes les formes s'appelle comme la slice sans son premier mot, le verbe du cas d'usage (`GetBooking` donne `BookingDto`, `ListRoomBookings` `RoomBookingsDto`), un nom d'un seul mot étant gardé entier : deux requêtes qui ne diffèrent que par leur verbe nomment le même DTO, ambigu (CS0104) dans le module qui les enregistre, limite documentée. Vérifié dans les deux variantes, avec chaque type de la liste sur une solution de travail (filtre lu, 400, validateur). `add-slice`, `AGENTS.md` (142 et 149 lignes, inchangé), `docs/item-templates.md`, le README du paquet et le CHANGELOG suivent. Contournement que Ghostline peut retirer : aucun code à changer dans `GetScheduledTrips`, déjà réécrite et dont le DTO a déjà le nom que le template donne, mais ses prochaines requêtes filtrées se génèrent avec `--result filtered --parameter <Name>:<Type>` au lieu de réécrire la requête, le DTO, le handler, la route et les tests. Une requête qui répond un seul DTO selon le filtre (404 sans version en vigueur, comme `GetScheduledTrips`) reste à adapter à la main depuis la liste générée.

**Résultat observable**

`hardslice-query` génère une requête qui prend un paramètre de filtre et répond une liste, avec son validateur. Les DTO générés ne portent plus le verbe de la slice.

**Constat**

Ghostline, entrée « hardslice-query covers reads by id and lists of one aggregate only » : `--aggregate` est exigé, et le template génère soit un DTO lu par son `Id`, soit la liste de tous les agrégats. `GetScheduledTrips`, filtrée par une date sur cinq tables, n'a gardé que l'espace de noms et la classe d'endpoint : requête, DTO, handler, route et tests ont été réécrits. Le DTO s'appelait `GetScheduledTripsDto`, renommé `ScheduledTripsDto`. HS006 a fonctionné comme prévu sur la jointure.

**Périmètre**

- `--result filtered` et `--parameter <Name>:<Type>` (formes libres) : requête avec son paramètre, validateur et ses tests, réponse en liste, paramètre lu dans la chaîne de requête, tests d'intégration du filtre et de la requête invalide.
- Nom du DTO : le nom de la slice sans le verbe initial (`GetBooking` donne `BookingDto`), pour toutes les formes de `--result`.
- Types de paramètre acceptés listés et refusés sinon (code 105).
- `add-slice` et `docs/item-templates.md` suivent.

**Cas vérifiés**

- `--result filtered --parameter Date:DateOnly` : vérification verte, 400 sur une date absente.
- Noms de DTO des trois formes sans verbe.
- Type de paramètre refusé : code 105, rien de créé.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-071 : Dire quand une règle du domaine peut composer une requête (HS006)

Statut : terminé le 04/11/2026. Ferme l'entrée « HS006 and a rule of the domain that a query needs as SQL » de `ghostline/docs/hardslice-friction.md`. `HS006.md` a une section « A rule of the domain in a query » : formes légitimes, un prédicat `Expression<Func<T, bool>>` du domaine passé à un opérateur, ou une composition de `IQueryable<T>` vers `IQueryable<TResult>` qui reçoit du handler chaque requête qu'elle compose, lequel les commence par `AsNoTracking()` et projette. Forme interdite : une aide qui crée sa source ou rend des entités déjà lues, ajoutée aux contournements interdits. Plutôt que de laisser la composition hors de l'analyse, HS006 la suit désormais par les requêtes qu'il lui passe, jamais par son corps : une méthode qui n'est pas un opérateur, rend une requête et en reçoit au moins une, chacune reconnue. La requête composée est garantie quand chacune l'est, sinon la première sans `AsNoTracking()` est nommée comme sa racine. La forme interdite n'est ni suivie ni signalée, comme le dit la fiche (« What the rule does not detect »). Tests de l'analyseur sur les deux formes légitimes (aucun diagnostic), sur une composition qui rend des entités d'une requête sans garantie (signalée, échouait avant), et sur la forme interdite (aucun diagnostic). `extract-to-domain` (chemin d'une règle dont une requête a besoin, forme, tests sur des listes, interdit), `docs/rules.md` et le CHANGELOG suivent. Contournement que Ghostline peut retirer : aucun, `ServiceCalendar.ServicesRunningOn` et `ScheduleVersion.InForceOn` sont la forme légitime, désormais vérifiée par HS006 (sources sous `AsNoTracking()`, résultat projeté), sans diagnostic.

**Résultat observable**

`HS006.md` et `extract-to-domain` disent quelles formes d'une règle du domaine utilisée par une requête sont légitimes, et HS006 se comporte comme la fiche le dit sur chacune.

**Constat**

Ghostline, entrée « HS006 and a rule of the domain that a query needs as SQL » : deux règles de GTFS doivent tourner dans la base (les services qui roulent un jour donné, la version en vigueur à une date). Elles sont dans le domaine en méthodes statiques qui composent un `IQueryable` (`ServiceCalendar.ServicesRunningOn`, `ScheduleVersion.InForceOn`), testées sur des listes, et le handler leur passe `Set<T>().AsNoTracking()`. HS006 ne suit pas une requête à travers une méthode qui n'est pas un opérateur LINQ, et sa fiche interdit de « get the query from a helper only because HS006 cannot analyze those forms ». Elle ne dit pas si une règle du domaine composée ainsi est légitime.

**Périmètre**

- `HS006.md` : une section sur les règles du domaine dont une requête a besoin. Formes légitimes : `Expression<Func<T, bool>>` du domaine, et composition `IQueryable<T>` vers `IQueryable<T>` qui reçoit sa source du handler, lequel garde `AsNoTracking()` sur chaque racine et projette en scalaires. Forme interdite : une aide qui crée elle-même la source ou rend des entités, pour échapper à l'analyse.
- `extract-to-domain` : la même consigne, du côté du domaine.
- Tests de l'analyseur sur les deux formes légitimes et sur la forme interdite, et `docs/rules.md` (limites de HS006) aligné sur ce qu'ils montrent.

**Cas vérifiés**

- Handler qui passe `Set<T>().AsNoTracking()` à une composition du domaine et projette : aucun diagnostic.
- Prédicat `Expression<Func<T, bool>>` du domaine dans un `Where` : aucun diagnostic.
- Forme interdite : le comportement de HS006 est celui que dit la fiche.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
```

### PB-072 : Documenter les écritures en masse d'une commande

Statut : terminé le 04/11/2026. Ferme l'entrée « bulk import against "one transaction per command, never SaveChangesAsync" » de `ghostline/docs/hardslice-friction.md`. `AGENTS.md` admet un second enregistrement anticipé, celui d'une racine avant l'écriture en masse de ses enfants dans la même transaction, et renvoie vers `add-aggregate` (142 et 149 lignes, inchangé). `add-aggregate` a une section « Children written in bulk » : enfants en `--link id` construits par leur méthode de fabrique, racine ajoutée puis enregistrée avant le premier enfant (elle reste suivie, ses événements de domaine sont publiés), écriture par la connexion de `AppDbContext`, qui porte déjà la transaction de la commande (par exemple un `COPY` binaire), jamais par une connexion ni une transaction à part, colonnes et convertisseurs du mapping, clé étrangère immédiate, jamais `DEFERRABLE INITIALLY DEFERRED` ni retirée, délai allongé pour cette commande seule (`Timeout` de l'importeur, `SetCommandTimeout`), jamais dans la chaîne de connexion, et test d'intégration d'un échec en cours de route. Une ligne de « Never » le reprend. `add-slice` nomme le cas parmi les commandes et ne dit plus « never » sans ses exceptions. Aucune aide générée. Aucune règle n'interdisait `SaveChangesAsync` dans un command handler : `BookingsReferenceTests` vérifie que toutes les règles, en mode par défaut et `All`, admettent `ConfirmBooking` qui écrit ses nuits par `COPY` après son enregistrement anticipé. `TemplatePackageTests` vérifie la consigne dans les deux variantes, et qu'aucun fichier du template ne fait d'écriture en masse. Contournement que Ghostline peut retirer : la version de `ImportSchedule` s'enregistre avant ses enfants (`SaveChangesAsync` après `Add`), et les tables enfants retrouvent leur clé étrangère immédiate vers la version. `BulkInsertExtensions` reste dans l'application, qui en est propriétaire.

**Résultat observable**

Le kit agents dit quand une commande peut écrire hors du suivi des modifications, dans quel ordre, et ce que cela demande des clés étrangères.

**Constat**

Ghostline, entrée « bulk import against "one transaction per command, never SaveChangesAsync" » : `ImportSchedule` écrit 3,8 millions d'horaires par un `COPY` binaire dans la transaction ouverte par `TransactionBehavior` (17 secondes pour l'import réel). La version, ajoutée par le suivi des modifications, est enregistrée après le handler, donc après ses enfants. Une clé étrangère vers elle doit alors être `DEFERRABLE INITIALLY DEFERRED`, vérifiée ligne à ligne au commit : 2 minutes, et le commit de Npgsql expire après 30 secondes. Enregistrer la version plus tôt l'éviterait, mais `AGENTS.md` ne permet un enregistrement anticipé que pour traduire une erreur de la base. Contournement : aucune clé étrangère depuis les tables enfants.

**Périmètre**

- `AGENTS.md` : l'exception « save early » couvre aussi l'enregistrement de la racine avant une écriture en masse de ses enfants, dans la même transaction. Le reste de la règle ne change pas.
- `add-aggregate` (ou la skill qui porte PB-066) : une écriture en masse passe par la connexion et la transaction du `AppDbContext`, après l'enregistrement de la racine, avec des clés étrangères immédiates. Le délai de commande s'ajuste pour la commande concernée.
- Aucune aide générée (arbitrage de l'incrément). Une vérification par les analyseurs ou les tests d'architecture que l'exception reste admise, si l'une d'elles interdit aujourd'hui `SaveChangesAsync` dans un command handler.
- `AGENTS.md` sous 150 lignes en `--no-sample`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes « instructions des agents » de scripts/test-template.sh
```

### PB-073 : Ne pas laisser les `.gitkeep` d'un module derrière ses premiers fichiers

Statut : terminé le 04/11/2026. Ferme les entrées « hardslice-module leaves its .gitkeep files behind » et « the frictions of roadmap 1.1 came back » (pour ses `.gitkeep`) de `ghostline/docs/hardslice-friction.md`. `hardslice-module` ne crée plus que son contrat `<Module>Module.cs` : aucune règle n'exige le dossier du domaine ni les dossiers de tests d'un module (les tests d'architecture et HS018 ne lisent que les fichiers C#, l'existence du module se lit dans son contrat par `HardSliceModuleFiles`), et les templates de ses agrégats, entités, slices et événements créent ces dossiers en y écrivant leurs fichiers. Aucun `.gitkeep` n'est donc créé, ni laissé. Sa description, ses lignes imprimées, `AGENTS.md` (142 et 149 lignes, inchangé), `add-module` (qui interdit aussi de créer un dossier vide du module, `.gitkeep` compris), `docs/item-templates.md` et le README du paquet le disent. `TemplatePackageTests` vérifie que le template de module ne livre que son contrat, qu'aucun template ne livre de `.gitkeep` et la consigne dans les deux variantes. Dans `scripts/test-template.sh`, `generate_module` exige que la génération n'ajoute que le contrat et aucun `.gitkeep`, `remove_module` retire tous les dossiers du module, et la variante `--no-sample` vérifie qu'aucun `.gitkeep` ne reste après le module `Rooms`, son agrégat, ses entités, ses slices et ses événements. `demo-add-feature.sh` admet encore les `.gitkeep` des exécutions enregistrées, faites avant ce ticket. Contournement que Ghostline peut retirer : aucun code à changer, ses `.gitkeep` sont déjà supprimés, mais ses prochains modules n'en reçoivent plus et n'ont plus à être nettoyés à la main.

**Résultat observable**

Après la génération d'un module puis de son agrégat, de son entité et de ses slices, aucun `.gitkeep` ne reste dans un dossier qui contient un fichier, ou la sortie de `hardslice-module` dit qu'ils peuvent partir.

**Constat**

Ghostline, entrées « hardslice-module leaves its .gitkeep files behind » et « the frictions of roadmap 1.1 came back » : `hardslice-module` crée un `.gitkeep` dans `src/<App>.Domain/<Module>/` et dans les trois dossiers de tests du module, parce que `dotnet new` ne crée pas de dossier vide (`TemplatePackageTests`). Les templates suivants y ajoutent des fichiers sans les retirer, et aucune consigne ne dit de le faire. Ils ont été supprimés à la main dans les deux modules.

**Périmètre**

- Mécanisme libre : `dotnet new` ne supprime pas de fichier existant. Par exemple, `hardslice-module` ne crée plus que les dossiers qu'un test ou une règle exige (vérifier `TestLayoutTests`, HS018 et les sondes), ou `check` signale un `.gitkeep` à côté d'un autre fichier, ou la sortie du template et `add-module` le disent.
- `TemplatePackageTests` et les démonstrations suivent si la liste des fichiers générés change.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-074 : Revalider les paquets 0.3.0-preview.1 après les retours de Ghostline

Statut : terminé le 04/11/2026. Ferme l'entrée « dotnet ef unusable after generating the solution » de `ghostline/docs/hardslice-friction.md`. `CHANGELOG.md` ajoute ce qui manquait encore à l'entrée `0.3.0-preview.1` : la restauration de `check --build` seulement quand un fichier lu par la restauration a changé (PB-061). Les autres tickets y avaient écrit leur ligne. Le README du paquet de templates liste `--route`, `--method`, `--parameters none`, `--endpoint none` et `--every` de `hardslice-command`, et `--route` de `hardslice-query`, avec des exemples qui passent `--route`. Le README racine nomme les mêmes options, étend les lignes de HS004 (commande système sans endpoint), HS006 (requêtes passées à une règle du domaine) et HS010 (`Task.Delay` et `new PeriodicTimer` sans `TimeProvider`), et sa limite sur les effets externes suit PB-063. Le README des analyseurs nomme la commande système et les minuteries de HS010. `docs/item-templates.md` ne dit plus que `hardslice-module` crée les dossiers du domaine et des tests (PB-073). `CONTRIBUTING.md` décrit le symptôme du cache des outils locaux (`Run "dotnet tool restore"` juste après une restauration réussie), sa cause (une restauration avec un `NUGET_PACKAGES` temporaire hors de `isolate_dotnet_cli_home`) et sa correction (supprimer le fichier du paquet dans `~/.dotnet/toolResolverCache/1/`). Passages locaux sur ce commit : `dotnet test --no-build` (1206 tests verts), `scripts/test-template.sh` complet (environ 1 h 15) et `scripts/verify-published.sh 0.3.0-preview.1 --source artifacts/packages` sur les paquets reconstruits, tous verts. Contournement que Ghostline peut retirer : aucun code à changer, l'entrée de `dotnet-ef` est déjà supprimée de son cache, et le symptôme a maintenant sa correction écrite.

**Résultat observable**

Le parcours de PB-059 passe à nouveau sur les paquets construits après PB-060 à PB-073, et l'étiquette de PB-051 peut viser ce commit.

**Périmètre**

- `CHANGELOG.md` : l'entrée `0.3.0-preview.1` dit ce que PB-060 à PB-073 ont changé (paquets de l'application, restauration, secrets et tests d'intégration, kit agents, options de `hardslice-command` et `hardslice-query`, `hardslice-entity`, commande système et `AddRecurringCommand`, HS004 et HS010 étendues, 409 sur une clé unique, consignes des cas avancés). `docs/item-templates.md`, `README.md` et les README des paquets listent le nouveau template et les nouvelles options.
- `CONTRIBUTING.md` : le symptôme d'un cache des outils locaux qui vise un dossier supprimé (`Run "dotnet tool restore"` juste après une restauration réussie) et sa correction (supprimer l'entrée concernée de `~/.dotnet/toolResolverCache/1/`). Ghostline l'a rencontré le 03/11/2026 (entrée « dotnet ef unusable after generating the solution ») : `scripts/test-template.sh` isole ce cache depuis PB-021, l'entrée venait d'une sonde lancée hors de `isolate_dotnet_cli_home` sur la machine du mainteneur. Un utilisateur des paquets publiés n'est pas concerné.
- Passage complet de `scripts/test-template.sh` et de `scripts/verify-published.sh --source artifacts/packages`.
- Statut de PB-051 : le commit que vise l'étiquette.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
scripts/verify-published.sh 0.3.0-preview.1 "$(mktemp -d)" --source artifacts/packages
```

### PB-075 : Proposer dans HS902 de ne plus mapper un objet valeur

Statut : terminé le 04/11/2026. Ferme l'entrée « HS902 on an entity turned into a value object points to the wrong fix » de `ghostline/docs/hardslice-friction.md`. Le correctif du message de HS902 se termine par « or, if '{0}' is a value object, remove '{4}' or map it with ComplexProperty in its entity », avant « Skill: add-aggregate ». '{4}' nomme le mapping à retirer : la classe de configuration et la propriété `DbSet` sont celles que nomme déjà '{1}', mais pour `modelBuilder.Entity<Room>()` c'est l'appel (`Entity<Room>`), jamais `OnModelCreating` entier que '{1}' nomme. `HS902.md`, le même fichier dans les deux variantes du template, cite le nouveau diagnostic, et son étape 2 décrit le cas d'une entité devenue objet valeur dont l'ancien mapping existe encore : retirer le mapping que nomme le message, mapper l'objet valeur dans la configuration de son entité avec `ComplexProperty` (ou `OwnsOne`), jamais lui ajouter `IEntity`, puis ajouter une migration. Sa section « Skill » dit que `add-aggregate` ne s'applique pas à un objet valeur. Tests qui échouaient avant : les messages attendus de `ClassificationAnalyzerTests` (les trois formes de mapping, et l'appel générique `Entity<Untagged<int>>`) et de `BookingsReferenceTests`, la fiche qui cite le diagnostic, et un test des correctifs de la fiche. La sonde HS902 de `scripts/test-template.sh` exige le nouveau correctif, et `demos/agent-fix/HS902/diagnostic.txt` le suit. `CHANGELOG.md` a sa ligne. Passages locaux : `dotnet test --no-build` (1207 tests verts) et la sonde HS902 lancée seule sur les deux variantes. Les paquets ne sont pas reconstruits : un ticket de revalidation suit, sur lequel l'étiquette de PB-051 sera posée. Contournement que Ghostline peut retirer : aucun, la configuration de `StopTime` est déjà remplacée par celle de `TripStopTimes`.

**Résultat observable**

Le message de HS902 propose aussi, pour un type devenu objet valeur, de retirer son mapping d'entité ou de le mapper en `ComplexProperty` dans son entité, et `HS902.md` décrit ce correctif.

**Constat**

Ghostline, entrée « HS902 on an entity turned into a value object points to the wrong fix » (04/11/2026, roadmap 2.3) : pendant que `StopTime` devenait un `record` sans `IEntity`, encore mappé par `StopTimeConfiguration`, HS902 a affiché « Fix: implement IAggregateRoot if 'StopTime' is the root of its aggregate, IEntity otherwise. Skill: add-aggregate. » Le bon correctif était l'inverse : ne plus mapper le type, ou le mapper en `ComplexProperty` de son entité. `add-aggregate` ne s'applique pas à un type qui n'est pas un agrégat.

**Périmètre**

- Message de HS902 (`ClassificationAnalyzer`) : il ajoute « or, if '{0}' is a value object, remove '{1}' or map it with ComplexProperty in its entity », où le mapping à retirer est la configuration, le `DbSet` ou l'appel à `Entity<T>()` (pas `OnModelCreating` entier).
- `HS902.md` (le même fichier dans les deux variantes du template) : le diagnostic cité et un correctif pour ce cas.
- Les textes des diagnostics sont figés par la publication : ce ticket passe avant PB-051, et un ticket de revalidation des paquets le suit.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sonde HS902 de scripts/test-template.sh
```

### PB-076 : Donner un `Fix:` et un `Doc:` aux erreurs de l'outil sur les fichiers HardSlice de la solution

Statut : terminé le 04/11/2026. Ferme l'entrée « no path to bring a generated solution to a newer template » de `ghostline/docs/hardslice-friction.md` pour sa partie « misleading message ». La commande de mise à niveau reste dans la tâche « mettre à niveau une solution générée vers un template plus récent ». Chaque erreur de `AnalyzerVersion` (`Directory.HardSlice.props` absent, sans une seule `GlobalPackageReference` versionnée ou à une autre version, `Directory.Packages.props` absent ou sans l'import, un de ces deux fichiers en XML invalide, un projet qui n'a pas reçu `HardSlice.Analyzers`) et celle de `Solution.Find` sans `.config/dotnet-tools.json` portent désormais un `Fix:` et finissent par `Doc: https://github.com/vincentaugerot/hardslice/blob/v<version>/docs/getting-started.md#when-check-stops-on-a-hardslice-file`, à l'étiquette de la version de l'outil. Un fichier absent, ou à une autre version, dit sa cause probable : « The solution was probably generated by another template or another version of dotnet hardslice, whose HardSlice files differ », ou, pour une autre version, une autre version des templates ou l'une des deux versions changée seule. Le `Fix:` d'un fichier protégé dit qu'un mainteneur, puisque `Directory.HardSlice.props` et `.config/dotnet-tools.json` sont protégés, copie le fichier d'une solution générée à neuf par le même template dans un dossier temporaire, `dotnet new hardslice-api -n <nom de la solution> --no-sample` avec `HardSlice.Templates` à la version de l'outil, ou remet l'entrée `hardslice.tool` à la version de la solution puis lance `dotnet tool restore`. Celui de `Directory.Packages.props`, que l'application possède, dit de le reprendre de la même génération puis d'y remettre les versions des paquets de l'application. `docs/getting-started.md` a la section « When check stops on a HardSlice file » : les trois fichiers lus, la cause probable, les deux correctifs et qui les fait, et l'absence de commande de mise à niveau. Le README de `HardSlice.Tool` le dit. Tests qui échouaient avant (19 cas de `CheckCommandTests`) : les messages attendus d'une autre version, de l'import absent, de la référence non versionnée, du manifeste absent et d'un projet sans les analyseurs, et les nouveaux tests de `Directory.HardSlice.props` absent (sous le nom `Ghostline`), de `Directory.Packages.props` absent et du XML invalide. Un test vérifie que la section existe dans `getting-started.md` et nomme ce que disent les erreurs. Dans `scripts/test-template.sh`, le bloc de l'autre version devient la sonde `probe_hardslice_files`, qui exige le `Fix:` et le `Doc:` sur les deux scripts, puis retire `Directory.HardSlice.props` et exige la cause, la génération sous le nom de la solution et le `Doc:`, avec et sans `--build`. `CHANGELOG.md` a sa ligne. `AGENTS.md` généré n'est pas modifié (142 et 149 lignes). Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1212 tests verts) et la sonde `probe_hardslice_files` lancée seule. Les paquets ne sont pas reconstruits : un ticket de revalidation suit, sur lequel l'étiquette de PB-051 sera posée. Contournement que Ghostline peut retirer : aucun, ses fichiers HardSlice restent à mettre à niveau par un mainteneur, ce que le message dit désormais.

**Résultat observable**

Sur une solution dont un fichier HardSlice manque ou porte une autre version, `dotnet hardslice check` dit la cause probable (solution générée par un autre template ou une autre version de l'outil), un `Fix:` (reprendre le fichier d'une génération neuve du même template, par un mainteneur s'il est protégé, ou remettre l'outil à la version de la solution) et un `Doc:` vers la section de `getting-started.md` qui le décrit.

**Constat**

Ghostline, entrée « no path to bring a generated solution to a newer template » (04/11/2026) : avec l'outil reconstruit après PB-060 à PB-074, `scripts/check.sh` s'arrête aussitôt sur « error: Directory.HardSlice.props was not found in '/root/Github/ghostline': it must reference HardSlice.Analyzers 0.3.0-preview.1, the version of dotnet hardslice. » (`src/HardSlice.Tool/AnalyzerVersion.cs`). Le message n'a ni `Fix:` ni `Doc:`, contrairement aux diagnostics, et ne dit pas que la solution a été générée par un template d'une autre disposition, ni quoi faire.

**Périmètre**

- Les erreurs de `AnalyzerVersion` et de `Solution.Find` sur un fichier HardSlice manquant, invalide ou à une autre version : cause probable, `Fix:` et `Doc:`.
- La section de `docs/getting-started.md` que vise le `Doc:`, à l'étiquette de la version de l'outil.
- Le texte de ces erreurs est lu par les agents et figé par la publication : ce ticket passe avant PB-051, et un ticket de revalidation des paquets le suit. La commande de mise à niveau d'une solution générée n'en fait pas partie.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sonde probe_hardslice_files de scripts/test-template.sh
```

### PB-077 : Montrer d'abord au hook après édition les erreurs des fichiers édités

Statut : terminé le 04/11/2026. Ferme l'entrée « the build hook reports the files not yet edited of a planned change » de `ghostline/docs/hardslice-friction.md`. Après l'édition d'un `.cs`, `AgentHook.AfterEdit` trie les erreurs localisées de `check --build` (`fichier(ligne,colonne): error ...`, chemin absolu ou relatif ramené à la racine de la solution) : celles des fichiers édités restent en entier, celles des autres fichiers sont retirées et comptées sur une dernière ligne, « HardSlice: 3 more errors in 2 other files, not shown here since the next edits of your change may fix them. Once the change is done, run scripts/check.sh --build to see those left: the full check before handing back lists every error. » Une ligne qui ne nomme aucun fichier (erreur de l'outil, `CSC : error`) et les titres de `check --build` restent. Sans erreur dans un fichier édité, ou sans erreur ailleurs, la réponse est celle d'avant, pour ne jamais cacher une erreur que l'édition a causée ailleurs. Le hook de fin de tour, `check --build` et `scripts/check.sh` ne changent pas. `docs/agent-loop.md` et le README de `HardSlice.Tool` le disent. Test qui échouait avant : `AfterEditErrorsTests`, nourri des charges utiles enregistrées de Claude Code, Codex et Copilot CLI (les trois adaptateurs) : erreurs du fichier édité montrées et autres comptées (3 cas rouges avant la correction), toutes montrées sans erreur dans le fichier édité, pas de décompte sans autre erreur, liste entière au hook de fin. La sonde `probe_claude_hooks` de `scripts/test-template.sh` ajoute, sur un vrai build, un second fichier qui lit `DateTime.Today` : après l'édition du premier, son HS010 n'est que compté, et le hook de fin les liste tous les deux. `CHANGELOG.md` a sa ligne. `AGENTS.md` généré n'est pas modifié (142 et 149 lignes). Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1222 tests verts), les sondes `probe_claude_hooks` et `probe_codex_and_copilot_hooks` lancées seules. Les paquets ne sont pas reconstruits : un ticket de revalidation suit, sur lequel l'étiquette de PB-051 sera posée. Contournement que Ghostline peut retirer : aucun, il lisait et ignorait les erreurs des fichiers pas encore édités, ce que le hook lui épargne désormais.

**Résultat observable**

Après une édition, le hook affiche en entier les erreurs des fichiers édités, puis seulement un décompte des autres (« 12 more errors in 4 other files »). Si aucune erreur ne concerne les fichiers édités, il les affiche toutes, comme avant, pour ne jamais cacher une erreur que l'édition a causée ailleurs. La vérification complète du hook de fin de tour garde la liste entière.

**Constat**

Ghostline, entrée « the build hook reports the files not yet edited of a planned change » (04/11/2026, feuille de route 2.3) : pendant un changement planifié d'une dizaine de fichiers, le hook `PostToolUse` (`AgentHook.AfterEdit`, `src/HardSlice.Tool/AgentHook.cs`) a affiché après chaque édition toutes les erreurs de la solution, jusqu'à quinze lignes, la plupart dans des fichiers que les éditions suivantes allaient changer (les tests de `StopTime`, `GtfsFeed`, le mapping). L'agent les lit et les ignore jusqu'à la dernière édition, ce qui remplit son contexte.

**Périmètre**

- La réponse du hook après une édition de Claude Code, Codex et Copilot CLI : les erreurs localisées (`fichier(ligne,colonne): error ...`) des fichiers édités en entier, celles des autres fichiers comptées, une ligne sans fichier (erreur de l'outil, `CSC : error`) toujours gardée.
- Le hook de fin de tour, `check --build` lancé à la main et `scripts/check.sh` ne changent pas.
- Le texte de cette réponse est lu par les agents et figé par la publication : ce ticket passe avant PB-051, et un ticket de revalidation des paquets le suit.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sonde probe_claude_hooks de scripts/test-template.sh
```

### PB-078 : Laisser rendre la main dès le premier échec de l'outil lui-même au hook de fin de tour

Statut : terminé le 04/11/2026. Ferme l'entrée « the Stop hook relaunches the agent on a failure only a protected file can fix » de `ghostline/docs/hardslice-friction.md`. `CheckCommand.StoppedOnProtectedFile` dit que la vérification s'est arrêtée avant le build sur ce que seul un mainteneur corrige : aucune solution (manifeste de l'outil absent) ou une erreur de `Directory.HardSlice.props` (absent, XML invalide, sans version ou à une autre version), séparées des erreurs de `Directory.Packages.props` (`AnalyzerVersion.References` puis `AnalyzerVersion.Imports`). Sur un tel échec, `AgentHook.BeforeHandingBack` ne compte aucune relance : il remet l'erreur à l'agent une fois, « HardSlice: the check stops before the build on a protected HardSlice file of the solution, so the work is not verified. Only a maintainer can fix it, as the error below says: do not try to fix it. Finish now by telling the user that the check fails on this file and what a maintainer must do, with the Fix: and the Doc: of the error. This hook lets you finish: it does not relaunch you for this failure. », suivie de l'erreur de PB-076. Les fins suivantes le laissent finir, avec un `systemMessage` pour l'utilisateur sous Claude Code et Codex, dans ce tour et les suivants : `StopRetries` garde les échecs rapportés de la session dans un second fichier, `<cli>-<session>.reported.json`, que les nouveaux tours ne vident pas et qu'une vérification réussie efface. Le compte de relances de la constatation de Ghostline, qui repartait à chaque message de l'utilisateur, ne s'applique donc plus. Un état qui ne peut s'écrire rapporte l'échec à la première fin du tour seulement. Les relances des autres échecs (build, tests, marqueurs, import absent de `Directory.Packages.props`) ne changent pas. Tests qui échouaient avant, nourris des charges utiles enregistrées : `Directory.HardSlice.props` absent sous Claude Code (rapporté une fois, fin laissée, nouveau tour laissé, oublié après une vérification réussie) et sous Copilot CLI (un seul `decision: "block"`), autre version après une relance pour un échec du build. Un test garde les trois relances de l'import absent. La sonde `probe_hardslice_files` de `scripts/test-template.sh` lance le vrai hook `Stop` sans `Directory.HardSlice.props` : code 2 avec l'erreur, puis fin laissée. `docs/agent-loop.md`, `docs/getting-started.md`, `docs/agents/support.md` et le README de `HardSlice.Tool` le disent, `CHANGELOG.md` a sa ligne. `AGENTS.md` généré n'est pas modifié (142 et 149 lignes). Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1226 tests verts), les sondes `probe_hardslice_files`, `probe_claude_hooks` et `probe_codex_and_copilot_hooks` lancées seules. Les paquets ne sont pas reconstruits : un ticket de revalidation suit, sur lequel l'étiquette de PB-051 sera posée. Contournement que Ghostline peut retirer : aucun, son agent expliquait le conflit à chaque relance, ce que le hook ne lui demande plus qu'une fois.

**Résultat observable**

Quand la vérification s'arrête avant le build sur un fichier HardSlice protégé (`.config/dotnet-tools.json` absent, `Directory.HardSlice.props` absent, en XML invalide, sans version ou à une autre version : les erreurs de PB-076), le hook de fin de tour de Claude Code, Codex et Copilot CLI remet l'erreur à l'agent une seule fois dans la session, avec la consigne de ne pas chercher à la corriger et de rendre la main en disant à l'utilisateur ce qu'un mainteneur doit faire, puis le laisse finir, comme après le refus d'une édition d'un fichier protégé. Un échec du build, des tests, des marqueurs ou de `Directory.Packages.props`, que l'agent peut corriger, garde ses trois relances.

**Constat**

Ghostline, entrée « the Stop hook relaunches the agent on a failure only a protected file can fix » (04/11/2026) : le hook de fin de tour (`AgentHook.BeforeHandingBack`, `StopRetries`, `src/HardSlice.Tool/`) a relancé l'agent deux fois sur « Directory.HardSlice.props was not found », qu'on ne corrige qu'en créant un fichier protégé et en éditant `Directory.Packages.props`, protégé lui aussi à ce moment-là. Chaque relance coûte un tour à répéter la même explication, jusqu'à la limite de trois, et le compte repart à chaque message de l'utilisateur : tant que l'agent attend que l'utilisateur copie les fichiers, chaque échange coûte jusqu'à trois tours de plus sur le même échec.

**Périmètre**

- `check` dit quand il s'arrête sur ce que seul un mainteneur corrige : aucune solution trouvée (manifeste de l'outil absent) ou une erreur de `Directory.HardSlice.props`. Les erreurs de `Directory.Packages.props`, que l'application possède, n'en font pas partie.
- Le hook de fin de tour rapporte un tel échec une fois par session et par échec, dans ce tour ou un précédent, et l'oublie quand la vérification passe. Le nombre de relances de `StopRetries` ne change pas pour les autres échecs.
- Le texte de cette réponse est lu par les agents et figé par la publication : ce ticket passe avant PB-051, et un ticket de revalidation des paquets le suit.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes probe_hardslice_files, probe_claude_hooks et probe_codex_and_copilot_hooks de scripts/test-template.sh
```

### PB-079 : Donner au pipeline HTTP son dossier de `Shared`, et dire ce que HS015 attend des réponses du middleware

Statut : terminé le 04/11/2026, arbitrages 1 et 2 validés le même jour. Ferme les entrées « no folder of Shared for a cross-cutting HTTP concern, authentication ended in Behaviors » et « HS015 does not see the 401 of an authorization policy » de `ghostline/docs/hardslice-friction.md`, et la tâche « dire où va l'authentification dans Shared et ce que HS015 attend des réponses du middleware ». `hardslice.json` généré autorise `["Messaging", "Persistence", "Behaviors", "Http"]`, format inchangé. `BadRequestExceptionHandler`, `ValidationExceptionHandler`, `DomainExceptionHandler` et `ExplicitOffsetJsonConverter` passent dans `Shared/Http` (espace de noms `MyApp.Shared.Http`, `using` ajouté à `Program.cs`, ordre et enregistrement inchangés), leurs tests unitaires dans `tests/MyApp.Tests/Shared/Http/`, dans les deux variantes. `Shared/Behaviors` ne garde que le pipeline du médiateur. `HS017.md` donne le rôle de chaque dossier dans un tableau (`Http` : réponses aux erreurs et aux requêtes illisibles, schémas d'authentification, politiques d'autorisation, limitation de débit, CORS), la liste des quatre dossiers dans la règle, l'exemple JSON et le message, et son correctif 4 envoie un schéma d'authentification partagé par plusieurs modules dans `Http`, jamais dans `Behaviors`. `AGENTS.md` sépare les lignes `Shared/Behaviors/` et `Shared/Http/`, qui renvoie à `HS015.md` pour la place d'une politique. La ligne `Shared/` est retirée, la règle d'or 7 disant désormais « never a use case » : 142 et 149 lignes, comme avant. `HS015.md` restreint la phrase sur la signature aux réponses que renvoie le délégué, dit que le module porte par son groupe les politiques qui couvrent tous ses endpoints, et a la section « Responses of the middleware » : un 401, un 403 ou un 429 n'est pas dans la signature, le schéma et ses politiques vivent dans `Shared/Http`, la politique va sur le groupe dans `<Module>Module.cs` quand elle couvre tout le module et sur l'endpoint dans sa slice sinon, une `FallbackPolicy` protège tout endpoint sans `AllowAnonymous`, et un test d'intégration du 401 tient lieu de la signature. Aucun message de diagnostic ne change. La spécification (disposition de `Shared`, ligne de HS017, décision du 04/11/2026) et `CHANGELOG.md` suivent. Les exécutions enregistrées de `demos/add-feature/` importent les gestionnaires depuis `Shop.Shared.Behaviors` : `changes.rename` accepte une ligne `using <ancien> <nouveau>`, qui ajoute `using <nouveau>;` après `using <ancien>;` dans les fichiers C# rejoués, et chaque exécution reçoit `using Shop.Shared.Behaviors Shop.Shared.Http` (README de la démonstration). Tests qui échouaient avant : `Template_hardslice_json_allows_the_default_Shared_folders_in_the_slices_style` (quatre dossiers), `Template_Shared_holds_the_allowed_folders_each_described_by_HS017_and_AGENTS_md` (dossiers de `Shared` égaux à la liste générée, rôle de chacun dans `HS017.md`, ligne de chacun dans `AGENTS.md` avec et sans l'exemple, réponses HTTP hors de `Behaviors`) et `HS015_sheet_says_what_it_expects_of_the_responses_of_the_middleware`. `ExceptionHandlersTests`, dans les deux variantes, fixe les réponses des trois gestionnaires (400 et motif d'une requête illisible, 400 et champs au nom du client d'une erreur de validation, 400 et 409 du domaine, toute autre exception laissée au suivant). Dans `scripts/test-template.sh`, la sonde `probe_shared_folders` remplace celle de `Shared/Helpers` dans les deux variantes : un schéma d'authentification par clé, sa politique et son enregistrement dans `Program.cs` sous `Shared/Http` passent `check.sh`, le même sous `Shared/Security` échoue sur HS017 avec les quatre dossiers (`check.sh`, et `check.ps1` sans l'exemple). La liste des tests de la fondation de `Blank` exige les tests de `Shared/Http`. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1232 tests verts), la sonde `probe_shared_folders` dans les deux variantes, les sondes HS017 du scénario avec l'exemple, la liste des tests de la fondation de `Blank`, `probe_hardslice_files`, `probe_claude_hooks` et `probe_codex_and_copilot_hooks`, lancées seules sur des solutions générées depuis les paquets locaux, et `scripts/demo-add-feature.sh verify-recorded`. Les paquets ne sont pas reconstruits : PB-080 les revalide. Contournement que Ghostline peut retirer : `Shared/Behaviors/AdminKey.cs` passe dans `Shared/Http/`, une fois son `hardslice.json`, protégé, mis à niveau par un mainteneur ou par la mise à niveau de PB-086. Aucun contournement pour HS015 : la politique sur l'endpoint de `Schedule` et la signature `Task<Ok<T>>` sont la forme que la fiche décrit désormais.

**Résultat observable**

Une solution générée autorise `Shared/Http`, qui porte les réponses HTTP aux erreurs et aux requêtes illisibles, et où `AGENTS.md` et `HS017.md` disent que vont l'authentification, les politiques d'autorisation, la limitation de débit et CORS. `Shared/Behaviors` ne porte plus que le pipeline du médiateur. `HS015.md` dit qu'une réponse du middleware (401, 403, 429) n'est pas dans la signature de l'endpoint, où va la politique, et quel test la couvre.

**Constat**

Ghostline, entrées « no folder of Shared for a cross-cutting HTTP concern, authentication ended in Behaviors » et « HS015 does not see the 401 of an authorization policy » (04/11/2026, feuille de route 2.1) :

- Le schéma d'authentification par clé d'administration (`AuthenticationHandler`, politique, extension `RequireAdminKey()`, options, avertissement au démarrage) protège des endpoints de deux modules. Il a fini dans `Shared/Behaviors/AdminKey.cs`, le dossier autorisé le plus proche, que le correctif 4 de `HS017.md` décrit comme « the request pipeline and HTTP error answers ». Un module `Admin`, sans agrégat ni cas d'usage, a été écarté.
- `HS015.md` dit que « `TypedResults` put every response of the endpoint in its signature ». Le 401 d'une politique est répondu par le middleware avant le délégué : la signature reste `Task<Ok<T>>`, et `Results<Ok<T>, UnauthorizedHttpResult>` mentirait dans l'autre sens. La fiche dit aussi que le module porte les politiques par son groupe, alors que `Schedule` mêle une lecture ouverte et une écriture protégée : la politique est allée sur l'endpoint, dans sa slice, ce que HS015 accepte sans le dire.

**Périmètre**

- `hardslice.json` généré : `"allowedFolders": ["Messaging", "Persistence", "Behaviors", "Http"]`, format inchangé (`"version": 1`). Les tests d'architecture de HS017, les sondes des dossiers de `Shared` et la spécification suivent.
- Les trois gestionnaires d'exceptions et `ExplicitOffsetJsonConverter` passent de `Shared/Behaviors` à `Shared/Http`, dans les deux variantes, avec leurs espaces de noms, leur enregistrement et leurs tests. L'ordre du pipeline et les réponses ne changent pas.
- `HS017.md` : le rôle de chaque dossier (`Behaviors` pour le pipeline du médiateur, `Http` pour le pipeline HTTP, avec l'authentification en exemple). `AGENTS.md` : les lignes de `Shared/Behaviors/` et `Shared/Http/` dans la carte, sous 150 lignes dans les deux variantes (149 aujourd'hui sans l'exemple : une ligne est regagnée ailleurs).
- `HS015.md` : un paragraphe sur les réponses du middleware. La politique va sur le groupe du module quand elle couvre tous ses endpoints, sur l'endpoint dans sa slice sinon. `FallbackPolicy` protège tout endpoint qui ne dit pas `AllowAnonymous`. Un test d'intégration du 401 tient lieu de la signature. La phrase sur la signature est restreinte aux réponses que renvoie le délégué. Aucun message de diagnostic ne change.
- `CHANGELOG.md` a sa ligne.

**Cas vérifiés**

- Un schéma d'authentification dans `Shared/Http` : vérification verte. Le même dans `Shared/Security` : HS017, dont le message liste les quatre dossiers.
- Une requête illisible, une erreur de validation et une erreur du domaine : mêmes réponses qu'avant le déplacement, dans les deux variantes.
- `AGENTS.md` : moins de 150 lignes avec et sans l'exemple.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application et des règles de scripts/test-template.sh, dans les deux variantes
```

### PB-080 : Revalider les paquets 0.3.0-preview.1 après PB-075 à PB-079 et PB-085

Statut : terminé le 04/11/2026. Dernier ticket avant PB-051, dont l'étiquette vise son commit. Ferme l'entrée « a rebuild under the same version stays invisible to the NuGet cache » de `ghostline/docs/hardslice-friction.md`. `CHANGELOG.md` dit déjà ce que livrent PB-075 à PB-079 et PB-085 : chaque ticket y avait écrit sa ligne, rien n'y manque. `HS001.md` disait encore que `Shared/Behaviors` traduit une `DomainException` ou une `ConflictException` : depuis PB-079, c'est `Shared/Http`, en 400 ou 409. Le README racine dit que le hook rend après une édition les erreurs des fichiers édités en entier et le compte des autres (PB-077), qu'un échec sur un fichier HardSlice protégé n'est rendu à l'agent qu'une fois, qui rend alors la main en disant ce qu'un mainteneur doit faire (PB-078), et que `check` donne un `Fix:` à chaque erreur sur un fichier HardSlice (PB-076). Le README du paquet de templates dit la même chose de ses hooks. Les README ne présentent pas les dossiers de `Shared` ni HS902 au-delà de ce qui reste juste, et celui de l'outil suivait déjà PB-076 à PB-078. `CONTRIBUTING.md` dit qu'une reconstruction sous la même version laisse deux caches servir l'ancienne en silence, et qu'il faut purger les deux : le dossier global des paquets NuGet (`dotnet nuget locals global-packages --clear`, ou les seuls dossiers `hardslice.*` de `~/.nuget/packages/`) et le fichier `hardslice.tool` de `~/.dotnet/toolResolverCache/1/`. Paquets reconstruits dans `artifacts/packages` sur ce commit. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1234 tests verts), `scripts/test-template.sh` complet (environ 1 h) et `scripts/verify-published.sh 0.3.0-preview.1 --source artifacts/packages` sur les paquets reconstruits, tous verts. Contournement que Ghostline peut retirer : aucun dans le code. Sa purge à la main est désormais la procédure écrite : à faire une fois sur ce commit, avant d'y refaire sa migration. C'est la dernière reconstruction avant la publication.

**Résultat observable**

Le parcours de PB-059 passe à nouveau sur les paquets construits après PB-075 à PB-079 et PB-085, et l'étiquette de PB-051 peut viser ce commit.

**Périmètre**

- `CHANGELOG.md` : l'entrée `0.3.0-preview.1` dit ce que PB-075 à PB-079 ont changé (correctif de HS902 pour un objet valeur, `Fix:` et `Doc:` des erreurs de l'outil sur les fichiers HardSlice, erreurs des fichiers édités d'abord après une édition, fin de tour sur un fichier HardSlice protégé, dossier `Shared/Http` et réponses du middleware dans HS015, migrations qui convertissent des lignes et mapping de champs privés dans `add-aggregate`). Chaque ticket y a écrit sa ligne : ce ticket vérifie que l'entrée dit ce que livre la version.
- `README.md` et README des paquets : ce que PB-075 à PB-079 et PB-085 changent de ce qu'ils présentent (dossiers de `Shared`, erreurs de l'outil, réponses de ses hooks).
- Passage complet de `scripts/test-template.sh` et de `scripts/verify-published.sh --source artifacts/packages` sur les paquets reconstruits.
- Les paquets ne sont reconstruits qu'ici : Ghostline purge ses caches et refait sa migration sur ce commit seulement.
- Statut de PB-051 : le commit que vise l'étiquette.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
scripts/verify-published.sh 0.3.0-preview.1 "$(mktemp -d)" --source artifacts/packages
```

### PB-081 : Générer et tester un endpoint protégé avec `--require`

Statut : terminé le 04/11/2026, arbitrage 3 validé le même jour. Ferme l'entrée « no template option, test helper or skill for a protected endpoint » de `ghostline/docs/hardslice-friction.md`, et la tâche « générer et tester un endpoint protégé (--require, client authentifié d'AppFactory, skill) ». `hardslice-command` et `hardslice-query` prennent `--require <policy>` : l'endpoint chaîne `.RequireAuthorization("<policy>")` dans sa slice (corps du délégué en retrait, appel chaîné sur sa ligne, comme l'endpoint de Ghostline), avec un commentaire qui dit que le 401 ou le 403 n'est pas dans la signature (HS015). Les tests générés passent par `app.CreateAuthorizedClient("<policy>")`, un symbole qui remplace `app.CreateClient().` aux mêmes appels, et un test `Rejects_a_request_without_authentication_before_its_handler` envoie la requête sans client authentifié et attend 401 : après le test du 400 d'une commande avec paramètres, après celui du succès d'une commande sans paramètres, après celui du 404, du 400 ou de la liste d'une requête. Le nom de la politique est fait de lettres, de chiffres, `.`, `_`, `:` et `-`, en commençant par une lettre : un autre est refusé (code 105, rien de créé), comme `--require` avec `--endpoint none` ou `--every`, qui n'ont pas d'endpoint. Après les lignes d'enregistrement, le template dit ce que la politique demande (schéma dans `Shared/Http`, client dans `AuthorizedClients` d'`AppFactory`). `AppFactory`, le même fichier dans les deux variantes, a la table `AuthorizedClients` (nom de la politique, insensible à la casse comme ASP.NET Core, vers une action qui configure le client), vide à la génération et remplie une fois par l'application. `CreateAuthorizedClient(policy)` est une extension de `WebApplicationFactory<Program>`, déclarée à côté : elle sert aussi une instance dérivée par `WithWebHostBuilder`, comme `FeedServer.App` de Ghostline, et lève une `InvalidOperationException` qui dit où ajouter le client d'une politique absente. `add-slice` a la section « Protected endpoints » (schéma et politiques dans `Shared/Http`, groupe ou endpoint comme le dit `HS015.md`, `--require`, client de test d'`AppFactory`, test du 401), renvoyée depuis les étapes 4 et 7 et sa description. Pas de skill `add-authorization`. `AGENTS.md` ne change pas (142 et 149 lignes). `docs/item-templates.md`, le README racine, celui du paquet de templates et `CHANGELOG.md` suivent. Sans l'option, les fichiers et les lignes imprimées sont identiques à ceux du template de HEAD, vérifié sur 26 combinaisons d'options des deux templates (réponses, paramètres, méthodes, routes, `--endpoint none`, `--every`, refus). Tests qui échouaient avant : `Slice_item_templates_protect_their_endpoint_with_the_policy_of_require` (commande et requête), `Slice_item_templates_accept_a_policy_name_that_a_string_of_the_endpoint_can_hold`, `AppFactory_holds_the_client_of_each_authorization_policy` et `Add_slice_says_how_to_protect_an_endpoint`, plus le compte des refus et des actions finales des deux templates. Dans `scripts/test-template.sh`, la sonde `probe_protected_endpoints`, dans les deux variantes : refus de `--require` avec `--endpoint none`, `--every` et deux noms invalides (code 105, rien de créé), puis, sur le schéma par clé de PB-079 dans `Shared/Http`, une commande avec paramètres, une commande sans paramètres et une requête générées avec `--require ProbeKey`. Tant qu'`AppFactory` n'a pas le client, les tests qui l'envoient échouent avec le message attendu et les trois tests du 401 passent. Le client ajouté, seuls les tests `TODO(hardslice):` échouent, puis la vérification passe après la complétion de référence. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1250 tests verts), et la sonde `probe_protected_endpoints` lancée seule sur des solutions générées depuis les paquets locaux, avec l'exemple (`Bookings`) et sans (`Rooms`, après `probe_module_template` et `probe_aggregate_template`). Les paquets ne sont pas reconstruits : PB-088 les revalide. Contournement que Ghostline peut retirer : l'extension `CreateAdminClient()` et sa classe `AdminClient` de `tests/Ghostline.IntegrationTests/AppFactory.cs`, une fois le fichier mis à niveau : la clé de test reste dans `ConfigureWebHost`, son client devient l'entrée `[AdminKey.Policy] = client => client.DefaultRequestHeaders.Add(AdminKey.Header, AdminApiKey)` d'`AuthorizedClients`, et les quatre appels deviennent `CreateAuthorizedClient(AdminKey.Policy)`. Ses prochains endpoints d'administration se génèrent avec `--require` au lieu d'ajouter la politique et le test du 401 à la main.

**Résultat observable**

`dotnet new hardslice-command --module Schedule --name ImportSchedule --parameters none --route versions --require AdminKey` génère un endpoint qui chaîne `.RequireAuthorization` avec la politique `AdminKey`, un test d'intégration du 401 sans authentification, et un test du succès qui passe par le client authentifié d'`AppFactory`. `add-slice` dit comment protéger une slice.

**Constat**

Ghostline, entrée « no template option, test helper or skill for a protected endpoint » (04/11/2026, feuille de route 2.1) : rien dans `add-slice`, les templates ni `AppFactory` ne couvre un endpoint qui exige une authentification. Après `RequireAdminKey()`, chaque test d'intégration qui postait sur les deux endpoints répondait 401, jusqu'à l'ajout de `AppFactory.AdminApiKey` et d'une extension `CreateAdminClient()` (quatre appels dans trois fichiers). Le test généré d'une commande ne prévoit pas de 401.

**Périmètre**

- `hardslice-command` et `hardslice-query` : `--require <policy>` chaîne `.RequireAuthorization` à l'endpoint dans sa slice, génère le test du 401 à côté de celui du 400, et fait passer les autres tests générés par le client authentifié. Refusé avec `--endpoint none` et `--every` (code 105, rien de créé) : une commande système n'a pas d'endpoint.
- `AppFactory`, dans les deux variantes : une place pour le client authentifié d'une politique, remplie une fois par l'application (forme libre, client (a) de l'arbitrage 3). Une politique absente de cette place fait échouer le test avec un message qui dit où l'ajouter.
- `add-slice` : une section « Protected endpoints » (choisir la politique, groupe ou endpoint comme le dit `HS015.md`, `--require`, client de test). Pas de skill `add-authorization` (arbitrage 3).
- `docs/item-templates.md`, README du paquet de templates, `CHANGELOG.md`.

**Cas vérifiés**

- Commande avec et sans paramètres, et requête, avec `--require` : vérification verte après complétion de référence, sur une application qui a un schéma dans `Shared/Http` et le client de sa politique dans `AppFactory`.
- Politique sans client dans `AppFactory` : le test échoue avec le message attendu.
- `--require` avec `--endpoint none` ou `--every` : code 105, rien de créé.
- Sans l'option : fichiers identiques à ceux d'avant.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-082 : Envoyer une commande à heure fixe, avec reprises, par le service des commandes récurrentes

Statut : terminé le 04/11/2026, arbitrage 4 validé le même jour (option A, pour toute commande sans paramètre). Ferme l'entrée « second background service, still no template nor documentation » de `ghostline/docs/hardslice-friction.md`, l'entrée « RecurringCommandCycles takes a bare AppFactory, with no way to configure the instance it builds » et, pour une commande planifiée, l'entrée « FakeTimeProvider does not say which timers wait on it », ainsi que la tâche « arbitrer et livrer un service hébergé non périodique (heure quotidienne, reprises) ». `Shared/Messaging/RecurringCommand.cs`, dans les deux variantes : `AddRecurringCommand<TCommand, TResponse>(section, interval)` reste, et une surcharge prend un `RecurringSchedule` : `RecurringSchedule.Every(interval)`, `RecurringSchedule.DailyAt(new TimeOnly(4, 30), "America/Toronto")` (fuseau IANA, ou `UTC`) et `.WithRetries(delay, count)`. La contrainte devient `TCommand : ICommand<TResponse>, new()` : une commande système ou une commande que son endpoint envoie aussi. Les clés `Interval`, `DailyAt`, `TimeZone`, `RetryDelay` et `Retries` de la section remplacent les valeurs du code, validées au démarrage (exactement un de `Interval` et `DailyAt`, un fuseau que la machine connaît, des reprises positives avec un délai). Le service ne repose plus sur un `PeriodicTimer` : il fixe ses instants depuis le démarrage de l'hôte (`StartAsync`), attend chacun par `Task.Delay(due - now, timeProvider)`, et un instant déjà passé part aussitôt, l'horaire reprenant de là. Une heure locale qu'un changement d'heure saute avance de la durée du changement (02:30 devient 03:30 à Toronto le 14/03/2100), une heure répétée ne part qu'une fois, à son premier instant. La reprise k part k délais après l'instant du cycle en échec, une reprise réussie arrête les reprises, puis vient l'instant de l'horaire qui suit la dernière. Le journal dit « retry 1 of 3 is sent in 01:00:00 », puis « the next one is sent in 21:00:00 ». Le service prévient une interface interne `IRecurringCommandMonitor<TCommand, TResponse>` de chaque instant attendu et de chaque issue, que l'application n'enregistre jamais et que les tests enregistrent. `RecurringCommandCycles` s'en sert : `RunNextAsync()` attend que le service attende, avance l'horloge à cet instant (intervalle, heure du jour ou reprise) et rend la réponse ou l'exception du cycle, `NextDueAsync()` donne l'instant sans avancer, `FailNext(count)` fait échouer les cycles suivants avant le pipeline (`FailedCycleException`), et un second argument facultatif `Action<IWebHostBuilder>` configure l'instance qui fait les cycles, avant son horloge. `hardslice-command` prend `--daily-at <hh:mm>`, `--time-zone <id>` (`UTC` par défaut) et `--retry <délai>x<nombre>` (délai `[d.]hh:mm:ss` sous 49 jours, nombre de 1 à 99), aussi avec `--every`. `--daily-at` implique `--endpoint none` et `--parameters none` comme `--every`. Refus (code 105, rien de créé) : une heure qui n'est pas `hh:mm`, un fuseau qui n'est pas un identifiant IANA (un nom Windows compris), des reprises invalides, `--every` avec `--daily-at`, `--daily-at` avec `--parameters id` ou `--endpoint http`, `--time-zone` sans `--daily-at`, `--retry` sans horaire, et `--response none` ou `--require` avec `--daily-at`. Le template imprime par exemple `.AddRecurringCommand<ImportNightlyScheduleCommand, Guid>("Schedule:ImportNightlySchedule", RecurringSchedule.DailyAt(new TimeOnly(4, 30), "America/Toronto").WithRetries(TimeSpan.Parse("01:00:00", CultureInfo.InvariantCulture), 3))`, et génère `Runs_a_cycle_every_day_at_its_local_time` (deux jours, heure locale vérifiée par `NextDueAsync`) et, avec `--retry`, `Sends_a_failed_cycle_again_at_each_retry_then_waits_for_its_next_instant`, vert dès la génération. Sans les nouvelles options, les fichiers et les lignes imprimées sont identiques à ceux du template de HEAD sur 27 combinaisons d'options, seuls trois messages de refus nommant désormais `--daily-at`. `AGENTS.md` (puce « Background work », étape 2, carte et règle d'or 1, toujours 142 et 149 lignes), `add-slice` (l'option, la commande avec endpoint planifiée en une ligne, `NextDueAsync`, `FailNext` et la configuration de l'instance), `docs/item-templates.md`, les fiches HS004 et HS010, le README racine, celui du paquet de templates et `CHANGELOG.md` suivent. Tests qui échouaient avant : dans `RecurringCommandTests`, l'envoi quotidien à 04:30 de Toronto sur deux nuits, les changements d'heure de mars et novembre 2100, trois reprises puis la nuit suivante et leurs journaux, une reprise réussie qui arrête les reprises, une commande ordinaire planifiée, la configuration de l'heure, du fuseau et des reprises, et les horaires refusés au démarrage. Dans `TemplatePackageTests`, `Command_item_template_accepts_a_daily_time_a_time_zone_and_retries_it_can_write`, `Command_item_template_generates_a_daily_command_with_retries_and_refuses_the_options_it_contradicts`, `Recurring_commands_follow_a_daily_schedule_with_retries_and_their_cycles_configure_their_instance` et `Agent_guidance_says_how_to_schedule_a_command_every_day_with_retries`, plus le compte des refus et des enregistrements imprimés. Dans `scripts/test-template.sh`, la sonde `probe_scheduled_commands`, dans les deux variantes : les treize refus (code 105, rien de créé), trois commandes planifiées (quotidienne à Toronto avec trois reprises, intervalle avec deux reprises et un DTO, quotidienne en UTC sans `--time-zone`), et une commande avec endpoint et `--parameters none` planifiée par une ligne de son module, envoyée par son endpoint et par l'horaire, HS004 inchangé, dont un test configure l'instance des cycles par `UseSetting`. La vérification échoue sur les seuls tests `TODO(hardslice):`, puis passe après la complétion de référence (`complete_scheduled_command` de `scripts/item-completion.sh`). Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build`, et les sondes `probe_recurring_commands` et `probe_scheduled_commands` lancées seules sur des solutions générées depuis les paquets locaux, avec l'exemple (`Bookings`) et sans (`Rooms`). Les paquets ne sont pas reconstruits : PB-088 les revalide. Contournement que Ghostline peut retirer : `ScheduleImporter`, `NightlyImport` pour le calcul de la nuit suivante, l'interrupteur `Schedule:ImporterEnabled` et sa ligne d'`AppFactory`, et `WaitingClock`, une fois `RecurringCommand.cs` et `RecurringCommandCycles.cs` repris du template. `ScheduleModule` enregistre `.AddRecurringCommand<ImportScheduleCommand, ImportedScheduleDto>("Schedule:ImportSchedule", RecurringSchedule.DailyAt(new TimeOnly(4, 30), "America/Toronto").WithRetries(TimeSpan.FromHours(1), 3))` après son handler, `ImportSchedule` garde son endpoint d'administration, `Schedule:ImportAt`, `RetryAfter` et `RetriesPerNight` deviennent `Schedule:ImportSchedule:DailyAt`, `RetryDelay` et `Retries`, et `ScheduleImporterTests` se réécrit avec `RecurringCommandCycles` (`NextDueAsync`, `FailNext`). Le paramètre `configure` ajouté par Ghostline à `RecurringCommandCycles` existe dans le template, `RealtimeCollectorTests` le garde tel quel. L'import au démarrage quand aucune version ne couvre le jour reste à PB-083.

**Résultat observable**

`dotnet new hardslice-command --module Schedule --name ImportNightlySchedule --daily-at 04:30 --time-zone America/Toronto --retry 01:00:00x3` génère une commande système sans paramètre, son enregistrement dans le module et un test d'intégration qui fait passer deux nuits et trois reprises sans attente réelle. Une commande existante sans paramètre, comme `ImportSchedule`, s'enregistre sur le même horaire en une ligne et garde son endpoint. `ScheduleImporter` et `WaitingClock` de Ghostline n'ont plus de raison d'être.

**Constat**

Ghostline, entrées « second background service, still no template nor documentation », « FakeTimeProvider does not say which timers wait on it » et « RecurringCommandCycles takes a bare AppFactory » (04/11/2026, feuille de route 2.2) :

- `ScheduleImporter` importe le GTFS chaque nuit à 04:30, heure d'Ottawa, et réessaie une heure plus tard après un échec. Il a été écrit à la main en recopiant `RealtimeCollector` presque ligne pour ligne (garde de l'interrupteur, scope et `Send`, exception journalisée, annulation à l'arrêt, interrupteur dans `AppFactory`). Seul le déclencheur diffère : un instant suivant calculé et `Task.Delay(delay, TimeProvider, token)`. `AddRecurringCommand` (PB-068) ne couvre qu'un intervalle fixe, et seulement une commande système, alors que `ImportSchedule` est aussi envoyée par son endpoint d'administration.
- Ses tests attendent que l'importeur attende, par `WaitingClock`, une sous-classe de `FakeTimeProvider` d'environ 85 lignes : avancer l'horloge à l'aveugle croise la création de sa minuterie.
- `RecurringCommandCycles` n'accepte qu'un `AppFactory`, scellé, et ne peut donc pas tester une commande qui lit un service HTTP simulé ou un réglage. Ghostline lui a ajouté un paramètre `configure`, dans un fichier venu du template.

**Périmètre**

- `Shared/Messaging/RecurringCommand.cs`, dans les deux variantes : un horaire quotidien (heure locale, fuseau IANA, instant suivant calculé depuis le `TimeProvider` du conteneur, changement d'heure défini : une heure qui n'existe pas passe à la suivante, une heure qui se répète ne compte qu'une fois) et des reprises après un cycle en échec (délai, nombre), aussi pour l'intervalle fixe. Forme exacte libre. Réglages remplaçables par la configuration de la section, comme `Interval`.
- Commande envoyée : toute commande sans paramètre (`new()`), système ou non (arbitrage 4). Une commande système reste la forme d'une commande que seule l'application envoie (PB-067).
- `hardslice-command` : `--daily-at <hh:mm>`, `--time-zone <id>` et `--retry <délai>x<nombre>` (noms libres), qui impliquent `--endpoint none` et `--parameters none` comme `--every`. Refus (code 105, rien de créé) : `--every` avec `--daily-at`, `--time-zone` sans `--daily-at`, une heure, un fuseau ou des reprises invalides.
- `RecurringCommandCycles` : avance l'horloge jusqu'à l'instant dû suivant, quel que soit l'horaire, rend la réponse ou l'exception de chaque reprise, et accepte une configuration de l'instance qu'il construit.
- `AGENTS.md` (puce « Background work », sous 150 lignes), `add-slice`, `docs/item-templates.md`, README du paquet de templates, `CHANGELOG.md`.

**Cas vérifiés**

- `--daily-at 04:30 --time-zone America/Toronto` : un cycle à chaque 04:30 locale sur deux nuits, puis sur la nuit d'un changement d'heure, sans attente réelle.
- Un cycle en échec avec `--retry 01:00:00x3` : trois reprises à une heure d'intervalle, puis la nuit suivante. Une reprise réussie arrête les reprises.
- Une commande avec endpoint enregistrée sur l'horaire : envoyée par l'horaire et par son endpoint, HS004 inchangé.
- Un test qui configure l'instance (client HTTP simulé, réglage) avec `RecurringCommandCycles`.
- Combinaisons refusées : code 105, rien de créé. Sans les nouvelles options : fichiers identiques à ceux d'avant.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application et des templates d'éléments de scripts/test-template.sh, dans les deux variantes
```

### PB-083 : Donner une condition de démarrage à une commande planifiée

Statut : terminé le 04/11/2026, arbitrage 5 validé le même jour (condition B). Ferme, pour la condition, l'entrée « a background service that must read before it sends its command has no sanctioned path » de `ghostline/docs/hardslice-friction.md` (la règle reste à PB-084), et les entrées « AddRecurringCommand has no start condition, so a missing key fails every cycle » et « AppFactory also removes the hosted services that only check the configuration at start ». `Shared/Messaging/RecurringCommand.cs`, dans les deux variantes : une condition est un `IRecurringCommandCondition<TCommand>`, nommé `<Slice>Condition`, dans le dossier de la slice, que le module enregistre après le handler par `.AddScoped<IRecurringCommandCondition<TCommand>, <Slice>Condition>()`. Avant chaque envoi, reprises comprises, le service résout les conditions de la commande dans un scope à elles, distinct de celui de la commande, et appelle `CheckAsync(RecurringCycle cycle, CancellationToken)` : `null` envoie la commande, une raison la retient. `RecurringCycle(Due, AtStart, Retry)` dit l'instant du cycle, si c'est l'envoi au démarrage ou l'une de ses reprises, et le numéro de la reprise, pour qu'une condition ne vise que l'envoi au démarrage. Un cycle retenu n'envoie rien, n'est pas repris, et l'horaire continue. La raison est journalisée une fois en `Information` (« CollectRealtimeFeedsCommand is not sent: NextRip:ApiKey is not set »), de nouveau quand elle change, et « is sent again: its conditions hold » au premier envoi qui suit. Une condition qui lève fait échouer le cycle comme la commande, avec ses reprises. `RecurringSchedule.AlsoAtStart()`, ou la clé `AtStart` de la section, envoie aussi la commande au démarrage de l'hôte, cycle soumis aux mêmes conditions et aux mêmes reprises, puis l'horaire reprend à son premier instant. `IRecurringCommandMonitor` apprend `Held(reason)`, et `RecurringCommandCycles.RunNextAsync()` lève `HeldCycleException`, dont le message est la raison, pour un cycle retenu. `ProgramTests` vérifie aussi l'enregistrement des conditions, par leur contrat. `AppFactory`, dans les deux variantes, journalise avant le démarrage de l'hôte une ligne `Information` par service hébergé de l'application qu'il retire : « AppFactory removed the hosted service MissingAdminKeyWarning of the application: a test that needs it calls KeepHostedService<MissingAdminKeyWarning>() on its AppFactory ». `GenericWebHostService`, qu'ASP.NET Core enregistre de nouveau après les services de test, n'est pas journalisé. `AGENTS.md` (puce « Background work » et ligne des tests d'intégration de la carte, toujours 142 et 149 lignes), `add-slice` (la condition, le cycle reçu, l'envoi au démarrage, la vérification d'un réglage dont la commande a besoin par une condition plutôt que par un service hébergé ou `ValidateOnStart`, `HeldCycleException`), `docs/item-templates.md` (aucune option ne génère la condition) et `CHANGELOG.md` suivent. Tests qui échouaient avant : dans `RecurringCommandTests`, la commande retenue sur trois cycles avec une seule ligne de journal et l'hôte qui continue, puis envoyée au cycle suivant, la raison journalisée de nouveau quand elle change, le scope propre des conditions, une condition qui lève et sa reprise, l'envoi au démarrage selon une condition qui ne vise que lui, le cycle reçu par la reprise de l'envoi au démarrage, et la clé `AtStart`. Dans `TemplatePackageTests`, `Recurring_commands_check_their_conditions_before_each_send_and_AppFactory_logs_the_hosted_services_it_removes` et `Agent_guidance_says_how_a_job_decides_whether_to_run`. Dans `scripts/test-template.sh`, dans les deux variantes : `probe_integration_isolation` capture les journaux de ses tests et y trouve la ligne du service retiré, absente quand le test le garde, et la nouvelle sonde `probe_recurring_condition` génère une commande récurrente, lui ajoute une condition qui lit un réglage dans le dossier de sa slice, vérifie que `ProgramTests` la nomme tant que le module ne l'enregistre pas, puis passe `check.sh` avec ses tests : des cycles retenus avec leur raison sans le réglage, et l'envoi au démarrage par la clé `AtStart` de la section. Les tests d'intégration du modèle ne peuvent pas porter ces cas eux-mêmes : un service ajouté par `WithWebHostBuilder` passe après le retrait d'`AppFactory`, d'où les sondes sur une solution générée. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1304 tests verts), et lancées seules sur des solutions générées depuis les paquets locaux, avec l'exemple (`Bookings`) et sans (`Rooms`), `probe_integration_isolation` et `probe_recurring_condition`, plus `probe_scheduled_commands` avec l'exemple, pour la boucle du service réécrite autour de `RecurringCycle`. Les paquets ne sont pas reconstruits : PB-088 les revalide. Contournement que Ghostline peut retirer, une fois `RecurringCommand.cs`, `RecurringCommandCycles.cs`, `AppFactory.cs` et `ProgramTests.cs` repris du template : l'erreur de chaque cycle sans clé de `CollectRealtimeFeeds` et son test `Fails_each_cycle_without_a_key_and_leaves_the_API_running`, remplacés par `CollectRealtimeFeedsCondition` (« NextRip:ApiKey is not set ») enregistré dans `RealtimeModule` et un test qui attend `HeldCycleException`, et l'import au démarrage de `ScheduleImporter`, remplacé par `.AlsoAtStart()` sur l'horaire de `ImportScheduleCommand` et `ImportScheduleCondition`, qui lit `AppDbContext` en `AsNoTracking()` avec `ScheduleVersion.InForceOn` et ne retient que le cycle `AtStart` quand une version couvre le jour d'Ottawa. `MissingAdminKeyWarning`, qui vérifie la clé d'une politique et non le réglage d'une commande, reste un service hébergé gardé par `KeepHostedService` dans son test, et la ligne d'`AppFactory` dit maintenant pourquoi il manque aux autres.

**Résultat observable**

Une commande planifiée peut porter une condition, évaluée dans un scope à elle avant un envoi. Sans la clé NextRip, `CollectRealtimeFeeds` n'est pas envoyée et une seule ligne de journal dit pourquoi. L'import au démarrage de Ghostline n'a lieu que si aucune version ne couvre le jour. `AGENTS.md` dit comment un job décide de s'exécuter, et `AppFactory` journalise chaque service hébergé qu'il retire.

**Constat**

- Entrée « a background service that must read before it sends its command has no sanctioned path » (04/11/2026, feuille de route 2.2) : au démarrage, `ScheduleImporter` n'importe que si aucune version ne couvre le jour courant d'Ottawa. Les chemins permis ne conviennent pas : une seconde requête dans la slice est interdite (HS004), une slice de requête à elle ne peut pas servir depuis `ImportSchedule` (HS016), et un paramètre `UnlessCovered` demanderait un validateur sans rien à valider (HS013) et mêlerait le minuteur à la commande HTTP. Le service lit `AppDbContext` dans son scope, en `AsNoTracking()`.
- Entrée « AddRecurringCommand has no start condition, so a missing key fails every cycle » : `RealtimeCollector` vérifiait `NextRip:ApiKey` une fois au démarrage. Avec `AddRecurringCommand`, chaque cycle lève la même exception du handler, journalisée en erreur toutes les 30 secondes avec sa pile. `ValidateOnStart` arrêterait toute l'API.
- Entrée « AppFactory also removes the hosted services that only check the configuration at start » : `AppFactory` retire `MissingAdminKeyWarning`, un `IHostedService` qui ne fait que journaliser un réglage manquant, et rien dans l'échec du test ni dans ses journaux ne le dit.

**Périmètre**

- Service générique de `Shared/Messaging`, dans les deux variantes : une condition facultative (forme libre), évaluée dans un scope à elle avant chaque envoi, qui retient la commande et journalise pourquoi une fois, puis de nouveau quand la raison change. Un envoi facultatif au démarrage, soumis à la même condition.
- La condition lit des options, ou la base par `AppDbContext` sans suivi, dans le dossier de la slice. `AGENTS.md` (puce « Background work », sous 150 lignes) et `add-slice` le disent, avec la vérification d'un réglage au démarrage par une condition plutôt que par un service hébergé ou `ValidateOnStart`.
- `AppFactory`, dans les deux variantes : une ligne `Information` par service hébergé retiré, qui nomme `KeepHostedService<T>()`.
- `docs/item-templates.md` si `hardslice-command` imprime la condition, `CHANGELOG.md`.

**Cas vérifiés**

- Condition fausse : aucune commande envoyée, une seule ligne de journal sur plusieurs cycles, l'hôte continue. Condition redevenue vraie : la commande part au cycle suivant.
- Envoi au démarrage : envoyé quand la condition est vraie, pas sinon.
- Un test d'intégration qui retire un service hébergé : la ligne d'`AppFactory` est dans les journaux capturés.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes du template d'application de scripts/test-template.sh, dans les deux variantes
```

### PB-084 : Étendre HS006 aux services hébergés et aux conditions d'une slice

Statut : terminé le 04/11/2026, arbitrage 5 validé le même jour (règle B). Ferme, pour la règle, l'entrée « a background service that must read before it sends its command has no sanctioned path » de `ghostline/docs/hardslice-friction.md`, dont PB-083 a fermé la condition. HS006 examine aussi, dans les espaces de noms de slice, les types qui implémentent `IHostedService` (directement ou par `BackgroundService`, abstraits compris) et ceux qui implémentent `IRecurringCommandCondition<TCommand>`, reconnue par symbole : l'interface de ce nom déclarée dans l'espace de noms et l'assembly du contrat `requestHandler`, soit `Shared/Messaging`, sans nouvelle clé dans `hardslice.json`. Mêmes cas que pour un handler de requête, sur tout leur code (types imbriqués et lambdas compris) : écriture, suivi rallumé ou reconfiguré, `Find`, lecture sans garantie locale. Le message nomme le sujet et sa slice, avec sa propre règle et ses propres corrections : « hosted service 'VisitPurger' of slice 'Bookings/PurgeVisits' writes through 'SaveChangesAsync'. The hosted services of a slice read without tracking and never write. Fix: send a command through IMediator in a scope of its own, and move the write into its handler. » et, pour une condition, « The conditions of a recurring command read without tracking and never write », avec « move the write into the handler of 'PurgeBookingsCommand', the command that the condition lets go ». Une lecture se corrige par « add AsNoTracking() where the query starts, and project to the values it needs ». Le message d'un handler de requête ne change pas. Le titre du descripteur devient « A query handler, or a hosted service or a condition of a slice, tracks or writes ». Les services hébergés de `Shared` (dont `RecurringCommandService`) et ceux de la racine d'un module ne sont pas examinés. HS003 ne change pas. `HS006.md` (titre, règle, raison, diagnostics, mauvais et bon exemples d'un service et d'une condition, correction, contournements interdits, limites), `docs/rules.md`, `docs/design/specifications.md`, README racine et des analyseurs, `CHANGELOG.md`, `AGENTS.md` (puce « Background work », toujours 142 et 149 lignes) et `add-slice` suivent. Tests qui échouaient avant : dans `QueryHandlerTrackingAnalyzerTests`, le `BackgroundService` d'une slice qui écrit (`Add`, `SaveChangesAsync`), lit en suivant et appelle `FindAsync`, dont la lecture en `AsNoTracking()` ne produit rien, un `IHostedService` direct avec `AsTracking`, `QueryTrackingBehavior` et un type imbriqué, une base abstraite, et une condition qui lit sans `AsNoTracking()` puis écrit. Restent verts les cas sanctionnés (condition et service qui lisent en `AsNoTracking()` ou projettent) et les services et conditions de `Shared` et de la racine d'un module, non examinés. Dans `TemplatePackageTests`, `Agent_guidance_says_that_HS006_covers_the_hosted_services_and_conditions_of_a_slice`. Dans `scripts/test-template.sh`, dans les deux variantes, `probe_query_tracking` génère aussi une slice dont le service hébergé écrit et dont la condition lit sans `AsNoTracking()`, et attend les deux diagnostics des deux scripts de vérification. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1310 tests verts), et, lancées seules sur des solutions générées depuis les paquets locaux avec l'exemple (`Bookings`) et sans (`Rooms`), `probe_query_tracking` et `probe_recurring_condition`, dont la condition sanctionnée passe HS006. `scripts/measure-analyzers.sh`, lancé avec un rapport copié hors du dépôt (`docs/performance/analyzers.md` n'est pas réécrit), tient les seuils de A à D sur la médiane : A 4,9 s (+0,5 s), B 5,8 s (+0,1 s), C +5 %, D 5,8 s. Le scénario E s'est arrêté dès son tour d'échauffement sur un test unitaire du modèle étranger à HS006, `RecurringCommandTests.Sends_a_new_command_at_each_interval_and_never_before_the_first_one` (délai de 10 s dépassé, machine chargée) : il avance l'horloge sans attendre que le service ait créé sa minuterie, et une avance qui tombe entre la lecture de l'heure et la création de la minuterie dans `WaitAsync` repousse le premier cycle d'un intervalle. Ce test instable, venu de PB-082 et PB-083, reste à corriger dans un ticket à part. Les paquets ne sont pas reconstruits : PB-088 les revalide. Contournement que Ghostline peut retirer : aucun code. La lecture directe de `ScheduleImporter` en `AsNoTracking()`, que l'entrée tenait pour un contournement sans diagnostic, est maintenant examinée par HS006, comme le sera `ImportScheduleCondition`, qui la remplace avec PB-083 : elle n'écrit pas et lit en `AsNoTracking()`, donc sans diagnostic. `MissingAdminKeyWarning`, dans `Shared/Behaviors`, n'est pas examiné. L'entrée peut passer à « fixed by PB-083 and PB-084 ».

**Résultat observable**

Un service hébergé déclaré dans une slice, ou la condition d'une commande planifiée, qui écrit dans la base, rallume le suivi ou lit une entité sans garantie de non-suivi produit HS006. Une lecture en `AsNoTracking()` ne produit rien.

**Constat**

Ghostline, entrée « a background service that must read before it sends its command has no sanctioned path » (04/11/2026, feuille de route 2.2) : HS003 ne couvre que les endpoints, HS006 que les handlers de requête. Un service hébergé d'une slice peut lire et même écrire la base hors du médiateur, de son pipeline et de sa transaction, sans diagnostic.

**Périmètre**

- HS006 examine aussi, dans les espaces de noms de slice, les types qui implémentent `IHostedService` (directement ou par `BackgroundService`) et les conditions de PB-083, reconnus par symbole. Mêmes cas que pour un handler de requête : écriture, suivi rallumé, lecture qui suit toujours, lecture sans garantie locale. Le message nomme le service ou la condition.
- HS003 ne change pas (arbitrage 5).
- `HS006.md` (titre et règle), `docs/rules.md`, README racine et des analyseurs, `CHANGELOG.md`.

**Cas vérifiés**

- `SaveChangesAsync` ou `Add` dans un `BackgroundService` d'une slice : HS006. Lecture en `AsNoTracking()` : aucun diagnostic.
- Condition qui lit sans `AsNoTracking()` : HS006.
- Service hébergé de `Shared` : non examiné.
- Référence Bookings : diagnostics inchangés. Seuils de `scripts/measure-analyzers.sh` tenus.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/measure-analyzers.sh
sondes des règles de scripts/test-template.sh
```

### PB-085 : Documenter une migration qui convertit des lignes existantes et le mapping de champs privés

Statut : terminé le 04/11/2026, arbitrage 6 validé le même jour (option C). Avancé avant PB-080 par décision du mainteneur, donc avant PB-051. Ferme, avec PB-066, l'entrée « no template nor skill to change the shape of a child entity » de `ghostline/docs/hardslice-friction.md`, et la tâche « documenter une migration qui convertit des lignes existantes et le mapping de champs privés ». `add-aggregate` a deux sections de plus, avant « Never ». « Changing the shape of stored rows » : lire `Up` et `Down` avant d'appliquer la migration, reconnaître une perte (`DropTable`, `DropColumn`, `AlterColumn` de type, `AddColumn` à défaut inventé, avertissement « may result in the loss of data ») et la réécrire en conversion par `migrationBuilder.Sql` (nouvelle table remplie par `INSERT INTO ... SELECT ...`, ou colonne ajoutée nullable, remplie puis rendue obligatoire), garder la forme qu'EF Core a générée pour que l'instantané du modèle reste juste, un `Down` qui reconvertit (une perte voulue se demande à l'utilisateur et se dit en commentaire), un délai de commande allongé pour ce seul passage par `dotnet ef database update --connection "<chaîne>;Command Timeout=600"`, jamais dans la configuration ni le code de l'application, et l'essai dans les deux sens sur une copie des données, avec la forme d'un test d'intégration de la conversion (`IMigrator.MigrateAsync` vers la migration précédente dans une base à lui). « Mapping private fields » : un exemple `Trip` (`Route` est refusé par `hardslice-aggregate`) avec son objet valeur `TripStop`, deux tableaux et un instant dans des champs privés, la vue `Stops` en lecture seule, `PrimitiveCollection<string[]>("_stopIds").HasColumnName("StopIds")`, `Property<DateTimeOffset?>("_plannedAt")` et `Ignore` des vues publiques, puis les règles : nom du champ, colonne nommée comme le domaine, vue jamais faite du tableau lui-même (HS007). Les étapes 6 et 7 et la description de la skill y renvoient, « Never » a deux lignes de plus. `AGENTS.md` y renvoie dans la phrase existante de l'étape 2 de « Changing the database », dans les deux variantes : aucune ligne de plus (142 et 149). `CHANGELOG.md` a sa ligne. Test qui échouait avant : `Agent_guidance_says_how_to_change_the_shape_of_stored_rows_and_to_map_private_fields`, dans les deux variantes. La copie de `.agents/skills/` reste vérifiée par le test du paquet. Dans `scripts/test-template.sh`, la sonde `probe_private_fields`, dans la variante avec l'exemple, applique les trois blocs C# de la section tels que la skill de la solution générée les écrit à un agrégat `Trip` généré par `hardslice-aggregate`, vérifie que la migration stocke chaque champ dans sa colonne (`text[]`, `integer[]`, `timestamp with time zone`) sans vue publique, puis passe `check.sh` avec un test d'intégration qui relit les arrêts d'un trajet enregistré. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1234 tests verts), et lancées seules sur des solutions générées depuis les paquets locaux, `probe_private_fields`, `probe_aggregate_template` et `probe_entity_template` dans la variante avec l'exemple, et les vérifications du kit agents (`check_agent_instructions`, aucune mention de l'exemple) dans les deux variantes. Les paquets ne sont pas reconstruits : PB-080 les revalide. Contournement que Ghostline peut retirer : aucun dans le code. La migration `CompactStopTimes` et le mapping de `TripStopTimes`, écrits à la main, ont déjà la forme que la skill décrit, et le délai de `Command Timeout=600` passe déjà par `--connection` (feuille de route), la forme que la skill retient.

**Résultat observable**

`add-aggregate` dit comment changer la forme d'une table qui a des lignes (migration réécrite en conversion, dans les deux sens, délai de commande, essai sur une copie des données) et comment mapper un champ privé. `AGENTS.md` y renvoie depuis « Changing the database ».

**Constat**

Ghostline, entrée « no template nor skill to change the shape of a child entity » (04/11/2026, feuille de route 2.3) : `StopTime` (une ligne par passage) est devenu `TripStopTimes` (une ligne par course, passages en tableaux). `dotnet ef migrations add` a produit des suppressions et des ajouts de colonnes aux défauts vides, qui perdent toutes les lignes et ne pouvaient pas ajouter la nouvelle clé, avec pour seul avertissement « may result in the loss of data ». `add-aggregate` dit seulement qu'une migration « must create the table of the aggregate and nothing else ». La migration a été réécrite (nouvelle table remplie par `INSERT ... SELECT`, `Down` qui reconvertit), et le `Down` de 3,8 millions de lignes a dépassé le délai de commande par défaut de 30 secondes (`Command Timeout=600` dans la chaîne de connexion). Le mapping de quatre tableaux sur des champs privés (`PrimitiveCollection<int[]>("_stopSequences").HasColumnName(...)`), forme naturelle pour tenir HS007 quand la forme stockée diffère de la forme publique, n'est documenté nulle part.

**Périmètre**

- `add-aggregate`, section « Changing the shape of stored rows » : lire la migration générée avant de l'appliquer, réécrire une perte de données en conversion (`migrationBuilder.Sql`, nouvelle table remplie depuis l'ancienne), un `Down` qui reconvertit, le délai de commande d'une longue conversion, l'essai dans les deux sens sur une copie des données.
- `add-aggregate`, section « Mapping private fields » : `Property` et `PrimitiveCollection` sur un champ privé, avec la vue publique en lecture seule qu'attend HS007.
- `AGENTS.md` : un renvoi dans l'étape 2 de « Changing the database », sous 150 lignes dans les deux variantes. Pas de skill `reshape-entity` ni d'option `--reshape` (arbitrage 6).
- `CHANGELOG.md`.

**Cas vérifiés**

- `TemplatePackageTests` : les deux sections dans `add-aggregate` et le renvoi dans `AGENTS.md`, dans les deux variantes, copie `.agents/` comprise.
- L'exemple de mapping de champs privés compile et passe la vérification sur une solution générée.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh
```

### PB-086 : Mettre à niveau une solution générée avec `dotnet hardslice upgrade`

Statut : terminé le 04/11/2026, arbitrage 7 validé le même jour avec l'option B : aucune fusion à trois voies, différée en section 9. Ferme l'entrée « no path to bring a generated solution to a newer template » de `ghostline/docs/hardslice-friction.md`, et en partie l'entrée « AGENTS.md mixes the text of the template and the text of the application » : le diff d'`AGENTS.md` est listé, les marqueurs ou la fusion restent différés. `dotnet hardslice upgrade` (`UpgradeCommand`) refuse une solution hors d'un dépôt git, et un arbre qui a des changements autres que `.config/dotnet-tools.json`, que la mise à jour de l'outil change d'abord (code 1, rien d'écrit, chemins relatifs à la racine de la solution, même dans un dossier du dépôt). Il déduit la variante de `src/<App>/Features/Bookings/` ou `src/<App>.Domain/Bookings/`, sauf `--sample` ou `--no-sample`, installe `HardSlice.Templates@<version de l'outil>` dans une ruche à lui depuis la racine de la solution (les `nuget.config` de la solution choisissent la source, vérifié sur la source locale de Ghostline), puis génère `hardslice-api` sous le nom de la solution dans un dossier temporaire supprimé à la fin. Il réécrit les fichiers que le template possède (`ProtectedFiles` et `docs/rules/`), ajoute les fichiers absents, et liste sans les toucher les fichiers de l'application qui diffèrent, chacun avec son diff unifié (`UnifiedDiff`, trois lignes de contexte, `-` côté solution, `+` côté template). Deux listes de plus, trouvées en essayant la commande sur un clone de Ghostline et sur le template de PB-074 : un fichier de la solution qui porte le nom d'un fichier ajouté dans un autre dossier (« may have moved », le déplacement de `Shared/Behaviors` vers `Shared/Http` de PB-079, dont l'ancienne copie casserait le build), et un fichier HardSlice d'un dossier du template que la génération n'a plus. Un fichier qui ne diffère que par ses fins de ligne, ou un projet qui ne diffère que par son `UserSecretsId`, tiré au sort par `dotnet new` à chaque génération, n'est pas listé. Codes de sortie : 0 rien à fusionner, 1 échec, 2 ligne de commande, 3 fichiers listés. Le `Fix:` des erreurs de PB-076 nomme la commande, `getting-started.md` a une section « Upgrading a generated solution », `AGENTS.md` (même nombre de lignes) dit qu'un agent ne la lance jamais, `agent-loop.md`, `README.md`, README de l'outil et `CHANGELOG.md` suivent. Tests : `UpgradeCommandTests` (dépôt git réel, génération simulée, les cas vérifiés ci-dessous, sous-dossier d'un dépôt, déplacement, `UserSecretsId`, classification sur le contenu réel du template, commandes de `dotnet new`), `UnifiedDiffTests`, `CliTests`, `CheckCommandTests` mis à jour. Dans `scripts/test-template.sh`, `probe_upgrade`, dans les deux variantes (`Legacy`, `LegacySample`), packe le template du commit `a1d50f4` (PB-074) sous la même version, génère la solution, lui ajoute un module et une slice de commande par les templates d'éléments de ce build, complète `AGENTS.md`, change `scripts/check.sh` à la main, puis vérifie le refus d'un arbre modifié, que chaque fichier protégé et chaque fiche est celui du template courant (14 réécrits, 6 ajoutés, 5 déplacements listés), qu'`AGENTS.md` est laissé avec son diff, que la solution fusionnée comme le disent les listes passe le check complet, et qu'une seconde mise à niveau ne réécrit ni n'ajoute rien. Elle a besoin de l'historique : `fetch-depth: 0` pour le job template de `ci.yml` et `release.yml`. Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1340 tests verts), `probe_upgrade` dans les deux variantes et `probe_hardslice_files`, lancées seules sur des solutions générées depuis les paquets locaux. Essai sur un clone de Ghostline (paquets de PB-080) : code 3, 8 fichiers réécrits, 6 ajoutés, 4 déplacements de `Shared/Behaviors` vers `Shared/Http`, 6 fichiers à fusionner dont `AGENTS.md`, `Program.cs` et `AppFactory.cs`. Contournement que Ghostline peut retirer : la comparaison à la main avec une génération neuve (diff d'une quarantaine de fichiers, script qui remet les cinq parties de Ghostline dans `AGENTS.md`) : sa mise à niveau de PB-088 passe par `dotnet hardslice upgrade`, puis la fusion des fichiers listés.

**Résultat observable**

Dans une solution générée, après la mise à jour de l'outil local, `dotnet hardslice upgrade` réécrit les fichiers que le template possède depuis une génération à la version de l'outil, ajoute les fichiers nouveaux du template, et liste avec leur diff les fichiers que l'application possède et qui diffèrent de la nouvelle génération, à fusionner à la main ou par l'agent. `dotnet hardslice check` passe ensuite, ou ne s'arrête que sur ce qui reste à fusionner.

**Constat**

Ghostline, entrées « no path to bring a generated solution to a newer template » et « AGENTS.md mixes the text of the template and the text of the application » (04/11/2026) : une génération neuve diffère de Ghostline sur une quarantaine de fichiers, la plupart protégés (`.claude/`, `.agents/`, `hardslice.json`, `Directory.Build.targets`, tests d'architecture), mêlés à des fichiers que l'application a changés (`AGENTS.md`, `Program.cs`, `AppFactory.cs`, projets). Rien ne dit lequel prendre tel quel ni lequel fusionner, et l'agent ne peut pas toucher aux fichiers protégés. Ramener `AGENTS.md` au template a demandé un diff mot à mot du fichier entier, puis de remettre à la main ce que Ghostline avait écrit à cinq endroits. L'adoption (incrément 6) ne couvre que les solutions qui n'ont jamais utilisé HardSlice.

**Périmètre**

- `dotnet hardslice upgrade` lit le nom de la solution et sa variante (`--no-sample`, passée ou déduite), et génère le template à la version de l'outil dans un dossier temporaire et une ruche de templates isolée. Aucune génération de la version de départ n'est restaurée.
- Fichiers que le template possède (liste de l'outil, à partir de `ProtectedFiles` : fichiers protégés, fiches des règles, skills, hooks, scripts, tests d'architecture) : réécrits depuis la nouvelle génération. Fichier nouveau du template : ajouté. Fichiers que l'application possède et qui diffèrent de la nouvelle génération : laissés tels quels et listés avec leur diff, sans distinguer un changement de l'application d'un changement du template.
- Sortie : les fichiers réécrits, ajoutés et à fusionner, et des codes de sortie documentés comme ceux de `check` (PB-055), dont un pour « reste à fusionner ».
- Refus sur un arbre git qui a des changements, pour que le diff de la mise à niveau se relise seul. La commande écrit des fichiers protégés : elle est lancée par un mainteneur, et le job `protected-files` de la CI générée garde le libellé `hardslice-approved`.
- Le `Fix:` des erreurs de PB-076 et la section « When check stops on a HardSlice file » de `docs/getting-started.md` nomment la commande. README de `HardSlice.Tool`, `CHANGELOG.md`.
- Hors périmètre : la fusion à trois voies des fichiers que l'application a changés (section 9).

**Cas vérifiés**

- Solution générée à une version antérieure, avec un module, une slice et un `AGENTS.md` complété par l'application : fichiers du template réécrits, fichiers nouveaux ajoutés, `AGENTS.md` listé avec son diff et laissé tel quel.
- Fichier protégé changé à la main dans la solution : réécrit, et listé comme tel.
- Arbre git avec des changements : refus, rien d'écrit.
- Les deux variantes.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sonde de mise à niveau de scripts/test-template.sh, depuis une solution générée par une version antérieure du template
```

### PB-087 : Dire que le 409 d'un index unique ne couvre que les créations, et générer un jeton de concurrence à chaque agrégat

Statut : terminé le 04/11/2026, arbitrage 8 validé le même jour (option B). Reprend la tâche « Dire que le 409 d'un index unique ne couvre que les créations, et protéger une modification concurrente d'un agrégat » et l'entrée « the 409 on a unique violation does not cover a command that merges into existing aggregates » de `ghostline/docs/hardslice-friction.md`. `hardslice-aggregate` génère `builder.Property<uint>("Version").IsRowVersion();` dans `<Aggregate>Configuration.cs`, avec le commentaire de ce qu'il protège. Le domaine n'en sait rien. La migration de l'agrégat liste `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` dans son `CreateTable`, que Npgsql n'écrit pas : l'instantané du modèle enregistre le jeton, aucune colonne n'est créée. Entités enfants, vérifié sur une solution générée : sans rien de plus, une suppression concurrente du même enfant échoue déjà (le second `DELETE` ne touche aucune ligne), mais deux ajouts concurrents d'un enfant, ou deux modifications du même enfant, valident tous les deux sans erreur, et deux ajouts cassent même une règle de la racine (au plus un enfant). Choix : la racine toujours modifiée avec ses enfants, sans rien demander au domaine ni au handler. `AppDbContext` surcharge `SaveChanges` et `SaveChangesAsync` : avant d'enregistrer, chaque racine suivie, inchangée et munie d'un jeton de concurrence, dont un enfant tenu dans sa collection (clé étrangère avec navigation de la racine vers l'enfant) est ajouté, modifié ou supprimé, passe en `Modified`. EF Core met alors à jour sa ligne en vérifiant son `xmin`, et la seconde commande répond 409 par `TransactionBehavior`. Les enfants `--link id` et ceux écrits en masse, sans navigation de la racine, la laissent telle quelle. Les racines sans jeton, comme `ObservedTrip`, ne changent pas. Limite trouvée en route : EF Core n'envoie aucun `UPDATE` pour une ligne sans colonne à écrire, donc une racine dont tout l'état tient dans ses enfants ne serait pas vérifiée. `AppDbContext` refuse alors l'enregistrement par une `InvalidOperationException` qui donne la correction (« The children of the aggregate <Aggregate> changed, but its mapping has no column besides its key and its concurrency token […] Map a state of the root in its configuration, as the add-aggregate skill says. ») au lieu de perdre une écriture. `AGENTS.md` (puce du handler de commande, 142 et 149 lignes), `add-slice` (la phrase « the index and the 409 cover the others » remplacée par un point qui distingue les trois cas, avec le test d'une modification concurrente), `add-aggregate` (étapes 6 et 7, section « Concurrent changes », description et « Never »), `docs/item-templates.md`, README du paquet de templates et `CHANGELOG.md` suivent. Tests : dans `TemplatePackageTests`, `Aggregates_get_a_concurrency_token_and_the_agent_kit_tells_the_three_concurrent_cases_apart` (copie `.agents/` comprise par le test du paquet). Dans `scripts/test-template.sh`, `probe_aggregate_template` vérifie la ligne du jeton, son absence du domaine, la migration et l'instantané, et la nouvelle `probe_aggregate_concurrency`, dans les deux variantes (`Bookings`, `Rooms`), génère `Inventory` et son enfant `InventoryItem`, puis `Shelf` et `ShelfSlot` sans état, et lance un test d'intégration de la solution générée : deux commandes concurrentes qui changent la racine, ajoutent un enfant ou changent seulement un enfant, une seule valide et l'autre reçoit une `ConflictException` (409), `Shelf` est refusée avec sa correction, et aucune colonne `xmin` ni `Version` n'existe. La création concurrente sous une clé unique reste couverte par `TransactionBehaviorTests` (PB-069 inchangé). Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1311 tests verts), et, lancées seules sur des solutions générées depuis les paquets locaux, `probe_aggregate_template`, `probe_entity_template` et `probe_aggregate_concurrency` avec l'exemple et sans. Sur la machine chargée, les tests unitaires de `RecurringCommandTests` (le test instable déjà relevé par PB-084, et `Logs_a_failed_cycle_and_runs_the_next_one_without_stopping_the_host`) ont dépassé leur délai de 10 s pendant le check : ils ont été ignorés dans les solutions de travail seulement, le modèle n'est pas touché. Ce ticket à part reste à ouvrir. Contournement que Ghostline peut retirer : aucun, son verrou reste la forme d'une fusion qui ne doit jamais échouer. Un jeton sur `ObservedTrip` lui reste possible, au choix de l'application, et couvrirait alors aussi ses enfants tenus dans sa collection.

**Résultat observable**

`dotnet new hardslice-aggregate` génère un mapping avec un jeton de concurrence sur `xmin`, comme celui de `Booking`, que la migration de l'agrégat enregistre. Quand deux commandes modifient en même temps le même agrégat généré, la seconde répond 409 au lieu d'écraser la première. `AGENTS.md`, `add-slice` et `add-aggregate` distinguent trois cas : une création sous une clé unique (l'index et son 409), une modification concurrente (le jeton et son 409, l'appel ou le cycle suivant reprend), et une fusion qui ne doit jamais échouer (le verrou de transaction).

**Constat**

Ghostline, entrée du 04/11/2026 : `CollectRealtimeFeeds` trouve un `ObservedTrip` déjà stocké et l'enrichit (`ObservedTrip.See`). Deux collectes concurrentes, depuis deux instances de l'application, ajoutent chacune la même observation à leur copie, et le dernier commit écrase les compteurs de l'autre sans erreur : `ObservedTrip` n'a pas de jeton de concurrence, et l'index de `TripObservations` n'est pas unique. `add-slice` dit pourtant, après le verrou d'un handler qui fusionne, que « the index and the 409 cover the others ». Seul `Booking` a un jeton (`Property<uint>("Version").IsRowVersion()`), et `TransactionBehavior` traduit déjà `DbUpdateConcurrencyException` en 409. `hardslice-aggregate` n'en génère pas et `add-aggregate` n'en parle pas. Ghostline garde son verrou (décision du mainteneur du 04/11/2026).

**Périmètre**

- `hardslice-aggregate` : la propriété fantôme `Version` sur `xmin` (`IsRowVersion()`) dans `<Aggregate>Configuration.cs`, avec un commentaire qui dit ce qu'elle protège, comme celui de `BookingConfiguration`. La migration que l'agent ajoute l'enregistre dans l'instantané du modèle, sans SQL (Npgsql n'en écrit pas pour `xmin`, comme `AddBookingConcurrencyToken`). Le domaine n'en sait rien.
- Entités enfants (`hardslice-entity`) : une commande qui ne change qu'un enfant ne met pas à jour la ligne de la racine, dont EF Core ne vérifie alors pas le `xmin`. Le ticket le vérifie, puis choisit et documente dans `add-aggregate` comment couvrir ce cas (jeton sur l'enfant, ou racine toujours modifiée avec ses enfants), avec le test qui le prouve.
- `AGENTS.md` (puce du handler de commande, sous 150 lignes dans les deux variantes), `add-slice` (la phrase « the index and the 409 cover the others » remplacée par les trois cas), `add-aggregate` (le jeton généré, ce qu'il couvre, le 409 qu'il produit).
- `docs/item-templates.md`, README du paquet de templates si la liste de ce que génère `hardslice-aggregate` y figure, `CHANGELOG.md`.

**Cas vérifiés**

- Agrégat généré, dans les deux variantes : deux modifications concurrentes du même agrégat, la seconde répond 409 (sonde de `scripts/test-template.sh` avec un test d'intégration de la solution générée).
- Migration de l'agrégat généré : le jeton est dans l'instantané du modèle, aucune colonne créée, `MigrationsTests` vert.
- Création concurrente sous une clé unique : toujours 409 par l'index (PB-069 inchangé).
- Modification concurrente d'un enfant seul : le comportement choisi est prouvé par un test.
- `TemplatePackageTests` : les trois cas dans `AGENTS.md` et `add-slice`, le jeton dans `add-aggregate`, copie `.agents/` comprise.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
sondes des templates d'éléments de scripts/test-template.sh, dans les deux variantes
```

Contournement que Ghostline peut retirer : aucun, son verrou reste la forme d'une fusion qui ne doit jamais échouer. Un jeton sur `ObservedTrip` lui reste possible, au choix de l'application.

### PB-088 : Revalider les paquets après PB-081 à PB-087, pour la mise à niveau de Ghostline

Statut : terminé le 04/11/2026. Reconstruction sous la même version, `0.3.0-preview.1` : rien n'est publié, et le numéro ne compte pas tant que rien ne l'est (décision du mainteneur du 04/11/2026). `CHANGELOG.md` dit déjà ce que livrent PB-081 à PB-087 : chaque ticket y avait écrit sa ligne, rien n'y manque. Le README racine et celui de l'outil suivaient déjà PB-081 à PB-087, et celui du paquet de templates nommait `--require`, `--daily-at`, `--retry` et le jeton sur `xmin`, mais ne disait pas comment une solution passe à une version suivante : une phrase dit maintenant qu'un mainteneur lance `dotnet hardslice upgrade` une fois l'outil mis à jour, qui réécrit les fichiers du template, ajoute ceux qui manquent et liste avec leur diff ceux de l'application à fusionner.

Défaut trouvé par le passage complet de `scripts/test-template.sh` : `RecurringCommandTests.Logs_a_failed_cycle_and_runs_the_next_one_without_stopping_the_host`, un test unitaire que porte chaque solution générée, a fait échouer `check.ps1` dans la sonde de HS017 (« Tick failed: the next one is sent in 00:00:00 » au lieu de `00:00:30`). Le test avançait l'horloge dès que le handler du premier cycle avait tourné, avant que le service journalise le délai jusqu'au cycle suivant. C'est la cause du test instable relevé par PB-084 et PB-087 : cinq tests de la classe avançaient l'horloge sans attendre que le service ait armé sa minuterie, et une avance tombée entre la lecture de l'heure et la création de la minuterie repoussait un cycle d'un intervalle. Ils attendent maintenant l'instant attendu par le service, par le moniteur `Waits` des autres tests de la classe, qui sert aussi `Ping` : le service arme sa minuterie avant de prévenir le moniteur, donc une horloge déplacée ensuite réveille toujours le cycle et ne change aucun journal. Le service ne change pas, et `RecurringCommandCycles`, qui attend déjà le moniteur, non plus. Sous charge (quatre cœurs occupés, six boucles en parallèle), la classe a échoué 3 fois sur 150 passages avant la correction, toujours sur ce test, et 0 fois sur 150 après. Le ticket à part que PB-084 et PB-087 laissaient à ouvrir n'a plus d'objet.

Défaut trouvé à l'essai sur Ghostline : la dernière ligne de `dotnet hardslice upgrade` disait « Upgrade done, 15 files left to merge or to review: 13 HardSlice files rewritten, 6 files added. », un détail après les deux-points qui ne compte pas les 15 fichiers. Elle dit maintenant « Upgrade done: 13 HardSlice files rewritten, 6 files added, 15 files left to merge or to review. », comme la ligne sans rien à fusionner. Tests : la ligne exacte attendue par `UpgradeCommandTests` et ses deux autres cas, et la seconde mise à niveau de `probe_upgrade`.

Passages locaux : `dotnet build --no-restore`, `dotnet test --no-build` (1340 tests verts), paquets reconstruits dans `artifacts/packages` sur ce commit. Le passage local complet de `scripts/test-template.sh`, arrêté à la demande du mainteneur après la variante avec l'exemple (144 vérifications vertes, environ 1 h pour le tout sur ce serveur), est remplacé par la CI de la branche `ci/pb-088`, qui vérifie le scénario complet, chaque groupe vert, avant que `main` soit poussée. `scripts/verify-published.sh 0.3.0-preview.1 --source artifacts/packages` tourne sur les paquets reconstruits avant ce push.

Essai de `dotnet hardslice upgrade` sur un clone jetable de Ghostline (commit `0bb5ad5`), dans un dossier du bloc-notes de la session, avec le home de `isolate_dotnet_cli_home` et un `NUGET_PACKAGES` à lui, le `nuget.config` de Ghostline servant les paquets de `artifacts/packages`. `dotnet tool restore` puis `dotnet hardslice upgrade` : code 3, solution sans l'exemple.

- Réécrits (13) : `hardslice.json` (dossier `Http` de `Shared`), `tests/Ghostline.ArchitectureTests/HardSliceConfiguration.cs`, les fiches `HS001`, `HS004`, `HS006`, `HS010`, `HS015`, `HS017` et `HS902`, et les skills `add-aggregate` et `add-slice` de `.claude/skills/` et `.agents/skills/`.
- Ajoutés (6) : `BadRequestExceptionHandler.cs`, `DomainExceptionHandler.cs`, `ExplicitOffsetJsonConverter.cs` et `ValidationExceptionHandler.cs` dans `src/Ghostline/Shared/Http/`, `ExceptionHandlersTests.cs` et `ExplicitOffsetJsonConverterTests.cs` dans `tests/Ghostline.Tests/Shared/Http/`.
- Peut-être déplacés (5) : les quatre premiers et `ExplicitOffsetJsonConverterTests.cs` dans les dossiers `Shared/Behaviors`. Ils ne diffèrent des fichiers ajoutés que par leur espace de noms : ils se suppriment.
- À fusionner (10) : `AGENTS.md`, `src/Ghostline/Ghostline.csproj` (commentaire du `UserSecretsId` seulement), `Program.cs`, `appsettings.Development.json` (niveaux de journal de Ghostline, à garder), `Shared/Messaging/RecurringCommand.cs`, `Shared/Persistence/AppDbContext.cs`, `AppFactory.cs`, `ProgramTests.cs`, `RecurringCommandCycles.cs` et `tests/Ghostline.Tests/Shared/Messaging/RecurringCommandTests.cs`. Les copies de Ghostline de `RecurringCommand.cs`, `AppDbContext.cs`, `ProgramTests.cs` et `RecurringCommandTests.cs` sont celles du template de PB-080, sans changement de l'application : elles se reprennent du template telles quelles. Le paramètre `configure` que Ghostline avait ajouté à `RecurringCommandCycles.cs` existe dans le template (PB-082) : il se reprend tel quel aussi.

Fusion d'essai sur le clone, pour vérifier que la liste suffit : les cinq fichiers déplacés supprimés, les cinq fichiers ci-dessus repris du template, `using Ghostline.Shared.Http;` ajouté à `Program.cs`, `AppFactory.cs` repris du template avec la clé de test (`AdminApiKey`, son `UseSetting`) et l'entrée `[AdminKey.Policy]` d'`AuthorizedClients`, et les appels `CreateAdminClient()` des tests remplacés par `CreateAuthorizedClient(AdminKey.Policy)`. `dotnet hardslice check` passe ensuite (166 tests unitaires, 91 d'architecture, 44 d'intégration), sans diagnostic HardSlice, HS006 sur `ScheduleImporter` compris. `AGENTS.md` n'a pas été fusionné dans l'essai : le check ne le lit pas.

Procédure de mise à niveau de Ghostline sur ce commit, par son mainteneur, sur un arbre git propre :

1. Reconstruire les paquets depuis ce commit de HardSlice (`dotnet pack` des trois projets vers `artifacts/packages`, comme le dit `CONTRIBUTING.md`), puis purger les deux caches qui serviraient l'ancienne construction sous la même version : `dotnet nuget locals global-packages --clear` (ou les dossiers `hardslice.analyzers`, `hardslice.tool` et `hardslice.templates` de `~/.nuget/packages/`) et le fichier `hardslice.tool` de `~/.dotnet/toolResolverCache/1/`.
2. Réinstaller le template : `dotnet new install artifacts/packages/HardSlice.Templates.0.3.0-preview.1.nupkg --force`.
3. Dans Ghostline : `dotnet tool restore`, puis `dotnet hardslice upgrade`. Le manifeste ne change pas : la version reste `0.3.0-preview.1`.
4. Fusionner ce que la commande liste, comme dans l'essai ci-dessus : supprimer les cinq fichiers de `Shared/Behaviors`, reprendre du template les cinq fichiers que Ghostline n'a pas changés, ajouter `using Ghostline.Shared.Http;` à `Program.cs`, reporter dans le nouvel `AppFactory.cs` la clé de test et l'entrée `[AdminKey.Policy]` d'`AuthorizedClients` en retirant `CreateAdminClient()` et `AdminClient` (contournement de PB-081), garder les niveaux de journal d'`appsettings.Development.json` et le commentaire du `UserSecretsId`, et fusionner dans `AGENTS.md` les lignes du template (carte de `Shared/Messaging`, `Shared/Behaviors`, `Shared/Http` et des tests d'intégration) en gardant les modules de Ghostline. Déplacer aussi `AdminKey.cs` et `AdminKeyTests.cs` de `Shared/Behaviors` vers `Shared/Http` (PB-079), avec leur espace de noms.
5. `dotnet hardslice check`, puis retirer les contournements que PB-081 à PB-084 rendent inutiles, comme le disent leurs statuts (`ScheduleImporter`, `NightlyImport` et `WaitingClock` remplacés par `RecurringSchedule.DailyAt(...).WithRetries(...).AlsoAtStart()` et `ImportScheduleCondition`, `CollectRealtimeFeedsCondition` à la place de l'échec de chaque cycle sans clé), et mettre à jour les statuts de `docs/hardslice-friction.md`.

La mise à niveau réécrit des fichiers protégés : sa demande de fusion porte le label `hardslice-approved`.

**Résultat observable**

Les paquets reconstruits dans `artifacts/packages` passent le parcours de PB-080, et `dotnet hardslice upgrade` (PB-086) met à niveau une copie de Ghostline vers eux. Ghostline peut se mettre à niveau sur ce commit.

**Périmètre**

- `CHANGELOG.md` : l'entrée `0.3.0-preview.1` dit ce que PB-081 à PB-087 ajoutent. Chaque ticket y écrit sa ligne : ce ticket vérifie que l'entrée dit ce que livrent les paquets.
- `README.md` et README des paquets : ce que PB-081 à PB-087 changent de ce qu'ils présentent.
- Passage complet de `scripts/test-template.sh` et de `scripts/verify-published.sh --source artifacts/packages` sur les paquets reconstruits.
- Essai de `dotnet hardslice upgrade` sur un clone jetable de Ghostline, dans un dossier temporaire et un home isolé (`scripts/dotnet-cli-home.sh`), jamais sur le dépôt de Ghostline lui-même : les fichiers réécrits, ajoutés et à fusionner sont notés dans le statut, pour préparer la mise à niveau de Ghostline. Un défaut de la commande trouvé ici se corrige dans ce ticket, avec son test.
- Statut : la procédure de mise à niveau de Ghostline sur ce commit (purge des deux caches que dit `CONTRIBUTING.md`, réinstallation du template, `dotnet tool restore`, `dotnet hardslice upgrade`, fusion des fichiers listés).

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh
for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
scripts/verify-published.sh 0.3.0-preview.1 "$(mktemp -d)" --source artifacts/packages
dotnet hardslice upgrade, sur un clone jetable de Ghostline
```

### PB-089 : Vérifier un changement par groupes de `scripts/test-template.sh`, et lancer les groupes en parallèle dans la CI

Statut : terminé le 04/11/2026. Le scénario complet, lancé sans argument, est inchangé : mêmes sondes, même ordre. Vérifié sur la branche `ci/pb-089` : chacun des neuf groupes passe seul, sur un runner neuf, et la CI dure 10 min au lieu de 23 (le plus long, `sample-items`, 9 min 45 s, puis `sample-rules`, 6 min). Un premier passage a échoué sur quatre groupes, tous sur `RecurringCommandTests` (le test instable de PB-087 et PB-088), et vert avec sa correction de PB-088.

**Constat**

PB-088, six fichiers modifiés, a pris près de deux heures, dont un quart de travail. Le scénario complet de `scripts/test-template.sh` prend 23 min sur un runner GitHub et environ 1 h sur la machine de développement, 2,7 fois plus lente (4 cœurs partagés avec d'autres agents). Ce sont environ 110 passages du check, en séquence, dans un seul fichier de 5 800 lignes : rien ne permet d'en lancer une partie, sauf extraire les fonctions avec awk. Le passage a été relancé trois fois depuis le début : coupé par un redémarrage de l'agent, puis arrêté sur un test instable, puis relancé après sa correction.

**Résultat observable**

`scripts/test-template.sh --list` liste les groupes du scénario. `--only <groupe>[,<groupe>...]` lance ceux de la zone d'un changement, chacun sur la solution qu'il demande, ou reprend après un échec sans relancer ce qui est passé. La CI lance un job par groupe, en parallèle, sur `main`, sur toute branche `ci/<nom>` et sur les pull requests.

**Périmètre**

- Neuf groupes : `sample`, `sample-rules`, `sample-hooks`, `sample-items`, `blank`, `blank-rules`, `blank-items`, `names`, `upgrade`. Chaque bloc du script devient une fonction, appelée dans l'ordre d'aujourd'hui. Un groupe lancé sans le premier groupe de sa solution construit d'abord cette solution (`check.sh --build`). Codes de sortie : 0, 1, et 2 pour un argument inconnu.
- `ci.yml` : la liste des groupes est lue dans `--list`, un groupe ajouté a donc son job sans changer le workflow. `release.yml` lance toujours le scénario complet, en un seul job.
- `CONTRIBUTING.md` : la table des groupes à lancer selon la zone du changement. `AGENTS.md` : les groupes de sa zone en local, jamais le scénario complet, et la branche `ci/<ticket>` avant `main`.
- Hors périmètre, pour un ticket suivant : semer les violations indépendantes d'un groupe de règles dans un seul build, et ne garder `check.ps1` que sur les sondes propres au lanceur (modes, codes de sortie, prérequis).

**Acceptation**

```text
scripts/test-template.sh --list
scripts/test-template.sh --only <groupe>, pour chaque groupe, sur un checkout neuf : les jobs template de la CI
scripts/test-template.sh, sans argument : le job template de release.yml
```

### Arbitrages validés le 05/11/2026

Pour PB-090 à PB-097, tirés des entrées du 04/11/2026 que `ghostline/docs/hardslice-friction.md` laisse ouvertes après la mise à niveau de Ghostline sur les paquets de PB-088 (d78ba83). Validés par le mainteneur le 05/11/2026 : les recommandations ci-dessous, sans écart. PB-051 restant suspendu, aucun placement avant ou après la publication n'est à décider : les tickets passent l'un après l'autre, et PB-097 reconstruit les paquets une seule fois, après eux.

9. **Ce que l'application a changé, pour `dotnet hardslice upgrade` (PB-095).** Entrées « AGENTS.md mixes the text of the template and the text of the application » et « first run of dotnet hardslice upgrade ». La première vraie mise à niveau, celle que la section 9 attendait pour arbitrer la fusion à trois voies, a pris environ 15 min, dont un tiers sur `AGENTS.md` : environ 45 des 119 lignes de son diff étaient les parties de Ghostline, montrées comme lignes à retirer. 5 des 10 fichiers de l'application listés n'avaient aucun changement de l'application, ce que seul `git log` a montré. Une base restaurée depuis NuGet.org, prévue par l'arbitrage 7, est impossible : rien n'est publié, et toutes les reconstructions portent `0.3.0-preview.1`, donc le numéro ne désigne pas une génération.
   - Option A : la base dans la solution. La génération (`dotnet new hardslice-api`) et chaque mise à niveau écrivent dans `.hardslice/generation/`, protégé et versionné, les fichiers de l'application tels que le template les a générés. `upgrade` fusionne alors à trois voies, avec `git merge-file`.
   - Option B : un manifeste des empreintes des fichiers générés. Les fichiers jamais changés sont mis à jour d'office, mais `AGENTS.md`, `Program.cs` et `AppFactory.cs` restent en diff à deux voies.
   - Option C : des marqueurs des parties de l'application dans `AGENTS.md`. Ne règle qu'`AGENTS.md`.
   - Option D : rien de plus, la fusion reste différée.
   - Décision : A. Elle règle `AGENTS.md` sans marqueurs, met à jour d'office ce que l'application n'a pas changé, et marche sous une version reconstruite. L'arbitrage 7 écartait un fichier de plus dans la solution parce que la publication l'aurait figé : rien n'étant publié, la raison ne tient plus. Une solution sans base, comme Ghostline aujourd'hui, garde le comportement de PB-086 à sa première mise à niveau, qui enregistre la base pour la suivante.
10. **Sortie de `dotnet hardslice upgrade` (PB-096).** Même entrée « first run of dotnet hardslice upgrade » : les en-têtes `--- <path> (solution)` et `+++ <path> (template <version>)` ne sont pas ceux de `git apply`, et un fichier déplacé est listé sans son diff contre son nouveau chemin.
    - Décision : les en-têtes de git (`diff --git a/<path> b/<path>`, `--- a/<path>`, `+++ b/<path>`), et le diff d'un fichier déplacé contre son nouveau chemin, avec une ligne qui dit quand il ne diffère que par son namespace. Après PB-095, qui réduit ce qui reste listé.
11. **Condition d'une commande planifiée (PB-094).** Entrée « no template for the condition of a recurring command, nor for a schedule on an existing command ». Les deux conditions de Ghostline (`ImportScheduleCondition`, `CollectRealtimeFeedsCondition`) ont été ajoutées à des commandes qui existaient déjà.
    - Option A : un template d'élément `hardslice-condition`, pour une commande nouvelle ou existante, plus `hardslice-command --at-start` et un paramètre `start` de `RecurringCommandCycles`. L'horaire d'une commande existante reste une ligne et un test décrits dans `add-slice`.
    - Option B : une option `--condition` de `hardslice-command`, qui ne sert pas pour une commande existante.
    - Option C : un exemple complet dans `add-slice`, rien de généré.
    - Décision : A.
12. **Test du jeton de concurrence (PB-093).** Entrée « a concurrency token added to an existing aggregate gets no test ».
    - Option A : `hardslice-aggregate` génère un test qui vérifie que le modèle mappe `Version` sur `xmin` comme jeton. Le test de comportement (deux contextes, 409) s'écrit avec la première commande qui change l'agrégat, comme `add-slice` le dit depuis PB-087.
    - Option B : un test de comportement généré, qui devrait construire et changer un agrégat dont le domaine n'existe pas encore (marqueurs à remplir à chaque génération).
    - Option C : un test d'architecture qui exige un jeton sur chaque racine, l'équivalent d'une règle que l'arbitrage 8 a écartée (option C).
    - Décision : A, avec une consigne de `add-aggregate` pour un agrégat qui reçoit son jeton plus tard.
13. **Trois corrections sans autre option.**
    - `--require` (PB-092), entrée « --require writes the name of the policy, never the constant of the application » : une phrase de `add-slice` fait remplacer le littéral par la constante de l'application quand elle en a une. Aucune option de plus : un renommage de la politique fait déjà échouer les tests générés.
    - HS006 et une lecture qui ne charge aucune entité (PB-090), entrée « HS006 lets a condition count tracked rows » : `AGENTS.md`, `add-slice` et le commentaire de `IRecurringCommandCondition` s'alignent sur la règle et sur `HS006.md`. Aucun diagnostic de plus.
    - `--help` et `-h` sur chaque commande de l'outil (PB-091), entrée « dotnet hardslice upgrade --help is refused ».

| Point | Ticket | Raison de l'ordre |
|---|---|---|
| HS006 et une lecture sans entité | PB-090 | texte seul, avant PB-094 qui touche la même consigne |
| `--help` et `-h` | PB-091 | outil seul |
| Constante d'une politique | PB-092 | consigne seule |
| Test du jeton de concurrence | PB-093 | template d'élément |
| Condition et cycle au démarrage | PB-094 | après PB-090 |
| Fusion à trois voies | PB-095 | après PB-090 à PB-094, pour que la base et sa sonde voient tout ce qu'ils changent du template |
| Sortie de `upgrade` | PB-096 | après PB-095 |
| Paquets revalidés | PB-097 | après PB-090 à PB-096, Ghostline se met à niveau sur ce commit |

Les entrées « decision » du 04/11/2026 n'appellent aucun ticket. L'entrée « AGENTS.md keeps "Never run dotnet hardslice upgrade" » est la consigne voulue. `AGENTS.override.md` périmé relève de l'agent Side Hub, qui le régénère.

### Critères communs à PB-090 à PB-097

- Les critères communs des sections 5 à 7 restent applicables, dont la sonde, l'entrée de `docs/item-templates.md` et la consigne de la skill de chaque template ou option ajouté.
- Chaque ticket cite l'entrée de `ghostline/docs/hardslice-friction.md` qu'il ferme, et dit dans son statut le contournement que Ghostline peut retirer.
- Chaque ticket ajoute sa ligne à `CHANGELOG.md`, sous `0.3.0-preview.1`.
- Vérification : `dotnet build --no-restore`, `dotnet test --no-build`, et en local les groupes de `scripts/test-template.sh` de sa zone (`--only`, table de `CONTRIBUTING.md`), jamais le scénario complet. Les commits passent par une branche `ci/<ticket>` dont la CI est verte, puis sur `main`.
- Aucun ticket ne reconstruit les paquets, ne pose d'étiquette ni ne publie : PB-097 reconstruit une fois après eux.

### PB-090 : Aligner le kit agents sur HS006 pour une lecture qui ne charge aucune entité

Statut : terminé le 05/11/2026. Ferme l'entrée « HS006 lets a condition count tracked rows » de `ghostline/docs/hardslice-friction.md` (arbitrage 13). Texte seul, HS006 ne change pas. `AGENTS.md` : la règle d'or 5 dit qu'un handler de requête met `AsNoTracking()` sur chaque requête qui charge une entité, et qu'une lecture qui n'en charge aucune (`AnyAsync`, `CountAsync`, projection en valeurs) n'en a pas besoin. La puce « Background work » dit la même chose d'une condition, qui lit ses options ou `AppDbContext` et n'écrit jamais. Les deux variantes restent sous 150 lignes, aucune ligne ajoutée. `add-slice` : la condition, le service hébergé et la requête disent la même chose, la requête générée garde son `AsNoTracking()`. Le commentaire de `IRecurringCommandCondition<TCommand>` (`Shared/Messaging/RecurringCommand.cs`) aussi. `HS006.md` inchangé : sa phrase nomme déjà la projection en valeurs (« projects to the values it needs, or asks `AnyAsync` or `CountAsync` »). Tests : dans `TemplatePackageTests`, `Agent_guidance_says_that_a_read_that_loads_no_entity_needs_no_AsNoTracking`, avec et sans l'exemple (copie `.agents/` comprise par le test du paquet), et les assertions de PB-083 et PB-084 suivent le nouveau texte. Passages : `dotnet build --no-restore`, `dotnet test --no-build` (1342 tests verts), `scripts/test-template.sh --only sample,blank` en local, CI verte sur `ci/pb-090`. Contournement que Ghostline peut retirer : aucun, `ImportScheduleCondition` peut garder son `AsNoTracking()`.

**Résultat observable**

`AGENTS.md`, `add-slice` et le commentaire de `IRecurringCommandCondition<TCommand>` disent la même chose que HS006 et `HS006.md` : une condition, un service hébergé ou un handler de requête met `AsNoTracking()` sur une requête qui charge une entité, et une lecture qui n'en charge aucune (`AnyAsync`, `CountAsync`, projection en valeurs) n'en a pas besoin.

**Constat**

Ghostline, entrée du 04/11/2026 : sans son `AsNoTracking()`, `ScheduleVersion.InForceOn(db.Set<ScheduleVersion>(), today).AnyAsync()` dans `ImportScheduleCondition` compile sans diagnostic, alors que `FirstOrDefaultAsync()` fait échouer le build. HS006 a raison, rien n'est suivi. Mais la puce « Background work » d'`AGENTS.md` dit qu'une condition « reads options or `AppDbContext` with `AsNoTracking()` », et `add-slice` comme le commentaire de `IRecurringCommandCondition` (`Shared/Messaging/RecurringCommand.cs`) le répètent, alors que `HS006.md` dit déjà qu'un service hébergé ou une condition « asks `AnyAsync` or `CountAsync` ».

**Périmètre**

- `src/HardSlice.Templates/content/hardslice-api/AGENTS.md` : la puce « Background work », et la règle d'or 5 si elle dit la même chose des handlers de requête, sans dépasser 150 lignes dans les deux variantes.
- La skill `add-slice` (copies `.claude/` et `.agents/`), le commentaire de `IRecurringCommandCondition<TCommand>`.
- `HS006.md` seulement si sa phrase sur `AnyAsync` et `CountAsync` doit nommer aussi la projection en valeurs. HS006 ne change pas.
- Hors périmètre : un diagnostic sur une lecture qui ne charge aucune entité.

**Cas vérifiés**

- `TemplatePackageTests` : la phrase alignée dans `AGENTS.md` et `add-slice`, copie `.agents/` comprise.
- Les deux variantes d'`AGENTS.md` sous 150 lignes.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only sample,blank
```

Contournement que Ghostline peut retirer : aucun, `ImportScheduleCondition` peut garder son `AsNoTracking()`.

### PB-091 : Afficher l'aide d'une commande de l'outil avec `--help` ou `-h`

Statut : terminé le 05/11/2026. Ferme l'entrée « dotnet hardslice upgrade --help is refused » de `ghostline/docs/hardslice-friction.md` (arbitrage 13). `Cli.cs` : `--help` ou `-h`, seul après `check`, `explain`, `protected`, `upgrade` ou `hook`, passe avant toute autre validation des arguments, n'exécute rien et sort avec le code 0. `Cli.HelpOf` tire l'aide de `Usage` : la ligne `Usage: dotnet hardslice <commande et ses arguments>`, les entrées de la commande sous `Commands` (`check` et `check --build` pour `check`), puis ses lignes `Exit codes of`. `Usage` gagne les codes de sortie d'`explain` (0, 2) et de `hook` (ceux de chaque CLI, 1 pour un CLI ou un événement inconnu, ou une charge qui n'est pas du JSON), et la ligne `--help, -h` de ses options. Un `--help` mêlé à d'autres arguments reste l'erreur d'aujourd'hui : code 2, et code 1 pour `hook`, que Claude Code et Codex liraient sinon comme un refus. README de `HardSlice.Tool` et `CHANGELOG.md`. Tests : dans `CliTests`, `Help_of_a_command_prints_its_paragraph_and_exit_codes_and_exits_0` (sortie exacte des cinq commandes, `--help` et `-h`, rien sur la sortie d'erreur, aucune commande lancée), `Help_of_upgrade_leaves_a_git_tree_with_changes_as_it_is` (dépôt git réel, arbre modifié, `git status` inchangé), `Short_help_prints_the_usage_as_help_does`, et les théories d'arguments refusés de `check`, `explain` et `upgrade` couvrent un `--help` mêlé à d'autres arguments. Passages : `dotnet build --no-restore`, `dotnet test --no-build` (1361 tests verts), CI verte sur `ci/pb-091`. Contournement que Ghostline peut retirer : aucun, `dotnet hardslice` sans argument suffisait.

**Résultat observable**

`dotnet hardslice <commande> --help` et `-h`, pour `check`, `explain`, `protected`, `upgrade` et `hook`, affichent le paragraphe de cette commande dans l'usage, avec ses options et ses codes de sortie, et sortent avec le code 0. `dotnet hardslice -h` vaut `--help`.

**Constat**

Ghostline, entrée du 04/11/2026 : `dotnet hardslice upgrade --help` répond « error: upgrade takes no argument, or --sample or --no-sample to choose the variant of the template. Unknown arguments: '--help'. » avec le code 2. `check` et `protected` refusent `--help` de la même façon (`src/HardSlice.Tool/Cli.cs`). C'est pourtant le premier réflexe avant une commande qui réécrit des fichiers protégés.

**Périmètre**

- `Cli.cs` : `--help` et `-h` reconnus seuls après le nom de chaque commande, avant toute autre validation de ses arguments. L'aide d'une commande est tirée de `Usage`, pour qu'un seul texte décrive chaque commande. Un `--help` mêlé à d'autres arguments reste une erreur de ligne de commande (code 2), comme aujourd'hui.
- README de `HardSlice.Tool`, `CHANGELOG.md`.

**Cas vérifiés**

- `CliTests` : `--help` et `-h` pour chaque commande, sortie exacte et code 0, rien d'écrit sur la sortie d'erreur. `upgrade --help` ne touche pas au dépôt git, même avec un arbre modifié.
- Les erreurs actuelles des arguments inconnus inchangées.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
```

Contournement que Ghostline peut retirer : aucun, `dotnet hardslice` sans argument suffisait.

### PB-092 : Dire dans `add-slice` de remplacer le nom littéral d'une politique par la constante de l'application

Statut : terminé le 05/11/2026. Ferme l'entrée « --require writes the name of the policy, never the constant of the application » de `ghostline/docs/hardslice-friction.md` (arbitrage 13). Consigne seule, le template ne change pas. `add-slice`, étape 3 de « Protected endpoints » : `--require` écrit le nom de la politique en littéral, dans l'endpoint et dans les tests, et une application qui nomme sa politique dans une constante, comme `AdminKey.Policy` dans `Shared/Http`, remplace chaque littéral par cette constante, `.RequireAuthorization(AdminKey.Policy)` et `app.CreateAuthorizedClient(AdminKey.Policy)`, avec le `using` de son espace de noms, pour que la politique garde un seul nom. Un littéral oublié compile toujours après un renommage, et échoue à la première requête de l'endpoint. La description de `--require` dans les `template.json` de `hardslice-command` et `hardslice-query`, et `docs/item-templates.md`, restent tels quels : ils disent ce que le template écrit, sans laisser croire que le littéral doit rester. Tests : dans `TemplatePackageTests`, `Add_slice_says_how_to_protect_an_endpoint` vérifie la consigne (copie `.agents/` comprise par le test du paquet). Passages : `dotnet build --no-restore`, `dotnet test --no-build` (1361 tests verts), `scripts/test-template.sh --only sample-items` en local, CI verte sur `ci/pb-092`. Contournement que Ghostline peut retirer : aucun, `ImportSchedule` chaîne déjà `.RequireAuthorization(AdminKey.Policy)`.

**Résultat observable**

La section « Protected endpoints » de `add-slice` dit que `--require <policy>` écrit le nom de la politique en littéral, dans l'endpoint et dans les tests (`.RequireAuthorization("<policy>")`, `app.CreateAuthorizedClient("<policy>")`), et qu'une application qui nomme sa politique dans une constante, comme `AdminKey.Policy` dans `Shared/Http`, remplace les littéraux par cette constante.

**Constat**

Ghostline, entrée du 04/11/2026 : sa politique est nommée une fois, `AdminKey.Policy`, que son schéma enregistre. Chaque slice générée avec `--require Admin` répète le littéral, que l'agent remplace à la main. Un renommage de la politique compile toujours, mais fait échouer les tests générés (le 401, et le client absent de `AuthorizedClients`), donc le check l'attrape.

**Périmètre**

- `add-slice` (copies `.claude/` et `.agents/`), section « Protected endpoints ».
- La description de `--require` dans `template.json` de `hardslice-command` et `hardslice-query`, et `docs/item-templates.md`, d'une demi-phrase si leur texte laisse croire que le littéral doit rester.
- Hors périmètre : une option qui génère la constante.

**Cas vérifiés**

- `TemplatePackageTests` : la consigne dans `add-slice`, copie `.agents/` comprise.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only sample-items
```

Contournement que Ghostline peut retirer : aucun, `ImportSchedule` chaîne déjà `.RequireAuthorization(AdminKey.Policy)`.

### PB-093 : Générer le test du jeton de concurrence d'un agrégat

Statut : terminé le 05/11/2026. Ferme l'entrée « a concurrency token added to an existing aggregate gets no test » de `ghostline/docs/hardslice-friction.md` (arbitrage 12, option A). `hardslice-aggregate` génère `tests/<App>.IntegrationTests/Features/<Module>/<Aggregate>ConfigurationTests.cs`, sans marqueur, dont le test `Maps_the_Version_of_<Aggregate>_on_xmin_as_its_concurrency_token` lit `FindEntityType(typeof(<Aggregate>))` dans le modèle de `AppDbContext` et vérifie la propriété `Version` : fantôme, `uint`, `IsConcurrencyToken`, `ValueGenerated.OnAddOrUpdate`, colonne `xmin`. L'exemple a le même test dans `BookingConfigurationTests`. `add-aggregate` nomme le fichier généré (introduction et étape 6), et « Concurrent changes » dit quoi faire pour un agrégat mappé avant son jeton : la ligne du mapping, une migration à part `Add<Aggregate>ConcurrencyToken` avec le commentaire de `AddBookingConcurrencyToken` (aucun SQL, l'instantané seul), puis le même test, donné en bloc de code. Le test du 409 s'écrit avec la première commande qui change l'agrégat, comme `add-slice` le dit. La description du template, `docs/item-templates.md` et le README du paquet de templates nomment le test. Constat de la mise au point : sans migration, retirer le jeton fait échouer tous les tests d'intégration par la fixture (`PendingModelChangesWarning`). Avec la migration qui l'enregistre (Npgsql n'écrit aucun SQL pour le `DropColumn` de `xmin`), seul le test généré échoue, avec son message, et dans l'exemple le test du 409 de `CancelBooking`. Tests : `TemplatePackageTests` (`Aggregates_get_the_test_of_their_concurrency_token_and_the_agent_kit_says_how_a_mapped_aggregate_gets_it`, liste des fichiers du template, copie `.agents/` comprise par le test du paquet). `probe_aggregate_template`, dans les deux variantes, vérifie le test généré, vert après la migration, puis retire la ligne `IsRowVersion()`, ajoute la migration `Remove<Aggregate>Version` et attend l'échec de ce seul test. `aggregate_files` liste le test, `probe_aggregate_concurrency` et `probe_private_fields` ne suppriment plus à part le fichier qu'ils réécrivent, leurs cas inchangés et verts, et la liste des tests de l'exemple nomme celui de `Booking`. Passages : `dotnet build --no-restore`, `dotnet test --no-build` (1363 tests verts), `scripts/test-template.sh --only sample-items,blank-items` en local, CI verte sur `ci/pb-093`. Contournement que Ghostline peut retirer : aucun, ses deux tests écrits à la main (modèle de `ScheduleVersion`, modification concurrente de `ObservedTrip`) gardent leur forme. Un agrégat suivant reçoit le test généré.

**Résultat observable**

`dotnet new hardslice-aggregate` génère, à côté du mapping, un test d'intégration qui vérifie que le modèle de `AppDbContext` mappe la propriété fantôme `Version` de l'agrégat sur `xmin` comme jeton de concurrence. Si le mapping du jeton est retiré ou cassé, ce test échoue. `add-aggregate` dit quoi faire pour un agrégat mappé avant son jeton : la migration à part, avec le commentaire de celle de l'exemple (aucun SQL, l'instantané seul), et ce même test.

**Constat**

Ghostline, entrée du 04/11/2026 : `hardslice-aggregate` mappe le jeton depuis PB-087 mais ne génère aucun test, ni pour un nouvel agrégat ni pour un agrégat qui le reçoit plus tard. `dotnet ef migrations add` a généré la migration de `ScheduleVersion` et `ObservedTrip` sans le commentaire que porte `AddBookingConcurrencyToken`. Ghostline a écrit à la main un test du modèle pour `ScheduleVersion`, et un test de modification concurrente pour `ObservedTrip` (deux contextes, `DbUpdateConcurrencyException` à la seconde sauvegarde). Le jeton protège du pire cas retenu par l'arbitrage 8, une écriture perdue sans erreur.

**Périmètre**

- `hardslice-aggregate` : `tests/<App>.IntegrationTests/Features/<Module>/<Aggregate>ConfigurationTests.cs`, comme `BookingConfigurationTests` de l'exemple, avec un test qui lit `FindEntityType(typeof(<Aggregate>))` et vérifie la propriété `Version` : `IsConcurrencyToken`, nom de colonne `xmin`, générée à l'ajout et à la mise à jour. Sans marqueur `TODO(hardslice):` : le test passe tel quel.
- L'exemple : le même test pour `Booking`, s'il n'en a pas.
- `add-aggregate` : la consigne pour un agrégat qui reçoit son jeton plus tard (migration commentée, test du modèle), et le rappel que le test de comportement s'écrit avec la première commande qui change l'agrégat, comme `add-slice` le dit.
- `docs/item-templates.md`, `CHANGELOG.md`.
- Hors périmètre : un test de comportement généré, un test d'architecture sur toutes les racines.

**Cas vérifiés**

- `probe_aggregate_template`, dans les deux variantes : le test généré passe, et échoue quand la sonde retire la ligne `IsRowVersion()` du mapping.
- `probe_aggregate_concurrency` inchangée et verte.
- `TemplatePackageTests` : la consigne dans `add-aggregate`, copie `.agents/` comprise.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only sample-items,blank-items
```

Contournement que Ghostline peut retirer : aucun, ses deux tests écrits à la main gardent leur forme. Un agrégat suivant reçoit le test généré.

### PB-094 : Générer la condition d'une commande planifiée, et le cycle au démarrage

Statut : terminé le 05/11/2026. Ferme l'entrée « no template for the condition of a recurring command, nor for a schedule on an existing command » de `ghostline/docs/hardslice-friction.md` (arbitrage 11, option A). Nouveau template d'élément `hardslice-condition` (`--module` et `--command` obligatoires, `--response` : `id` par défaut pour un `Guid`, `none` pour un `bool`, ou le nom du DTO). Il génère `src/<App>/Features/<Module>/<Slice>/<Slice>Condition.cs`, `internal sealed`, qui implémente `IRecurringCommandCondition<<Slice>Command>`, dit de recevoir ce qu'elle lit par son constructeur et porte un marqueur `TODO(hardslice):` à la place de sa règle, et `tests/<App>.IntegrationTests/Features/<Module>/<Slice>/<Slice>ConditionTests.cs` : `Holds_a_cycle_back_with_its_reason` (`HeldCycleException` et sa raison, puis l'instant suivant de l'horaire, sans reprise) et `Lets_a_cycle_go_and_sends_the_command`, avec un marqueur à la place de l'état de chacun. Il affiche la ligne `.AddScoped<IRecurringCommandCondition<<Slice>Command>, <Slice>Condition>()` à ajouter après le handler. Il refuse avec le code 105 et un message qui dit quoi faire : application illisible, module invalide ou absent, commande qui n'est pas en PascalCase ou finit par `Command` ou `Condition`, dossier de slice sans fichier source, slice sans `<Slice>.cs` ni `<Slice>Command.cs`, condition qui existe, réponse invalide, et une solution dont `Directory.Build.targets` ne liste pas les fichiers des slices. `Directory.Build.targets` gagne pour cela `HardSliceSliceFiles`, les fichiers `.cs` de `Features`, lus par la liaison du template : une solution plus ancienne, comme Ghostline, le reçoit par `dotnet hardslice upgrade`. `hardslice-command --at-start`, avec `--every` ou `--daily-at`, chaîne `AlsoAtStart()` à l'horaire affiché (huit textes d'enregistrement de plus), et génère `Runs_a_cycle_at_start_before_the_first_instant_of_its_schedule`, qui lance le cycle au démarrage à `TestInstants.Start` : les tests des autres cycles passent d'abord ce cycle. Refusé sans horaire. `RecurringCommandCycles(app, configure, start)` : `start`, un `DateTimeOffset?`, fixe l'heure de `Clock` avant que l'application démarre, `TestInstants.Start` par défaut, et le commentaire de la classe le dit. `add-slice` nomme `hardslice-condition` (avec sa commande) et `--at-start`, et garde pour l'horaire d'une commande existante sa ligne d'enregistrement, avec `.AlsoAtStart()` et le test de ses cycles par `RecurringCommandCycles`, `start` compris. `AGENTS.md` nomme le template et `--at-start` dans la puce « Background work » et le rôle de `HardSliceSliceFiles`, sans ligne de plus. `docs/item-templates.md` (section « The condition of a scheduled command », `--at-start`), README racine et README du paquet de templates (sept templates d'élément), `docs/getting-started.md`, `docs/agent-loop.md`, `CONTRIBUTING.md` et `CHANGELOG.md`. Tests : dans `TemplatePackageTests`, `Condition_item_template_generates_the_condition_of_a_command_and_its_integration_tests_only`, `Condition_item_template_decides_on_each_cycle_and_tests_a_held_cycle_and_a_sent_one`, `Condition_item_template_refuses_with_an_error_that_creates_nothing`, la théorie `Condition_item_template_finds_the_slice_its_command_and_its_condition_in_the_files_of_the_slices`, `Command_item_template_sends_a_recurring_command_at_start_too_and_refuses_it_without_a_schedule`, `Recurring_command_cycles_start_at_a_chosen_instant_and_the_agent_kit_generates_conditions_and_cycles_at_start`, le paquet liste les fichiers du nouveau template et `Skill_generates_its_element_with_dotnet_new_commands_that_exist_in_the_package` couvre `add-slice` avec `hardslice-condition` (copie `.agents/` comprise par le test du paquet). Les tests qui fixaient les conditions des enregistrements de `hardslice-command` suivent `&& !AtStart`. `probe_condition_template`, dans `sample-items` et `blank-items` : refus de `--at-start` sans horaire, chaque refus de `hardslice-condition` en 105, une condition sur une commande générée avec `--every`, une autre sur une commande qui garde son endpoint, renvoie un DTO et à laquelle la sonde ajoute un horaire quotidien, une condition qui existe refusée, puis `--at-start` avec `--every` et avec `--daily-at --retry`. Le check liste les marqueurs et n'échoue que sur les tests des conditions et des cycles, puis passe après la complétion de référence, qui ajoute un cycle au démarrage la veille au soir à Toronto par `start`. Passages : `dotnet build --no-restore`, `dotnet test --no-build` (1377 tests verts), `scripts/test-template.sh --only sample-items,blank-items` en local, CI verte sur `ci/pb-094`. Contournement que Ghostline peut retirer : aucun à retirer, ses conditions écrites à la main ont la forme générée. Son test du cycle au démarrage peut passer `start` au lieu de `SetUtcNow`, une fois `RecurringCommandCycles` repris par sa mise à niveau.

**Résultat observable**

`dotnet new hardslice-condition --module <Module> --command <Slice>` ajoute à une slice de commande, nouvelle ou existante, `<Slice>Condition` dans le dossier de la slice, qui implémente `IRecurringCommandCondition<<Slice>Command>`, et un test d'intégration qui fait retenir un cycle par la condition (`HeldCycleException`, avec sa raison) et passer un cycle quand elle le laisse aller. Il affiche la ligne d'enregistrement à ajouter après le handler dans `Add<Module>Module`, comme `--every` affiche la sienne. `hardslice-command --at-start`, avec `--every` ou `--daily-at`, chaîne `AlsoAtStart()` à l'horaire et génère le test du cycle au démarrage. `RecurringCommandCycles` reçoit un paramètre `start`, l'instant de départ de son horloge, `TestInstants.Start` par défaut.

**Constat**

Ghostline, entrée du 04/11/2026 : `add-slice` décrit une condition en prose et donne la ligne d'un horaire sur une commande qui garde son endpoint, mais aucun template ne génère l'une ni l'autre. `ImportScheduleCondition`, `CollectRealtimeFeedsCondition`, leur enregistrement et les tests des cycles (au démarrage retenu ou envoyé, nuits, reprises) ont été écrits à la main, sur des commandes qui existaient déjà. Le test généré par `--daily-at` ne couvre ni `AlsoAtStart()` ni une condition. Pour tester le cycle au démarrage à 02:00 UTC (la veille à Ottawa), le test déplace `Clock` avec `SetUtcNow` avant le premier usage de `Services`, ce qu'aucun commentaire ne dit.

**Périmètre**

- Nouveau template d'élément `hardslice-condition`, dans `src/HardSlice.Templates/content/`, sur le modèle de `hardslice-command` : `--module` et `--command` obligatoires, refusé (code 105, message qui dit quoi faire) quand la slice ou sa commande n'existe pas, ou quand la condition existe déjà. La condition générée est `internal sealed`, reçoit ce qu'elle lit par son constructeur, et porte un marqueur `TODO(hardslice):` à la place de sa règle. Le test généré utilise `RecurringCommandCycles` et porte un marqueur à la place de l'état qui retient le cycle. `ProgramTests` nomme déjà une condition non enregistrée.
- `hardslice-command --at-start` : refusé sans `--every` ni `--daily-at`. Le test généré lance le cycle au démarrage à `TestInstants.Start`.
- `RecurringCommandCycles(app, configure, start)` : un `DateTimeOffset?` qui fixe l'heure de `Clock` avant que l'application démarre. Le commentaire de la classe le dit.
- `add-slice` : la section des commandes planifiées nomme `hardslice-condition` et `--at-start`, et garde pour l'horaire d'une commande existante sa ligne d'enregistrement, avec le test de ses cycles décrit par `RecurringCommandCycles`. `AGENTS.md` nomme le template dans la puce « Background work », sous 150 lignes.
- `docs/item-templates.md`, README du paquet de templates, `CHANGELOG.md`.

**Cas vérifiés**

- Sonde de `hardslice-condition` dans les groupes des templates d'éléments, dans les deux variantes : sur une commande générée avec `--every`, puis sur une commande avec endpoint à laquelle la sonde ajoute un horaire. Refus d'une slice inconnue, d'une condition qui existe, marqueurs listés par `check`, puis complétion de référence verte.
- `--at-start` avec `--every` et avec `--daily-at` : test du cycle au démarrage vert après complétion. Refus sans horaire.
- `RecurringCommandTests` et les tests qui utilisent `RecurringCommandCycles` sans `start`, inchangés et verts.
- `TemplatePackageTests` : le nouveau template dans le paquet, la consigne dans `add-slice`, copie `.agents/` comprise.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only sample-items,blank-items
```

Contournement que Ghostline peut retirer : aucun à retirer, ses conditions écrites à la main ont la forme générée. Son test du cycle au démarrage peut passer `start` au lieu de `SetUtcNow`.

### PB-095 : Fusionner à trois voies dans `dotnet hardslice upgrade`, depuis la génération enregistrée dans la solution

Statut : à faire, après PB-090 à PB-094. Ferme l'entrée « AGENTS.md mixes the text of the template and the text of the application » et la suggestion « know which files the application never changed » de l'entrée « first run of dotnet hardslice upgrade » (arbitrage 9, option A). Retire de la section 9 la fusion à trois voies.

**Résultat observable**

Une solution générée garde dans `.hardslice/generation/` les fichiers de l'application tels que le template les a générés. `dotnet hardslice upgrade` s'en sert de base : un fichier que l'application n'a pas changé est mis à jour d'office, un fichier que l'application et le template ont changés est fusionné à trois voies, un conflit est écrit dans le fichier avec les marqueurs de git, et la base est remplacée par la nouvelle génération. Sur une solution sans base, `upgrade` fait ce que fait PB-086, puis enregistre la base pour la mise à niveau suivante.

**Constat**

Ghostline, entrées du 04/11/2026 : la première vraie mise à niveau (d78ba83) a pris environ 15 min jusqu'au check vert, dont un tiers sur `AGENTS.md`. Les diffs vont de la solution vers le template : environ 45 des 119 lignes du diff d'`AGENTS.md` étaient les parties de Ghostline (carte des modules, clé d'administration, deux paragraphes de « Running the API », section « Ghostline »), qu'un diff appliqué tel quel supprime. 5 des 10 fichiers listés (`RecurringCommand.cs`, `AppDbContext.cs`, `ProgramTests.cs`, `RecurringCommandTests.cs`, `RecurringCommandCycles.cs`) n'avaient aucun changement de l'application, ce que seul `git log` a montré. `Program.cs` et `AppFactory.cs` auraient fusionné sans conflit. Une base restaurée depuis NuGet.org est impossible tant que rien n'est publié, et toutes les reconstructions portent le même numéro.

**Périmètre**

- La base : `.hardslice/generation/<chemin>`, pour chaque fichier que le template génère et que l'application possède (tout sauf `ProtectedFiles` et `docs/rules/`), sous le nom et la variante de la solution. Elle ne contient aucun fichier du kit agents, pour qu'aucun CLI ne lise une skill, un hook ou une consigne en double. Le dossier entre dans la liste de `ProtectedFiles` : un agent ne l'édite jamais, et la CI générée demande le libellé `hardslice-approved` pour un changement, comme pour les autres fichiers que `upgrade` écrit. Aucun projet ne compile ses fichiers, et `check` ne cherche les marqueurs `TODO(hardslice):` que dans `src/` et `tests/`.
- Écrite par la génération : une seconde source de `template.json` de `hardslice-api`, cible `.hardslice/generation/`, avec les modificateurs de la variante et les exclusions de la liste protégée. Un test de `TemplatePackageTests` vérifie que ces exclusions suivent `ProtectedFiles`, pour que la liste reste unique (PB-053). Si `dotnet new` ne permet pas d'écrire la base ainsi dans les deux variantes et sous tous les noms de la sonde `names`, le ticket s'arrête avec un blocker qui dit ce qui manque, sans choisir une autre forme.
- `upgrade`, pour chaque fichier de l'application, avec la base `B`, la solution `S` et la nouvelle génération `T` (fins de ligne et `UserSecretsId` normalisés comme aujourd'hui, le `UserSecretsId` de la solution reporté dans `B` et `T`) :
  - `S` égal à `B` : remplacé par `T`, listé « updated, unchanged by the application ».
  - `T` égal à `B` : laissé, non listé.
  - Sinon, `git merge-file` : une fusion propre est écrite et listée « merged », un conflit est écrit avec ses marqueurs (`<<<<<<< solution`, `||||||| generation <version>`, `>>>>>>> template <version>`) et listé « conflicts to resolve ».
  - Fichier supprimé par l'application et changé par le template : listé, laissé supprimé.
  - Fichier de la base que la nouvelle génération n'a plus : supprimé si `S` égal `B`, listé sinon. Un déplacement de PB-086 dont l'ancien fichier est égal à sa base est supprimé et n'est plus listé.
  - Fichier sans base : comportement et diff de PB-086.
  - Puis `.hardslice/generation/` est remplacé par celui de la nouvelle génération.
- Codes de sortie : 0 quand il ne reste ni conflit ni fichier à fusionner à la main (les fichiers fusionnés sont listés, à relire avec `git diff`), 3 sinon, 1 et 2 inchangés. La dernière ligne compte chaque liste.
- `UpgradeCommand` et son commentaire de tête, `Cli.Usage`, `docs/getting-started.md` (« Upgrading a generated solution »), `agent-loop.md` si elle décrit la mise à niveau, README de l'outil et du paquet de templates, `CHANGELOG.md`. `AGENTS.md` du template ne change que si une ligne nomme ce que `upgrade` laisse à fusionner.
- Section 9 : la ligne de la fusion à trois voies part, celle des marqueurs d'`AGENTS.md` reste écartée avec sa raison mise à jour (la base les remplace).
- Hors périmètre : une base restaurée depuis NuGet.org, le format des diffs (PB-096).

**Cas vérifiés**

- `UpgradeCommandTests`, dépôt git réel, génération simulée : chacun des cas du périmètre, une fusion propre de deux changements sur des lignes différentes, un conflit et ses marqueurs, le `UserSecretsId` qui ne crée aucun conflit, une solution sans base puis la base écrite, une seconde mise à niveau qui ne réécrit ni ne liste rien.
- Sonde de mise à niveau du groupe `upgrade`, dans les deux variantes : la sonde actuelle, depuis le template de PB-074 sans base, inchangée et suivie de la vérification que la base est écrite. Une nouvelle sonde packe le template de ce commit, génère la solution, lui ajoute un module, complète `AGENTS.md` et change `Program.cs`, puis packe une copie du template où elle change une ligne d'`AGENTS.md` loin des parties de l'application, une ligne de `Program.cs` et un fichier que la solution n'a pas changé, et lance `upgrade`. Les parties de l'application dans `AGENTS.md` restent, les trois changements du template sont appliqués sans liste à fusionner, code 0, check vert. Un changement du template sur une ligne que la solution a changée finit en conflit, code 3.
- Sonde `names` : la base porte le nom de la solution, sous ses deux formes.
- `TemplatePackageTests` : la seconde source et ses exclusions alignées sur `ProtectedFiles`.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only upgrade,names,sample,blank
```

Contournement que Ghostline peut retirer : le script qui remettait ses parties dans `AGENTS.md` et le `git log` sur chaque fichier listé, à partir de sa deuxième mise à niveau. La première après ce ticket (PB-097) se passe comme avec PB-086 et enregistre la base.

### PB-096 : Écrire les diffs de `dotnet hardslice upgrade` au format de git, et comparer un fichier déplacé à son nouveau chemin

Statut : à faire, après PB-095. Ferme le reste de l'entrée « first run of dotnet hardslice upgrade » (arbitrage 10).

**Résultat observable**

Chaque diff que `upgrade` écrit porte les en-têtes de git (`diff --git a/<path> b/<path>`, `--- a/<path>`, `+++ b/<path>`), et le bloc d'un fichier, coupé de la sortie, s'applique avec `git apply`. Un fichier que le template a peut-être déplacé, et que PB-095 ne supprime pas, est listé avec son diff contre son nouveau chemin, et une ligne dit quand ils ne diffèrent que par la déclaration du namespace.

**Constat**

Ghostline, entrée du 04/11/2026 : les en-têtes `--- <path> (solution)` et `+++ <path> (template 0.3.0-preview.1)` ne sont pas ceux de `git apply`. Un script a découpé les 1 585 lignes de sortie par fichier, puis `patch` a appliqué chaque diff. Les cinq fichiers déplacés de `Shared/Behaviors` vers `Shared/Http` étaient listés sans diff : un `diff` à la main a montré qu'ils ne différaient que par leur namespace.

**Périmètre**

- `UnifiedDiff` et `UpgradeCommand` : les en-têtes de git, sens inchangé (`-` dans la solution, `+` dans le template), la version du template dite une fois dans le titre de la liste et non plus dans chaque en-tête. Un fichier sans fin de ligne finale porte `\ No newline at end of file`, comme git.
- Déplacement : diff de l'ancien fichier contre le nouveau (`diff --git a/<ancien> b/<nouveau>`), et la ligne « differs only by its namespace » quand c'est le cas.
- `Cli.Usage`, `docs/getting-started.md`, README de l'outil, `CHANGELOG.md`.
- Hors périmètre : un dossier de fichiers patch, un autre sens des diffs.

**Cas vérifiés**

- `UnifiedDiffTests` : les en-têtes, la fin de fichier sans saut de ligne, et un diff que `git apply --check` accepte sur un dépôt réel.
- `UpgradeCommandTests` : le diff d'un déplacement, la ligne du namespace seul, un déplacement qui change autre chose.
- Groupe `upgrade` : les sorties attendues mises à jour.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
scripts/test-template.sh --only upgrade
```

Contournement que Ghostline peut retirer : le script qui découpait la sortie et le `diff` à la main des fichiers déplacés.

### PB-097 : Revalider les paquets après PB-090 à PB-096, pour la mise à niveau de Ghostline

Statut : à faire, après PB-090 à PB-096. Comme PB-088 : reconstruction sous `0.3.0-preview.1`, rien n'est publié et aucune étiquette n'est posée.

**Résultat observable**

Les paquets de `artifacts/packages`, reconstruits sur ce commit, portent PB-090 à PB-096, la CI de la branche `ci/pb-097` est verte sur tous les groupes, et le statut de ce ticket donne à Ghostline la procédure de sa mise à niveau, essayée sur un clone jetable.

**Périmètre**

- `CHANGELOG.md` : une ligne par ticket, rien ne manque. README racine, de l'outil et du paquet de templates : `hardslice-condition`, `--at-start`, la base de `upgrade` et sa fusion.
- Paquets reconstruits, `scripts/verify-published.sh 0.3.0-preview.1 --source artifacts/packages`.
- CI de `ci/pb-097` : chaque groupe vert, puis `main`.
- Essai sur un clone jetable de Ghostline (jamais le dépôt lui-même), dans un dossier du bloc-notes, avec le home de `isolate_dotnet_cli_home` et un `NUGET_PACKAGES` à lui, comme PB-088 : purge des caches de `CONTRIBUTING.md`, `dotnet tool restore`, `dotnet hardslice upgrade`. Attendu : sans base, la sortie de PB-086 au format de PB-096, puis `.hardslice/generation/` écrit. Une seconde mise à niveau sur le même clone, le résultat fusionné et commité, ne réécrit ni ne liste rien.
- Statut : la procédure pour Ghostline, les listes vues sur le clone, et les contournements que Ghostline peut retirer d'après les statuts de PB-090 à PB-096.

**Acceptation**

```text
dotnet build --no-restore && dotnet test --no-build
CI de ci/pb-097, chaque groupe vert
scripts/verify-published.sh 0.3.0-preview.1 --source artifacts/packages
```

### Définition de fini

L'incrément est terminé lorsque PB-064 à PB-097 sont satisfaits, que les jobs CI build, template, démonstration et performance sont verts sur `main`, et que les contournements de Ghostline que ces tickets rendent inutiles sont listés dans leurs résumés. Ghostline se met alors à niveau sur le commit de PB-097, comme il l'a fait sur celui de PB-088. PB-051 reste suspendu jusqu'à ce que le mainteneur décide une publication (décision du 04/11/2026).

## 8. Incréments suivants

| Incrément | Résultat produit | Contenu principal | Acceptation globale |
|---|---|---|---|
| **3, Analyseurs v0.2** (terminé, sauf la publication) | Les dérives principales sont détectées par des diagnostics réparables | Détaillé en section 4 (PB-018 à PB-033) : classification par contrats, HS003 à HS011, HS015 et HS019, diagnostics de configuration HS900 à HS902, niveaux d'analyse, `check`, `explain`, performance, préversion NuGet | Définition de fini de la section 4 |
| **4, Boucle agent v0.3** (terminé, sauf la publication) | Un agent génère puis corrige par les chemins officiels | Détaillé en section 5 (PB-034 à PB-051) : templates d'éléments, six skills, fichiers de guidage des quatre agents, hooks après édition et de fin de tâche, HS000, scripts uniques pour hooks, pre-commit et CI, boucles E2E, préversion NuGet | Une slice est générée, complétée et vérifiée sans recréer manuellement sa structure, et les quatre agents ciblés disposent d'un adaptateur natif ou du fallback documenté |
| **5, Benchmark v0.4** | L'effet des garde-fous est mesuré et publié | Configurations A/B/C, tâches Bookings, runner, répétitions, coûts et qualité | Protocole, prompts, versions, résultats bruts et analyse sont publiables et reproductibles |
| **6, Adoption v0.5** | Une équipe ajoute HardSlice sans corriger toute sa dette | `init`, `map`, conventions, baseline, cliquet, `report`, `verify`, mode couches applicable | Les violations existantes sont baselinées, une nouvelle violation est bloquée et une correction réduit la baseline |
| **7, Comparaison v0.6** | Les architectures couches et slices sont comparées | Applications équivalentes, mêmes tâches, agents, modèles et budgets | Les conclusions distinguent l'effet de l'architecture de celui des garde-fous |
| **8, Distribution v1.0** | HardSlice devient un produit open source installable et maintenable | Paquets stables, documentation, site, CI de publication, compatibilité, sécurité et contribution, options de template `--aspire` et SQL Server | Un utilisateur externe réussit les parcours greenfield et adoption depuis la documentation publique |

## 9. Différé, mais non abandonné

| Élément | Incrément prévu |
|---|---:|
| Catalogue initial complet | 3, livré |
| Templates d'éléments, six skills et hooks | 4, livré |
| Claude Code, Codex, GitHub Copilot et Gemini CLI | 4, livré : adaptateur natif pour Claude Code, Codex et Copilot CLI, fallback `AGENTS.md` et vérification pour Gemini CLI |
| Benchmark statistique A/B/C | 5 |
| `init`, `map`, baseline, cliquet, `report` et `verify` | 6 |
| Mode couches complet | 6 et 7 |
| Benchmark couches contre slices | 7 |
| Paquets stables et site public | 8 |
| SQL Server en option et orchestration Aspire (`--aspire`) | 8 |
| Contrat de données de référence immuables hors `IEntity` (HS902), vu par Ghostline | à arbitrer après PB-066 |
| Effet externe d'une commande après la validation de sa transaction (boîte d'envoi), vu par Ghostline | à arbitrer après PB-063 |
| Template d'un service hébergé que ne déclenche pas une horloge (consommer une file, par exemple), et horloge de test générique qui expose ses minuteries (`WaitingClock`, vu par Ghostline) | à arbitrer sur un cas réel. L'heure quotidienne et les reprises passent par le service des commandes récurrentes depuis PB-082, dont `RecurringCommandCycles` donne l'instant attendu |
| Aide d'écriture en masse et verrou consultatif dans `Shared/Persistence` | écarté par l'arbitrage de la section 7, documenté par PB-069 et PB-072 |
| Règle qui exige qu'un endpoint d'écriture déclare son autorisation (`RequireAuthorization` ou `AllowAnonymous`), vue par Ghostline | à arbitrer après PB-081 (arbitrage 2) |
| Skill `add-authorization` | écarté par l'arbitrage 3 : la consigne va dans `add-slice` (PB-081) et `HS015.md` (PB-079) |
| HS003 étendue, ou une nouvelle règle, pour les services hébergés d'une slice | écarté par l'arbitrage 5 : HS006 étendue par PB-084 |
| Skill `reshape-entity` ou option `--reshape` de `hardslice-entity` | écarté par l'arbitrage 6 : documenté dans `add-aggregate` par PB-085 |
| Marqueurs des parties de l'application dans `AGENTS.md` | écarté par l'arbitrage 9 : la base que PB-095 enregistre dans la solution les garde par la fusion à trois voies |
| Base de la fusion de `dotnet hardslice upgrade` restaurée depuis NuGet.org | écarté par l'arbitrage 9 : la base est enregistrée dans la solution (PB-095), ce qui marche aussi sous une version reconstruite |
| Mise à niveau d'une solution générée par une construction locale non publiée | couverte par PB-086 (option B), qui ne restaure aucune génération de départ, puis par la fusion de PB-095 une fois la base enregistrée |

La recherche utilisateur et la mécanique du benchmark ne sont pas des prérequis aux incréments 1 et 2.

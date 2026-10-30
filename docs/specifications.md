> **Statut : contrat produit approuvé.** Ce document définit le produit HardSlice et son périmètre. Le [plan de livraison](plan.md) en organise le séquençage et le [backlog produit](product-backlog.md) décrit le travail actif. Aucun de ces documents ne peut supprimer ou rendre conditionnel un livrable ci-dessous sans décision explicite du mainteneur.

# HardSlice : spécifications du kit d'architecture .NET pour agents IA

Statut : spécification produit v2.2, 30 octobre 2026. Le contenu fonctionnel provient de la v2 du 27 octobre 2026, complétée par les décisions de la v2.2 sur les analyseurs v0.2.

Historique :
- v1 : architecture en couches.
- v2, sous le nom provisoire « Keel » : le template passe en **vertical slices avec un domaine riche**. Nouvelles règles HS016 à HS018, outil d'adoption compatible couches et slices, comparaison couches contre slices dans le benchmark.
- v2.1 : « Keel » devient **HardSlice** ; les identifiants `KEELxxx` deviennent `HSxxx` et la CLI canonique devient `dotnet hardslice`.
- v2.2 (préparation de v0.2) : classification des types par les contrats applicatifs `IEntity` et `IAggregateRoot` ; HS010 et HS011 deviennent des analyseurs HardSlice au lieu de `BannedApiAnalyzers` ; HS009 est séparée en contrôle structurel (HS009) et convention de nommage non bloquante (HS019) ; diagnostics de configuration HS900 à HS902 ; niveaux d'analyse ; `check` et `explain` livrés dès v0.2.

## 1. En une phrase

Un kit open source qui fait respecter une architecture en vertical slices avec un domaine riche (DDD/CQRS) aux agents IA de code (Claude Code, Codex, Copilot, Gemini) **par le build lui-même**, pour un nouveau projet comme pour une solution existante, et un benchmark public qui mesure l'effet.

Slogan : « des vertical slices, maintenues propres par le build ».

## 2. Pourquoi

- **Le problème** : un agent lit `AGENTS.md`, puis l'oublie ou le contourne dès que la tâche est longue. Les consignes en prose sont des conseils, pas des garde-fous.
- **Pour Vincent** : crédibilité technique à Ottawa (DDD, CQRS, microservices, IA en production chez Edenred, SideHub). Matière à articles, à talks (meetup .NET Ottawa, ConFoo Montréal) et à conseil.
- **Monétisation** : indirecte (ateliers, accompagnement d'équipes, entretiens). Piste plus tard : un support entreprise pour l'adoption sur des solutions existantes.

## 3. Ce qui existe déjà (vérifié le 27 octobre 2026, à approfondir)

| Existant | Ce qu'il fait | Ce qui manque |
|---|---|---|
| Template Clean Architecture de Milan Jovanović (gratuit, très utilisé) | Skills Claude Code (`/add-feature` qui crée des vertical slices), tests d'architecture des couches, xUnit, Shouldly, NSubstitute, variante Aspire | Centré Claude Code et skills (consultatifs). Nouveau projet seulement |
| Cours et « dotnet claude kit » de Mukesh Murugan | Modèles de `CLAUDE.md`, hooks (`dotnet format` après chaque édition) | Pas de règles d'architecture appliquées par le build (à confirmer) |
| Templates Jason Taylor, Ardalis | Références Clean Architecture | Rien pour les agents |

**Conséquence : un template de plus ne suffit pas.** HardSlice se distingue sur quatre points, et chacun doit rester vrai :

1. **Appliqué par le build, pour tous les agents** : les règles sont des analyseurs Roslyn et des tests. `dotnet build` et `dotnet test` échouent, quel que soit l'agent ou l'humain. Les skills et les hooks accélèrent, ils ne sont pas le garde-fou.
2. **Des erreurs écrites pour un agent** : chaque violation dit quoi corriger, comment, et pointe vers la fiche de la règle. L'agent se corrige seul.
3. **Adoption sur une solution existante**, en couches ou en slices : un outil ajoute le kit à une solution qui existe déjà, avec une base des violations existantes et un cliquet (le nombre de violations ne peut que baisser). C'est là que sont les vrais projets d'entreprise.
4. **Un benchmark public et reproductible** : le même travail donné à plusieurs agents, avec et sans HardSlice, en couches et en slices, avec des chiffres.

## 4. Choix d'architecture : vertical slices et domaine riche

### Pourquoi les slices pour des agents

- **Moins de contexte à charger** : une tâche d'agent est presque toujours « ajoute ou modifie tel cas d'usage ». En couches, c'est 4 projets et 8 fichiers. En slice, c'est un dossier, souvent un fichier.
- **Des changements qui s'ajoutent** : une fonctionnalité crée un nouveau dossier au lieu de modifier des fichiers partagés. Moins de risque de casser le reste, des diffs plus lisibles.
- **Le découpage colle aux tâches** : une tâche correspond à une slice.

L'affirmation « les agents trouvent le contexte plus vite » reste une hypothèse : le benchmark la mesure (section 11).

### Le piège, et ce que HardSlice y oppose

Sans garde-fous, les agents dégradent une architecture en slices plus vite qu'une architecture en couches :

| Dérive | Réponse de HardSlice |
|---|---|
| Duplication : chaque slice réécrit sa version d'une règle métier | Les invariants vivent dans le domaine (HS007, HS008), skill `add-slice` qui part du domaine existant |
| Domaine anémique : la logique glisse dans les handlers | HS007 renforcée : pas de setter public, l'état change par des méthodes du domaine |
| Couplage entre slices : une slice appelle le handler d'une autre | HS016 |
| `Shared` fourre-tout | HS017 |
| Tests éparpillés ou absents | HS014 et HS018 |

### Le modèle retenu

- Le **Domain reste un projet séparé**. C'est la règle la plus précieuse, et le compilateur l'applique gratuitement : le domaine ne peut pas référencer EF Core ni ASP.NET.
- **Tout le reste d'un cas d'usage vit dans sa slice** : commande ou requête, validateur, handler, endpoint, DTO.
- Les slices sont regroupées par **module** (`Features/Bookings/`). Entre modules, on passe par un événement de domaine ou le contrat public du module.
- **Un fichier par slice par défaut**, découpable en plusieurs fichiers dans le même dossier quand elle dépasse environ 200 lignes.
- Les **requêtes** lisent directement la base avec des projections, sans charger d'agrégat. Les **commandes** passent par les agrégats.

## 5. Périmètre

### Dans le périmètre (v1)
- Backend .NET 10, API ASP.NET Core Minimal APIs, monolithe modulaire en vertical slices.
- DDD tactique (agrégats, objets valeur, événements de domaine) et CQRS (commandes et requêtes séparées), sans event sourcing.
- PostgreSQL avec EF Core (SQL Server en option).
- Agents : Claude Code, Codex, GitHub Copilot, Gemini CLI.
- Template : un seul style, les slices, pour ne pas maintenir deux variantes. Kit d'adoption : couches et slices.

### Hors périmètre
- Frontend, microservices distribués, bus de messages, event sourcing (peut-être plus tard).
- Une bibliothèque d'exécution : HardSlice n'ajoute **aucune dépendance au runtime** de l'application. Tout est du code source généré ou des outils de build.
- Un template en couches.

## 6. Les livrables

| Livrable | Forme | Rôle |
|---|---|---|
| Template de solution | `dotnet new hardslice-api` (paquet NuGet de templates) | Démarrer un projet neuf |
| Templates d'éléments | `dotnet new hardslice-command`, `hardslice-query` (une slice complète chacun, endpoint et test compris), `hardslice-module`, `hardslice-aggregate`, `hardslice-event` | Générer le code répétitif de façon déterministe. Les skills les appellent au lieu d'écrire le code à la main |
| Analyseurs Roslyn | Paquet NuGet `HardSlice.Analyzers` dès le premier incrément | Règles appliquées à la compilation, dans l'IDE et en CI, sans copie susceptible de dériver |
| Tests d'architecture | Projet `tests/MyApp.ArchitectureTests` | Règles de dépendances entre projets, modules et slices |
| Kit agents | `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `GEMINI.md`, skills, hooks | Guider les agents et boucler sur les erreurs |
| Script de vérification | `scripts/check.sh` et `scripts/check.ps1` | Point unique appelé par les hooks, le pre-commit git et la CI |
| Outil `dotnet hardslice` | Outil local `HardSlice.Tool`, restauré avec `dotnet tool restore` et épinglé à la version des analyseurs : `check` et `explain` dès v0.2, puis `init`, `map`, `baseline`, `report` et `verify` | Vérifier, expliquer une règle, puis ajouter HardSlice à une solution existante, en couches ou en slices |
| Benchmark | Dossier `bench/` et page de résultats | Mesurer l'effet, nourrir les articles |
| Documentation | Une fiche par règle (`docs/rules/HS001.md`…) et un site simple | Comprendre et corriger |

## 7. Architecture générée

### Structure

```
MyApp.sln
Directory.Build.props        # net10.0, Nullable, TreatWarningsAsErrors, HardSliceAnalysisMode
Directory.Packages.props     # versions centralisées, HardSlice.Analyzers
.editorconfig                # réglages individuels des règles HS
hardslice.json               # style (slices), contrats de classification, dossiers de Shared
.config/dotnet-tools.json    # dotnet-ef (version d'EF Core), HardSlice.Tool (version des analyseurs)
AGENTS.md  CLAUDE.md  GEMINI.md  .github/copilot-instructions.md
.claude/settings.json        # hooks
.claude/skills/              # skills
scripts/check.sh  scripts/check.ps1
src/
  MyApp.Domain/              # aucune dépendance, partagé par les slices
    IEntity.cs  IAggregateRoot.cs  IDomainEvent.cs   # contrats de classification
    Bookings/
      Booking.cs  BookingId.cs  BookingPeriod.cs  Events/BookingConfirmed.cs
  MyApp/                     # hôte web, slices, persistance
    Features/
      Bookings/              # un module
        ConfirmBooking/
          ConfirmBooking.cs  # commande, validateur, handler, endpoint
        CancelBooking/
          CancelBooking.cs
        GetBooking/
          GetBooking.cs      # requête, projection, endpoint
        BookingConfiguration.cs  # mapping EF Core du module, hors de toute slice
        BookingsModule.cs    # enregistrement du module (MapGroup, DI) et contrat public
    Shared/                  # transverse seulement
      Messaging/             # médiateur maison (pas de MediatR)
      Persistence/           # AppDbContext, connexion et migrations
      Behaviors/             # validation, transactions, logs
    Program.cs
tests/
  MyApp.Tests/               # unitaires, même chemin que la slice
    Features/Bookings/ConfirmBooking/ConfirmBookingTests.cs
  MyApp.IntegrationTests/    # Testcontainers PostgreSQL, même chemin que la slice
  MyApp.ArchitectureTests/
docs/rules/
```

### Exemple livré : module « Bookings » (réservation de salles)

Un module complet, que l'agent recopie : un agrégat `Booking` avec un invariant réel (pas deux réservations qui se chevauchent sur la même salle), un objet valeur `BookingPeriod`, un événement `BookingConfirmed`, trois slices (`ConfirmBooking`, `CancelBooking`, `GetBooking`), leurs tests unitaires, d'intégration et d'architecture. Option `--no-sample` pour partir à vide.

### Anatomie d'une slice de commande

```csharp
namespace MyApp.Features.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End) : IRequest<BookingId>;

internal sealed class ConfirmBookingValidator : AbstractValidator<ConfirmBookingCommand> { /* ... */ }

internal sealed class ConfirmBookingHandler(AppDbContext db, TimeProvider clock)
    : IRequestHandler<ConfirmBookingCommand, BookingId>
{
    public async Task<BookingId> Handle(ConfirmBookingCommand command, CancellationToken ct)
    {
        // charge l'agrégat, appelle une méthode du domaine, sauvegarde
    }
}

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, IMediator mediator, CancellationToken ct) =>
            TypedResults.Created($"/bookings/{await mediator.Send(command, ct)}"));
}
```

Tout est `internal` sauf la commande : une autre slice ne peut pas appeler ce handler directement.

### Choix techniques et licences

| Besoin | Choix | Pourquoi |
|---|---|---|
| Médiateur | Code maison d'environ 100 lignes (comme dans SideHub : `IRequest`, `IRequestHandler`, `IPipelineBehavior`, `IMediator`) | MediatR est sous licence commerciale depuis 2025 |
| Mapping | Manuel (méthodes `ToDto`, projections) | AutoMapper est passé sous licence commerciale |
| Validation | FluentValidation | Licence Apache 2.0 |
| Assertions | Shouldly | FluentAssertions 8 est sous licence commerciale |
| Tests | xUnit v3, NSubstitute, Testcontainers | Standards libres |
| Persistance | EF Core et Npgsql, `AsNoTracking` et projections pour les requêtes | Une seule techno de données à apprendre |
| Règles d'architecture | ArchUnitNET (tranché par PB-014, cf. section 13) | |
| Tests présents au chemin des slices (HS014, HS018) | `Microsoft.CodeAnalysis.CSharp`, arbre syntaxique des sources de tests (PB-015) | Licence MIT ; ne dépend pas d'assemblies de tests construits ou périmés |
| Horloge et appels bloquants (HS010, HS011) | Analyseurs HardSlice ciblés | Décision du 30/10/2026, qui remplace `BannedApiAnalyzers` : il produirait `RS0030` pour les deux règles, qu'on ne pourrait ni configurer, ni supprimer, ni compter séparément. Aucun autre analyseur ne signale les mêmes appels |
| Classification des types (entités, agrégats, événements, commandes, requêtes, handlers) | Interfaces de l'application, déclarées dans `hardslice.json` | Aucune dépendance runtime à HardSlice ; reconnaissance par symbole, jamais par heuristique de forme (cf. section 8) |
| Orchestration locale | Aspire en option (`--aspire`) | |

Les licences sont à revérifier au moment du développement.

## 8. Le catalogue de règles

Chaque règle a un identifiant Roslyn propre, une sévérité par défaut (modifiable dans `.editorconfig`), un mécanisme et une fiche `docs/rules/HSxxx.md` (pourquoi, mauvais exemple, bon exemple, contournements interdits, limites de détection, skill à utiliser dès qu'elle existe). La colonne « Style » dit si la règle s'applique aux slices, aux couches (adoption d'une solution existante) ou aux deux.

| Id | Règle | Mécanisme | Style |
|---|---|---|---|
| HS000 | Toute suppression d'une règle HS porte un commentaire `// hardslice-justification: …` | Analyseur | Les deux |
| HS001 | Le Domain ne dépend d'aucun autre projet ni d'EF Core, d'ASP.NET Core ou de la DI | Test d'architecture | Les deux |
| HS002 | Couches : Application ne dépend ni d'Infrastructure ni d'Api, ni d'EF Core, ni de `HttpContext` | Test d'architecture | Couches |
| HS003 | Un endpoint n'accède jamais directement à la base : il envoie une commande ou une requête | Analyseur | Les deux |
| HS004 | Nommage et emplacement : une slice `Xxx` est dans `Features/<Module>/Xxx/` et contient `XxxCommand` (ou `XxxQuery`), son handler et son endpoint ; un validateur présent est nommé `XxxValidator` et reste dans la slice (son obligation relève de HS013) | Analyseur | Slices (variante couches dans `hardslice.json`, v0.5) |
| HS005 | Un handler de commande ne renvoie jamais d'entité ni d'agrégat (un identifiant, un DTO ou rien), ni directement, ni dans une enveloppe générique, une collection, un tableau, un tuple ou un DTO | Analyseur | Les deux |
| HS006 | Un handler de requête ne matérialise aucune entité suivie par EF Core, ne réactive ni ne reconfigure le suivi, et n'écrit jamais : ni `SaveChanges`, ni `ExecuteUpdate`/`ExecuteDelete`, ni SQL brut d'écriture. Il lit avec `AsNoTracking` et des projections : l'absence de suivi est garantie dans le handler, sans dépendre du réglage global du contexte | Analyseur | Les deux |
| HS007 | Les entités, racines et entités enfants, n'ont ni `set` ni `init` accessible de l'extérieur, y compris par une interface publique, ni champ public modifiable : l'état change par des méthodes du domaine qui protègent les invariants. **C'est la règle qui empêche le domaine anémique** | Analyseur | Les deux |
| HS008 | Un agrégat référence un autre agrégat par son identifiant, jamais par une propriété de navigation, une collection ou depuis une de ses entités enfants. Les références à ses propres entités enfants restent permises | Analyseur | Les deux |
| HS009 | Les événements de domaine (`IDomainEvent`) sont des `record` | Analyseur | Les deux |
| HS010 | Pas de `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now` ni `DateTimeOffset.UtcNow` dans le domaine et les slices : utiliser `TimeProvider` | Analyseur | Les deux |
| HS011 | Pas de `.Result`, `.Wait()`, `GetAwaiter().GetResult()` sur une tâche, ni `async void` (méthodes et lambdas converties en délégués `void`) | Analyseur | Les deux |
| HS012 | Un module n'utilise pas les types d'un autre module : il passe par un événement de domaine ou le contrat public du module (`XxxModule`) | Test d'architecture | Les deux |
| HS013 | Toute commande qui a des paramètres a un validateur | Test d'architecture | Les deux |
| HS014 | Tout handler de commande a des tests | Test d'architecture | Les deux |
| HS015 | L'endpoint d'une slice est dans la slice, s'enregistre dans le `MapGroup` du module et renvoie des `TypedResults` | Analyseur | Slices |
| **HS016** | **Une slice n'utilise aucun type d'une autre slice** (ni son handler, ni sa commande, ni son DTO). Pour partager : le domaine, un événement ou le contrat du module. Les handlers, validateurs et endpoints sont `internal` | Test d'architecture et analyseur | Slices |
| **HS017** | **`Shared` reste transverse** : il ne dépend jamais de `Features` ni d'un module, et ne contient que les dossiers autorisés dans `hardslice.json` (par défaut `Messaging`, `Persistence`, `Behaviors`) | Test d'architecture | Slices |
| **HS018** | **Les tests reflètent les slices** : les tests de `Features/<Module>/<Slice>/` sont au même chemin dans `tests/`. Une slice sans dossier de tests est signalée | Test d'architecture | Slices |
| HS019 | Les événements de domaine sont nommés au passé (`BookingConfirmed`). Convention de nommage, jamais bloquante : suggestion | Analyseur | Les deux |

### Classification des types

Les règles du domaine et de la messagerie ne devinent pas le rôle d'un type. Le Domain généré déclare `IEntity` et `IAggregateRoot : IEntity`, à côté d'`IDomainEvent`, et `hardslice.json` déclare ces contrats ainsi que `ICommand<T>`, `IQuery<T>` et `IRequestHandler<TRequest, TResponse>`, par nom de métadonnée et assembly déclarant. Ces interfaces appartiennent à l'application : aucun paquet HardSlice n'est requis au runtime.

- Un type appartient à une catégorie s'il implémente le contrat, directement ou par héritage, y compris depuis un assembly référencé. Un type homonyme d'un autre espace de noms ou d'un autre assembly est ignoré.
- Une propriété `Id` ne classe jamais un type.
- La cartographie des slices (espaces de noms de la compilation analysée) et la classification des types (symboles, références comprises) sont deux mécanismes distincts. Une règle sur une déclaration s'exécute dans la compilation qui déclare le type ; une règle sur un usage lit les symboles des assemblies référencés. Aucun analyseur ne charge la solution entière.

Diagnostics de configuration, émis seulement lorsqu'une règle active en dépend :

| Id | Condition |
|---|---|
| HS900 | `hardslice.json` introuvable ou invalide, contrat absent, mal formé ou introuvable dans son assembly déclarant alors que la compilation le référence, contrats incohérents |
| HS901 | Type classé dans deux catégories incompatibles : entité et événement, commande et requête, entité et commande ou requête |
| HS902 | Type de l'assembly du Domain mappé comme entité EF Core (`IEntityTypeConfiguration<T>`, `DbSet<T>`, `ModelBuilder.Entity<T>()`) sans implémenter `IEntity` |

Restent indétectables : une entité mappée par convention, par `Entity(typeof(T))` ou par nom, une entité enfant mappée comme type possédé (`OwnsOne`, `OwnsMany`) qui n'implémente pas `IEntity`, car le mapping ne dit pas si un type possédé est un objet valeur ou une entité : seuls les contrats font autorité, une entité non persistée qui n'implémente aucun contrat, un mapping dans un projet qui n'utilise pas les analyseurs.

### Niveaux d'analyse

La propriété MSBuild `HardSliceAnalysisMode` choisit `Minimum`, `Recommended` (par défaut) ou `All` ; le template utilise `All`. Un niveau active ou désactive des règles, il ne change jamais la sévérité d'une règle active. Un réglage individuel de `.editorconfig` l'emporte sur le niveau. Comme le template traite les warnings comme des erreurs, une règle heuristique comme HS019 n'est jamais au-delà de `suggestion`. La matrice des règles par niveau est tenue dans le [backlog](product-backlog.md) (PB-020).

### Format des messages (le cœur du projet)

Chaque message doit permettre à un agent de corriger sans rien deviner :

```
HS016: 'CancelBookingHandler' (slice Bookings/CancelBooking) uses 'ConfirmBookingHandler'
(slice Bookings/ConfirmBooking). Slices never call each other.
Fix: move the shared rule into the Booking aggregate (MyApp.Domain/Bookings/Booking.cs), or raise a
domain event. Skill: add-slice. Rule: docs/rules/HS016.md
```

Critères : dire ce qui est fautif (le symbole exact et sa slice), la règle en une phrase, la correction attendue, la skill à utiliser, la fiche. Messages en anglais (langue des agents et de la communauté). La mention `Skill:` n'apparaît qu'une fois la skill livrée (v0.3) ; les diagnostics ne fournissent pas de correction automatique (`CodeFixProvider`) en v0.2.

## 9. Le kit agents

### Trois couches

1. **Guider** :
   - `AGENTS.md` est la source unique, courte (moins de 150 lignes) : carte du projet (modules et slices), 7 règles d'or, comment ajouter une slice, comment vérifier, ce qui est interdit.
   - Règle d'or n°1 : « pour un cas d'usage, lis sa slice et l'agrégat qu'elle utilise, rien d'autre ».
   - `CLAUDE.md` importe `AGENTS.md` (`@AGENTS.md`). `GEMINI.md` et `copilot-instructions.md` y renvoient aussi.
2. **Contraindre** : les analyseurs et les tests d'architecture font échouer `dotnet build` et `dotnet test`, pour tous les agents.
3. **Boucler** (Claude Code d'abord) :
   - Après chaque édition d'un `.cs` (`PostToolUse`), un build incrémental. Les erreurs HS reviennent à l'agent (code de sortie 2). Budget : moins de 10 secondes.
   - Avant que l'agent rende la main (`Stop`), `scripts/check.sh` lance le build complet, les tests d'architecture et les tests de la slice touchée. En cas d'échec, l'agent continue de corriger. Budget : moins de 60 secondes.
   - Les autres CLI : hooks équivalents quand ils existent (à vérifier pour Codex, Copilot et Gemini), sinon `AGENTS.md` leur demande de lancer `scripts/check.sh` avant de finir.

### Skills

| Skill | Fait |
|---|---|
| `add-slice` | Crée une slice (commande ou requête) via `dotnet new hardslice-command` ou `hardslice-query`, vérifie d'abord si le domaine porte déjà la règle métier, remplit la logique, écrit le test au même chemin |
| `add-module` | Crée un module (`hardslice-module`) : dossier, `XxxModule.cs`, enregistrement, dossier de domaine |
| `add-aggregate` | Agrégat, identifiant typé, invariants, tests |
| `add-domain-event` | Événement, gestionnaire, et la slice ou le module qui y réagit |
| `extract-to-domain` | Déplace une règle dupliquée dans deux slices vers l'agrégat |
| `fix-architecture-violation` | Lit la fiche de la règle, applique la correction, relance la vérification |

Les skills sont au format Agent Skills (`SKILL.md`). Vérifier quels autres CLI le lisent.

### Garde-fous pour les agents

- `scripts/check.sh` est le seul juge : l'agent n'a pas fini tant qu'il ne passe pas.
- Désactiver une règle (`#pragma warning disable HS…`, `[SuppressMessage]`, modifier `.editorconfig`, `hardslice.json` ou `tests/MyApp.ArchitectureTests`) est interdit sans justification (HS000). Le hook peut aussi bloquer l'édition de ces fichiers.

## 10. Adoption sur une solution existante

La plupart des solutions existantes sont en couches. L'outil doit donc appliquer les règles **aux deux styles**, configurés dans `hardslice.json`.

- `dotnet hardslice init` : détecte le style (couches ou slices), ajoute les analyseurs, `Directory.Build.props`, le kit agents et `scripts/check.sh`. Ne déplace aucun fichier.
- `dotnet hardslice map` : décrit l'architecture existante dans `hardslice.json` si les noms ne suffisent pas (quel projet est le Domain, où sont les modules, les slices ou les couches).
- `dotnet hardslice baseline` : enregistre les violations existantes dans `hardslice.baseline.json`. Elles ne font pas échouer le build. Toute **nouvelle** violation, si.
- **Cliquet** : la CI échoue si le nombre de violations de la base augmente. Quand l'agent en corrige, `dotnet hardslice baseline --update` réduit la base.
- `dotnet hardslice report` : nombre de violations par règle et par projet, à suivre dans le temps.
- Plus tard, piste : une skill `migrate-to-slices` qui déplace un cas d'usage d'une solution en couches vers une slice, un par un.

## 11. Le benchmark

Objectif : des chiffres mesurés, publiés avec les données brutes. Deux questions.

### Question 1 : HardSlice change-t-il quelque chose ?

- **Tâches** : 5 demandes de fonctionnalités de difficulté croissante sur le module Bookings. Exemples : annuler une réservation avec remboursement partiel, lister les salles libres sur une période, ajouter des équipements à une salle, notifier quand une réservation est confirmée, refactoriser la règle de chevauchement.
- **Trois configurations**, toutes en slices :
  - A : même structure de solution, sans kit ni analyseurs.
  - B : avec `AGENTS.md` et les skills, sans analyseurs ni hooks (consignes seules).
  - C : HardSlice complet.

### Question 2 : couches ou vertical slices, avec quelle architecture les agents travaillent-ils le mieux ?

- Mêmes tâches, **HardSlice complet des deux côtés** : la même application en couches (règles en mode couches) et en slices.
- C'est probablement l'article le plus partagé du projet.

### Commun aux deux

- **Agents** : Claude Code, Codex, Copilot CLI, avec les mêmes modèles pour chaque configuration.
- **Répétitions** : chaque combinaison 3 à 5 fois, pour mesurer la variance.
- **Mesures** :
  - violations HS, comptées après coup avec le même outil partout.
  - build vert et tests verts.
  - **fichiers lus et tokens d'entrée** (pour vérifier l'hypothèse du contexte plus rapide).
  - duplication de logique métier entre slices.
  - coût, durée, nombre d'itérations, interventions humaines.
- **Exécution** : des workflows SideHub lancent les runs et collectent les résultats (le benchmark sert aussi de démonstration de SideHub).
- **Transparence** : publier le protocole, les prompts, les résultats bruts et les cas où HardSlice, ou les slices, coûtent plus de tokens ou de temps.

## 12. Plan

Cette table décrit le périmètre produit promis. Le séquençage d'exécution courant et le premier jalon vertical sont détaillés dans [plan.md](plan.md).

| Version | Contenu | Résultat publiable |
|---|---|---|
| v0.1 | Template de solution en slices, module Bookings, tests d'architecture HS001, 012 à 014 et 016 à 018, `AGENTS.md`, `scripts/check.sh` | Repo public |
| v0.2 | Analyseurs HS003 à 011, 015 et 019, classification par contrats et diagnostics de configuration HS900 à 902, niveaux d'analyse, `dotnet hardslice check` et `explain`, messages pour agents, fiches de règles, préversion NuGet | Post « des erreurs de compilation écrites pour les agents » |
| v0.3 | Templates d'éléments, skills, hooks Claude Code, HS000 | Démo vidéo courte |
| v0.4 | Benchmark, question 1 (A/B/C) | Article principal et post LinkedIn chiffré |
| v0.5 | `dotnet hardslice init`, `map`, `baseline`, cliquet et `report`, règles en mode couches (HS002) | Post « adopter sur un projet existant » |
| v0.6 | Benchmark, question 2 (couches contre slices) | Article « couches ou vertical slices pour les agents » |
| v1.0 | Paquets NuGet (templates, analyseurs, outil), site de documentation, CI GitHub Actions et Azure Pipelines | Annonce, talk |

## 13. Questions ouvertes

1. ~~ArchUnitNET ou NetArchTest (maintenance, expressivité, vitesse) ?~~ **Résolu : ArchUnitNET, maintenu, expose la cible de chaque dépendance et permet de nommer le type fautif (évaluation dans PB-014).**
2. ~~Analyseurs dans la solution puis paquet NuGet ?~~ **Résolu : paquet `HardSlice.Analyzers` dès le premier incrément.**
3. Quels hooks existent aujourd'hui pour Codex, Copilot CLI et Gemini CLI ?
4. Quels CLI lisent les skills au format `SKILL.md` ?
5. Seuil de découpage d'une slice en plusieurs fichiers : 200 lignes, ou au nombre de types ?
6. Les modules restent-ils des dossiers d'un même projet, ou deviennent-ils des projets séparés quand la solution grossit ?
7. ~~Où vit la persistance d'un module ?~~ **Résolu : `Shared/Persistence` contient `AppDbContext`, la connexion et les migrations ; chaque configuration EF Core d'agrégat vit à la racine de son module, hors de toute slice.**
8. Temps réel du build incrémental après chaque édition sur une grosse solution : tenir les 10 secondes ?
9. ~~Nom définitif ?~~ **Résolu : HardSlice.** La disponibilité exacte des identifiants NuGet et du domaine reste à vérifier avant publication.
10. Licence du projet : MIT.

## 14. Indicateurs de réussite

- Benchmark publié avec un écart net entre B et C.
- Article couches contre slices publié, avec des chiffres sur les fichiers lus et les tokens.
- Étoiles GitHub et téléchargements NuGet.
- Une équipe externe qui adopte `dotnet hardslice init` sur une solution existante.
- Un talk accepté (meetup .NET Ottawa, ConFoo Montréal).
- Des demandes d'atelier ou de conseil.

## Projet lié

Ghostline Ottawa (page « Ghostline Ottawa : spécifications »), qui peut être construit avec HardSlice pour en être la première vraie démonstration.

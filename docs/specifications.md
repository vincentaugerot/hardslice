# Keel : spécifications du kit d'architecture .NET pour agents IA

Statut : spécification v2, 27 octobre 2026. « Keel » (la quille, ce qui garde le bateau dans l'axe) est un nom provisoire, à vérifier sur NuGet et GitHub. Autres pistes : Ballast, Guardrail.

Historique :
- v1 : architecture en couches.
- v2 : le template passe en **vertical slices avec un domaine riche**. Nouvelles règles KEEL016 à KEEL018, outil d'adoption compatible couches et slices, comparaison couches contre slices dans le benchmark.

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

**Conséquence : un template de plus ne suffit pas.** Keel se distingue sur quatre points, et chacun doit rester vrai :

1. **Appliqué par le build, pour tous les agents** : les règles sont des analyseurs Roslyn et des tests. `dotnet build` et `dotnet test` échouent, quel que soit l'agent ou l'humain. Les skills et les hooks accélèrent, ils ne sont pas le garde-fou.
2. **Des erreurs écrites pour un agent** : chaque violation dit quoi corriger, comment, et pointe vers la fiche de la règle. L'agent se corrige seul.
3. **Adoption sur une solution existante**, en couches ou en slices : un outil ajoute le kit à une solution qui existe déjà, avec une base des violations existantes et un cliquet (le nombre de violations ne peut que baisser). C'est là que sont les vrais projets d'entreprise.
4. **Un benchmark public et reproductible** : le même travail donné à plusieurs agents, avec et sans Keel, en couches et en slices, avec des chiffres.

## 4. Choix d'architecture : vertical slices et domaine riche

### Pourquoi les slices pour des agents

- **Moins de contexte à charger** : une tâche d'agent est presque toujours « ajoute ou modifie tel cas d'usage ». En couches, c'est 4 projets et 8 fichiers. En slice, c'est un dossier, souvent un fichier.
- **Des changements qui s'ajoutent** : une fonctionnalité crée un nouveau dossier au lieu de modifier des fichiers partagés. Moins de risque de casser le reste, des diffs plus lisibles.
- **Le découpage colle aux tâches** : une tâche correspond à une slice.

L'affirmation « les agents trouvent le contexte plus vite » reste une hypothèse : le benchmark la mesure (section 11).

### Le piège, et ce que Keel y oppose

Sans garde-fous, les agents dégradent une architecture en slices plus vite qu'une architecture en couches :

| Dérive | Réponse de Keel |
|---|---|
| Duplication : chaque slice réécrit sa version d'une règle métier | Les invariants vivent dans le domaine (KEEL007, KEEL008), skill `add-slice` qui part du domaine existant |
| Domaine anémique : la logique glisse dans les handlers | KEEL007 renforcée : pas de setter public, l'état change par des méthodes du domaine |
| Couplage entre slices : une slice appelle le handler d'une autre | KEEL016 |
| `Shared` fourre-tout | KEEL017 |
| Tests éparpillés ou absents | KEEL014 et KEEL018 |

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
- Une bibliothèque d'exécution : Keel n'ajoute **aucune dépendance au runtime** de l'application. Tout est du code source généré ou des outils de build.
- Un template en couches.

## 6. Les livrables

| Livrable | Forme | Rôle |
|---|---|---|
| Template de solution | `dotnet new keel-api` (paquet NuGet de templates) | Démarrer un projet neuf |
| Templates d'éléments | `dotnet new keel-command`, `keel-query` (une slice complète chacun, endpoint et test compris), `keel-module`, `keel-aggregate`, `keel-event` | Générer le code répétitif de façon déterministe. Les skills les appellent au lieu d'écrire le code à la main |
| Analyseurs Roslyn | Projet dans la solution en v1 (visible et modifiable), paquet NuGet `Keel.Analyzers` en v1.0 | Règles appliquées à la compilation, dans l'IDE et en CI |
| Tests d'architecture | Projet `tests/MyApp.ArchitectureTests` | Règles de dépendances entre projets, modules et slices |
| Kit agents | `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `GEMINI.md`, skills, hooks | Guider les agents et boucler sur les erreurs |
| Script de vérification | `scripts/check.sh` et `scripts/check.ps1` | Point unique appelé par les hooks, le pre-commit git et la CI |
| Outil d'adoption | `dotnet tool install keel` puis `keel init` et `keel baseline` | Ajouter Keel à une solution existante, en couches ou en slices |
| Benchmark | Dossier `bench/` et page de résultats | Mesurer l'effet, nourrir les articles |
| Documentation | Une fiche par règle (`docs/rules/KEEL001.md`…) et un site simple | Comprendre et corriger |

## 7. Architecture générée

### Structure

```
MyApp.sln
Directory.Build.props        # net10.0, Nullable, TreatWarningsAsErrors, analyseurs
Directory.Packages.props     # versions centralisées
.editorconfig                # sévérités des règles KEEL
keel.json                    # style (slices), modules, chemins
AGENTS.md  CLAUDE.md  GEMINI.md  .github/copilot-instructions.md
.claude/settings.json        # hooks
.claude/skills/              # skills
scripts/check.sh  scripts/check.ps1
src/
  MyApp.Domain/              # aucune dépendance, partagé par les slices
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
        BookingsModule.cs    # enregistrement du module (MapGroup, DI) et contrat public
    Shared/                  # transverse seulement
      Messaging/             # médiateur maison (pas de MediatR)
      Persistence/           # AppDbContext, configurations EF Core
      Behaviors/             # validation, transactions, logs
    Program.cs
  MyApp.Analyzers/           # règles KEEL (v1)
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
| Règles d'architecture | ArchUnitNET ou NetArchTest (à trancher, cf. section 13) | |
| API bannies | `Microsoft.CodeAnalysis.BannedApiAnalyzers` et `BannedSymbols.txt` | Interdire `DateTime.Now`, `.Result`… sans écrire d'analyseur |
| Orchestration locale | Aspire en option (`--aspire`) | |

Les licences sont à revérifier au moment du développement.

## 8. Le catalogue de règles

Chaque règle a un identifiant, une sévérité par défaut (modifiable dans `.editorconfig`), un mécanisme et une fiche `docs/rules/KEELxxx.md` (pourquoi, mauvais exemple, bon exemple, skill à utiliser). La colonne « Style » dit si la règle s'applique aux slices, aux couches (adoption d'une solution existante) ou aux deux.

| Id | Règle | Mécanisme | Style |
|---|---|---|---|
| KEEL000 | Toute suppression d'une règle KEEL porte un commentaire `// keel-justification: …` | Analyseur | Les deux |
| KEEL001 | Le Domain ne dépend d'aucun autre projet ni d'EF Core, d'ASP.NET Core ou de la DI | Test d'architecture | Les deux |
| KEEL002 | Couches : Application ne dépend ni d'Infrastructure ni d'Api, ni d'EF Core, ni de `HttpContext` | Test d'architecture | Couches |
| KEEL003 | Un endpoint n'accède jamais directement à la base : il envoie une commande ou une requête | Analyseur | Les deux |
| KEEL004 | Nommage et emplacement : une slice `Xxx` est dans `Features/<Module>/Xxx/` et contient `XxxCommand` (ou `XxxQuery`), son handler, son validateur et son endpoint | Analyseur | Slices (variante couches dans `keel.json`) |
| KEEL005 | Un handler de commande ne renvoie jamais d'entité ni d'agrégat (un identifiant, un DTO ou rien) | Analyseur | Les deux |
| KEEL006 | Un handler de requête ne charge pas d'agrégat suivi par EF Core et n'appelle jamais `SaveChanges` : il lit avec `AsNoTracking` et des projections | Analyseur | Les deux |
| KEEL007 | Les entités n'ont pas de setter public : l'état change par des méthodes du domaine qui protègent les invariants. **C'est la règle qui empêche le domaine anémique** | Analyseur | Les deux |
| KEEL008 | Un agrégat référence un autre agrégat par son identifiant, jamais par une propriété de navigation | Analyseur | Les deux |
| KEEL009 | Les événements de domaine sont des `record` au passé (`BookingConfirmed`) | Analyseur | Les deux |
| KEEL010 | Pas de `DateTime.Now` ni `DateTime.UtcNow` dans le domaine et les slices : utiliser `TimeProvider` | API bannies | Les deux |
| KEEL011 | Pas de `.Result`, `.Wait()` ni `async void` | API bannies et analyseurs existants | Les deux |
| KEEL012 | Un module n'utilise pas les types d'un autre module : il passe par un événement de domaine ou le contrat public du module (`XxxModule`) | Test d'architecture | Les deux |
| KEEL013 | Toute commande qui a des paramètres a un validateur | Test d'architecture | Les deux |
| KEEL014 | Tout handler de commande a des tests | Test d'architecture | Les deux |
| KEEL015 | L'endpoint d'une slice est dans la slice, s'enregistre dans le `MapGroup` du module et renvoie des `TypedResults` | Analyseur | Slices |
| **KEEL016** | **Une slice n'utilise aucun type d'une autre slice** (ni son handler, ni sa commande, ni son DTO). Pour partager : le domaine, un événement ou le contrat du module. Les handlers, validateurs et endpoints sont `internal` | Test d'architecture et analyseur | Slices |
| **KEEL017** | **`Shared` reste transverse** : il ne dépend jamais de `Features` ni d'un module, et ne contient que les dossiers autorisés dans `keel.json` (par défaut `Messaging`, `Persistence`, `Behaviors`) | Test d'architecture | Slices |
| **KEEL018** | **Les tests reflètent les slices** : les tests de `Features/<Module>/<Slice>/` sont au même chemin dans `tests/`. Une slice sans dossier de tests est signalée | Test d'architecture | Slices |

### Format des messages (le cœur du projet)

Chaque message doit permettre à un agent de corriger sans rien deviner :

```
KEEL016: 'CancelBookingHandler' (slice Bookings/CancelBooking) uses 'ConfirmBookingHandler'
(slice Bookings/ConfirmBooking). Slices never call each other.
Fix: move the shared rule into the Booking aggregate (MyApp.Domain/Bookings/Booking.cs), or raise a
domain event. Skill: add-slice. Rule: docs/rules/KEEL016.md
```

Critères : dire ce qui est fautif (le symbole exact et sa slice), la règle en une phrase, la correction attendue, la skill à utiliser, la fiche. Messages en anglais (langue des agents et de la communauté).

## 9. Le kit agents

### Trois couches

1. **Guider** :
   - `AGENTS.md` est la source unique, courte (moins de 150 lignes) : carte du projet (modules et slices), 7 règles d'or, comment ajouter une slice, comment vérifier, ce qui est interdit.
   - Règle d'or n°1 : « pour un cas d'usage, lis sa slice et l'agrégat qu'elle utilise, rien d'autre ».
   - `CLAUDE.md` importe `AGENTS.md` (`@AGENTS.md`). `GEMINI.md` et `copilot-instructions.md` y renvoient aussi.
2. **Contraindre** : les analyseurs et les tests d'architecture font échouer `dotnet build` et `dotnet test`, pour tous les agents.
3. **Boucler** (Claude Code d'abord) :
   - Après chaque édition d'un `.cs` (`PostToolUse`), un build incrémental. Les erreurs KEEL reviennent à l'agent (code de sortie 2). Budget : moins de 10 secondes.
   - Avant que l'agent rende la main (`Stop`), `scripts/check.sh` lance le build complet, les tests d'architecture et les tests de la slice touchée. En cas d'échec, l'agent continue de corriger. Budget : moins de 60 secondes.
   - Les autres CLI : hooks équivalents quand ils existent (à vérifier pour Codex, Copilot et Gemini), sinon `AGENTS.md` leur demande de lancer `scripts/check.sh` avant de finir.

### Skills

| Skill | Fait |
|---|---|
| `add-slice` | Crée une slice (commande ou requête) via `dotnet new keel-command` ou `keel-query`, vérifie d'abord si le domaine porte déjà la règle métier, remplit la logique, écrit le test au même chemin |
| `add-module` | Crée un module (`keel-module`) : dossier, `XxxModule.cs`, enregistrement, dossier de domaine |
| `add-aggregate` | Agrégat, identifiant typé, invariants, tests |
| `add-domain-event` | Événement, gestionnaire, et la slice ou le module qui y réagit |
| `extract-to-domain` | Déplace une règle dupliquée dans deux slices vers l'agrégat |
| `fix-architecture-violation` | Lit la fiche de la règle, applique la correction, relance la vérification |

Les skills sont au format Agent Skills (`SKILL.md`). Vérifier quels autres CLI le lisent.

### Garde-fous pour les agents

- `scripts/check.sh` est le seul juge : l'agent n'a pas fini tant qu'il ne passe pas.
- Désactiver une règle (`#pragma warning disable KEEL…`, `[SuppressMessage]`, modifier `.editorconfig`, `keel.json` ou `tests/MyApp.ArchitectureTests`) est interdit sans justification (KEEL000). Le hook peut aussi bloquer l'édition de ces fichiers.

## 10. Adoption sur une solution existante

La plupart des solutions existantes sont en couches. L'outil doit donc appliquer les règles **aux deux styles**, configurés dans `keel.json`.

- `keel init` : détecte le style (couches ou slices), ajoute les analyseurs, `Directory.Build.props`, le kit agents et `scripts/check.sh`. Ne déplace aucun fichier.
- `keel map` : décrit l'architecture existante dans `keel.json` si les noms ne suffisent pas (quel projet est le Domain, où sont les modules, les slices ou les couches).
- `keel baseline` : enregistre les violations existantes dans `keel.baseline.json`. Elles ne font pas échouer le build. Toute **nouvelle** violation, si.
- **Cliquet** : la CI échoue si le nombre de violations de la base augmente. Quand l'agent en corrige, `keel baseline --update` réduit la base.
- `keel report` : nombre de violations par règle et par projet, à suivre dans le temps.
- Plus tard, piste : une skill `migrate-to-slices` qui déplace un cas d'usage d'une solution en couches vers une slice, un par un.

## 11. Le benchmark

Objectif : des chiffres mesurés, publiés avec les données brutes. Deux questions.

### Question 1 : Keel change-t-il quelque chose ?

- **Tâches** : 5 demandes de fonctionnalités de difficulté croissante sur le module Bookings. Exemples : annuler une réservation avec remboursement partiel, lister les salles libres sur une période, ajouter des équipements à une salle, notifier quand une réservation est confirmée, refactoriser la règle de chevauchement.
- **Trois configurations**, toutes en slices :
  - A : même structure de solution, sans kit ni analyseurs.
  - B : avec `AGENTS.md` et les skills, sans analyseurs ni hooks (consignes seules).
  - C : Keel complet.

### Question 2 : couches ou vertical slices, avec quelle architecture les agents travaillent-ils le mieux ?

- Mêmes tâches, **Keel complet des deux côtés** : la même application en couches (règles en mode couches) et en slices.
- C'est probablement l'article le plus partagé du projet.

### Commun aux deux

- **Agents** : Claude Code, Codex, Copilot CLI, avec les mêmes modèles pour chaque configuration.
- **Répétitions** : chaque combinaison 3 à 5 fois, pour mesurer la variance.
- **Mesures** :
  - violations KEEL, comptées après coup avec le même outil partout.
  - build vert et tests verts.
  - **fichiers lus et tokens d'entrée** (pour vérifier l'hypothèse du contexte plus rapide).
  - duplication de logique métier entre slices.
  - coût, durée, nombre d'itérations, interventions humaines.
- **Exécution** : des workflows SideHub lancent les runs et collectent les résultats (le benchmark sert aussi de démonstration de SideHub).
- **Transparence** : publier le protocole, les prompts, les résultats bruts et les cas où Keel, ou les slices, coûtent plus de tokens ou de temps.

## 12. Plan

| Version | Contenu | Résultat publiable |
|---|---|---|
| v0.1 | Template de solution en slices, module Bookings, tests d'architecture KEEL001, 012 à 014 et 016 à 018, `AGENTS.md`, `scripts/check.sh` | Repo public |
| v0.2 | Analyseurs KEEL003 à 011 et 015, API bannies, messages pour agents, fiches de règles | Post « des erreurs de compilation écrites pour les agents » |
| v0.3 | Templates d'éléments, skills, hooks Claude Code, KEEL000 | Démo vidéo courte |
| v0.4 | Benchmark, question 1 (A/B/C) | Article principal et post LinkedIn chiffré |
| v0.5 | `keel init`, `keel map`, `keel baseline`, cliquet, `keel report`, règles en mode couches (KEEL002) | Post « adopter sur un projet existant » |
| v0.6 | Benchmark, question 2 (couches contre slices) | Article « couches ou vertical slices pour les agents » |
| v1.0 | Paquets NuGet (templates, analyseurs, outil), site de documentation, CI GitHub Actions et Azure Pipelines | Annonce, talk |

## 13. Questions ouvertes

1. ArchUnitNET ou NetArchTest (maintenance, expressivité, vitesse) ?
2. Analyseurs dans la solution (v1) puis paquet NuGet : le bon ordre ?
3. Quels hooks existent aujourd'hui pour Codex, Copilot CLI et Gemini CLI ?
4. Quels CLI lisent les skills au format `SKILL.md` ?
5. Seuil de découpage d'une slice en plusieurs fichiers : 200 lignes, ou au nombre de types ?
6. Les modules restent-ils des dossiers d'un même projet, ou deviennent-ils des projets séparés quand la solution grossit ?
7. Où vit la persistance d'un module (configurations EF Core dans le module ou dans `Shared/Persistence`) ?
8. Temps réel du build incrémental après chaque édition sur une grosse solution : tenir les 10 secondes ?
9. Nom définitif et disponibilité (NuGet, GitHub, domaine).
10. Licence du projet : MIT.

## 14. Indicateurs de réussite

- Benchmark publié avec un écart net entre B et C.
- Article couches contre slices publié, avec des chiffres sur les fichiers lus et les tokens.
- Étoiles GitHub et téléchargements NuGet.
- Une équipe externe qui adopte `keel init` sur une solution existante.
- Un talk accepté (meetup .NET Ottawa, ConFoo Montréal).
- Des demandes d'atelier ou de conseil.

## Projet lié

Ghostline Ottawa (page « Ghostline Ottawa : spécifications »), qui peut être construit avec Keel pour en être la première vraie démonstration.
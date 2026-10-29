# Note persona (UXR-01)

Statut : **version précédemment validée, amendement en attente de revalidation.** La version validée par le mainteneur le 28/10/2026 (D-UXR-01 `DECIDED` dans [phase-0-decisions.md](../execution/phase-0-decisions.md)) est celle du commit `40a2763`. L'amendement A1 du 28/10/2026 (A-05 : relances conditionnelles C1 et C4, règle « indéterminé = non qualifié », remplacement des participants disqualifiés) **n'est pas couvert par D-UXR-01** : il attend D-UXR-01-A1 (`OPEN`). Note interne en français ; questions de présélection destinées aux participants en français et en anglais (D-11).
Source : [plan.md](../plan.md) §3, §10 ; backlog UXR-01.

## 1. Persona prioritaire

Le **lead technique d'une équipe produit .NET de 3 à 15 développeurs** qui maintient une **API ASP.NET Core existante** organisée en **feature folders ou vertical slices** (souvent avec MediatR ou des handlers maison), dont l'équipe **utilise déjà un agent de code**, et qui veut bloquer les **nouvelles** dérives d'architecture sans corriger tout le passé.

Ce que la série d'entretiens cherche à savoir : si ce persona cite **spontanément** une dérive d'architecture due à un agent (règle de codage dans [interview-guide.md](interview-guide.md)). La sélection ne doit donc **pas** exiger ni suggérer ce problème.

## 2. Critères de sélection observables

Chaque critère est vérifié par une question de présélection neutre. Les questions ne relient jamais agents et architecture : l'usage d'outils et l'organisation du code sont demandés séparément, dans des listes à choix qui ne mettent pas les agents en avant.

| # | Critère | Réponse qualifiante |
|---|---|---|
| C1 | Rôle de décision technique sur le code | Lead technique, architecte, ou développeur senior qui fixe les conventions de code |
| C2 | Équipe de 3 à 15 développeurs | 3–5 ou 6–15 |
| C3 | API ASP.NET Core en production | ASP.NET Core (API) ; en production |
| C4 | Organisation par fonctionnalité | Par fonctionnalité / cas d'usage, ou mélange incluant des dossiers par fonctionnalité |
| C5 | Agent de code déjà utilisé dans l'équipe | Au moins un **agent** (ligne de commande ou mode agent de l'IDE) utilisé chaque semaine par au moins un membre ; la seule complétion IA dans l'IDE ne qualifie pas |
| C6 | Implication dans la revue | Au moins une fois par semaine |
| C7 | Équipe produit responsable de l'API | L'équipe développe et fait évoluer ce service elle-même |

Un participant est **qualifié** s'il satisfait C1 à C7, y compris après les relances conditionnelles ci-dessous. Une réponse « mélange » à C4 n'est qualifiante que si la relance C4 confirme qu'une organisation par fonctionnalité existe dans le service cité. **Une réponse indéterminée ne qualifie pas.**

Justification de C3 : le premier cas d'usage est l'adoption sur un **dépôt existant** (§3 du plan) ; « en production » garantit un code existant, avec des utilisateurs et un historique de changements. Aucune durée minimale n'est imposée : un seuil n'aurait pas de fondement dans le plan.

### Questions de présélection

| # | FR | EN | Options (FR / EN) |
|---|---|---|---|
| Q-C1 | Quel est votre rôle sur la base de code principale de votre équipe ? | What is your role on your team's main codebase? | développeur, développeur senior, lead technique, architecte, manager sans code, autre / developer, senior developer, tech lead, architect, non-coding manager, other |
| Q-C2 | Combien de développeurs modifient régulièrement cette base de code ? | How many developers regularly change this codebase? | 1–2, 3–5, 6–15, 16+ |
| Q-C3 | Quelle est la technologie principale de ce service ? Est-il en production ? | What is the main technology of this service? Is it in production? | ASP.NET Core (API), ASP.NET Core (MVC/Razor), autre .NET, autre / ASP.NET Core (API), ASP.NET Core (MVC/Razor), other .NET, other ; oui, non / yes, no |
| Q-C4 | Comment le code de ce service est-il principalement organisé ? | How is this service's code mainly organized? | par couches techniques, par fonctionnalité ou cas d'usage, Clean Architecture en plusieurs projets, mélange, je ne sais pas / by technical layers, by feature or use case, Clean Architecture across several projects, a mix, I don't know |
| Q-C5 | Quels outils l'équipe utilise-t-elle au moins une fois par semaine pour écrire ou modifier du code ? (plusieurs réponses) | Which tools does your team use at least weekly to write or change code? (select all) | IDE, complétion IA dans l'IDE, agent en ligne de commande (Claude Code, Codex, Copilot CLI…), mode agent dans l'IDE (Cursor, Copilot agent…), générateurs de code, aucun outil d'IA, autre / IDE, AI completion in the IDE, command-line agent (Claude Code, Codex, Copilot CLI…), agent mode in the IDE (Cursor, Copilot agent…), code generators, no AI tools, other |
| Q-C6 | À quelle fréquence relisez-vous des pull requests sur ce service ? | How often do you review pull requests on this service? | plusieurs fois par semaine, une fois par semaine, moins souvent, jamais / several times a week, once a week, less often, never |
| Q-C7 | Quelle est la responsabilité de votre équipe vis-à-vis de ce service ? | What is your team's responsibility for this service? | nous le développons et le faisons évoluer, nous fournissons une plateforme ou des outils à d'autres équipes, nous l'auditons ou le conseillons de l'extérieur, autre / we build and evolve it, we provide a platform or tools to other teams, we audit or advise on it from outside, other |

### Relances conditionnelles de présélection (avant l'entretien)

Posées par écrit ou de vive voix, avant la planification, seulement dans les cas indiqués. Comme les questions de présélection, elles ne mentionnent ni les agents ni l'IA.

| Critère | Condition | FR | EN | Qualifie si |
|---|---|---|---|---|
| C1 | Réponse « développeur senior », « autre », ou rôle dont l'autorité sur les conventions n'est pas évidente | Fixez-vous ou tranchez-vous vous-même les conventions de code de l'équipe ? Donnez un exemple récent où vous l'avez fait. | Do you yourself set or decide your team's code conventions? Give a recent example where you did. | Oui, avec un exemple concret où la personne a fixé ou tranché une convention |
| C4 | Réponse « mélange » ou « je ne sais pas » | Dans ce service, existe-t-il des dossiers ou des projets regroupés par fonctionnalité ou par cas d'usage ? Pouvez-vous en citer un ? | In this service, are there folders or projects grouped by feature or use case? Can you name one? | Oui, avec au moins un exemple nommé d'organisation par fonctionnalité |

Règles :
- **Une réponse indéterminée, évasive ou sans exemple ne qualifie pas.** Le doute vaut « non qualifié ».
- **Remplacement** : un candidat disqualifié à la présélection n'est pas planifié, et le recrutement continue jusqu'à obtenir le nombre de participants qualifiés visé (3 pour le jalon de phase 0, puis 5). Un participant disqualifié pendant l'entretien par la vérification du guide (« non qualifié à la vérification ») est exclu des décomptes ≤ 1/3 et ≥ 3/5 et remplacé de la même manière. Le nombre de disqualifications et leur critère (C1 ou C4) sont comptés dans le journal anonymisé, sans autre détail.

## 3. Critères d'exclusion

- Personne qui connaît déjà le plan HardSlice, ses documents ou ses hypothèses.
- Collègue placé sous la responsabilité directe du mainteneur, ou personne dont le mainteneur est le client ou le prestataire actuel (complaisance).
- Auteur ou mainteneur d'un outil concurrent cité dans l'annexe A.2 du plan.
- Projet uniquement greenfield (aucune API en production) : persona secondaire, hors de cette série.
- Équipe plateforme sans responsabilité d'une API produit : persona secondaire, hors de cette série (C7).

Le recrutement surveille aussi la diversité des sources (au moins deux canaux distincts) pour limiter le cercle trop favorable (R-04).

## 4. Personas secondaires écartés pour cette série

| Persona | Raison de l'écart | Quand |
|---|---|---|
| Développeur individuel en greenfield | Visé par E1 (application de référence), pas par le premier cas d'usage | Après la v1 core, si ≥ 2 entretiens ou un partenaire le demandent |
| Équipe plateforme | Persona plus tard (§3) | Après la v1 core |
| Équipe en architecture purement en couches | Profil couches (HS002) seulement sur demande d'un partenaire | Sur demande |

## 5. Hypothèses que la série met à l'épreuve

1. Le persona rencontre des dérives d'architecture dans les PR et les relie de lui-même à l'usage d'agents (signal ≥ 3/5 du §3).
2. Il les détecte aujourd'hui surtout en revue humaine, sans contrôle automatique au build.
3. Sur un dépôt existant, il accepterait un gel de l'existant (baseline) pour bloquer seulement les nouvelles dérives.
4. Une installation en moins de 30 minutes sur son dépôt est un prérequis pour essayer (critère de la phase 4).

Les hypothèses 2 à 4 sont qualitatives et ne changent pas la règle de décision du §3, qui ne repose que sur le signal codé de l'hypothèse 1.

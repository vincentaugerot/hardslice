# Sprint 1 — Phase 0

Statut : proposition, 28/10/2026, à valider par le mainteneur. Rien n'est démarré.
Backlog de référence : [phase-0-backlog.md](phase-0-backlog.md). Les identifiants de tickets, de décisions (D-xx), de risques (R-xx) et d'inputs (M-xx) renvoient à ce document.

---

## 1. Objectif du sprint

> **À la fin du sprint, la note S1 répond aux sept questions Q1 à Q7 par les mesures obligatoires sourcées, et la décision D-S1 consigne un go, un go sous conditions non bloquantes ou un no-go technique sur HS016. Une impossibilité démontrée par le protocole est un résultat négatif mesuré pouvant mener au no-go ; une étape simplement non exécutée rend le sprint partiel.**

Mesure : existence de `docs/spikes/s1-roslyn.md#conclusion` avec sept réponses liées à un artefact, et de l'entrée D-S1 signée dans `docs/execution/phase-0-decisions.md`. Un faux positif connu non résolu, une mesure obligatoire absente ou un budget mesurable dépassé ne peut jamais produire un go.

Pourquoi S1 d'abord : c'est le seul no-go technique de la phase 0 (§10). Son résultat conditionne le gel de S2a, l'acceptation de l'ADR 0001 et le gel des énoncés du pilote. Le résultat lui-même n'est pas présumé : un no-go documenté est un objectif atteint.

En parallèle, et sans faire partie de l'objectif, le sprint **lance les actions humaines à longue latence** (recherche utilisateur, décisions financières), parce que leur délai calendaire est inconnu et qu'elles sont probablement le chemin critique de la phase.

## 2. Hypothèse de capacité (provisoire)

**La capacité réelle est inconnue (D-05, M-03).** Ce sprint est dimensionné sur une hypothèse à remplacer dès que GOV-05 est tranchée :

| Hypothèse provisoire | Valeur |
|---|---|
| Durée du sprint | 2 semaines calendaires |
| Travail technique et rédactionnel focalisé | **5 j-p**, une seule personne (le mainteneur), aidée d'un agent de code |
| Recherche et démarchage | **~0,75 j-p** : 3 h par semaine pendant 2 semaines, en plus des 5 j-p |
| Gouvernance et provisionnement | **~0,25 à 0,5 j-p**, en plus des 5 j-p |
| Autres contributeurs | aucun |

L'hypothèse représente donc **environ 6 à 6,25 j-p de capacité totale**, dont 5 j-p focalisés. L'engagement technique et rédactionnel du §3.1 vaut **≈ 4,3 j-p**, soit une marge focalisée d'environ 0,7 j-p. Cette marge ne prolonge pas la timebox S1 de 3 jours sans décision D-12.

### Ajustement selon la capacité réelle

| Capacité confirmée | Contenu du sprint |
|---|---|
| < 3,25 j-p focalisés | Ne pas promettre la conclusion de S1 : faire GOV-00/GOV-05, puis autant de S1 que la capacité permet et replanifier l'objectif du sprint |
| 3,25 à < 5 j-p focalisés | GOV-00, GOV-05 et S1 seulement ; les travaux UXR ne démarrent que s'ils disposent de leur capacité humaine séparée |
| **5 j-p focalisés + 1 à 1,25 j-p humains (hypothèse)** | **Contenu du §3** |
| 7–8 j-p focalisés | §3 + les deux premiers jours de S4 selon l'allocation approuvée par D-04 **si GOV-03 est décidée** ; sinon ADR-01 à ADR-06 (brouillons). Ajouter la charge humaine du §3.2 |
| ≥ 10 j-p focalisés au total, éventuellement répartis entre deux personnes | §3 + S4 complet en parallèle de S1, si GOV-03 et GOV-02 sont décidées. Ajouter la charge humaine du §3.2 |

## 3. Tickets retenus, dans l'ordre

### 3.1 Engagés

| Ordre | Ticket | Estimation | Owner | Dépend de |
|---|---|---|---|---|
| 1 | GOV-00 — Journal des décisions | 0,1 j | Mainteneur (+ agent) | — |
| 2 | GOV-05 — Capacité et cadence confirmées | < 30 min | Mainteneur | — |
| 3 | GOV-01 — Visibilité avant le premier push décidée et alignée avec le plan | < 30 min | Mainteneur | GOV-00 |
| 4 | UXR-01 — Note persona | 0,25 j | Mainteneur (+ agent) | — |
| 5 | S1-01 — Protocole S1 et choix des solutions (D-06, D-07) | 0,25 j | Mainteneur (+ agent) | GOV-00 |
| 6 | UXR-02 — Guide d'entretien et règle de codage | 0,5 j | Mainteneur | UXR-01 |
| 7 | S1-02 — Détection par opération / symbole, formes 1A | 0,5 j | Mainteneur + agent | S1-01 |
| 8 | S2A-01 — Clés strictement nécessaires à HS016 (non figées) | 0,25 j | Mainteneur + agent | — |
| 9 | S1-03 — Détection entre assemblies | 0,25 j | Mainteneur + agent | S1-02 |
| 10 | S1-05 — `.globalconfig` par niveau et précédence | 0,5 j | Mainteneur + agent | S1-02 |
| 11 | S1-04 — IDE et CLI depuis le paquet local | 0,25 j | Mainteneur | S1-02, S1-05 |
| 12 | S1-06 — Coût sur la grosse solution | 0,5 j | Mainteneur + agent | S1-01, S1-02, S1-03 |
| 13 | S1-07 — Première calibration des opportunités | 0,25 j | Mainteneur | S1-02 |
| 14 | S1-08 — Frontière analyseur / `check` | 0,25 j | Mainteneur | S1-03, S1-05, S1-06 |
| 15 | S1-09 — Conclusion S1 et décision D-S1 | 0,25 j | Mainteneur | S1-02 à S1-08 |
| 16 | DOC-01 — Bannière du spec v2 | 0,05 j | Agent | — |
| | **Total engagé focalisé** | **≈ 4,3 j-p** | | |

### 3.2 Actions humaines à lancer immédiatement (~1 à 1,25 j-p + latence externe)

| Ticket | Charge initiale | Action | Pourquoi maintenant |
|---|---:|---|---|
| GOV-02 | < 1 h | Fixer le **budget maximal du pilote** et sa couverture. Aucun montant n'est proposé. | Bloquant pour la porte ; à décider **avant** de connaître l'estimation de S4-07 (R-12) |
| GOV-03 | < 1 h + provisionnement | Fixer l'enveloppe de dépense de S4, provisionner la clé d'accès hors dépôt | Sans elle, S4 ne peut pas démarrer au sprint 2 |
| GOV-04 | < 30 min | Décider si Codex est envisagé dans la timebox globale S4 de 3 jours | Fixe la répartition de S4 au sprint 2 |
| UXR-03 | ~1 h + suivi | Après UXR-01, préparer canaux et messages ; après UXR-02 et D-13, contacter, présélectionner et planifier | Chemin critique calendaire probable (R-04) |
| UXR-08 | ~2 h + relances | Rédiger puis valider la fiche de rôle ; après D-13, démarcher les premiers candidats | Latence longue ; livrable de phase 0 et condition de la phase 3 |
| UXR-07 | ~2 h + relances | Après D-13, démarcher d'éventuels design partners **qui ne sont pas des participants d'entretien** ; solliciter les participants seulement après leur entretien | Latence longue ; livrable de phase 0 et exigence de la phase 4 |

### 3.3 Opportuniste (non engagé)

- **UXR-04** : si un participant qualifié est disponible pendant le sprint, que UXR-02 et D-13 sont validés et que son consentement est recueilli, l'entretien a lieu. Il n'est pas compté dans l'objectif, car sa date ne dépend pas du mainteneur.
- Si la marge n'est pas consommée par S1 : ADR-01 (gabarit MADR, 0,1 j), puis DOC-02 (0,25 j).

## 4. Dépendances

**Internes au sprint** : S1-01 → S1-02 → {S1-03, S1-05, S1-07} ; S1-05 → S1-04 ; {S1-02, S1-03} → S1-06 ; {S1-03, S1-05, S1-06} → S1-08 ; tout S1 → S1-09. UXR-01 → UXR-02. GOV-00 → GOV-01, S1-01.

**Externes au sprint**
| Dépendance | Nature | Effet si absente |
|---|---|---|
| D-06 : grosse solution OSS compilable avec le SDK épinglé, dépôt en slices pour la calibration | Choix du mainteneur dans S1-01 | S1-06 ou S1-07 « non mesuré » → sprint partiel |
| D-07 : IDE retenu (candidat : VS Code + C# Dev Kit) | Choix du mainteneur | S1-04 limité à la CLI → sprint partiel |
| M-08 : machine de mesure | Poste local par défaut, à noter dans le protocole | Mesures moins reproductibles, à signaler |
| D-12 : politique d'extension de timebox | Décision du mainteneur | Sans elle, S1 s'arrête à 3 j et conclut avec ses inconnues |
| D-13 : emplacement hors dépôt des données de recherche | Décision du mainteneur dans UXR-02 | Bloque tout contact, toute collecte nominative et tout entretien ; seule la préparation des canaux et messages peut avancer |
| Participants, design partners, gardiens | Personnes externes | N'affecte pas l'objectif ; affecte le calendrier de la phase |

## 5. Ce qui peut avancer en parallèle

- **Piste S1** (mainteneur + agent) : S1-01 → S1-09.
- **Piste rédaction** (agent, validation du mainteneur) : UXR-01, UXR-02, S2A-01, DOC-01, et en marge ADR-01.
- **Piste humaine** (mainteneur, capacité du §3.2) : GOV-01..05, UXR-03, UXR-07, UXR-08.
- Dans S1 : S1-03, S1-05 et S1-07 sont indépendants une fois S1-02 fait.

## 6. Preuves attendues à la fin du sprint

| Preuve | Chemin |
|---|---|
| Journal des décisions avec D-01, D-05 (et D-02, D-03, D-04 si décidées), D-06, D-07, D-S1 | `docs/execution/phase-0-decisions.md` |
| Note S1 : protocole, résultats Q1–Q7, frontière, conclusion | `docs/spikes/s1-roslyn.md` |
| Code jetable, fixtures et données brutes de S1 | `spikes/s1-roslyn/`, `spikes/s1-roslyn/results/<date>/` |
| Table des formes, cross-assembly, IDE/CLI, précédence, perf, exposition | `spikes/s1-roslyn/results/<date>/forms.md`, `cross-assembly.md`, `ide-cli.md`, `globalconfig-precedence.md`, `perf.md`, `exposure.md` dans le même dossier |
| Inventaire provisoire des clés S2a | `docs/spikes/s2a-conventions.md#cles` |
| Note persona validée | `docs/research/persona.md` + entrée de validation |
| Guide d'entretien et règle de codage, datés | `docs/research/interview-guide.md` + entrée de validation |
| Compteur de recrutement anonymisé | `docs/research/outreach-log.md` |
| Bannière du spec v2 | diff de `docs/specifications.md` |

Aucune de ces preuves n'existe aujourd'hui.

## 7. Critères de fin de sprint

| Statut | Conditions |
|---|---|
| **Réussi** | D-S1 signée (go, go sous conditions **non bloquantes** ou no-go) ; `s1-roslyn.md` répond à Q1–Q7, chaque mesure obligatoire de phase 0 a été exécutée et chaque réponse est liée à un artefact ; un faux positif connu non résolu ou un budget mesurable dépassé produit obligatoirement un no-go, qui reste un résultat réussi du sprint ; UXR-01 et UXR-02 validés ; UXR-03 lancé après D-13 (premières sollicitations attestées) ; GOV-00, GOV-01 et GOV-05 faits |
| **Partiel** | S1 conclue mais au moins un critère de no-go reste « non mesuré » (par exemple la mesure sur la grosse solution), **ou** la décision D-S1 n'est pas encore signée ; **ou** UXR-01 / UXR-02 non validés ; **ou** aucune sollicitation de recrutement envoyée |
| **Bloqué** | S1 n'a pas pu exécuter ses mesures (aucune solution OSS compilable, SDK ou Roslyn inutilisable, capacité focalisée confirmée sous 3,25 j-p sans replanification) ; ou une décision humaine nécessaire à S1 (D-06) n'a pas été prise |

Les décisions GOV-02, GOV-03, GOV-04 et le nombre d'entretiens planifiés sont **suivis** mais ne conditionnent pas le statut du sprint, car ils ne font pas partie de son objectif S1. Leur charge reste comptée au §3.2 ; leur absence en fin de sprint est signalée comme risque pour le sprint 2.

## 8. Explicitement laissé au sprint suivant

| Élément | Raison |
|---|---|
| S4-01 à S4-08 (et S4-C1, S4-C2 si D-04) | Capacité ; exige GOV-03 ; le résultat de S1 n'en dépend pas |
| S2A-02 à S2A-05 | S2A-02 utilise le prototype S1 ; S2A-05 ne se fige qu'après D-S1 |
| ADR-01 à ADR-07 | Rédaction parallélisable au sprint 2 ; l'ADR 0001 attend D-S1 |
| TASK-01 à TASK-03 | Le gel attend D-S1 et S4-07 |
| REPO-01 à REPO-03 | Faible risque ; REPO-03 exige GOV-01 et l'autorisation de push |
| DOC-02 | Faible risque ; inclut D-16 |
| UXR-04, UXR-05 | Dépendent des disponibilités des participants |
| UXR-06 | Avant 1B (avant 1A si UXR-05 donne ≤ 1/3) |
| EXIT-01 | Fin de phase |

**Pool candidat du sprint 2**, non engagé avant la rétrospective du sprint 1 : S4 complet, S2A-02 à S2A-05, ADR-01 à ADR-06, TASK-01 et TASK-02, REPO-01 à REPO-03, DOC-02 et entretiens au fil des disponibilités. Avec 5 j-p focalisés, la recommandation initiale est S4 (3 j), S2A-02..05 (0,75 j), puis REPO-01..03 et DOC-02 dans la capacité restante ; ADR et TASK basculent au sprint 3 sauf capacité supplémentaire. La phase 0 ne tient probablement pas en deux sprints sous l'hypothèse actuelle (≈ 14–15 j-p au total, §6 du backlog).

## 9. Hors périmètre du sprint (contrôle)

Aucun ticket de ce sprint ne porte sur S2b, S3, HS019, la 3ᵉ règle, la baseline, `verify`, le hook Stop de production, la mini-application, l'implémentation de HS016 ou de `check`, les phases 1A et suivantes, ou les extensions E1 à E5. Le prototype de S1 est jetable et ne constitue pas l'implémentation de 1A.

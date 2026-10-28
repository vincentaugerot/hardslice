# Sprint 1 — Phase 0

Statut : approuvé comme base d'exécution par le mainteneur le 28/10/2026 (attestation A-00 de [phase-0-decisions.md](phase-0-decisions.md)). Cette approbation ne tranche aucune décision D-xx et ne déclare aucun ticket terminé.
Backlog de référence : [phase-0-backlog.md](phase-0-backlog.md). Les identifiants de tickets, de décisions (D-xx), de risques (R-xx) et d'inputs (M-xx) renvoient à ce document.

---

## 1. Objectif du sprint

> **À la fin du sprint, la note S1 répond aux sept questions Q1 à Q7 par les mesures obligatoires sourcées, et la décision D-S1 consigne un go, un go sous conditions non bloquantes ou un no-go technique sur HS016. Une impossibilité démontrée par le protocole est un résultat négatif mesuré pouvant mener au no-go ; une étape simplement non exécutée rend le sprint partiel.**

Mesure : existence de `docs/spikes/s1-roslyn.md#conclusion` avec sept réponses liées à un artefact, et de l'entrée D-S1 signée dans `docs/execution/phase-0-decisions.md`. Un faux positif connu non résolu, une mesure obligatoire absente ou un budget mesurable dépassé ne peut jamais produire un go.

Pourquoi S1 d'abord : c'est le seul no-go technique de la phase 0 (§10). Son résultat conditionne le gel de S2a, l'acceptation de l'ADR 0001 et le gel des énoncés du pilote. Le résultat lui-même n'est pas présumé : un no-go documenté est un objectif atteint.

En parallèle, et sans faire partie de l'objectif, le sprint **lance les actions humaines à longue latence** (recherche utilisateur, décisions financières), parce que leur délai calendaire est inconnu et qu'elles sont probablement le chemin critique de la phase.

## 2. Capacité confirmée (D-05)

Capacité décidée par le mainteneur le 28/10/2026 (D-05, [phase-0-decisions.md](phase-0-decisions.md)). Conversion : **1 j-p = 7 h de travail focalisé du mainteneur** (D-12) ; le temps de l'agent n'est jamais converti en j-p.

| Élément | Valeur |
|---|---|
| Durée du sprint | 2 semaines calendaires |
| Travail technique et rédactionnel focalisé | **35 h = 5 j-p**, une seule personne (le mainteneur), aidée d'un agent de code |
| Recherche et démarchage | **6 h ≈ 0,86 j-p**, en plus des 5 j-p |
| Gouvernance et provisionnement | **3 h 30 = 0,5 j-p**, en plus des 5 j-p |
| Autres contributeurs | aucun |

La capacité totale est donc de **44 h 30 ≈ 6,36 j-p**, dont 5 j-p focalisés. L'engagement technique et rédactionnel du §3.1 vaut **≈ 4,3 j-p**, soit une marge focalisée d'environ 0,7 j-p. Cette marge ne prolonge pas la timebox S1 de 3 jours sans décision D-12.

### Ajustement selon la capacité réelle

| Capacité confirmée | Contenu du sprint |
|---|---|
| < 3,25 j-p focalisés | Ne pas promettre la conclusion de S1 : faire GOV-00/GOV-05, puis autant de S1 que la capacité permet et replanifier l'objectif du sprint |
| 3,25 à < 5 j-p focalisés | GOV-00, GOV-05 et S1 seulement ; les travaux UXR ne démarrent que s'ils disposent de leur capacité humaine séparée |
| **5 j-p focalisés + ≈ 1,36 j-p humains (confirmé par D-05)** | **Contenu du §3** |
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

### 3.2 Actions humaines (capacité humaine ≈ 1,36 j-p, D-05)

Statuts tirés du registre [phase-0-decisions.md](phase-0-decisions.md), qui fait foi. Seules les actions de la première table sont autorisées aujourd'hui ; les autres attendent leur porte.

**Gouvernance**

| Ticket | Statut | Action dans ce sprint |
|---|---|---|
| GOV-02 | D-02 `DEFERRED` jusqu'à la revue de planification du sprint 2, avant D-03, avant S4-01 et avant tout travail ou résultat S4 | Aucune ; aucun calcul ni signal de coût S4 avant D-02 |
| GOV-03 | D-03 `DEFERRED` jusqu'à la même revue du sprint 2, après D-02, avant S4-01 | Aucune |
| GOV-04 | D-04 `DECIDED` (Codex non envisagé en phase 0) | Terminé |

**Actions immédiates autorisées (sans contact externe)**

| Ticket ou prérequis | Charge initiale | Action | Pourquoi maintenant |
|---|---:|---|---|
| UXR-02 | ~30 min | Relire le guide corrigé, fournir le moyen de demande de suppression, valider ou non D-UXR-02 | Conditionne tout contact |
| OP-UXR-01 | ~30 min | Vérifier et attester le stockage de recherche (synchronisation cloud, permissions, sauvegardes) | Conditionne tout contact |
| UXR-03 | ~1 h | Choisir les canaux de recrutement, sans contact | Chemin critique calendaire probable (R-04) |
| UXR-08 | ~30 min | Relire et valider la fiche de rôle du gardien | Préalable au démarchage |
| OP-01 | selon préparation | Préparer et attester le compte macOS dédié (M-08-A1) | Préalable au premier restore OSS de S1-06 et S1-07 |
| PRE-S1-04 | selon installation | Installer VS Code stable et C# Dev Kit stable, relever les versions | Préalable à S1-04 |
| M-10 | selon disponibilités | Identifier un second relecteur indépendant pour les cas de triage ambigus | Évite de bloquer S1-07 |

**Actions conditionnelles (après la porte « premier contact UXR »)**

| Ticket | Charge initiale | Action |
|---|---:|---|
| UXR-03 | ~1 h + suivi | Contacter, présélectionner (C1–C7) et planifier |
| UXR-08 | ~2 h + relances | Démarcher les premiers candidats gardiens |
| UXR-07 | ~2 h + relances | Démarcher d'éventuels design partners **qui ne sont pas des participants d'entretien** ; solliciter les participants seulement après leur entretien |

### 3.3 Opportuniste (non engagé)

- **UXR-04** : si un participant qualifié est disponible pendant le sprint, que la porte « entretien UXR » est ouverte et que son consentement est recueilli, l'entretien a lieu. Il n'est pas compté dans l'objectif, car sa date ne dépend pas du mainteneur.
- Si la marge n'est pas consommée par S1 : ADR-01 (gabarit MADR, 0,1 j), puis DOC-02 (0,25 j).

## 4. Dépendances

**Internes au sprint** : S1-01 → S1-02 → {S1-03, S1-05, S1-07} ; S1-05 → S1-04 ; {S1-02, S1-03} → S1-06 ; {S1-03, S1-05, S1-06} → S1-08 ; tout S1 → S1-09. UXR-01 → UXR-02. GOV-00 → GOV-01, S1-01.

**Externes au sprint**
| Dépendance | Statut (registre) | Effet si absente |
|---|---|---|
| D-06 : grosse solution OSS compilable avec le SDK épinglé, dépôt en slices pour la calibration | `DECIDED` | S1-06 ou S1-07 « non mesuré » → sprint partiel |
| D-07 : IDE retenu ; PRE-S1-04 : IDE installé, versions relevées | D-07 `DECIDED` ; PRE-S1-04 `OPEN` | S1-04 « non mesuré » → sprint partiel |
| M-08, M-08-A1 : machine de mesure, isolation des builds OSS ; OP-01 : attestation | M-08, M-08-A1 `DECIDED` ; OP-01 `OPEN` | Sans OP-01, aucun restore OSS : S1-06 et S1-07 « non mesuré » |
| D-12 : politique d'extension de timebox | `DECIDED` | — |
| D-13 : emplacement hors dépôt des données de recherche ; OP-UXR-01 : attestation | D-13 `DECIDED` ; OP-UXR-01 `OPEN` | Sans OP-UXR-01, aucun contact |
| M-10 : second relecteur du triage S1-07 | `OPEN` | Un cas ambigu ou contesté laisse Q6 « non mesuré » |
| Participants, design partners, gardiens | Personnes externes | N'affecte pas l'objectif ; affecte le calendrier de la phase |

## 5. Ce qui peut avancer en parallèle

- **Piste S1** (mainteneur + agent) : S1-01 → S1-09.
- **Piste rédaction** (agent, validation du mainteneur) : UXR-01, UXR-02, S2A-01, DOC-01, et en marge ADR-01.
- **Piste humaine** (mainteneur, capacité du §3.2) : alignement de la visibilité effective (GOV-01, autorisation GitHub distincte), prérequis OP-01, PRE-S1-04, OP-UXR-01, M-10, puis UXR-03, UXR-07, UXR-08.
- Dans S1 : S1-03, S1-05 et S1-07 sont indépendants une fois S1-02 fait.

## 6. Preuves attendues à la fin du sprint

| Preuve | Chemin |
|---|---|
| Journal des décisions avec D-01, D-05 (et D-02, D-03, D-04 si décidées), D-06, D-07, D-S1 | `docs/execution/phase-0-decisions.md` |
| Note S1 : protocole, résultats Q1–Q7, frontière, conclusion | `docs/spikes/s1-roslyn.md` |
| Code jetable et fixtures de S1 ; fiches et extraits normalisés ; sorties brutes hors git (D-10-A1) | `spikes/s1-roslyn/` ; `spikes/s1-roslyn/results/<RUN_ID>/` (fiches et extraits normalisés seulement) ; sorties brutes exclusivement sous `$HS_ART/<RUN_ID>/`, manifeste privé |
| Table des formes, cross-assembly, IDE/CLI, précédence, perf, exposition | `spikes/s1-roslyn/results/<RUN_ID>/forms.md`, `cross-assembly.md`, `ide-cli.md`, `globalconfig-precedence.md`, `perf.md`, `exposure.md`, chacun dans le dossier de sa tentative |
| Inventaire provisoire des clés S2a | `docs/spikes/s2a-conventions.md#clés` |
| Note persona validée | `docs/research/persona.md` + entrée de validation |
| Guide d'entretien et règle de codage, datés | `docs/research/interview-guide.md` + entrée de validation |
| Compteur de recrutement anonymisé | `docs/research/outreach-log.md` |
| Bannière du spec v2 | diff de `docs/specifications.md` |

État au 28/10/2026 : des **artefacts préparatoires** existent : registre, protocole S1 (`DRAFT`), wrapper `run-logged.sh`, inventaire S2a provisoire, note persona validée, guide d'entretien (`DRAFT`), journal de démarchage à zéro, messages non envoyés, bannière du spec v2. Les **validations sont incomplètes** (V-S1-01, D-UXR-02, OP-01, PRE-S1-04, OP-UXR-01). Aucune mesure n'a été exécutée, aucune donnée brute n'existe et aucune sollicitation n'a été envoyée.

## 7. Critères de fin de sprint

| Statut | Conditions |
|---|---|
| **Réussi** | D-S1 signée (go, go sous conditions **non bloquantes** ou no-go) ; `s1-roslyn.md` répond à Q1–Q7, chaque mesure obligatoire de phase 0 a été exécutée et chaque réponse est liée à un artefact ; un faux positif connu non résolu ou un budget mesurable dépassé produit obligatoirement un no-go, qui reste un résultat réussi du sprint ; UXR-01 et UXR-02 validés ; UXR-03 lancé après l'ouverture de la porte « premier contact UXR » (premières sollicitations attestées) ; GOV-00, GOV-01 et GOV-05 faits |
| **Partiel** | S1 conclue mais au moins un critère de no-go reste « non mesuré » (par exemple la mesure sur la grosse solution), **ou** la décision D-S1 n'est pas encore signée ; **ou** UXR-01 / UXR-02 non validés ; **ou** aucune sollicitation de recrutement envoyée |
| **Bloqué** | S1 n'a pas pu exécuter ses mesures (aucune solution OSS compilable, SDK ou Roslyn inutilisable, capacité focalisée confirmée sous 3,25 j-p sans replanification) ; ou une décision humaine nécessaire à S1 (D-06) n'a pas été prise |

GOV-02 et GOV-03 sont `DEFERRED` jusqu'au sprint 2 et GOV-04 est décidé (registre). Le nombre d'entretiens planifiés est **suivi** mais ne conditionne pas le statut du sprint, car il ne fait pas partie de son objectif S1.

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
